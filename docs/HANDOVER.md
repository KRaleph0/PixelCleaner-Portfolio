# Pixel Cleaners — 인수인계 문서

> 최초 작성: 2026-05-21 / 최종 업데이트: **2026-09-09 (코드 전수 대조 + 블로커 수정)**
> 엔진: Unity 6 (6000.3.11f1) / Android API 24+
> 브랜치: `main`

---

## 0. 이번 개정에서 바로잡은 것 (이전 문서와 다른 점)

이전 문서는 2026-08-28 기준이며, 그 뒤 코드가 크게 바뀌었다. 아래는 **문서가 틀렸던 부분**이다.
여기서 발견된 버그는 이번에 함께 수정했다 — §10 참조.

| 이전 문서 서술 | 실제 코드 |
|---|---|
| 로그인 씬 "파일 생성만 남음" | 씬 파일도 빌더도 없어 **신규 설치 시 앱 시작 불가**였음 → ✅ 이번에 수정 (§10) |
| 포획 미니게임 "자이로스코프" | `GravitySensor` / `Accelerometer` + 터치 드래그 (새 Input System) |
| 미니게임 "Phase 1 도구 선택 → Phase 2" | 도구는 AR 씬 하단 `ToolSelectorUI`에서 미리 고름. 2단계 팝업은 미사용 레거시 오버로드 |
| 생명체 "컬러 구체로 대체 중" | Meshy AI 모델 13종 전용 + 빌보드. 구체는 로드 실패 시 폴백 |
| 지도 "OSM tile.openstreetmap.org, zoom 17, 3×3" | **zoom 18, 5×5**, 타일 프루닝·로드 세대 관리. 제공자는 이제 교체 가능 (§17) |
| TMP 폰트 에셋 "생성 필요 ❌" | **이미 생성돼 있음** (`Assets/Resources/Fonts/PF스타더스트 3.0 SDF.asset`) |
| "ARScene에 XR Origin 수동 추가" | `SceneBuilder`가 자동 추가 + 전용 메뉴 2개 추가됨 |
| 창고 현황 "하단 고정, 2열, 11종" | **상단 고정 스트립, 가로 1열, 8종** (공장재고+창고 합산) |
| 각성제 "지급·소모 ✅" | 소모 UI가 데드 코드라 실행 경로가 없었음 → ✅ 이번에 수정 (§10) |
| `ARSessionManager` / `SlotCardUpdater` / `FacilitySlotItem` ✅ | 어디서도 참조되지 않는 데드 코드 |
| 씬 5개 | **6개** (Delivery 포함), Build Settings 등록 완료 |
| XR Interaction Toolkit 3.3.0 | 3.3.1 |

---

## 1. 프로젝트 개요

AR 플로깅 타이쿤 게임.
GPS 핫스팟에 진입하면 지도에 생명체 마커가 스폰되고, 마커를 탭하면 AR 씬으로 이동해 포획 미니게임을 진행한다. 포획한 생명체를 공장 시설에 배치해 자원 생산 사이클을 돌리고, 제작한 납품 물품을 납품해 글로벌 랭킹에 점수를 올린다.

| 항목 | 내용 |
|------|------|
| 엔진 | Unity 6 (6000.3.11f1) |
| AR | AR Foundation 6.3.3 + ARCore XR Plugin 6.3.3 |
| 렌더 | Universal RP 17.3.0 |
| 입력 | **Input System 1.19.0 전용** (`activeInputHandler: 1`) |
| 언어 | C# 10.0 |
| 플랫폼 | Android 7.0+ (minSdk 24, ARCore 최소) |
| GPS | Unity Location Service (`Input.location`) |
| 지도 | 래스터 슬리피맵 타일 zoom 18 — CARTO Positron(키 필요) / OpenStreetMap(키 불필요) 선택 |
| QR | ZXing.Net (미설치 → 스텁 모드) |
| 서버 | `https://your-server.example.com` (랭킹/납품). 플로깅 인증 서버는 미구현 |
| 폰트 | PF스타더스트 3.0 (TTF + TMP SDF 에셋 모두 존재) |
| 패키지 ID | `com.pixelcleaner.game` |

> ⚠️ **입력 시스템 주의**: 프로젝트는 신형 Input System **전용** 모드다. 게임 스크립트는 모두 `UnityEngine.InputSystem`을 쓰지만, `Assets/Scripts/Demo/`(PixelUI 데모 잔재)와 `GameBootstrap.EnsureEventSystem()`은 아직 구형 API를 쓴다. §10-B 참조.

---

## 2. 구현 현황 요약

> `✅ 구현됨` `🔧 부분 구현` `❌ 미구현`

### 코어 루프

| 시스템 | 상태 | 비고 |
|--------|------|------|
| GPS 수집 + 에디터 Mock | ✅ | 서울시청 좌표, 1초 폴링, FINE→COARSE 폴백 권한 요청 |
| 실시간 지도 타일 | ✅ | zoom 18, 5×5, 이동 시 프루닝·재로드. 제공자 교체 가능, 키 없으면 OSM 자동 폴백 |
| 핫스팟 등급 판별 (Haversine) | ✅ | Normal 5% / Polluted 20% / Hotspot 45% 스폰 확률. 시드 핫스팟: 서울시청(에디터 테스트, 500m), **군산 은파호수공원**(35.95098, 126.69731, 반경 1,100m) |
| 지도 위 생명체 마커 | ✅ | **포켓몬 GO식 고정 스폰 지점**(`MapSpawnSystem`) — 위치·시간으로 결정, 나갔다 와도 유지, 잡은 스폰만 저장 |
| `PendingCapture` 씬간 전달 | ✅ | 정의/희귀도/등급/풀 인덱스 정적 홀더 |
| 포획 도구 사전 선택 | ✅ | AR 씬 하단 `ToolSelectorUI` 상시 노출, 보유량 표시 |
| 포획 미니게임 | ✅ | 스타듀밸리 낚시형. 중력센서/가속도계 + 터치 드래그 + 마우스 |
| AR 카메라 초기화 | ✅ | 권한 획득 전 `ARSession` 선(先)비활성화 → 획득 후 활성화 → state 폴링 |
| AR 생명체 스폰 | ✅ | FBX 캐릭터 + `BillboardFace` + `CapsuleCollider` |
| 포획 → 정화/제거 | ✅ | 정화=인벤토리 보관, 제거=일반 각성제 1개 |

### 공장·자원

| 시스템 | 상태 | 비고 |
|--------|------|------|
| 크리처 기반 시설 5종 × 5슬롯 | ✅ | 런타임 `CreateInstance<FacilitySO>()`로 정의 생성 |
| 레시피 기반 합성/제작 4기 | ✅ | 일반 제작소×2(슬롯 0-1, 재구성력) + 고급 제작소×2(슬롯 2-3, 합성력). 제작소도 24h 수면 사이클 |
| `HumanCycle` 상태 머신 | ✅ | Idle → Working(24h) → Sleeping. Paused는 구버전 세이브 호환용(재고 상한 폐지) |
| 자원 생산 (기초·1차) | ✅ | 지수 쿨타임, 공장 재고(**상한 없음**) → 수거 → 창고 |
| 자원 합성 (2차·제작) | ✅ | 창고 재료 소모, 창고/포획도구 산출 |
| 정제소 이중 출력 선택 | ✅ | `hasChoice=true` 시설 카드에서 A/B 전환 |
| 크리처 배치 팝업 | ✅ | `CreaturePickerPopup` — 티어·능력치·이 슬롯에서의 실제 사이클 표시, 스탯 불일치 경고, 적합→능력치 순 정렬 |
| 레시피 팝업 | ✅ | `RecipePickerPopup` — 카테고리 필터 + 재료 충족 실시간 표시 |
| 자원 수거 | ✅ | 시설 행 우측 수거 버튼 → 창고 이전 (수거하지 않아도 생산은 멈추지 않음) |
| 상단 창고 현황 스트립 | ✅ | **10종 × 5열 2줄**. 숫자 = 창고 보유량, 초록 `+N` = 공장에서 수거 대기. 2차 자원(보라) 포함. 납품 물품은 납품 씬에 표시 |
| 각성제 지급 | ✅ | 생명체 제거 시 일반 각성제 +1 |
| 각성제 소모(재활성) | ✅ | 공장 씬에서 수면 슬롯(시설) 또는 수면 제작소의 생명체 칸 탭 → 각성제 자동 소모 (유효기간 짧은 등급부터) |

### 씬·영속성·서버

| 시스템 | 상태 | 비고 |
|--------|------|------|
| 씬 7개 + 전환 | ✅ | Login / Map / Factory / Creature / Bag / AR / Delivery |
| **LoginScene** | ✅ | 닉네임 입력 → 서버 등록 → Map. 서버 실패 시 "서버 없이 시작" 탈출구 |
| 하단 내비게이션 바 | ✅ | 씬에 사전 배치(`NavButtonSetter`) + 없으면 런타임 생성 폴백 |
| 데이터 영속성 | ✅ | `SaveManager` — JSON, 일시정지/종료 시 저장, 최대 72h 오프라인 생산 시뮬 |
| 납품 → 포인트 | ✅ | `FactoryManager.Deliver()` — 일반 100pt / 고급 250pt, 납품 씬에 두 종류를 함께 표시 |
| 글로벌 랭킹 서버 | 🔧 | 서버 v1.1 구현 완료(토큰 인증·`?me=`·이벤트, §18). 클라이언트 토큰·`?me=` 연동 대기 |
| 생명체 도감 씬 | ✅ | `CreatureScene` — **도감 탭**(13종, 획득 시 해금·실루엣) + **보유 탭** |
| 가방 씬 | ✅ | `BagScene` — 포획 도구 & 각성제 |
| 씬 자동 생성 (에디터) | ✅ | `PixelCleaners → 씬 생성 (전체 6개)` |
| TMP 폰트 에셋 | ✅ | 생성 완료 |
| Android APK 빌드 | ✅ | 에디터 메뉴 원클릭, IL2CPP, ARM64 |
| 13종 `CreatureSO` 에셋 | ❌ | 코드 정의만. 런타임 7종 인메모리 생성으로 대체 |
| QR 스캔 실제 동작 | ❌ | ZXing 미설치 + 스크립트가 씬에 배치조차 안 됨 |

---

## 3. 씬 구성

