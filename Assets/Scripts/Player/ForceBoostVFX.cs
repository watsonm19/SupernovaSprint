// ═════════════════════════════════════════════════════════════════════════════
//  ForceBoostVFX.cs
//  Three cyan ring pulses that expand and fade when Force Boost is used.
//
//  Rings are generated in code — no prefabs or assets required.
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

public class ForceBoostVFX : MonoBehaviour
{
    [Header("References")]
    public SupernovaSprintController controller;

    [Tooltip("Optional: assign a URP Unlit/Transparent/Additive material. " +
             "Leave empty to use the auto-generated fallback.")]
    public Material ringMaterial;

    [Header("Appearance")]
    public Color ringColor  = new Color(0f, 1f, 1f, 1f); // Cyan, matches thruster glow
    public float lineWidth  = 0.05f;
    public int   segments   = 6;

    [Header("Layout")]
    [Tooltip("Start radius of the top ring.")]
    public float startRadiusTop = 0.5f;

    [Tooltip("End radius of the top ring.")]
    public float endRadiusTop = 1f;

    [Tooltip("Start radius of the middle ring.")]
    public float startRadiusMid = 0.35f;

    [Tooltip("End radius of the middle ring.")]
    public float endRadiusMid = 0.75f;

    [Tooltip("Start radius of the bottom ring.")]
    public float startRadiusBottom = 0.2f;

    [Tooltip("End radius of the bottom ring.")]
    public float endRadiusBottom = 0.5f;

    [Tooltip("How far above and below the player center the top/bottom rings spawn.")]
    public float verticalOffset = 0.5f;

    [Header("Animation")]
    [Tooltip("How long the rings take to fully expand and fade out.")]
    public float duration = 0.75f;

    // ── Private ───────────────────────────────────────────────────────────────

    private Material _mat;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (ringMaterial != null)
        {
            _mat = ringMaterial;
        }
        else
        {
            _mat           = new Material(Shader.Find("Sprites/Default"));
            _mat.color     = ringColor;
        }
    }

    private void OnEnable()
    {
        if (controller != null) controller.OnForceBoost += TriggerVFX;
    }

    private void OnDisable()
    {
        if (controller != null) controller.OnForceBoost -= TriggerVFX;
    }

    private void OnDestroy()
    {
        // Only destroy the material if we created it ourselves
        if (ringMaterial == null && _mat != null)
            Destroy(_mat);
    }

    // ── VFX ───────────────────────────────────────────────────────────────────

    private void TriggerVFX() => StartCoroutine(PulseRings());

    private IEnumerator PulseRings()
    {
        Vector3 center = transform.position;

        LineRenderer top    = CreateRing(center + Vector3.up * verticalOffset);
        LineRenderer mid    = CreateRing(center);
        LineRenderer bottom = CreateRing(center - Vector3.up * verticalOffset);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t     = elapsed / duration;
            float alpha = Mathf.Pow(1f - t, 1.5f); // snappy fade

            UpdateRing(top,    Mathf.Lerp(startRadiusTop,    endRadiusTop,    t), alpha);
            UpdateRing(mid,    Mathf.Lerp(startRadiusMid,    endRadiusMid,    t), alpha);
            UpdateRing(bottom, Mathf.Lerp(startRadiusBottom, endRadiusBottom, t), alpha);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(top.gameObject);
        Destroy(mid.gameObject);
        Destroy(bottom.gameObject);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private LineRenderer CreateRing(Vector3 worldPos)
    {
        var go = new GameObject("ForceBoostRing");
        go.transform.position = worldPos;

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
