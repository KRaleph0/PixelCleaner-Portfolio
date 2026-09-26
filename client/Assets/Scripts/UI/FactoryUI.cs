using System.Collections.Generic;
using UnityEngine;
using PixelCleaners;

namespace PixelCleaners.UI
{
    public class FactoryUI : MonoBehaviour
    {
        [SerializeField] Transform slotListParent;
        [SerializeField] GameObject slotItemPrefab;
        [SerializeField] List<FacilitySlot> facilitySlots = new();

        readonly List<FacilitySlotItem> slotItems = new();

        void Start()
        {
            BuildSlotList();

            if (FactoryManager.Instance != null)
                FactoryManager.Instance.OnInventoryChanged += RefreshAll;
        }

        void OnDestroy()
        {
            if (FactoryManager.Instance != null)
                FactoryManager.Instance.OnInventoryChanged -= RefreshAll;
        }

        void BuildSlotList()
        {
            foreach (Transform child in slotListParent)
                Destroy(child.gameObject);
            slotItems.Clear();

            foreach (var slot in facilitySlots)
            {
                var go = Instantiate(slotItemPrefab, slotListParent);
                var item = go.GetComponent<FacilitySlotItem>();
                item.Bind(slot);
                slotItems.Add(item);
            }
        }

        void RefreshAll()
        {
            foreach (var item in slotItems)
                item.Refresh();
        }
    }
}
