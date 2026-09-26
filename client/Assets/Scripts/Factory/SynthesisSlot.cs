using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCleaners.Capture;

namespace PixelCleaners
{
    /// <summary>
    /// 일반 제작소·고급 제작소 한 기.
    /// 레벨만큼 생명체를 배치할 수 있다 (Lv1 = 1마리 ~ Lv5 = 5마리, 업그레이드 비용은 <see cref="SynthesisUpgrade"/>).
    /// 생명체가 여러 마리면 각자 작업하는 것처럼 제작 속도가 합산된다.
    ///
    /// 요구 능력치: 일반 제작소(슬롯 0-1) = 재구성력, 고급 제작소(슬롯 2-3) = 합성력.
    ///
    /// 수면 사이클: 시설 슬롯과 같이 실제로 제작한 시간이 24시간(<see cref="WorkDuration"/>)을 채우면
    /// 제작소 전체가 잠든다. 재료 부족으로 대기하는 시간은 세지 않는다. 각성제로 다시 깨운다.
    /// </summary>
    public class SynthesisSlot
    {
        public const int      MaxLevel     = 5;
        public const float    DecayBase    = 0.90f;
        public const float    MinFraction  = 0.10f;
        public const float    WorkDuration = HumanCycle.DefaultWorkDuration;

        public int    Index            { get; }
        public Recipe Recipe           { get; private set; }
        public bool   RequiresCreature { get; set; }

        /// 일반 제작소 = 재구성력, 고급 제작소 = 합성력
        public StatType RequiredStat => RequiredStatFor(Index);
        public bool     IsAdvanced   => Index >= SynthesisManager.SynthesisCount;

        public static StatType RequiredStatFor(int slotIndex)
            => slotIndex < SynthesisManager.SynthesisCount ? StatType.Reconstruction : StatType.Synthesis;

        /// 현재 레벨 = 배치 가능한 생명체 수
        public int  Level    { get; private set; } = 1;
        public int  Capacity => Level;
        public bool CanUpgrade => Level < MaxLevel;

        readonly List<CreatureInstance> creatures = new();
        public IReadOnlyList<CreatureInstance> Creatures => creatures;
        public bool HasCreature => creatures.Count > 0;
        public bool IsFull      => creatures.Count >= Capacity;

        public float CraftTimer => craftTimer;
        public float Progress   => Recipe == null ? 0f : Mathf.Clamp01(craftTimer / CycleSec);
        public bool  CanCraft   => Recipe != null
                                   && (!RequiresCreature || HasCreature)
                                   && !IsSleeping
                                   && CheckMaterials();

        // ── 수면 ──
        public bool  IsSleeping    { get; private set; }
        public float WorkElapsed   => workElapsed;
        public float WorkRemaining => Mathf.Max(0f, WorkDuration - workElapsed);

        public event Action OnCraftSuccess;
        public event Action OnStateChanged;

        float craftTimer;
        float workElapsed;

        public SynthesisSlot(int index) => Index = index;

        // ── 속도 계산 ───────────────────────────────────────────────

        /// <summary>
        /// 생명체 한 마리의 사이클 배율 (DecayBase^power, 최소 10%).
        /// 요구 능력치가 아닌 생명체는 배치는 되지만 보너스 없이 1.0 (기본 사이클).
        /// </summary>
        public static float CycleFraction(CreatureInstance c, StatType requiredStat)
        {
            if (c == null || c.definition.specialStat != requiredStat) return 1f;
            return Mathf.Max(Mathf.Pow(DecayBase, c.GetStatPower()), MinFraction);
        }

        public float CycleFraction(CreatureInstance c) => CycleFraction(c, RequiredStat);

        /// <summary>
        /// 여러 마리의 합산 사이클. 각자 1/사이클 속도로 일한다고 보고 처리량을 더한다.
        ///   2마리가 각각 60초 → 30초,  60초 + 30초 → 20초
        /// 생명체가 없으면 레시피 기본 사이클.
        /// </summary>
        public static float EffectiveCycleSeconds(float recipeCycle, IEnumerable<CreatureInstance> workers,
                                                  StatType requiredStat)
        {
            float rate = 0f;
            if (workers != null)
                foreach (var c in workers)
                    if (c != null) rate += 1f / (recipeCycle * CycleFraction(c, requiredStat));
            return rate > 0f ? 1f / rate : recipeCycle;
        }

        float CycleSec => Recipe == null ? 1f : EffectiveCycleSeconds(Recipe.craftCycleSeconds, creatures, RequiredStat);

        /// <summary>creature를 추가로 배치했을 때의 제작 사이클(초). 레시피가 없으면 0.</summary>
        public float PreviewCycleSeconds(CreatureInstance candidate)
        {
            if (Recipe == null) return 0f;
            var workers = new List<CreatureInstance>(creatures);
            if (candidate != null) workers.Add(candidate);
            return EffectiveCycleSeconds(Recipe.craftCycleSeconds, workers, RequiredStat);
        }

        // ── 레시피 ─────────────────────────────────────────────────

        public void AssignRecipe(Recipe r)
        {
            Recipe     = r;
            craftTimer = 0f;
            OnStateChanged?.Invoke();
        }

        public void ClearRecipe()
        {
            Recipe     = null;
            craftTimer = 0f;
            OnStateChanged?.Invoke();
        }

        // ── 생명체 ─────────────────────────────────────────────────

