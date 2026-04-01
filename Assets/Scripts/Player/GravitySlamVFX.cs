// ═════════════════════════════════════════════════════════════════════════════
//  GravitySlamVFX.cs
//  On slam activation: two rings shrink in radius and drift downward, holding
//  at their end position. The upper ring fills in as a solid disc when it lands.
//  On impact: disc disappears, both rings travel back up to their original
//  position and radius, then disappear.
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

public class GravitySlamVFX : MonoBehaviour
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

    [Tooltip("End radius both rings shrink to during the slam.")]
    public float endRadius = 0.7f;

    [Tooltip("Shifts both rings up or down together from the player root.")]
    public float centerHeight = 1.2f;

    [Tooltip("How far above and below the center height the rings spawn.")]
    public float verticalOffset = 0.25f;

    [Header("Animation")]
    [Tooltip("How long the rings take to reach their end position on activation.")]
    public float duration = 0.48f;

    [Tooltip("How far the rings drift downward (local space) over the duration.")]
    public float dropDistance = 1.2f;

    [Tooltip("How long the rings take to travel back up on impact.")]
    public float impactDuration = 0.3f;

    // ── Private ───────────────────────────────────────────────────────────────

    private Material     _mat;
    private LineRenderer _upper;
    private LineRenderer _lower;
    private GameObject   _filledDisc;
    private Coroutine    _activationCoroutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _mat = ringMaterial != null
            ? ringMaterial
            : new Material(Shader.Find("Sprites/Default")) { color = ringColor };

    }

    private void OnEnable()
    {
        if (controller != null)
        {
            controller.OnGravitySlam          += TriggerActivationVFX;
            controller.OnGravitySlamImpact    += TriggerImpactVFX;
            controller.OnGravitySlamCancelled += TriggerCancelVFX;
            controller.OnGrindStart           += OnGrindStart;
            controller.OnGrindEnd             += OnGrindEnd;
        }
    }

    private void OnDisable()
    {
        if (controller != null)
        {
            controller.OnGravitySlam          -= TriggerActivationVFX;
            controller.OnGravitySlamImpact    -= TriggerImpactVFX;
            controller.OnGravitySlamCancelled -= TriggerCancelVFX;
            controller.OnGrindStart           -= OnGrindStart;
            controller.OnGrindEnd             -= OnGrindEnd;
        }

        if (_activationCoroutine != null)
        {
            StopCoroutine(_activationCoroutine);
            _activationCoroutine = null;
        }
        CleanupAll();
    }

    private void OnDestroy()
    {
        if (ringMaterial == null && _mat != null)
            Destroy(_mat);
    }

    // ── VFX ───────────────────────────────────────────────────────────────────

    private bool _isGrinding;

    private void OnGrindStart()
    {
        _isGrinding = true;
        // Keep the disc visible — it becomes the grind board for the duration of the rail.
    }

    private void OnGrindEnd()
    {
        _isGrinding = false;
        // Rail is done — clean up the disc now.
        if (_filledDisc != null) { Destroy(_filledDisc); _filledDisc = null; }
        CleanupRings();
    }

    private void TriggerCancelVFX()
    {
        if (_activationCoroutine != null)
        {
            StopCoroutine(_activationCoroutine);
            _activationCoroutine = null;
        }

        if (_isGrinding)
        {
            // Force-complete the activation so the disc always appears during a grind,
            // even if the slam triggered right above the rail before the routine finished.
            Vector3 upperEnd = Vector3.up * (centerHeight + verticalOffset) + Vector3.down * dropDistance;
            Vector3 lowerEnd = Vector3.up * (centerHeight - verticalOffset) + Vector3.down * dropDistance;

            if (_upper != null) { _upper.transform.localPosition = upperEnd; UpdateRing(_upper, endRadius, 1f); }
            if (_lower != null) { _lower.transform.localPosition = lowerEnd; UpdateRing(_lower, endRadius, 1f); }

            if (_filledDisc == null)
                _filledDisc = CreateFilledDisc(upperEnd, endRadius);

            if (_lower != null) { Destroy(_lower.gameObject); _lower = null; }
            return;
        }

        CleanupAll();
    }

    private void TriggerActivationVFX()
    {
        CleanupAll();
        if (_activationCoroutine != null)
            StopCoroutine(_activationCoroutine);
        _activationCoroutine = StartCoroutine(ActivationRoutine());
    }

    private void TriggerImpactVFX()
    {
        if (_activationCoroutine != null)
        {
            StopCoroutine(_activationCoroutine);
            _activationCoroutine = null;
        }

        // Snap upper ring to end state in case activation hadn't finished yet
        if (_upper != null)
        {
            _upper.transform.localPosition = Vector3.up * (centerHeight + verticalOffset) + Vector3.down * dropDistance;
            UpdateRing(_upper, endRadius, 1f);
        }

        // Always snap lower ring to its end position before the impact animation.
        // If activation was interrupted mid-way, _lower exists but is at a partial position.
        // If activation completed, _lower was destroyed when the disc appeared.
        // Both cases need the same result: _lower sitting at the end position, ready to travel up.
        Vector3 lowerEndPos = Vector3.up * (centerHeight - verticalOffset) + Vector3.down * dropDistance;
        if (_lower != null)
        {
            _lower.transform.localPosition = lowerEndPos;
            UpdateRing(_lower, endRadius, 1f);
        }
        else
        {
            _lower = CreateRing(lowerEndPos);
            UpdateRing(_lower, endRadius, 1f);
        }

        // Remove the disc before the return trip
        if (_filledDisc != null)
        {
            Destroy(_filledDisc);
            _filledDisc = null;
        }

        StartCoroutine(ImpactRoutine());
    }

    private IEnumerator ActivationRoutine()
    {
        _upper = CreateRing(Vector3.up * (centerHeight + verticalOffset));
        _lower = CreateRing(Vector3.up * (centerHeight - verticalOffset));

        Vector3 upperOrigin = _upper.transform.localPosition;
        Vector3 lowerOrigin = _lower.transform.localPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_upper == null || _lower == null) yield break;

            float t      = elapsed / duration;
            float radius = Mathf.Lerp(startRadius, endRadius, t);
            float d      = dropDistance * t;

            _upper.transform.localPosition = upperOrigin + Vector3.down * d;
            _lower.transform.localPosition = lowerOrigin + Vector3.down * d;

            UpdateRing(_upper, radius, 1f);
            UpdateRing(_lower, radius, 1f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Snap to exact end state
        Vector3 upperEnd = upperOrigin + Vector3.down * dropDistance;
        Vector3 lowerEnd = lowerOrigin + Vector3.down * dropDistance;
        _upper.transform.localPosition = upperEnd;
        _lower.transform.localPosition = lowerEnd;
        UpdateRing(_upper, endRadius, 1f);
        UpdateRing(_lower, endRadius, 1f);

        // Fill in the upper ring as a solid disc; lower ring disappears at the same moment
        _filledDisc = CreateFilledDisc(upperEnd, endRadius);
        if (_lower != null) { Destroy(_lower.gameObject); _lower = null; }

        _activationCoroutine = null;
    }

    private IEnumerator ImpactRoutine()
    {
        if (_upper == null || _lower == null) yield break;

        Vector3 upperFrom = _upper.transform.localPosition;
        Vector3 lowerFrom = _lower.transform.localPosition;
        Vector3 upperTo   = Vector3.up * (centerHeight + verticalOffset);
        Vector3 lowerTo   = Vector3.up * (centerHeight - verticalOffset);

        float elapsed = 0f;
        while (elapsed < impactDuration)
        {
            if (_upper == null || _lower == null) yield break;

            float t      = elapsed / impactDuration;
            float radius = Mathf.Lerp(endRadius, startRadius, t);

            _upper.transform.localPosition = Vector3.Lerp(upperFrom, upperTo, t);
            _lower.transform.localPosition = Vector3.Lerp(lowerFrom, lowerTo, t);

            UpdateRing(_upper, radius, 1f);
            UpdateRing(_lower, radius, 1f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        CleanupRings();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void CleanupRings()
    {
        if (_upper != null) { Destroy(_upper.gameObject); _upper = null; }
        if (_lower != null) { Destroy(_lower.gameObject); _lower = null; }
    }

    private void CleanupAll()
    {
        CleanupRings();
        if (_filledDisc != null) { Destroy(_filledDisc); _filledDisc = null; }
    }

    private LineRenderer CreateRing(Vector3 localOffset)
    {
        var go = new GameObject("GravitySlamRing");
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

    private GameObject CreateFilledDisc(Vector3 localPosition, float radius)
    {
        var go = new GameObject("GravitySlamDisc");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.material          = _mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        // Fan triangulation: centre vertex + one vertex per segment
        var mesh   = new Mesh();
        var verts  = new Vector3[segments + 1];
        var tris   = new int[segments * 3];

        verts[0] = Vector3.zero; // centre
        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i % segments) + 1 == segments ? 1 : i + 2;
        }

        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mf.mesh = mesh;

        return go;
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
