// ═════════════════════════════════════════════════════════════════════════════
//  LoopGenerator.cs
//  Generates a custom mesh vertical loop-de-loop road section.
//
//  The road centre travels in a VERTICAL CIRCLE in the YZ plane — the classic
//  roller-coaster loop shape.
//
//  JOURNEY:
//    Entry (green gizmo)  →  player moving in +Z at ground level
//    Quarter of the way   →  curving upward, still moving forward in Z
//    Apex                 →  player is upside-down, moving in −Z (backwards)
//    Three-quarters       →  curving back down
//    Exit (red gizmo)     →  back at ground level, moving in +Z again
//
//  The road surface always faces INWARD toward the loop centre, so the player
//  experiences centripetal force rather than gravity at the apex — same as an
//  SA2 or real roller-coaster loop.
//
//  SIDE OFFSET (X drift):
//    A pure circle has entry and exit at the same XZ position — the caps block
//    each other.  sideOffset drifts the entire path linearly in X as it goes
//    around, so the exit lands sideOffset metres to the side of the entry.
//    Set it to at least trackWidth for complete lane separation.
//
//  SPIRAL OFFSET (Y rise):
//    Lifts the exit above the entry so the exit road sits above the approach
//    road and they don't clip vertically.  Rule of thumb: ≥ trackThickness.
//
//  See CorkscrewGenerator for a helix where the centre orbits the Z axis.
//  See TunnelGenerator for a prefab-tiled version of the same loop shape.
// ═════════════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class LoopGenerator : MonoBehaviour
{
    [Header("── Shape ───────────────────────────────────────────────────────")]
    [Tooltip("Radius of the loop (metres). Apex reaches 2 × Radius above the entry.")]
    public float loopRadius = 8f;

    [Tooltip("Width of the road ribbon (metres).")]
    public float trackWidth = 8f;

    [Tooltip("Thickness of the road slab (metres).")]
    public float trackThickness = 0.5f;

    [Tooltip("How far the exit is displaced sideways from the entry (metres).\n" +
             "The path drifts linearly in +X as it travels around the loop.\n" +
             "Set to at least Track Width so entry and exit lanes don't overlap.\n" +
             "Negative values drift in −X instead.")]
    public float sideOffset = 8f;

    [Tooltip("Vertical rise of the exit above the entry (metres).\n" +
             "Prevents the exit road from sitting at the exact same height as the entry.\n" +
             "Set to at least your track thickness.  0 = no rise.")]
    public float spiralOffset = 1.5f;

    [Tooltip("Number of mesh segments around the 360° loop. More = smoother circle.")]
    [Range(8, 128)]
    public int numSteps = 48;

    [Header("── Visual ───────────────────────────────────────────────────────")]
    public Material roadMaterial;

    // ─────────────────────────────────────────────────────────────────────────
    #region Generation

    public void Generate()
    {
        if (loopRadius <= 0f)
        {
            Debug.LogError("[LoopGenerator] Loop Radius must be > 0.", this);
            return;
        }

        float halfW  = trackWidth     * 0.5f;
        float halfT  = trackThickness * 0.5f;
        int   slices = numSteps + 1;

        // ── Vertex layout: 8 per slice ────────────────────────────────────────
        //   [0] tl — top face,  normal =  inward
        //   [1] tr — top face,  normal =  inward
        //   [2] bl — bot face,  normal = −inward
        //   [3] br — bot face,  normal = −inward
        //   [4] tl — left side, normal = −X
        //   [5] bl — left side, normal = −X
        //   [6] tr — right side, normal = +X
        //   [7] br — right side, normal = +X
        //   [8..15] two flat end caps
        var verts = new Vector3[slices * 8 + 8];
        var norms = new Vector3[slices * 8 + 8];
        var uvs   = new Vector2[slices * 8 + 8];
        var tris  = new List<int>(numSteps * 24 + 12);

        // ── Build body slices ─────────────────────────────────────────────────
        //
        //  Loop circle sits in the YZ plane, centre at (0, loopRadius, 0).
        //
        //  θ progresses from −π/2 to 3π/2 (one full revolution).
        //
        //  Surface position at angle θ:
        //    x = t · sideOffset             (0 at entry, sideOffset at exit)
        //    y = R + R·sin θ + rise         (0 at entry, 2R at apex)
        //    z = R·cos θ                    (0 at entry, R at 3-o'clock, 0 at apex)
        //
        //  The X drift is LINEAR in t so it spreads evenly around the loop.
        //  It does not affect the inward normal or road width direction —
        //  those are still determined purely by the YZ circle geometry.
        //
        //  Inward normal (toward loop centre from surface point):
        //    (0, −sin θ, −cos θ)
        //    θ = −π/2 → (0, +1,  0)  world-up    ✓  (entry, flat road)
        //    θ =  π/2 → (0, −1,  0)  world-down  ✓  (apex, player inverted)
        //    θ =  0   → (0,  0, −1)  toward near face from far side
        //    θ =  π   → (0,  0, +1)  toward far face from near side
        //
        //  Road width direction: +X  (constant — loop circle is in the YZ plane)

        for (int i = 0; i < slices; i++)
        {
            float t     = (float)i / numSteps;
            float theta = -Mathf.PI * 0.5f + t * Mathf.PI * 2f;
            float rise  = t * spiralOffset;

            float sinT = Mathf.Sin(theta);
            float cosT = Mathf.Cos(theta);

            Vector3 centre = new Vector3(
                t * sideOffset,
                loopRadius + loopRadius * sinT + rise,
                loopRadius * cosT);

            // Inward normal (points toward the loop axis)
            Vector3 inward = new Vector3(0f, -sinT, -cosT);

            // Road width always lies along world X for a YZ-plane loop
            Vector3 right = Vector3.right;

            Vector3 tl = centre + inward * halfT - right * halfW;
            Vector3 tr = centre + inward * halfT + right * halfW;
            Vector3 bl = centre - inward * halfT - right * halfW;
            Vector3 br = centre - inward * halfT + right * halfW;

            int b = i * 8;
            verts[b+0] = tl; norms[b+0] =  inward;      uvs[b+0] = new Vector2(0f, t);
            verts[b+1] = tr; norms[b+1] =  inward;      uvs[b+1] = new Vector2(1f, t);
            verts[b+2] = bl; norms[b+2] = -inward;      uvs[b+2] = new Vector2(0f, t);
            verts[b+3] = br; norms[b+3] = -inward;      uvs[b+3] = new Vector2(1f, t);
            verts[b+4] = tl; norms[b+4] = -right;       uvs[b+4] = new Vector2(0f, t);
            verts[b+5] = bl; norms[b+5] = -right;       uvs[b+5] = new Vector2(1f, t);
            verts[b+6] = tr; norms[b+6] =  right;       uvs[b+6] = new Vector2(0f, t);
            verts[b+7] = br; norms[b+7] =  right;       uvs[b+7] = new Vector2(1f, t);
        }

        // ── Connect consecutive slices ────────────────────────────────────────
        //   Winding follows the same convention as CorkscrewGenerator.
        //   Because +X is constant, the winding stays correct at all loop angles
        //   including the inverted apex section.

        for (int i = 0; i < numSteps; i++)
        {
            int a = i * 8, b = (i + 1) * 8;

            // Top face (player surface — normal = inward)
            tris.Add(a+0); tris.Add(b+0); tris.Add(b+1);
            tris.Add(a+0); tris.Add(b+1); tris.Add(a+1);

            // Bottom face (underside — normal = outward)
            tris.Add(a+2); tris.Add(a+3); tris.Add(b+3);
            tris.Add(a+2); tris.Add(b+3); tris.Add(b+2);

            // Left side (−X face)
            tris.Add(a+4); tris.Add(a+5); tris.Add(b+5);
            tris.Add(a+4); tris.Add(b+5); tris.Add(b+4);

            // Right side (+X face)
            tris.Add(a+6); tris.Add(b+7); tris.Add(a+7);
            tris.Add(a+6); tris.Add(b+6); tris.Add(b+7);
        }

        // ── End caps ──────────────────────────────────────────────────────────
        //
        //  At t=0: centre=(0, 0, 0),             tangent=+Z → cap faces −Z.
        //  At t=1: centre=(sideOffset, rise, 0),  tangent=+Z → cap faces +Z.
        //  Both are flat horizontal slabs centred on their respective lane.

        int cap = slices * 8;

        // Entry cap  (faces −Z)
        verts[cap+0] = new Vector3(-halfW,  halfT, 0f);
        verts[cap+1] = new Vector3( halfW,  halfT, 0f);
        verts[cap+2] = new Vector3(-halfW, -halfT, 0f);
        verts[cap+3] = new Vector3( halfW, -halfT, 0f);
        for (int k = 0; k < 4; k++) { norms[cap+k] = Vector3.back; uvs[cap+k] = Vector2.zero; }
        tris.Add(cap+0); tris.Add(cap+2); tris.Add(cap+1);
        tris.Add(cap+1); tris.Add(cap+2); tris.Add(cap+3);

        // Exit cap  (faces +Z, shifted by sideOffset in X and spiralOffset in Y)
        float ex = sideOffset, ey = spiralOffset;
        verts[cap+4] = new Vector3(ex - halfW, ey + halfT, 0f);
        verts[cap+5] = new Vector3(ex + halfW, ey + halfT, 0f);
        verts[cap+6] = new Vector3(ex - halfW, ey - halfT, 0f);
        verts[cap+7] = new Vector3(ex + halfW, ey - halfT, 0f);
        for (int k = 4; k < 8; k++) { norms[cap+k] = Vector3.forward; uvs[cap+k] = Vector2.one; }
        tris.Add(cap+4); tris.Add(cap+5); tris.Add(cap+6);
        tris.Add(cap+5); tris.Add(cap+7); tris.Add(cap+6);

        ApplyMesh(verts, norms, uvs, tris.ToArray());
    }

    void ApplyMesh(Vector3[] v, Vector3[] n, Vector2[] u, int[] t)
    {
        var mesh = new Mesh { name = "LoopMesh" };
        if (v.Length > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = v; mesh.normals = n; mesh.uv = u; mesh.triangles = t;
        mesh.RecalculateBounds();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        var mc = GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = mesh;
        var mr = GetComponent<MeshRenderer>();
        if (roadMaterial != null) mr.sharedMaterial = roadMaterial;
#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        Debug.Log($"[LoopGenerator] {v.Length} verts, {t.Length / 3} tris. " +
                  $"Apex: {loopRadius * 2f:F1} m.  Exit: x={sideOffset:F1} y={spiralOffset:F2}.", this);
    }

    public void Clear()
    {
        GetComponent<MeshFilter>().sharedMesh = null;
        GetComponent<MeshCollider>().sharedMesh = null;
#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
    }

    #endregion
    #region Gizmos

    private void OnDrawGizmos()
    {
        if (loopRadius <= 0f) return;

        float halfW = trackWidth * 0.5f;
        const int STEPS = 64;

        Vector3 prevL = Vector3.zero, prevR = Vector3.zero;

        for (int i = 0; i <= STEPS; i++)
        {
            float t     = (float)i / STEPS;
            float theta = -Mathf.PI * 0.5f + t * Mathf.PI * 2f;
            float rise  = t * spiralOffset;

            Vector3 c = transform.TransformPoint(new Vector3(
                t * sideOffset,
                loopRadius + loopRadius * Mathf.Sin(theta) + rise,
                loopRadius * Mathf.Cos(theta)));

            Vector3 L = c - transform.right * halfW;
            Vector3 R = c + transform.right * halfW;

            if (i > 0)
            {
                Gizmos.color = Color.Lerp(Color.yellow, Color.cyan, t);
                Gizmos.DrawLine(prevL, L);
                Gizmos.DrawLine(prevR, R);
                if (i % 8 == 0) Gizmos.DrawLine(L, R);
            }
            prevL = L; prevR = R;
        }

        // Entry marker (green)
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.3f);
        Gizmos.DrawRay(transform.position, transform.forward * 3f);

        // Exit marker (red) — shifted in X and Y
        Vector3 exit = transform.TransformPoint(new Vector3(sideOffset, spiralOffset, 0f));
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(exit, 0.3f);
        Gizmos.DrawRay(exit, transform.forward * 3f);

        // Apex marker (orange) — midpoint of X drift, top of loop
        Vector3 apex = transform.TransformPoint(new Vector3(sideOffset * 0.5f, loopRadius * 2f + spiralOffset * 0.5f, 0f));
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f);
        Gizmos.DrawWireSphere(apex, 0.5f);
    }

    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(LoopGenerator))]
public class LoopGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Loop Tools", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("⚙  Generate Loop", GUILayout.Height(32)))
            { var g = (LoopGenerator)target; Undo.RecordObject(g, "Generate Loop"); g.Generate(); }
            EditorGUILayout.Space(2);
            var prev = GUI.backgroundColor; GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("🗑  Clear Mesh", GUILayout.Height(26)))
            { if (EditorUtility.DisplayDialog("Clear", "Remove mesh?", "Clear", "Cancel")) { Undo.RecordObject(target, "Clear"); ((LoopGenerator)target).Clear(); } }
            GUI.backgroundColor = prev;
        }
        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "Vertical loop-de-loop — centre path is a circle in the YZ plane.\n" +
            "Side Offset shifts the exit lane in X so entry and exit don't block each other.\n" +
            "Set Side Offset ≥ Track Width for complete lane separation.",
            MessageType.Info);
    }
}
#endif
