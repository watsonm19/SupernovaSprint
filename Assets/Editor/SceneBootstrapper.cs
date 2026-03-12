// ═════════════════════════════════════════════════════════════════════════════
//  SceneBootstrapper.cs                                               [Editor]
//
//  Builds the Supernova Sprint test scene directly as scene GameObjects.
//  No prefab creation step — that was the cause of the previous silent crash.
//
//  HOW IT WORKS:
//    [InitializeOnLoad] fires after every domain reload (compile, project open).
//    Guard: if "[SupernovaSprint]" root already exists → skip (idempotent).
//    First run → dialog → build → save scene → persists across project opens.
//
//  MANUAL REBUILD:  Menu → Supernova Sprint → ⚡ Rebuild Test Scene
// ═════════════════════════════════════════════════════════════════════════════

using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SceneBootstrapper
{
    const string ROOT_NAME        = "[SupernovaSprint]";
    const string SCENE_NOTE       = "Supernova Sprint / ⚡ Rebuild Test Scene";
    const string TITLE_SCENE_PATH = "Assets/Scenes/TitleScreen.unity";
    const string GAME_SCENE_PATH  = "Assets/Scenes/StrangeWorld.unity";

    // Used by BuildKillPlane to centre the trigger under the level.
    const float TRACK_LEN   = 160f;

    // ── Auto-setup on project/domain load ────────────────────────────────────

    static SceneBootstrapper()
    {
        EditorApplication.delayCall += TryAutoSetup;
    }

    static void TryAutoSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (BuildPipeline.isBuildingPlayer) return;
        if (EditorSceneManager.GetActiveScene().path == TITLE_SCENE_PATH) return; // Never touch the title scene
        if (GameObject.Find(ROOT_NAME) != null) return; // Already built

        bool go = EditorUtility.DisplayDialog(
            "Supernova Sprint — First Time Setup",
            "No test scene found in this scene.\n\n" +
            "Build the player, track, and camera now?\n\n" +
            "(You can always rebuild from: " + SCENE_NOTE + ")",
            "Build It", "Skip");

        if (go) BuildScene();
    }

    // ── Menu items ────────────────────────────────────────────────────────────

    [MenuItem("Supernova Sprint/⚡ Rebuild Test Scene", priority = 0)]
    static void MenuRebuild()
    {
        if (!EditorUtility.DisplayDialog("Rebuild Test Scene",
            "Destroy existing scene content and rebuild?\n" +
            "Any manual edits to the scene will be lost.",
            "Rebuild", "Cancel")) return;

        var old = GameObject.Find(ROOT_NAME);
        if (old != null) Undo.DestroyObjectImmediate(old);

        BuildScene();
    }

    [MenuItem("Supernova Sprint/⚡ Rebuild Test Scene", validate = true)]
    static bool MenuRebuildValidate() =>
        !EditorApplication.isPlaying &&
        EditorSceneManager.GetActiveScene().path != TITLE_SCENE_PATH;

    [MenuItem("Supernova Sprint/⚡ Rebuild Title Screen", priority = 1)]
    static void MenuBuildTitleScreen()
    {
        if (!EditorUtility.DisplayDialog("Build Title Screen",
            "Create/rebuild the title screen scene at " + TITLE_SCENE_PATH + "?",
            "Build", "Cancel")) return;

        BuildTitleScene();
    }

    [MenuItem("Supernova Sprint/⚡ Rebuild Title Screen", validate = true)]
    static bool MenuBuildTitleScreenValidate() => !EditorApplication.isPlaying;

    // ─────────────────────────────────────────────────────────────────────────
    //  CORE BUILD
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildScene()
    {
        try
        {
            // ── Project setup ─────────────────────────────────────────────────
            int groundLayer = EnsureLayer("Ground");
            EnsureTag("Target");
            EnsureTag("Player");
            EnsureMaterialFolder();
            AssetDatabase.SaveAssets();

            // ── Materials ─────────────────────────────────────────────────────
            Material matPlayer = Mat("Mat_Player", new Color(0.90f, 0.40f, 0.10f));
            Material matVisor  = Mat("Mat_Visor",  new Color(0.30f, 0.85f, 1.00f));
            Material matTarget = Mat("Mat_Target", new Color(1.00f, 0.85f, 0.05f),
                                      emission: new Color(0.8f, 0.6f, 0f) * 2f);

            // ── Scene root ────────────────────────────────────────────────────
            var root = new GameObject(ROOT_NAME);
            Undo.RegisterCreatedObjectUndo(root, "Build Supernova Test Scene");

            // ── Homing targets ────────────────────────────────────────────────
            BuildTargets(root.transform, matTarget);

            // ── Player ────────────────────────────────────────────────────────
            GameObject player = BuildPlayer(root.transform, groundLayer, matPlayer, matVisor);
            player.transform.position = new Vector3(0f, 1f, 3f);

            // ── Camera ────────────────────────────────────────────────────────
            GameObject camGO = SetupCamera(root.transform, player.transform, groundLayer);

            // ── HUD ───────────────────────────────────────────────────────────
            SupernovaHUD hud = BuildHUD(root.transform, player);

            // ── Fail Screen (kill plane) ──────────────────────────────────────
            var failGO = new GameObject("FailScreen");
            failGO.transform.SetParent(root.transform, false);
            var failScreen = failGO.AddComponent<FailScreen>();

            // ── Time Fail Screen (timer expiry) ───────────────────────────────
            var timeFailGO = new GameObject("TimeFailScreen");
            timeFailGO.transform.SetParent(root.transform, false);
            hud.timeFailScreen = timeFailGO.AddComponent<TimeFailScreen>();

            // ── Kill plane / respawn manager ──────────────────────────────────
            BuildKillPlane(root.transform, player, camGO, hud, failScreen);

            // ── Wire controller references ────────────────────────────────────
            var ctrl = player.GetComponent<SupernovaSprintController>();
            if (ctrl != null)
            {
                // ── References ────────────────────────────────────────────────
                ctrl.cameraTransform = camGO.transform;
                ctrl.visualModel     = player.transform.Find("Visual");
                ctrl.groundLayers    = 1 << groundLayer;

                EditorUtility.SetDirty(player);
            }
            else
            {
                Debug.LogWarning(
                    "[Supernova Sprint] SupernovaSprintController not found on player. " +
                    "Check the Console for compile errors — fix them and run Rebuild.");
            }

            // ── Pause Manager ─────────────────────────────────────────────────
            var pauseGO = new GameObject("PauseManager");
            pauseGO.transform.SetParent(root.transform, false);
            pauseGO.AddComponent<PauseManager>();

            // ── Earthquake intro shake ─────────────────────────────────────────
            var eqGO = new GameObject("EarthquakeIntro");
            eqGO.transform.SetParent(root.transform, false);
            var eq = eqGO.AddComponent<EarthquakeIntro>();
            eq.earthquakeVolume = 1.25f;
            var eqGuids = AssetDatabase.FindAssets("earthquake t:AudioClip", new[] { "Assets/Audio/SFX" });
            if (eqGuids.Length > 0)
                eq.earthquakeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(eqGuids[0]));

            // ── Lighting ──────────────────────────────────────────────────────
            SetupLighting();

            // ── Save ─────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            // Frame the player in the Scene view
            Selection.activeGameObject = player;
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.FrameSelected();

            UpdateBuildSettings();
            Debug.Log("[Supernova Sprint] Scene built and saved. Press Play to test!");
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Supernova Sprint] Build failed:\n" + e.Message + "\n\n" + e.StackTrace +
                "\n\nFix any compile errors in the Console, then run " + SCENE_NOTE);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TRACK
    // ─────────────────────────────────────────────────────────────────────────
    //  TARGETS
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildTargets(Transform parent, Material mat)
    {
        var tRoot = new GameObject("Targets");
        tRoot.transform.SetParent(parent, false);

        Vector3[] positions =
        {
            new Vector3( 0f, 3f,  85f),
            new Vector3( 3f, 5f, 100f),
            new Vector3(-3f, 4f, 115f),
            new Vector3( 2f, 6f, 130f),
        };

        for (int i = 0; i < positions.Length; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Target_{i}";
            go.tag  = "Target";
            go.transform.SetParent(tRoot.transform, true);
            go.transform.position   = positions[i];
            go.transform.localScale = Vector3.one * 1.2f;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PLAYER
    // ─────────────────────────────────────────────────────────────────────────
    //
    //  CapsuleCollider: center=(0,1,0), height=2 → bottom at transform.position.y
    //  So player.y = 1 means capsule bottom is at y=1 (settles on ground surface at y=0.25)

    const string PLAYER_PREFAB_PATH = "Assets/Prefabs/Player/Player.prefab";

    static GameObject BuildPlayer(Transform parent, int groundLayer,
                                   Material matBody, Material matVisor)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);

        if (prefab != null)
        {
            // ── Instantiate from saved prefab ─────────────────────────────────
            //  PrefabUtility keeps the prefab connection so changes to the prefab
            //  propagate to the scene instance automatically.
            var playerGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            playerGO.tag = "Player";
            Debug.Log("[Supernova Sprint] Player instantiated from prefab at " + PLAYER_PREFAB_PATH);
            return playerGO;
        }

        // ── Fallback: build placeholder from primitives ───────────────────────
        //  Runs only when no prefab exists yet (first-time setup).
        //  To use your own player model: save the Player as a prefab at
        //  Assets/Prefabs/Player/Player.prefab and rebuild the scene.
        Debug.LogWarning("[Supernova Sprint] No player prefab found at " + PLAYER_PREFAB_PATH +
                         ". Building placeholder — drag the Player to Assets/Prefabs/Player/ to use your own model.");

        var go = new GameObject("Player");
        go.tag = "Player";
        go.transform.SetParent(parent, false);

        go.AddComponent<SupernovaSprintController>();

        var rb            = go.GetComponent<Rigidbody>();
        rb.useGravity     = false;
        rb.linearDamping  = 0f;
        rb.angularDamping = 0f;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        rb.constraints    = RigidbodyConstraints.FreezeRotation;

        var col       = go.AddComponent<CapsuleCollider>();
        col.center    = new Vector3(0f, 1f, 0f);
        col.height    = 2f;
        col.radius    = 0.4f;
        col.direction = 1;

        var visual = new GameObject("Visual");
        visual.transform.SetParent(go.transform, false);

        MeshChild(visual.transform, PrimitiveType.Cylinder, "Body",
            new Vector3(0f, 1f, 0f), new Vector3(0.5f, 1f, 0.5f), matBody);
        MeshChild(visual.transform, PrimitiveType.Sphere, "Helmet",
            new Vector3(0f, 2.35f, 0f), Vector3.one * 0.55f, matBody);
        MeshChild(visual.transform, PrimitiveType.Sphere, "Visor",
            new Vector3(0f, 2.35f, 0.23f), new Vector3(0.40f, 0.28f, 0.12f), matVisor);
        MeshChild(visual.transform, PrimitiveType.Cube, "Jetpack",
            new Vector3(0f, 1.3f, -0.28f), new Vector3(0.40f, 0.52f, 0.18f), matBody);

        return go;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CAMERA
    // ─────────────────────────────────────────────────────────────────────────

    static GameObject SetupCamera(Transform parent, Transform playerTarget, int groundLayer)
    {
        // Reuse the existing Main Camera if present (avoids duplicate AudioListeners).
        Camera main  = Camera.main;
        var    camGO = main != null ? main.gameObject : MakeMainCamera();

        camGO.transform.SetParent(parent, true);

        var tpc = camGO.GetComponent<ThirdPersonCamera>()
               ?? camGO.AddComponent<ThirdPersonCamera>();

        tpc.target          = playerTarget;
        tpc.collisionLayers = 1 << groundLayer;

        var cam = camGO.GetComponent<Camera>();
        if (cam != null) cam.farClipPlane = 10000f;

        // Start behind and above the player so the first frame looks right.
        camGO.transform.position = playerTarget.position + new Vector3(0f, 5f, -10f);
        camGO.transform.LookAt(playerTarget.position + Vector3.up * 1.4f);

        return camGO;
    }

    static GameObject MakeMainCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.AddComponent<Camera>();
        go.AddComponent<AudioListener>();
        return go;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HUD
    // ─────────────────────────────────────────────────────────────────────────
    //
    //  Creates a Screen Space – Overlay canvas with:
    //    • TimerText  — top-left,  anchored to (0,1)
    //    • SpeedText  — top-right, anchored to (1,1)
    //  and wires them into SupernovaHUD on the canvas root.

    static SupernovaHUD BuildHUD(Transform parent, GameObject player)
    {
        // ── Canvas ────────────────────────────────────────────────────────────
        var canvasGO = new GameObject("HUD_Canvas");
        canvasGO.transform.SetParent(parent, false);

        var canvas          = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler                  = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight   = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Timer — top-left ──────────────────────────────────────────────────
        var timerTMP = MakeTMPLabel(canvasGO.transform, "TimerText",
            anchorMin:        new Vector2(0f, 1f),
            anchorMax:        new Vector2(0f, 1f),
            pivot:            new Vector2(0f, 1f),
            anchoredPosition: new Vector2(24f, -20f),
            sizeDelta:        new Vector2(280f, 56f),
            text:             "00:00:00",
            alignment:        TextAlignmentOptions.TopLeft);

        // ── Speedometer — top-right ───────────────────────────────────────────
        var speedTMP = MakeTMPLabel(canvasGO.transform, "SpeedText",
            anchorMin:        new Vector2(1f, 1f),
            anchorMax:        new Vector2(1f, 1f),
            pivot:            new Vector2(1f, 1f),
            anchoredPosition: new Vector2(-24f, -20f),
            sizeDelta:        new Vector2(240f, 56f),
            text:             "0 M/S",
            alignment:        TextAlignmentOptions.TopRight);

        // ── SupernovaHUD component ────────────────────────────────────────────
        var hud                = canvasGO.AddComponent<SupernovaHUD>();
        hud.timerText          = timerTMP;
        hud.speedText          = speedTMP;
        hud.playerController   = player.GetComponent<SupernovaSprintController>();
        hud.maxDisplaySpeed    = 31.6f;

        EditorUtility.SetDirty(canvasGO);
        return hud;
    }

    // Creates a TextMeshProUGUI label as a child of parent with the given rect settings.
    static TextMeshProUGUI MakeTMPLabel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta,
        string text, TextAlignmentOptions alignment)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);

        var tmp             = go.AddComponent<TextMeshProUGUI>();
        tmp.text            = text;
        tmp.fontSize        = 36f;
        tmp.fontStyle       = FontStyles.Bold;
        tmp.color           = Color.white;
        tmp.alignment       = alignment;

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta        = sizeDelta;

        return tmp;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  KILL PLANE
    // ─────────────────────────────────────────────────────────────────────────
    //
    //  A large trigger box well below the track.  Any object tagged "Player"
    //  that enters it triggers the SupernovaRespawnManager respawn sequence.

    static void BuildKillPlane(Transform parent, GameObject player, GameObject camGO,
                                SupernovaHUD hud, FailScreen failScreen)
    {
        var kp = new GameObject("KillPlane");
        kp.transform.SetParent(parent, false);
        kp.transform.position = new Vector3(0f, -30f, TRACK_LEN * 0.5f);

        // A wide, shallow trigger box — catches the player wherever they fall off.
        var col      = kp.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size      = new Vector3(500f, 5f, 500f);

        // Wire the respawn manager to the player, camera, and fail screen.
        var rm = kp.AddComponent<SupernovaRespawnManager>();
        rm.playerController  = player.GetComponent<SupernovaSprintController>();
        rm.thirdPersonCamera = camGO.GetComponent<ThirdPersonCamera>();
        rm.hud               = hud;
        rm.failScreen        = failScreen;

        EditorUtility.SetDirty(kp);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LIGHTING
    // ─────────────────────────────────────────────────────────────────────────

    static void SetupLighting()
    {
        Light dir = UnityEngine.Object.FindFirstObjectByType<Light>();
        if (dir == null || dir.type != LightType.Directional)
        {
            dir = new GameObject("Directional Light").AddComponent<Light>();
            dir.type = LightType.Directional;
        }

        dir.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
        dir.intensity          = 1.1f;
        dir.color              = new Color(1f, 0.95f, 0.85f);

        RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.22f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    static void MakeCube(string name, Transform parent, int layer, Material mat,
                          Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.layer = layer;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    // Adds a mesh-only child (collider removed — physics lives on the player root).
    static void MeshChild(Transform parent, PrimitiveType type, string name,
                           Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = localScale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (col != null) UnityEngine.Object.DestroyImmediate(col);
    }

    // Creates or loads a material saved as a .mat asset.
    static Material Mat(string name, Color baseColor, Color? emission = null)
    {
        string path = $"Assets/Materials/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader) { name = name };
        mat.SetColor("_BaseColor", baseColor);

        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureMaterialFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
    }

    static int EnsureLayer(string layerName)
    {
        int idx = LayerMask.NameToLayer(layerName);
        if (idx != -1) return idx;

        var tm = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tm.ApplyModifiedProperties();
                Debug.Log($"[Supernova Sprint] Created layer '{layerName}' at index {i}.");
                return i;
            }
        }

        Debug.LogError("[Supernova Sprint] No free layer slots. Using Default layer (0).");
        return 0;
    }

    static void EnsureTag(string tagName)
    {
        foreach (string t in UnityEditorInternal.InternalEditorUtility.tags)
            if (t == tagName) return;

        var tm = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tm.FindProperty("tags");
        int idx  = tags.arraySize;
        tags.InsertArrayElementAtIndex(idx);
        tags.GetArrayElementAtIndex(idx).stringValue = tagName;
        tm.ApplyModifiedProperties();
        Debug.Log($"[Supernova Sprint] Added tag '{tagName}'.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TITLE SCREEN
    // ─────────────────────────────────────────────────────────────────────────

    static void BuildTitleScene()
    {
        try
        {
            EnsureScenesFolder();

            // Create the title scene additively so the current scene is untouched,
            // then explicitly set it as active so new GameObjects land inside it.
            var titleScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(titleScene);

            // ── Camera ────────────────────────────────────────────────────────
            var camGO           = new GameObject("Main Camera");
            camGO.tag           = "MainCamera";
            var cam             = camGO.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            camGO.AddComponent<AudioListener>();

            // ── TitleScreen controller ────────────────────────────────────────
            new GameObject("TitleScreen").AddComponent<TitleScreen>();

            // ── Save ─────────────────────────────────────────────────────────
            EditorSceneManager.SaveScene(titleScene, TITLE_SCENE_PATH);
            EditorSceneManager.CloseScene(titleScene, true);

            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Supernova Sprint] Title screen built at " + TITLE_SCENE_PATH);
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[Supernova Sprint] Title screen build failed:\n" +
                e.Message + "\n\n" + e.StackTrace);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BUILD SETTINGS
    // ─────────────────────────────────────────────────────────────────────────
    //  Ensures scene order: 0 = TitleScreen, 1 = TestTrack (game).
    //  Called after building either scene so they stay in sync.

    static void UpdateBuildSettings()
    {
        bool hasTitle = AssetDatabase.LoadAssetAtPath<SceneAsset>(TITLE_SCENE_PATH) != null;
        bool hasGame  = AssetDatabase.LoadAssetAtPath<SceneAsset>(GAME_SCENE_PATH)  != null;

        int count = (hasTitle ? 1 : 0) + (hasGame ? 1 : 0);
        var scenes = new EditorBuildSettingsScene[count];
        int i = 0;
        if (hasTitle) scenes[i++] = new EditorBuildSettingsScene(TITLE_SCENE_PATH, true);
        if (hasGame)  scenes[i++] = new EditorBuildSettingsScene(GAME_SCENE_PATH,  true);

        EditorBuildSettings.scenes = scenes;
        Debug.Log($"[Supernova Sprint] Build settings updated — {count} scene(s) registered.");
    }

    static void EnsureScenesFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
    }
}
