// ═════════════════════════════════════════════════════════════════════════════
//  SupernovaHUD.cs
//  In-game heads-up display for Supernova Sprint.
//
//  CANVAS SETUP:
//    1. Create a Canvas (Screen Space – Overlay, or Camera).
//    2. Add a TextMeshProUGUI in the top-left  → assign to Timer Text.
//    3. Add a TextMeshProUGUI in the top-right → assign to Speed Text.
//    4. Add this component anywhere (e.g. the Canvas root).
//    5. Assign Player Controller from the scene.
//
//  TIMER FORMAT:   MM:SS:ff   (ff = centiseconds 00-99)
//
//  SPEED COLOURS:
//    0–50  display → White
//    50–90 display → Yellow
//    90–100 display → Red-orange + pulse  (normal top speed)
//    100+  display → Cyan (boost colour) + faster/larger pulse
// ═════════════════════════════════════════════════════════════════════════════

using TMPro;
using UnityEngine;

public class SupernovaHUD : MonoBehaviour
{
    // ── References ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("TextMeshProUGUI placed in the top-left of the canvas for the timer.")]
    public TextMeshProUGUI timerText;

    [Tooltip("TextMeshProUGUI placed in the top-right of the canvas for the speedometer.")]
    public TextMeshProUGUI speedText;

    [Tooltip("The player's SupernovaSprintController — provides currentSpeed.")]
    public SupernovaSprintController playerController;

    // ── Speed colour thresholds ───────────────────────────────────────────────

    [Header("Speed Colours")]
    [Tooltip("Colour when speed is below the low threshold (0–10 m/s).")]
    public Color colorLow  = Color.white;

    [Tooltip("Colour at the mid range (10–18 m/s). Lerps from colorLow at 10 m/s.")]
    public Color colorMid  = Color.yellow;

    [Tooltip("Colour above the high threshold (18+ m/s). Lerps from colorMid at 18 m/s.")]
    public Color colorHigh = new Color(1f, 0.42f, 0.08f); // red-orange

    [Tooltip("Colour when speed exceeds 100 (boost state — above normal top speed).")]
    public Color colorBoost = Color.cyan;

    [Tooltip("Display value (0–100) where the colour starts shifting from white to yellow.")]
    public float thresholdLow  = 50f;

    [Tooltip("Display value (0–100) where the colour shifts to red-orange and pulse begins.")]
    public float thresholdHigh = 90f;

    // ── Speedometer scale ─────────────────────────────────────────────────────

    [Header("Speedometer Scale")]
    [Tooltip("The real speed (m/s) that maps to a display value of 100.\n" +
             "Matches topSpeed × 1.25 (the controller's slope-boost ceiling).\n" +
             "Values above 100 only appear when a boost trigger is active.")]
    public float maxDisplaySpeed = 38f;

    // ── Pulse ─────────────────────────────────────────────────────────────────

    [Header("Pulse (high speed only)")]
    [Tooltip("Oscillations per second for the scale pulse at high speed.")]
    public float pulseFrequency = 5f;

    [Tooltip("Peak scale multiplier added on top of 1.0 (e.g. 0.08 = ±8% size swing).")]
    public float pulseAmplitude = 0.08f;

    // ── Private state ─────────────────────────────────────────────────────────

    private float   _elapsed;           // Total seconds counted so far
    private bool    _timerRunning;      // True once the player first moves
    private bool    _hasStarted;        // Latched — prevents re-triggering mid-run
    private Vector3 _speedBaseScale;    // Original localScale of speedText transform

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (speedText != null)
            _speedBaseScale = speedText.transform.localScale;

        // Show the zeroed timer immediately so the canvas doesn't flash empty.
        RefreshTimerDisplay();
    }

    private void Update()
    {
        TickTimer();
        RefreshSpeedometer();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TIMER
    // ─────────────────────────────────────────────────────────────────────────

    private void TickTimer()
    {
        // Start the clock the first time the player moves.
        if (!_hasStarted && playerController != null && playerController.currentSpeed > 0.1f)
        {
            _hasStarted   = true;
            _timerRunning = true;
        }

        if (!_timerRunning) return;

        _elapsed += Time.deltaTime;
        RefreshTimerDisplay();
    }

    private void RefreshTimerDisplay()
    {
        if (timerText == null) return;

        int minutes      = (int)(_elapsed / 60f);
        int seconds      = (int)(_elapsed % 60f);
        int centiseconds = (int)((_elapsed % 1f) * 100f);

        timerText.text = $"{minutes:D2}:{seconds:D2}:{centiseconds:D2}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SPEEDOMETER
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshSpeedometer()
    {
        if (speedText == null || playerController == null) return;

        float speed = playerController.currentSpeed;

        // ── Text ──────────────────────────────────────────────────────────────
        //  Remap real speed to a 0–100+ display scale where 100 = maxDisplaySpeed.
        //  No Clamp01 — values above 100 are intentional during boost.
        int displayValue = Mathf.RoundToInt(speed / maxDisplaySpeed * 100f);
        speedText.text = $"{displayValue}";

        // ── Colour ────────────────────────────────────────────────────────────
        //
        //  Thresholds are on the 0–100 display scale (not raw m/s).
        //  0 → thresholdLow             : white
        //  thresholdLow → thresholdHigh : lerp white → yellow
        //  thresholdHigh → 100          : lerp yellow → red-orange
        //  100+                         : lerp red-orange → cyan (boost)
        Color targetColor;
        if (displayValue < thresholdLow)
        {
            targetColor = colorLow;
        }
        else if (displayValue < thresholdHigh)
        {
            float t = (displayValue - thresholdLow) / (thresholdHigh - thresholdLow);
            targetColor = Color.Lerp(colorLow, colorMid, t);
        }
        else if (displayValue < 100)
        {
            float t = Mathf.Clamp01((displayValue - thresholdHigh) / (100f - thresholdHigh));
            targetColor = Color.Lerp(colorMid, colorHigh, t);
        }
        else
        {
            // Boost range: 100 → 150 lerps red-orange fully into cyan.
            float t = Mathf.Clamp01((displayValue - 100f) / 50f);
            targetColor = Color.Lerp(colorHigh, colorBoost, t);
        }
        speedText.color = targetColor;

        // ── Pulse (high speed only) ────────────────────────────────────────────
        //
        //  Mathf.Sin returns –1 → +1.  We map that to 1–amplitude → 1+amplitude
        //  so the text breathes around its natural size, never shrinking below it.
        //  Above 100 (boost): frequency and amplitude are doubled for extra urgency.
        if (displayValue >= thresholdHigh)
        {
            float freqMult = displayValue >= 100 ? 2f : 1f;
            float ampMult  = displayValue >= 100 ? 2f : 1f;
            float sine  = Mathf.Sin(Time.time * pulseFrequency * freqMult * Mathf.PI * 2f);
            float scale = 1f + sine * pulseAmplitude * ampMult;
            speedText.transform.localScale = _speedBaseScale * scale;
        }
        else
        {
            speedText.transform.localScale = _speedBaseScale;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Freezes the timer. Call this when the player hits the goal at the end
    /// of the level. The displayed time is preserved until ResetTimer() is called.
    /// </summary>
    public void StopTimer()
    {
        _timerRunning = false;
    }

    /// <summary>
    /// Resets the timer back to 00:00:00 and restarts it.
    /// Hook this into SupernovaRespawnManager's respawn sequence if needed.
    /// </summary>
    public void ResetTimer()
    {
        _elapsed      = 0f;
        _timerRunning = false;
        _hasStarted   = false;  // Wait for the player to move again before counting
        RefreshTimerDisplay();
    }
}
