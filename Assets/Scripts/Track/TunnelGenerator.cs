// ═════════════════════════════════════════════════════════════════════════════
//  TunnelGenerator.cs
//  Procedurally builds a vertical loop (or helix) from a road-segment prefab.
//
//  PREFAB ORIENTATION REQUIRED:
//    Local Z+  =  forward  (direction of travel through the loop)
//    Local Y+  =  road surface "up"  (the face the player runs on)
//    If your mesh faces a different direction, use the Segment Rotation Offset
//    field to correct it (e.g. Y = 90 if your forward is X+).
//
//  WHY spiralOffset?
//    A perfect circle has its entry and exit at exactly the same height.
//    The flat track running under the loop would occupy the same space as the
//    exit path — they collide.  Adding a vertical rise (spiral / helix) shifts
//    the exit upward so both paths coexist without clipping.
//    Rule of thumb: set spiralOffset to at least your track thickness (0.5 m).
//
//  CONTROLLER COMPATIBILITY:
//    The generated rotation — LookRotation(tangent, inward) — ensures each
//    segment's local Y+ points toward the loop centre.  When the player's
//    transform aligns to the hit.normal from a SphereCast, transform.up will
//    match this inward direction, keeping the player glued at the apex.
//
//  WORKFLOW:
//    1. Create an empty GameObject.  Add this component.
//    2. Assign a Segment Prefab.
//    3. Tune Radius, Num Segments, Spiral Offset, Opening Half Angle.
//    4. Click "⚙ Generate Loop" in the Inspector.
//    5. Move / rotate the generator GameObject to position the loop.
//    6. Re-generate after moving — segments are children so they move with it.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public class TunnelGenerator : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    #region Inspector Fields

    [Header("── Segment ─────────────────────────────────────────────────────")]

    [Tooltip("Road segment prefab to tile around the loop.\n" +
             "Local Z+ must be the forward/travel direction.\n" +
             "Local Y+ must face the road surface (the side the player runs on).")]
    public GameObject segmentPrefab;

    [Tooltip("Euler-angle correction applied to each placed segment.\n" +
             "Use this when your prefab's forward axis is not local Z+.\n" +
             "Example: if your mesh's forward is X+, set Y = -90 here.")]
    public Vector3 segmentRotationOffset = Vector3.zero;

    // ── Shape ─────────────────────────────────────────────────────────────────

    [Header("── Loop Shape ──────────────────────────────────────────────────")]

    [Tooltip("Distance from the loop centre-line to the running surface (m).")]
    public float radius = 8f;

    [Tooltip("Total segments in a full 360°.  The actual count placed is lower\n" +
             "because the bottom opening eats some slots.")]
    [Range(4, 64)]
    public int numSegments = 20;

    [Tooltip("Vertical rise of the exit above the entry (m).\n" +
             "Prevents the entry flat and exit flat from occupying the same space.\n" +
             "Set to at least your track thickness.  0 = perfect circle (paths collide).")]
    public float spiralOffset = 1.5f;

    // ── Opening ───────────────────────────────────────────────────────────────

    [Header("── Opening ─────────────────────────────────────────────────────")]

    [Tooltip("Half-angle of the gap left open at the bottom of the loop (degrees).\n" +
             "0 = fully closed circle (tunnel).\n" +
             "Setting this equal to 360/numSegments leaves exactly one segment-slot\n" +
             "open on each side — just enough for the flat track to slot in.")]
    [Range(0f, 89f)]
    public float openingHalfAngleDeg = 0f;

    // ── Scale ─────────────────────────────────────────────────────────────────

    [Header("── Scale ───────────────────────────────────────────────────────")]

    [Tooltip("Rescale each segment so its Z length exactly covers its arc slice.\n" +
             "Disable if your prefab has a hand-tuned size you want to preserve.")]
    public bool rescaleSegments = true;

    [Tooltip("Track width (X scale) when Rescale Segments is on.")]
    public float trackWidth = 8f;

    [Tooltip("Track slab thickness (Y scale) when Rescale Segments is on.")]
    public float trackThickness = 0.5f;

    #endregion
    // ─────────────────────────────────────────────────────────────────────────

    // Serialised so the list survives domain reloads and scene saves.
    [HideInInspector]
    [SerializeField] private List<GameObject> _generated = new List<GameObject>();

    // ─────────────────────────────────────────────────────────────────────────
    #region Generation

    /// <summary>
    /// Destroys previously generated segments and rebuilds the loop from scratch.
    /// Safe to call multiple times — always clears first.
    /// </summary>
    public void Generate()
    {
        ClearGenerated();

        if (segmentPrefab == null)
        {
            Debug.LogError("[TunnelGenerator] No Segment Prefab assigned.", this);
            return;
        }

        if (radius <= 0f)
        {
            Debug.LogError("[TunnelGenerator] Radius must be > 0.", this);
            return;
        }

        // ── Angle range ───────────────────────────────────────────────────────
        //
        //  θ = −π/2  →  directly below the loop centre  (entry/exit side)
        //  θ increases counter-clockwise  (forward through the loop)
        //
        //  We leave a symmetric gap of openingHalfAngleDeg on each side of the
        //  bottom so the flat approach track can connect without a wall.
        float openHalfRad = openingHalfAngleDeg * Mathf.Deg2Rad;
        float startTheta  = -Mathf.PI * 0.5f + openHalfRad;
        float endTheta    = -Mathf.PI * 0.5f + Mathf.PI * 2f - openHalfRad;
        float totalAngle  = endTheta - startTheta;      // Slightly less than 2π

        // Determine how many segments to place, matching the density of numSegments
        // around a full circle.
        float fullCircleSegAngle = Mathf.PI * 2f / numSegments;
        int   placedCount        = Mathf.Max(2, Mathf.RoundToInt(totalAngle / fullCircleSegAngle));

        // Z-length for each segment: exact arc slice + 5% overlap to close seams.
        float segArcLen = (Mathf.PI * 2f * radius / numSegments) * 1.05f;

        // Spiral: constant vertical rise per radian of arc.
        float risePerRad = (totalAngle > 0f) ? spiralOffset / totalAngle : 0f;

        Quaternion rotOffset = Quaternion.Euler(segmentRotationOffset);

        // Closed loop: divide by placedCount so segments tile evenly — no two
        // segments land on the same spot at the bottom.
        // Open loop:   divide by placedCount-1 so the last segment lands exactly
        // at endTheta (both ends of the arc are capped).
        bool  isClosed = openingHalfAngleDeg <= 0f;
        float tDiv     = isClosed ? placedCount : Mathf.Max(placedCount - 1, 1);

        for (int i = 0; i < placedCount; i++)
        {
            float t     = (float)i / tDiv;
            float theta = Mathf.Lerp(startTheta, endTheta, t);
            // Closed loop must return to its start height — ignore spiralOffset.
            float rise  = isClosed ? 0f : t * spiralOffset;

            // ── Position (local to this generator transform) ───────────────────
            //
            //  Loop centre sits one radius above the generator origin.
            //  sin(θ): −1 at the bottom, +1 at the top.
            //  cos(θ): +1 at the near face, −1 at the far face.
            Vector3 localPos = new Vector3(
                0f,
                radius + radius * Mathf.Sin(theta) + rise,
                radius * Mathf.Cos(theta));

            // ── Tangent: direction of travel ───────────────────────────────────
            //
            //  d(pos)/dθ for the helix, then normalised.
            //  The risePerRad term biases the tangent slightly upward to track
            //  the spiral pitch — keeps the player's alignment smooth.
            Vector3 localTangent = new Vector3(
                0f,
                radius * Mathf.Cos(theta) + risePerRad,
                -radius * Mathf.Sin(theta)).normalized;

            // ── Inward normal: surface the player stands on ────────────────────
            //
            //  This is the vector pointing from the segment toward the loop centre.
            //  Using the pure-circle approximation (ignoring spiral curvature) is
            //  accurate enough for small spiralOffset values.
            //
            //  At the bottom (θ = −π/2): inward = (0,  1, 0)  →  world-up   ✓
            //  At the top    (θ = +π/2): inward = (0, −1, 0)  →  world-down ✓
            Vector3 localInward = new Vector3(
                0f,
                -Mathf.Sin(theta),
                -Mathf.Cos(theta)).normalized;

            // ── World-space transform ──────────────────────────────────────────
            Vector3    worldPos     = transform.TransformPoint(localPos);
            Vector3    worldTangent = transform.TransformDirection(localTangent);
            Vector3    worldInward  = transform.TransformDirection(localInward);

            if (worldTangent.sqrMagnitude < 0.001f) continue;

            // LookRotation(forward, up):  Z+ = tangent,  Y+ = inward
            Quaternion worldRot = Quaternion.LookRotation(worldTangent, worldInward) * rotOffset;

            // ── Instantiate ────────────────────────────────────────────────────
#if UNITY_EDITOR
            // InstantiatePrefab maintains the prefab connection in the scene.
            var seg = (GameObject)PrefabUtility.InstantiatePrefab(segmentPrefab, transform);
#else
            var seg = Instantiate(segmentPrefab, transform);
#endif
            seg.name = $"LoopSeg_{i:D2}";
            seg.transform.SetPositionAndRotation(worldPos, worldRot);

            if (rescaleSegments)
                seg.transform.localScale = new Vector3(trackWidth, trackThickness, segArcLen);

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(seg, "Generate Loop Segment");
#endif
            _generated.Add(seg);
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        Debug.Log(
            $"[TunnelGenerator] Built {placedCount} segments. " +
            $"Entry ≈ y {transform.position.y:F2} m  |  " +
            $"Exit ≈ y {transform.position.y + spiralOffset:F2} m.",
            this);
    }

    /// <summary>Destroys all segments previously generated by this component.</summary>
    public void ClearGenerated()
    {
        foreach (var go in _generated)
        {
            if (go == null) continue;
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(go);
#else
            Destroy(go);
#endif
        }
        _generated.Clear();

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
    }

    #endregion
    // ─────────────────────────────────────────────────────────────────────────
    #region Scene-View Gizmos

    private void OnDrawGizmos()
    {
        if (radius <= 0f || numSegments < 4) return;

        float openHalfRad = openingHalfAngleDeg * Mathf.Deg2Rad;
        float startTheta  = -Mathf.PI * 0.5f + openHalfRad;
        float endTheta    = -Mathf.PI * 0.5f + Mathf.PI * 2f - openHalfRad;

        // Draw the centreline arc — yellow at the entry, cyan at the exit.
        const int STEPS = 64;
        Vector3   prev  = Vector3.zero;

        for (int i = 0; i <= STEPS; i++)
        {
            float t     = (float)i / STEPS;
            float theta = Mathf.Lerp(startTheta, endTheta, t);
            float rise  = t * spiralOffset;

            Vector3 world = transform.TransformPoint(new Vector3(
                0f,
                radius + radius * Mathf.Sin(theta) + rise,
                radius * Mathf.Cos(theta)));

            if (i > 0)
            {
                Gizmos.color = Color.Lerp(Color.yellow, Color.cyan, t);
                Gizmos.DrawLine(prev, world);
            }
            prev = world;
        }

        // Entry marker (green sphere + forward arrow)
        Vector3 entry = transform.TransformPoint(new Vector3(
            0f,
            radius + radius * Mathf.Sin(startTheta),
            radius * Mathf.Cos(startTheta)));

        Vector3 entryTangent = transform.TransformDirection(new Vector3(
            0f,
            radius * Mathf.Cos(startTheta),
            -radius * Mathf.Sin(startTheta)).normalized);

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(entry, 0.3f);
        Gizmos.DrawRay(entry, entryTangent * 2f);

        // Exit marker (red sphere + forward arrow)
        Vector3 exit = transform.TransformPoint(new Vector3(
            0f,
            radius + radius * Mathf.Sin(endTheta) + spiralOffset,
            radius * Mathf.Cos(endTheta)));

        Vector3 exitTangent = transform.TransformDirection(new Vector3(
            0f,
            radius * Mathf.Cos(endTheta) + (spiralOffset / Mathf.Max(endTheta - startTheta, 0.001f)),
            -radius * Mathf.Sin(endTheta)).normalized);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(exit, 0.3f);
        Gizmos.DrawRay(exit, exitTangent * 2f);

        // Loop-centre cross
        Vector3 centre = transform.TransformPoint(new Vector3(0f, radius, 0f));
        Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
        Gizmos.DrawWireSphere(centre, 0.2f);
    }

    #endregion
}

// ─────────────────────────────────────────────────────────────────────────────
//  Custom Inspector
// ─────────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
[CustomEditor(typeof(TunnelGenerator))]
public class TunnelGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Loop Tools", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("⚙  Generate Loop", GUILayout.Height(32)))
            {
                var gen = (TunnelGenerator)target;
                Undo.RecordObject(gen, "Generate Loop");
                gen.Generate();
            }

            EditorGUILayout.Space(2);

            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);

            if (GUILayout.Button("🗑  Clear Generated Segments", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear Loop Segments",
                    "Destroy all generated loop segments?\n\nThis can be undone with Ctrl+Z.",
                    "Clear", "Cancel"))
                {
                    Undo.RecordObject(target, "Clear Loop");
                    ((TunnelGenerator)target).ClearGenerated();
                }
            }

            GUI.backgroundColor = prevBg;
        }

        if (EditorApplication.isPlaying)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Stop Play Mode to generate or clear segments.",
                MessageType.Info);
        }
    }
}
#endif
