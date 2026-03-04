// ═════════════════════════════════════════════════════════════════════════════
//  PlayerAnimator.cs
//  Bridges SupernovaSprintController's runtime state to the Astronaut Animator.
//
//  SETUP:
//    1. Drag Astronaut.fbx into the scene as a child of the Player root,
//       replacing the primitive visual model.
//    2. Assign AstronautCharacterController.controller to the Animator on the FBX.
//    3. Add this component to the Player root.
//    4. Assign the Controller and Animator references in the Inspector.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The SupernovaSprintController on this player.")]
    public SupernovaSprintController controller;

    [Tooltip("The Animator on the Astronaut FBX child.")]
    public Animator animator;

    [Header("Thresholds")]
    [Tooltip("Speed above which the run animation plays.")]
    public float runThreshold = 0.5f;

    // AnimationPar values defined by AstronautCharacterController.controller
    private static readonly int AnimationPar = Animator.StringToHash("AnimationPar");

    private void Update()
    {
        if (controller == null || animator == null) return;

        int state = controller.currentSpeed > runThreshold ? 1 : 0;
        animator.SetInteger(AnimationPar, state);
    }
}
