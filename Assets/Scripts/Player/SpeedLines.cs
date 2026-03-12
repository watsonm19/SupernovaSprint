// ═════════════════════════════════════════════════════════════════════════════
//  SpeedLines.cs
//  Radial streak particles that intensify with speed.
//  White in normal mode, cyan-tinted in rocket mode.
//  Attach as a child of the player root — wired by SceneBootstrapper.
// ═════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.Rendering;

public class SpeedLines : MonoBehaviour
{
    [Header("References")]
    public SupernovaSprintController controller;

    [Header("Thresholds")]
    [Tooltip("Speed (m/s) at which lines begin appearing.")]
    public float speedThreshold = 14f;

    [Tooltip("Speed (m/s) at which emission reaches its maximum.")]
    public float fullSpeed = 28f;

    [Tooltip("Peak particles per second at full speed.")]
    public float maxEmissionRate = 45f;

    [Tooltip("Speed (m/s) above which lines shift to cyan.")]
    public float cyanThreshold = 60f;

    [Header("Shape & Position")]
    [Tooltip("Local position of the emitter relative to the player root.")]
    public Vector3 emitterPosition = new Vector3(0f, 0.3f, -1f);

    [Tooltip("Stretches particles along their velocity. Lower = shorter lines.")]
    public float velocityScale = 0.1f;

    [Tooltip("Multiplies particle length on top of velocity scale.")]
    public float lengthScale = 1.5f;

    [Header("Travel")]
    [Tooltip("How fast particles shoot out (m/s). Controls travel distance.")]
    public float minStartSpeed = 28f;
    public float maxStartSpeed = 42f;

    [Tooltip("How long particles live (seconds). Controls travel distance.")]
    public float minLifetime = 0.12f;
    public float maxLifetime = 0.22f;

    // ── Private state ─────────────────────────────────────────────────────────

    private ParticleSystem _ps;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (controller == null)
            controller = GetComponent<SupernovaSprintController>();
        _ps = BuildParticleSystem();
    }

    private void Update()
    {
        if (controller == null || _ps == null) return;

        float speed = controller.currentSpeed;
        float t     = Mathf.Clamp01((speed - speedThreshold) / (fullSpeed - speedThreshold));

        var emission          = _ps.emission;
        emission.rateOverTime = t * maxEmissionRate;

        // Cyan tint above 60 m/s, white below
        var main = _ps.main;
        main.startColor = speed >= cyanThreshold
            ? new Color(0.55f, 0.95f, 1f, 0.28f)
            : new Color(1f,    1f,    1f, 0.22f);
    }

    // ── Particle system builder ───────────────────────────────────────────────

    private ParticleSystem BuildParticleSystem()
    {
        transform.localPosition = emitterPosition;
        transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var ps = gameObject.AddComponent<ParticleSystem>();

        // ── Main ──────────────────────────────────────────────────────────────
        var main             = ps.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 300;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(minLifetime,  maxLifetime);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(minStartSpeed, maxStartSpeed);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor      = new Color(1f, 1f, 1f, 0.4f);

        // ── Emission — zero at start, Update drives it ────────────────────────
        var emission          = ps.emission;
        emission.rateOverTime = 0f;

        // ── Shape — cone pointing backward (emitter is rotated 180°) ─────────
        var shape        = ps.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Cone;
        shape.angle      = 32f;
        shape.radius     = 0.4f;

        // ── Colour over lifetime — fade out as particles travel ───────────────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.black, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // ── Renderer — stretched billboard for streak look ────────────────────
        var rend                = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode         = ParticleSystemRenderMode.Stretch;
        rend.velocityScale      = velocityScale;
        rend.lengthScale        = lengthScale;
        rend.shadowCastingMode  = ShadowCastingMode.Off;
        rend.receiveShadows     = false;
        rend.material           = BuildMaterial();

        return ps;
    }

    private static Material BuildMaterial()
    {
        // Try URP particles shader first, fall back to legacy
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");

        var mat = new Material(shader) { name = "Mat_SpeedLines" };

        // White, additive-style blending for a bright streak effect
        mat.SetColor("_BaseColor", Color.white);

        // Enable transparency for URP Particles/Unlit
        mat.SetFloat("_Surface", 1f);   // Transparent
        mat.SetFloat("_Blend",   2f);   // Additive
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;

        return mat;
    }
}