```
Assets/Scenes/
├── LoginScene.unity      ← Build index 0, 앱 진입점 (닉네임 입력 → 서버 등록)
├── MapScene.unity        ← GPS 지도 (타일 + 몬스터 마커)
├── FactoryScene.unity    ← 공장 관리 (생산·합성·수거)
├── CreatureScene.unity   ← 생명체 도감
├── BagScene.unity        ← 가방 (포획 도구·각성제)
├── ARScene.unity         ← AR 포획 (미니게임)
├── DeliveryScene.unity   ← 납품 센터 (납품 탭 + 글로벌 랭킹 탭)
└── SampleScene.unity     ← Unity 템플릿 잔재, 미등록·미사용
```

Build Settings 등록 순서(= `SceneBuilder.RegisterBuildSettings()`):
`Login → Map → Factory → Creature → Bag → AR → Delivery`

> ⚠️ `LoginScene.unity`는 아직 디스크에 없다. **`PixelCleaners → 씬 생성 (전체 7개)`을 한 번 실행**해야 생성·등록된다.
> 실행 전에도 앱은 뜬다 — `GameBootstrap`이 씬 등록 여부를 확인하고 없으면 로그인을 건너뛴다.

| 씬 | 배치 컴포넌트 | 비고 |
|----|-------------|---------|
| **MapScene** | `MapSceneSetup` (+ 런타임 `MapTileLoader`) | 정사영 카메라 **50° 기울기**(`CameraPitch`), 몬스터는 카메라를 향함(`BillboardFace`), 안개 오버레이 |
| **FactoryScene** | `FactorySceneSetup` | 1프레임 지연 후 UI 빌드 (`BuildUINextFrame`) |
| **CreatureScene** | `CreatureSceneSetup` | 포획 생명체 스크롤 목록 |
| **BagScene** | `BagSceneSetup` | `OnInventoryChanged` 구독 |
| **ARScene** | `ARSceneSetup` (`[DefaultExecutionOrder(-1000)]`) | + `ARSession`/`ARInputManager` + XR Origin 프리팹 |
| **DeliveryScene** | `DeliverySceneSetup` | 납품/랭킹 탭 |
| **LoginScene** | `LoginSceneSetup` | 내비 바 없음. 등록돼 있으면 UI를 만들지 않고 즉시 Map |

각 씬에는 `SceneBuilder`가 `EventSystem`(+`InputSystemUIInputModule`)과 `NavCanvas`(5버튼: 지도/공장/납품/생명체/가방)를 함께 심는다. 씬 설정 스크립트는 `GameObject.Find("NavCanvas") == null`일 때만 내비를 직접 만든다.

> 씬이 Build Settings에 없으면 `SceneController`가 인-게임 오버레이(생명체/가방/납품 내용)로 폴백한다. 단 이 폴백은 `SceneController.Load()` 경유일 때만 동작하며, `GameBootstrap`의 로그인 리다이렉트는 폴백을 타지 않는다(§10-A).

---

## 4. 스크립트 구현 현황

### 영속 시스템 (`Assets/Scripts/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `GameBootstrap.cs` | ✅ | `RuntimeInitializeOnLoad` 자동 초기화 + 로그인 리다이렉트(씬 미등록 시 안전하게 건너뜀) |
| `SceneController.cs` | ✅ | `GoLogin/GoAR/GoMap/GoFactory/GoCreature/GoBag/GoDelivery()` + 오버레이 폴백 |
| `SaveManager.cs` | ✅ | JSON 저장/복원 + 오프라인 시뮬 (최대 72h) |
| `NavButtonSetter.cs` | ✅ | 현재 씬 버튼은 비활성 + 밑줄 강조 |
| `FontProvider.cs` | ✅ | `Resources.Load` 1회 캐시 후 `Apply()` |
| `MapTileLoader.cs` | ✅ | 타일 다운로드·배치·프루닝, `loadGeneration`으로 중복/취소 처리, 제공자 교체(§17) |
| `CharacterAssets.cs` | ✅ | `Resources/Characters/{ID}` 로드 (.prefab 우선, 없으면 .fbx) |
| `PrototypeDebugUI.cs` | ❌ | 내용 없음 (주석 2줄). 삭제 가능 |

### AR (`Scripts/AR/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `CreatureSpawner.cs` | ✅ | `PendingCapture` 우선, 없으면 핫스팟 등급 기반 가중치 스폰 |
| `CaptureInteraction.cs` | ✅ | 새 Input System 터치/마우스 레이캐스트 → `ToolSelectorUI.SelectedTool`로 미니게임 진입 |
| `BillboardFace.cs` | ✅ | 매 프레임 카메라 방향 Y축 회전 |
| `ARSessionManager.cs` | ❌ | **어디서도 사용 안 함** (AR 초기화는 `ARSceneSetup`이 직접 수행) |

### 포획 (`Scripts/Capture/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `CaptureMiniGame.cs` | ✅ | `Launch(c, tool, cb)`가 실사용 경로. `Launch(c, cb)`(도구 선택 포함)는 미사용 레거시 |
| `CaptureConfig.cs` | ✅ | `CaptureToolTier`, 희귀도별 난이도, 도구 세이프존 보너스, 한글명 |
| `PendingCapture.cs` | ✅ | 씬간 포획 대상 전달 (`Set`/`Clear`) |

### GPS·지도 (`Scripts/GPS/`, `Scripts/Map/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `LocationManager.cs` | ✅ | 1초 폴링, 타임스탬프 중복 제거, GPS 꺼짐 시 최대 30초 대기, 정적 `DistanceMeters()` |
| `HotspotDetector.cs` | ✅ | 반경 내 최근접 핫스팟 판정, `ForceGrade()` |
| `MapMonsterMarker.cs` | ✅ | 맥동 + 탭 → `PendingCapture.Set()` → AR 이동 |

### 공장 (`Scripts/Factory/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `FactoryManager.cs` | ✅ | 공장 재고/창고/크리처/도구/각성제/납품 점수 전부 관리 + 저장 복원용 Direct 세터 |
| `FacilitySlot.cs` | ✅ | 지수 쿨타임(`0.90^statPower`, 최소 5%), 만재 Pause / 수거 시 Resume |
| `HumanCycle.cs` | ✅ | 4상태, `while` 루프 생산 틱(프레임 드랍 보정), `RestoreState()` |
| `SynthesisManager.cs` | ✅ | `DontDestroyOnLoad` 싱글톤, 슬롯 4개 `Update()` 구동 |
| `SynthesisSlot.cs` | ✅ | 레시피+크리처 기반 제작 (`0.90^statPower`, 최소 10%) |

### 데이터 (`Scripts/Data/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `CreatureData.cs` | ✅ | `CreatureInstance`, `CreatureRarity`, `StatType`(6종), `GetStatPower()` 1~21 |
| `ItemData.cs` | ✅ | `AwakenItem` — 활동시간 24h 고정, `ShelfLifeHours` |
| `RecipeData.cs` | ✅ | `Recipe`, `RecipeBook` (합성 2종 + 제작 5종) |
| `GameSaveData.cs` | ✅ | `JsonUtility` 호환 DTO (Dictionary → List) |

### 네트워크 (`Scripts/Network/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `ApiClient.cs` | ✅ | `POST /players/register`, `POST /delivery/submit`, `GET /ranking`, `GET /ranking/me` |
| `PlayerSession.cs` | ✅ | `player_id`/`display_name` 저장, `SaveOffline()` 오프라인 시작, 닉네임 유지 재등록 |

### QR (`Scripts/QR/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `QRScanner.cs` | ❌ | ZXing 스텁. `ZXING_PRESENT` 심볼 필요. 어떤 씬에도 배치 안 됨 |
| `PloggingAuthManager.cs` | ❌ | `http://localhost:8000` 하드코딩. 서버·씬 배치 모두 없음 |

### UI (`Scripts/UI/`)

| 파일 | 상태 | 설명 |
|------|------|------|
| `ToolSelectorUI.cs` | ✅ | AR 하단 포획틀 선택 바, 정적 `SelectedTool` |
| `CaptureUI.cs` | ✅ | 포획 성공 팝업 (정화/제거) |
| `WorkshopCardUpdater.cs` | ✅ | 슬롯 상태 + 재고(N개) + 수거 버튼 색상 |
| `SynthesisCardUpdater.cs` | ✅ | 레시피명·진행바·상태 문구·크리처 슬롯 |
| `CreaturePickerPopup.cs` | ✅ | `Show(FacilitySlot)` / `Show(SynthesisSlot)` |
| `RecipePickerPopup.cs` | ✅ | `Show(slot, category)` — 카테고리 필터 + 재료 충족 표시 |
| `FacilitySlotItem.cs` | ❌ | 데드 코드. 각성제 사용은 `WorkshopCardUpdater`로 옮겨졌으므로 삭제 가능 |
| `SlotCardUpdater.cs` | ❌ | 데드 코드 |
| `FactoryUI.cs` | ❌ | 데드 코드 (Inspector 연결용) |
| `InventoryUI.cs` | ❌ | 데드 코드 (Inspector 연결용) |

### 씬 설정 스크립트

| 파일 | 줄 수 | 설명 |
|------|------|------|
| `ARSceneSetup.cs` | 521 | 카메라 권한 → ARSession 활성화 → 크리처 풀(FBX 7종) → CaptureUI → ToolSelector → 디버그 |
| `MapSceneSetup.cs` | 558 | 탑뷰 카메라, 안개 오버레이, 핫스팟 마커·반경 링, 몬스터 스폰 |
| `FactorySceneSetup.cs` | 983 | 상단 현황 스트립 + 시설 행 5개 + 제작 카드 4개 + 출력 선택 팝업 + 디버그 |
| `DeliverySceneSetup.cs` | 526 | 납품/랭킹 탭 |
| `LoginSceneSetup.cs` | ~300 | 닉네임 입력 → 서버 등록 → Map. 실패 시 "서버 없이 시작" 노출 |
| `CreatureSceneSetup.cs` | 176 | 포획 생명체 목록 |
| `BagSceneSetup.cs` | 191 | 도구·각성제 인벤토리 |

### PixelUI 키트 (`Assets/Scripts/` 루트 다수)

