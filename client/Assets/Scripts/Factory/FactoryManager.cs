using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCleaners.Capture;

namespace PixelCleaners
{
    public class FactoryManager : MonoBehaviour
    {
        public static FactoryManager Instance { get; private set; }

        readonly List<CreatureInstance> creatureInventory = new();

        // 공장 재고: 크리처가 생산한 기초/1차 자원 (상한 없음 — 수거하면 창고로)
        readonly Dictionary<ResourceType, int> resources = new()
        {
            { ResourceType.Garbage,       0 },
            { ResourceType.Plastic,       0 },
            { ResourceType.Glass,         0 },
            { ResourceType.Metal,         0 },
            { ResourceType.Can,           0 },
            { ResourceType.Paper,         0 },
            { ResourceType.Textile,       0 },
            { ResourceType.PixelFragment, 0 },
        };

        // 창고: 수거된 자원 + 합성/제작 출력물 (상한 없음)
        readonly Dictionary<ResourceType, int> warehouse = new()
        {
            { ResourceType.Garbage,           0 },
            { ResourceType.Plastic,           0 },
            { ResourceType.Glass,             0 },
            { ResourceType.Metal,             0 },
            { ResourceType.Can,               0 },
            { ResourceType.Paper,             0 },
            { ResourceType.Textile,           0 },
            { ResourceType.PixelFragment,     0 },
            { ResourceType.RecycledComposite, 0 },
            { ResourceType.RecycledAlloy,     0 },
            { ResourceType.DeliveryItem,         0 },
            { ResourceType.AdvancedDeliveryItem, 0 },
        };

        readonly Dictionary<CaptureToolTier, int> captureTools = new()
        {
            { CaptureToolTier.Basic,     -1 },
            { CaptureToolTier.Enhanced,   0 },
            { CaptureToolTier.Precision,  0 },
            { CaptureToolTier.Pixel,      0 }
        };

        readonly Dictionary<AwakenItemTier, int> awakenItems = new()
        {
            { AwakenItemTier.Normal,   0 },
            { AwakenItemTier.Advanced, 0 },
            { AwakenItemTier.Plogging, 0 },
        };

        int deliveryScore;

        public IReadOnlyDictionary<ResourceType, int>    Resources        => resources;
        public IReadOnlyDictionary<ResourceType, int>    Warehouse        => warehouse;
        public IReadOnlyDictionary<CaptureToolTier, int> CaptureTools     => captureTools;
        public IReadOnlyDictionary<AwakenItemTier, int>  AwakenItems      => awakenItems;
        public IReadOnlyList<CreatureInstance>           CreatureInventory => creatureInventory;
        public int                                        DeliveryScore    => deliveryScore;

        public const int PointsPerDelivery         = 100;   // 납품 물품
        public const int PointsPerAdvancedDelivery = 250;   // 고급 납품 물품

        public event Action OnInventoryChanged;
        public event Action<ResourceType> OnResourceCollected;

        // ── 도감: 한 번이라도 획득한 생명체 (로스터 ID → 최초 획득 UTC ticks) ──
        readonly Dictionary<string, long> discovered = new();
        public IReadOnlyDictionary<string, long> Discovered => discovered;
        /// 처음 발견했을 때만 발생 (로스터 ID)
        public event Action<string> OnCreatureDiscovered;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void AddCreatureToInventory(CreatureInstance creature)
        {
            creatureInventory.Add(creature);
            Discover(creature);
            OnInventoryChanged?.Invoke();
        }

        public bool IsDiscovered(string creatureId)
            => !string.IsNullOrEmpty(creatureId) && discovered.ContainsKey(creatureId);

        /// <summary>
        /// 도감 해금. 이미 발견했으면 아무것도 하지 않는다 (최초 획득 시각 유지).
        /// 로스터에 없는 생명체(ID가 빈 디버그 생명체 등)는 무시한다.
        /// </summary>
        public void Discover(CreatureInstance creature)
        {
            string id = creature?.definition != null ? creature.definition.name : null;
            if (string.IsNullOrEmpty(id) || CreatureRoster.Find(id, null) == null) return;
            if (discovered.ContainsKey(id)) return;
            discovered[id] = DateTime.UtcNow.Ticks;
            OnCreatureDiscovered?.Invoke(id);
        }

        /// 저장 복원용 (이벤트 없음)
        public void SetDiscoveredDirect(string creatureId, long firstTicks)
        {
            if (string.IsNullOrEmpty(creatureId)) return;
            discovered[creatureId] = firstTicks;
        }

        public void AddResource(ResourceType type, int amount = 1)
        {
            if (!resources.ContainsKey(type)) return;
            resources[type] += amount;
            OnInventoryChanged?.Invoke();
        }

