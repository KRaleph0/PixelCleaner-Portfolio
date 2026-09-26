# Pixel Cleaners — 서버 운영 메뉴얼

> 최종 갱신: 2026-09-09 (v1.1.0)
> 대상: `https://your-server.example.com` — FastAPI + SQLite + Cloudflare Tunnel
> 클라이언트 연동: [CLIENT_MANUAL.md](CLIENT_MANUAL.md)
> 설계 판단 기록: [../server/README.md](../server/README.md)

이 문서는 **코드를 복붙해 두지 않습니다.** 코드는 [`../server/`](../server/) 아래가 유일한
원본이고, 여기에는 운영에 필요한 것 — 구조, 환경변수, API 계약, 배포·점검 절차 — 만 적습니다.

---

## 1. 구조

```
server/
├── main.py              FastAPI 앱, 라우터 등록, lifespan(테이블 생성 + 마이그레이션), /health
├── config.py            환경변수 → 설정값. 하드코딩 상수는 전부 여기로 모음
├── database.py          엔진/세션, SQLite PRAGMA, 마이그레이션(run_migrations)
├── models.py            Player, DeliveryRecord, DeliverySubmission, Event
├── schemas.py           요청/응답 Pydantic 모델
├── auth.py              제출 토큰 검증 (경고 모드 / 강제 모드)
├── ranking_core.py      랭킹 계산 공통 로직 — 세 엔드포인트가 이걸 공유
├── timeutil.py          utcnow(). DB에는 naive UTC로 저장
├── routers/
│   ├── players.py       POST /players/register
│   ├── delivery.py      POST /delivery/submit
│   ├── ranking.py       GET  /ranking, /ranking/me
│   └── events.py        GET  /events/active, /events/{id}/ranking
├── scripts/
│   ├── purge_test_data.py    테스트/리허설 계정 정리
│   └── manage_events.py      이벤트 등록/조회/종료
└── tests/               검증 스크립트 (임시 DB만 사용, 운영 DB 무관)
```

**데이터 모델 요점**

| 테이블 | 성격 | 왜 필요한가 |
|--------|------|-----------|
| `players` | 기기당 1행 (`device_id` unique) | `auth_token` 보관 |
| `delivery_records` | 플레이어당 1행, **최고점만** | 상시 랭킹 |
| `delivery_submissions` | **append-only 이력** | 이벤트 기간 집계 + 치팅 사후 추적 |
| `events` | 이벤트 정의 | 배너 + 기간 한정 랭킹 |

`delivery_records`는 최고점을 덮어써서 이력이 남지 않는다. 기간 집계와 조사에 필요한 건
전부 `delivery_submissions`에서 나온다.

---

## 2. 환경변수

`.env.example`을 `.env`로 복사해 채운다. **`.env`는 저장소에 올리지 않는다.**

| 변수 | 기본값 | 설명 |
|------|-------|------|
| `DATABASE_URL` | `sqlite+aiosqlite:///./pixelcleaners.db` | PostgreSQL 전환 시 `postgresql+asyncpg://...` |
| `CLOUDFLARE_TUNNEL_TOKEN` | — | **비밀값.** 유출 시 도메인 트래픽을 가로챌 수 있다 |
| `AUTH_ENFORCE` | `false` | `false`=검증 실패도 통과(경고 로그), `true`=401/403 거절 |
| `POINTS_PER_ITEM` | `100` | 제출 점수는 이 값의 배수여야 함. **클라 `PointsPerDelivery`와 동일해야 한다** |
| `DISPLAY_NAME_MAX` | `12` | 닉네임 최대 길이 |
| `DEVICE_ID_MAX` | `128` | |
| `RANKING_EXCLUDE_PREFIXES` | (없음) | 랭킹에서 감출 `device_id` 접두사. 예: `demo-` |
| `SCORE_RATE_LIMIT_PER_HOUR` | `30000` | 시간당 증가분이 넘으면 경고 로그 (거절 안 함) |
| `DISPLAY_TIMEZONE` | `Asia/Seoul` | 이벤트 시각 표기 |

---

## 3. API

