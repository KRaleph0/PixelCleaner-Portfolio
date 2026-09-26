using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 래스터 지도 타일을 GPS 좌표 기반 월드 공간에 배치합니다.
/// loadGeneration으로 중복 로드 / 기존 로드 취소를 처리합니다.
/// 타일 제공자는 인스펙터에서 교체할 수 있습니다(<see cref="MapTileProvider"/>).
/// </summary>
public class MapTileLoader : MonoBehaviour
{
    /// <summary>래스터 타일 제공자.</summary>
    public enum MapTileProvider
    {
        /// CARTO Positron(밝은 회색). 2026-08부터 API 키 필수 —
        /// 키 없이 요청하면 "API KEY REQUIRED" 워터마크가 찍힌 타일이 온다.
        /// 무료 티어 5,000,000 타일/월. https://carto.com/basemaps/apikey/
        CartoPositron,
        /// CARTO Dark Matter(어두운 스타일). 동일하게 키 필요.
        CartoDarkMatter,
        /// OpenStreetMap 표준 타일. 키 불필요.
        /// 단 OSM 타일 사용 정책상 식별 가능한 User-Agent가 필수이며
        /// 대량 트래픽은 차단될 수 있다. https://operations.osmfoundation.org/policies/tiles/
        OpenStreetMap,
    }

    [SerializeField] int zoom       = 18;  // 18 = 포켓몬고 수준 (≈150m/타일)
    [SerializeField] int tileRadius = 2;  // 2 = 5×5, 3 = 7×7

    [Header("타일 제공자")]
    [SerializeField] MapTileProvider provider = MapTileProvider.CartoPositron;

    [Tooltip("CARTO 사용 시 필수. 비워 두면 워터마크가 찍히므로 자동으로 OSM으로 폴백한다.")]
    [SerializeField] string cartoApiKey = "";

    // OSM 정책상 앱을 식별할 수 있어야 한다. 배포 시 연락 가능한 주소로 바꿀 것.
    const string UserAgent = "PixelCleaner/1.0 (+https://github.com/KRaleph0/PixelCleaner-Portfolio)";

    static readonly string[] CartoServers = { "a", "b", "c", "d" };

    // MapSceneSetup.worldScale 과 반드시 일치
    const float WorldScale = 0.005f;

    double refLat, refLon;
    bool   hasRef;

    readonly Dictionary<Vector2Int, GameObject> tiles = new();
    Vector2Int centerTile  = new(int.MinValue, int.MinValue);
    int        serverIndex;
    bool       loading;
    int        loadGeneration; // 세대 번호 — 변경 시 진행 중 로드를 무시/재시작

    Material tileMat;
    bool     isUrpShader;
    bool     warnedNoKey;

    // ── 타일 URL 생성 ────────────────────────────────────────────

    /// <summary>키가 없는 CARTO 설정은 워터마크가 찍히므로 OSM으로 자동 폴백한다.</summary>
    MapTileProvider EffectiveProvider
    {
        get
        {
            bool needsKey = provider == MapTileProvider.CartoPositron ||
                            provider == MapTileProvider.CartoDarkMatter;
            if (!needsKey || !string.IsNullOrWhiteSpace(cartoApiKey))
                return provider;

            if (!warnedNoKey)
            {
                warnedNoKey = true;
                Debug.LogWarning("[MapTile] CARTO API 키가 비어 있어 OpenStreetMap 타일로 폴백합니다. " +
                                 "https://carto.com/basemaps/apikey/ 에서 무료 키를 받아 " +
                                 "MapTileLoader의 cartoApiKey에 입력하세요.");
            }
            return MapTileProvider.OpenStreetMap;
        }
    }

    string BuildTileUrl(Vector2Int coord)
    {
        switch (EffectiveProvider)
        {
            case MapTileProvider.CartoPositron:
            case MapTileProvider.CartoDarkMatter:
            {
                string style  = EffectiveProvider == MapTileProvider.CartoPositron
                                ? "light_all" : "dark_all";
                string server = CartoServers[serverIndex++ % CartoServers.Length];
                return $"https://{server}.basemaps.cartocdn.com/{style}/" +
                       $"{zoom}/{coord.x}/{coord.y}.png?key={cartoApiKey}";
            }
            default:
                // OSM은 a/b/c 서브도메인이 폐지돼 단일 호스트만 쓴다.
                return $"https://tile.openstreetmap.org/{zoom}/{coord.x}/{coord.y}.png";
        }
    }

