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

    [Header("Nova Surge Thruster")]
    [Tooltip("X size multiplier for thruster particles during Nova Surge.")]
    public float surgeThrusterScaleX = 1.3f;

    [Tooltip("Y size multiplier for thruster particles during Nova Surge.")]
    public float surgeThrusterScaleY = 1.3f;

    [Tooltip("Speed multiplier for thruster particles during Nova Surge.")]
    public float surgeThrusterSpeedMultiplier = 2f;

    [Header("Glow Appearance")]
    [Tooltip("Color of the pentagon glow.")]
    public Color glowColor = Color.cyan;

    [Tooltip("Radius of the pentagon in world units.")]
    public float glowRadius = 0.0002f;

    [Tooltip("Local position offset from the Glow Parent. Tweak this to place the pentagon correctly.")]
    public Vector3 glowOffset = new Vector3(0f, 0.00075f, 0.000005f);

    [Tooltip("Pre-saved material for the glow. Assign Mat_ThrusterGlow from Assets/Materials/.")]
    public Material glowMaterial;

    [Tooltip("Material used for the glow during Nova Surge. Assign Mat_ThrusterNova from Assets/Materials/.")]
    public Material novaMaterial;

    // ── Private state ──────────────────────────────────────────────────────────

    private bool       _wasRocketMode;
    private bool       _wasNovaSurging;
    private bool       _thrusterWasPlaying;
    private GameObject _thrusterGlow;
    private MeshRenderer _glowRenderer;

    // Baseline thruster values (snapshotted on Start)
    private float _baseThrusterSizeX;
    private float _baseThrusterSizeY;
    private float _baseThrusterSpeed;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Start()
    {
        // Build the glow under the Jetpack GO — falls back to thruster transform
        // if no glowParent is assigned.
        Transform parent = glowParent != null ? glowParent
                         : jetpackThruster != null ? jetpackThruster.transform
                         : transform;

        _thrusterGlow  = BuildGlow(parent);
        _glowRenderer  = _thrusterGlow.GetComponent<MeshRenderer>();
        _thrusterGlow.SetActive(false);

        if (jetpackThruster != null)
        {
            var main = jetpackThruster.main;
            _baseThrusterSizeX = main.startSizeX.constant;
            _baseThrusterSizeY = main.startSizeY.constant;
            _baseThrusterSpeed = main.startSpeed.constant;
            jetpackThruster.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        if (controller == null) return;

        // Glow — active during Rocket Mode or Nova Surge, material swaps accordingly
        bool surging    = controller.isNovaSurging;
        bool rocketMode = controller.isRocketMode;

        if (surging != _wasNovaSurging || rocketMode != _wasRocketMode)
        {
            _wasNovaSurging = surging;
            _wasRocketMode  = rocketMode;

            if (_thrusterGlow != null)
                _thrusterGlow.SetActive(rocketMode || surging);

            if (_glowRenderer != null)
                _glowRenderer.material = (surging && novaMaterial != null) ? novaMaterial : glowMaterial;

            if (jetpackThruster != null)
            {
                var main = jetpackThruster.main;
                if (surging)
                {
                    main.startSizeX = new ParticleSystem.MinMaxCurve(_baseThrusterSizeX * surgeThrusterScaleX);
                    main.startSizeY = new ParticleSystem.MinMaxCurve(_baseThrusterSizeY * surgeThrusterScaleY);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(_baseThrusterSpeed * surgeThrusterSpeedMultiplier);
                }
                else
                {
                    main.startSizeX = new ParticleSystem.MinMaxCurve(_baseThrusterSizeX);
                    main.startSizeY = new ParticleSystem.MinMaxCurve(_baseThrusterSizeY);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(_baseThrusterSpeed);
                }
            }
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
        mr.material = glowMaterial;

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
            tris[i * 3 + 1] = (i + 1) % sides + 1;
            tris[i * 3 + 2] = i + 1;
        }

        var mesh      = new Mesh { name = "PentagonGlow" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        return mesh;
    }

}