`Button.cs`, `Panel.cs`, `Grid.cs`, `SkillTree.cs`, `ValueBar.cs`, `Demo/*` 등은 외부 UI 키트(`namespace PixelUI`)다. 게임 로직은 이 키트를 쓰지 않고 UGUI를 코드로 직접 조립한다. `Demo/`는 구형 `Input` API를 쓰므로 실행 씬에 넣지 말 것.

### 에디터 (`Assets/Editor/`)

| 메뉴 | 동작 |
|------|------|
| `PixelCleaners/씬 생성 (전체 7개)` | 씬 7개 생성 + Build Settings 등록 (Login이 index 0) |
| `PixelCleaners/TMP 폰트 에셋 생성 (PF스타더스트 3.0)` | SDF 에셋 생성 |
| `PixelCleaners/쉐이더 빌드 포함 설정` | `Unlit/Color`, `Unlit/Texture`, `Unlit/Transparent`, `Sprites/Default`를 Always Included에 추가 — **APK 빌드 전 필수** |
| `PixelCleaners/ARScene 완성 (ARSession + XR Origin 추가)` | ARScene에 누락 오브젝트 보강 |
| `PixelCleaners/XR Origin ARScene에 추가` | XR Origin만 추가 |
| `PixelCleaners/EventSystem 패치` | AR/Map/Factory 씬의 `StandaloneInputModule` → `InputSystemUIInputModule` |
| `PixelCleaners/생명체 머티리얼 설정` | 13종 텍스처 머티리얼 생성 + FBX 머티리얼 교체 (§7) |
| `PixelCleaners/생명체 아이콘 촬영` | 로스터 13종 정면샷 → `Resources/CreatureIcons/{ID}.png` (§7) |
| `PixelCleaners/모델 폴리곤 줄이기 (Decimate)` | 고폴리 모델을 원본 UV 유지한 채 목표 삼각형 수로 줄여 `Resources/Characters/{ID}.prefab` 생성 (§7) |
| `PixelCleaners/Android 빌드 설정 적용` | minSdk 24, IL2CPP, ARM64, Portrait, 인터넷 권한 |
| `PixelCleaners/테스트 APK 빌드` | `Builds/Android/PixelCleaner_test.apk` |

---

## 5. 포획 흐름

```
MapScene: MapMonsterMarker 탭
    ↓ PendingCapture.Set(def=null, rarity, grade, charIndex)
    ↓ SceneController.GoAR()

ARScene: ARSceneSetup.InitAR()
    ↓ 카메라 권한 → ARSession 활성화 → state 폴링 → Camera.main 대기
    ↓ SetupCreatureSpawner(): FBX 7종을 CreatureSO로 감싸 풀 등록
    ↓ CreatureSpawner.Start(): PendingCapture.PoolIndex로 해당 캐릭터 스폰

플레이어: 하단 ToolSelectorUI에서 포획틀 선택 (기본=기본 포획구)
    ↓ 생명체 탭 (CaptureInteraction)
    ↓ CaptureMiniGame.Launch(creature, selectedTool, cb)
        → FactoryManager.UseTool(tool) 로 즉시 소모
        → 세로 트랙: 세이프존 안에 생명체를 유지하면 게이지 상승
        → 조작: 터치 드래그 Y / 중력센서·가속도계 / 마우스 Y
        → 게이지 100% = 성공, 제한시간 초과 = 실패

성공 → CaptureUI 팝업
    정화 → FactoryManager.AddCreatureToInventory()
    제거 → AddAwakenItem(Normal, 1)
```

> `PendingCapture.Definition`은 지도에서 항상 `null`로 전달되고 `poolIndex`만 유효하다. 지도 캐릭터 인덱스와 AR 풀 인덱스가 같은 순서(`Ranger, Druid, Barbarian, Knight, Engineer, Mage, Rogue_Hooded`)라서 같은 캐릭터가 나온다. **두 배열의 순서를 바꿀 때는 반드시 함께 바꿔야 한다.**

### 지도 스폰 (`MapSpawnSystem`) — 포켓몬 GO 방식

세계를 위경도 격자(`CellDegrees` 0.0008° ≈ 서울 기준 89m × 71m)로 나누고, 칸마다 고정된 스폰 지점이 하나 있다.

- 각 지점은 **1시간 주기**(`CycleMinutes`) 중 칸마다 다른 분에 나타나 **30분 유지**(`ActiveMinutes`)
- 출현 여부·생명체·희귀도·등급·칸 안의 위치를 **칸 좌표 + 주기 번호의 해시**로 결정
  - 지도를 나갔다 와도, 앱을 껐다 켜도 같은 자리에 같은 생명체
  - 모든 플레이어에게 같은 스폰이 보인다 (서버 불필요)
  - 기기·플랫폼과 무관한 결과를 위해 `UnityEngine.Random`·`string.GetHashCode` 대신 SplitMix64 해시를 쓴다. `WorldSeed`를 바꾸면 전 세계 배치가 바뀐다
- 출현 확률은 **스폰 지점의 오염 등급**(`HotspotDetector.GradeAt`)에 따라 Normal 35% / Polluted 60% / Hotspot 90% — 오염 지역일수록 많이 나온다
- 제로픽셀(픽셀 전용)은 Hotspot 지점에서만, 나머지는 `SpawnWeight` 가중치
- 등급 분포는 AR 스포너와 같다 (A 5% / B 25% / C 70%)
- 플레이어 반경 220m(`VisibleRadiusMeters`) 안의 스폰만 그린다. 화면 모서리까지 약 207m
- **포획 성공** 또는 **도주** 시 그 스폰 키를 기록해 **그 스폰이 사라질 때까지 나에게만** 숨긴다

### 도주 확률 (포켓몬 GO식)

미니게임에 실패하면 생명체 티어에 따라 확률로 도망간다 (`CaptureConfig.FleeChance`).

| 티어 | 도주 확률 |
|------|---------|
| 1티어 | 20% |
| 2티어 | 35% |
| 특수 (제로픽셀) | 50% |

| 결과 | 문구 | 처리 |
|------|------|------|
| 성공 | 포획 성공! | 정화/제거 선택, 지도 스폰 기록 |
| 실패 · 놓침 | 놓쳤다! | 생명체가 남아 **그 자리에서 재도전** (포획틀 다시 소모, 0.4초 재탭 방지) |
| 실패 · 도주 | 도망쳤다! | **지도 스폰 삭제 + 지도 씬으로 강제 이동** |

도주 판정은 결과 문구를 띄우기 전에 미니게임 안(`CaptureMiniGame.EndGame`)에서 정해진다.
- 저장되는 것은 잡은 스폰 기록뿐 (`GameSaveData.caughtSpawns`, 만료되면 버림)
- 지도 씬은 위치가 바뀔 때와 5초마다 목록을 조회하고, 달라졌을 때만 다시 그린다

**시뮬레이션 (Python 포팅, 24시간 7분 간격)**

| 지역 | 반경 220m 안 | 화면 안 |
|------|------------|--------|
| 일반 | 평균 4.8 (1~10) | 평균 1.6 (0~4) |
| 핫스팟 | 평균 10.7 (3~20) | 평균 3.6 (0~9) |

밀도는 `CellSpawnChance`로 조절한다. 디버그 버튼 **[DEBUG] 다음 스폰 주기**는 시간을 1시간 넘겨 새 스폰을 보여준다 (Play를 끄면 초기화).

### 포획 도구

| 도구 | 한국어 | 세이프존 보너스 | 보유 |
|------|--------|---------------|------|
| Basic | 기본 포획구 | 없음 | 무제한(`-1`) |
| Enhanced | 강화 포획구 | +10% | 제작 |
| Precision | 정밀 포획구 | +25% | 제작 |
| Pixel | 픽셀 포획구 | +40% | 제작 |

### 희귀도별 난이도

| 희귀도 | 세이프존 | 속도 | 제한시간 | 불규칙 |
|--------|---------|------|---------|-------|
| Common | 40% | 0.20 | 15s | ❌ |
| Rare | 30% | 0.32 | 12s | ❌ |
| Epic | 20% | 0.48 | 10s | ❌ |
| Legendary | 12% | 0.65 | 8s | ❌ |
| Pixel | 8% | 0.70 | 10s | ✅ |

---

## 6. 자원 시스템

```
① 크리처 생산 (기초·1차)
   FacilitySlot ← 크리처 배치
       ↓ HumanCycle.OnProductionTick (productionCycle 마다)
   FactoryManager.AddResource()  →  공장 재고 (상한 없음)
       ↓ 시설 행 우측 [수거]
   CollectResource()  →  창고 (상한 없음)

② 레시피 제작 (2차·포획구·납품물품)
   SynthesisSlot ← 레시피 + 크리처 (일반 제작소 = 재구성력, 고급 제작소 = 합성력)
       ↓ SynthesisManager.Update() → slot.Tick(dt)
   창고 재료 소모  →  창고 산출물 / 포획 도구 재고

③ 납품
   창고 DeliveryItem  →  Deliver()  →  deliveryScore (+100pt/개)
       ↓ ApiClient.Submit(총점)
   서버 랭킹
```

- **공장 재고 상한 없음** (2026-09-14 폐지). 예전의 50개 도달 → `Paused` 규칙은 삭제했고, 구버전 세이브의 `Paused` 슬롯은 불러올 때 `Working`으로 이어서 일한다
- 24h 작업 완료 → `Sleeping` → 슬롯 탭 시 각성제로 복귀
- 제작소(`SynthesisSlot`)도 수면 사이클이 있다 — 아래 '제작소 수면' 참고
- 합성 슬롯은 크리처가 없으면 정지 (`RequiresCreature = true`)
- **요구 스탯 불일치**: 배치는 되지만 능력치 보너스 없이 **기본 사이클** 그대로 (`FacilitySlot.MatchesStat`, `SynthesisSlot.CycleFraction` — 일반 제작소는 재구성력, 고급 제작소는 합성력 기준)
- **픽셀 재구성소 = 제로픽셀 전용** (`FacilitySO.pixelExclusiveOnly`, `FacilitySlot.IsAllowed`). 픽셀 파편은 희귀 재화라 다른 생명체는 배치 자체가 안 된다 (배치 팝업에 흐리게 "배치 불가"). 이 규칙 이전 세이브에서 다른 생명체가 배치돼 있었다면 불러올 때 인벤토리로 돌려보낸다
- 불러오기 시 시설 슬롯 사이클은 저장값이 아니라 **현재 규칙으로 재계산**한다 (규칙·밸런스 변경이 기존 세이브에도 반영)

