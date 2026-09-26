using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;
using PixelCleaners.AR;
using PixelCleaners.Capture;
using PixelCleaners.GPS;
using PixelCleaners.UI;

// ARSceneSetup.Awake()가 ARSession.Awake()보다 반드시 먼저 실행돼야
// 카메라 권한 없이 ARCore가 초기화되는 것을 막을 수 있다.
[DefaultExecutionOrder(-1000)]
public class ARSceneSetup : MonoBehaviour
{
    TMP_Text _statusText;

#if UNITY_ANDROID
    void Awake()
    {
        // ARSession이 Awake에서 ARCore를 초기화하기 전에 비활성화
        // (카메라 권한이 없는 상태에서 ARCore가 카메라를 선점하면 이후 권한 획득해도 복구 불가)
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            var arSess = GameObject.Find("ARSession");
            if (arSess != null) arSess.SetActive(false);
        }
    }
#endif

    void Start()
    {
        StartCoroutine(InitAR());
    }

    IEnumerator InitAR()
    {
#if UNITY_ANDROID
        // ── 1. 카메라 권한 요청 ──────────────────────────────────
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            bool done = false;
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => done = true;
            cb.PermissionDenied  += _ => done = true;
            Permission.RequestUserPermission(Permission.Camera, cb);
            yield return new WaitUntil(() => done);

            // 권한 획득 → ARSession 활성화 (이제 ARCore가 카메라에 접근 가능)
            var arSessionGO = GameObject.Find("ARSession");
            if (arSessionGO != null) arSessionGO.SetActive(true);
        }

        // ── 2. ARSession.state 폴링 (최대 10초) ─────────────────
        float elapsed = 0f;
        while (elapsed < 10f)
        {
            var s = UnityEngine.XR.ARFoundation.ARSession.state;
            if (s == UnityEngine.XR.ARFoundation.ARSessionState.SessionTracking   ||
                s == UnityEngine.XR.ARFoundation.ARSessionState.Unsupported       ||
                s == UnityEngine.XR.ARFoundation.ARSessionState.NeedsInstall      ||
                s == UnityEngine.XR.ARFoundation.ARSessionState.SessionInitializing)
                break;
            yield return new WaitForSeconds(0.25f);
            elapsed += 0.25f;
        }
        Debug.Log($"[ARSetup] ARSession.state={UnityEngine.XR.ARFoundation.ARSession.state} ({elapsed:F1}s)");

        // ── 3. Camera.main이 실제로 설정될 때까지 추가 대기 ─────
        // ARSession 활성화 직후엔 XR Origin AR 카메라가 아직 MainCamera로 등록 안 됨
        float camWait = 0f;
        while (Camera.main == null && camWait < 3f)
        {
            yield return new WaitForSeconds(0.1f);
            camWait += 0.1f;
        }
        Debug.Log($"[ARSetup] Camera.main={(Camera.main != null ? Camera.main.name : "null")} ({camWait:F1}s)");
#else
        yield return null;
