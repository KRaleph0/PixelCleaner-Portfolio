# Pixel Cleaners — 랭킹 API

클라이언트와 랭킹 서버(v1.1) 사이의 API 명세입니다. 클라이언트 구현은
[`client/Assets/Scripts/Network/ApiClient.cs`](../client/Assets/Scripts/Network/ApiClient.cs),
서버 구현은 [`server/`](../server/)에 있습니다.

| 시점 | API |
|------|-----|
| 첫 실행 | `POST /players/register` |
| 납품 | `POST /delivery/submit` |
| 랭킹 탭 | `GET /ranking` |
| 내 순위 | `GET /ranking/me` |
| 이벤트 배너 | `GET /events/active`, `GET /events/{event_id}/ranking` |

---

## POST /players/register

기기 고유 ID로 플레이어를 등록합니다. 같은 기기는 항상 같은 `player_id`를 받고, 닉네임만 갱신됩니다(upsert).

```json
// Request
{ "device_id": "<SystemInfo.deviceUniqueIdentifier>", "display_name": "닉네임" }

// Response 200
{ "player_id": "42f96186-...", "display_name": "닉네임", "token": "kf3Jx9-..." }
```

- `player_id`와 `token`을 저장해 이후 요청에 씁니다.
- 닉네임이 비었거나 12자를 넘으면 `400`.

## POST /delivery/submit

```
Authorization: Bearer <token>
```
```json
// Request
{ "player_id": "...", "score": 300 }

// Response 200
{ "accepted_score": 300, "rank": 1 }
```

- `score`는 **누적 총점**입니다. 서버는 최댓값만 유지하므로 매번 현재 총점을 보내면 됩니다.
- `score`는 100의 배수여야 합니다(클라이언트 `PointsPerDelivery`와 서버 `POINTS_PER_ITEM`이 같아야 함).
- 없는 `player_id`는 `404`입니다. 클라이언트는 이를 받아 재등록 후 재제출합니다.
  인증 오류가 이 404를 가리지 않도록 서버는 플레이어 조회를 토큰 검증보다 먼저 합니다.

## GET /ranking

```
GET /ranking?limit=10&offset=0&me=<player_id>
```
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

- `limit` 기본 10·최대 100, `offset` 기본 0.
- `me`(선택)를 주면 내 줄에 `is_me: true`가 붙고, 10위 밖이어도 `me` 블록으로 내 순위가 옵니다.
- 응답에 다른 플레이어의 `player_id`는 포함하지 않습니다.
- `rank`는 경쟁 순위입니다(동점자는 같은 순위, 다음 순위는 건너뜀: `1, 1, 3`). 표시 순서는 `entries` 순서를 따릅니다.

## GET /ranking/me

```
GET /ranking/me?player_id=<player_id>
```
```json
{ "rank": 1, "display_name": "Tester", "score": 300, "is_me": true }
```

기록이 없으면 `404`가 아니라 `200`과 `rank: 0`을 돌려줍니다.

## 이벤트

```json
// GET /events/active — 진행 중인 이벤트가 없으면 { "events": [] }
{
  "events": [{
    "event_id": "autumn2026",
    "title": "가을 대청소 주간",
    "starts_at": "2026-09-15T00:00:00+09:00",
    "ends_at": "2026-09-22T23:59:59+09:00",
    "score_multiplier": 2.0,
    "server_time": "2026-09-09T15:00:00+09:00"
  }]
}
```

- `GET /events/{event_id}/ranking`은 `GET /ranking`과 같은 형식입니다.
- 이벤트 점수 = 기간 내 증가분 × 배율. 배율은 서버가 이벤트 집계에만 곱하므로 클라이언트는 평소처럼 총점을 보냅니다.
- 남은 시간은 기기 시계가 아니라 `server_time` 기준으로 계산합니다.

---

## 오류 처리

| 상황 | 코드 | 클라이언트 대응 |
|------|------|----------------|
| 없는 `player_id` | 404 | 재등록 후 재시도 |
| 점수가 100의 배수가 아니거나 음수 | 400 | 클라이언트 버그 — 재전송해도 실패 |
| 빈 닉네임 / 12자 초과 | 400 | 입력 화면에서 막음 |
| 토큰 없음 / 불일치 (인증 강제 시) | 401 / 403 | 재등록해 새 토큰을 받은 뒤 재시도 |
| 네트워크 오류 | — | 제출을 대기 상태로 두고 다음 접속 때 재전송 |

## 클라이언트 연동 현황

현재 `ApiClient.cs`는 v1.0 형식(토큰 없음, `?me=` 없음)입니다.
토큰 저장·전송과 `is_me` 기반 내 줄 강조는 남은 작업이며, 서버는 그 전까지 토큰 검증 실패를 경고로만 처리합니다.
