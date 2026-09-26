using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

namespace PixelCleaners.UI
{
    /// <summary>
    /// 합성 슬롯에 레시피를 지정하는 팝업.
    /// RecipePickerPopup.Show(slot) 으로 열기, 선택 또는 취소 시 자기 파괴.
    /// </summary>
    public class RecipePickerPopup : MonoBehaviour
    {
        SynthesisSlot targetSlot;

        RecipeCategory category;

        public static void Show(SynthesisSlot slot, RecipeCategory cat)
        {
            var go = new GameObject("RecipePickerPopup");
            var popup = go.AddComponent<RecipePickerPopup>();
            popup.category = cat;
            popup.Build(slot);
        }

        void Build(SynthesisSlot slot)
        {
            targetSlot = slot;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // 어두운 배경
            var dimRT  = MakeRT("Dim", transform);
            FillRT(dimRT);
            var dimImg = dimRT.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            var dimBtn = dimRT.gameObject.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(Close);

            // 패널
            var panel = MakeRT("Panel", transform);
            panel.anchorMin = new Vector2(0.04f, 0.07f);
            panel.anchorMax = new Vector2(0.96f, 0.93f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.12f, 0.18f, 0.98f);

            var vl = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.padding                = new RectOffset(16, 16, 16, 16);
            vl.spacing                = 10f;
            vl.childControlWidth      = true;
            vl.childForceExpandWidth  = true;
            vl.childControlHeight     = true;
            vl.childForceExpandHeight = false;

            // 헤더
            var hdrGO = MakeRT("Header", panel).gameObject;
            hdrGO.AddComponent<LayoutElement>().preferredHeight = 74f;
            hdrGO.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f);
            var hdrVL = hdrGO.AddComponent<VerticalLayoutGroup>();
            hdrVL.padding                = new RectOffset(12, 12, 12, 12);
            hdrVL.spacing                = 4f;
            hdrVL.childControlWidth      = true;
            hdrVL.childForceExpandWidth  = true;
            hdrVL.childControlHeight     = true;
            hdrVL.childForceExpandHeight = false;

            var titleGO  = MakeRT("Title", hdrGO.transform).gameObject;
            titleGO.AddComponent<LayoutElement>().preferredHeight = 34f;
            var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
            titleTxt.text      = $"레시피 선택 — 슬롯 {slot.Index + 1}";
            titleTxt.fontSize  = 22f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color     = Color.white;
            titleTxt.alignment = TextAlignmentOptions.Center;
            FontProvider.Apply(titleTxt);

            var subGO  = MakeRT("Sub", hdrGO.transform).gameObject;
            subGO.AddComponent<LayoutElement>().preferredHeight = 22f;
            var subTxt = subGO.AddComponent<TextMeshProUGUI>();
            subTxt.text      = "레시피 선택 시 재료 충족 시 자동 제작됩니다";
            subTxt.fontSize  = 15f;
            subTxt.color     = new Color(0.55f, 0.75f, 0.95f);
            subTxt.alignment = TextAlignmentOptions.Center;
            FontProvider.Apply(subTxt);

            // 스크롤 리스트
            var scrollGO = MakeRT("Scroll", panel).gameObject;
            scrollGO.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.12f);

            var vp = MakeRT("Viewport", scrollGO.transform);
            FillRT(vp);
            vp.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            vp.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = MakeRT("Content", vp);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot     = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var cvl = content.gameObject.AddComponent<VerticalLayoutGroup>();
            cvl.spacing                = 8f;
            cvl.padding                = new RectOffset(0, 0, 4, 4);
            cvl.childControlWidth      = true;
            cvl.childControlHeight     = true;
            cvl.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport   = vp;
            scrollRect.content    = content;
            scrollRect.horizontal = false;

            if (slot.Recipe != null)
                BuildClearItem(content);

            foreach (var r in RecipeBook.ForCategory(category))
                BuildRecipeItem(content, r);

