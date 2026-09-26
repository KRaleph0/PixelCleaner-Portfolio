# Pixel Cleaners — Unity 클라이언트 연동 메뉴얼

## 개요

기존 PlayerPrefs 로컬 랭킹을 제거하고, 아래 글로벌 랭킹 서버(REST API)로 교체한다.
서버는 Cloudflare Tunnel을 통해 HTTPS로 노출되어 있음.

**Base URL**
```
https://your-server.example.com
```

---

## v1.1 변경 요약 (2026-09-09)

기존 필드는 전부 그대로다. 아래는 **추가**된 것과 새로 지켜야 할 것.

| 변경 | 클라 작업 | 급한 정도 |
|------|----------|----------|
| `POST /players/register` 응답에 **`token`** 추가 | 저장 후 제출 시 헤더로 전송 | 인증 강제 전환 전까지 |
| `POST /delivery/submit`에 **`Authorization: Bearer <token>`** | 헤더 1줄 | 위와 동일 |
| `GET /ranking`에 **`me` 파라미터**, 응답에 **`is_me`·`me`** | 내 줄 강조를 점수+닉네임 대조 대신 `is_me`로 | 지금 바로 (기존 방식은 동점자에서 오작동) |
| rank가 **경쟁 순위**로 통일 (`1, 1, 3, 4`) | 순위 번호로 인덱싱하는 코드가 있으면 수정 | 지금 바로 |
| 빈 닉네임/12자 초과 → **400** | 등록 실패 처리 확인 | 지금 바로 |
| **이벤트 API** 신설 (`/events/...`) | 배너·이벤트 랭킹 화면 | 필요할 때 |

> 인증은 서버에서 2단계로 전환된다. 1단계에서는 토큰 검증 실패가 서버 로그에만 남아
> 구버전 클라이언트가 계속 동작하고, 2단계에서 401/403으로 거절한다.
> **전환 시점은 서버 담당과 협의할 것** — 클라 배포 일정에 맞춰 정한다.

---

## 연동 흐름 요약

| 시점 | 기존 코드 | 교체할 API |
|------|-----------|------------|
| 앱 첫 실행 | - | `POST /players/register` |
| 납품(배송) 버튼 클릭 | `UpdateRanking()` | `POST /delivery/submit` |
| 랭킹 탭 열기 | `LoadRanking()` | `GET /ranking` |
| 내 순위 표시 | - | `GET /ranking/me` |

**제거 가능한 기존 코드**: `SeedDemoRanking()`, `SaveRanking()`, `LoadRanking()`, PlayerPrefs 랭킹 키 전체

---

## 1. 플레이어 등록 — `POST /players/register`

앱 최초 실행 시 1회 호출. 기기 고유 ID로 서버에 플레이어를 등록하고 `player_id`를 발급받아 로컬에 저장한다. 이미 등록된 기기면 닉네임만 갱신된다.

**Request**
```json
{
  "device_id": "SystemInfo.deviceUniqueIdentifier 값",
  "display_name": "닉네임"
}
```

**Response `200`**
```json
{
  "player_id": "42f96186-26b0-4a71-aa06-2f60ce4233ee",
  "display_name": "닉네임",
  "token": "kf3Jx9-...."
}
```

**주의**
- `token`은 점수 제출 인증에 쓴다. `player_id`와 함께 `PlayerPrefs`에 저장할 것.
  같은 기기가 재등록해도 토큰은 바뀌지 않지만, **매번 응답값으로 덮어쓰는 편이 안전하다.**
- 닉네임이 비었거나(공백만 있는 경우 포함) 12자를 넘으면 `400 Bad Request`.
  `422`가 아니라 `400`이다.

