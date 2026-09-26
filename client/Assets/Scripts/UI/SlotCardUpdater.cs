using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;
using PixelUI; // ValueBar 사용

namespace PixelCleaners.UI
{
    /// <summary>
    /// 공장 슬롯 카드의 에그 이미지, 시간 텍스트, 생명체명을 갱신합니다.
    /// </summary>
    public class SlotCardUpdater : MonoBehaviour
    {
        FacilitySlot                  slot;
        Image                         eggImg;
        TMP_Text                      timeTxt;
        TMP_Text                      creatureTxt;
        UnityEngine.UI.Button         eggBtn;
        ValueBar                      progressBar;

        static readonly Color ColEmpty    = new Color(0.25f, 0.25f, 0.32f);
        static readonly Color ColWorking  = new Color(0.3f,  0.85f, 0.45f);
        static readonly Color ColSleeping = new Color(0.9f,  0.55f, 0.15f);
        static readonly Color ColIdle     = new Color(0.55f, 0.55f, 0.65f);

        public void Init(FacilitySlot s, Image egg, TMP_Text time, TMP_Text creature,
                         UnityEngine.UI.Button btn)
        {
            slot        = s;
            eggImg      = egg;
            timeTxt     = time;
            creatureTxt = creature;
            eggBtn      = btn;

            if (btn != null)
                btn.onClick.AddListener(OnEggTapped);

            slot.Cycle.OnStateChanged += _ => Refresh();
            Refresh();
        }

        public void SetProgressBar(ValueBar bar) => progressBar = bar;

        void OnDestroy()
        {
            if (slot != null)
                slot.Cycle.OnStateChanged -= _ => Refresh();
        }

        void Update()
        {
            if (slot == null || slot.Cycle.State != CycleState.Working) return;
            int secs = Mathf.CeilToInt(slot.Cycle.WorkRemaining);
            if (timeTxt != null) timeTxt.text = $"{secs / 60:00}:{secs % 60:00}";
            if (progressBar != null) progressBar.CurrentValue = slot.Cycle.WorkProgress * 100f;
        }

        public void Refresh()
        {
            if (slot == null) return;

            bool hasCreature = slot.AssignedCreature != null;

            // 에그 색상: 생명체 없으면 비어있음, 있으면 사이클 상태 반영
            if (eggImg != null)
            {
                eggImg.color = hasCreature
                    ? slot.Cycle.State switch
                    {
                        CycleState.Working  => ColWorking,
                        CycleState.Sleeping => ColSleeping,
                        _                   => ColIdle
                    }
                    : ColEmpty;
            }

            // 생명체명
            if (creatureTxt != null)
            {
                creatureTxt.text = hasCreature
                    ? slot.AssignedCreature.definition.purifiedName
                    : "비어있음\n(탭하여 배치)";
                creatureTxt.color = hasCreature ? Color.white : new Color(0.6f, 0.6f, 0.7f);
            }

            // 시간 텍스트
            if (timeTxt != null && slot.Cycle.State != CycleState.Working)
            {
                timeTxt.text = slot.Cycle.State switch
                {
                    CycleState.Sleeping => "수면중",
                    CycleState.Paused   => "가득참",
                    _ => hasCreature ? "준비" : "--:--"
                };
            }

            // 진행 바
            if (progressBar != null)
                progressBar.CurrentValue = slot.Cycle.WorkProgress * 100f;
        }

        void OnEggTapped()
        {
            if (FactoryManager.Instance == null) return;

            if (slot.AssignedCreature != null)
            {
                // 탭 → 배치 해제
                FactoryManager.Instance.UnassignCreatureFromSlot(slot);
                return;
            }

            // 탭 → 인벤토리에서 배치
            var inv = FactoryManager.Instance.CreatureInventory;
            if (inv.Count == 0) return;

            // 스탯 호환 생명체 우선
            foreach (var c in inv)
            {
                if (FactoryManager.Instance.AssignCreatureToSlot(c, slot)) return;
            }

            // 호환 없으면 강제 배치 (프로토타입)
            FactoryManager.Instance.ForceAssignToSlot(inv[0], slot);
        }
    }
}
