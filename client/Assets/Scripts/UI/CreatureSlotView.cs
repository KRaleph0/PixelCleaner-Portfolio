using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PixelCleaners.UI
{
    /// <summary>
    /// 공장의 생명체 슬롯(시설 슬롯·합성 카드) 공용 표시.
    /// 생명체가 있으면 정면샷 아이콘 + 하단 상태 글자, 비어 있으면 안내 글자만.
    /// 아이콘을 아직 촬영하지 않은 생명체는 이름 글자로 대체한다.
    /// </summary>
    public static class CreatureSlotView
    {
        const float IconBottom  = 0.30f;   // 아이콘은 칸의 위쪽 70%
        const float StateFont   = 11f;
        const float FullFont    = 13f;

        /// <summary>슬롯 칸 안에 아이콘 Image를 만든다. 처음엔 숨김, 탭은 슬롯 버튼이 받는다.</summary>
        public static Image CreateIcon(Transform slot)
        {
            var go = new GameObject("Icon");
            go.transform.SetParent(slot, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, IconBottom);
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 0f);
            rt.offsetMax = new Vector2(-4f, -4f);

            var img = go.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget  = false;
            img.enabled        = false;
            return img;
        }

        /// <param name="state">아이콘 아래 상태 글자 (예: "23시간 12분", "수면중"). 없으면 ""</param>
        /// <param name="emptyText">생명체가 없을 때 글자</param>
        public static void Apply(Image icon, TMP_Text label, CreatureInstance creature,
                                 string state, string emptyText,
                                 Color occupiedColor, Color emptyColor)
        {
            if (label == null) return;
            Sprite sprite = creature != null ? creature.definition.icon : null;
            bool showIcon = sprite != null && icon != null;

            if (icon != null)
            {
                icon.sprite  = sprite;
                icon.enabled = showIcon;
            }

            var rt = label.rectTransform;
            if (showIcon)
            {
                rt.anchorMin   = Vector2.zero;
                rt.anchorMax   = new Vector2(1f, IconBottom);
                rt.offsetMin   = new Vector2(2f, 2f);
                rt.offsetMax   = new Vector2(-2f, 0f);
                label.fontSize = StateFont;
                label.text     = state;
                label.color    = occupiedColor;
            }
            else
            {
                rt.anchorMin   = Vector2.zero;
                rt.anchorMax   = Vector2.one;
                rt.offsetMin   = new Vector2(4f, 4f);
                rt.offsetMax   = new Vector2(-4f, -4f);
                label.fontSize = FullFont;
                if (creature != null)
                {
                    // 아이콘 미촬영 → 이름으로 대체
                    string name = creature.definition.purifiedName;
                    label.text  = string.IsNullOrEmpty(state) ? name : $"{name}\n{state}";
                    label.color = occupiedColor;
                }
                else
                {
                    label.text  = emptyText;
                    label.color = emptyColor;
                }
            }
        }
    }
}
