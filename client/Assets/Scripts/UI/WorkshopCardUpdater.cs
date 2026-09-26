using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PixelCleaners.UI
{
    /// <summary>
    /// 하나의 시설 유형에 속하는 최대 5개 슬롯을 관리하는 워크숍 카드 업데이터.
    /// </summary>
    public class WorkshopCardUpdater : MonoBehaviour
    {
        List<FacilitySlot> slots;
        // OnStateChanged 해제를 위해 구독한 델리게이트 인스턴스를 보관한다.
        // (-= 에 새 람다를 넘기면 서로 다른 인스턴스라 절대 해제되지 않는다)
        System.Action<CycleState> stateHandler;
        Image[]            slotBgs;
        TMP_Text[]         slotLabels;
        Image[]            slotIcons;
        TMP_Text           resourceCountTxt;
        Image              collectBtnBg;
        TMP_Text           collectCountTxt;
        float              nextLabelRefresh;

        static readonly Color ColEmpty    = new Color(0.20f, 0.22f, 0.28f);
        static readonly Color ColWorking  = new Color(0.25f, 0.75f, 0.40f);
        static readonly Color ColSleeping = new Color(0.85f, 0.50f, 0.10f);
        static readonly Color ColIdle     = new Color(0.50f, 0.50f, 0.60f);
        static readonly Color ColPaused   = new Color(0.35f, 0.38f, 0.55f);

        public void Init(List<FacilitySlot> facilitySlots,
                         Image[] bgs, TMP_Text[] labels,
                         Button[] btns, TMP_Text resTxt,
                         Image collectBg = null, TMP_Text collectCntTxt = null,
                         Image[] icons = null)
        {
            slots            = facilitySlots;
            slotBgs          = bgs;
            slotLabels       = labels;
            slotIcons        = icons;
            resourceCountTxt = resTxt;
            collectBtnBg     = collectBg;
            collectCountTxt  = collectCntTxt;

            stateHandler = _ => Refresh();
            for (int i = 0; i < slots.Count; i++)
            {
                int idx = i;
                slots[i].Cycle.OnStateChanged += stateHandler;
                if (btns != null && idx < btns.Length && btns[idx] != null)
                    btns[idx].onClick.AddListener(() => OnSlotTapped(idx));
            }

            if (FactoryManager.Instance != null)
                FactoryManager.Instance.OnInventoryChanged += Refresh;

            Refresh();
        }

        // Working 슬롯의 남은 활동 시간 텍스트를 60초마다 갱신
        void Update()
        {
            if (Time.time < nextLabelRefresh || slots == null) return;
            nextLabelRefresh = Time.time + 60f;
            foreach (var s in slots)
                if (s != null && s.Cycle.State == CycleState.Working) { Refresh(); return; }
        }

        void OnDestroy()
        {
            // FacilitySlot은 DontDestroyOnLoad라 씬을 나가도 살아 있다.
            // 해제하지 않으면 파괴된 이 컴포넌트의 Refresh()가 계속 호출된다.
            if (slots != null && stateHandler != null)
                foreach (var s in slots)
                    if (s != null) s.Cycle.OnStateChanged -= stateHandler;
            if (FactoryManager.Instance != null)
                FactoryManager.Instance.OnInventoryChanged -= Refresh;
        }

        public void Refresh()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool occupied = slot.AssignedCreature != null;

                if (slotBgs != null && i < slotBgs.Length && slotBgs[i] != null)
                    slotBgs[i].color = occupied
                        ? slot.Cycle.State switch
                        {
                            CycleState.Working  => ColWorking,
                            CycleState.Sleeping => ColSleeping,
                            CycleState.Paused   => ColPaused,
                            _                   => ColIdle
                        }
                        : ColEmpty;

                if (slotLabels != null && i < slotLabels.Length && slotLabels[i] != null)
                {
                    string state = occupied ? slot.Cycle.State switch
                    {
                        CycleState.Working  => FormatWork(slot.Cycle.WorkRemaining),
                        CycleState.Sleeping => HasAwakenItem ? "수면중\n탭=각성제" : "수면중",
                        CycleState.Paused   => "대기",
                        _                   => ""
                    } : "";

                    // 정면샷 아이콘 + 상태 (이름은 표시하지 않음 — 도감에서 확인)
                    var icon = slotIcons != null && i < slotIcons.Length ? slotIcons[i] : null;
                    CreatureSlotView.Apply(icon, slotLabels[i], slot.AssignedCreature, state, "생명체",
                                           Color.white, new Color(0.45f, 0.45f, 0.50f));
                }
            }

            if (slots.Count > 0 && FactoryManager.Instance != null)
            {
                var resType = slots[0].ActiveOutput;
                int count   = FactoryManager.Instance.Resources.TryGetValue(resType, out int v) ? v : 0;

                // 재고 상한 없음 — 쌓인 개수만 표시
                if (resourceCountTxt != null)
                    resourceCountTxt.text = $"{count}개";

                if (collectCountTxt != null)
                    collectCountTxt.text = count.ToString();

                if (collectBtnBg != null)
                    collectBtnBg.color = count > 0
                        ? new Color(0.14f, 0.50f, 0.32f)  // 보유 중 — 초록
                        : new Color(0.22f, 0.26f, 0.38f); // 비어 있음 — 카드보다 밝은 회청
            }
        }

        static bool HasAwakenItem =>
            FactoryManager.Instance != null && FactoryManager.Instance.TotalAwakenItems > 0;

        static string FormatWork(float sec)
        {
            int h = (int)(sec / 3600f);
            int m = (int)((sec % 3600f) / 60f);
            return h > 0 ? $"{h}시간 {m:D2}분" : $"{m}분";
        }

        void OnSlotTapped(int idx)
        {
            if (FactoryManager.Instance == null || idx >= slots.Count) return;
            var slot = slots[idx];

            if (slot.AssignedCreature != null)
            {
                // 수면 중이면 각성제를 소모해 재활성한다.
                // 각성제가 없을 때만 생명체를 회수한다.
                if (slot.Cycle.State == CycleState.Sleeping &&
                    FactoryManager.Instance.TryReactivateSlot(slot))
                    return;

                FactoryManager.Instance.UnassignCreatureFromSlot(slot);
                return;
            }

            CreaturePickerPopup.Show(slot);
        }
    }
}