        /// 빈자리가 있으면 추가. 제작 진행도·활동 시간은 유지된다 (일손이 늘 뿐).
        /// 비어 있던 제작소에 첫 생명체가 들어오면 새로 24시간 활동을 시작한다.
        public bool AddCreature(CreatureInstance c)
        {
            if (c == null || IsFull) return false;
            if (creatures.Count == 0) ResetWork();
            creatures.Add(c);
            OnStateChanged?.Invoke();
            return true;
        }

        public CreatureInstance RemoveCreatureAt(int i)
        {
            if (i < 0 || i >= creatures.Count) return null;
            var c = creatures[i];
            creatures.RemoveAt(i);
            if (creatures.Count == 0) ResetWork();   // 전원 회수 = 시설 슬롯의 StopWork와 같다
            OnStateChanged?.Invoke();
            return c;
        }

        // ── 수면·각성 ──────────────────────────────────────────────

        /// 각성제 사용: 수면 → 새 24시간 활동. 각성제 차감은 FactoryManager.TryReactivateSynthesisSlot이 한다
        public void Reactivate()
        {
            if (!IsSleeping) return;
            ResetWork();
            OnStateChanged?.Invoke();
        }

        /// 디버그: 활동 시간을 즉시 소진해 잠재운다
        public void DebugForceSleep()
        {
            if (!HasCreature || IsSleeping) return;
            workElapsed = WorkDuration;
            IsSleeping  = true;
            OnStateChanged?.Invoke();
        }

        void ResetWork()
        {
            workElapsed = 0f;
            IsSleeping  = false;
        }

        // ── 업그레이드 ─────────────────────────────────────────────

        /// 레벨만 올린다. 재료 차감은 FactoryManager.TryUpgradeSynthesisSlot이 한다
        public bool Upgrade()
        {
            if (!CanUpgrade) return false;
            Level++;
            OnStateChanged?.Invoke();
            return true;
        }

        // ── 저장·복원 (이벤트 없음) ─────────────────────────────────

        public void RestoreCraftTimer(float timer) => craftTimer = Mathf.Max(0f, timer);

        public void RestoreLevel(int level) => Level = Mathf.Clamp(level, 1, MaxLevel);

        public void RestoreWork(float elapsed, bool sleeping)
        {
            workElapsed = Mathf.Clamp(elapsed, 0f, WorkDuration);
            IsSleeping  = sleeping && HasCreature;
        }

        public void RestoreCreatures(IEnumerable<CreatureInstance> list)
        {
            creatures.Clear();
            if (list == null) return;
            foreach (var c in list)
                if (c != null && creatures.Count < Capacity) creatures.Add(c);
        }

        // ── 제작 ───────────────────────────────────────────────────

        public void Tick(float dt)
        {
            if (Recipe == null) return;
            if (RequiresCreature && !HasCreature) return;
            if (IsSleeping) return;
            if (!CheckMaterials()) return;

            // 실제로 제작하는 시간만 활동 시간으로 센다
            workElapsed += dt;
            craftTimer  += dt;
            if (craftTimer >= CycleSec)
            {
                craftTimer = 0f;
                ExecuteCraft();
            }

            if (workElapsed >= WorkDuration)
            {
                workElapsed = WorkDuration;
                IsSleeping  = true;
                OnStateChanged?.Invoke();
            }
        }

        bool CheckMaterials()
        {
            if (FactoryManager.Instance == null || Recipe == null) return false;
            var wh = FactoryManager.Instance.Warehouse;
            foreach (var ing in Recipe.ingredients)
            {
                wh.TryGetValue(ing.type, out int have);
                if (have < ing.amount) return false;
            }
            return true;
        }

        void ExecuteCraft()
        {
            if (FactoryManager.Instance == null || Recipe == null) return;
            if (!CheckMaterials()) return;

            foreach (var ing in Recipe.ingredients)
                FactoryManager.Instance.DeductFromWarehouse(ing.type, ing.amount);

            switch (Recipe.outputType)
            {
                case CraftOutputType.Resource:
                    FactoryManager.Instance.AddToWarehouse(Recipe.outputResource, Recipe.outputCount);
                    break;
                case CraftOutputType.CaptureTool:
                    FactoryManager.Instance.AddTool(Recipe.captureToolTier, Recipe.outputCount);
                    break;
            }

            OnCraftSuccess?.Invoke();
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// 제작소 확장 비용. 일반 제작소·고급 제작소 공통, 창고에서 차감한다.
    /// Lv2·3은 1차 재료, Lv4·5는 2차 재료.
    /// </summary>
    public static class SynthesisUpgrade
    {
        // 현재 레벨 → 다음 레벨로 올리는 비용
        static readonly Dictionary<int, List<CraftIngredient>> costs = new()
        {
            // Lv1 → Lv2 (생명체 2마리) — 1차 재료
            { 1, new() { new(ResourceType.Metal,   10), new(ResourceType.Glass, 10) } },
            // Lv2 → Lv3 (3마리) — 1차 재료
            { 2, new() { new(ResourceType.Plastic, 15), new(ResourceType.Paper, 15), new(ResourceType.Can, 10) } },
            // Lv3 → Lv4 (4마리) — 2차 재료
            { 3, new() { new(ResourceType.RecycledComposite, 3), new(ResourceType.RecycledAlloy, 3) } },
            // Lv4 → Lv5 (5마리) — 2차 재료
            { 4, new() { new(ResourceType.RecycledComposite, 6), new(ResourceType.RecycledAlloy, 6) } },
        };

        /// <returns>다음 레벨 비용. 최대 레벨이면 null</returns>
        public static IReadOnlyList<CraftIngredient> CostFor(int currentLevel)
            => costs.TryGetValue(currentLevel, out var c) ? c : null;
    }
}
