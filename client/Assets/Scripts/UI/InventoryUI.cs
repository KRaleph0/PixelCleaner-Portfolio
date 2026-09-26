using UnityEngine;
using TMPro;
using PixelCleaners;

namespace PixelCleaners.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] TMP_Text normalText;
        [SerializeField] TMP_Text advancedText;
        [SerializeField] TMP_Text ploggingText;

        void Start()
        {
            if (FactoryManager.Instance != null)
                FactoryManager.Instance.OnInventoryChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (FactoryManager.Instance != null)
                FactoryManager.Instance.OnInventoryChanged -= Refresh;
        }

        void Refresh()
        {
            if (FactoryManager.Instance == null) return;
            var items = FactoryManager.Instance.AwakenItems;
            normalText.text   = $"일반 각성제: {items[AwakenItemTier.Normal]}";
            advancedText.text = $"상급 각성제: {items[AwakenItemTier.Advanced]}";
            ploggingText.text = $"플로깅 각성제: {items[AwakenItemTier.Plogging]}";
        }
    }
}