**Unity 예시**
```csharp
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;

[System.Serializable]
public class PlayerRegisterRequest { public string device_id; public string display_name; }

[System.Serializable]
public class PlayerResponse { public string player_id; public string display_name; public string token; }

public IEnumerator RegisterPlayer(string displayName, System.Action<PlayerResponse> onDone)
{
    var body = new PlayerRegisterRequest {
        device_id = SystemInfo.deviceUniqueIdentifier,
        display_name = displayName
    };
    string json = JsonUtility.ToJson(body);

    using var req = new UnityWebRequest("https://your-server.example.com/players/register", "POST");
    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
    req.downloadHandler = new DownloadHandlerBuffer();
    req.SetRequestHeader("Content-Type", "application/json");

    yield return req.SendWebRequest();

    if (req.result == UnityWebRequest.Result.Success)
    {
        var res = JsonUtility.FromJson<PlayerResponse>(req.downloadHandler.text);
        PlayerPrefs.SetString("player_id", res.player_id);   // 로컬에 저장, 이후 계속 사용
        onDone?.Invoke(res);
    }
    else
    {
        Debug.LogError($"register failed: {req.error}");
    }
}
```

`player_id`는 이후 모든 API 호출에 필요하므로 `PlayerPrefs` 등에 영구 저장해둔다.

---

## 2. 납품 점수 제출 — `POST /delivery/submit`

납품(배송) 버튼 클릭 시 호출. 서버가 **최고 점수만 보존**하므로, 클라이언트에서 낮은 점수를 보내도 서버 기록은 갱신되지 않는다 (매번 그냥 현재 누적 점수를 보내면 됨).

**Request**
```
Authorization: Bearer {저장된 token}
```
```json
{
  "player_id": "저장된 player_id",
  "score": 300
}
```

**Response `200`**
```json
{
  "accepted_score": 300,
  "rank": 1
}
```

**주의**
- `score`는 **100의 배수**여야 함 (`PointsPerDelivery` 값과 서버 `POINTS_PER_ITEM` 동기화 필요, 현재 100).
  100의 배수가 아니거나 음수면 `400 Bad Request`.
- 등록되지 않은 `player_id`면 `404 Not Found` — 이 경우 재등록(`/players/register`) 후 재시도.
  이 동작은 인증이 강제된 뒤에도 유지된다 (없는 id는 401/403이 아니라 계속 404).
- `Authorization` 헤더를 붙일 것:
  ```csharp
  req.SetRequestHeader("Authorization", "Bearer " + PlayerPrefs.GetString("token"));
  ```
  인증 강제 전환 후에는 토큰 없음 → `401`, 토큰 불일치 → `403`.
  `403`을 받으면 재등록해 새 토큰을 받은 뒤 재시도한다.

```csharp
[System.Serializable]
public class DeliverySubmitRequest { public string player_id; public int score; }

[System.Serializable]
public class DeliverySubmitResponse { public int accepted_score; public int rank; }

public IEnumerator SubmitDelivery(int score, System.Action<DeliverySubmitResponse> onDone)
{
    var body = new DeliverySubmitRequest {
        player_id = PlayerPrefs.GetString("player_id"),
        score = score
    };
    string json = JsonUtility.ToJson(body);

    using var req = new UnityWebRequest("https://your-server.example.com/delivery/submit", "POST");
    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
    req.downloadHandler = new DownloadHandlerBuffer();
    req.SetRequestHeader("Content-Type", "application/json");

    yield return req.SendWebRequest();

    if (req.result == UnityWebRequest.Result.Success)
    {
        var res = JsonUtility.FromJson<DeliverySubmitResponse>(req.downloadHandler.text);
        onDone?.Invoke(res);
    }
    else if (req.responseCode == 404)
    {
        Debug.LogWarning("player_id 만료/미등록 — 재등록 필요");
    }
    else
    {
        Debug.LogError($"submit failed ({req.responseCode}): {req.error}");
    }
}
```

---

## 3. 글로벌 랭킹 조회 — `GET /ranking`

랭킹 탭 진입 시 호출. `limit`(기본 10, 최대 100), `offset`(기본 0) 쿼리 파라미터로 페이지네이션.

```
GET /ranking?limit=10&offset=0&me={저장된 player_id}
```

`me`는 **선택**이다. 주면 목록에서 내 줄에 `is_me: true`가 붙고, 내 순위가 `me` 블록으로
함께 온다 (내가 10위 밖이어도 온다 — `/ranking/me`를 따로 부를 필요가 없다).

