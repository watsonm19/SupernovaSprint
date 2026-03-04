// ═════════════════════════════════════════════════════════════════════════════
//  AstronautAnimatorBuilder.cs                                        [Editor]
//  Generates the AstronautController.controller asset with all states,
//  transitions, and parameters wired up for SupernovaSprintController.
//
//  Run via: Supernova Sprint → Build Astronaut Animator
//  Output:  Assets/Animation/AstronautController.controller
//
//  After running:
//    1. Assign the generated controller to the Animator on the Visual child.
//    2. The PlayerAnimator component drives Speed, IsGrounded, IsHoming.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class AstronautAnimatorBuilder
{
    const string FBX_PATH        = "Assets/Stylized_Astronaut/Character/Astronaut.fbx";
    const string OUTPUT_PATH     = "Assets/Animation/AstronautController.controller";
    const float  RUN_THRESHOLD   = 25f;   // Matches PlayerAnimator.runThreshold
    const float  WALK_THRESHOLD  = 0.5f;  // Below this = Idle

    [MenuItem("Supernova Sprint/Build Astronaut Animator", priority = 10)]
    public static void Build()
    {
        // ── Load clips from FBX ───────────────────────────────────────────────
        var clips = AssetDatabase.LoadAllAssetsAtPath(FBX_PATH)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToDictionary(c => c.name);

        string[] required = { "Idle", "Walk", "Run", "Jump_start", "Float", "Flip" };
        foreach (string name in required)
        {
            if (!clips.ContainsKey(name))
            {
                Debug.LogError($"[AstronautAnimatorBuilder] Clip '{name}' not found in {FBX_PATH}. " +
                               "Check the FBX animation clip names and update the required list.");
                return;
            }
        }

        // ── Ensure output folder exists ───────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Animation"))
            AssetDatabase.CreateFolder("Assets", "Animation");

        // ── Create controller ─────────────────────────────────────────────────
        var controller = AnimatorController.CreateAnimatorControllerAtPath(OUTPUT_PATH);

        // ── Parameters ───────────────────────────────────────────────────────
        controller.AddParameter("Speed",      AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsHoming",   AnimatorControllerParameterType.Bool);

        // ── States ────────────────────────────────────────────────────────────
        var sm = controller.layers[0].stateMachine;

        var idle      = AddState(sm, "Idle",       clips["Idle"]);
        var walk      = AddState(sm, "Walk",       clips["Walk"]);
        var run       = AddState(sm, "Run",        clips["Run"]);
        var jumpStart = AddState(sm, "JumpStart",  clips["Jump_start"]);
        var airFloat  = AddState(sm, "Float",      clips["Float"]);
        var flip      = AddState(sm, "Flip",       clips["Flip"]);

        sm.defaultState = idle;

        // ── Transitions ───────────────────────────────────────────────────────
        //
        //  Convention:
        //    hasExitTime = false  — transition fires immediately when conditions are met
        //    duration    = 0.1   — short cross-fade for smooth blending
        //
        //  Grounded locomotion (Idle ↔ Walk ↔ Run)
        Transition(idle, walk,  0.1f, false).AddCondition(AnimatorConditionMode.Greater, WALK_THRESHOLD, "Speed");
        Transition(walk, idle,  0.1f, false).AddCondition(AnimatorConditionMode.Less,    WALK_THRESHOLD, "Speed");
        Transition(walk, run,   0.1f, false).AddCondition(AnimatorConditionMode.Greater, RUN_THRESHOLD,  "Speed");
        Transition(run,  walk,  0.1f, false).AddCondition(AnimatorConditionMode.Less,    RUN_THRESHOLD,  "Speed");

        //  Grounded → airborne (all three grounded states → JumpStart)
        foreach (var groundedState in new[] { idle, walk, run })
        {
            var t = Transition(groundedState, jumpStart, 0.05f, false);
            t.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");
        }

        //  JumpStart → Float (plays the full jump_start clip, then blends to float)
        var jumpToFloat = Transition(jumpStart, airFloat, 0.1f, true);
        jumpToFloat.exitTime = 1f;

        //  Float → grounded locomotion
        var floatToIdle = Transition(airFloat, idle, 0.15f, false);
        floatToIdle.AddCondition(AnimatorConditionMode.If,      0f,             "IsGrounded");
        floatToIdle.AddCondition(AnimatorConditionMode.Less,    WALK_THRESHOLD, "Speed");

        var floatToWalk = Transition(airFloat, walk, 0.15f, false);
        floatToWalk.AddCondition(AnimatorConditionMode.If,      0f,            "IsGrounded");
        floatToWalk.AddCondition(AnimatorConditionMode.Greater, WALK_THRESHOLD, "Speed");
        floatToWalk.AddCondition(AnimatorConditionMode.Less,    RUN_THRESHOLD,  "Speed");

        var floatToRun = Transition(airFloat, run, 0.15f, false);
        floatToRun.AddCondition(AnimatorConditionMode.If,      0f,            "IsGrounded");
        floatToRun.AddCondition(AnimatorConditionMode.Greater, RUN_THRESHOLD, "Speed");

        //  Any State → Flip (homing attack — highest priority)
        var anyToFlip = sm.AddAnyStateTransition(flip);
        anyToFlip.hasExitTime  = false;
        anyToFlip.duration     = 0.05f;
        anyToFlip.canTransitionToSelf = false;
        anyToFlip.AddCondition(AnimatorConditionMode.If, 0f, "IsHoming");

        //  Flip → Float (plays full flip clip, then returns to air)
        var flipToFloat = Transition(flip, airFloat, 0.1f, true);
        flipToFloat.exitTime = 1f;

        // ── Save ─────────────────────────────────────────────────────────────
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[AstronautAnimatorBuilder] Controller saved to {OUTPUT_PATH}. " +
                  "Assign it to the Animator on your Visual child.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        var state  = sm.AddState(name);
        state.motion = clip;
        return state;
    }

    static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to,
                                               float duration, bool hasExitTime)
    {
        var t          = from.AddTransition(to);
        t.hasExitTime  = hasExitTime;
        t.duration     = duration;
        return t;
    }
}
