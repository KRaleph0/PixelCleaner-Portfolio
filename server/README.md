# server — Pixel Cleaners 글로벌 랭킹 서버

Unity 클라이언트의 납품 점수를 집계하는 REST API. FastAPI + SQLAlchemy(async) + SQLite,
Cloudflare Tunnel로 HTTPS 노출.

운영 절차는 [`../docs/SERVER_MANUAL.md`](../docs/SERVER_MANUAL.md),
클라이언트 연동은 [`../docs/CLIENT_MANUAL.md`](../docs/CLIENT_MANUAL.md)를 참고하세요.

---

## API

| Method | Path | 설명 |
|--------|------|------|
| POST | `/players/register` | 기기 등록 / 닉네임 갱신. 제출 토큰 발급 |
| POST | `/delivery/submit` | 납품 점수 제출 (최고 점수만 보존) |
| GET | `/ranking?limit=&offset=&me=` | 글로벌 랭킹 |
| GET | `/ranking/me?player_id=` | 내 순위 |
| GET | `/events/active` | 진행 중인 이벤트 |
| GET | `/events/{event_id}/ranking` | 이벤트 기간 한정 랭킹 |
| GET | `/health` | DB 연결까지 확인하는 헬스체크 |

## 구조

```
main.py           앱 생성, 라우터 등록, lifespan(테이블 생성 + 마이그레이션), /health
config.py         환경변수 → 설정값
database.py       엔진/세션, SQLite PRAGMA, 마이그레이션
models.py         Player, DeliveryRecord, DeliverySubmission, Event
schemas.py        요청/응답 모델
auth.py           제출 토큰 검증 (경고 모드 / 강제 모드)
ranking_core.py   랭킹 계산 공통 로직
timeutil.py       utcnow()
routers/          players, delivery, ranking, events
scripts/          운영 스크립트 (테스트 계정 정리, 이벤트 관리)
tests/            검증 스크립트 (임시 DB 사용)
```

## 실행

```bash
cp .env.example .env          # 값 채우기
docker compose up -d --build
curl -s http://localhost:8000/health

# 로컬 개발
python -m venv .venv && .venv/bin/pip install -r requirements-dev.txt
.venv/bin/uvicorn main:app --reload
```

## 테스트

```bash
PYTHON=.venv/bin/python tests/run_all.sh
```

| 스크립트 | 검증 대상 |
|---------|----------|
| `smoke_test.py` | API 계약 보존, 랭킹·인증·이벤트 동작 (41건) |
| `migration_test.py` | 구 스키마 DB에 신 코드를 올렸을 때의 마이그레이션·데이터 보존·구버전 클라 호환 (19건) |
| `concurrency_test.py` | 동시 제출 락, 랭킹 계정 필터, 이벤트 집계 경계 (9건) |

전부 임시 DB를 만들어 돌기 때문에 운영 DB에 영향이 없습니다.

---

## 설계 노트

포트폴리오 목적상, 구현하면서 판단이 갈렸던 지점을 남겨 둡니다.

### 랭킹에서 "내 줄"을 찾는 문제

클라이언트는 랭킹 목록에서 자기 줄을 강조해야 하는데, 응답에 식별자가 없어 점수+닉네임으로
추측하고 있었습니다(동점자나 동명이인에서 틀린 줄을 강조함). 가장 단순한 해법은 `RankEntry`에
`player_id`를 넣는 것이지만, **그렇게 하지 않았습니다.**

`player_id`만 알면 그 계정의 점수를 제출할 수 있는 구조였기 때문에, 랭킹 API가 상위 100명의
`player_id`를 뿌리면 그 응답이 그대로 공격 대상 목록이 됩니다. 대신 조회자가 자기 id를
`?me=`로 주면 서버가 해당 줄에 `is_me: true`를 붙여 주는 방식으로 풀었습니다.

- 남의 `player_id`는 응답에 나가지 않습니다
- `me`는 선택 파라미터라 기존 클라이언트가 그대로 동작합니다
- 내 순위를 `me` 블록으로 함께 돌려주므로, 내가 10위 밖이어도 `/ranking/me` 추가 호출이 필요 없습니다 (호출 2회 → 1회)

### rank 계산이 세 곳에서 서로 달랐던 문제

`/ranking`은 정렬된 행의 순번(`offset + i + 1`)을, `/ranking/me`와 `/delivery/submit`은
경쟁 순위(`count(score > 내점수) + 1`)를 쓰고 있었습니다. 그래서 같은 플레이어가 목록에서는
2위, 내 순위 조회에서는 1위로 보였습니다.

세 곳 모두 경쟁 순위로 통일하고 계산을 `ranking_core.py` 한 곳에 모았습니다. 목록 조회는
SQL 윈도우 함수(`RANK() OVER`)를 써서 행 수만큼 쿼리가 늘지 않게 했습니다.

### 동점자 정렬이 불안정했던 문제

