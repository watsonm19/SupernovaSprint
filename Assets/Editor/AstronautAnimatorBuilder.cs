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
using UnityEditor.SceneManagement;
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

        string[] required = { "Idle", "Walk", "Run", "Jump_start", "Jump_loop", "Float", "Flip" };
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
        controller.AddParameter("Speed",        AnimatorControllerParameterType.Float);
        controller.AddParameter("IsGrounded",  AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsHoming",    AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSlamming",  AnimatorControllerParameterType.Bool);
        controller.AddParameter("ForceBoost",  AnimatorControllerParameterType.Trigger);

        // ── States ────────────────────────────────────────────────────────────
        var sm = controller.layers[0].stateMachine;

        var idle        = AddState(sm, "Idle",         clips["Idle"],       1.5f);
        var walk        = AddState(sm, "Walk",         clips["Walk"],       1.75f);
        var run         = AddState(sm, "Run",          clips["Run"],        3.5f);
        var jumpStart   = AddState(sm, "JumpStart",    clips["Jump_start"], 2.1f);
        var airFloat    = AddState(sm, "Float",        clips["Jump_loop"],  1.25f);
        var gravitySlam = AddState(sm, "GravitySlam",  clips["Idle"],       1.5f);
        var forceBoost  = AddState(sm, "ForceBoost",   clips["Float"],      9f);
        var flip        = AddState(sm, "Flip",         clips["Flip"],       1f);

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

        //  Float → GravitySlam (downward slam phase)
        var floatToSlam = Transition(airFloat, gravitySlam, 0.1f, false);
        floatToSlam.AddCondition(AnimatorConditionMode.If, 0f, "IsSlamming");

        //  GravitySlam → Float (slam cancelled mid-air)
        var slamToFloat = Transition(gravitySlam, airFloat, 0.1f, false);
        slamToFloat.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSlamming");
        slamToFloat.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");

        //  GravitySlam → grounded locomotion (on impact)
        var slamToIdle = Transition(gravitySlam, idle, 0.15f, false);
        slamToIdle.AddCondition(AnimatorConditionMode.If,      0f,             "IsGrounded");
        slamToIdle.AddCondition(AnimatorConditionMode.Less,    WALK_THRESHOLD, "Speed");

        var slamToWalk = Transition(gravitySlam, walk, 0.15f, false);
        slamToWalk.AddCondition(AnimatorConditionMode.If,      0f,             "IsGrounded");
        slamToWalk.AddCondition(AnimatorConditionMode.Greater, WALK_THRESHOLD, "Speed");
        slamToWalk.AddCondition(AnimatorConditionMode.Less,    RUN_THRESHOLD,  "Speed");

        var slamToRun = Transition(gravitySlam, run, 0.15f, false);
        slamToRun.AddCondition(AnimatorConditionMode.If,      0f,             "IsGrounded");
        slamToRun.AddCondition(AnimatorConditionMode.Greater, RUN_THRESHOLD,  "Speed");

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

        //  Any State → ForceBoost (trigger-based, one-shot)
        var anyToForceBoost = sm.AddAnyStateTransition(forceBoost);
        anyToForceBoost.hasExitTime        = false;
        anyToForceBoost.duration           = 0.05f;
        anyToForceBoost.canTransitionToSelf = false;
        anyToForceBoost.AddCondition(AnimatorConditionMode.If, 0f, "ForceBoost");

        //  ForceBoost → Float (returns to air loop at 80% of clip while still airborne)
        var forceBoostToFloat = Transition(forceBoost, airFloat, 0.15f, true);
        forceBoostToFloat.exitTime = 0.8f;
        forceBoostToFloat.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsGrounded");

        //  ForceBoost → grounded locomotion (if player lands before clip finishes)
        var forceBoostToIdle = Transition(forceBoost, idle, 0.15f, false);
        forceBoostToIdle.AddCondition(AnimatorConditionMode.If,      0f,             "IsGrounded");
        forceBoostToIdle.AddCondition(AnimatorConditionMode.Less,    WALK_THRESHOLD, "Speed");

        var forceBoostToWalk = Transition(forceBoost, walk, 0.15f, false);
        forceBoostToWalk.AddCondition(AnimatorConditionMode.If,      0f,             "IsGrounded");
        forceBoostToWalk.AddCondition(AnimatorConditionMode.Greater, WALK_THRESHOLD, "Speed");
        forceBoostToWalk.AddCondition(AnimatorConditionMode.Less,    RUN_THRESHOLD,  "Speed");

        var forceBoostToRun = Transition(forceBoost, run, 0.15f, false);
        forceBoostToRun.AddCondition(AnimatorConditionMode.If,      0f,             "IsGrounded");
        forceBoostToRun.AddCondition(AnimatorConditionMode.Greater, RUN_THRESHOLD,  "Speed");

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

        // ── Auto-assign to Animators in the scene using this controller ────
        var reloaded = AssetDatabase.LoadAssetAtPath<AnimatorController>(OUTPUT_PATH);
        foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
        {
            if (animator.runtimeAnimatorController == null ||
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == OUTPUT_PATH)
            {
                animator.runtimeAnimatorController = reloaded;
                EditorUtility.SetDirty(animator);
            }
        }
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[AstronautAnimatorBuilder] Controller saved to {OUTPUT_PATH}.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static AnimatorState AddState(AnimatorStateMachine sm, string name, AnimationClip clip, float speed = 1f)
    {
        var state  = sm.AddState(name);
        state.motion = clip;
        state.speed  = speed;
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
