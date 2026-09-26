using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace PixelCleaners.EditorTools
{
    /// <summary>
    /// 메뉴: PixelCleaners → 모델 폴리곤 줄이기 (Decimate)
    ///
    /// 고폴리 원본(AI 생성 FBX 등)을 원본 UV·텍스처를 유지한 채 줄여 두 버전을 만든다.
    ///   AR 버전  → Assets/Resources/Characters/{ID}.prefab       (가까이서 보는 AR 씬)
    ///   지도 버전 → Assets/Resources/Characters/{ID}_Map.prefab   (화면에 작게 보이는 지도 씬)
    /// CharacterAssets가 .prefab을 .fbx보다 우선하고, CreatureRoster가 지도에서 _Map을 먼저 찾으므로
    /// 코드 수정 없이 결과가 적용된다.
    ///
    /// 두 버전 모두 원본에서 각각 줄인다 (AR 버전을 다시 줄이면 오차가 누적된다).
    ///
    /// 이 어셈블리는 com.whinarn.unitymeshsimplifier 패키지가 있을 때만 컴파일된다
    /// (asmdef defineConstraints). 패키지가 없는 환경에서도 프로젝트 컴파일은 깨지지 않는다.
    /// </summary>
    public class ModelDecimatorWindow : EditorWindow
    {
        const string OutputRoot = "Assets/Resources/Characters";
        // 생성된 메시는 Resources 밖에 둔다 — 프리팹이 참조하므로 빌드에는 필요한 만큼만 포함된다
        const string MeshRoot   = "Assets/Characters/_Decimated";
        const string BackupRoot = "Assets/Characters/_Backup";
        const string MapSuffix  = "_Map";   // CreatureRoster.MapSuffix 와 일치해야 함

        // 원본 대비 이 비율보다 작게 줄이면 사전 경고
        const float AggressiveRatio = 0.05f;
        // 접힌 면이 삼각형 수의 이 비율을 넘으면 결과 경고.
        // 실측: 원본 38만 = 0, Meshy 리메시 1만 = 0.02~0.06%, 깨져 보인 결과 = 0.25%·6%
        const float FoldedWarnRatio = 0.001f;

        GameObject source;
        GameObject measured;
        int        sourceTris;

        string outputId = "";

        bool buildAr        = true;
        int  arTargetTris   = 30000;
        bool buildMap       = true;
        int  mapTargetTris  = 2000;

        bool preserveUVSeams     = true;
        bool preserveCurvature   = true;
        bool preserveBorders     = false;
        // 지도 모델은 화면에 수십 픽셀이라 텍스처 이음새가 벌어져도 보이지 않는다.
        // 이음새를 보존하면 2천 같은 낮은 목표에 도달하지 못하는 경우가 많아 기본으로 끈다.
        bool mapPreserveUVSeams  = false;

        Vector2 scroll;

        [MenuItem("PixelCleaners/모델 폴리곤 줄이기 (Decimate)")]
        static void Open()
        {
            var w = GetWindow<ModelDecimatorWindow>("Decimate");
            w.minSize = new Vector2(420f, 480f);
        }

        // 프로젝트 창에서 3D 모델을 선택한 채 창을 열면 자동으로 채운다.
        // UI 프리팹 등 모델이 아닌 에셋은 무시한다 (체력바 프리팹이 채워지던 문제).
        void OnEnable()
        {
            if (Selection.activeObject is GameObject go &&
                AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(go)) is ModelImporter)
                source = go;
        }

        /// <summary>
        /// 출력 ID 추정: Assets/Characters/{ID}/원본.fbx 구조면 폴더 이름,
        /// Resources/Characters/{ID}.fbx 면 파일 이름, 그 외에는 오브젝트 이름.
        /// </summary>
        static string GuessId(GameObject go)
        {
            string path  = AssetDatabase.GetAssetPath(go);
            string[] seg = path.Split('/');
            if (seg.Length >= 4 && seg[0] == "Assets" && seg[1] == "Characters" && !seg[2].StartsWith("_"))
                return seg[2];
            if (path.StartsWith(OutputRoot + "/"))
                return Path.GetFileNameWithoutExtension(path);
            return go.name;
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            try { DrawGUI(); }
            finally { EditorGUILayout.EndScrollView(); }
        }

        void DrawGUI()
        {
            EditorGUILayout.HelpBox(
                "고폴리 원본을 원본 UV·텍스처를 유지한 채 줄입니다.\n" +
                "AR 버전 → {ID}.prefab   /   지도 버전 → {ID}_Map.prefab\n" +
                "생명체 로스터가 자동으로 사용합니다. 원본 FBX는 Resources 밖(Assets/Characters/{id}/)에 두세요.",
                MessageType.Info);

            source = (GameObject)EditorGUILayout.ObjectField("원본 모델", source, typeof(GameObject), false);
            if (source != measured)
            {
                measured   = source;
                sourceTris = source != null ? CountTris(source) : 0;
                // 원본이 바뀌면 ID도 항상 새로 추정한다. 비어 있을 때만 채우면
                // 이전 원본의 이름이 남아 결과가 엉뚱한 이름으로 저장된다.
                if (source != null) outputId = GuessId(source);
            }
            if (source == null) return;

            EditorGUILayout.LabelField("원본 삼각형", sourceTris.ToString("N0"));
            if (sourceTris == 0)
            {
                EditorGUILayout.HelpBox("메시를 찾지 못했습니다. FBX 또는 프리팹을 넣어 주세요.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();
            outputId = EditorGUILayout.TextField(
                new GUIContent("출력 ID", "생명체 로스터 ID와 같아야 합니다 (예: CanBug)"), outputId);
            string id    = outputId != null ? outputId.Trim() : "";
            bool validId = id.Length > 0 && id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

            // ── AR 버전 ──
            EditorGUILayout.Space();
            buildAr = EditorGUILayout.ToggleLeft($"AR 버전  →  {id}.prefab", buildAr, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!buildAr))
            {
                arTargetTris = TargetSlider(arTargetTris);
                EditorGUILayout.LabelField(" ", "권장 1.5만, 곡면이 많은 모델 최대 3만", EditorStyles.miniLabel);
            }

            // ── 지도 버전 ──
            EditorGUILayout.Space();
            buildMap = EditorGUILayout.ToggleLeft($"지도 버전  →  {id}{MapSuffix}.prefab", buildMap, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(!buildMap))
            {
                mapTargetTris = TargetSlider(mapTargetTris);
                EditorGUILayout.LabelField(" ", "권장 약 2천 — 지도에서는 화면에 작게 보인다", EditorStyles.miniLabel);
                mapPreserveUVSeams = EditorGUILayout.ToggleLeft(
                    new GUIContent("지도 버전도 UV 이음새 보존",
                        "켜면 텍스처 이음새가 덜 벌어지지만 낮은 목표치까지 줄지 못할 수 있습니다"),
                    mapPreserveUVSeams);
            }

            // ── 공통 옵션 ──
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("공통 옵션", EditorStyles.boldLabel);
            preserveUVSeams   = EditorGUILayout.ToggleLeft("UV 이음새 보존 — 텍스처 찢어짐 방지 (AR 권장)", preserveUVSeams);
            preserveCurvature = EditorGUILayout.ToggleLeft("곡률 보존 — 캔·병 같은 곡면 형태 유지 (권장)", preserveCurvature);
            preserveBorders   = EditorGUILayout.ToggleLeft("열린 가장자리 보존 — 입구·구멍이 있는 모델", preserveBorders);

            // ── 경고 ──
            EditorGUILayout.Space();
            if (!validId)
                EditorGUILayout.HelpBox("출력 ID가 비었거나 파일명에 쓸 수 없는 문자가 있습니다.", MessageType.Error);
            else
            {
                if (buildAr && File.Exists($"{OutputRoot}/{id}.fbx"))
                    EditorGUILayout.HelpBox(
                        $"Resources에 같은 이름의 {id}.fbx 가 있습니다. 빌드에서 어느 쪽이 로드될지 보장되지 않아 " +
                        $"생성 시 {BackupRoot}/ 로 옮깁니다.", MessageType.Warning);
                if (AssetDatabase.GetAssetPath(source).StartsWith(OutputRoot + "/"))
                    EditorGUILayout.HelpBox(
                        "원본이 Resources 안에 있습니다. Resources의 파일은 사용 여부와 무관하게 빌드에 포함되므로 " +
                        "고폴리 원본은 Assets/Characters/{id}/ 로 옮기는 것을 권합니다.", MessageType.Warning);
                if (!buildAr && !buildMap)
                    EditorGUILayout.HelpBox("생성할 버전을 하나 이상 선택하세요.", MessageType.Error);

                int smallest = Mathf.Min(buildAr  ? arTargetTris  : int.MaxValue,
                                         buildMap ? mapTargetTris : int.MaxValue);
                if (smallest < sourceTris * AggressiveRatio)
                    EditorGUILayout.HelpBox(
                        $"원본의 {(float)smallest / sourceTris:P1}까지 줄입니다. AI 생성 모델을 이 정도로 크게 줄이면 " +
                        "면이 접혀 깨져 보이기 쉽습니다. 결과에 경고가 나오면 Meshy Remesh로 목표 폴리곤을 지정해 받은 뒤 " +
                        "Resources/Characters/{ID}.fbx 또는 {ID}_Map.fbx 로 넣으세요.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(!validId || (!buildAr && !buildMap)))
            {
                if (GUILayout.Button("생성", GUILayout.Height(34f)))
                    Run(id);
            }
        }

        int TargetSlider(int value)
        {
            int max = Mathf.Max(sourceTris, 500);
            int v   = EditorGUILayout.IntSlider("목표 삼각형", Mathf.Clamp(value, 100, max), 100, max);
            EditorGUILayout.LabelField(" ", $"원본의 {(float)v / sourceTris:P1}", EditorStyles.miniLabel);
            return v;
        }

        // ── 생성 ────────────────────────────────────────────────────

        void Run(string id)
        {
            string fbxClash = $"{OutputRoot}/{id}.fbx";
            if (buildAr && File.Exists(fbxClash) &&
                !EditorUtility.DisplayDialog("Decimate",
                    $"{fbxClash} 를 {BackupRoot}/ 로 옮기고 {id}.prefab 을 생성합니다.", "진행", "취소"))
                return;

            var baseOptions = SimplificationOptions.Default;
            baseOptions.PreserveUVSeamEdges      = preserveUVSeams;
            baseOptions.PreserveSurfaceCurvature = preserveCurvature;
            baseOptions.PreserveBorderEdges      = preserveBorders;

            var mapOptions = baseOptions;
            mapOptions.PreserveUVSeamEdges = mapPreserveUVSeams;

            EnsureFolder(MeshRoot);
            EnsureFolder(OutputRoot);

            var report = new List<string>();
            GameObject lastSaved = null;
            try
            {
                if (buildAr)
                {
                    // AR 프리팹을 저장하기 전에 같은 이름 FBX를 치운다
                    if (File.Exists(fbxClash)) MoveToBackup(fbxClash, id);
                    var (prefab, tris, folded) = BuildVariant(id, "", arTargetTris, baseOptions, "AR");
                    report.Add(VariantLine("AR", id, tris, arTargetTris, folded, prefab != null));
                    if (prefab != null) lastSaved = prefab;
                }
                if (buildMap)
                {
                    var (prefab, tris, folded) = BuildVariant(id, MapSuffix, mapTargetTris, mapOptions, "지도");
                    report.Add(VariantLine("지도", id + MapSuffix, tris, mapTargetTris, folded, prefab != null));
                    if (prefab != null) lastSaved = prefab;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
            }

            string msg = $"원본 {sourceTris:N0} 삼각형\n\n" + string.Join("\n", report);
            Debug.Log($"[Decimate] {msg.Replace('\n', ' ')}");
            if (lastSaved != null) EditorGUIUtility.PingObject(lastSaved);
            EditorUtility.DisplayDialog("Decimate", msg + "\n\nPlay 하면 로스터가 이 모델을 사용합니다.", "확인");
        }

        static string VariantLine(string label, string name, int tris, int target, int folded, bool ok)
        {
            if (!ok) return $"{label}: {name}.prefab 저장 실패 — Console 확인";
            string line = $"{label}: {name}.prefab  →  {tris:N0} 삼각형, 접힌 면 {folded:N0}";
            if (folded > tris * FoldedWarnRatio)
                line += "\n   ⚠ 접힌 면이 많아 깨져 보일 수 있습니다 — 이 버전은 Meshy Remesh를 권장합니다";
            // 이음새·가장자리 보존 때문에 목표까지 못 줄이는 경우가 있다
            if (tris > target * 1.25f)
                line += $"\n   ⚠ 목표 {target:N0}에 도달하지 못함 — UV 이음새/가장자리 보존을 끄면 더 줄어듭니다";
            return line;
        }

        (GameObject prefab, int tris, int folded) BuildVariant(string id, string suffix, int targetTris,
                                                               SimplificationOptions options, string label)
        {
            string name  = id + suffix;
            float quality = Mathf.Clamp((float)targetTris / sourceTris, 0.001f, 1f);

            // PrefabUtility.InstantiatePrefab이 아니라 일반 복제를 쓴다.
            // 프리팹 인스턴스로 저장하면 원본 FBX를 베이스로 하는 Variant가 되어
            // 줄이기 전 고폴리 메시까지 빌드에 딸려 들어간다.
            var clone = Instantiate(source);
            clone.name = name;
            int resultTris = 0;
            int folded     = 0;
            try
            {
                var jobs = new List<(Mesh mesh, System.Action<Mesh> assign)>();
                foreach (var mf in clone.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) { var t = mf; jobs.Add((t.sharedMesh, m => t.sharedMesh = m)); }
                foreach (var smr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr.sharedMesh != null) { var t = smr; jobs.Add((t.sharedMesh, m => t.sharedMesh = m)); }

                for (int i = 0; i < jobs.Count; i++)
                {
                    var src = jobs[i].mesh;
                    EditorUtility.DisplayProgressBar($"Decimate — {label} 버전",
                        $"{src.name} ({i + 1}/{jobs.Count})  목표 {targetTris:N0}", (float)i / jobs.Count);

                    var simplifier = new MeshSimplifier { SimplificationOptions = options };
                    simplifier.Initialize(src);
                    simplifier.SimplifyMesh(quality);

                    var result = simplifier.ToMesh();
                    result.name = jobs.Count > 1 ? $"{name}_{i}" : $"{name}_mesh";
                    result.RecalculateBounds();

                    // 재실행 시 같은 경로를 덮어쓴다 (프리팹도 곧 다시 저장하므로 참조가 끊기지 않음)
                    string meshPath = $"{MeshRoot}/{result.name}.asset";
                    if (File.Exists(meshPath)) AssetDatabase.DeleteAsset(meshPath);
                    AssetDatabase.CreateAsset(result, meshPath);

                    jobs[i].assign(result);
                    resultTris += CountTris(result);
                    folded     += CountFoldedEdges(result);
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(clone, $"{OutputRoot}/{name}.prefab", out bool ok);
                return (ok ? saved : null, resultTris, folded);
            }
            finally
            {
                DestroyImmediate(clone);
            }
        }

        static void MoveToBackup(string assetPath, string id)
        {
            EnsureFolder(BackupRoot);
            string dest = AssetDatabase.GenerateUniqueAssetPath($"{BackupRoot}/{id}.fbx");
            string err  = AssetDatabase.MoveAsset(assetPath, dest);
            if (!string.IsNullOrEmpty(err)) Debug.LogWarning($"[Decimate] FBX 이동 실패: {err}");
            else                            Debug.Log($"[Decimate] {assetPath} → {dest}");
        }

        // ── 헬퍼 ────────────────────────────────────────────────────

        static int CountTris(GameObject go)
        {
            int total = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) total += CountTris(mf.sharedMesh);
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null) total += CountTris(smr.sharedMesh);
            return total;
        }

        static int CountTris(Mesh mesh)
        {
            long indices = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
                if (mesh.GetTopology(s) == MeshTopology.Triangles)
                    indices += mesh.GetIndexCount(s);
            return (int)(indices / 3);
        }

        /// <summary>
        /// 접힌 면 검사. UV 이음새로 쪼개진 정점을 위치 기준으로 합친 뒤,
        /// 같은 방향의 변이 두 번 나오면 인접한 두 면 중 하나가 뒤집혀 접힌 것이다.
        /// 뒤집힌 면은 컬링되어 구멍·삐죽한 조각처럼 보인다.
        /// </summary>
        static int CountFoldedEdges(Mesh mesh)
        {
            var verts  = mesh.vertices;
            var size   = mesh.bounds.size;
            float cell = Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 1e-5f;
            if (cell <= 0f) return 0;

            var weld = new int[verts.Length];
            var grid = new Dictionary<Vector3Int, int>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                var k = new Vector3Int(Mathf.RoundToInt(v.x / cell),
                                       Mathf.RoundToInt(v.y / cell),
                                       Mathf.RoundToInt(v.z / cell));
                if (!grid.TryGetValue(k, out int id)) { id = grid.Count; grid[k] = id; }
                weld[i] = id;
            }

            var directed = new HashSet<long>();
            int folded = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                if (mesh.GetTopology(s) != MeshTopology.Triangles) continue;
                var tri = mesh.GetTriangles(s);
                for (int t = 0; t + 2 < tri.Length; t += 3)
                {
                    int a = weld[tri[t]], b = weld[tri[t + 1]], c = weld[tri[t + 2]];
                    if (a == b || b == c || a == c) continue;
                    if (!directed.Add(((long)a << 32) | (uint)b)) folded++;
                    if (!directed.Add(((long)b << 32) | (uint)c)) folded++;
                    if (!directed.Add(((long)c << 32) | (uint)a)) folded++;
                }
            }
            return folded;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts   = path.Split('/');
            string curr = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{curr}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(curr, parts[i]);
                curr = next;
            }
        }
    }
}
