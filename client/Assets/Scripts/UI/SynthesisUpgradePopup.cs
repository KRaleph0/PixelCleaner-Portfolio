using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

namespace PixelCleaners.UI
{
    /// <summary>
    /// 합성·고급 제작소 확장 팝업. 다음 레벨 비용(창고 보유량 대비)을 보여주고 확장한다.
    /// SynthesisUpgradePopup.Show(slot, title) 으로 열고, 확장·취소 시 자기 파괴.
    /// </summary>
    public class SynthesisUpgradePopup : MonoBehaviour
    {
        SynthesisSlot slot;
        string        title;

        static readonly Color ColHave    = new Color(0.30f, 0.90f, 0.52f);
        static readonly Color ColLack    = new Color(0.92f, 0.36f, 0.30f);
        static readonly Color ColPanel   = new Color(0.09f, 0.12f, 0.18f, 0.98f);
        static readonly Color ColSub     = new Color(0.60f, 0.72f, 0.90f);

        public static void Show(SynthesisSlot target, string facilityTitle)
        {
            if (target == null || !target.CanUpgrade) return;
            var go    = new GameObject("SynthesisUpgradePopup");
            var popup = go.AddComponent<SynthesisUpgradePopup>();
            popup.slot  = target;
            popup.title = facilityTitle;
            popup.Build();
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // 어두운 배경 — 누르면 닫힘
            var dim = Rect("Dim", transform);
            Fill(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);

            var panel = Rect("Panel", transform);
            panel.anchorMin = new Vector2(0.08f, 0.28f);
            panel.anchorMax = new Vector2(0.92f, 0.72f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = ColPanel;

            var vl = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding                = new RectOffset(28, 28, 26, 26);
            vl.spacing                = 12f;
            vl.childControlWidth      = true;
            vl.childForceExpandWidth  = true;
            vl.childControlHeight     = true;
            vl.childForceExpandHeight = false;

            int next = slot.Level + 1;
            Line(panel, $"{title} 확장", 32f, Color.white, FontStyles.Bold, 48f);
            Line(panel, $"Lv{slot.Level} → Lv{next}   ·   생명체 {slot.Level}마리 → {next}마리",
                 21f, ColSub, FontStyles.Normal, 34f);
            Line(panel, "필요 재료 (창고에서 차감)", 19f, new Color(0.55f, 0.58f, 0.68f), FontStyles.Normal, 40f);

            // 재료 목록 — 보유/필요
            var cost = SynthesisUpgrade.CostFor(slot.Level);
            var wh   = FactoryManager.Instance != null ? FactoryManager.Instance.Warehouse : null;
            if (cost != null)
            {
                foreach (var ing in cost)
                {
                    int have = 0;
                    if (wh != null) wh.TryGetValue(ing.type, out have);
                    bool ok = have >= ing.amount;

                    var row = Rect("Cost", panel);
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;
                    row.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.22f);

                    var nameRT = Rect("Name", row);
                    nameRT.anchorMin = Vector2.zero; nameRT.anchorMax = new Vector2(0.6f, 1f);
                    nameRT.offsetMin = new Vector2(18f, 0f); nameRT.offsetMax = Vector2.zero;
                    Text(nameRT, FactorySceneSetup.ResourceKorName(ing.type), 22f, Color.white,
                         FontStyles.Normal, TextAlignmentOptions.MidlineLeft);

                    var cntRT = Rect("Count", row);
                    cntRT.anchorMin = new Vector2(0.6f, 0f); cntRT.anchorMax = Vector2.one;
                    cntRT.offsetMin = Vector2.zero; cntRT.offsetMax = new Vector2(-18f, 0f);
                    Text(cntRT, $"{have} / {ing.amount}", 22f, ok ? ColHave : ColLack,
                         FontStyles.Bold, TextAlignmentOptions.MidlineRight);
                }
            }

            // 여백
            var spacer = Rect("Spacer", panel);
            spacer.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            // 버튼
            bool affordable = FactoryManager.Instance != null &&
                              FactoryManager.Instance.CanAffordSynthesisUpgrade(slot.Index);

            var buttons = Rect("Buttons", panel);
            buttons.gameObject.AddComponent<LayoutElement>().preferredHeight = 76f;
            var hl = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14f;
            hl.childControlWidth = hl.childControlHeight = true;
            hl.childForceExpandWidth = hl.childForceExpandHeight = true;

            MakeButton(buttons, "취소", new Color(0.30f, 0.32f, 0.40f), true, Close);
            MakeButton(buttons, affordable ? "확장" : "재료 부족",
                       affordable ? new Color(0.18f, 0.55f, 0.30f) : new Color(0.20f, 0.22f, 0.26f),
                       affordable, OnUpgrade);
        }

        void OnUpgrade()
        {
            if (FactoryManager.Instance != null)
                FactoryManager.Instance.TryUpgradeSynthesisSlot(slot.Index);
            Close();
        }

        void Close() => Destroy(gameObject);

        // ── 헬퍼 ────────────────────────────────────────────────────

        static void MakeButton(RectTransform parent, string label, Color color, bool interactable,
                               UnityEngine.Events.UnityAction onClick)
        {
            var rt  = Rect("Button", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable  = interactable;
            btn.onClick.AddListener(onClick);
            Text(rt, label, 26f, interactable ? Color.white : new Color(0.55f, 0.55f, 0.60f),
                 FontStyles.Bold, TextAlignmentOptions.Center);
        }

        static void Line(RectTransform parent, string text, float size, Color color, FontStyles style, float height)
        {
            var rt = Rect("Line", parent);
            rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            Text(rt, text, size, color, style, TextAlignmentOptions.Center);
        }

        static void Text(RectTransform parent, string text, float size, Color color, FontStyles style,
                         TextAlignmentOptions align)
        {
            var rt = Rect("Text", parent);
            Fill(rt);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.fontStyle     = style;
            tmp.color         = color;
            tmp.alignment     = align;
            tmp.raycastTarget = false;
            FontProvider.Apply(tmp);
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
