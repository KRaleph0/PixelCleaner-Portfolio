using UnityEngine;

namespace PixelCleaners
{
    public class FacilitySlot : MonoBehaviour
    {
        [SerializeField] FacilitySO definition;

        public FacilitySO       Definition       => definition;
        public CreatureInstance AssignedCreature { get; private set; }
        public HumanCycle       Cycle            { get; private set; }
        public ResourceType     ActiveOutput     { get; private set; }

        const float DecayBase   = 0.90f;
        const float MinFraction = 0.05f;

        // 요구 스탯이 맞으면 능력치만큼 단축 (시설의 모든 생산품에 동일), 안 맞으면 배치는 되지만 기본 사이클 그대로.
        // 특화 생산 자원(CreatureSO.specialtyResource)은 컨셉 표시용이라 속도에 영향이 없다
        float ComputeProductionCycle(CreatureInstance creature)
        {
            if (!MatchesStat(creature)) return definition.baseCycleSeconds;
            int power = creature.GetStatPower();
            float fraction = Mathf.Max(Mathf.Pow(DecayBase, power), MinFraction);
            return definition.baseCycleSeconds * fraction;
        }

        public bool MatchesStat(CreatureInstance creature)
            => definition != null && creature != null
               && creature.definition.specialStat == definition.requiredStat
               && IsAllowed(creature);

        /// <summary>
        /// 이 슬롯에 배치할 수 있는 생명체인지. 픽셀 재구성소는 픽셀 전용 생명체(제로픽셀)만 받는다.
        /// 그 외 시설은 누구나 배치 가능 (스탯이 안 맞으면 보너스만 없음).
        /// </summary>
        public bool IsAllowed(CreatureInstance creature)
            => definition != null && creature != null
               && (!definition.pixelExclusiveOnly || creature.definition.isPixelExclusive);

        /// <summary>
        /// 이 슬롯에 creature를 배치했을 때의 생산 사이클(초). 배치 팝업 미리보기용.
        /// 실제 배치와 같은 함수를 쓰므로 계산 규칙이 바뀌어도 표시가 어긋나지 않는다.
        /// </summary>
        public float PreviewCycleSeconds(CreatureInstance creature)
            => definition != null && creature != null ? ComputeProductionCycle(creature) : 0f;

        public float BaseCycleSeconds => definition != null ? definition.baseCycleSeconds : 0f;

        void Awake()
        {
            ActiveOutput = definition != null ? definition.outputResource : default;
            Cycle = GetComponent<HumanCycle>() ?? gameObject.AddComponent<HumanCycle>();
            Cycle.OnProductionTick += ProduceResource;
        }

        void OnDestroy()
        {
            if (Cycle != null)
                Cycle.OnProductionTick -= ProduceResource;
        }

        // 공장 재고 상한은 없다 — 수거하지 않아도 생산이 멈추지 않는다
        void ProduceResource()
        {
            if (definition == null || AssignedCreature == null) return;
            FactoryManager.Instance?.AddResource(ActiveOutput);
        }

        // HumanCycle 상태를 SaveManager가 별도로 복원할 때 사용 (StartWork 호출 없음)
        public void RestoreCreature(CreatureInstance creature)
        {
            AssignedCreature = creature;
        }

        public void SetDefinition(FacilitySO so)
        {
            definition   = so;
            ActiveOutput = so != null ? so.outputResource : default;
        }

        // 정제소 생산 출력 전환 (카드 버튼에서 호출)
        public void SetOutput(ResourceType resource)
        {
            ActiveOutput = resource;
            if (AssignedCreature == null) return;

            // 재고 상한이 있던 구버전 세이브의 '가득참' 정지 상태 해제
            if (Cycle.State == CycleState.Paused) Cycle.Resume();
        }

        public bool CanAccept(CreatureInstance creature)
        {
            if (AssignedCreature != null) return false;
            return MatchesStat(creature);
        }

        public void AssignCreature(CreatureInstance creature)
        {
            AssignedCreature = creature;
            Cycle.StartWork(ComputeProductionCycle(creature));
        }

        public CreatureInstance UnassignCreature()
        {
            var c = AssignedCreature;
            if (c == null) return null;
            AssignedCreature = null;
            Cycle.StopWork();
            return c;
        }

        public void Reactivate()
        {
            if (AssignedCreature == null) return;
            Cycle.Reactivate(HumanCycle.DefaultWorkDuration);
        }
    }
}
