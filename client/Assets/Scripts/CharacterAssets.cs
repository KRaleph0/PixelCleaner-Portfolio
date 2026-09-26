using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 생명체 모델 로더. Assets/Resources/Characters/{이름}.prefab 또는 .fbx
/// 에디터: AssetDatabase, 빌드: Resources.Load
/// </summary>
public static class CharacterAssets
{
    const string CharFbxRoot = "Assets/Resources/Characters/";

    /// <summary>
    /// 파일명(확장자 제외)으로 캐릭터 모델 GameObject를 반환.
    /// .prefab을 .fbx보다 우선한다 — Decimate 도구가 만든 {ID}.prefab이 원본 FBX를 대체한다.
    /// </summary>
    public static GameObject Load(string fbxName)
    {
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharFbxRoot}{fbxName}.prefab");
        if (prefab != null) return prefab;
        return AssetDatabase.LoadAssetAtPath<GameObject>($"{CharFbxRoot}{fbxName}.fbx");
#else
        // 빌드: Assets/Resources/Characters/{fbxName}.prefab 또는 .fbx
        // 같은 이름이 둘 다 있으면 어느 쪽이 로드될지 보장되지 않는다 (Decimate 도구가 FBX를 옮김)
        return Resources.Load<GameObject>($"Characters/{fbxName}");
#endif
    }
}