### 자원 종류 (12종)

| 분류 | `ResourceType` | 한국어 | 산출처 |
|------|-------------|--------|---------|
| 기초 | `Garbage` | 쓰레기 | 오염 집합소 |
| 1차 | `Plastic` | 플라스틱 | 용해 정제소 (A) |
| 1차 | `Glass` | 유리 | 용해 정제소 (B) |
| 1차 | `Metal` | 금속 | 단조 정제소 (A) |
| 1차 | `Can` | 캔 | 단조 정제소 (B) |
| 1차 | `Paper` | 종이 | 압축 정제소 (A) |
| 1차 | `Textile` | 섬유 | 압축 정제소 (B) |
| 1차 | `PixelFragment` | 픽셀 파편 (희귀) | 픽셀 재구성소 — **제로픽셀만** |
| 2차 | `RecycledComposite` | 재생 복합재 | 일반 제작소 |
| 2차 | `RecycledAlloy` | 재생 합금 | 일반 제작소 |
| 출력 | `DeliveryItem` | 납품 물품 (100pt) | 고급 제작소 "납품 패키지" |
| 출력 | `AdvancedDeliveryItem` | 고급 납품 물품 (250pt) | 고급 제작소 "고급 납품 패키지" |

> 공장 재고 딕셔너리는 앞의 8종만 갖는다. 2차 자원·납품 물품은 창고 전용이라 `AddResource()`로 넣을 수 없다.
> 공장 상단 **창고 현황**은 창고 보유량을 크게, 수거 대기 중인 공장 재고를 초록 `+N`으로 따로 보여준다. (예전에는 둘을 합산해 수거해도 숫자가 변하지 않았고, 2차 자원·납품 물품은 표시되지 않았다)

### 시설별 기본 사이클

| 시설명 | `FacilityType` | 요구 스탯 | 슬롯 | 사이클 | 출력 |
|--------|-------------|---------|-----|-------|------|
| 오염 집합소 | `PollutionCollector` | 오염 감지 | 5 | 10s | 쓰레기 |
| 용해 정제소 | `DissolutionRefinery` | 용해 | 5 | 15s | 플라스틱 / 유리 |
| 단조 정제소 | `ForgingRefinery` | 단조 | 5 | 15s | 금속 / 캔 |
| 압축 정제소 | `CompressionRefinery` | 압축 | 5 | 15s | 종이 / 섬유 |
| 픽셀 재구성소 | `PixelReconstructor` | **특수** (제로픽셀 전용) | 5 | 20s | 픽셀 파편 |
| 일반 제작소 ×2 | (SynthesisSlot 0-1) | **재구성** | **Lv1~5 (1~5마리)** | 레시피별 | 2차 자원 |
| 고급 제작소 ×2 | (SynthesisSlot 2-3) | **합성** | **Lv1~5 (1~5마리)** | 레시피별 | 포획구·납품 물품·고급 납품 물품 |

> 출력 전환은 슬롯 단위가 아니라 **시설 단위**다 (`FactoryManager.SetRefineryOutput()`이 같은 타입 슬롯 5개를 전부 바꾼다).

### 제작소 확장 (`SynthesisUpgrade`)

일반 제작소·고급 제작소는 **레벨만큼 생명체를 배치**한다 (Lv1 = 1마리 ~ Lv5 = 5마리). 공장 카드의 생명체 칸 5개 중 레벨만큼 사용하고, **바로 다음 칸이 `+ 확장` 버튼**이다 (재료가 모이면 초록색). 누르면 비용 팝업(`SynthesisUpgradePopup`)이 뜬다. 제작소 4기는 각각 따로 확장한다.

| 확장 | 재료 (창고에서 차감) | 구분 |
|------|------------------|------|
| Lv1 → Lv2 | 금속 ×10, 유리 ×10 | 1차 |
| Lv2 → Lv3 | 플라스틱 ×15, 종이 ×15, 캔 ×10 | 1차 |
| Lv3 → Lv4 | 재생 복합재 ×3, 재생 합금 ×3 | 2차 |
| Lv4 → Lv5 | 재생 복합재 ×6, 재생 합금 ×6 | 2차 |

**여러 마리의 속도** — 각자 일하는 것처럼 처리량을 더한다 (`SynthesisSlot.EffectiveCycleSeconds`).

```
합산 사이클 = 1 / Σ( 1 / (레시피 사이클 × 배율_i) )
  예) 60초 레시피에 각 48.6초짜리 2마리 → 24.3초
```

- 배율은 1마리일 때와 같다 (요구 능력치 일치 시 `0.90^능력치`, 최소 10% / 불일치 시 1.0)
- 배치 팝업의 예상 시간은 "지금 배치된 생명체 + 새로 넣을 생명체" 기준
- 생명체를 추가·해제해도 제작 진행도는 유지된다
- 세이브: `SynthesisSlotSave.level`, `creatures` 목록. 레벨이 없는 구버전 세이브는 Lv1 + 생명체 1마리로 읽는다
- 비용은 `SynthesisSlot.cs`의 `SynthesisUpgrade` 표에서 조절

### 제작소 수면 (`SynthesisSlot.IsSleeping`)

- 시설 슬롯과 같은 24시간(`SynthesisSlot.WorkDuration`) 활동 사이클. 단위는 **제작소 전체**다 (생명체별 아님)
- **실제로 제작한 시간만** 센다 — 레시피 없음·생명체 없음·재료 부족으로 대기하는 시간은 활동 시간을 쓰지 않는다 (시설의 옛 Paused와 같은 취급)
- 24시간을 채우면 잠들고 제작이 멈춘다. 카드 상태 = `수면 중`, 생명체 칸 = 주황 `수면중 / 탭=각성제`
- 생명체 칸 탭 → 각성제 1개 소모 후 새 24시간 (`FactoryManager.TryReactivateSynthesisSlot`). 각성제가 없으면 기존처럼 그 생명체를 해제
- 비어 있던 제작소에 첫 생명체가 들어오거나, 전원을 해제하면 활동 시간이 초기화된다 (시설 슬롯의 `StartWork`/`StopWork`와 같은 규칙)
- 세이브: `SynthesisSlotSave.workElapsed`, `sleeping`. 오프라인 시뮬도 남은 활동 시간 안에서만 제작하고, 다 쓰면 수면으로 복원
- 시연용: 디버그 `[DEBUG] 전체 수면 · 각성제+3` — 일하는 시설·제작소를 즉시 재우고 일반 각성제 3개 지급

### 레시피

**일반 제작소 (`RecipeCategory.Synthesis`)**

| 레시피 | 재료 | 출력 | 사이클 |
|---------|------|------|-------|
| 재생 복합재 | 쓰레기×3 + 플라스틱×2 + 종이×2 | 재생 복합재×1 | 60s |
| 재생 합금 | 쓰레기×3 + 금속×2 + 캔×2 | 재생 합금×1 | 90s |

**고급 제작소 (`RecipeCategory.Craft`)**

| 레시피 | 재료 | 출력 | 사이클 |
|---------|------|------|-------|
| 강화 포획구 | 금속×2 + 재생 합금×1 | 강화 포획구×1 | 45s |
| 정밀 포획구 | 유리×2 + 재생 복합재×1 + 재생 합금×1 | 정밀 포획구×1 | 90s |
| 픽셀 포획구 | 픽셀 파편×3 + 재생 복합재×2 + 재생 합금×2 | 픽셀 포획구×1 | 180s |
| 납품 패키지 | 종이×3 + 섬유×2 | 납품 물품×1 | 30s |
| 고급 납품 패키지 | 플라스틱×3 + 재생 복합재×1 | **고급 납품 물품×1** (250pt) | 60s |

> 레시피 재료는 **창고**에서 소모된다. 생산 직후 공장 재고에 쌓인 자원은 수거하기 전엔 재료로 못 쓴다.

### 지수 쿨타임

```
FacilitySlot : productionCycle = baseCycleSeconds  × max(0.90^statPower, 0.05)
SynthesisSlot: craftCycle      = craftCycleSeconds × max(0.90^statPower, 0.10)
```

`statPower` = 희귀도 기본값 + 등급 보너스 + **티어 보너스(2티어 +2)**. 요구 스탯이 맞을 때만 적용되며, 그 시설의 **모든 생산품**에 똑같이 적용된다 (불일치 시 기본 사이클).

**특화 생산**(`CreatureSO.specialtyResource`)은 **컨셉 표시용**이다. 속도 보너스가 없다. 캔 몸통인 캔버그 → "캔 특화" → 캔을 만드는 단조 정제소에 넣으면 된다는 것을 직관적으로 보여주는 역할이다. 실제 효과는 능력치(단조력)가 내며, 단조 정제소에서 금속을 만들어도 캔과 같은 속도다. 배치 팝업·포획 팝업·도감 상세에 표시된다.

| 희귀도 | C(grade 0) | B(grade 1) | A(grade 2) |
|--------|------|------|------|
| Common | 1 | 2 | 4 |
| Rare | 4 | 5 | 7 |
| Epic | 8 | 9 | 11 |
| Legendary | 13 | 14 | 16 |
| Pixel | 18 | 19 | 21 |

능력치 1당 사이클이 10%씩 곱으로 줄어드는 **지수 감소**다. 따라서 +2 보너스는 능력치 구간과 무관하게 항상 생산량 약 +23%다 (체감 감소 없음).

| Common, 기본 15초 | 1티어 | 2티어 |
|---|---|---|
| C등급 | power 1 → 13.50s | power 3 → 10.94s |
| B등급 | power 2 → 12.15s | power 4 → 9.84s |
| A등급 | power 4 → 9.84s | power 6 → 7.97s |

효율(`GetEfficiency()`)은 등급별 0.3 / 1.0 / 2.0이며 **도감 표시 전용**이다. 생산 속도에는 `statPower`만 쓰인다. 티어 보너스는 배치 팝업(`(+2 티어)`), 포획 팝업, 도감 상세에 표시된다.

---

## 7. 캐릭터 / 폰트 에셋

### 캐릭터 (AR·지도 공용)

```
Assets/Resources/Characters/       Barbarian, Barbarian_Large, Druid, Engineer,
                                   Knight, Mage, Ranger, Rogue, Rogue_Hooded (.fbx)
Assets/Resources/Animations/       Rig_Medium/Rig_Medium_General.fbx
                                   Rig_Large/Rig_Large_General.fbx
```

