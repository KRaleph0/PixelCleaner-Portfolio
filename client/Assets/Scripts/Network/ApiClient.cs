using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class ApiClient
{
    static string BaseUrl => ServerConfig.BaseUrl;

    // ── 데이터 클래스 ─────────────────────────────────────────────

    [Serializable] public class PlayerRegisterRequest  { public string device_id; public string display_name; }
    [Serializable] public class PlayerResponse         { public string player_id; public string display_name; }

    [Serializable] public class DeliverySubmitRequest  { public string player_id; public int score; }
    [Serializable] public class DeliverySubmitResponse { public int accepted_score; public int rank; }

    [Serializable] public class RankEntry       { public int rank; public string display_name; public int score; }
    [Serializable] public class RankingResponse { public RankEntry[] entries; public int total; }

    // ── API ───────────────────────────────────────────────────────

    public static IEnumerator Register(string displayName, Action<PlayerResponse> onDone, Action<long> onError = null)
    {
        string json = JsonUtility.ToJson(new PlayerRegisterRequest {
            device_id    = SystemInfo.deviceUniqueIdentifier,
            display_name = displayName
        });
        yield return Post<PlayerResponse>($"{BaseUrl}/players/register", json, onDone, onError);
    }

    public static IEnumerator Submit(int score, Action<DeliverySubmitResponse> onDone, Action<long> onError = null)
    {
        string json = JsonUtility.ToJson(new DeliverySubmitRequest {
            player_id = PlayerSession.PlayerId,
            score     = score
        });
        yield return Post<DeliverySubmitResponse>($"{BaseUrl}/delivery/submit", json, onDone, onError);
    }

    public static IEnumerator GetRanking(int limit, int offset, Action<RankingResponse> onDone, Action onError = null)
    {
        using (var req = UnityWebRequest.Get($"{BaseUrl}/ranking?limit={limit}&offset={offset}"))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onDone?.Invoke(JsonUtility.FromJson<RankingResponse>(req.downloadHandler.text));
            else
            {
                Debug.LogWarning($"[ApiClient] GET /ranking failed: {req.error}");
                onError?.Invoke();
            }
        }
    }

    public static IEnumerator GetMyRank(Action<RankEntry> onDone, Action onError = null)
    {
        using (var req = UnityWebRequest.Get($"{BaseUrl}/ranking/me?player_id={PlayerSession.PlayerId}"))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onDone?.Invoke(JsonUtility.FromJson<RankEntry>(req.downloadHandler.text));
            else
            {
                Debug.LogWarning($"[ApiClient] GET /ranking/me failed: {req.error}");
                onError?.Invoke();
            }
        }
    }

    // ── 공통 POST 헬퍼 ────────────────────────────────────────────

    static IEnumerator Post<T>(string url, string json, Action<T> onDone, Action<long> onError)
    {
        using (var req = new UnityWebRequest(url, "POST"))
        {
            req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onDone?.Invoke(JsonUtility.FromJson<T>(req.downloadHandler.text));
            else
            {
                Debug.LogWarning($"[ApiClient] POST {url} failed ({req.responseCode}): {req.error}");
                onError?.Invoke(req.responseCode);
            }
        }
    }
}