    void Awake()
    {
        var shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            shader      = Shader.Find("Universal Render Pipeline/Unlit");
            isUrpShader = shader != null;
        }
        if (shader == null) shader = Shader.Find("Mobile/Diffuse");
        tileMat = new Material(shader);
    }

    void OnDestroy()
    {
        if (tileMat != null) Destroy(tileMat);
    }

    /// <summary>
    /// 제공자·API 키 설정. 이 컴포넌트는 런타임에 AddComponent로 붙기 때문에
    /// 인스펙터 값이 적용되지 않는다. MapSceneSetup이 씬에 저장된 값을 넘겨준다.
    /// </summary>
    public void Configure(MapTileProvider newProvider, string apiKey)
    {
        provider    = newProvider;
        cartoApiKey = apiKey != null ? apiKey.Trim() : "";
        warnedNoKey = false;
    }

    public void SetReference(double lat, double lon)
    {
        refLat = lat;
        refLon = lon;
        hasRef = true;
        // 기준점이 바뀌면 타일 좌표계가 달라지므로 강제 재로드
        centerTile = new Vector2Int(int.MinValue, int.MinValue);
        foreach (var kv in tiles)
            if (kv.Value != null) Destroy(kv.Value);
        tiles.Clear();
    }

    public void RefreshAt(double lat, double lon)
    {
        if (!hasRef) SetReference(lat, lon);

        var next = new Vector2Int(LonToTile(lon, zoom), LatToTile(lat, zoom));
        if (next == centerTile && tiles.Count > 0) return;

        centerTile = next;
        loadGeneration++;   // 진행 중인 로드에 취소 신호
        PruneTiles();

        if (!loading)
            StartCoroutine(LoadGrid(loadGeneration));
        // loading=true 이면 진행 중 LoadGrid가 세대 불일치를 감지하고 스스로 재시작
    }

    void PruneTiles()
    {
        var toRemove = new List<Vector2Int>();
        foreach (var kv in tiles)
        {
            if (Mathf.Abs(kv.Key.x - centerTile.x) > tileRadius ||
                Mathf.Abs(kv.Key.y - centerTile.y) > tileRadius)
                toRemove.Add(kv.Key);
        }
        foreach (var k in toRemove)
        {
            if (tiles[k] != null) Destroy(tiles[k]);
            tiles.Remove(k);
        }
    }

    IEnumerator LoadGrid(int gen)
    {
        loading = true;
        var snapshotCenter = centerTile;
        bool cancelled = false;

        for (int dy = -tileRadius; dy <= tileRadius && !cancelled; dy++)
        for (int dx = -tileRadius; dx <= tileRadius; dx++)
        {
            // 세대가 바뀌면 중단 (break가 내부 루프만 탈출하므로 플래그 사용)
            if (gen != loadGeneration) { cancelled = true; break; }

            var coord = new Vector2Int(snapshotCenter.x + dx, snapshotCenter.y + dy);
            if (!tiles.ContainsKey(coord))
                yield return StartCoroutine(FetchTile(coord, gen));

            yield return null; // 프레임 분산 (네트워크 집중 방지)
        }

        loading = false;

        // 로드 중에 중심이 바뀌었으면 새 세대로 재시작
        if (gen != loadGeneration)
            StartCoroutine(LoadGrid(loadGeneration));
    }

    IEnumerator FetchTile(Vector2Int coord, int gen)
    {
        string url = BuildTileUrl(coord);

        using var req = UnityWebRequestTexture.GetTexture(url);
        req.SetRequestHeader("User-Agent", UserAgent);
        yield return req.SendWebRequest();

        // 세대가 바뀌었으면 결과 버림
        if (gen != loadGeneration) yield break;

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[MapTile] 실패: {req.error} — {url}");
            yield break;
        }

        var tex = DownloadHandlerTexture.GetContent(req);
        tex.filterMode = FilterMode.Bilinear;

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = $"Tile({coord.x},{coord.y})";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        go.transform.position         = TileWorldCenter(coord);
        go.transform.localScale       = Vector3.one * TileWorldSize(coord);

        var mat = new Material(tileMat);
        mat.mainTexture = tex;
        if (isUrpShader) mat.SetTexture("_BaseMap", tex);
        go.GetComponent<Renderer>().material = mat;

        // 마지막으로 한 번 더 세대 확인 후 등록
        if (gen == loadGeneration)
            tiles[coord] = go;
        else
            Destroy(go);
    }

    // ── GPS → 월드 좌표 변환 ─────────────────────────────────────

    Vector3 TileWorldCenter(Vector2Int coord)
    {
        double n    = 1 << zoom;
        double lon  = (coord.x + 0.5) / n * 360.0 - 180.0;
        double latR = Math.Atan(Math.Sinh(Math.PI * (1.0 - 2.0 * (coord.y + 0.5) / n)));
        double lat  = latR * 180.0 / Math.PI;

        float dLat = (float)((lat  - refLat) * 111320.0 * WorldScale);
        float dLon = (float)((lon  - refLon) * 111320.0
                     * Math.Cos(refLat * Math.PI / 180.0) * WorldScale);

        return new Vector3(dLon, 0.002f, dLat);
    }

    float TileWorldSize(Vector2Int coord)
    {
        double n    = 1 << zoom;
        double lon0 =  coord.x      / n * 360.0 - 180.0;
        double lon1 = (coord.x + 1) / n * 360.0 - 180.0;
        float span  = (float)((lon1 - lon0) * 111320.0
                      * Math.Cos(refLat * Math.PI / 180.0) * WorldScale);
        return Mathf.Max(span, 0.01f);
    }

    // ── OSM 타일 좌표 변환 ───────────────────────────────────────

    static int LonToTile(double lon, int z)
        => (int)Math.Floor((lon + 180.0) / 360.0 * (1 << z));

    static int LatToTile(double lat, int z)
    {
        double r = lat * Math.PI / 180.0;
        return (int)Math.Floor(
            (1 - Math.Log(Math.Tan(r) + 1.0 / Math.Cos(r)) / Math.PI) / 2.0 * (1 << z));
    }
}
