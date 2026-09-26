using System;
using UnityEngine;

namespace PixelCleaners
{
    public enum CycleState { Idle, Working, Sleeping, Paused }

    /// <summary>
    /// 생명체 공장 사이클.
    /// StartWork() → 24시간 연속 활동 (생산 틱 반복) → Sleeping.
    /// 수면 중에는 재활성재(Reactivate)로만 재개 가능.
    /// Paused는 공장 재고 상한이 있던 구버전 세이브 호환용으로만 남아 있다 (현재는 상한 없음).
    /// </summary>
    public class HumanCycle : MonoBehaviour
    {
        // 기본 1회 활동 지속 시간 (재활성재 미사용 시)
        public const float DefaultWorkDuration = 86400f; // 24시간

        public CycleState State       { get; private set; } = CycleState.Idle;
        public float WorkProgress     => workDuration > 0f ? Mathf.Clamp01(workElapsed / workDuration) : 0f;
        public float WorkRemaining    => Mathf.Max(0f, workDuration - workElapsed);

        public event Action              OnProductionTick;  // productionCycle 마다 아이템 생산
        public event Action              OnWorkComplete;    // 활동 시간 만료 시 (수면 전)
        public event Action<CycleState>  OnStateChanged;

        float productionCycle;   // 아이템 1개 생산 간격 (초)
        float productionElapsed;
        float workDuration;      // 이번 활동 기간 (재활성재 등급에 따라 다름)
        float workElapsed;       // 누적 활동 시간 (Paused 시 정지)

        // ── 공개 메서드 ──────────────────────────────────────────

        /// <summary>새 생명체 배치 또는 재시작: 24시간 활동 개시.</summary>
        public void StartWork(float prodCycleSeconds, float durationSeconds = DefaultWorkDuration)
        {
            productionCycle   = Mathf.Max(prodCycleSeconds, 0.1f);
            productionElapsed = 0f;
            workDuration      = durationSeconds;
            workElapsed       = 0f;
            SetState(CycleState.Working);
        }

        /// <summary>자원 수거 후 재개: 24시간 타이머는 유지됨.</summary>
        public void Resume()
        {
            if (State != CycleState.Paused) return;
            productionElapsed = 0f;
            SetState(CycleState.Working);
        }

        /// <summary>생명체 회수: 모든 상태 초기화.</summary>
        public void StopWork()
        {
            workElapsed       = 0f;
            productionElapsed = 0f;
            workDuration      = 0f;
            SetState(CycleState.Idle);
        }

        /// <summary>일시 정지 (24시간 타이머 정지).</summary>
        public void Pause() => SetState(CycleState.Paused);

        /// <summary>디버그: 활동 시간을 즉시 소진해 수면 상태로 만든다.</summary>
        public void DebugForceSleep()
        {
            if (State != CycleState.Working && State != CycleState.Paused) return;
            workElapsed       = workDuration;
            productionElapsed = 0f;
            SetState(CycleState.Sleeping);
        }

        /// <summary>재활성재 사용: 수면 → 활동 재개. durationSeconds = 재활성재가 부여하는 활동 시간.</summary>
        public void Reactivate(float durationSeconds)
        {
            if (State != CycleState.Sleeping) return;
            productionElapsed = 0f;
            workElapsed       = 0f;
            workDuration      = durationSeconds;
            SetState(CycleState.Working);
        }

        // ── 저장·복원 ──────────────────────────────────────────────

        public float ProductionCycle   => productionCycle;
        public float ProductionElapsed => productionElapsed;
        public float WorkElapsed       => workElapsed;
        public float WorkDuration      => workDuration;

        // 앱 재시작 시 상태 무음 복원 (이벤트 발생 없음)
        public void RestoreState(CycleState state, float wkElapsed, float wkDuration,
                                 float prodCycle, float prodElapsed)
        {
            workElapsed       = wkElapsed;
            workDuration      = wkDuration;
            productionCycle   = Mathf.Max(prodCycle, 0.1f);
            productionElapsed = prodElapsed;
            State             = state;
        }

        // ── 내부 ─────────────────────────────────────────────────

        void Update()
        {
            if (State != CycleState.Working) return;

            float dt = Time.deltaTime;
            productionElapsed += dt;
            workElapsed       += dt;

            // 생산 틱: 생산 사이클마다 OnProductionTick 발생
            while (productionElapsed >= productionCycle && State == CycleState.Working)
            {
                productionElapsed -= productionCycle;
                OnProductionTick?.Invoke();
            }

            // 활동 시간 만료 → 수면
            if (State == CycleState.Working && workElapsed >= workDuration)
            {
                workElapsed = workDuration;
                OnWorkComplete?.Invoke();
                if (State != CycleState.Paused)   // 마지막 틱에서 Pause 됐을 수 있음
                    SetState(CycleState.Sleeping);
            }
        }

        void SetState(CycleState next)
        {
            State = next;
            OnStateChanged?.Invoke(next);
        }
    }
}
