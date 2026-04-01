// ═════════════════════════════════════════════════════════════════════════════
//  MotionBlurController.cs
//  Drives URP Motion Blur intensity based on the player's current speed.
//
//  SETUP:
//    1. Add this component anywhere in the scene (e.g. the player root).
//    2. Assign the PostProcessing Volume that contains your Motion Blur override.
//    3. Assign the player controller.
//    4. Make sure Motion Blur is added to the Volume with Intensity overridden.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class MotionBlurController : MonoBehaviour
{
    [Header("References")]
    public SupernovaSprintController controller;
    public Volume postProcessVolume;

    [Header("Speed Thresholds")]
    [Tooltip("No blur below this speed.")]
    public float noBlurBelow  = 25f;

    [Tooltip("Light blur starts here, ramps up to full blur at highBlurAbove.")]
    public float lowBlurStart = 25f;

    [Tooltip("Full blur intensity is reached at this speed.")]
    public float highBlurAbove = 41f;

    [Header("Intensity Range")]
    [Tooltip("Intensity applied in the low blur zone (25–40 m/s).")]
    public float lowIntensity  = 0.05f;

    [Tooltip("Intensity applied at and above the high blur threshold (41+ m/s).")]
    public float highIntensity = 0.25f;

    private MotionBlur _motionBlur;

    private void Update()
    {
        if (controller == null) return;

        if (_motionBlur == null && postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out _motionBlur);
            if (_motionBlur == null) postProcessVolume.sharedProfile?.TryGet(out _motionBlur);
        }

        if (_motionBlur == null) return;

        float speed = controller.currentSpeed;
        float intensity;

        if (speed <= noBlurBelow)
            intensity = 0f;
        else
            intensity = Mathf.Lerp(lowIntensity, highIntensity,
                Mathf.Clamp01((speed - lowBlurStart) / (highBlurAbove - lowBlurStart)));

        _motionBlur.intensity.value = intensity;
        Debug.Log($"[MotionBlur] speed={speed:F1} intensity={intensity:F3}");
    }
}