**Response `200`**
```json
{
  "entries": [
    { "rank": 1, "display_name": "Tester", "score": 300, "is_me": false },
    { "rank": 1, "display_name": "나",     "score": 300, "is_me": true  }
  ],
  "total": 2,
  "me": { "rank": 1, "display_name": "나", "score": 300, "is_me": true }
}
```

**주의**
- **내 줄 강조는 반드시 `is_me`로 할 것.** 점수+닉네임 대조는 동점자나 동명이인에서 틀린
  줄을 강조한다. 응답에 다른 사람의 `player_id`는 오지 않는다 (그 목록 자체가 점수 조작
  대상이 되기 때문에 의도적으로 빼 두었다).
- `rank`는 **경쟁 순위**다. 동점자는 같은 순위를 받고 다음 순위는 건너뛴다 — `1, 1, 3, 4`.
  `rank`를 배열 인덱스나 정렬 키로 쓰고 있다면 고쳐야 한다. 표시 순서는 `entries` 순서를 그대로 쓸 것.
- `me`를 주지 않으면 `is_me`는 전부 `false`, `me`는 `null`이다 (기존 동작과 동일).

```csharp
[System.Serializable]
public class RankEntry { public int rank; public string display_name; public int score; public bool is_me; }

[System.Serializable]
public class RankingResponse { public RankEntry[] entries; public int total; public RankEntry me; }

public IEnumerator GetRanking(int limit, int offset, System.Action<RankingResponse> onDone)
{
    string playerId = PlayerPrefs.GetString("player_id");
    string url = $"https://your-server.example.com/ranking?limit={limit}&offset={offset}&me={playerId}";
    using var req = UnityWebRequest.Get(url);
    yield return req.SendWebRequest();

    if (req.result == UnityWebRequest.Result.Success)
    {
        var res = JsonUtility.FromJson<RankingResponse>(req.downloadHandler.text);
        onDone?.Invoke(res);
    }
    else
    {
        Debug.LogError($"ranking failed: {req.error}");
    }
}
```

> `JsonUtility`는 최상위가 배열인 JSON을 직접 파싱하지 못하지만, 위 응답처럼 객체(`{"entries":[...]}`)로 감싸져 있으므로 그대로 사용 가능.

---

## 4. 내 순위 조회 — `GET /ranking/me`

```
GET /ranking/me?player_id={저장된 player_id}
```

**Response `200`**
```json
{ "rank": 1, "display_name": "Tester", "score": 300, "is_me": true }
```

`GET /ranking?me=`의 `me` 블록과 같은 값이다. 랭킹 목록을 함께 띄우는 화면이라면
이 엔드포인트를 따로 호출할 필요가 없다.

기록이 없는 플레이어는 `{"rank": 0, "display_name": "?", "score": 0}` 반환 (에러 아님, 정상 응답).

```csharp
public IEnumerator GetMyRank(System.Action<RankEntry> onDone)
{
    string playerId = PlayerPrefs.GetString("player_id");
    string url = $"https://your-server.example.com/ranking/me?player_id={playerId}";
    using var req = UnityWebRequest.Get(url);
    yield return req.SendWebRequest();

    if (req.result == UnityWebRequest.Result.Success)
    {
        var res = JsonUtility.FromJson<RankEntry>(req.downloadHandler.text);
        onDone?.Invoke(res);
    }
    else
    {
        Debug.LogError($"my rank failed: {req.error}");
    }
}
```

---

## 5. 이벤트 — `GET /events/active`, `GET /events/{event_id}/ranking`

진행 중인 이벤트가 있으면 배너를 띄우고, 이벤트 기간 한정 랭킹을 별도 탭으로 보여준다.
앱 시작 시(또는 지도 씬 진입 시) `active`를 1회 호출하면 된다.

```
GET /events/active
```

**Response `200`** — 진행 중인 이벤트가 없으면 `{"events": []}` (배너를 감춘다)
```json
{
  "events": [
    {
      "event_id": "autumn2026",
      "title": "가을 대청소 주간",
      "description": "기간 내 납품 점수가 2배로 집계됩니다",
      "starts_at": "2026-09-15T00:00:00+09:00",
      "ends_at": "2026-09-22T23:59:59+09:00",
      "score_multiplier": 2.0,
      "banner_color": "#3aa76d",
      "server_time": "2026-09-09T15:00:00+09:00"
    }
  ]
}
```