            // 닫기 버튼
            var cancelGO  = MakeRT("Cancel", panel).gameObject;
            cancelGO.AddComponent<LayoutElement>().preferredHeight = 74f;
            cancelGO.AddComponent<Image>().color = new Color(0.52f, 0.10f, 0.08f);
            cancelGO.AddComponent<Button>().onClick.AddListener(Close);
            var cancelLbl = MakeRT("Label", cancelGO.transform).gameObject;
            FillRT(cancelLbl.GetComponent<RectTransform>());
            var cancelTxt = cancelLbl.AddComponent<TextMeshProUGUI>();
            cancelTxt.text      = "취소";
            cancelTxt.fontSize  = 22f;
            cancelTxt.fontStyle = FontStyles.Bold;
            cancelTxt.alignment = TextAlignmentOptions.Center;
            cancelTxt.color     = Color.white;
            FontProvider.Apply(cancelTxt);
        }

        void BuildClearItem(RectTransform parent)
        {
            var itemGO  = MakeRT("Clear", parent).gameObject;
            itemGO.AddComponent<LayoutElement>().preferredHeight = 64f;
            var itemImg = itemGO.AddComponent<Image>();
            itemImg.color = new Color(0.18f, 0.10f, 0.10f);

            var btn = itemGO.AddComponent<Button>();
            btn.targetGraphic = itemImg;
            btn.onClick.AddListener(() => { targetSlot.ClearRecipe(); Close(); });

            var lbl = MakeRT("Label", itemGO.transform).gameObject;
            FillRT(lbl.GetComponent<RectTransform>());
            var txt = lbl.AddComponent<TextMeshProUGUI>();
            txt.text      = "레시피 해제 (슬롯 비우기)";
            txt.fontSize  = 18f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color     = new Color(1f, 0.55f, 0.55f);
            FontProvider.Apply(txt);
        }

        void BuildRecipeItem(RectTransform parent, Recipe recipe)
        {
            bool hasMaterials = CheckMaterials(recipe);
            bool isSelected   = targetSlot.Recipe == recipe;

            // 카드 높이: 상단행(36) + 재료 행(26×n) + padding+spacing
            float itemH = 16f + 36f + 8f + recipe.ingredients.Count * 26f + 8f + 16f;

            var itemGO  = MakeRT($"Recipe_{recipe.name}", parent).gameObject;
            itemGO.AddComponent<LayoutElement>().preferredHeight = itemH;
            var itemImg = itemGO.AddComponent<Image>();
            itemImg.color = isSelected
                ? new Color(0.12f, 0.22f, 0.16f)
                : new Color(0.12f, 0.15f, 0.22f);

            var outline = itemGO.AddComponent<Outline>();
            outline.effectColor    = isSelected
                ? new Color(0.25f, 0.80f, 0.45f, 0.70f)
                : new Color(0.30f, 0.35f, 0.50f, 0.40f);
            outline.effectDistance = new Vector2(1.5f, 1.5f);

            var btn = itemGO.AddComponent<Button>();
            btn.targetGraphic = itemImg;
            var captured = recipe;
            btn.onClick.AddListener(() => { targetSlot.AssignRecipe(captured); Close(); });

            var itemVL = itemGO.AddComponent<VerticalLayoutGroup>();
            itemVL.padding                = new RectOffset(16, 16, 16, 16);
            itemVL.spacing                = 8f;
            itemVL.childControlWidth      = true;
            itemVL.childForceExpandWidth  = true;
            itemVL.childControlHeight     = false;
            itemVL.childForceExpandHeight = false;

            // 이름 행 (출력물 태그 포함)
            var nameRowGO = MakeRT("NameRow", itemGO.transform).gameObject;
            nameRowGO.AddComponent<LayoutElement>().preferredHeight = 36f;
            var nameHL = nameRowGO.AddComponent<HorizontalLayoutGroup>();
            nameHL.childControlWidth      = true;
            nameHL.childForceExpandWidth  = false;
            nameHL.childControlHeight     = true;
            nameHL.childForceExpandHeight = true;

            var nameGO  = MakeRT("Name", nameRowGO.transform).gameObject;
            nameGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
            nameTxt.text      = (isSelected ? "▶ " : "") + recipe.name;
            nameTxt.fontSize  = 20f;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.color     = isSelected ? new Color(0.35f, 1f, 0.60f) : Color.white;
            nameTxt.alignment = TextAlignmentOptions.Left;
            FontProvider.Apply(nameTxt);

            // 시간 태그
            var timeGO  = MakeRT("Time", nameRowGO.transform).gameObject;
            timeGO.AddComponent<LayoutElement>().preferredWidth = 100f;
            var timeTxt = timeGO.AddComponent<TextMeshProUGUI>();
            timeTxt.text      = FormatTime(recipe.craftCycleSeconds);
            timeTxt.fontSize  = 15f;
            timeTxt.color     = new Color(0.55f, 0.75f, 1.00f);
            timeTxt.alignment = TextAlignmentOptions.Right;
            FontProvider.Apply(timeTxt);

            // 재료 행
            foreach (var ing in recipe.ingredients)
            {
                wh_TryGet(ing.type, out int have);
                bool enough = have >= ing.amount;

                var ingGO  = MakeRT($"Ing_{ing.type}", itemGO.transform).gameObject;
                ingGO.AddComponent<LayoutElement>().preferredHeight = 26f;
                var ingHL = ingGO.AddComponent<HorizontalLayoutGroup>();
                ingHL.childControlWidth      = true;
                ingHL.childForceExpandWidth  = false;
                ingHL.childControlHeight     = true;
                ingHL.childForceExpandHeight = true;
                ingHL.spacing                = 6f;

                var dotGO  = MakeRT("Dot", ingGO.transform).gameObject;
                dotGO.AddComponent<LayoutElement>().preferredWidth = 12f;
                var dotTxt = dotGO.AddComponent<TextMeshProUGUI>();
                dotTxt.text      = "·";
                dotTxt.fontSize  = 16f;
                dotTxt.color     = new Color(0.45f, 0.45f, 0.55f);
                dotTxt.alignment = TextAlignmentOptions.Center;
                FontProvider.Apply(dotTxt);

                var ingNameGO  = MakeRT("IngName", ingGO.transform).gameObject;
                ingNameGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var ingNameTxt = ingNameGO.AddComponent<TextMeshProUGUI>();
                ingNameTxt.text      = ResourceKorName(ing.type);
                ingNameTxt.fontSize  = 16f;
                ingNameTxt.color     = new Color(0.75f, 0.80f, 0.90f);
                ingNameTxt.alignment = TextAlignmentOptions.Left;
                FontProvider.Apply(ingNameTxt);

                var ingCntGO  = MakeRT("IngCnt", ingGO.transform).gameObject;
                ingCntGO.AddComponent<LayoutElement>().preferredWidth = 160f;
                var ingCntTxt = ingCntGO.AddComponent<TextMeshProUGUI>();
                ingCntTxt.text      = $"보유 {have} / 필요 {ing.amount}";
                ingCntTxt.fontSize  = 16f;
                ingCntTxt.color     = enough
                    ? new Color(0.25f, 0.90f, 0.50f)
                    : new Color(0.90f, 0.30f, 0.25f);
                ingCntTxt.alignment = TextAlignmentOptions.Right;
                FontProvider.Apply(ingCntTxt);
            }
        }

        bool CheckMaterials(Recipe recipe)
        {
            if (FactoryManager.Instance == null) return false;
            var wh = FactoryManager.Instance.Warehouse;
            foreach (var ing in recipe.ingredients)
            {
                wh.TryGetValue(ing.type, out int have);
                if (have < ing.amount) return false;
            }
            return true;
        }

        void wh_TryGet(ResourceType type, out int value)
        {
            value = 0;
            if (FactoryManager.Instance == null) return;
            FactoryManager.Instance.Warehouse.TryGetValue(type, out value);
        }

        void Close() => Destroy(gameObject);

        static string FormatTime(float sec)
        {
            int s = Mathf.RoundToInt(sec);
            return s >= 60 ? $"{s / 60}분 {s % 60:00}초" : $"{s}초";
        }

        static string ResourceKorName(ResourceType r) => r switch
        {
            ResourceType.Garbage           => "쓰레기",
            ResourceType.Plastic           => "플라스틱",
            ResourceType.Glass             => "유리",
            ResourceType.Metal             => "금속",
            ResourceType.Can               => "캔",
            ResourceType.Paper             => "종이",
            ResourceType.Textile           => "섬유",
            ResourceType.PixelFragment     => "픽셀 파편",
            ResourceType.RecycledComposite => "재생 복합재",
            ResourceType.RecycledAlloy     => "재생 합금",
            ResourceType.DeliveryItem         => "납품 물품",
            ResourceType.AdvancedDeliveryItem => "고급 납품 물품",
            _                              => r.ToString()
        };

        static RectTransform MakeRT(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void FillRT(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
