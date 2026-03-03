// ═════════════════════════════════════════════════════════════════════════════
//  ThirdPersonCamera.cs
//  Orbit camera for Supernova Sprint. Reads input directly from the InputSystem
//  so it needs NO PlayerInput component of its own.
//
//  Controls:
//    Gamepad : Right stick to orbit
//    Mouse   : Hold right mouse button + drag to orbit
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Transform to follow. Assign the player root.")]
    public Transform target;

    [Tooltip("World-space offset added to the target position before orbiting. " +
             "Raise Y to look at the character's chest instead of its feet.")]
    public Vector3 followOffset = new Vector3(0f, 1.4f, 0f);

    [Header("Orbit")]
    [Tooltip("Rest distance from the pivot point (m).")]
    public float distance = 8f;

    [Tooltip("Fallback starting horizontal angle used only when no target is assigned at startup. " +
             "When a target exists, yaw is derived from the camera's actual world position instead.")]
    public float initialYaw = 0f;

    [Tooltip("Fallback starting vertical angle used only when no target is assigned at startup.")]
    public float initialPitch = 15f;

    [Tooltip("Minimum vertical angle (prevent looking too far up).")]
    public float minPitch = -20f;

    [Tooltip("Maximum vertical angle (prevent looking under the ground).")]
    public float maxPitch = 75f;

    [Header("Sensitivity")]
    [Tooltip("Gamepad right-stick orbit speed (degrees per second at full deflection).")]
    public float gamepadSensitivity = 180f;

    [Tooltip("Mouse orbit speed while holding right mouse button.")]
    public float mouseSensitivity = 0.2f;

    [Header("Smoothing")]
    [Tooltip("Position smooth time (seconds). Lower = snappier follow.")]
    public float positionSmoothTime = 0.08f;

    [Tooltip("How quickly the camera's look direction catches up after position moves.")]
    public float lookSmoothSpeed = 20f;

    [Header("Collision")]
    [Tooltip("Layers the camera pulls in to avoid clipping. " +
             "Should match the controller's groundLayers.")]
    public LayerMask collisionLayers = ~0;

    [Tooltip("Minimum pull-in distance when geometry is between camera and target.")]
    public float minDistance = 1.5f;

    // ── Private state ─────────────────────────────────────────────────────────

    private float        yaw;
    private float        pitch;
    private Vector3      smoothVelocity;     // Used by SmoothDamp
    private Quaternion   smoothLookRotation; // Lerped look-at rotation
    private Vector3      _shakeOffset;       // Added to pivot each frame
    private Coroutine    _shakeCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Derive yaw/pitch from the camera's actual world position relative to the target.
        // This means the first frame exactly matches wherever the camera was placed in the
        // scene — immune to stale serialised inspector values (e.g. after a code default change).
        // Falls back to initialYaw / initialPitch only when no target is assigned.
        if (target != null)
        {
            Vector3 dir = transform.position - (target.position + followOffset);
            if (dir.sqrMagnitude > 0.01f)
            {
                dir        = dir.normalized;
                float xzLen = Mathf.Sqrt(dir.x * dir.x + dir.z * dir.z);
                yaw   = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
                pitch = Mathf.Atan2( dir.y,  xzLen)  * Mathf.Rad2Deg;
            }
            else
            {
                yaw   = initialYaw;
                pitch = initialPitch;
            }
        }
        else
        {
            yaw   = initialYaw;
            pitch = initialPitch;
        }

        smoothLookRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // ── 1. Read look input directly from devices ──────────────────────────
        //
        //  We poll the InputSystem directly here so the camera doesn't need its
        //  own PlayerInput component (which would create device ownership conflicts).
        Vector2 lookDelta = Vector2.zero;

        if (Gamepad.current != null)
        {
            // Multiply by Time.deltaTime so sensitivity is framerate-independent.
            lookDelta += Gamepad.current.rightStick.ReadValue()
                         * (gamepadSensitivity * Time.deltaTime);
        }

        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            // Mouse.delta is in raw pixels. mouseSensitivity scales it to degrees.
            lookDelta += Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        yaw   += lookDelta.x;
        pitch -= lookDelta.y; // Subtract: stick/mouse up → camera looks up (negative pitch)
        pitch  = Mathf.Clamp(pitch, minPitch, maxPitch);

        // ── 2. Compute desired position ───────────────────────────────────────
        //
        //  Build the orbit rotation, then project backwards along it.
        //  A SphereCast from the pivot toward the camera position pulls the
        //  camera in if geometry is in the way, preventing clipping.
        Quaternion orbitRot  = Quaternion.Euler(pitch, yaw, 0f);
        Vector3    pivot     = target.position + followOffset + _shakeOffset;
        Vector3    cameraDir = orbitRot * Vector3.back; // Points from pivot toward camera

        float desiredDist = distance;
        if (Physics.SphereCast(pivot, 0.25f, cameraDir, out RaycastHit hit,
            desiredDist, collisionLayers, QueryTriggerInteraction.Ignore))
        {
            desiredDist = Mathf.Max(hit.distance - 0.1f, minDistance);
        }

        Vector3 desiredPos = pivot + cameraDir * desiredDist;

        // ── 3. Smooth position ────────────────────────────────────────────────
        transform.position = Vector3.SmoothDamp(
            transform.position, desiredPos, ref smoothVelocity, positionSmoothTime);

        // ── 4. Smooth look-at ─────────────────────────────────────────────────
        //
        //  We Slerp the look rotation separately from the position so the camera
        //  doesn't lag behind when pulled in by collision, which would make it look
        //  at the wrong spot.
        if ((pivot - transform.position).sqrMagnitude > 0.001f)
        {
            Quaternion lookTarget  = Quaternion.LookRotation(pivot - transform.position);
            smoothLookRotation     = Quaternion.Slerp(
                smoothLookRotation, lookTarget, lookSmoothSpeed * Time.deltaTime);
            transform.rotation     = smoothLookRotation;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantly snaps the camera to its correct orbit position around the target.
    /// Clears all SmoothDamp velocity so there is no lag on the next frame.
    /// Call this immediately after teleporting the player (e.g. on respawn).
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        Quaternion orbitRot  = Quaternion.Euler(pitch, yaw, 0f);
        Vector3    pivot     = target.position + followOffset;
        Vector3    snapPos   = pivot + orbitRot * Vector3.back * distance;

        transform.position    = snapPos;
        smoothVelocity        = Vector3.zero;

        if ((pivot - snapPos).sqrMagnitude > 0.001f)
        {
            smoothLookRotation = Quaternion.LookRotation(pivot - snapPos);
            transform.rotation = smoothLookRotation;
        }
    }

    /// <summary>
    /// Plays a decaying camera shake for the given duration and peak magnitude.
    /// Any shake already in progress is replaced.
    /// </summary>
    public void StartShake(float duration, float magnitude)
    {
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float progress    = elapsed / duration;
            float currentMag  = magnitude * (1f - progress); // Decays to zero
            _shakeOffset      = Random.insideUnitSphere * currentMag;
            elapsed          += Time.deltaTime;
            yield return null;
        }
        _shakeOffset    = Vector3.zero;
        _shakeCoroutine = null;
    }
}
