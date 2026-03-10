// ═════════════════════════════════════════════════════════════════════════════
//  SupernovaRespawnManager.cs
//
//  Handles player death and respawn for Supernova Sprint.
//
//  SETUP:
//    1. Create an empty GameObject below the track level (e.g. y = -30).
//    2. Add a BoxCollider (set Is Trigger = true) and size it to cover the play
//       area — a 500 × 5 × 500 box is a good starting point.
//    3. Add this component and assign:
//         • Player Controller  → the SupernovaSprintController on the Player
//         • Third Person Camera → the ThirdPersonCamera in the scene
//    4. Optionally assign a CanvasGroup on a full-screen Image for UI fade.
//
//  TRIGGER:
//    When the player falls through the kill-plane trigger, Respawn() is called
//    automatically.  You can also call Respawn() from a kill-timer or menu.
//
//  SEQUENCE:
//    Camera shake  →  (optional) fade to black
//    →  disable controller  →  zero velocity
//    →  teleport to spawn  →  snap camera
//    →  re-enable controller  →  (optional) fade back in
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SupernovaRespawnManager : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The SupernovaSprintController on the Player GameObject.")]
    public SupernovaSprintController playerController;

    [Tooltip("The ThirdPersonCamera in the scene.")]
    public ThirdPersonCamera thirdPersonCamera;

    [Tooltip("The SupernovaHUD in the scene. Timer is reset when the player respawns.")]
    public SupernovaHUD hud;

    // ── UI Fade (optional) ────────────────────────────────────────────────────

    [Header("Fade (optional)")]
    [Tooltip("CanvasGroup on a full-screen black Image. " +
             "Leave empty to skip the UI fade and use camera-shake only.")]
    public CanvasGroup fadeCanvasGroup;

    [Tooltip("Duration of each fade-out / fade-in pass (seconds).")]
    public float fadeDuration = 0.3f;

    // ── Camera Shake ──────────────────────────────────────────────────────────

    [Header("Camera Shake")]
    [Tooltip("How long the camera shakes when the player hits the kill plane (seconds).")]
    public float shakeDuration = 0.45f;

    [Tooltip("Peak displacement of the camera shake (world metres).")]
    public float shakeMagnitude = 0.6f;

    // ── Fail screen ───────────────────────────────────────────────────────────

    [Header("Fail Screen")]
    [Tooltip("The FailScreen component in the scene. Shown instead of respawning.")]
    public FailScreen failScreen;

    // ── Fall death ────────────────────────────────────────────────────────────

    [Header("Fall Death")]
    [Tooltip("If the player's Y position drops below this value, death is triggered.\n" +
             "Set it well below your lowest track surface. Works regardless of level size.")]
    public float deathYThreshold = -20f;

    // ── Private state ─────────────────────────────────────────────────────────

    private Vector3    _spawnPosition;
    private Quaternion _spawnRotation;
    private Rigidbody  _rb;
    private bool       _isRespawning;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Guarantee the collider attached to this object is a trigger.
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        if (playerController == null)
        {
            Debug.LogError("[RespawnManager] Player Controller is not assigned.", this);
            return;
        }

        // Snapshot where the player starts — this becomes the respawn point.
        _spawnPosition = playerController.transform.position;
        _spawnRotation = playerController.transform.rotation;
        _rb            = playerController.GetComponent<Rigidbody>();
    }

    // ── Fall death ────────────────────────────────────────────────────────────

    private void Update()
    {
        if (_isRespawning || playerController == null) return;
        if (playerController.transform.position.y < deathYThreshold)
            TriggerDeath();
    }

    // ── Kill-plane trigger ────────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            TriggerDeath();
    }

    private void TriggerDeath()
    {
        if (_isRespawning) return;
        _isRespawning = true;    // Prevent repeated calls

        if (hud != null) hud.StopTimer();

        if (failScreen != null)
        {
            if (thirdPersonCamera != null)
                thirdPersonCamera.StartShake(shakeDuration, shakeMagnitude);
            failScreen.Show();
        }
        else
        {
            // Fallback: respawn if no fail screen is wired up
            _isRespawning = false;
            Respawn();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Triggers the full respawn sequence.
    /// Safe to call from kill-timers, menu buttons, or other game systems.
    /// Concurrent calls are silently ignored while a respawn is in progress.
    /// </summary>
    public void Respawn()
    {
        if (_isRespawning) return;
        StartCoroutine(RespawnSequence());
    }

    // ── Sequence ──────────────────────────────────────────────────────────────

    private IEnumerator RespawnSequence()
    {
        _isRespawning = true;

        // ── 1. Immediate visual feedback ──────────────────────────────────────
        //  Stop the timer instantly — it will reset to 00:00:00 and wait for
        //  first movement again once the player regains control (step 6).
        if (hud != null)
            hud.StopTimer();

        //  Camera shake fires immediately (overlaps with any subsequent wait).
        if (thirdPersonCamera != null)
            thirdPersonCamera.StartShake(shakeDuration, shakeMagnitude);

        // ── 2. Wait before resetting ──────────────────────────────────────────
        //  With a fade canvas: fade to black (hides the teleport from the player).
        //  Without a canvas: wait half the shake duration so the crash feels real.
        if (fadeCanvasGroup != null)
            yield return StartCoroutine(Fade(0f, 1f, fadeDuration));
        else
            yield return new WaitForSeconds(Mathf.Max(shakeDuration * 0.5f, 0.15f));

        // ── 3. Freeze the player ──────────────────────────────────────────────
        if (playerController != null)
            playerController.enabled = false;

        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // Zero currentSpeed explicitly so the HUD's TickTimer doesn't see a
        // stale pre-death value and start the clock before the player moves.
        // (currentSpeed is only recalculated on the controller's next Update,
        //  which may run after SupernovaHUD.Update in the same frame.)
        if (playerController != null)
            playerController.currentSpeed = 0f;

        // ── 4. Teleport to spawn ──────────────────────────────────────────────
        //  rb.position/rotation is the correct API for an interpolated Rigidbody:
        //  it moves the physics body without waiting for the next FixedUpdate and
        //  avoids the one-frame position-interpolation jitter that transform.SetPositionAndRotation
        //  can produce.
        if (_rb != null)
        {
            _rb.position = _spawnPosition;
            _rb.rotation = _spawnRotation;
            Physics.SyncTransforms();
        }
        else if (playerController != null)
        {
            playerController.transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
        }

        // ── 5. Snap camera ────────────────────────────────────────────────────
        //  Clears SmoothDamp lag so the camera isn't racing across the world to
        //  catch up with the newly repositioned player.
        if (thirdPersonCamera != null)
            thirdPersonCamera.SnapToTarget();

        // ── 6. Restore all homing targets ────────────────────────────────────
        foreach (var target in FindObjectsByType<HomingTarget>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            target.gameObject.SetActive(true);

        // ── 7. Re-enable player movement + reset HUD timer ───────────────────
        if (playerController != null)
            playerController.enabled = true;

        if (hud != null)
            hud.ResetTimer();

        // ── 8. Fade back in ───────────────────────────────────────────────────
        if (fadeCanvasGroup != null)
            yield return StartCoroutine(Fade(1f, 0f, fadeDuration));

        _isRespawning = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            fadeCanvasGroup.alpha  = Mathf.Lerp(from, to, elapsed / duration);
            elapsed               += Time.deltaTime;
            yield return null;
        }
        fadeCanvasGroup.alpha = to;
    }
}
