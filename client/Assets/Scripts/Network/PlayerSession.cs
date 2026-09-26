using System;
using System.Collections;
using UnityEngine;

public static class PlayerSession
{
    const string PrefKey     = "player_id";
    const string NamePrefKey = "display_name";
    const string DefaultName = "나";

    public static string PlayerId     => PlayerPrefs.GetString(PrefKey, "");
    public static string DisplayName  => PlayerPrefs.GetString(NamePrefKey, DefaultName);
    public static bool   IsRegistered => !string.IsNullOrEmpty(PlayerId);

    /// <summary>서버 등록 없이 로컬로만 시작한 상태. 납품 제출 시 404 → 자동 재등록된다.</summary>
    public static bool IsOffline => PlayerId.StartsWith(OfflinePrefix);

    const string OfflinePrefix = "local:";

    /// <summary>
    /// 서버 연결이 안 될 때 로컬 임시 ID로 게임을 시작한다.
    /// 이후 납품 제출이 404를 받으면 <see cref="ReRegister"/>가 이 닉네임으로 정식 등록한다.
    /// </summary>
    public static void SaveOffline(string displayName)
        => Save(OfflinePrefix + Guid.NewGuid().ToString("N"), displayName);

    /// <summary>등록 결과를 저장한다. 로그인 씬과 자동 등록이 공용으로 쓴다.</summary>
    public static void Save(string playerId, string displayName)
    {
        PlayerPrefs.SetString(PrefKey, playerId);
        if (!string.IsNullOrEmpty(displayName))
            PlayerPrefs.SetString(NamePrefKey, displayName);
        PlayerPrefs.Save();
    }

    /// <summary>player_id 없으면 서버에 등록 후 저장. 이미 있으면 즉시 완료.</summary>
    public static IEnumerator EnsureRegistered(Action onDone = null)
    {
        if (IsRegistered) { onDone?.Invoke(); yield break; }

        // 로그인 씬에서 입력한 닉네임이 있으면 그대로 쓴다 (없으면 DefaultName).
        yield return ApiClient.Register(DisplayName, res => {
            Save(res.player_id, res.display_name);
            Debug.Log($"[PlayerSession] 등록 완료: {res.player_id}");
        });

        onDone?.Invoke();
    }

    /// <summary>player_id 만료(404) 시 재등록.</summary>
    public static IEnumerator ReRegister(Action onDone = null)
    {
        PlayerPrefs.DeleteKey(PrefKey);
        yield return EnsureRegistered(onDone);
    }
}