| Method | Path | 비고 |
|--------|------|------|
| POST | `/players/register` | 기기 등록/이름 갱신. **응답에 `token` 포함** |
| POST | `/delivery/submit` | `Authorization: Bearer <token>` |
| GET | `/ranking?limit=&offset=&me=` | `me`는 선택 |
| GET | `/ranking/me?player_id=` | |
| GET | `/events/active` | 없으면 `{"events": []}` |
| GET | `/events/{event_id}/ranking?limit=&offset=&me=` | |
| GET | `/health` | DB까지 확인 |

Swagger: `https://your-server.example.com/docs`

### 반드시 유지해야 하는 계약

클라이언트가 아래 동작에 의존한다. 리팩터링할 때 깨지 말 것.

1. `POST /players/register`는 `device_id` 기준 **upsert** — 같은 기기는 항상 같은 `player_id`
2. `POST /delivery/submit`은 **누적 총점**을 받아 **최댓값을 유지** (합산 아님)
3. 없는 `player_id`로 제출하면 **404** — 클라가 이 404를 받아 자동 재등록 후 재제출한다.
   **인증 오류(401/403)가 이 404를 가리면 안 된다.** 그래서 플레이어 조회를 토큰 검증보다 먼저 한다
4. `GET /ranking/me`에 없는 id를 주면 404가 아니라 **200 + `rank: 0`**
5. 응답에 다른 플레이어의 `player_id`를 넣지 않는다 — 그 자체가 점수 조작 대상 목록이 된다

### rank 정의

세 곳(`/ranking`, `/ranking/me`, `/delivery/submit`) 전부 **경쟁 순위**다.
동점자는 같은 순위를 받고 다음 순위는 건너뛴다 (`1, 1, 3, 4`).
목록 정렬은 `score DESC, updated_at ASC, player_id ASC` — 동점 구간에서도 순서가 고정된다.

### 이벤트 점수 규칙

```
상시 랭킹 점수   = 제출된 누적 총점의 최댓값        ← score_multiplier 미적용
이벤트 랭킹 점수 = SUM(기간 내 delta) × multiplier  ← 배율은 여기만
```

배율을 상시 점수에 곱하면, 최댓값 유지 규칙 때문에 이벤트가 끝난 뒤에도 부풀린 점수가
남아 한동안 점수가 멈춰 보인다. **곱하는 대상을 절대 바꾸지 말 것.**

`delivery_submissions.submitted_at`은 SQLite `CURRENT_TIMESTAMP` 기준이라 **초 단위 해상도**다.
며칠 단위 이벤트에는 문제없지만, 경계에서 초 단위로 다투는 설계는 하지 말 것.

---

## 4. 배포

```bash
cd server
cp .env.example .env      # 값 채우기 (비밀값은 저장소에 올리지 않는다)
docker compose up -d --build
docker compose logs -f api
```

`docker compose`는 컨테이너 시작 시 자동으로:
- 없는 테이블 생성 (`create_all`)
- `players.auth_token` 컬럼 추가 + 인덱스 생성 (`run_migrations`)
- 토큰 없는 기존 플레이어에게 토큰 백필

**워커는 1개로 고정한다.** SQLite는 파일 단위로 쓰기를 직렬화하므로 워커를 여러 개
띄우면 동시 제출이 몰릴 때 `database is locked`로 500이 난다. 트래픽이 커지면
워커를 늘리기 전에 PostgreSQL로 옮긴다.

### 인증 강제 전환 (2단계)

```bash
# 신버전 클라 배포를 확인한 뒤
AUTH_ENFORCE=true docker compose up -d api
curl -s https://your-server.example.com/health   # auth_enforce: true 확인
```

되돌리려면 `AUTH_ENFORCE=false`로 다시 올린다. 경고 모드에서는 토큰 불일치가
`WARNING pixelcleaners.auth` 로그로 남으므로, 전환 전에 로그를 보고 구버전 클라가
얼마나 남았는지 판단한다.

---

## 5. 운영 절차

### 백업 (삭제·마이그레이션 전 필수)

```bash
docker compose cp api:/app/data/pixelcleaners.db ./backup-$(date +%F-%H%M).db
```

### 테스트/리허설 계정 정리