`CharacterAssets.Load()`는 에디터에선 `AssetDatabase`, 빌드에선 `Resources.Load`로 FBX를 가져온다. 둘 다 동작한다.

생명체 모델에는 리그·애니메이션이 없다(Meshy 정적 메시). 움직임이 필요하면 코드 연출(떠다니기 등)로 준다.

### 생명체 로스터 (`Assets/Scripts/Data/CreatureRoster.cs`)

생명체 13종의 **단일 출처**다. AR 스폰 풀과 지도 몬스터가 모두 `CreatureRoster.Spawnable`을 같은 순서로 쓰므로, 예전처럼 두 파일의 배열 순서를 맞출 필요가 없다.

| No | 능력치 | 티어 | 특화 생산 | ID | 정화명 | 포획명 | 형태 컨셉 | 현재 모델 |
|----|--------|------|----------|----|--------|--------|----------|----------|
| 01 | 오염 감지력 | 1 | 쓰레기 | `BinCore` | 빈코어 | 클램프 | 쓰레기통 몸통 | **BinCore.fbx (전용, 9,781)** |
| 02 | 오염 감지력 | 2 | 쓰레기 | `WastePix` | 웨이스트픽스 | 패커 | 비닐봉투 몸통 | **WastePix.fbx (전용, 10,355)** |
| 03 | 용해력 | 1 | 플라스틱 | `PlasVox` | 플라스복스 | 크래쉬 | 페트병 몸통 | **PlasVox.fbx (전용, 10,325)** |
| 04 | 용해력 | 2 | 유리 | `GlassNode` | 글라스노드 | 머지 | 유리병 몸통 | **GlassNode.fbx (전용, 10,189)** |
| 05 | 단조력 | 1 | 금속 | `LeafByte` | 리프바이트 | 코로드 | 미정 (디지털 계열) | **LeafByte.fbx (전용, 10,322)** |
| 06 | 단조력 | 2 | 캔 | `CanBug` | 캔버그 | 슬러지 | 캔 몸통 | **CanBug.fbx (전용, 10,294)** |
| 07 | 압축력 | 2 | 종이 | `PaperBit` | 페이퍼빗 | 스태틱 | 구겨진 종이 뭉치 | **PaperBit.fbx (전용, 9,696)** |
| 08 | 압축력 | 1 | 섬유 | `ThreadVox` | 스레드복스 | 탱글 | 실타래 몸통 | **ThreadVox.fbx (전용, 9,975)** |
| 09 | 재구성력 (일반 제작소) | 1 | — | `GreenBit` | 그린빗 | 스캐터 | 픽셀 큐브 + 집게팔 | **GreenBit.fbx (전용, 10,219)** |
| 10 | 재구성력 (일반 제작소) | 2 | — | `EcoPix` | 에코픽스 | 노이즈 | 픽셀 큐브 + 하단 흡입구 | **EcoPix.fbx (전용, 9,909)** |
| 11 | 합성력 (고급 제작소) | 1 | — | `CleanLog` | 클린로그 | 프래그 | 플로피 디스크 | **CleanLog.fbx (전용, 9,297)** |
| 12 | 합성력 (고급 제작소) | 2 | — | `EcoByte` | 에코바이트 | 링크르 | 하드 드라이브 | **EcoByte.fbx (전용, Meshy 리메시 19,484)** |
| 13 | 특수 (픽셀 재구성소) | — | 픽셀 파편 | `ZeroPixel` | 제로픽셀 | 코어버그 | 미정 — 세계 오염의 근원 | **ZeroPixel.fbx (전용, 14,860)** |

현재 스폰 가능: **13종 전부, 모두 전용 모델**. 모델 파일이 없는 종은 스폰 목록에서 빠지고, 전 종이 없으면 구체로 폴백한다.

**파일 배치 (2026-09-13 정리 후)**
- 모델 FBX: `Assets/Resources/Characters/{ID}.fbx` **한 곳에만** 둔다 (게임이 읽는 파일)
- `Assets/Characters/{ID}/`: 기본 색상 PNG(`*_texture.png`)와 머티리얼(`{ID}_Mat.mat`) — 머티리얼 설정이 이 PNG를 쓴다
- 같은 폴더의 metallic·roughness·normal PNG 4장은 게임에서 쓰지 않는다 (13종 합계 약 172MB)
- 예전에 원본 FBX를 이 폴더에도 두었으나 Resources 복사본과 동일해 삭제했다 (143MB)

- 제로픽셀은 **특수 능력치**(`StatType.Special`, enum 맨 뒤 추가)이고 픽셀 핫스팟 전용이다. 픽셀 재구성소의 요구 능력치도 특수라, 일반 제작소(재구성력)에서는 보너스를 받지 않는다.
- **2티어는 능력치 +2** (`CreatureInstance.Tier2StatBonus`) — 생산·합성 사이클 ×0.81, 생산량 약 +23%. 특수(0)·1티어는 보너스 없음. 희귀도는 13종 모두 Common.
- 티어·능력치·특화는 세이브에 ID와 함께 저장되며, 불러올 때는 **로스터 값을 기준으로 복원**한다 (로스터를 바꾸면 기존 세이브에도 반영). 그래서 능력치가 바뀐 생명체가 이미 다른 시설에 배치돼 있으면 불러온 뒤 '능력치 불일치(기본 사이클)'가 된다 — 다시 배치하면 된다.
- **2026-09-14 능력치 재배치**: 컨셉에 맞춰 쓰레기통·비닐봉투 → 오염 감지(쓰레기), 페트병 → 용해(플라스틱), 유리병(글라스노드) → 용해(유리), 캔 → 단조(캔), 종이 → 압축(종이). 픽셀 파편은 제로픽셀만 생산하므로 그린빗·에코픽스는 특화 없이 재구성력(일반 제작소)으로 둔다. 합성력(고급 제작소)은 저장 매체 컨셉으로 클린로그(1티어, 플로피 디스크) → 에코바이트(2티어, 하드 드라이브). 재구성력(일반 제작소)은 그린빗·에코픽스.
- `CreatureRoster.All` 순서가 도감 번호와 지도 스폰 종 결정(해시 → 인덱스)에 쓰이므로, 순서를 바꾸면 같은 시각·위치의 스폰 종과 도감 번호가 달라진다.

### 새 모델 추가 절차

1. 권장 사양으로 제작: FBX, **삼각형 1.5만 이하 (곡면이 많아 형태가 무너지는 모델은 최대 3만)**, 텍스처 1024 Base Color, 발바닥 중앙 기준점
   - 근거: AR은 동시 1마리, 지도는 최대 3마리라 3만 모델도 씬당 최대 9만 삼각형. 메시보다 텍스처가 더 무겁다
   - 리메시 후 찌그러져 보이면 먼저 Meshy Quad 토폴로지, 또는 Unity 임포트 Normals=`Calculate`·Smoothing Angle 40~60을 시도
2. **`Assets/Resources/Characters/{ID}.fbx`** 로 넣는다 (예: `EcoPix.fbx`). 파일명이 로스터 ID와 같아야 한다.
3. `PixelCleaners → 생명체 아이콘 촬영` 실행 (머티리얼 설정 포함). 코드 수정 없이 다음 실행부터 스폰된다
4. 정면이 반대로 보이면 로스터의 `modelYawOffset = 180f`로 보정 — 다시 내보낼 필요 없다.

### 폴리곤 줄이기 (Decimate 도구)

`PixelCleaners → 모델 폴리곤 줄이기 (Decimate)` — 고폴리 원본(예: 30만)에서 **AR용과 지도용 두 버전**을 만든다.
Meshy Remesh는 UV를 새로 펼쳐 텍스처가 뭉개질 수 있지만, 이 도구는 **원본 UV·텍스처를 그대로 유지**한다.

| 버전 | 출력 | 권장 목표 | 쓰는 곳 |
|------|------|----------|--------|
| AR | `Resources/Characters/{ID}.prefab` | 1.5만 (곡면 모델 최대 3만) | AR 씬 |
| 지도 | `Resources/Characters/{ID}_Map.prefab` | 약 2천 | 지도 씬 |

1. 고폴리 원본 FBX는 `Assets/Characters/{id}/`에 둔다 — `Resources` 밖
2. 도구에 원본을 넣고 출력 ID(로스터 ID)와 버전별 목표 삼각형 지정 → 생성
3. 두 버전 모두 **원본에서 각각** 줄인다 (AR 버전을 다시 줄이면 오차 누적)

**로드 우선순위**
- AR: `{ID}.prefab` → `{ID}.fbx`
- 지도: `{ID}_Map.prefab` → (없으면 AR과 동일)

**이미 줄여 둔 모델**(예: CanBug 1만)은 그대로 쓴다. 지도에서도 1만짜리가 쓰이는데, 지도용만 따로 만들고 싶다면 도구에서 AR 버전을 끄고 지도 버전만 켠 채 `CanBug.fbx`를 넣으면 된다.

**옵션**
- UV 이음새 보존 (AR 기본 켬, **지도 기본 끔**) — 지도 모델은 화면에 수십 픽셀이라 이음새가 보이지 않고, 켜면 2천까지 못 줄이는 경우가 많다
- 곡률 보존 (기본 켬) · 열린 가장자리 보존 (기본 끔)
- 목표보다 25% 이상 남으면 결과 창에 경고가 뜬다

**⚠ 한계 — 크게 줄이면 깨진다 (2026-09-13 실측)**

EcoByte(원본 381,368)를 AR 3만 / 지도 2천으로 줄였더니 결과가 깨져 보였다. 원본은 결함 0이었는데 결과에 접힌 면이 생겼다.

| 메시 | 삼각형 | 접힌 면 |
|------|--------|--------|
| 원본 | 381,368 | 0 |
| Meshy 리메시 (PaperBit·CanBug) | 약 1만 | 2~6 (0.02~0.06%) |
| 도구 결과 AR | 43,956 (목표 미달) | 110 (0.25%) |
| 도구 결과 지도 | 4,052 (목표 미달) | 244 (6%) |

AI 모델은 UV 조각이 매우 많아(EcoByte: 정점 41,262개 중 실제 위치 21,881개) 원본의 수 % 이하로 줄이면 면이 접히기 쉽다.

