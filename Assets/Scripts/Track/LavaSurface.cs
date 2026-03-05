// ═════════════════════════════════════════════════════════════════════════════
//  LavaSurface.cs
//  Animates a lava material: scrolling UVs + pulsing emission.
//
//  SETUP:
//    1. Add this component to the lava plane.
//    2. In the material Inspector, enable Emission and set an emission color
//       (orange-red works well). The script drives the intensity — the color
//       you set becomes the base tint.
//    3. Tune ScrollSpeed and pulse values in the Inspector.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class LavaSurface : MonoBehaviour
{
    [Header("UV Scroll")]
    [Tooltip("How fast the texture scrolls on each axis. Negative values reverse direction.")]
    public Vector2 scrollSpeed = new Vector2(0.02f, 0.008f);

    [Header("Emission Pulse")]
    [Tooltip("Base emission color — should match the color set in the material.")]
    public Color emissionColor = new Color(1f, 0.35f, 0f);   // molten orange

    [Tooltip("Emission intensity at the dim point of the pulse.")]
    public float emissionMin = 0.6f;

    [Tooltip("Emission intensity at the bright point of the pulse.")]
    public float emissionMax = 2.2f;

    [Tooltip("How many pulses per second.")]
    public float pulseSpeed = 0.8f;

    // ── Private state ──────────────────────────────────────────────────────────

    private Material _mat;
    private Vector2  _offset;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        // .material creates a per-instance copy so we only affect this plane.
        _mat = GetComponent<Renderer>().material;
        _mat.EnableKeyword("_EMISSION");
    }

    private void Update()
    {
        // ── UV scroll ─────────────────────────────────────────────────────────
        _offset += scrollSpeed * Time.deltaTime;
        _mat.mainTextureOffset = _offset;

        // ── Emission pulse ────────────────────────────────────────────────────
        // Sine wave mapped to 0..1 so intensity smoothly oscillates min↔max.
        float t         = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(emissionMin, emissionMax, t);
        _mat.SetColor("_EmissionColor", emissionColor * intensity);
    }
}
