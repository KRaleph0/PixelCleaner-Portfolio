using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using PixelCleaners;

/// <summary>
/// 메뉴: PixelCleaners → 생명체 아이콘 촬영
///
/// CreatureRoster 13종을 게임과 같은 크기 맞춤(CreatureModelFitter)으로 불러와 정면에서 촬영하고
/// Assets/Resources/CreatureIcons/{ID}.png (투명 배경, 256×256, Sprite)로 저장한다.
/// 공장 슬롯·도감이 CreatureRoster.LoadIcon으로 이 이미지를 쓴다.
///
/// 모델을 바꾸면 이 메뉴를 다시 실행하면 된다.
/// </summary>
public static class CreatureIconBaker
{
    const string OutputDir   = "Assets/Resources/" + CreatureRoster.IconFolder;
    const int    IconSize    = 256;
    const int    Supersample = 2;      // 2배로 찍고 줄여서 가장자리 계단 완화
    const float  Padding     = 1.08f;  // 모델 주변 여백

    [MenuItem("PixelCleaners/생명체 아이콘 촬영")]
    public static void BakeAll()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("아이콘 촬영", "Play 모드를 끄고 실행하세요.", "확인");
            return;
        }

        // 텍스처가 빠진 회색 머티리얼로 찍히지 않도록 머티리얼부터 맞춘다
        string materialReport = CreatureMaterialSetup.SetupAll();

        EnsureFolder(OutputDir);
        var baked   = new List<string>();
        var skipped = new List<string>();

        var pru = new PreviewRenderUtility();
        try
        {
            SetupLights(pru);

            var all = CreatureRoster.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                EditorUtility.DisplayProgressBar("생명체 아이콘 촬영", $"{e.id} ({i + 1}/{all.Count})", (float)i / all.Count);

                var prefab = CreatureRoster.LoadModel(e);
                if (prefab == null) { skipped.Add(e.id); continue; }

                // 게임과 동일한 정규화: 높이 1, 발밑 원점, 정면 +Z
                var go = CreatureModelFitter.Build(prefab, $"Icon_{e.id}", 1f, e.modelYawOffset, e.fallbackColor);
                try
                {
                    pru.AddSingleGO(go);
                    Frame(pru.camera, go);

                    var onBlack = RenderOn(pru, Color.black);
                    var onWhite = RenderOn(pru, Color.white);
                    var icon    = ComposeAlpha(onBlack, onWhite, IconSize);
                    Object.DestroyImmediate(onBlack);
                    Object.DestroyImmediate(onWhite);

                    string path = $"{OutputDir}/{e.id}.png";
                    File.WriteAllBytes(path, icon.EncodeToPNG());
                    Object.DestroyImmediate(icon);
                    baked.Add(path);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }
        }
        finally
        {
            pru.Cleanup();
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.Refresh();
        foreach (var path in baked) ConfigureSprite(path);
        EditorUtility.UnloadUnusedAssetsImmediate();

        string msg = $"[머티리얼] {materialReport}\n\n[아이콘] 촬영 완료 {baked.Count}종 → {OutputDir}/";
        if (skipped.Count > 0) msg += $"\n모델 없음(건너뜀): {string.Join(", ", skipped)}";
        Debug.Log($"[IconBaker] {msg.Replace('\n', ' ')}");
        EditorUtility.DisplayDialog("생명체 아이콘 촬영", msg, "확인");
    }

    // ── 촬영 ────────────────────────────────────────────────────────

    static void SetupLights(PreviewRenderUtility pru)
    {
        // 카메라는 +Z 쪽에서 -Z를 본다. 조명도 정면(카메라 쪽)에서 비춘다.
        pru.lights[0].intensity          = 1.15f;
        pru.lights[0].transform.rotation = Quaternion.Euler(35f, 205f, 0f);   // 왼쪽 위 키 라이트
        pru.lights[1].intensity          = 0.55f;
        pru.lights[1].transform.rotation = Quaternion.Euler(15f, 140f, 0f);   // 오른쪽 필 라이트
        pru.ambientColor                 = new Color(0.38f, 0.38f, 0.42f);
    }

    static void Frame(Camera cam, GameObject go)
    {
        var b = new Bounds(go.transform.position, Vector3.zero);
        bool found = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!found) { b = r.bounds; found = true; }
            else b.Encapsulate(r.bounds);
        }

        cam.orthographic     = true;
        cam.orthographicSize = Mathf.Max(b.size.x, b.size.y) * 0.5f * Padding;
        // 정면(+Z)을 마주 보도록 +Z 앞에서 -Z 방향으로
        cam.transform.position = new Vector3(b.center.x, b.center.y, b.max.z + 3f);
        cam.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        cam.nearClipPlane      = 0.01f;
        cam.farClipPlane       = 3f + b.size.z + 3f;
        cam.clearFlags         = CameraClearFlags.SolidColor;
    }

    static Texture2D RenderOn(PreviewRenderUtility pru, Color background)
    {
        int px = IconSize * Supersample;
        pru.camera.backgroundColor = background;
        pru.BeginStaticPreview(new Rect(0, 0, px, px));
        pru.Render(allowScriptableRenderPipeline: true, updatefov: false);   // URP 머티리얼로 렌더
        return pru.EndStaticPreview();
    }

    /// <summary>
    /// 검은 배경/흰 배경 두 장의 차이로 투명도를 복원한다.
    ///   검정 위: C_b = a·F          흰색 위: C_w = a·F + (1 − a)
    ///   → a = 1 − (C_w − C_b),  F = C_b / a
    /// 렌더러가 알파를 보존하지 않아도 동작한다.
    /// </summary>
    static Texture2D ComposeAlpha(Texture2D onBlack, Texture2D onWhite, int size)
    {
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        var result  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels  = new Color[size * size];

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
            var cb = onBlack.GetPixelBilinear(u, v);
            var cw = onWhite.GetPixelBilinear(u, v);
            if (linear) { cb = cb.linear; cw = cw.linear; }   // 합성은 선형 공간에서 일어났다

            float diff = ((cw.r - cb.r) + (cw.g - cb.g) + (cw.b - cb.b)) / 3f;
            float a    = Mathf.Clamp01(1f - diff);

            Color c = a > 0.001f
                ? new Color(Mathf.Clamp01(cb.r / a), Mathf.Clamp01(cb.g / a), Mathf.Clamp01(cb.b / a), a)
                : new Color(0f, 0f, 0f, 0f);
            if (linear) { var g = c.gamma; g.a = a; c = g; }
            pixels[y * size + x] = c;
        }

        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    // ── 에셋 설정 ──────────────────────────────────────────────────

    static void ConfigureSprite(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) return;
        ti.textureType         = TextureImporterType.Sprite;
        ti.spriteImportMode    = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled       = false;
        ti.wrapMode            = TextureWrapMode.Clamp;
        ti.maxTextureSize      = IconSize;
        ti.SaveAndReimport();
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