- **원본 대비 5% 미만으로 줄일 때는 Meshy Remesh를 우선한다.** 리메시로 받은 파일을 `Resources/Characters/{ID}.fbx`, 지도용은 `{ID}_Map.fbx`로 넣으면 된다 (로더가 `.fbx`도 찾는다)
- 도구는 결과마다 **접힌 면 수를 검사**해 삼각형의 0.1%를 넘으면 경고한다. 5% 미만 목표는 실행 전에도 경고한다
- 출력 ID는 원본 경로에서 추정한다 (`Assets/Characters/{ID}/` 폴더 이름). 이전에는 창을 열 때 선택돼 있던 에셋 이름이 남아 결과가 엉뚱한 이름으로 저장되는 버그가 있었다

**주의**
- 결과 프리팹의 머티리얼·텍스처는 **원본 FBX 안의 내장 텍스처를 참조**한다. 원본 FBX를 저장소에서 빼면 팀원 쪽에서 텍스처가 사라진다
- Resources에 같은 이름 `.fbx`가 있으면 빌드 로드 대상이 모호해져 `Assets/Characters/_Backup/`으로 옮긴다 (확인 창)
- 도구 어셈블리는 `defineConstraints`로 **패키지가 있을 때만 컴파일**된다

### 생명체 머티리얼 (`CreatureMaterialSetup`)

`PixelCleaners → 생명체 머티리얼 설정` (아이콘 촬영이 자동으로 먼저 실행)

Meshy FBX는 텍스처를 파일 내부에만 넣고 머티리얼은 `texture_0.png`라는 이름으로 찾는다. 프로젝트에 그 파일이 없어 **Unity가 텍스처 없는 회색 머티리얼을 만든다.** 13종 모두 내부 이름이 같아 "이름으로 찾기"는 남의 텍스처를 가져갈 위험이 있다.

- 생명체마다 `Assets/Characters/{ID}/*_texture.png`(기본 색상)로 URP/Lit `{ID}_Mat.mat`을 만든다
- `Resources/Characters/{ID}.fbx`의 내장 머티리얼을 이 머티리얼로 **명시적으로 교체(Remap)**한다
- 기본 색상 PNG는 Max Size 1024로 설정한다
- 조건: 폴더 이름이 ID와 같고(대소문자 무시), `*_texture.png`가 정확히 1개
- **모델을 새로 넣거나 텍스처를 바꾸면 다시 실행**해야 한다

### 정면샷 아이콘 (`CreatureIconBaker`)

`PixelCleaners → 생명체 아이콘 촬영` — 로스터 13종을 게임과 같은 크기 맞춤으로 불러와 정면에서 촬영한다.

- 출력: `Assets/Resources/CreatureIcons/{ID}.png` (투명 배경, 256×256, Sprite)
- **모델을 바꾸면 메뉴를 다시 실행**해야 아이콘이 갱신된다
- 투명 배경은 검은/흰 배경 두 장의 차이로 복원한다 (미리보기 렌더러의 알파 지원 여부와 무관)
- 런타임: `CreatureRoster.LoadIcon(entry)` → `CreatureSO.icon`. 세이브 복원 시에도 로스터에서 다시 찾는다

**쓰는 곳**
- 공장 생명체 슬롯 전부(시설 슬롯 25칸, 합성·고급 제작소 카드) — `CreatureSlotView`: 정면샷 + 상태 글자, **이름은 표시하지 않음**. 아이콘이 없으면 이름 글자로 대체
- 생명체 씬 도감·보유 탭
- 생명체 선택(배치) 팝업 — 정면샷 + 등급 배지(희귀도 색), 요구 스탯 불일치 생명체는 흐리게

### 도감

- **해금 조건**: 생명체가 인벤토리에 들어온 순간(`FactoryManager.AddCreatureToInventory` → `Discover`). 포획 후 "정화"를 골라야 해금되며 "제거"는 해금되지 않는다
- 기록: `FactoryManager.Discovered` (로스터 ID → 최초 획득 시각), 세이브의 `discovered` 목록
- 도감 기능 이전 세이브는 불러올 때 보유·배치 중인 생명체를 자동으로 발견 처리
- 미발견: 정면샷을 검게 칠한 실루엣 + `???` / 발견: 정면샷·이름·능력치·티어, 카드를 누르면 상세(포획명·형태·보유 수·최초 획득일)
- 형태 컨셉이 "미정"으로 시작하면 상세에서 숨긴다
- 디버그 "생명체 추가"는 로스터 1티어 6종으로 만들어져 도감도 해금된다

### 크기 자동 맞춤 (`CreatureModelFitter`)

모델마다 원본 크기·기준점이 달라도 렌더러 경계를 측정해 자동으로 맞춘다.

| 씬 | 표시 높이 | 상수 |
|----|----------|------|
| AR | 0.7m | `CreatureModelFitter.ArHeight` |
| 지도 | 0.1 유닛 | `CreatureModelFitter.MapHeight` |

결과 오브젝트는 **루트(발바닥 중앙 원점, 탭용 CapsuleCollider) + 자식 `Model`** 구조다. 모델 원본의 콜라이더는 제거한다(남아 있으면 레이캐스트가 자식에 맞아 탭이 실패). **모델 루트의 원래 회전·위치는 보존**한다 — Blender를 거친 FBX는 루트의 -90° X 회전으로 서 있으므로, 덮어쓰면 모델이 눕는다 (2026-09-13 수정).

### 폰트

| 파일 | 경로 | 상태 |
|------|------|------|
| TTF 원본 | `Assets/Fonts/PF스타더스트 3.0.ttf` | ✅ |
| TMP SDF | `Assets/Resources/Fonts/PF스타더스트 3.0 SDF.asset` | ✅ |

---

## 8. 각성제

활동 시간은 등급 무관 **24시간 고정**. 등급 차이는 보관 기한(`ShelfLifeHours`)에만 반영되며, **보관 기한 만료 로직은 아직 구현돼 있지 않다.**

| 등급 | 활동 | 보관 기한 | 획득 |
|------|------|---------|------|
| 일반 | 24h | 24h | 생명체 제거 ✅ |
| 상급 | 24h | 48h | ❌ 경로 없음 |
| 플로깅 | 24h | 168h | ❌ QR 미구현 |

---

## 9. 영속성 (`SaveManager`)

- 저장 위치: `Application.persistentDataPath/save.json`
- 저장 시점: `OnApplicationPause(true)`, `OnApplicationQuit()`
- 로드 시점: `Start()` (`GameBootstrap.SetupFacilitySlots()`가 Awake에서 끝난 뒤)
- 저장 대상: 공장 재고 / 창고 / 포획 도구 / 각성제 / 납품 점수 / 크리처 인벤토리 / `FacilitySlot` 15개 상태 / `SynthesisSlot` 4개 상태
- 오프라인 시뮬: 저장 시각 대비 경과 시간을 최대 **72시간**까지 반영
  - 시설: `Working` 상태만 시뮬 (구버전 `Paused`는 `Working`으로 취급). 재고 상한 없음, 24h 소진 시 `Sleeping`
  - 합성: 레시피+크리처가 모두 있을 때만, 창고 재료가 떨어지면 중단
- 슬롯 매칭 키는 **GameObject 이름**(`Slot_{FacilityType}_{i}`)이다. 이름 규칙을 바꾸면 기존 세이브의 슬롯 상태가 통째로 유실된다.
- 크리처는 이름/스탯/희귀도/등급만 저장하고 로드 시 `CreatureSO`를 새로 만든다. 프리팹·아이콘 참조는 복원되지 않는다(도감·공장 UI는 텍스트라 문제없음).

---

## 10. 알려진 문제

### ✅ 2026-09-09에 수정한 것

| # | 증상 | 원인 | 수정 |
|---|------|------|------|
| 1 | **신규 설치 시 앱이 시작되지 않음** | `GameBootstrap`이 존재하지 않는 `LoginScene`을 `SceneManager.LoadScene()`으로 직접 로드 | `SceneController.IsSceneInBuild()`로 방어 + `SceneBuilder.BuildLoginScene()` 추가 + Build index 0 등록 |
| 2 | EventSystem 폴백이 실행 즉시 예외 | Input System 전용 모드인데 `StandaloneInputModule` 사용 | `InputSystemUIInputModule`로 교체 |
| 3 | **각성제를 쓸 방법이 없음** | 유일한 호출부 `FacilitySlotItem`이 데드 코드 | `FactoryManager.TryReactivateSlot()` 추가 → `WorkshopCardUpdater`의 수면 슬롯 탭에 연결 |
| 4 | 공장 씬을 나가면 예외가 쏟아짐 | `OnStateChanged -= _ => Refresh()` — 새 람다라 구독 해제가 안 됨 | 델리게이트 인스턴스를 보관해 정확히 해제 |
| 5 | 핫스팟 등급이 바뀌면 지도 몬스터가 사라짐 | `RebuildMarkers()`가 몬스터까지 같은 리스트에서 파괴 | `monsterMarkers` 리스트 분리 + 재스폰 시 이전 것 정리 |
| 6 | 랭킹에서 동점자가 있으면 엉뚱한 줄이 강조됨 | 점수+이름으로 "내 순위" 판별 | 목록 내 유일값인 `rank`로 판별 |
| 7 | 지도 타일에 `API KEY REQUIRED` 워터마크 | CARTO가 2026-08부터 래스터 타일에 키 요구 | 제공자 교체 가능 구조 + 키 미입력 시 OSM 자동 폴백 (§17) |
| 8 | `Camera.main`이 null이면 마커 탭에서 예외 | null 체크 없음 | `MapMonsterMarker.TryRaycast()` 방어 |
| 9 | minSdk 24/25 불일치 | 프로젝트 설정과 빌드 스크립트가 다름 | 프로젝트 설정을 24로 정렬 |
| 10 | 빌드 캐시가 git에 추적됨 | `.gitignore` 누락 | `.gradle/`, `.utmp/` 추가 + 추적 해제 |

**수정에 딸린 동작 변화**

- 앱 진입점이 `MapScene` → `LoginScene`으로 바뀐다. `player_id`가 이미 있으면 UI를 만들지 않고 곧바로 `MapScene`으로 넘어가므로 기존 사용자 체감은 동일하다.
- 서버가 죽어 있으면 로그인에서 막히므로, 등록 실패 시 **"서버 없이 시작"** 버튼이 나타난다. `local:<guid>` 임시 ID로 게임을 진행하고, 이후 납품 제출이 404를 받으면 기존 404 핸들러가 입력한 닉네임으로 정식 재등록한다.
- **수면 슬롯을 탭하면 이제 각성제를 소모한다.** 각성제가 없을 때만 기존처럼 생명체를 회수한다. 슬롯 라벨에 `수면중 / 탭=각성제`로 표시된다.

