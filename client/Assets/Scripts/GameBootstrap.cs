using UnityEngine;
using UnityEngine.SceneManagement;
using PixelCleaners;
using PixelCleaners.GPS;

public class GameBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        if (FindFirstObjectByType<GameBootstrap>() != null) return;
        DontDestroyOnLoad(new GameObject("[Bootstrap]").AddComponent<GameBootstrap>());
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        CreatePersistentManagers();
        SeedHotspot();
        SetupFacilitySlots();
        RedirectToMapIfNeeded();
    }

    static void RedirectToMapIfNeeded()
    {
        var current = SceneManager.GetActiveScene().name;

        // 로그인 씬은 LoginSceneSetup이 등록 여부를 판단해 직접 Map으로 넘긴다.
        // 여기서 또 로드하면 같은 프레임에 씬을 두 번 로드하게 된다.
        if (current == SceneController.Login) return;

        if (!PlayerSession.IsRegistered)
        {
            // LoginScene이 Build Settings에 없으면 LoadScene이 예외를 던진다.
            // 씬을 아직 만들지 않은 상태에서도 앱이 뜨도록 방어한다.
            if (SceneController.IsSceneInBuild(SceneController.Login))
                SceneManager.LoadScene(SceneController.Login);
            else
                Debug.LogWarning("[Bootstrap] LoginScene이 Build Settings에 없어 로그인을 건너뜁니다. " +
                                 "PixelCleaners → 씬 생성 을 실행하세요.");
            return;
        }

        if (current == SceneController.AR)
            SceneManager.LoadScene(SceneController.Map);
    }

    void CreatePersistentManagers()
    {
        EnsurePersistent<SceneController>("SceneController");
        EnsurePersistent<LocationManager>("LocationManager");
        EnsurePersistent<HotspotDetector>("HotspotDetector");
        EnsurePersistent<FactoryManager>("FactoryManager");
        EnsurePersistent<SynthesisManager>("SynthesisManager");
        EnsurePersistent<SaveManager>("SaveManager");
        EnsureEventSystem();
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var esGO = new GameObject("EventSystem");
        DontDestroyOnLoad(esGO);
        esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
        // 프로젝트가 Input System 전용(activeInputHandler=1)이라
        // StandaloneInputModule은 실행 즉시 예외를 던진다.
        esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void SeedHotspot()
    {
        var det = HotspotDetector.Instance;
        if (det == null || det.HotspotCount > 0) return;

        // 에디터 Mock GPS(서울시청)와 같은 위치 — 에디터 테스트용
        det.AddHotspot(new HotspotData
        {
            label        = "테스트 핫스팟",
            latitude     = 37.5665f,
            longitude    = 126.9780f,
            grade        = HotspotGrade.Hotspot,
            radiusMeters = 500f
        });

        // 군산 은파호수공원 — 호수 전체와 둘레 산책로(8.56km)를 덮는다.
        // 중심 = OpenStreetMap 호수 범위의 중앙(남북 1.65km × 동서 1.29km).
        // 반경 1,100m: 음악분수·물빛다리가 있는 공원 중심부(820m)와 입구 버스정류장(1,020m) 포함
        det.AddHotspot(new HotspotData
        {
            label        = "은파호수공원",
            latitude     = 35.95098f,
            longitude    = 126.69731f,
            grade        = HotspotGrade.Hotspot,
            radiusMeters = 1100f
        });
    }

    // ── 시설 슬롯 5종 (크리처 기반 자원 생산) ─────────────────────
    // 일반 제작소(재구성력) & 고급 제작소(합성력)는 SynthesisManager(레시피 기반)로 처리

    void SetupFacilitySlots()
    {
        if (FindFirstObjectByType<FacilitySlot>() != null) return;

        // (type, requiredStat, cycleSeconds, primaryOutput, secondaryOutput, hasChoice, slotCount)
        var defs = new[]
        {
            (FacilityType.PollutionCollector,  StatType.PollutionDetection, 10f,
             ResourceType.Garbage,       ResourceType.Garbage,  false, 5),

            (FacilityType.DissolutionRefinery, StatType.Dissolution,        15f,
             ResourceType.Plastic,       ResourceType.Glass,    true,  5),

            (FacilityType.ForgingRefinery,     StatType.Forging,            15f,
             ResourceType.Metal,         ResourceType.Can,      true,  5),

            (FacilityType.CompressionRefinery, StatType.Compression,        15f,
             ResourceType.Paper,         ResourceType.Textile,  true,  5),

            (FacilityType.PixelReconstructor,  StatType.Special,            20f,
             ResourceType.PixelFragment, ResourceType.PixelFragment, false, 5),
        };

        var parent = new GameObject("[FacilitySlots]").transform;
        DontDestroyOnLoad(parent.gameObject);

        foreach (var (type, stat, cycle, primary, secondary, choice, slotCount) in defs)
        {
            var fso = ScriptableObject.CreateInstance<FacilitySO>();
            fso.facilityType     = type;
            fso.requiredStat     = stat;
            fso.baseCycleSeconds = cycle;
            fso.outputResource   = primary;
            fso.secondaryOutput  = secondary;
            fso.hasChoice        = choice;
            // 픽셀 파편은 희귀 재화 — 제로픽셀만 생산한다
            fso.pixelExclusiveOnly = type == FacilityType.PixelReconstructor;

            for (int i = 0; i < slotCount; i++)
            {
                var slotGO = new GameObject($"Slot_{type}_{i}");
                slotGO.transform.SetParent(parent);
                slotGO.AddComponent<FacilitySlot>().SetDefinition(fso);
            }
        }
    }

    static T EnsurePersistent<T>(string goName) where T : MonoBehaviour
    {
        var existing = FindFirstObjectByType<T>();
        if (existing != null) return existing;
        var go = new GameObject(goName);
        DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }
}
