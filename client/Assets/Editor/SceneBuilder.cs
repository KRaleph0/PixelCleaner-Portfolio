using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.XR.ARFoundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 메뉴: PixelCleaners → 씬 생성
/// ARScene / MapScene / FactoryScene 을 Assets/Scenes/ 에 생성하고
/// Build Settings에 자동 등록합니다.
/// </summary>
public static class SceneBuilder
{
    const string SceneDir        = "Assets/Scenes";
    const string XROriginPrefabPath =
        "Assets/Samples/XR Interaction Toolkit/3.3.0/AR Starter Assets/Prefabs/XR Origin (AR Rig).prefab";

    // PixelUI 프리팹 경로
    const string ButtonAPrefabPath      = "Assets/Prefabs/Buttons/ButtonA.prefab";
    const string PanelFramePrefabPath   = "Assets/Prefabs/Panels/PanelFrame.prefab";
    const string PanelInnerPrefabPath   = "Assets/Prefabs/Panels/PanelInner.prefab";
    const string SimpleBarPrefabPath    = "Assets/Prefabs/ValueBars/SimpleBar.prefab";
    const string GridSlotPrefabPath     = "Assets/Prefabs/Grid/GridSlot.prefab";
    const string FontAssetPath          = "Assets/Resources/Fonts/PF스타더스트 3.0 SDF.asset";

    [MenuItem("PixelCleaners/씬 생성 (전체 7개)", validate = true)]
    public static bool BuildAllScenesValidate() => !UnityEditor.EditorApplication.isPlaying;

    [MenuItem("PixelCleaners/씬 생성 (전체 7개)")]
    public static void BuildAllScenes()
    {
        EnsureDirectory(SceneDir);

        BuildLoginScene();
        BuildARScene();
        BuildMapScene();
        BuildFactoryScene();
        BuildCreatureScene();
        BuildBagScene();
        BuildDeliveryScene();
        RegisterBuildSettings();

        AssetDatabase.Refresh();
        Debug.Log("[SceneBuilder] 씬 7개 생성 및 Build Settings 등록 완료.");
    }

    // ── LoginScene ───────────────────────────────────────────────
    // 앱 최초 진입점. 등록된 player_id가 있으면 LoginSceneSetup이 즉시 Map으로 넘긴다.

    static void BuildLoginScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddEventSystem();
        new GameObject("LoginSceneSetup").AddComponent<LoginSceneSetup>();
        // 내비게이션 바 없음 — 로그인 전에는 다른 씬으로 갈 수 없다.