#endif

        SetupCamera();
        SetupCreatureSpawner();
        SetupCaptureUI();
        SetupToolSelectorUI();
        SetupDebugUI();
        if (GameObject.Find("NavCanvas") == null) SetupNavUI();
    }

    void Update()
    {
        if (_statusText == null) return;

        var loc = LocationManager.Instance;
        var det = HotspotDetector.Instance;
        var fac = FactoryManager.Instance;

        string gps = loc != null && loc.IsReady
            ? $"GPS  {loc.CurrentLocation.x:F4}, {loc.CurrentLocation.y:F4}"
            : "GPS 초기화 중...";
        string grade = det != null
            ? $"핫스팟  {det.CurrentGrade}  ({det.GetSpawnChance() * 100f:F0}%)"
            : "";
        string inv = fac != null
            ? $"인벤토리  {fac.CreatureInventory.Count}마리  |  각성제  {fac.AwakenItems[AwakenItemTier.Normal]}"
            : "";

        _statusText.text = $"AR 씬\n{gps}\n{grade}\n{inv}";
    }

    // ── 카메라 ───────────────────────────────────────────────────

    void SetupCamera()
    {
        Camera cam;

        if (Camera.main != null)
        {
            cam = Camera.main;
            bool isArCam = cam.GetComponent<UnityEngine.XR.ARFoundation.ARCameraBackground>() != null;

            if (isArCam)
            {
                // AR 카메라: clearFlags = Depth라서 ARCameraBackground가 없거나
                // 아직 렌더링 안 되면 검은화면 → 배경 전용 보조 카메라 추가
                EnsureBackgroundCamera(cam.depth - 1);
            }
            else if (cam.clearFlags == CameraClearFlags.SolidColor)
            {
                cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            }
        }
        else
        {
            // XR Origin 없는 기기용 테스트 카메라
            var camGO = new GameObject("ARTestCamera");
            cam                 = camGO.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            cam.fieldOfView     = 60f;
            cam.tag             = "MainCamera";
            camGO.transform.position = new Vector3(0f, 1.6f, 0f);
        }

        // SpawnAnchor: AR/비AR 공통으로 카메라 앞에 배치
        if (GameObject.Find("SpawnAnchor") == null)
        {
            var anchor = new GameObject("SpawnAnchor");
            anchor.transform.SetParent(cam.transform, false);
            anchor.transform.localPosition = new Vector3(0f, -0.5f, 3f);
        }
    }

    // AR 카메라 뒤에서 단색 배경을 그려주는 보조 카메라
    // ARCameraBackground가 렌더링되면 자동으로 덮어씌워짐
    static void EnsureBackgroundCamera(float depth)
    {
        if (GameObject.Find("ARBackgroundCamera") != null) return;
        var go  = new GameObject("ARBackgroundCamera");
        var cam = go.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cam.cullingMask     = 0;        // 아무것도 렌더링하지 않고 배경만 그림
        cam.depth           = depth;    // AR 카메라보다 낮은 depth
    }

    // ── 생명체 스포너 ────────────────────────────────────────────

    void SetupCreatureSpawner()
    {
        if (FindFirstObjectByType<CreatureSpawner>() != null) return;

        var go      = new GameObject("CreatureSpawner");
        var spawner = go.AddComponent<CreatureSpawner>();

        // 풀 순서 = CreatureRoster.Spawnable 순서.
        // 지도 씬이 같은 목록의 인덱스를 PendingCapture.PoolIndex로 넘긴다.
        foreach (var entry in CreatureRoster.Spawnable)
        {
            var so    = CreatureRoster.CreateDefinition(entry);
            so.prefab = MakeCreatureTemplate(entry);
            spawner.AddToPool(so);
        }
    }

    /// <summary>
    /// 로스터 항목의 모델(Resources/Characters/{id})을 불러와 AR 표시 높이로 맞춘 비활성 템플릿을 만든다.
    /// </summary>
    static GameObject MakeCreatureTemplate(CreatureEntry entry)
    {
        var prefab = CreatureRoster.LoadModel(entry);
        if (prefab == null)
            Debug.LogWarning($"[ARSetup] {entry.id} 모델 없음 → 구체 폴백");

        var go = CreatureModelFitter.Build(prefab, $"Creature_{entry.id}",
                                           CreatureModelFitter.ArHeight,
                                           entry.modelYawOffset, entry.fallbackColor);

        // 경계 측정이 끝난 뒤 비활성화 (비활성 렌더러는 경계가 0으로 나온다)
        go.SetActive(false);
        return go;
    }

    // ── CaptureUI 캔버스 ─────────────────────────────────────────

    void SetupCaptureUI()
    {
        if (FindFirstObjectByType<PixelCleaners.UI.CaptureUI>() != null) return;

        var canvasGO = MakeCanvas("CaptureCanvas", 10);

        // 팝업 레이아웃은 CaptureUI가 직접 만든다 (정면샷·등급·능력치·도감 신규 표시)
        var ui = canvasGO.AddComponent<PixelCleaners.UI.CaptureUI>();
        ui.Build(canvasGO.transform);
    }

    // ── 포획틀 선택 UI (하단 고정 바) ───────────────────────────

    void SetupToolSelectorUI()
    {
        var canvas = MakeCanvas("ToolSelectorCanvas", 8);
        var selector = canvas.AddComponent<ToolSelectorUI>();
        selector.Build(canvas.transform, bottomOffset: 180f); // nav 바 위
    }

    // ── Canvas 내비게이션 (우측 하단) ────────────────────────────

    void SetupNavUI()
    {
        var canvas = MakeCanvas("NavCanvas", 5);

        var panel     = MakeRectGO("NavPanel", canvas.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin        = Vector2.zero;
        panelRect.anchorMax        = new Vector2(1f, 0f);
        panelRect.pivot            = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta        = new Vector2(0f, 180f);
        panel.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.95f);

        var layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing               = 8f;
        layout.padding               = new RectOffset(16, 16, 8, 8);
        layout.childControlWidth     = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight    = true;
        layout.childForceExpandHeight = true;

        MakeNavButton("메인(지도)", panel.transform, SceneController.GoMap);
        MakeNavButton("공장",       panel.transform, SceneController.GoFactory);
        MakeNavButton("생명체",     panel.transform, SceneController.GoCreature);
        MakeNavButton("가방",       panel.transform, SceneController.GoBag);
    }

    // ── Canvas 디버그 패널 (좌측 상단) ───────────────────────────

    void SetupDebugUI()
    {
        var canvas = MakeCanvas("DebugCanvas", 5);

        var panel     = MakeRectGO("DebugPanel", canvas.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0f, 1f);
        panelRect.anchorMax        = new Vector2(0f, 1f);
        panelRect.pivot            = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(32f, -32f);
        panelRect.sizeDelta        = new Vector2(320f, 0f);
        panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding            = new RectOffset(16, 16, 16, 16);
        layout.spacing            = 8f;
        layout.childControlWidth  = true;
        layout.childControlHeight = false;
        panel.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        _statusText          = MakeTMPInLayout("Status", panel.transform, 18f);
        _statusText.text     = "초기화 중...";
        _statusText.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 110f);

        MakeNavButton("핫스팟 강제 진입", panel.transform,
            () => HotspotDetector.Instance?.ForceGrade(HotspotGrade.Hotspot));
        MakeNavButton("생명체 강제 스폰", panel.transform,
            () => FindFirstObjectByType<CreatureSpawner>()?.ForceSpawn());
        MakeNavButton("각성제 +1", panel.transform,
            () => FactoryManager.Instance?.AddAwakenItem(AwakenItemTier.Normal, 1));
        MakeNavButton("강화 포획틀 +3", panel.transform,
            () => { FactoryManager.Instance?.AddTool(CaptureToolTier.Enhanced, 3);
                    FindFirstObjectByType<ToolSelectorUI>()?.Refresh(); });
        MakeNavButton("정밀 포획틀 +3", panel.transform,
            () => { FactoryManager.Instance?.AddTool(CaptureToolTier.Precision, 3);
                    FindFirstObjectByType<ToolSelectorUI>()?.Refresh(); });
        MakeNavButton("픽셀 포획틀 +1", panel.transform,
            () => { FactoryManager.Instance?.AddTool(CaptureToolTier.Pixel, 1);
                    FindFirstObjectByType<ToolSelectorUI>()?.Refresh(); });

        // GPS·핫스팟 상태 글자와 디버그 버튼 전체 — 디버그 토글(+)로 표시
        DebugUI.Register(canvas);
    }

    // ── UI 헬퍼 ─────────────────────────────────────────────────

    static GameObject MakeCanvas(string name, int sortOrder = 0)
    {
        var go     = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static GameObject MakeRectGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static GameObject MakePanel(Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go   = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
        return go;
    }

    static TMP_Text MakeTMP(string name, Transform parent,
                             Vector2 aMin, Vector2 aMax, float size)
    {
        var go   = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = aMin; rect.anchorMax = aMax;
        rect.offsetMin = new Vector2(8f, 4f); rect.offsetMax = new Vector2(-8f, -4f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        FontProvider.Apply(tmp);
        return tmp;
    }

    static TMP_Text MakeTMPInLayout(string name, Transform parent, float size)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, size + 8f);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize  = size;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Left;
        FontProvider.Apply(tmp);
        return tmp;
    }

    static UnityEngine.UI.Button MakeAnchoredButton(string label, Transform parent,
                                     Vector2 aMin, Vector2 aMax, Color color)
    {
        var go   = new GameObject(label);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = aMin; rect.anchorMax = aMax;
        rect.offsetMin = new Vector2(4f, 4f); rect.offsetMax = new Vector2(-4f, -4f);
        go.AddComponent<Image>().color = color;
        var btn = go.AddComponent<UnityEngine.UI.Button>();
        go.AddComponent<PixelUI.Button>();

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var tr = textGO.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 16f;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        FontProvider.Apply(tmp);
        return btn;
    }

    static UnityEngine.UI.Button MakeNavButton(string label, Transform parent, System.Action onClick)
    {
        var go = MakeRectGO(label, parent);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 120f);
        go.AddComponent<Image>().color = new Color(0.15f, 0.2f, 0.32f);
        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        go.AddComponent<PixelUI.Button>();

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var tr = textGO.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 17f;
        tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
        FontProvider.Apply(tmp);
        return btn;
    }
}
