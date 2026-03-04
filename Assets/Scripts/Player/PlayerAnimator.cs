// ═════════════════════════════════════════════════════════════════════════════
//  PlayerAnimator.cs
//  Drives the Astronaut Animator from SupernovaSprintController's runtime state.
//
//  PARAMETERS (set by AstronautAnimatorBuilder):
//    Speed      (float) — current movement speed
//    IsGrounded (bool)  — true when on a surface
//    IsHoming   (bool)  — true during a homing attack
//
//  SETUP:
//    1. Run Supernova Sprint → Build Astronaut Animator to generate the controller.
//    2. Assign the generated controller to the Animator on the Visual child.
//    3. Add this component to the Player root and assign both references.
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
    [Tooltip("Speed above which the Run animation plays instead of Walk.\n" +
             "Set this to Normal Mode topSpeed (25) so Rocket Mode triggers Run.")]
    public float runThreshold = 25f;

    // Cached parameter hashes — faster than string lookup every frame.
    private static readonly int SpeedHash      = Animator.StringToHash("Speed");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsHomingHash   = Animator.StringToHash("IsHoming");

    private void Update()
    {
        if (controller == null || animator == null) return;

        animator.SetFloat(SpeedHash,      controller.currentSpeed);
        animator.SetBool(IsGroundedHash,  controller.isGroundedPublic);
        animator.SetBool(IsHomingHash,    controller.isHomingPublic);
    }
}
