# Pixel Cleaners

**AR 플로깅 타이쿤 게임** — 현실 속 쓰레기를 포획하고, 자원으로 되살린다.

실외에서 GPS 핫스팟에 진입하면 지도에 오염 생명체 마커가 나타납니다. 마커를 탭해 AR 씬으로 이동하고,
자이로스코프 미니게임으로 포획합니다. 포획한 생명체는 공장 시설에 배치해 쓰레기를 자원으로 정제·합성하고,
완성한 납품 물품을 서버에 제출해 글로벌 랭킹을 겨룹니다.

## 프로젝트 정보

| 항목 | 내용 |
|------|------|
| 팀 구성 | 학생 3인 + 지도교수 1인 (총 4인) |
| 담당 역할 | 리드 개발 — 클라이언트 핵심 루프(GPS → AR 포획 → 공장 → 납품), 글로벌 랭킹 서버 |
| 관련 논문 | 「메타 SW 기반 AR 플로깅 게임 콘텐츠 개발에 관한 연구」 (공저 4인, 지도교수 포함) |
| 발표 | 2026 KIIT 하계 종합학술대회 특별세션 |

## 저장소 구조

```
client/   Unity 6 프로젝트 (Android, AR)
server/   FastAPI 랭킹 서버
docs/     인수인계 문서, 서버·클라이언트 연동 매뉴얼, 흐름도(drawio)
```

## 게임 루프

```
닉네임 입력 → 서버 등록
   ↓
GPS 핫스팟 진입 → 지도에 생명체 마커 스폰
   ↓
마커 탭 → AR 씬 → 포획 도구 선택(4종) → 포획 미니게임
   ↓
정화(보관) / 제거(각성제 획득)
   ↓
공장에 생명체 배치 → 자원 생산 → 합성·제작 → 납품 물품
   ↓
납품 센터에서 제출 → 글로벌 랭킹
```

## 주요 기능

- **GPS × 실시간 지도** — OpenStreetMap 타일(zoom 17, 3×3), Haversine 거리 기반 핫스팟 등급 판정
- **AR 포획 미니게임** — 자이로스코프 세이프존 조작, 도구 4종, 희귀도 5단계
- **자원 생산·합성** — 생명체 13종, 시설 5종 + 합성/제작소 4기, 지수형 쿨타임 `baseCycle × 0.90^statPower`
- **납품 & 글로벌 랭킹** — 납품 센터 씬, 최고 점수만 보존하는 REST API
- **영속성** — JSON 자동 저장, 재접속 시 최대 72시간 오프라인 생산 시뮬레이션

## 기술 스택

| 구분 | 내용 |
|------|------|
| 엔진 | Unity 6 (6000.3.11f1), C# |
| AR | AR Foundation 6.3.3 + ARCore XR Plugin 6.3.3 |
| 플랫폼 | Android 7.0+ (API 24), IL2CPP, ARM64 |
| 지도 | Unity Location Service + OpenStreetMap |
| 백엔드 | FastAPI, SQLAlchemy(async), SQLite |

## 외부 에셋 (저장소에 미포함)

UI는 itch.io의 [Pixel UI & HUD Pack](https://deadrevolver.itch.io/pixel-ui-hud-pack) (Dead Revolver)을 사용했습니다.
유료 에셋이라 재배포를 피하기 위해 이 저장소에는 포함하지 않았습니다.
프로젝트를 직접 열려면 팩을 구매해 `client/Assets/`에 임포트해야 합니다 (`Sprites`, `Animations`, `Tilemaps`, `Prefabs`,
`PixelUI` 스크립트, `DeadRevolver*` 폰트). 임포트하기 전에는 `PixelUI`를 참조하는 스크립트에서 컴파일 오류가 납니다.

## 실행 방법

1. Unity Hub에서 `client/` 폴더를 Unity 6000.3.11f1로 엽니다.
2. 메뉴 `PixelCleaners → 씬 생성`, `PixelCleaners → TMP 폰트 에셋 생성`을 실행합니다.
3. `PixelCleaners → 테스트 APK 빌드`로 Android APK를 빌드합니다.

에디터에서는 GPS Mock(서울시청 좌표)이 동작합니다. 자세한 절차는 [`docs/HANDOVER.md`](docs/HANDOVER.md)를 참고하세요.

## 문서

- [`docs/HANDOVER.md`](docs/HANDOVER.md) — 구현 현황, 시스템 구조, 빌드 절차
- [`docs/CLIENT_MANUAL.md`](docs/CLIENT_MANUAL.md) — Unity 클라이언트 API 연동
- [`docs/SERVER_MANUAL.md`](docs/SERVER_MANUAL.md) — FastAPI 서버 구현
- [`docs/GameFlow.drawio`](docs/GameFlow.drawio), [`docs/factory-flow.drawio`](docs/factory-flow.drawio) — 흐름도

## 현황

핵심 루프(포획 → 생산 → 납품 → 랭킹 → 저장)는 구현 완료입니다. 남은 작업: 로그인 씬 파일 등록,
QR 플로깅 인증 서버 API, 랭킹 서버 인증 강화.
