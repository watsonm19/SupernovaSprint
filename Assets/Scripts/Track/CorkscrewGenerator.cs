// ═════════════════════════════════════════════════════════════════════════════
//  CorkscrewGenerator.cs
//  Generates a custom mesh corkscrew road section.
//
//  The road centre travels in a HELIX — it physically orbits around the Z axis
//  at a given radius as it advances forward, producing the classic roller-coaster
//  corkscrew shape.
//
//  See BarrelRollGenerator for an inline 360° roll where the centre goes straight.
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
public class CorkscrewGenerator : MonoBehaviour
{
    [Header("── Shape ───────────────────────────────────────────────────────")]
    [Tooltip("Total Z distance the corkscrew covers (metres).")]
    public float forwardLength = 40f;

    [Tooltip("How far the road centre orbits away from the Z axis (metres).\n" +
             "The track reaches 2 × Radius above entry at the apex.\n" +
             "Keep at least half of Track Width to avoid self-intersection.")]
    public float radius = 5f;

    [Tooltip("Width of the road ribbon (metres).")]
    public float trackWidth = 8f;

    [Tooltip("Thickness of the road slab (metres).")]
    public float trackThickness = 0.5f;

    [Tooltip("Cross-section slices along the corkscrew. More = smoother curve.")]
    [Range(8, 128)]
    public int numSteps = 64;

    [Tooltip("Roll to the right first when true.")]
    public bool rollRight = true;

    [Header("── Visual ───────────────────────────────────────────────────────")]
    public Material roadMaterial;

    // ─────────────────────────────────────────────────────────────────────────
    #region Generation

    public void Generate()
    {
        if (forwardLength <= 0f || radius <= 0f)
        {
            Debug.LogError("[CorkscrewGenerator] Forward Length and Radius must be > 0.", this);
            return;
        }

        float dir   = rollRight ? -1f : 1f;
        float halfW = trackWidth     * 0.5f;
        float halfT = trackThickness * 0.5f;
        int   slices = numSteps + 1;

        var verts = new Vector3[slices * 8 + 8];
        var norms = new Vector3[slices * 8 + 8];
        var uvs   = new Vector2[slices * 8 + 8];
        var tris  = new List<int>(numSteps * 24 + 12);

        for (int i = 0; i < slices; i++)
        {
            float t      = (float)i / numSteps;
            float tEased = t * t * (3f - 2f * t);
            float theta  = dir * tEased * Mathf.PI * 2f;
            float cosT   = Mathf.Cos(theta);
            float sinT   = Mathf.Sin(theta);

            Vector3 centre = new Vector3(radius * sinT, radius * (1f - cosT), t * forwardLength);
            Vector3 normal = new Vector3(-sinT, cosT, 0f);
            Vector3 right  = new Vector3( cosT, sinT, 0f);

            Vector3 tl = centre + normal * halfT - right * halfW;
            Vector3 tr = centre + normal * halfT + right * halfW;
            Vector3 bl = centre - normal * halfT - right * halfW;
            Vector3 br = centre - normal * halfT + right * halfW;

            int b = i * 8;
            verts[b+0] = tl; norms[b+0] =  normal; uvs[b+0] = new Vector2(0f, t);
            verts[b+1] = tr; norms[b+1] =  normal; uvs[b+1] = new Vector2(1f, t);
            verts[b+2] = bl; norms[b+2] = -normal; uvs[b+2] = new Vector2(0f, t);
            verts[b+3] = br; norms[b+3] = -normal; uvs[b+3] = new Vector2(1f, t);
            verts[b+4] = tl; norms[b+4] = -right;  uvs[b+4] = new Vector2(0f, t);
            verts[b+5] = bl; norms[b+5] = -right;  uvs[b+5] = new Vector2(1f, t);
            verts[b+6] = tr; norms[b+6] =  right;  uvs[b+6] = new Vector2(0f, t);
            verts[b+7] = br; norms[b+7] =  right;  uvs[b+7] = new Vector2(1f, t);
        }

        for (int i = 0; i < numSteps; i++)
        {
            int a = i * 8, b = (i + 1) * 8;
            tris.Add(a+0); tris.Add(b+0); tris.Add(b+1);
            tris.Add(a+0); tris.Add(b+1); tris.Add(a+1);
            tris.Add(a+2); tris.Add(a+3); tris.Add(b+3);
            tris.Add(a+2); tris.Add(b+3); tris.Add(b+2);
            tris.Add(a+4); tris.Add(a+5); tris.Add(b+5);
            tris.Add(a+4); tris.Add(b+5); tris.Add(b+4);
            tris.Add(a+6); tris.Add(b+7); tris.Add(a+7);
            tris.Add(a+6); tris.Add(b+6); tris.Add(b+7);
        }

        int cap = slices * 8;
        Vector3[] capCorners(float z, Vector3 fwd)
        {
            return new[] {
                new Vector3(-halfW,  halfT, z),
                new Vector3( halfW,  halfT, z),
                new Vector3(-halfW, -halfT, z),
                new Vector3( halfW, -halfT, z)
            };
        }
        var fc = capCorners(0f, Vector3.back);
        for (int k = 0; k < 4; k++) { verts[cap+k] = fc[k]; norms[cap+k] = Vector3.back; uvs[cap+k] = Vector2.zero; }
        tris.Add(cap+0); tris.Add(cap+2); tris.Add(cap+1);
        tris.Add(cap+1); tris.Add(cap+2); tris.Add(cap+3);

        var bc = capCorners(forwardLength, Vector3.forward);
        for (int k = 0; k < 4; k++) { verts[cap+4+k] = bc[k]; norms[cap+4+k] = Vector3.forward; uvs[cap+4+k] = Vector2.one; }
        tris.Add(cap+4); tris.Add(cap+5); tris.Add(cap+6);
        tris.Add(cap+5); tris.Add(cap+7); tris.Add(cap+6);

        ApplyMesh(verts, norms, uvs, tris.ToArray());
    }

