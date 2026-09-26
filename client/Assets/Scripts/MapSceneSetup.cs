using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;
using PixelCleaners.Capture;
using PixelCleaners.GPS;

public class MapSceneSetup : MonoBehaviour
{
    const float worldScale   = 0.005f;
    const float markerHeight = 0.05f;

    // 카메라 기울기. 90 = 완전 탑뷰(머리 윗부분만 보임), 낮을수록 옆모습이 보인다.
    // 정사영이라 거리는 화면 크기에 영향이 없고 클리핑용이다.
    const float CameraPitch    = 50f;
    const float CameraDistance = 20f;

    [Header("지도 타일")]
    [SerializeField]
    MapTileLoader.MapTileProvider tileProvider = MapTileLoader.MapTileProvider.CartoPositron;

    [Tooltip("CARTO 무료 키: https://carto.com/basemaps/apikey/ — 비우면 OSM으로 폴백한다")]
    [SerializeField] string cartoApiKey = "";

    // 핫스팟/플레이어 마커. RebuildMarkers()가 통째로 파괴한다.
    readonly List<GameObject> markers = new();
    // 몬스터 마커. 핫스팟 등급이 바뀌어도 살아남아야 하므로 별도 관리.
    readonly List<GameObject> monsterMarkers = new();

    // ── 지도 스폰 (MapSpawnSystem) ──
    const float SpawnRefreshSeconds = 5f;
    Vector2 playerLatLon;
    float   nextSpawnRefresh;
    string  shownSpawnSignature = "";   // 지금 그려진 스폰 키 목록 — 같으면 다시 그리지 않는다
    System.Action spawnChangedHandler;

    Camera mapCamera;
    TMP_Text _statusText;
    MapTileLoader tileLoader;
    bool mapInitialized;

    // 씬 언로드 시 구독 해제를 위해 명시적 저장
    System.Action<Vector2>      locationHandler;
    System.Action<HotspotGrade> hotspotHandler;

    void Start()
    {
        SetupCamera();
        SetupLighting();
        SetupGround();
        SetupTileLoader();
        SetupStatusUI();
        SetupDebugUI();
        if (GameObject.Find("NavCanvas") == null) SetupNavUI();

        hotspotHandler = _ => RebuildMarkers();
        locationHandler = OnLocationUpdated;

        if (HotspotDetector.Instance != null)
            HotspotDetector.Instance.OnHotspotGradeChanged += hotspotHandler;

        if (LocationManager.Instance != null)
            LocationManager.Instance.OnLocationUpdated += locationHandler;

        var loc = LocationManager.Instance;
        if (loc != null && loc.IsReady)
            OnLocationUpdated(loc.CurrentLocation);
        else
        {
            var fallback = new Vector2(37.5665f, 126.9780f);
            InitMap(fallback);
            tileLoader.RefreshAt(fallback.x, fallback.y);
        }
    }

    // 최초 1회 지도 기준점·마커·타일을 초기화한다.
    void InitMap(Vector2 latLon)
    {
        referenceLatLon = latLon;
        playerLatLon    = latLon;
        tileLoader.SetReference(latLon.x, latLon.y);
        RebuildMarkers();
        mapInitialized = true;   // RefreshSpawns가 LatLonToWorld를 쓰므로 먼저 표시

        spawnChangedHandler = () => RefreshSpawns(force: true);
        MapSpawnSystem.OnChanged += spawnChangedHandler;
        RefreshSpawns(force: true);
    }

    void OnDestroy()
    {
        if (LocationManager.Instance != null && locationHandler != null)
            LocationManager.Instance.OnLocationUpdated -= locationHandler;
        if (HotspotDetector.Instance != null && hotspotHandler != null)
            HotspotDetector.Instance.OnHotspotGradeChanged -= hotspotHandler;
        if (spawnChangedHandler != null)
            MapSpawnSystem.OnChanged -= spawnChangedHandler;
    }

    void OnLocationUpdated(Vector2 latLon)
    {
        if (!mapInitialized)
            InitMap(latLon);

        playerLatLon = latLon;
        tileLoader.RefreshAt(latLon.x, latLon.y);
        UpdatePlayerMarker();
        RefreshSpawns();
    }

    void SetupTileLoader()
    {
        var go = new GameObject("MapTileLoader");
        tileLoader = go.AddComponent<MapTileLoader>();
        tileLoader.Configure(tileProvider, cartoApiKey);
    }

