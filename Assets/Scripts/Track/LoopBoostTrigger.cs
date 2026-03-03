// ═════════════════════════════════════════════════════════════════════════════
//  LoopBoostTrigger.cs
//  Speed gate for vertical loop-de-loop sections.
//
//  WHY THE PREVIOUS VERSION DIDN'T WORK:
//    The trigger only raised speed to minLoopSpeed (30) which equalled topSpeed,
//    so it never gave the player any extra.  Worse, ClampSpeed() in the controller
//    runs every FixedUpdate and immediately caps any overspeed back to topSpeed —
//    so even a large boost would be removed within one physics step.
//
//  THE FIX — two-part:
//    1. boostSpeed (default 40) intentionally exceeds topSpeed (30).
//    2. This trigger sets overrideSpeedCap = true on the controller so ClampSpeed()
//       stands down.  The flag self-clears once gravity has naturally slowed the
//       player back to topSpeed after completing the loop.
//
//  MINIMUM SPEED MATH:
//    Effective gravity on-surface = gravityForce + groundStickyForce = 25 + 20 = 45.
//    Minimum entry speed = sqrt(4 × 45 × loopRadius) = sqrt(4 × 45 × 8) ≈ 37.9 m/s.
//    Default boostSpeed (40) clears this with a small margin.
//    If you change loopRadius: new boostSpeed ≥ sqrt(180 × loopRadius).
//
//  SETUP:
//    1. Create an empty GameObject just in front of the loop entry.
//    2. Add this component (a BoxCollider is added automatically).
//    3. Resize the BoxCollider to span the full track width + player height
//       — good starting size: (trackWidth, 4, 2).
//    4. Rotate so the +Z gizmo arrow points into the loop.
//    5. Tag the player GameObject as "Player".
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class LoopBoostTrigger : MonoBehaviour
{
    [Header("── Speed Gate ──────────────────────────────────────────────────")]
    [Tooltip("Speed the player is boosted to on loop entry (m/s).\n" +
             "MUST exceed the controller's topSpeed — the trigger is useless otherwise.\n" +
             "Formula: sqrt(4 × (gravityForce + groundStickyForce) × loopRadius).\n" +
             "Default settings (gravity=25, sticky=20, R=8) → ~37.9 m/s → use 40.")]
    public float boostSpeed = 40f;

    [Tooltip("Only boost if the player is moving within this angle of the trigger's\n" +
             "+Z forward axis.  Prevents misfires on exit or sideways passes.\n" +
             "60° is a generous window that still blocks backwards entry.")]
    [Range(10f, 90f)]
    public float entryAngle = 60f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Reset()
    {
        var box       = GetComponent<BoxCollider>();
        box.isTrigger = true;
        box.size      = new Vector3(10f, 4f, 2f);
        box.center    = Vector3.zero;
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null) return;

        Vector3 vel   = rb.linearVelocity;
        float   speed = vel.magnitude;

        // Ignore near-stationary entry (player fell in from above, etc.)
        if (speed < 0.5f) return;

        // Direction guard — only boost when heading into the loop
        float cosThreshold = Mathf.Cos(entryAngle * Mathf.Deg2Rad);
        if (Vector3.Dot(vel.normalized, transform.forward) < cosThreshold) return;

        // Tell the controller to stop capping speed so the boost persists.
        // The flag self-clears in ClampSpeed() once gravity slows the player
        // back to topSpeed after the loop.
        var ctrl = other.GetComponentInParent<SupernovaSprintController>();
        if (ctrl != null)
        {
            ctrl.overrideSpeedCap      = true;
            ctrl.overrideSpeedCapTimer = 10f; // Safety timeout — clears if loop takes > 10 s
        }

        // Apply the boost — always raise to boostSpeed, never reduce.
        if (speed < boostSpeed)
            rb.linearVelocity = vel.normalized * boostSpeed;

        Debug.Log($"[LoopBoostTrigger] Boosted player {speed:F1} → {boostSpeed:F1} m/s, cap override ON.", this);
    }

    // ─────────────────────────────────────────────────────────────────────────
    #region Gizmos

    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null) return;

        // Trigger volume
        Gizmos.color  = new Color(0.1f, 0.9f, 0.2f, 0.18f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color  = new Color(0.1f, 0.9f, 0.2f, 0.7f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = Matrix4x4.identity;

        // Forward arrow (direction that triggers the boost)
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, transform.forward * 4f);

        // Entry angle cone
        float   coneR   = Mathf.Tan(entryAngle * Mathf.Deg2Rad) * 4f;
        Vector3 coneTip = transform.position + transform.forward * 4f;
        Gizmos.color = new Color(0.1f, 0.9f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(coneTip, coneR);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + transform.up * (box.size.y * 0.5f + 0.3f),
            $"Loop Boost → {boostSpeed:F0} m/s");
#endif
    }

    #endregion
}
