// ═════════════════════════════════════════════════════════════════════════════
//  KineticBrakeVFX.cs
//  Two white shockwave rings that expand and fade when Kinetic Brake is used.
//  One ring spawns slightly above the player midline, one slightly below.
//
//  SETUP:
//    1. Add this component to the Player root.
//    2. Assign Controller.
//    3. Optionally assign a Ring Material (URP Unlit/Transparent/Additive).
//       If left empty, falls back to Sprites/Default.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class KineticBrakeVFX : MonoBehaviour
{
    [Header("References")]
    public SupernovaSprintController controller;

    [Tooltip("Optional: assign a URP Unlit/Transparent/Additive material. " +
             "Leave empty to use the auto-generated fallback.")]
    public Material ringMaterial;

    [Header("Appearance")]
    public Color ringColor = Color.white;
    public float lineWidth = 0.1f;
    public int   segments  = 6;

    [Header("Layout")]
    [Tooltip("Start radius of both rings.")]
    public float startRadius = 1.7f;

    [Tooltip("End radius of both rings.")]
    public float endRadius = 0.7f;

    [Tooltip("Shifts both rings up or down together from the player root.")]
    public float centerHeight = 1.2f;

    [Tooltip("How far above and below the center height the rings spawn.")]
    public float verticalOffset = 0.25f;

    [Header("Animation")]
    [Tooltip("How long the rings take to fully expand and fade out.")]
    public float duration = 0.48f;

    [Tooltip("How far the rings drift downward (local space) over the duration.")]
    public float dropDistance = 1.2f;

    // ── Private ───────────────────────────────────────────────────────────────

    private Material _mat;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _mat = ringMaterial != null
            ? ringMaterial
            : new Material(Shader.Find("Sprites/Default")) { color = ringColor };
    }

    private void OnEnable()
    {
        if (controller != null) controller.OnKineticBrake += TriggerVFX;
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnKineticBrake -= TriggerVFX;
    }

    private void OnDestroy()
    {
        if (ringMaterial == null && _mat != null)
            Destroy(_mat);
    }

    // ── VFX ───────────────────────────────────────────────────────────────────

    private void TriggerVFX() => StartCoroutine(PulseRings());

    private IEnumerator PulseRings()
    {
        LineRenderer upper = CreateRing(Vector3.up * (centerHeight + verticalOffset));
        LineRenderer lower = CreateRing(Vector3.up * (centerHeight - verticalOffset));

        Vector3 upperStart = upper.transform.localPosition;
        Vector3 lowerStart = lower.transform.localPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t      = elapsed / duration;
            float alpha  = Mathf.Pow(1f - t, 1.5f);
            float radius = Mathf.Lerp(startRadius, endRadius, t);
            float drop   = dropDistance * t;

            upper.transform.localPosition = upperStart + Vector3.down * drop;
            lower.transform.localPosition = lowerStart + Vector3.down * drop;

            UpdateRing(upper, radius, alpha);
            UpdateRing(lower, radius, alpha);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(upper.gameObject);
        Destroy(lower.gameObject);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private LineRenderer CreateRing(Vector3 localOffset)
    {
        var go = new GameObject("KineticBrakeRing");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localOffset;

        var lr = go.AddComponent<LineRenderer>();
        lr.loop                 = true;
        lr.positionCount        = segments;
        lr.startWidth           = lineWidth;
        lr.endWidth             = lineWidth;
        lr.material             = _mat;
        lr.useWorldSpace        = false;
        lr.shadowCastingMode    = ShadowCastingMode.Off;
        lr.receiveShadows       = false;
        lr.generateLightingData = false;

        return lr;
    }

    private void UpdateRing(LineRenderer lr, float radius, float alpha)
    {
        Color c = new Color(ringColor.r, ringColor.g, ringColor.b, alpha);
        lr.startColor = c;
        lr.endColor   = c;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius));
        }
    }
}