> ⚠️ 수정 사항 중 씬에 반영되는 것(`LoginScene` 생성, Build Settings 순서)은 **`PixelCleaners → 씬 생성 (전체 7개)`을 한 번 실행해야** 적용된다.

### 남아 있는 문제

| 심각도 | 문제 | 비고 |
|------|------|------|
| 🟡 | 각성제로 재활성해도 **생명체를 뺐다 다시 꽂으면 24h가 공짜로 리셋된다** | `UnassignCreatureFromSlot` → `StopWork()` → 재배치 시 `StartWork()`. 각성제의 가치를 유지하려면 누적 활동시간을 생명체에 저장해야 함 |
| 🟠 | **클라이언트가 서버 v1.1(`?me=`, 토큰)에 아직 연동되지 않았다** | 현재는 점수+닉네임 대조로 내 줄을 임시 판별 (§18) |
| 🟡 | 각성제 보관 기한(`ShelfLifeHours`) 만료 로직 없음 | 등급 차이가 실질적으로 무의미 |
| 🟡 | 몬스터 마커가 각자 `Update()`에서 전체 화면 레이캐스트 | 마커 수가 늘면 낭비. 입력 처리를 `MapSceneSetup`으로 모으는 편이 낫다 |
| 🟢 | `AndroidManifest.xml`의 `android:hardwareAccelerated="false"` | 의도적 회피책이면 이유를 남기고, 아니면 제거 검토 |
| 🟢 | 데드 코드 | `PrototypeDebugUI`(빈 파일), `ARSessionManager`, `FactoryUI`, `InventoryUI`, `SlotCardUpdater`, `FacilitySlotItem`, `Scripts/Demo/*`(구형 Input API), `SampleScene.unity` |

### 이전에 해결된 것 (기록 보존)

| 증상 | 원인 | 조치 |
|------|------|------|
| 앱 실행 즉시 크래시 | `UnityPlayerGameActivity` + `Theme.AppCompat` (Unity 버그) | `UnityPlayerActivity` + `.androidlib` 테마 오버라이드 |
| 지도 타일 분홍색 | Android 빌드에서 `Shader.Find()` null | Unlit → URP Unlit → Mobile/Diffuse 폴백 + 쉐이더 Always Included 메뉴 |
| GPS 초기화 고착 | 런타임 위치 권한 미요청 | `PermissionCallbacks` + COARSE 폴백 |
| AR 검은 화면 | 권한 없는 상태로 ARCore가 카메라 선점 | `ARSceneSetup.Awake()`에서 ARSession 선비활성화 + 배경 전용 보조 카메라 |
| `build-tools;35.0.0` 라이선스 | Unity SDK에 35 없음 | `compileSdk 34` + `buildToolsVersion "36.0.0"` |
| `CS0104` 'Button' 모호 | `PixelUI.Button` vs `UnityEngine.UI.Button` | 전체 경로 명시 |
| 앱 재시작 시 초기화 | 영속성 없음 | `SaveManager` |
| 납품 물품 소모처 없음 | 납품 미구현 | `DeliveryScene` + `FactoryManager.Deliver()` |

---

## 11. 잔여 작업

### 즉시 (데모 전 필수)

- [ ] **`PixelCleaners → 씬 생성 (전체 7개)` 실행** — LoginScene 생성 + Build Settings 재등록
- [ ] `PixelCleaners → 쉐이더 빌드 포함 설정` 실행 (APK에서 타일/마커 분홍색 방지)
- [ ] CARTO 무료 API 키 발급 → MapScene의 `MapSceneSetup` 인스펙터 `cartoApiKey`에 입력 (§17)
- [ ] 실기에서 **신규 설치 상태로** 1회 검증 (`adb uninstall com.pixelcleaner.game` 후 재설치)

### 단기 (데모 품질)

- [ ] 각성제 우회 경로 차단 — 누적 활동시간을 `CreatureInstance`에 저장
- [ ] 발표장 좌표로 에디터 Mock / 핫스팟 시드 교체 (`LocationManager.mockLatitude`, `GameBootstrap.SeedHotspot`)
- [ ] 데모 시작 시 합성 스탯 크리처 자동 지급

### 중기 — 서버

- [ ] 클라이언트에 토큰 저장·전송 및 `?me=` 연동 (`ApiClient`, `PlayerSession`) — `CLIENT_MANUAL.md`
- [ ] 클라이언트 이벤트 API 연동 (서버 v1.1에 구현됨)
- [ ] 신버전 클라이언트 배포 확인 후 서버 `AUTH_ENFORCE=true` 전환 — `SERVER_MANUAL.md`

### 중기 — 클라이언트
- [ ] 나머지 6종 크리처 + `CreatureSO` 에셋화
- [ ] 희귀도 다양화 — 현재 AR 스폰은 전부 Common
- [ ] ZXing.Net 설치 + `ZXING_PRESENT` 심볼 + 플로깅 인증 서버(`POST /api/plogging/verify`)
- [ ] 상급 각성제 획득 경로 / 보관 기한 만료 처리

### 장기

- [ ] 지도 타일 디스크 캐싱 (CARTO 무료 티어 절약에도 직결)
- [ ] 지도 줌 슬라이더
- [ ] 스킬 트리 UI 연결
- [ ] 데드 코드 정리

---

## 11.5 디버그 UI 토글

화면 **오른쪽 위의 작은 `+` 버튼**으로 디버그 UI 전체를 켜고 끈다 (`DebugUI`). 시연 영상에 디버그 버튼이 보이지 않도록 **기본은 숨김**이며 `PlayerPrefs["debug_ui_visible"]`에 저장된다.

- 켜지면 버튼이 빨간색 `×`로 바뀐다
- 토글 버튼은 **디버그 UI가 등록된 씬(지도·AR·공장)에서만** 보인다
- 대상: 지도의 상태 패널(등급·스폰 확률·GPS)과 디버그 버튼 3개, AR의 디버그 패널 전체, 공장의 디버그 버튼 2개
- 새 디버그 UI를 만들면 `DebugUI.Register(gameObject)`로 등록한다
- 토글 버튼 자리를 비우기 위해 지도·공장의 오른쪽 위 디버그 버튼을 한 칸 아래로 내렸다
- 노치·상태바에 가리지 않도록 `Screen.safeArea` 안쪽에 배치한다

---

## 12. 에디터 테스트 절차

0. 로그인 흐름을 보려면 먼저 PlayerPrefs를 비운다 (Edit → Clear All PlayerPrefs).
   **LoginScene** Play → 닉네임 입력 → 시작하기. 서버가 죽어 있으면 "서버 없이 시작"으로 진행.
1. 이후에는 **MapScene** 열고 Play (등록돼 있으므로 로그인은 건너뛴다)
2. `GameBootstrap`이 매니저 전부 생성
3. 에디터 Mock GPS = 서울시청 → 지도 타일 5×5 로드 + 몬스터 1~3마리 스폰
   - 우측 상단 디버그 버튼: `[DEBUG] 다음 스폰 주기` / `강화 포획틀 +3` / `정밀 포획틀 +1`
4. 몬스터 클릭 → `PendingCapture` 설정 → **ARScene** 전환
5. AR 씬 좌측 디버그 패널

| 버튼 | 동작 |
|------|------|
| 핫스팟 강제 진입 | `HotspotGrade.Hotspot` 강제 |
| 생명체 강제 스폰 | 카메라 앞 2m |
| 각성제 +1 | 일반 각성제 |
| 강화 포획틀 +3 / 정밀 포획틀 +3 / 픽셀 포획틀 +1 | 도구 지급 후 선택 바 갱신 |

6. 하단 포획틀 선택 → 생명체 클릭 → 마우스 Y축으로 미니게임
7. 하단 내비 → **FactoryScene**
8. 오른쪽 위 `+`로 디버그 표시를 켠 뒤 `[DEBUG] 생명체 추가` — 7종 순환(빈코어·플라스복스·캔버그·페이퍼빗·제로픽셀·그린빗·에코바이트) / `[DEBUG] 자원 +10` — 8종 공장 재고 +10 / `[DEBUG] 전체 수면 · 각성제+3`
9. 시설 행 슬롯 탭 → 생명체 배치 → 생산 확인 (상단 스트립 수치 증가)
   - 수면 중(주황) 슬롯 탭 → 각성제를 소모해 재활성. 각성제가 없을 때만 생명체 회수
10. 수거 버튼 → 창고 이전
11. 일반 제작소 카드(재구성력) / 고급 제작소 카드(합성력): 생명체 칸 탭 → 원형(레시피) 탭 → 자동 제작
12. 고급 제작소에서 `납품 패키지` 제작 → 내비 `납품` → 납품 → 랭킹 탭 확인

---

## 13. 영속 오브젝트 구조

```
[DontDestroyOnLoad]
├── [Bootstrap]         GameBootstrap
├── SceneController     Go*() 7종 + 오버레이 폴백
├── LocationManager     GPS, OnLocationUpdated (1초)
├── HotspotDetector     등급 판별, OnHotspotGradeChanged
├── FactoryManager      공장재고/창고/크리처/도구/각성제/납품점수
├── SynthesisManager    SynthesisSlot[4] (0-1 합성 / 2-3 고급)
├── SaveManager         JSON 저장·복원 + 오프라인 시뮬
├── EventSystem         (씬에 없을 때만 생성 — §10-B)
└── [FacilitySlots]
    ├── Slot_PollutionCollector_0..4    (+ HumanCycle)
    ├── Slot_DissolutionRefinery_0..4
    ├── Slot_ForgingRefinery_0..4
    ├── Slot_CompressionRefinery_0..4
    └── Slot_PixelReconstructor_0..4
    // 합성 슬롯은 순수 C# 객체 — GameObject 없음

[씬 전용]
ARScene      : ARSession, XR Origin, CreatureSpawner, CaptureUI, ToolSelectorUI,
               CaptureMiniGame(런타임 생성·자기파괴), ARSceneSetup, NavCanvas
MapScene     : MapCamera, Ground, Fog, MapTileLoader, 핫스팟/몬스터 마커, NavCanvas
FactoryScene : FactoryCanvas(현황 스트립 + 시설 행 + 제작 카드 + 팝업), NavCanvas
DeliveryScene: DeliveryCanvas(납품/랭킹 탭), NavCanvas
CreatureScene: CreatureCanvas, NavCanvas
BagScene     : BagCanvas, NavCanvas
```

