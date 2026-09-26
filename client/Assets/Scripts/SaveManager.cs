using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using PixelCleaners;
using PixelCleaners.Capture;

/// <summary>
/// 오프라인 영속성 매니저.
/// OnApplicationPause / OnApplicationQuit 시 자동 저장.
/// 앱 재시작 시 경과 시간만큼 오프라인 생산 시뮬레이션 후 복원.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    const string SaveFileName  = "save.json";
    const float  MaxOfflineSec = 72f * 3600f; // 오프라인 시뮬 최대 72시간

    string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // FacilitySlots가 GameBootstrap.SetupFacilitySlots()로 생성된 뒤 Start에서 로드
    void Start() => Load();

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit()             { Save(); }

    // ── 저장 ──────────────────────────────────────────────────────

    public void Save()
    {
        var fm = FactoryManager.Instance;
        if (fm == null) return;

        var data = new GameSaveData { savedAtUtcTicks = DateTime.UtcNow.Ticks };

        foreach (var kv in fm.Resources)
            data.resources.Add(new ResourceEntry { type = kv.Key.ToString(), count = kv.Value });
        foreach (var kv in fm.Warehouse)
            data.warehouse.Add(new ResourceEntry { type = kv.Key.ToString(), count = kv.Value });
        foreach (var kv in fm.CaptureTools)
            data.captureTools.Add(new CaptureToolEntry { tier = kv.Key.ToString(), count = kv.Value });
        foreach (var kv in fm.AwakenItems)
            data.awakenItems.Add(new AwakenItemEntry { tier = kv.Key.ToString(), count = kv.Value });
        data.deliveryScore = fm.DeliveryScore;
        foreach (var kv in fm.Discovered)
            data.discovered.Add(new DiscoveredEntry { id = kv.Key, firstTicks = kv.Value });
        foreach (var kv in MapSpawnSystem.CaughtEntries)
            data.caughtSpawns.Add(new CaughtSpawnEntry { key = kv.Key, expiresTicks = kv.Value });

        foreach (var c in fm.CreatureInventory)
            data.creatureInventory.Add(ToCreatureSave(c));

        foreach (var slot in FindObjectsByType<FacilitySlot>(FindObjectsSortMode.None))
        {
            var cy  = slot.Cycle;
            var sv  = new FacilitySlotSave
            {
                slotName          = slot.gameObject.name,
                hasCreature       = slot.AssignedCreature != null,
                cycleState        = (int)cy.State,
                workElapsed       = cy.WorkElapsed,
                workDuration      = cy.WorkDuration,
                productionCycle   = cy.ProductionCycle,
                productionElapsed = cy.ProductionElapsed,
                activeOutput      = slot.ActiveOutput.ToString(),
            };
            if (sv.hasCreature) sv.creature = ToCreatureSave(slot.AssignedCreature);
            data.facilitySlots.Add(sv);
        }

        if (SynthesisManager.Instance != null)
        {
            for (int i = 0; i < SynthesisManager.SlotCount; i++)
            {
                var ss = SynthesisManager.Instance.GetSlot(i);
                var sv = new SynthesisSlotSave
                {
                    index       = i,
                    recipeName  = ss.Recipe != null ? ss.Recipe.name : "",
                    level       = ss.Level,
                    hasCreature = ss.HasCreature,
                    craftTimer  = ss.CraftTimer,
                    workElapsed = ss.WorkElapsed,
                    sleeping    = ss.IsSleeping,
                };
                foreach (var c in ss.Creatures)
                    sv.creatures.Add(ToCreatureSave(c));
                // 구버전 호환 필드 — 첫 번째 생명체
                if (sv.hasCreature) sv.creature = ToCreatureSave(ss.Creatures[0]);
                data.synthesisSlots.Add(sv);
            }
        }

        try   { File.WriteAllText(SavePath, JsonUtility.ToJson(data, false)); }
        catch (Exception e) { Debug.LogWarning($"[Save] 저장 실패: {e.Message}"); }
    }

    // ── 불러오기 ───────────────────────────────────────────────────

    void Load()
    {
        if (!File.Exists(SavePath)) return;
        GameSaveData data;
        try   { data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath)); }
        catch (Exception e) { Debug.LogWarning($"[Save] 로드 실패: {e.Message}"); return; }
        if (data == null) return;

        float elapsed = CalculateElapsed(data.savedAtUtcTicks);
        ApplyLoad(data, elapsed);
        Debug.Log($"[Save] 불러오기 완료 (오프라인 {elapsed / 3600f:F1}시간)");
    }

    void ApplyLoad(GameSaveData data, float elapsedSec)
    {
        var fm = FactoryManager.Instance;
        if (fm == null) return;

        // 1. 공장 재고·창고·포획 도구 복원
        foreach (var e in data.resources)
            if (TryParseEnum<ResourceType>(e.type, out var t)) fm.SetResourceDirect(t, e.count);
        foreach (var e in data.warehouse)
            if (TryParseEnum<ResourceType>(e.type, out var t)) fm.SetWarehouseDirect(t, e.count);
        foreach (var e in data.awakenItems)
            if (TryParseEnum<AwakenItemTier>(e.tier, out var t)) fm.SetAwakenItemDirect(t, e.count);
        fm.SetDeliveryScoreDirect(data.deliveryScore);
        // 도감은 인벤토리 복원보다 먼저 — 최초 획득 시각이 불러오는 시점으로 덮이지 않도록
        if (data.discovered != null)
            foreach (var e in data.discovered)
                fm.SetDiscoveredDirect(e.id, e.firstTicks);

        // 지도 포획 기록 — 만료된 항목은 RestoreCaught가 버린다
        var caught = new List<KeyValuePair<string, long>>();
        if (data.caughtSpawns != null)
            foreach (var e in data.caughtSpawns)
                caught.Add(new KeyValuePair<string, long>(e.key, e.expiresTicks));
        MapSpawnSystem.RestoreCaught(caught);
        foreach (var e in data.captureTools)
            if (TryParseEnum<CaptureToolTier>(e.tier, out var t)) fm.SetCaptureToolDirect(t, e.count);

        // 2. 크리처 인벤토리 복원
        fm.ClearCreatureInventory();
        foreach (var cd in data.creatureInventory)
            fm.AddCreatureToInventory(ReconstructCreature(cd));

        // 3. 시설 슬롯: 오프라인 생산 시뮬 → HumanCycle 복원
        var slotMap = BuildSlotMap();
        foreach (var sv in data.facilitySlots)
        {
            if (!slotMap.TryGetValue(sv.slotName, out var slot)) continue;

            bool hasOutput = TryParseEnum<ResourceType>(sv.activeOutput, out var savedOutput);

            if (sv.hasCreature)
            {
                var creature = ReconstructCreature(sv.creature);
                fm.Discover(creature);   // 도감 기능 이전 세이브: 배치 중인 생명체도 발견 처리

                // 픽셀 재구성소가 제로픽셀 전용이 되기 전 세이브: 다른 생명체는 인벤토리로 돌려보낸다
                if (!slot.IsAllowed(creature))
                {
                    fm.AddCreatureToInventory(creature);
                    slot.Cycle.RestoreState(CycleState.Idle, 0f, 0f, slot.BaseCycleSeconds, 0f);
                    if (hasOutput) slot.SetOutput(savedOutput);
                    continue;
                }

                slot.RestoreCreature(creature);
                // 저장 당시 사이클이 아니라 현재 규칙(스탯 일치·티어)으로 재계산
                sv.productionCycle = slot.PreviewCycleSeconds(creature);
            }

            var (postState, postWkEl, postProdEl) = SimulateFacility(sv, elapsedSec, fm);
            slot.Cycle.RestoreState(postState, postWkEl, sv.workDuration,
                                    sv.productionCycle, postProdEl);

            if (hasOutput) slot.SetOutput(savedOutput);
        }

        // 4. 합성 슬롯: 오프라인 제작 시뮬 → craftTimer 복원
        if (SynthesisManager.Instance != null)
        {
            foreach (var sv in data.synthesisSlots)
            {
                if (sv.index >= SynthesisManager.SlotCount) continue;
                var ss = SynthesisManager.Instance.GetSlot(sv.index);

                var recipe = FindRecipeByName(sv.recipeName);
                if (recipe != null) ss.AssignRecipe(recipe);

                // 레벨을 먼저 복원해야 생명체가 정원 초과로 잘리지 않는다 (level 필드 없는 구버전 = 1)
                ss.RestoreLevel(sv.level > 0 ? sv.level : 1);

                var creatures = new List<CreatureInstance>();
                if (sv.creatures != null && sv.creatures.Count > 0)
                    foreach (var cd in sv.creatures) creatures.Add(ReconstructCreature(cd));
                else if (sv.hasCreature)
                    creatures.Add(ReconstructCreature(sv.creature));   // 구버전: 생명체 1마리
                ss.RestoreCreatures(creatures);
                foreach (var c in creatures) fm.Discover(c);
                ss.RestoreWork(sv.workElapsed, sv.sleeping);

                var (postTimer, postWork, postSleeping) =
                    SimulateSynthesis(sv, elapsedSec, recipe, ss, fm);
                ss.RestoreCraftTimer(postTimer);
                ss.RestoreWork(postWork, postSleeping);
            }
        }

        fm.NotifyInventoryChanged();
    }

    // ── 오프라인 시뮬: 시설 슬롯 ─────────────────────────────────

    static (CycleState state, float workElapsed, float prodElapsed)
        SimulateFacility(FacilitySlotSave sv, float elapsedSec, FactoryManager fm)
    {
        var state = (CycleState)sv.cycleState;
        // 재고 상한이 있던 구버전의 '가득참' 정지 → 상한이 없어졌으므로 이어서 일한다
        if (state == CycleState.Paused && sv.hasCreature) state = CycleState.Working;
        if (state != CycleState.Working) return (state, sv.workElapsed, sv.productionElapsed);
        if (!TryParseEnum<ResourceType>(sv.activeOutput, out var resType))
            return (state, sv.workElapsed, sv.productionElapsed);

        // 남은 활동 시간 내에서만 시뮬
        float canWork    = sv.workDuration - sv.workElapsed;
        float toSimulate = Mathf.Min(elapsedSec, canWork);
        float timer      = sv.productionElapsed;
        float cycle      = Mathf.Max(sv.productionCycle, 0.1f);
        float consumed   = 0f;

        while (consumed < toSimulate)
        {
            float toTick = cycle - timer;
            if (consumed + toTick > toSimulate)
            {
                timer   += toSimulate - consumed;
                consumed = toSimulate;
                break;
            }
            consumed += toTick;
            timer     = 0f;

            fm.AddResource(resType, 1);   // 공장 재고 상한 없음
        }

        float newWkEl = sv.workElapsed + consumed;
        if (newWkEl >= sv.workDuration) return (CycleState.Sleeping, sv.workDuration, 0f);
        return (CycleState.Working, newWkEl, timer);
    }

    // ── 오프라인 시뮬: 합성 슬롯 ─────────────────────────────────

    /// 제작한 시간만 활동 시간으로 세고, 24시간을 채우면 잠든다 (게임 중 SynthesisSlot.Tick과 같은 규칙)
    static (float timer, float workElapsed, bool sleeping) SimulateSynthesis(
        SynthesisSlotSave sv, float elapsedSec, Recipe recipe, SynthesisSlot slot, FactoryManager fm)
    {
        float work = slot.WorkElapsed;
        if (recipe == null || !slot.HasCreature || slot.IsSleeping)
            return (sv.craftTimer, work, slot.IsSleeping);

        // 게임 중과 같은 합산 속도 (생명체 여러 마리, 제작소 요구 능력치 기준)
        float cycleSec  = SynthesisSlot.EffectiveCycleSeconds(recipe.craftCycleSeconds, slot.Creatures, slot.RequiredStat);
        float timer     = sv.craftTimer;
        float remaining = Mathf.Min(elapsedSec, SynthesisSlot.WorkDuration - work);

        while (remaining > 0f)
        {
            if (!HasOfflineMaterials(recipe, fm)) break;   // 재료가 떨어지면 대기 — 활동 시간도 멈춘다
            float toComplete = cycleSec - timer;
            if (remaining < toComplete) { timer += remaining; work += remaining; break; }
            remaining -= toComplete;
            work      += toComplete;
            timer      = 0f;
            TryOfflineCraft(recipe, fm);
        }

        bool sleeping = work >= SynthesisSlot.WorkDuration - 0.01f;
        return (timer, Mathf.Min(work, SynthesisSlot.WorkDuration), sleeping);
    }

    static bool HasOfflineMaterials(Recipe recipe, FactoryManager fm)
    {
        foreach (var ing in recipe.ingredients)
        {
            fm.Warehouse.TryGetValue(ing.type, out int have);
            if (have < ing.amount) return false;
        }
        return true;
    }

    static bool TryOfflineCraft(Recipe recipe, FactoryManager fm)
    {
        var wh = fm.Warehouse;
        foreach (var ing in recipe.ingredients)
        {
            wh.TryGetValue(ing.type, out int have);
            if (have < ing.amount) return false;
        }
        foreach (var ing in recipe.ingredients)
            fm.DeductFromWarehouse(ing.type, ing.amount);

        switch (recipe.outputType)
        {
            case CraftOutputType.Resource:
                fm.AddToWarehouse(recipe.outputResource, recipe.outputCount); break;
            case CraftOutputType.CaptureTool:
                fm.AddTool(recipe.captureToolTier, recipe.outputCount); break;
        }
        return true;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    static float CalculateElapsed(long savedTicks)
    {
        float e = (float)((DateTime.UtcNow.Ticks - savedTicks) / (double)TimeSpan.TicksPerSecond);
        return Mathf.Clamp(e, 0f, MaxOfflineSec);
    }

    static Dictionary<string, FacilitySlot> BuildSlotMap()
    {
        var map = new Dictionary<string, FacilitySlot>();
        foreach (var s in FindObjectsByType<FacilitySlot>(FindObjectsSortMode.None))
            map[s.gameObject.name] = s;
        return map;
    }

    static CreatureInstance ReconstructCreature(CreatureSaveData cd)
    {
        var so = ScriptableObject.CreateInstance<CreatureSO>();
        so.name            = cd.creatureId ?? "";
        so.purifiedName    = cd.purifiedName;
        so.capturedName    = cd.capturedName;
        so.specialStat     = (StatType)cd.stat;
        so.rarity          = (CreatureRarity)cd.rarity;
        so.baseSpawnWeight = 0.15f;

        // 티어·능력치·특화 자원은 로스터를 기준으로 복원한다. 구버전 세이브나
        // 이후 로스터에서 기획을 조정한 경우(예: 캔버그 용해력 → 단조력)에도 현재 기획값이 적용된다.
        var entry = CreatureRoster.Find(cd.creatureId, cd.purifiedName);
        so.tier   = entry != null ? entry.tier : cd.tier;
        if (entry != null)
        {
            so.name              = entry.id;
            so.specialStat       = entry.stat;
            so.hasSpecialty      = entry.specialty.HasValue;
            so.specialtyResource = entry.specialty ?? default;
            so.isPixelExclusive  = entry.isPixelExclusive;
            so.icon = CreatureRoster.LoadIcon(entry);   // 아이콘은 저장하지 않고 로스터에서 다시 찾는다
        }
        var inst   = new CreatureInstance(so, cd.grade);
        inst.uniqueId = cd.uniqueId;
        return inst;
    }

    static CreatureSaveData ToCreatureSave(CreatureInstance c) => new()
    {
        uniqueId     = c.uniqueId,
        creatureId   = c.definition.name,
        tier         = c.definition.tier,
        purifiedName = c.definition.purifiedName,
        capturedName = c.definition.capturedName,
        stat         = (int)c.definition.specialStat,
        rarity       = (int)c.definition.rarity,
        grade        = c.grade,
    };

    static Recipe FindRecipeByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var r in RecipeBook.Synthesis) if (r.name == name) return r;
        foreach (var r in RecipeBook.Craft)     if (r.name == name) return r;
        return null;
    }

    static bool TryParseEnum<T>(string s, out T result) where T : struct
        => Enum.TryParse(s, out result);
}