`ORDER BY score DESC`만 있어서 동점자 사이 순서가 정의되지 않았습니다. 점수가 100 단위라
동점이 필연적으로 몰리는데, 이 상태로 페이징하면 같은 플레이어가 두 페이지에 나오거나
빠질 수 있습니다. `score DESC, updated_at ASC, player_id ASC`로 고정했습니다 —
"먼저 그 점수에 도달한 사람이 위".

### 인증을 단계적으로 켜기

점수 제출에 토큰 검증을 넣으면 구버전 클라이언트의 제출이 전부 실패합니다. 모바일 앱은
배포 즉시 전원이 업데이트하지 않으므로, `AUTH_ENFORCE` 환경변수로 두 단계로 나눴습니다.

| 단계 | 동작 |
|------|------|
| `false` | 토큰을 발급·검증하되 실패해도 통과. 서버 로그에만 경고 |
| `true` | 토큰 없음 → `401`, 불일치 → `403` |

로그로 구버전이 얼마나 남았는지 확인한 뒤 전환합니다.

JWT 대신 DB 저장 랜덤 토큰을 썼습니다. 단일 인스턴스라 무상태의 이점이 없고, 문제가 된
계정의 토큰만 즉시 폐기할 수 있는 쪽이 운영에 낫습니다.

또 인증을 넣을 때 **없는 `player_id`에 대한 404를 가리지 않도록** 플레이어 조회를 토큰
검증보다 먼저 합니다. 클라이언트가 이 404를 받아 자동 재등록하는 흐름에 의존하고 있어서,
여기서 401/403이 먼저 나가면 오프라인으로 시작한 플레이어가 복구되지 못합니다.

### 이벤트 배율을 어디에 곱할 것인가

클라이언트는 **누적 총점**을 보내고 서버는 **최댓값을 유지**합니다. 이 구조에서 저장값에
이벤트 배율을 곱하면, 이벤트가 끝난 뒤에도 부풀린 점수가 남고 플레이어는 그만큼 정상
플레이를 채울 때까지 점수가 멈춘 것처럼 보입니다.

```
상시 랭킹 점수   = 제출된 누적 총점의 최댓값        ← 배율 미적용
이벤트 랭킹 점수 = SUM(기간 내 증가분) × 배율       ← 배율은 여기만
```

"기간 내 획득 점수"는 기존 데이터로 계산할 수 없었습니다 — 플레이어당 최고점 한 개만 있고
제출 이력이 없었기 때문입니다. append-only `delivery_submissions` 테이블을 추가해 증가분을
합산하는 방식으로 풀었고, 이 테이블은 점수 급증 탐지와 사후 추적에도 그대로 쓰입니다.

기간 판정은 전적으로 서버가 합니다. 응답에 `server_time`을 함께 보내 클라이언트가
카운트다운을 기기 시계가 아니라 서버 시각 기준으로 계산하게 했습니다.

### SQLite에 워커를 여러 개 붙이면 안 되는 이유

배포 설정이 `uvicorn --workers 4` + 단일 SQLite 파일이었습니다. SQLite는 쓰기를 파일 단위로
직렬화하므로, 독립 프로세스 4개가 동시에 쓰면 제출이 몰릴 때 `database is locked`로 500이
납니다. 워커를 1개로 고정하고 WAL 모드 + `busy_timeout`을 켰습니다. FastAPI는 비동기라
이 규모에서는 워커 1개로 충분하고, 트래픽이 커지면 워커를 늘리기 전에 PostgreSQL로 옮기는
편이 맞습니다. (동시 제출 200건 + 동시 조회 30건으로 확인 — `tests/concurrency_test.py`)

### 관리자 엔드포인트를 만들지 않은 이유

리허설마다 쌓이는 더미 계정을 지우려고 `DELETE /admin/players/{id}`,
`POST /admin/ranking/reset`을 요청받았지만 만들지 않았습니다. 인증이 뚫리면 랭킹 전체가
한 번에 날아가는 경로를 인터넷에 노출하게 됩니다. 대신:

1. `scripts/purge_test_data.py` — 서버에서 실행. 기본 dry-run, `--apply`로 삭제
2. `RANKING_EXCLUDE_PREFIXES` — 특정 `device_id` 접두사를 랭킹에서 자동 제외

2번을 쓰면 리허설 점수가 애초에 랭킹에 올라오지 않아 지울 일이 없습니다.

삭제 스크립트는 자식 테이블부터 지웁니다. SQLite는 기본적으로 외래 키를 강제하지 않아
부모만 지우면 고아 행이 조용히 남고, 그러면 랭킹의 `total`과 `entries` 개수가 어긋납니다.

### 마이그레이션

`Base.metadata.create_all()`은 없는 테이블만 만들고 기존 테이블에 컬럼을 추가하지 않습니다.
Alembic을 들이기엔 이른 규모라 `database.py`의 `run_migrations()`에서 필요한 `ALTER`만
멱등하게 처리하고, 기존 플레이어에게 토큰을 백필합니다.

운영과 동일한 구 스키마 DB를 만들어 신 코드를 올려 보는 테스트(`tests/migration_test.py`)로
컬럼 추가·데이터 보존·구버전 클라이언트 호환을 검증합니다.
