// ═════════════════════════════════════════════════════════════════════════════
//  PlayerAudio.cs
//  Handles all player SFX — footsteps, jump, land, homing attack/hit,
//  rocket toggle, and looping thruster.
//  Attach to the player root alongside SupernovaSprintController.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("References")]
    public SupernovaSprintController controller;

    [Header("Clips")]
    public AudioClip footstep;
    public AudioClip jump;
    public AudioClip land;
    public AudioClip homingAttack;
    public AudioClip homingHit;
    public AudioClip rocketToggleOn;
    public AudioClip rocketToggleOff;
    public AudioClip thruster;
    public AudioClip forceBoost;
    public AudioClip kineticBrake;
    public AudioClip novaSurgeActivate;
    public AudioClip novaSurgeRecharged;

    [Header("Footstep")]
    [Tooltip("Step interval at low speed (seconds).")]
    public float footstepIntervalSlow = 0.5f;
    [Tooltip("Step interval at top speed (seconds).")]
    public float footstepIntervalFast = 0.2f;

    [Header("Volume")]
    public float footstepVolume     = 0.6f;
    public float jumpVolume         = 1f;
    public float landVolume         = 1f;
    public float homingAttackVolume = 1f;
    public float homingHitVolume    = 1f;
    public float forceBoostVolume   = 1f;
    public float kineticBrakeVolume    = 1f;
    public float novaSurgeActivateVolume  = 1f;
    public float novaSurgeRechargedVolume = 1f;
    public float thrusterVolume     = 0.5f;
    public float thrusterPitch      = 1.25f;
    public float thrusterSurgePitch = 2f;

    // ── Private ───────────────────────────────────────────────────────────────

    private AudioSource _sfx;
    private AudioSource _thrusterSource;
    private float       _footstepTimer;
    private bool        _wasOnCooldown;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<SupernovaSprintController>();

        // One-shot SFX source
        _sfx             = gameObject.AddComponent<AudioSource>();
        _sfx.playOnAwake = false;
        _sfx.spatialBlend = 0f;

        // Looping thruster source
        _thrusterSource             = gameObject.AddComponent<AudioSource>();
        _thrusterSource.clip        = thruster;
        _thrusterSource.loop        = true;
        _thrusterSource.playOnAwake = false;
        _thrusterSource.spatialBlend = 0f;
        _thrusterSource.volume      = thrusterVolume;
        _thrusterSource.pitch       = thrusterPitch;
    }

    private void OnEnable()
    {
        if (controller == null) return;
        controller.OnJump         += PlayJump;
        controller.OnLand         += PlayLand;
        controller.OnHomingAttack += PlayHomingAttack;
        controller.OnHomingHit    += PlayHomingHit;
        controller.OnForceBoost   += PlayForceBoost;
        controller.OnKineticBrake += PlayKineticBrake;
        controller.OnRocketToggle += PlayRocketToggle;
        controller.OnNovaSurge    += PlayNovaSurgeActivate;
    }

    private void OnDisable()
    {
        if (controller == null) return;
        controller.OnJump         -= PlayJump;
        controller.OnLand         -= PlayLand;
        controller.OnHomingAttack -= PlayHomingAttack;
        controller.OnHomingHit    -= PlayHomingHit;
        controller.OnForceBoost   -= PlayForceBoost;
        controller.OnKineticBrake -= PlayKineticBrake;
        controller.OnRocketToggle -= PlayRocketToggle;
        controller.OnNovaSurge    -= PlayNovaSurgeActivate;
    }

    private void Update()
    {
        if (controller == null) return;
        HandleFootsteps();
        HandleThruster();
        HandleSurgeRecharged();
    }

    // ── Footsteps ─────────────────────────────────────────────────────────────

    private void HandleFootsteps()
    {
        if (!controller.isGroundedPublic || controller.currentSpeed < 1f)
        {
            _footstepTimer = 0f;
            return;
        }

        float t        = Mathf.Clamp01(controller.currentSpeed / controller.topSpeed);
        float interval = Mathf.Lerp(footstepIntervalSlow, footstepIntervalFast, t);

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer <= 0f)
        {
            Play(footstep, footstepVolume);
            _footstepTimer = interval;
        }
    }

    // ── Thruster ──────────────────────────────────────────────────────────────

    private void HandleThruster()
    {
        bool shouldPlay = controller.isRocketMode && controller.currentSpeed > 0.5f;

        if (shouldPlay && !_thrusterSource.isPlaying)
            _thrusterSource.Play();
        else if (!shouldPlay && _thrusterSource.isPlaying)
            _thrusterSource.Stop();

        _thrusterSource.pitch = controller.isNovaSurging ? thrusterSurgePitch : thrusterPitch;
    }

    // ── Event callbacks ───────────────────────────────────────────────────────

    private void PlayJump()                  => Play(jump, jumpVolume);
    private void PlayLand()                  => Play(land, landVolume);
    private void PlayHomingAttack()          => Play(homingAttack, homingAttackVolume);
    private void PlayHomingHit()             => Play(homingHit, homingHitVolume);
    private void PlayForceBoost()            => Play(forceBoost, forceBoostVolume);
    private void PlayKineticBrake()          => Play(kineticBrake, kineticBrakeVolume);
    private void PlayRocketToggle(bool on)    => Play(on ? rocketToggleOn : rocketToggleOff);
    private void PlayNovaSurgeActivate()      => Play(novaSurgeActivate, novaSurgeActivateVolume);

    private void HandleSurgeRecharged()
    {
        bool onCooldown = controller.surgeCooldownRemaining > 0f;
        if (_wasOnCooldown && !onCooldown)
            Play(novaSurgeRecharged, novaSurgeRechargedVolume);
        _wasOnCooldown = onCooldown;
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private void Play(AudioClip clip, float volume = 1f)
    {
        if (clip != null) _sfx.PlayOneShot(clip, volume);
    }
}