    void Update()
    {
        // 제자리에 있어도 스폰 주기가 넘어가면 나타나고 사라진다
        if (mapInitialized && Time.unscaledTime >= nextSpawnRefresh)
        {
            nextSpawnRefresh = Time.unscaledTime + SpawnRefreshSeconds;
            RefreshSpawns();
        }

        if (_statusText == null) return;

        var det = HotspotDetector.Instance;
        var loc = LocationManager.Instance;

        string grade = det != null
            ? $"현재 등급  {det.CurrentGrade}\n스폰 확률  {det.GetSpawnChance() * 100f:F0}%"
            : "";
        string gps = loc != null && loc.IsReady
            ? $"GPS  {loc.CurrentLocation.x:F4}, {loc.CurrentLocation.y:F4}"
            : "GPS 초기화 중...";

        _statusText.text = $"지도\n{grade}\n{gps}";
    }

    // ── 카메라 ───────────────────────────────────────────────────

    void SetupCamera()
    {
        var camGO = new GameObject("MapCamera");
        mapCamera = camGO.AddComponent<Camera>();
        mapCamera.orthographic     = true;
        // 세로 10유닛 표시 → 타일 12유닛 커버로 사방 여백 없이 전체화면 채움
        // zoom18 기준: 타일 1개 ≈ 0.6 world unit, 5×5 = 3.0 world unit
        // 포켓몬고 수준: 반경 약 150m(0.75 world unit) → size=0.75
        mapCamera.orthographicSize = 0.75f;
        FocusCamera(Vector3.zero);
        // CartoDB Positron 배경색과 맞춰 타일 로딩 전 빈 곳을 자연스럽게
        mapCamera.backgroundColor = new Color(0.95f, 0.95f, 0.93f);
        mapCamera.clearFlags      = CameraClearFlags.SolidColor;
        mapCamera.tag             = "MainCamera";
    }

