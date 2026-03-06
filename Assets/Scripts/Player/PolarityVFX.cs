// ═════════════════════════════════════════════════════════════════════════════
//  PolarityVFX.cs
//  Visual feedback for Normal ↔ Rocket Mode transitions.
//
//  Effects:
//    • Jetpack thruster particle system fires in Rocket Mode.
//    • Cyan pentagon glow appears at the jetpack in Rocket Mode.
//      The pentagon is generated in code at runtime — no ProBuilder needed.
//
//  SETUP:
//    1. Add this component to the Player root.
//    2. Assign Controller, Jetpack Thruster, and Glow Parent in the Inspector.
//    3. Glow Parent = the Jetpack empty GO (where the thruster lives).
//    4. Keep the Thruster GameObject active — uncheck Play On Awake instead.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public class PolarityVFX : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The SupernovaSprintController on this player.")]
    public SupernovaSprintController controller;

    [Tooltip("Particle System at the jetpack. Must be active — Play On Awake unchecked.")]
    public ParticleSystem jetpackThruster;

    [Tooltip("The Jetpack empty GO — the pentagon glow is created here at runtime.")]
    public Transform glowParent;

    [Header("Glow Appearance")]
    [Tooltip("Color of the pentagon glow.")]
    public Color glowColor = Color.cyan;

    [Tooltip("Radius of the pentagon in world units.")]
    public float glowRadius = 0.0002f;

    [Tooltip("Local position offset from the Glow Parent. Tweak this to place the pentagon correctly.")]
    public Vector3 glowOffset = new Vector3(0f, 0.00075f, 0.000005f);

    // ── Private state ──────────────────────────────────────────────────────────

    private bool       _wasRocketMode;
    private bool       _thrusterWasPlaying;
    private GameObject _thrusterGlow;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        // Build the glow under the Jetpack GO — falls back to thruster transform
        // if no glowParent is assigned.
        Transform parent = glowParent != null ? glowParent
                         : jetpackThruster != null ? jetpackThruster.transform
                         : transform;

        _thrusterGlow = BuildGlow(parent);
        _thrusterGlow.SetActive(false);

        if (jetpackThruster != null)
            jetpackThruster.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        if (controller == null) return;

        // Glow — follows Rocket Mode only.
        if (controller.isRocketMode != _wasRocketMode)
        {
            _wasRocketMode = controller.isRocketMode;
            if (_thrusterGlow != null)
                _thrusterGlow.SetActive(controller.isRocketMode);
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

    // ── Glow builder ───────────────────────────────────────────────────────────

    private GameObject BuildGlow(Transform parent)
    {
        var go = new GameObject("ThrusterGlow");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = glowOffset;
        go.transform.localRotation = Quaternion.identity;

        var mf   = go.AddComponent<MeshFilter>();
        var mr   = go.AddComponent<MeshRenderer>();

        mf.mesh     = BuildPentagonMesh();
        mr.material = BuildGlowMaterial();

        return go;
    }

    private Mesh BuildPentagonMesh()
    {
        const int sides = 5;

        var verts = new Vector3[sides + 1];
        var tris  = new int[sides * 3];

        verts[0] = Vector3.zero; // center

        for (int i = 0; i < sides; i++)
        {
            // Start at bottom (90°) so the pentagon points up correctly.
            float angle = (i / (float)sides) * Mathf.PI * 2f + Mathf.PI * 0.5f;
            verts[i + 1] = new Vector3(
                Mathf.Cos(angle) * glowRadius,
                Mathf.Sin(angle) * glowRadius,
                0f);
        }

        for (int i = 0; i < sides; i++)
        {
            tris[i * 3 + 0] = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % sides + 1;
        }

        var mesh      = new Mesh { name = "PentagonGlow" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

    private Material BuildGlowMaterial()
    {
        // URP Unlit keeps the cyan vivid regardless of scene lighting.
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Unlit/Color");

        var mat = new Material(shader) { name = "ThrusterGlowMat" };
        mat.SetColor("_BaseColor", glowColor);

        // Render both faces so it's visible from any angle.
        mat.SetFloat("_Cull", 0f);

        return mat;
    }
}
