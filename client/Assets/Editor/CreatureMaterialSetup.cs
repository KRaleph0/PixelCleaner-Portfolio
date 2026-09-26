using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PixelCleaners;

/// <summary>
/// 메뉴: PixelCleaners → 생명체 머티리얼 설정
///
/// Meshy FBX는 텍스처를 파일 내부에만 넣고, 머티리얼은 "texture_0.png"라는 이름으로 텍스처를 찾는다.
/// 프로젝트에 그 파일이 없어 Unity가 텍스처 없는 회색 머티리얼을 만든다.
/// 13종 모두 내부 이름이 texture_0.png로 같아서 "이름으로 찾기"는 남의 텍스처를 가져갈 위험이 있다.
///
/// 그래서 생명체마다
///   Assets/Characters/{ID}/*_texture.png  (Meshy가 함께 준 기본 색상 PNG)
/// 를 쓰는 URP/Lit 머티리얼 {ID}_Mat.mat 을 만들고, Resources/Characters/{ID}.fbx 의
/// 내장 머티리얼을 그 머티리얼로 명시적으로 교체(Remap)한다.
///
/// "생명체 아이콘 촬영"이 촬영 전에 이 작업을 먼저 실행한다.
/// </summary>
public static class CreatureMaterialSetup
{
    const string ModelRoot     = "Assets/Resources/Characters";
    const string SourceRoot    = "Assets/Characters";
    const int    MaxTextureSize = 1024;   // 70cm 생명체를 2m에서 보면 2048과 차이가 거의 없다

    [MenuItem("PixelCleaners/생명체 머티리얼 설정")]
    static void Menu()
    {
        string report = SetupAll();
        EditorUtility.DisplayDialog("생명체 머티리얼 설정", report, "확인");
    }

    /// <returns>사람이 읽을 결과 요약</returns>
    public static string SetupAll()
    {
        var ok   = new List<string>();
        var fail = new List<string>();
        var all  = CreatureRoster.All;
        try
        {
            for (int i = 0; i < all.Count; i++)
            {
                string id = all[i].id;
                EditorUtility.DisplayProgressBar("생명체 머티리얼 설정", $"{id} ({i + 1}/{all.Count})", (float)i / all.Count);
                string err = Setup(id);
                if (err == null) ok.Add(id);
                else             fail.Add($"{id}: {err}");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        string report = $"설정 완료 {ok.Count}종";
        if (fail.Count > 0) report += "\n\n실패:\n" + string.Join("\n", fail);
        Debug.Log($"[MaterialSetup] {report.Replace('\n', ' ')}");
        return report;
    }

    /// <returns>실패 사유. 성공이면 null</returns>
    static string Setup(string id)
    {
        string fbxPath = $"{ModelRoot}/{id}.fbx";
        if (!(AssetImporter.GetAtPath(fbxPath) is ModelImporter importer))
            return File.Exists($"{ModelRoot}/{id}.prefab")
                ? "프리팹 모델은 원본 FBX에서 설정하세요"
                : "Resources/Characters에 FBX 없음";

        string folder = FindSourceFolder(id);
        if (folder == null) return $"{SourceRoot}/{id}/ 폴더 없음";

        var basePngs = Directory.GetFiles(folder)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => p.EndsWith("_texture.png"))
            .ToArray();
        if (basePngs.Length != 1)
            return $"기본 색상 PNG(*_texture.png)가 {basePngs.Length}개 — 1개여야 합니다";

        // ── 텍스처 ──
        string texPath = basePngs[0];
        if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
        {
            bool dirty = false;
            if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
            if (!ti.sRGBTexture)                               { ti.sRGBTexture = true;                         dirty = true; }
            if (ti.maxTextureSize != MaxTextureSize)           { ti.maxTextureSize = MaxTextureSize;            dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) return "PNG를 텍스처로 불러오지 못함";

        // ── 머티리얼 (재실행 시 기존 파일 갱신) ──
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return "URP/Lit 셰이더를 찾지 못함";

        string matPath = $"{folder}/{id}_Mat.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = shader;
        mat.SetTexture("_BaseMap", tex);
        mat.mainTexture = tex;
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", 0.2f);   // 카툰 스타일 — 번들거림 최소화
        EditorUtility.SetDirty(mat);

        // ── FBX 내장 머티리얼 → 새 머티리얼로 교체 ──
        // 내장 머티리얼 하위 에셋 + (재실행 대비) 이미 교체된 항목을 모두 대상으로 한다
        var ids = new List<AssetImporter.SourceAssetIdentifier>();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is Material embedded)
                ids.Add(new AssetImporter.SourceAssetIdentifier(embedded));
        foreach (var kv in importer.GetExternalObjectMap())
            if (kv.Key.type == typeof(Material))
                ids.Add(kv.Key);
        if (ids.Count == 0) return "FBX에서 머티리얼을 찾지 못함";

        foreach (var sid in ids.Distinct())
            importer.AddRemap(sid, mat);
        importer.SaveAndReimport();
        return null;
    }

    /// Assets/Characters/{ID} 폴더 (대소문자 무시 — canbug 폴더 대응)
    static string FindSourceFolder(string id)
    {
        if (!Directory.Exists(SourceRoot)) return null;
        foreach (var dir in Directory.GetDirectories(SourceRoot))
            if (string.Equals(Path.GetFileName(dir), id, System.StringComparison.OrdinalIgnoreCase))
                return dir.Replace('\\', '/');
        return null;
    }
}