```bash
docker compose exec api python scripts/purge_test_data.py              # 대상 확인 (dry-run)
docker compose exec api python scripts/purge_test_data.py --apply      # 실제 삭제
```

기본 접두사는 `claudecode-verify-`, `cc-edge-`, `demo-`. 자식 테이블부터 지우고
고아 행까지 정리한다. **`players`만 지우면 안 된다** — SQLite는 기본적으로 외래 키를
강제하지 않아 고아 행이 조용히 남고, 그러면 랭킹의 `total`과 `entries` 개수가 어긋난다.

더 나은 방법은 애초에 안 쌓이게 하는 것이다. 리허설 기기의 `device_id`를 `demo-`로
시작하게 하고 `RANKING_EXCLUDE_PREFIXES=demo-`를 켜면 랭킹에 올라오지 않는다.

### 이벤트

```bash
docker compose exec api python scripts/manage_events.py list
docker compose exec api python scripts/manage_events.py add autumn2026 "가을 대청소 주간" \
    --starts "2026-09-15 00:00" --ends "2026-09-22 23:59" \
    --multiplier 2.0 --description "기간 내 납품 점수가 2배로 집계됩니다"
docker compose exec api python scripts/manage_events.py deactivate autumn2026
```

시각은 `DISPLAY_TIMEZONE`(기본 KST) 기준으로 입력받아 UTC로 저장한다.
이벤트 관리 API를 공개 엔드포인트로 만들지 않는 이유는, 인증이 뚫렸을 때
랭킹 전체가 한 번에 날아가는 경로를 인터넷에 두지 않기 위해서다.

### 점검

```bash
curl -s https://your-server.example.com/health
docker compose ps                      # api 컨테이너의 healthcheck 상태
docker compose logs api | grep WARNING # 토큰 불일치 / 점수 급증 경고
```

### 치팅 조사

```sql
-- 특정 플레이어의 제출 이력
SELECT submitted_at, total_score, delta FROM delivery_submissions
 WHERE player_id = '<id>' ORDER BY submitted_at DESC LIMIT 50;

-- 최근 1시간 증가분 상위
SELECT player_id, SUM(delta) d FROM delivery_submissions
 WHERE submitted_at >= datetime('now','-1 hour') GROUP BY player_id ORDER BY d DESC LIMIT 10;
```

토큰 무효화는 해당 플레이어의 `auth_token`을 새 값으로 바꾸면 된다
(그 기기는 다음 재등록 때 새 토큰을 받는다).

---

## 6. 검증

```bash
python -m venv .venv && .venv/bin/pip install -r requirements-dev.txt
PYTHON=.venv/bin/python tests/run_all.sh
```

- `tests/smoke_test.py` — 기존 계약 보존, 랭킹/인증/이벤트 동작
- `tests/migration_test.py` — **구 스키마 DB에 신 코드를 올렸을 때** 마이그레이션·데이터 보존·구버전 클라 호환
- `tests/concurrency_test.py` — 동시 제출 락, 리허설 계정 숨김, 이벤트 delta 경계

전부 임시 DB를 만들어 돌기 때문에 운영 DB에 영향이 없다.

---

## 7. 알려진 한계

- **SQLite 단일 파일 + 워커 1개.** 동시 접속이 수십 명을 넘어가면 PostgreSQL로 전환
- **토큰은 클라에 저장된다.** 루팅 기기에서는 추출 가능하다. 이 인증이 막는 것은
  "남의 계정 조작"과 단순한 요청 위조이지, 결심한 치터의 자기 점수 부풀리기가 아니다.
  그건 `delivery_submissions` 로그와 속도 임계값으로만 대응할 수 있다
- **속도 초과는 거절하지 않고 로그만 남긴다.** 정상 플레이가 걸리지 않는 임계값을
  리허설에서 확인한 뒤 거절로 바꿀지 결정한다
- **CORS가 `*`다.** Unity 네이티브 클라는 CORS와 무관하지만, WebGL 빌드를 내면 제한해야 한다
- **마이그레이션은 `run_migrations()` 수동 관리다.** 컬럼 변경이 잦아지면 Alembic 도입