        SaveScene(scene, $"{SceneDir}/LoginScene.unity");
    }

    // ── ARScene ──────────────────────────────────────────────────

    static void BuildARScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightGO = new GameObject("Directional Light");
        var light   = lightGO.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1f;
        lightGO.transform.eulerAngles = new Vector3(50f, -30f, 0f);

        AddEventSystem();

        var arSessionGO = new GameObject("ARSession");
        arSessionGO.AddComponent<ARSession>();
        arSessionGO.AddComponent<ARInputManager>();

        new GameObject("ARSceneSetup").AddComponent<ARSceneSetup>();

        AddNavCanvas("NavCanvas",
            ("지도",   NavButtonSetter.Destination.Map),
            ("공장",   NavButtonSetter.Destination.Factory),
            ("납품",   NavButtonSetter.Destination.Delivery),
            ("생명체", NavButtonSetter.Destination.Creature),
            ("가방",   NavButtonSetter.Destination.Bag));

        TryAddPrefab(XROriginPrefabPath, "XR Origin (AR Rig)");

        SaveScene(scene, $"{SceneDir}/ARScene.unity");
    }

    // ── MapScene ─────────────────────────────────────────────────

    static void BuildMapScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddEventSystem();
        new GameObject("MapSceneSetup").AddComponent<MapSceneSetup>();

        AddNavCanvas("NavCanvas",
            ("지도",   NavButtonSetter.Destination.Map),
            ("공장",   NavButtonSetter.Destination.Factory),
            ("납품",   NavButtonSetter.Destination.Delivery),
            ("생명체", NavButtonSetter.Destination.Creature),
            ("가방",   NavButtonSetter.Destination.Bag));

        SaveScene(scene, $"{SceneDir}/MapScene.unity");
    }

    // ── FactoryScene ─────────────────────────────────────────────

    static void BuildFactoryScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddEventSystem();

        var setupGO = new GameObject("FactorySceneSetup");
        var setup   = setupGO.AddComponent<FactorySceneSetup>();

        var gridSlotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GridSlotPrefabPath);
        if (gridSlotPrefab != null)
        {
            var so   = new SerializedObject(setup);
            var prop = so.FindProperty("facilitySlotPrefab");
            if (prop != null)
            {
                prop.objectReferenceValue = gridSlotPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        else
        {
            Debug.LogWarning($"[SceneBuilder] GridSlot 프리팹 없음: {GridSlotPrefabPath}");
        }

        AddNavCanvas("NavCanvas",
            ("지도",   NavButtonSetter.Destination.Map),
            ("공장",   NavButtonSetter.Destination.Factory),
            ("납품",   NavButtonSetter.Destination.Delivery),
            ("생명체", NavButtonSetter.Destination.Creature),
            ("가방",   NavButtonSetter.Destination.Bag));

        SaveScene(scene, $"{SceneDir}/FactoryScene.unity");
    }

    // ── CreatureScene ────────────────────────────────────────────

    static void BuildCreatureScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddEventSystem();
        new GameObject("CreatureSceneSetup").AddComponent<CreatureSceneSetup>();

        AddNavCanvas("NavCanvas",
            ("지도",   NavButtonSetter.Destination.Map),
            ("공장",   NavButtonSetter.Destination.Factory),
            ("납품",   NavButtonSetter.Destination.Delivery),
            ("생명체", NavButtonSetter.Destination.Creature),
            ("가방",   NavButtonSetter.Destination.Bag));

        SaveScene(scene, $"{SceneDir}/CreatureScene.unity");
    }

    // ── BagScene ─────────────────────────────────────────────────

    static void BuildBagScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddEventSystem();
        new GameObject("BagSceneSetup").AddComponent<BagSceneSetup>();

        AddNavCanvas("NavCanvas",
            ("지도",   NavButtonSetter.Destination.Map),
            ("공장",   NavButtonSetter.Destination.Factory),
            ("납품",   NavButtonSetter.Destination.Delivery),
            ("생명체", NavButtonSetter.Destination.Creature),
            ("가방",   NavButtonSetter.Destination.Bag));

        SaveScene(scene, $"{SceneDir}/BagScene.unity");
    }

    // ── DeliveryScene ────────────────────────────────────────────

    static void BuildDeliveryScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AddEventSystem();
        new GameObject("DeliverySceneSetup").AddComponent<DeliverySceneSetup>();

        AddNavCanvas("NavCanvas",
            ("지도",   NavButtonSetter.Destination.Map),
            ("공장",   NavButtonSetter.Destination.Factory),
            ("납품",   NavButtonSetter.Destination.Delivery),
            ("생명체", NavButtonSetter.Destination.Creature),
            ("가방",   NavButtonSetter.Destination.Bag));

        SaveScene(scene, $"{SceneDir}/DeliveryScene.unity");
    }

    // ── Build Settings 등록 ──────────────────────────────────────

    static void RegisterBuildSettings()
    {
        // LoginScene을 index 0으로 — 앱 최초 진입점
        // (player_id가 이미 있으면 LoginSceneSetup이 곧바로 MapScene으로 넘긴다)
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene($"{SceneDir}/LoginScene.unity",    true),
            new EditorBuildSettingsScene($"{SceneDir}/MapScene.unity",      true),
            new EditorBuildSettingsScene($"{SceneDir}/FactoryScene.unity",  true),
            new EditorBuildSettingsScene($"{SceneDir}/CreatureScene.unity", true),
            new EditorBuildSettingsScene($"{SceneDir}/BagScene.unity",      true),
            new EditorBuildSettingsScene($"{SceneDir}/ARScene.unity",       true),
            new EditorBuildSettingsScene($"{SceneDir}/DeliveryScene.unity", true),
        };
    }

    // ── Always Included Shaders 등록 ─────────────────────────────

    [MenuItem("PixelCleaners/쉐이더 빌드 포함 설정")]
    public static void EnsureAlwaysIncludedShaders()
    {
        string[] required = { "Unlit/Color", "Unlit/Texture", "Unlit/Transparent", "Sprites/Default" };

        var gs = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("GraphicsSettings"));
        var prop = gs.FindProperty("m_AlwaysIncludedShaders");
        if (prop == null)
        {
            Debug.LogWarning("[SceneBuilder] Always Included Shaders 프로퍼티를 찾을 수 없습니다.");
            return;
        }

        bool changed = false;
        foreach (var name in required)
        {
            var shader = Shader.Find(name);
            if (shader == null) continue;

            bool exists = false;
            for (int i = 0; i < prop.arraySize; i++)
                if (prop.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                { exists = true; break; }

            if (!exists)
            {
                prop.InsertArrayElementAtIndex(prop.arraySize);
                prop.GetArrayElementAtIndex(prop.arraySize - 1).objectReferenceValue = shader;
                Debug.Log($"[SceneBuilder] 쉐이더 추가: {name}");
                changed = true;
            }
        }

        if (changed)
        {
            gs.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[SceneBuilder] Always Included Shaders 설정 완료. 빌드 전 반드시 실행하세요.");
        }
        else
        {
            Debug.Log("[SceneBuilder] 이미 모든 쉐이더가 포함되어 있습니다.");
        }
    }

    // ── TMP 폰트 에셋 생성 ───────────────────────────────────────

    [MenuItem("PixelCleaners/TMP 폰트 에셋 생성 (PF스타더스트 3.0)")]
    public static void CreateFontAsset()
    {
        const string ttfPath  = "Assets/Fonts/PF스타더스트 3.0.ttf";
        const string outDir   = "Assets/Resources/Fonts";
        const string outPath  = outDir + "/PF스타더스트 3.0 SDF.asset";

        var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (ttf == null)
        {
            Debug.LogError($"[SceneBuilder] TTF 파일을 찾을 수 없습니다: {ttfPath}");
            return;
        }

        EnsureDirectory(outDir);

        // 픽셀 폰트에 최적화된 설정: 작은 포인트 크기 + 큰 아틀라스
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            ttf,
            samplingPointSize: 40,
            atlasPadding:       4,
            renderMode:         GlyphRenderMode.SDFAA,
            atlasWidth:         4096,
            atlasHeight:        4096,
            atlasPopulationMode: AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError("[SceneBuilder] 폰트 에셋 생성 실패.");
            return;
        }

        fontAsset.name = "PF스타더스트 3.0 SDF";
        AssetDatabase.CreateAsset(fontAsset, outPath);

        // 아틀라스 텍스처와 머티리얼을 서브 에셋으로 저장하지 않으면
        // 재로드 시 m_AtlasTextures 레퍼런스가 끊겨 MissingReferenceException 발생
        foreach (var tex in fontAsset.atlasTextures)
        {
            if (tex == null) continue;
            tex.name = fontAsset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(tex, fontAsset);
        }

        if (fontAsset.material != null)
        {
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SceneBuilder] 폰트 에셋 생성 완료: {outPath}");
        EditorGUIUtility.PingObject(fontAsset);
    }

    // ── XR Origin ARScene 추가 ───────────────────────────────────

    [MenuItem("PixelCleaners/ARScene 완성 (ARSession + XR Origin 추가)")]
    public static void SetupARScene()
    {
        var scenePath = $"{SceneDir}/ARScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        bool changed = false;

        // ARSession
        bool hasARSession = false;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<ARSession>() != null) { hasARSession = true; break; }

        if (!hasARSession)
        {
            var go = new GameObject("ARSession");
            go.AddComponent<ARSession>();
            go.AddComponent<ARInputManager>();
            changed = true;
            Debug.Log("[SceneBuilder] ARSession 추가 완료.");
        }

        // XR Origin
        bool hasXROrigin = false;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name.Contains("XR Origin")) { hasXROrigin = true; break; }

        if (!hasXROrigin)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
            if (prefab != null)
            {
                PrefabUtility.InstantiatePrefab(prefab);
                changed = true;
                Debug.Log("[SceneBuilder] XR Origin 추가 완료.");
            }
            else
            {
                Debug.LogError($"[SceneBuilder] XR Origin 프리팹 없음: {XROriginPrefabPath}");
            }
        }

        if (changed) EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Debug.Log("[SceneBuilder] ARScene 설정 완료.");
    }

    [MenuItem("PixelCleaners/XR Origin ARScene에 추가")]
    public static void AddXROriginToARScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XROriginPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[SceneBuilder] XR Origin 프리팹 없음: {XROriginPrefabPath}");
            return;
        }

        var scenePath = $"{SceneDir}/ARScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 이미 있으면 스킵
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name.Contains("XR Origin"))
            {
                Debug.Log("[SceneBuilder] XR Origin 이미 존재합니다.");
                return;
            }
        }

        PrefabUtility.InstantiatePrefab(prefab);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();
        Debug.Log("[SceneBuilder] XR Origin (AR Rig) ARScene에 추가 완료.");
    }

    // ── PixelUI 내비게이션 캔버스 ────────────────────────────────

    static void AddNavCanvas(string canvasName,
        params (string label, NavButtonSetter.Destination dest)[] buttons)
    {
        var canvasGO = new GameObject(canvasName);
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        var panel     = new GameObject("NavPanel");
        panel.transform.SetParent(canvasGO.transform, false);
        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin        = Vector2.zero;
        panelRect.anchorMax        = new Vector2(1f, 0f);
        panelRect.pivot            = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta        = new Vector2(0f, 180f);
        panel.AddComponent<UnityEngine.UI.Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.95f);

        var hlg = panel.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hlg.spacing               = 8f;
        hlg.padding               = new RectOffset(16, 16, 8, 8);
        hlg.childControlWidth     = true;
        hlg.childForceExpandWidth = true;
        hlg.childControlHeight    = true;
        hlg.childForceExpandHeight = true;

        var btnPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonAPrefabPath);
        var fontAsset  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        foreach (var (label, dest) in buttons)
        {
            GameObject btnGO;
            if (btnPrefab != null)
            {
                btnGO = PrefabUtility.InstantiatePrefab(btnPrefab) as GameObject;
                btnGO.name = $"BTN_{dest}";
            }
            else
            {
                btnGO = new GameObject($"BTN_{dest}");
                btnGO.AddComponent<UnityEngine.UI.Image>()
                     .color = new Color(0.15f, 0.2f, 0.32f);
                btnGO.AddComponent<UnityEngine.UI.Button>();
            }

            btnGO.transform.SetParent(panel.transform, false);

            // HorizontalLayoutGroup이 크기를 제어하므로 앵커/sizeDelta 초기화
            var rect = btnGO.GetComponent<RectTransform>();
            rect.anchorMin        = Vector2.zero;
            rect.anchorMax        = Vector2.one;
            rect.pivot            = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta        = Vector2.zero;

            // 9-slice 테두리를 스케일로 굵게 표시
            foreach (var img in btnGO.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                img.pixelsPerUnitMultiplier = 0.5f;

            var tmps = btnGO.GetComponentsInChildren<TMPro.TMP_Text>(true);
            if (tmps.Length > 0)
            {
                foreach (var t in tmps)
                {
                    t.text      = label;
                    t.fontSize  = 32f;
                    if (fontAsset != null) t.font = fontAsset;
                }
            }
            else
            {
                var textGO = new GameObject("Label");
                textGO.transform.SetParent(btnGO.transform, false);
                var tr = textGO.AddComponent<RectTransform>();
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
                var tmp2 = textGO.AddComponent<TMPro.TextMeshProUGUI>();
                tmp2.text      = label;
                tmp2.fontSize  = 32f;
                tmp2.alignment = TMPro.TextAlignmentOptions.Center;
                tmp2.color     = Color.white;
                if (fontAsset != null) tmp2.font = fontAsset;
            }

            var setter = btnGO.AddComponent<NavButtonSetter>();
            setter.destination = dest;
        }
    }

    // ── EventSystem 패치 ─────────────────────────────────────────

    [MenuItem("PixelCleaners/EventSystem 패치 (StandaloneInputModule → InputSystemUI)")]
    public static void PatchEventSystems()
    {
        string[] scenePaths =
        {
            $"{SceneDir}/ARScene.unity",
            $"{SceneDir}/MapScene.unity",
            $"{SceneDir}/FactoryScene.unity",
        };

        foreach (var path in scenePaths)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                var standalone = root.GetComponentInChildren<UnityEngine.EventSystems.StandaloneInputModule>(true);
                if (standalone == null) continue;

                var go = standalone.gameObject;
                Object.DestroyImmediate(standalone);
                if (go.GetComponent<InputSystemUIInputModule>() == null)
                    go.AddComponent<InputSystemUIInputModule>();
                changed = true;
                Debug.Log($"[SceneBuilder] {path} — StandaloneInputModule 교체 완료");
            }

            if (changed) EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.Refresh();
        Debug.Log("[SceneBuilder] EventSystem 패치 완료.");
    }

    // ── 헬퍼 ────────────────────────────────────────────────────

    static void AddEventSystem()
    {
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    static void TryAddPrefab(string prefabPath, string logName)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.Log($"[SceneBuilder] {logName} 프리팹 없음 — 수동으로 추가하세요.");
            return;
        }
        PrefabUtility.InstantiatePrefab(prefab);
        Debug.Log($"[SceneBuilder] {logName} 추가 완료.");
    }

    static void SaveScene(UnityEngine.SceneManagement.Scene scene, string path)
    {
        bool ok = EditorSceneManager.SaveScene(scene, path);
        Debug.Log(ok
            ? $"[SceneBuilder] 저장 완료: {path}"
            : $"[SceneBuilder] 저장 실패: {path}");
    }

    static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