    void SetupLighting()
    {
        var lightGO = new GameObject("DirectionalLight");
        var light   = lightGO.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1.2f;
        lightGO.transform.eulerAngles = new Vector3(50f, -30f, 0f);
        RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.35f);
    }

    // ── 지면 ─────────────────────────────────────────────────────

    void SetupGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "MapGround";
        ground.transform.localScale = Vector3.one * 1.2f;
        var groundMat = MakeColorMat(new Color(0.95f, 0.95f, 0.93f));
        ground.GetComponent<Renderer>().material = groundMat;
        Destroy(ground.GetComponent<Collider>());

        SpawnFogOverlay();
    }

    void SpawnFogOverlay()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "MapFogOverlay";
        Destroy(go.GetComponent<Collider>());
        go.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        go.transform.localPosition   = new Vector3(0f, 0.05f, 0f);
        go.transform.localScale      = Vector3.one * 30f;

        var mat = MakeTransparentMat(new Color(1f, 1f, 1f, 0.35f));
        go.GetComponent<Renderer>().material = mat;
        go.GetComponent<Renderer>().sortingOrder = 1;
    }

    // URP → 레거시 → 최후 폴백 순으로 사용 가능한 쉐이더를 찾는다
    static Shader SafeFind(params string[] candidates)
    {
        foreach (var name in candidates)
        {
            var s = Shader.Find(name);
            if (s != null) return s;
        }
        // "UI/Default"는 Unity 모든 빌드에 항상 포함된 최후 안전망
        var fallback = Shader.Find("UI/Default");
        if (fallback != null) return fallback;
        Debug.LogError($"[MapSceneSetup] 쉐이더를 찾을 수 없습니다: {string.Join(", ", candidates)}");
        return null;
    }

    static Material MakeColorMat(Color color)
    {
        var s = SafeFind(
            "Universal Render Pipeline/Unlit",
            "Unlit/Color",
            "Mobile/Unlit (Supports Lightmap)");
        if (s == null) return new Material(Shader.Find("Hidden/InternalErrorShader"));
        var mat = new Material(s);
        mat.color = color;
        if (s.name.Contains("Universal Render Pipeline"))
            mat.SetColor("_BaseColor", color);
        return mat;
    }

    static Material MakeTransparentMat(Color color)
    {
        var s = SafeFind(
            "Sprites/Default",
            "Universal Render Pipeline/Particles/Unlit",
            "Unlit/Transparent");
        if (s == null) return new Material(Shader.Find("Hidden/InternalErrorShader"));
        var mat = new Material(s);
        mat.color = color;
        return mat;
    }

    // ── 핫스팟 마커 ──────────────────────────────────────────────

    Vector2 referenceLatLon;
    GameObject playerMarker;


    void RebuildMarkers()
    {
        // referenceLatLon이 설정되기 전(InitMap 호출 전)에는 마커를 그릴 수 없다.
        if (referenceLatLon == Vector2.zero) return;

        foreach (var m in markers) if (m != null) Destroy(m);
        markers.Clear();

        var det = HotspotDetector.Instance;
        if (det == null) return;

        foreach (var spot in det.Hotspots)
            SpawnHotspotMarker(spot);

        if (playerMarker == null)
            playerMarker = SpawnMarker("Player", Color.white, 0.04f); // zoom18: 카메라 뷰의 ~3%

        UpdatePlayerMarker();
    }

    void SpawnHotspotMarker(HotspotData spot)
    {
        var color = spot.grade switch
        {
            HotspotGrade.Hotspot  => new Color(1f, 0.3f, 0.2f),
            HotspotGrade.Polluted => new Color(1f, 0.7f, 0.1f),
            _                     => new Color(0.4f, 0.9f, 0.4f)
        };

        var pos = LatLonToWorld(new Vector2(spot.latitude, spot.longitude));
        var go  = SpawnMarker(spot.label, color, 0.05f);
        go.transform.position = new Vector3(pos.x, markerHeight, pos.y);

        var radius = (spot.radiusMeters > 0f ? spot.radiusMeters : 50f) * worldScale;
        SpawnRadiusRing(go.transform.position, radius, color);
        SpawnLabel(spot.label, go.transform.position + Vector3.up * 0.06f, color);
        markers.Add(go);
    }

    void UpdatePlayerMarker()
    {
        if (playerMarker == null || LocationManager.Instance == null) return;
        var pos = LatLonToWorld(LocationManager.Instance.CurrentLocation);
        playerMarker.transform.position = new Vector3(pos.x, markerHeight + 0.05f, pos.y);

        // 카메라가 플레이어를 따라감
        FocusCamera(new Vector3(pos.x, 0f, pos.y));
    }

    /// <summary>
    /// 기울어진 카메라가 target을 화면 중앙에 두도록 배치한다.
    /// 탑뷰일 때처럼 x,z만 옮기면 기울기만큼 플레이어가 화면 아래로 밀려난다.
    /// </summary>
    void FocusCamera(Vector3 target)
    {
        if (mapCamera == null) return;
        var rot = Quaternion.Euler(CameraPitch, 0f, 0f);
        mapCamera.transform.rotation = rot;
        mapCamera.transform.position = target - rot * Vector3.forward * CameraDistance;
    }

    // ── 지도 몬스터 스폰 ─────────────────────────────────────────
    // 스폰 자체는 MapSpawnSystem이 위치·시간으로 결정한다 (포켓몬 GO식 고정 스폰 지점).
    // 이 씬은 플레이어 주변의 활성 스폰을 조회해 그리기만 한다.

    /// <param name="force">스폰 목록이 같아도 다시 그린다 (포획 기록 복원 등)</param>
    void RefreshSpawns(bool force = false)
    {
        if (!mapInitialized) return;

        var spawns = MapSpawnSystem.GetSpawnsNear(playerLatLon);

        var sb = new System.Text.StringBuilder();
        foreach (var s in spawns) sb.Append(s.key).Append('|');
        string signature = sb.ToString();
        if (!force && signature == shownSpawnSignature) return;
        shownSpawnSignature = signature;

        foreach (var m in monsterMarkers) if (m != null) Destroy(m);
        monsterMarkers.Clear();

        var pool = CreatureRoster.Spawnable;
        foreach (var s in spawns)
        {
            if (s.poolIndex < 0 || s.poolIndex >= pool.Count) continue;
            var entry = pool[s.poolIndex];
            // 지도 전용 저폴리 모델({id}_Map)이 있으면 그것을, 없으면 AR 모델을 쓴다
            var prefab = CreatureRoster.LoadModel(entry, CreatureRoster.ModelVariant.Map);

            // 모델 크기·기준점이 제각각이어도 지도 표시 높이로 통일, 발밑이 원점
            var go = CreatureModelFitter.Build(prefab, $"Monster_{entry.id}_{s.key}",
                                               CreatureModelFitter.MapHeight,
                                               entry.modelYawOffset, entry.fallbackColor);
            var world = LatLonToWorld(new Vector2((float)s.latitude, (float)s.longitude));
            go.transform.position = new Vector3(world.x, markerHeight + 0.01f, world.y);
            // 카메라가 남쪽에서 비스듬히 보므로, 그대로 두면 +Z(북)를 향한 모델의 등이 보인다
            go.AddComponent<PixelCleaners.AR.BillboardFace>();

            // poolIndex = AR 스폰 풀 인덱스 → 지도에서 탭한 생명체가 AR 씬에 그대로 나타난다
            var marker = go.AddComponent<MapMonsterMarker>();
            marker.Init(null, s.rarity, s.grade, s.poolIndex, s.key, s.endsTicks);

            monsterMarkers.Add(go);
        }
    }

    /// 디버그: 다음 스폰 주기로 시간을 넘겨 새 스폰을 본다
    void DebugNextSpawnCycle()
    {
        MapSpawnSystem.DebugTimeOffsetMinutes += MapSpawnSystem.CycleMinutes;
        RefreshSpawns(force: true);
    }

    Vector2 LatLonToWorld(Vector2 latLon)
    {
        if (referenceLatLon == Vector2.zero) return Vector2.zero;
        float dLat = (latLon.x - referenceLatLon.x) * 111320f * worldScale;
        float dLon = (latLon.y - referenceLatLon.y) * 111320f
                     * Mathf.Cos(referenceLatLon.x * Mathf.Deg2Rad) * worldScale;
        return new Vector2(dLon, dLat);
    }

    static GameObject SpawnMarker(string name, Color color, float size)
    {
        var go   = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name  = name;
        go.transform.localScale = Vector3.one * size;

        var rend = go.GetComponent<Renderer>();
        // shared material을 직접 수정하면 모든 구체에 적용됨 → 인스턴스 복제
        var mat  = new Material(rend.sharedMaterial);
        mat.color = color;
        // URP Lit/Unlit 계열은 _Color 대신 _BaseColor 사용
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        rend.material = mat;

        Destroy(go.GetComponent<Collider>());
        return go;
    }

    void SpawnRadiusRing(Vector3 center, float radius, Color color)
    {
        const int segments = 48;
        var go = new GameObject("RadiusRing");
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = segments + 1;
        lr.startWidth = lr.endWidth = 0.015f;
        lr.material = MakeTransparentMat(Color.white);
        lr.startColor = lr.endColor = new Color(color.r, color.g, color.b, 0.6f);
        lr.useWorldSpace = true;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            lr.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius,
                                                    0.01f,
                                                    Mathf.Sin(angle) * radius));
        }
        markers.Add(go);
    }

    void SpawnLabel(string text, Vector3 worldPos, Color color)
    {
        var go  = new GameObject($"Label_{text}");
        var tmp = go.AddComponent<TMPro.TextMeshPro>();
        tmp.text      = text;
        tmp.fontSize  = 2f;
        tmp.color     = color;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        FontProvider.Apply(tmp);
        go.transform.position    = worldPos;
        go.transform.eulerAngles = new Vector3(90f, 0f, 0f);
        markers.Add(go);
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

        MakeNavButton("AR 포획", panel.transform, SceneController.GoAR);
        MakeNavButton("공장",    panel.transform, SceneController.GoFactory);
        MakeNavButton("납품",    panel.transform, SceneController.GoDelivery);
    }

    // ── Canvas 상태 패널 (좌측 상단) ─────────────────────────────

    void SetupStatusUI()
    {
        var canvas = MakeCanvas("StatusCanvas", 5);

        var panel     = MakeRectGO("StatusPanel", canvas.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0f, 1f);
        panelRect.anchorMax        = new Vector2(0f, 1f);
        panelRect.pivot            = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(32f, -32f);
        panelRect.sizeDelta        = new Vector2(300f, 0f);
        panel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.75f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding            = new RectOffset(16, 16, 16, 16);
        layout.spacing            = 6f;
        layout.childControlWidth  = true;
        layout.childControlHeight = false;
        panel.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        _statusText      = MakeTMPInLayout("Status", panel.transform, 18f);
        _statusText.text = "초기화 중...";
        _statusText.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 100f);

        // 등급·스폰 확률·GPS 좌표는 개발용 정보 — 디버그 토글로 표시
        DebugUI.Register(canvas);
    }

    // ── 디버그 UI ────────────────────────────────────────────────

    void SetupDebugUI()
    {
        var canvas = MakeCanvas("DebugCanvas", 10);

        // 우측 상단에서 아래로 쌓임 — 맨 위는 디버그 토글(+) 버튼 자리라 비워 둔다
        // 스폰은 위치·시간으로 정해지므로 "생성" 대신 시간을 한 주기 넘긴다
        MakeDebugButton(canvas.transform, "[DEBUG]\n다음 스폰 주기",
            new Vector2(-20f, -90f), DebugNextSpawnCycle);
        MakeDebugButton(canvas.transform, "[DEBUG]\n강화 포획틀 +3",
            new Vector2(-20f, -180f),
            () => FactoryManager.Instance?.AddTool(CaptureToolTier.Enhanced, 3));
        MakeDebugButton(canvas.transform, "[DEBUG]\n정밀 포획틀 +1",
            new Vector2(-20f, -275f),
            () => FactoryManager.Instance?.AddTool(CaptureToolTier.Precision, 1));

        DebugUI.Register(canvas);
    }

    static void MakeDebugButton(Transform canvasParent, string label,
                                 Vector2 anchoredPos, System.Action onClick)
    {
        var go = new GameObject($"DebugBtn_{label}");
        go.transform.SetParent(canvasParent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(1f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(220f, 75f);
        go.AddComponent<UnityEngine.UI.Image>().color = new Color(0.75f, 0.18f, 0.08f, 0.9f);

        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lr = lblGO.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 20f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        FontProvider.Apply(tmp);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────

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
