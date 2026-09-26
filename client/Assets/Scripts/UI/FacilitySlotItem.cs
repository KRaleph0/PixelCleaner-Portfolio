using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

namespace PixelCleaners.UI
{
    public class FacilitySlotItem : MonoBehaviour
    {
        [SerializeField] TMP_Text facilityNameText;
        [SerializeField] TMP_Text creatureNameText;
        [SerializeField] TMP_Text stateText;
        [SerializeField] TMP_Text timeRemainingText;
        [SerializeField] Slider progressBar;
        [SerializeField] Button reactivateButton;

        FacilitySlot slot;

        public void Bind(FacilitySlot facilitySlot)
        {
            if (slot != null)
                slot.Cycle.OnStateChanged -= OnStateChanged;

            slot = facilitySlot;
            facilityNameText.text = slot.Definition.facilityType.ToString();
            slot.Cycle.OnStateChanged += OnStateChanged;
            reactivateButton.onClick.AddListener(OnReactivateClicked);
            Refresh();
        }

        void OnDestroy()
        {
            if (slot != null)
                slot.Cycle.OnStateChanged -= OnStateChanged;
        }

        void OnStateChanged(CycleState _) => Refresh();

        void Update()
        {
            if (slot == null || slot.Cycle.State != CycleState.Working) return;
            progressBar.value = slot.Cycle.WorkProgress;
            int secs = Mathf.CeilToInt(slot.Cycle.WorkRemaining);
            timeRemainingText.text = $"{secs / 60:00}:{secs % 60:00}";
        }

        public void Refresh()
        {
            if (slot == null) return;
            var cycle = slot.Cycle;

            creatureNameText.text  = slot.AssignedCreature?.definition.purifiedName ?? "비어있음";
            stateText.text = cycle.State switch
            {
                CycleState.Working  => "활동 중",
                CycleState.Sleeping => "수면 중",
                CycleState.Paused   => "가득참",
                _                   => "유휴"
            };
            progressBar.value = cycle.WorkProgress;

            bool canReactivate = cycle.State == CycleState.Sleeping
                && FactoryManager.Instance != null
                && FactoryManager.Instance.AwakenItems[AwakenItemTier.Normal] > 0;
            reactivateButton.interactable = canReactivate;
        }

        void OnReactivateClicked()
        {
            if (slot == null || FactoryManager.Instance == null) return;
            FactoryManager.Instance.ReactivateSlot(slot, AwakenItemTier.Normal);
        }
    }
}