---

## 14. Android 빌드

### 최초 설정

1. `PixelCleaners → Android 빌드 설정 적용`
2. **Project Settings → Player → Android → Other Settings → Application Entry Point = `Activity`**
   ⚠️ 빠뜨리면 실행 즉시 크래시 (Unity 공식 버그)
3. **Project Settings → XR Plug-in Management → Android → ARCore** 체크
4. `PixelCleaners → 쉐이더 빌드 포함 설정` ← **누락 시 타일·마커가 분홍색**
5. (씬이 없으면) `PixelCleaners → 씬 생성 (전체 6개)`

### 빌드·설치

> `테스트 APK 빌드`는 **Build Settings에 등록된(활성) 씬을 그 순서대로** 포함한다. 예전에는 씬 목록을 코드에 따로 적어 DeliveryScene이 빠졌고, APK에서 납품 센터가 기능 없는 대체 팝업으로 떴다 (2026-09-14 수정).

```
PixelCleaners → 테스트 APK 빌드
→ Builds/Android/PixelCleaner_test.apk
```

```bash
adb devices
adb install -r Builds/Android/PixelCleaner_test.apk
adb logcat -s Unity AndroidRuntime
```

### 설정 요약

| 항목 | 값 |
|------|-----|
| Platform | Android |
| Minimum API | 24 (Android 7.0, ARCore 최소) |
| Target API | Auto |
| Scripting Backend | IL2CPP |
| Target Architecture | ARM64 |
| Orientation | Portrait (upside-down 허용, `useOSAutorotation: 1`) |
| Application Entry Point | **Activity** |
| Bundle ID | `com.pixelcleaner.game` |

### Android 파일 구조

```
Assets/Plugins/Android/
├── AndroidManifest.xml               ← CAMERA / FINE·COARSE_LOCATION / INTERNET,
│                                        UnityPlayerActivity + LAUNCHER, com.google.ar.core=required
└── PixelCleanerTheme.androidlib/
    ├── build.gradle                  ← namespace, compileSdk 34, buildTools "36.0.0"
    ├── AndroidManifest.xml
    ├── res/values/themes.xml         ← UnityThemeSelector → Theme.AppCompat.NoActionBar
    └── .gradle/                      ← ⚠️ 빌드 캐시가 git에 추적 중 (§10-F)
```

**왜 `.androidlib`인가** — Unity 6은 `Assets/Plugins/Android/res/` 직접 지원을 제거했다.
**왜 `compileSdk 34` + `buildTools 36`인가** — Unity 내장 SDK에 build-tools 36은 있고 35는 없다.

---

## 15. 패키지 의존성

| 패키지 | 버전 | 상태 |
|--------|------|------|
| AR Foundation | 6.3.3 | ✅ |
| ARCore XR Plugin | 6.3.3 | ✅ |
| ARKit XR Plugin | 6.3.3 | ✅ (미사용) |
| XR Interaction Toolkit | **3.3.1** | ✅ |
| XR Management | 4.5.4 | ✅ |
| Universal RP | 17.3.0 | ✅ |
| **Input System** | **1.19.0** | ✅ 전용 모드 |
| Android Logcat | 1.4.7 | ✅ |
| TextMeshPro | Unity 내장 | ✅ |
| Unity Mesh Simplifier (Whinarn, MIT) | 3.1.1 (git 태그 고정) | ✅ Decimate 에디터 도구 전용 |
| ZXing.Net.Bindings.Unity | — | ❌ 미설치 |

---

## 16. 네임스페이스

```csharp
PixelCleaners            // 코어 데이터·매니저 (CreatureData, FactoryManager,
                         //  SynthesisManager, SceneController, FacilitySO, RecipeBook …)
PixelCleaners.AR         // CreatureSpawner, CaptureInteraction, BillboardFace, ARSessionManager
PixelCleaners.GPS        // LocationManager, HotspotDetector
PixelCleaners.Capture    // CaptureMiniGame, CaptureConfig, CaptureToolTier, PendingCapture
PixelCleaners.UI         // Canvas UI 컴포넌트
PixelUI / PixelUI.Demo   // 외부 UI 키트 (게임 로직 미사용)

// 전역 (네임스페이스 없음)
GameBootstrap / SaveManager / FontProvider / CharacterAssets / NavButtonSetter
MapTileLoader / MapMonsterMarker
ApiClient / PlayerSession
*SceneSetup (7개)
SceneBuilder / AndroidBuildSetup   // Editor only
GameSaveData 및 DTO들
```

---

## 17. 지도 타일

### 제공자 선택

CARTO는 **2026년 8월부터 래스터 basemap 타일에 API 키를 요구한다.** 키 없이 요청하면 타일은 오지만
`API KEY REQUIRED` 워터마크가 찍혀 지도가 깨져 보인다. 이에 대응해 제공자를 교체할 수 있게 만들었다.

`MapTileLoader.MapTileProvider`

| 값 | 키 | 스타일 | 비고 |
|----|----|--------|------|
| `CartoPositron` (기본값) | **필요** | 밝은 회색 | 게임 배경으로 가장 적합. 무료 5,000,000 타일/월 |
| `CartoDarkMatter` | **필요** | 어두움 | 야간 테마용 |
| `OpenStreetMap` | 불필요 | 표준 OSM | 색이 진해 마커 가독성이 떨어짐. 타일 사용 정책 준수 필요 |

**설정 위치** — `MapTileLoader`는 런타임에 `AddComponent`로 붙어서 자체 인스펙터 값이 적용되지 않는다.
**MapScene의 `MapSceneSetup` 오브젝트**에 있는 `tileProvider` / `cartoApiKey`를 편집하면
`SetupTileLoader()`가 `Configure()`로 넘겨준다.

**키가 비어 있으면 자동으로 `OpenStreetMap`으로 폴백**하고 경고를 한 번 남긴다.
따라서 키 없이도 앱은 정상 동작하며, 워터마크가 찍히는 일은 없다.

### API 키 발급

1. https://carto.com/basemaps/apikey/ 에서 폼 제출 (무료, 승인 대기 없음, 카드 불필요)
2. 받은 키를 MapScene → `MapSceneSetup` → `cartoApiKey`에 입력
3. 타일 URL에 `?key=<발급키>`가 자동으로 붙는다

> 클라이언트에 박히는 키라 APK를 뜯으면 노출된다. 학기 데모 수준에서는 감수할 만하지만,
> 실제 배포한다면 타일을 자체 서버로 프록시하고 키를 서버에 두는 편이 맞다.

> OSM으로 운용할 경우 [타일 사용 정책](https://operations.osmfoundation.org/policies/tiles/)상
> 앱을 식별할 수 있는 User-Agent가 필수다. `MapTileLoader.UserAgent`의 연락처를 실제 값으로 바꿀 것.

### 동작

```
GPS (lat, lon)
    ↓ LonToTile / LatToTile (Web Mercator)
(tileX, tileY) at zoom = 18
    ↓ tileRadius = 2 → 5×5 = 25타일
CARTO: https://{a|b|c|d}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}.png?key=…
OSM  : https://tile.openstreetmap.org/{z}/{x}/{y}.png
    ↓ UnityWebRequestTexture
Quad (90° 회전, WorldScale 0.005)
    ↓ material.mainTexture
화면
```

- zoom 18 ≈ 150m/타일, 5×5 커버리지 ≈ 750m
- `SetReference()` — 월드 원점이 되는 GPS 기준점 설정. 바뀌면 전체 재로드
- `RefreshAt()` — 중심 타일이 바뀔 때만 재구성. `loadGeneration` 증가로 진행 중 로드를 취소·재시작
- `PruneTiles()` — 새 중심에서 벗어난 타일 파괴
- 셰이더 폴백: `Unlit/Texture` → `Universal Render Pipeline/Unlit` → `Mobile/Diffuse`
- `MapTileLoader.WorldScale`과 `MapSceneSetup.worldScale`은 **둘 다 0.005f로 반드시 일치**해야 한다

---

## 18. 랭킹 서버

서버 소스는 [`../server/`](../server/), API·운영 상세는 [`SERVER_MANUAL.md`](SERVER_MANUAL.md),
클라이언트 연동은 [`CLIENT_MANUAL.md`](CLIENT_MANUAL.md)에 있다.

### 서버 v1.1 반영 사항

| 항목 | 내용 |
|------|------|
| 랭킹 rank | 세 엔드포인트 모두 경쟁 순위로 통일, 동점자 정렬 고정(페이징 중복·누락 제거) |
| 내 줄 식별 | `?me=` 파라미터 → `is_me` / `me` 응답. 다른 플레이어의 `player_id`는 노출하지 않음 |
| 점수 제출 인증 | 등록 시 토큰 발급, 제출 시 Bearer 검증. 경고/강제 2단계 전환 |
| 검증 | 닉네임 빈 문자열·길이 400, 점수 배수 검증 |
| 이벤트 | 이벤트 기간 한정 랭킹 API, 제출 이력 테이블 |

### 클라이언트 연동 현황

이 저장소의 `ApiClient.cs`는 v1.1의 **토큰 저장·전송**과 **`?me=` 연동을 아직 반영하지 않은 상태**다.
현재는 점수+닉네임 대조로 내 줄을 임시 판별한다(`DeliverySceneSetup.LoadRankFromServer`).
연동 절차는 `CLIENT_MANUAL.md`를 따른다.

### 남은 과제

- 클라이언트 토큰·`?me=` 연동, 이후 서버 `AUTH_ENFORCE` 강제 전환
- QR 플로깅 인증(`POST /api/plogging/verify`)은 미구현. `PloggingAuthManager`는 로컬 주소를 본다.

---

## 19. 참고

- `GameFlow.drawio` — 게임 플로우 다이어그램 (draw.io)
- 서버 소스는 `server/`에 있다. API 계약은 `docs/SERVER_MANUAL.md`와 `client/Assets/Scripts/Network/ApiClient.cs`가 기준이다.
