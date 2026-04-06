// ═════════════════════════════════════════════════════════════════════════════
//  SlingAnchor.cs
//  A corner anchor that tethers the player in Normal Mode and slings them 90°
//  around the corner with zero speed loss. Rocket Mode players are ignored.
//
//  SETUP:
//    1. Place on a GameObject at the corner pivot point.
//    2. Add a Trigger Collider (SphereCollider recommended) — set radius to
//       match how close the player needs to be to trigger the tether.
//    3. Set Swing Direction (Left or Right from the player's perspective).
//    4. Assign the ThirdPersonCamera reference.
//    5. Optionally assign a Glow Object — it will activate when a Normal Mode
//       player is nearby and deactivate when they leave.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;

public class SlingAnchor : MonoBehaviour
{
    public enum SwingDirection { Left, Right }

    [Header("Settings")]
    [Tooltip("Which way the player swings around the corner.")]
    public SwingDirection swingDirection = SwingDirection.Left;

    [Tooltip("How many degrees the player swings around the anchor.")]
    public float swingDegrees = 90f;

    [Tooltip("Duration of the 90-degree swing (seconds).")]
    public float swingDuration = 0.35f;

    [Tooltip("Duration of the camera yaw rotation after the swing (seconds).")]
    public float cameraRotateDuration = 0.3f;

    [Tooltip("How many degrees the camera rotates after the swing.")]
    public float cameraRotateDegrees = 90f;

    [Tooltip("How many degrees the player's facing direction rotates at swing exit.")]
    public float playerRotateDegrees = 90f;

    [Tooltip("Minimum speed (m/s) required to tether. Prevents accidental grabs when walking slowly.")]
    public float minTetherSpeed = 5f;

    [Tooltip("Extra speed (m/s) added to the exit velocity for the slingshot kick.")]
    public float exitBoost = 8f;

    [Header("References")]
    public ThirdPersonCamera thirdPersonCamera;

    [Tooltip("Optional GameObject to activate when a Normal Mode player is nearby.")]
    public GameObject glowObject;

    // ── Private ───────────────────────────────────────────────────────────────

    private bool _occupied;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (glowObject != null) glowObject.SetActive(true);
    }

    // ── Trigger ───────────────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (_occupied) return;

        var controller = other.GetComponent<SupernovaSprintController>()
                      ?? other.GetComponentInParent<SupernovaSprintController>();
        if (controller == null) return;

        // Rocket Mode: no tether
        if (controller.isRocketMode) return;

        var rb    = controller.GetComponent<Rigidbody>();
        float speed = rb.linearVelocity.magnitude;

        // Too slow to tether
        if (speed < minTetherSpeed) return;

        // Must be moving toward the anchor, not away
        Vector3 toAnchor = (transform.position - controller.transform.position);
        toAnchor.y = 0f;
        Vector3 flatVel = rb.linearVelocity;
        flatVel.y = 0f;
        if (flatVel.sqrMagnitude > 0.01f && Vector3.Dot(flatVel.normalized, toAnchor.normalized) < 0.1f)
            return;

        _occupied = true;
        StartCoroutine(SwingRoutine(controller, rb, speed));
    }

    // ── Swing ─────────────────────────────────────────────────────────────────

    private IEnumerator SwingRoutine(SupernovaSprintController controller, Rigidbody rb, float speed)
    {
        controller.StartTether();

        // ── Calculate arc ──────────────────────────────────────────────────────
        Vector3 anchorPos   = transform.position;
        Vector3 startPos    = controller.transform.position;

        // Project to horizontal plane around the anchor
        Vector3 toPlayer    = startPos - anchorPos;
        toPlayer.y          = 0f;
        float   radius      = Mathf.Max(toPlayer.magnitude, 0.5f);
        float   startAngle  = Mathf.Atan2(toPlayer.z, toPlayer.x);
        float   sweepAngle  = swingDirection == SwingDirection.Left
                              ?  swingDegrees * Mathf.Deg2Rad
                              : -swingDegrees * Mathf.Deg2Rad;
        float   endAngle    = startAngle + sweepAngle;
        float   startY      = startPos.y;

        // ── Swing over duration ────────────────────────────────────────────────
        float elapsed = 0f;
        while (elapsed < swingDuration)
        {
            if (controller == null) yield break;

            float t     = elapsed / swingDuration; // Linear — preserves speed through the arc
            float angle = Mathf.Lerp(startAngle, endAngle, t);

            Vector3 newPos = anchorPos + new Vector3(
                Mathf.Cos(angle) * radius,
                startY - anchorPos.y,
                Mathf.Sin(angle) * radius);

            rb.MovePosition(newPos);

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // ── Snap to exact end position ─────────────────────────────────────────
        Vector3 finalPos = anchorPos + new Vector3(
            Mathf.Cos(endAngle) * radius,
            startY - anchorPos.y,
            Mathf.Sin(endAngle) * radius);
        rb.MovePosition(finalPos);

        // ── Exit velocity: tangent to arc at end angle, original speed ─────────
        // Tangent direction at endAngle (perpendicular to the radius, in travel direction)
        Vector3 tangent = swingDirection == SwingDirection.Left
            ? new Vector3(-Mathf.Sin(endAngle), 0f, Mathf.Cos(endAngle))
            : new Vector3( Mathf.Sin(endAngle), 0f, -Mathf.Cos(endAngle));

        Vector3 exitVelocity = tangent.normalized * (speed + exitBoost);

        // Rotate player facing to match exit direction + offset
        float facingDelta = swingDirection == SwingDirection.Left ? -playerRotateDegrees : playerRotateDegrees;
        controller.transform.rotation = Quaternion.Euler(0f,
            controller.transform.eulerAngles.y + facingDelta, 0f);

        controller.ReleaseTether(exitVelocity);

        // ── Rotate camera to match new direction ───────────────────────────────
        if (thirdPersonCamera != null)
        {
            float yawDelta = swingDirection == SwingDirection.Left ? -cameraRotateDegrees : cameraRotateDegrees;
            thirdPersonCamera.RotateYaw(yawDelta, cameraRotateDuration);
        }

        _occupied = false;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.4f);

        // Draw the swing arc direction
        Vector3 arrowDir = swingDirection == SwingDirection.Left
            ? -transform.right
            : transform.right;
        Gizmos.DrawRay(transform.position, arrowDir * 1.5f);
    }
}