```
GET /events/{event_id}/ranking?limit=10&offset=0&me={player_id}
```

응답 스키마는 `GET /ranking`과 **완전히 동일**하다 (`is_me`·`me` 포함). 같은 파싱 코드와
같은 UI 프리팹을 재사용하면 된다. 없는/종료된 `event_id`는 `404`.

**주의**

- **남은 시간 카운트다운은 `server_time` 기준으로 계산할 것.** 기기 시계는 사용자가 바꿀 수
  있고 실제로 어긋난 기기가 많다. 기간 판정 자체는 전적으로 서버가 한다.
- **`score_multiplier`를 클라에서 곱해 보내지 말 것.** 서버가 이벤트 집계에만 곱한다.
  클라가 곱한 값을 보내면 상시 랭킹 점수까지 부풀고, 최고점 보존 규칙 때문에 이벤트가
  끝난 뒤에도 점수가 한동안 멈춘 것처럼 보인다. 제출은 평소처럼 **누적 총점 그대로** 보내면 된다.
- 이벤트 점수 = **기간 내 증가분** × 배율. 상시 랭킹 점수(누적 총점)와 값이 다른 게 정상이다.
- `starts_at`/`ends_at`/`server_time`은 오프셋이 붙은 ISO8601이다.
  `JsonUtility`는 `string`으로 받고 `DateTime.Parse`로 변환할 것.

```csharp
[System.Serializable]
public class EventInfo {
    public string event_id; public string title; public string description;
    public string starts_at; public string ends_at;
    public float score_multiplier; public string banner_color; public string server_time;
}

[System.Serializable]
public class EventListResponse { public EventInfo[] events; }
```

---

## 에러 처리 가이드

| 상황 | HTTP 코드 | 클라이언트 대응 |
|------|-----------|------------------|
| 미등록 `player_id`로 요청 | 404 | `/players/register` 재호출 후 재시도 |
| `score`가 100의 배수 아님/음수 | 400 | 클라이언트 로직 점검 (버그) — 재전송해도 계속 실패 |
| 빈 닉네임 / 12자 초과 등록 | 400 | 입력 화면에서 막을 것 (서버도 막지만 UI에서 먼저) |
| 제출 시 토큰 없음 | 401 | 저장된 토큰 확인. 없으면 재등록 |
| 제출 시 토큰 불일치 | 403 | 재등록해 새 토큰을 받은 뒤 재시도 |
| 네트워크 끊김/타임아웃 | - | 재시도 또는 로컬 캐시 값으로 폴백 표시, 다음 접속 시 재동기화 |

서버가 요청 시점에 응답하지 않을 수 있으므로(네트워크 상태 등), 납품 제출은 실패 시 로컬에 "전송 대기" 상태로 남겨두고 다음 접속 때 재전송하는 방식을 권장.

---

## 체크리스트

- [ ] 앱 첫 실행 시 `player_id` 없으면 `/players/register` 호출 후 저장
- [ ] 기존 `PlayerPrefs` 랭킹 키, `SeedDemoRanking()`, `SaveRanking()`, `LoadRanking()` 제거
- [ ] 납품 버튼 → `/delivery/submit` 연결
- [ ] 랭킹 탭 → `/ranking` 연결 (페이지네이션 필요 시 `limit`/`offset` 사용)
- [ ] 내 순위 UI → `/ranking/me` 연결
- [ ] 404/400/401/403 에러 및 오프라인 상황 처리 추가
- [ ] 등록 응답의 `token`을 `PlayerPrefs`에 저장, 제출 시 `Authorization` 헤더로 전송
- [ ] 랭킹 요청에 `&me={player_id}` 추가, 내 줄 강조를 `is_me`로 교체
- [ ] `rank`가 `1, 1, 3`처럼 건너뛰어도 UI가 깨지지 않는지 확인
- [ ] (선택) 이벤트 배너 — `GET /events/active`, 카운트다운은 `server_time` 기준