        // 공장 재고 → 창고 이전
        public int CollectResource(ResourceType type)
        {
            if (!resources.TryGetValue(type, out int amount) || amount <= 0) return 0;
            resources[type] = 0;
            if (!warehouse.ContainsKey(type)) warehouse[type] = 0;
            warehouse[type] += amount;
            OnResourceCollected?.Invoke(type);
            OnInventoryChanged?.Invoke();
            return amount;
        }

        // 합성/제작 결과물을 창고에 직접 추가 (2차 자원, 납품물품)
        public void AddToWarehouse(ResourceType type, int amount = 1)
        {
            if (!warehouse.ContainsKey(type)) warehouse[type] = 0;
            warehouse[type] += amount;
            OnInventoryChanged?.Invoke();
        }

        // 창고 재료 차감 (합성/제작 레시피 소모)
        public bool DeductFromWarehouse(ResourceType type, int amount)
        {
            if (!warehouse.TryGetValue(type, out int have) || have < amount) return false;
            warehouse[type] = have - amount;
            OnInventoryChanged?.Invoke();
            return true;
        }

        // 정제소 생산 출력 전환: 해당 시설의 모든 슬롯 출력 변경
        public void SetRefineryOutput(FacilityType type, ResourceType resource)
        {
            foreach (var slot in FindObjectsByType<FacilitySlot>(FindObjectsSortMode.None))
                if (slot.Definition != null && slot.Definition.facilityType == type)
                    slot.SetOutput(resource);
            OnInventoryChanged?.Invoke();
        }

        // ── 포획 도구 ──────────────────────────────────────────────
        public bool HasTool(CaptureToolTier tier)
            => captureTools[tier] < 0 || captureTools[tier] > 0;

        public bool UseTool(CaptureToolTier tier)
        {
            if (!HasTool(tier)) return false;
            if (captureTools[tier] > 0) captureTools[tier]--;
            return true;
        }

        public void AddTool(CaptureToolTier tier, int count)
        {
            if (captureTools[tier] < 0) return;
            captureTools[tier] = Mathf.Max(0, captureTools[tier] + count);
            OnInventoryChanged?.Invoke();
        }

        // ── 각성제 ─────────────────────────────────────────────────
        public void AddAwakenItem(AwakenItemTier tier, int count)
        {
            awakenItems[tier] = Mathf.Max(0, awakenItems[tier] + count);
            OnInventoryChanged?.Invoke();
        }