    void ApplyMesh(Vector3[] v, Vector3[] n, Vector2[] u, int[] t)
    {
        var mesh = new Mesh { name = "CorkscrewMesh" };
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
        Debug.Log($"[CorkscrewGenerator] {v.Length} verts, {t.Length / 3} tris. Apex: {radius * 2f:F1} m.", this);
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
        if (forwardLength <= 0f || radius <= 0f) return;
        float dir = rollRight ? -1f : 1f;
        float halfW = trackWidth * 0.5f;
        Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
        for (int i = 0; i <= 64; i++)
        {
            float t = (float)i / 64;
            float tE = t * t * (3f - 2f * t);
            float theta = dir * tE * Mathf.PI * 2f;
            Vector3 c = transform.TransformPoint(new Vector3(radius * Mathf.Sin(theta), radius * (1f - Mathf.Cos(theta)), t * forwardLength));
            Vector3 r = transform.TransformDirection(new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0f));
            Vector3 L = c - r * halfW, R = c + r * halfW;
            if (i > 0) { Gizmos.color = Color.Lerp(Color.yellow, Color.cyan, t); Gizmos.DrawLine(prevL, L); Gizmos.DrawLine(prevR, R); if (i % 8 == 0) Gizmos.DrawLine(L, R); }
            prevL = L; prevR = R;
        }
        Gizmos.color = Color.green; Gizmos.DrawSphere(transform.position, 0.3f); Gizmos.DrawRay(transform.position, transform.forward * 3f);
        Vector3 exit = transform.TransformPoint(new Vector3(0f, 0f, forwardLength));
        Gizmos.color = Color.red; Gizmos.DrawSphere(exit, 0.3f); Gizmos.DrawRay(exit, transform.forward * 3f);
    }

    #endregion
}

#if UNITY_EDITOR
[CustomEditor(typeof(CorkscrewGenerator))]
public class CorkscrewGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Corkscrew Tools", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledGroupScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("⚙  Generate Corkscrew", GUILayout.Height(32)))
            { var g = (CorkscrewGenerator)target; Undo.RecordObject(g, "Generate Corkscrew"); g.Generate(); }
            EditorGUILayout.Space(2);
            var prev = GUI.backgroundColor; GUI.backgroundColor = new Color(1f, 0.55f, 0.55f);
            if (GUILayout.Button("🗑  Clear Mesh", GUILayout.Height(26)))
            { if (EditorUtility.DisplayDialog("Clear", "Remove mesh?", "Clear", "Cancel")) { Undo.RecordObject(target, "Clear"); ((CorkscrewGenerator)target).Clear(); } }
            GUI.backgroundColor = prev;
        }
    }
}
#endif
