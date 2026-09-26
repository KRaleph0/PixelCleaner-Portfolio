using UnityEngine;

/// <summary>
/// 서버 주소 설정. 저장소에는 예시 주소만 두고, 실제 주소는
/// Assets/Resources/ServerConfig.json (git 제외) 에서 읽는다.
/// 형식은 client/ServerConfig.example.json 참고.
/// </summary>
public static class ServerConfig
{
    public const string Placeholder = "https://your-server.example.com";

    [System.Serializable] class Data { public string baseUrl; }

    static string cached;

    public static string BaseUrl => cached ??= Load();

    static string Load()
    {
        var asset = Resources.Load<TextAsset>("ServerConfig");
        if (asset != null)
        {
            try
            {
                var data = JsonUtility.FromJson<Data>(asset.text);
                if (data != null && !string.IsNullOrWhiteSpace(data.baseUrl))
                    return data.baseUrl.Trim().TrimEnd('/');
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[ServerConfig] ServerConfig.json 파싱 실패: {e.Message}");
            }
        }

        Debug.LogWarning("[ServerConfig] Assets/Resources/ServerConfig.json 이 없어 예시 주소를 사용합니다. " +
                         "client/ServerConfig.example.json 을 복사해 실제 서버 주소로 채우세요.");
        return Placeholder;
    }
}
