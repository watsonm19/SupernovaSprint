// ═════════════════════════════════════════════════════════════════════════════
//  PolarityVFX.cs
//  Visual feedback for Normal ↔ Rocket Mode transitions.
//
//  Effects:
//    • Jetpack thruster particle system fires in Rocket Mode.
//    • ThrusterGlow pentagon appears in Rocket Mode.
//
//  SETUP:
//    1. Add this component to the Player root.
//    2. Assign all three references in the Inspector.
//    3. Keep ThrusterGlow inside the Jetpack empty GO (sibling of Thruster).
//    4. Keep the Thruster GameObject active — uncheck Play On Awake instead.
//    5. Keep ThrusterGlow inactive by default (unchecked in Inspector).
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class PolarityVFX : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The SupernovaSprintController on this player.")]
    public SupernovaSprintController controller;

    [Tooltip("Particle System at the jetpack. Must be active — Play On Awake unchecked.")]
    public ParticleSystem jetpackThruster;

    [Tooltip("The ThrusterGlow pentagon mesh. Shown in Rocket Mode, hidden in Normal Mode.")]
    public GameObject thrusterGlow;

    // ── Private state ──────────────────────────────────────────────────────────

    private bool _wasRocketMode;
    private bool _thrusterWasPlaying;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        if (thrusterGlow != null) thrusterGlow.SetActive(false);
        if (jetpackThruster != null) jetpackThruster.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (controller == null) return;

        // Glow — follows Rocket Mode only.
        if (controller.isRocketMode != _wasRocketMode)
        {
            _wasRocketMode = controller.isRocketMode;
            if (thrusterGlow != null)
                thrusterGlow.SetActive(controller.isRocketMode);
        }

        // Thruster — plays only when in Rocket Mode and actually moving.
        bool shouldPlay = controller.isRocketMode && controller.currentSpeed > 0.1f;
        if (shouldPlay == _thrusterWasPlaying) return;

        _thrusterWasPlaying = shouldPlay;
        if (jetpackThruster == null) return;

        if (shouldPlay)
            jetpackThruster.Play();
        else
            jetpackThruster.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