        public void ReactivateSlot(FacilitySlot slot, AwakenItemTier tier)
        {
            if (awakenItems[tier] <= 0) return;
            awakenItems[tier]--;
            slot.Reactivate();
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// 보관 기한이 짧은 등급부터 소모해 수면 중인 슬롯을 재활성한다.
        /// 사용 가능한 각성제가 없으면 false.
        /// </summary>
        public bool TryReactivateSlot(FacilitySlot slot)
        {
            if (slot == null || slot.AssignedCreature == null) return false;
            if (slot.Cycle.State != CycleState.Sleeping) return false;

            foreach (var tier in AwakenPriority)
            {
                if (awakenItems[tier] <= 0) continue;
                ReactivateSlot(slot, tier);
                return true;
            }
            return false;
        }

        /// <summary>수면 중인 제작소를 각성제 1개로 재활성한다 (시설 슬롯과 같은 소모 순서).</summary>
        public bool TryReactivateSynthesisSlot(int slotIndex)
        {
            if (SynthesisManager.Instance == null) return false;
            var slot = SynthesisManager.Instance.GetSlot(slotIndex);
            if (slot == null || !slot.IsSleeping || !slot.HasCreature) return false;

            foreach (var tier in AwakenPriority)
            {
                if (awakenItems[tier] <= 0) continue;
                awakenItems[tier]--;
                slot.Reactivate();
                OnInventoryChanged?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>보유한 각성제 총 개수.</summary>
        public int TotalAwakenItems
        {
            get
            {
                int sum = 0;
                foreach (var kv in awakenItems) sum += kv.Value;
                return sum;
            }
        }

        // 유효기간이 짧은 순 — 먼저 썩는 것부터 쓴다
        static readonly AwakenItemTier[] AwakenPriority =
        {
            AwakenItemTier.Normal, AwakenItemTier.Advanced, AwakenItemTier.Plogging
        };

        // ── 납품 ───────────────────────────────────────────────────
        /// 지금 창고의 납품 물품을 모두 납품하면 얻는 점수
        public int PendingDeliveryPoints
        {
            get
            {
                warehouse.TryGetValue(ResourceType.DeliveryItem,         out int normal);
                warehouse.TryGetValue(ResourceType.AdvancedDeliveryItem, out int advanced);
                return normal * PointsPerDelivery + advanced * PointsPerAdvancedDelivery;
            }
        }

        /// <summary>일반·고급 납품 물품을 모두 납품한다.</summary>
        /// <returns>납품한 물품 수 (일반 + 고급)</returns>
        public int Deliver()
        {
            warehouse.TryGetValue(ResourceType.DeliveryItem,         out int normal);
            warehouse.TryGetValue(ResourceType.AdvancedDeliveryItem, out int advanced);
            if (normal + advanced <= 0) return 0;

            deliveryScore += PendingDeliveryPoints;
            warehouse[ResourceType.DeliveryItem]         = 0;
            warehouse[ResourceType.AdvancedDeliveryItem] = 0;
            OnInventoryChanged?.Invoke();
            return normal + advanced;
        }

        // ── 저장·복원용 직접 설정 (이벤트 발생 없음) ──────────────
        public void SetResourceDirect(ResourceType type, int count)
        {
            if (resources.ContainsKey(type))
                resources[type] = Mathf.Max(0, count);
        }

        public void SetWarehouseDirect(ResourceType type, int count)
        {
            if (warehouse.ContainsKey(type))
                warehouse[type] = Mathf.Max(0, count);
        }

        public void SetCaptureToolDirect(CaptureToolTier tier, int count)
        {
            if (captureTools.ContainsKey(tier) && captureTools[tier] >= 0)
                captureTools[tier] = Mathf.Max(0, count);
        }

        public void SetAwakenItemDirect(AwakenItemTier tier, int count)
            => awakenItems[tier] = Mathf.Max(0, count);

        public void SetDeliveryScoreDirect(int score) => deliveryScore = Mathf.Max(0, score);

        public void ClearCreatureInventory() => creatureInventory.Clear();

        public void NotifyInventoryChanged() => OnInventoryChanged?.Invoke();

        // ── 합성·고급 제작소 생명체 배치 (레벨만큼) ───────────────
        public bool AssignCreatureToSynthesisSlot(CreatureInstance creature, int slotIndex)
        {
            if (SynthesisManager.Instance == null) return false;
            var slot = SynthesisManager.Instance.GetSlot(slotIndex);
            if (!slot.AddCreature(creature)) return false;   // 가득 참
            creatureInventory.Remove(creature);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <param name="creatureIndex">제작소 안에서 몇 번째 생명체인지</param>
        public void UnassignCreatureFromSynthesisSlot(int slotIndex, int creatureIndex)
        {
            if (SynthesisManager.Instance == null) return;
            var slot = SynthesisManager.Instance.GetSlot(slotIndex);
            var c = slot.RemoveCreatureAt(creatureIndex);
            if (c == null) return;
            creatureInventory.Add(c);
            OnInventoryChanged?.Invoke();
        }

        // ── 제작소 확장 ────────────────────────────────────────────

        public bool CanAffordSynthesisUpgrade(int slotIndex)
        {
            if (SynthesisManager.Instance == null) return false;
            var slot = SynthesisManager.Instance.GetSlot(slotIndex);
            var cost = SynthesisUpgrade.CostFor(slot.Level);
            if (!slot.CanUpgrade || cost == null) return false;
            foreach (var ing in cost)
            {
                warehouse.TryGetValue(ing.type, out int have);
                if (have < ing.amount) return false;
            }
            return true;
        }

        /// <summary>창고 재료를 차감하고 제작소 레벨을 올린다 (배치 가능 생명체 +1).</summary>
        public bool TryUpgradeSynthesisSlot(int slotIndex)
        {
            if (!CanAffordSynthesisUpgrade(slotIndex)) return false;
            var slot = SynthesisManager.Instance.GetSlot(slotIndex);
            foreach (var ing in SynthesisUpgrade.CostFor(slot.Level))
                warehouse[ing.type] -= ing.amount;
            slot.Upgrade();
            OnInventoryChanged?.Invoke();
            return true;
        }

        // ── 크리처 배치 ────────────────────────────────────────────
        public bool AssignCreatureToSlot(CreatureInstance creature, FacilitySlot slot)
        {
            if (!slot.CanAccept(creature)) return false;
            slot.AssignCreature(creature);
            creatureInventory.Remove(creature);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public bool ForceAssignToSlot(CreatureInstance creature, FacilitySlot slot)
        {
            if (slot.AssignedCreature != null) return false;
            if (!slot.IsAllowed(creature)) return false;   // 픽셀 재구성소 = 제로픽셀 전용
            slot.AssignCreature(creature);
            creatureInventory.Remove(creature);
            OnInventoryChanged?.Invoke();
            return true;
        }

        public void UnassignCreatureFromSlot(FacilitySlot slot)
        {
            var creature = slot.UnassignCreature();
            if (creature == null) return;
            creatureInventory.Add(creature);
            OnInventoryChanged?.Invoke();
        }
    }
}
