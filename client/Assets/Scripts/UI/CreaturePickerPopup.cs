using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

namespace PixelCleaners.UI
{
    public class CreaturePickerPopup : MonoBehaviour
    {
        // 목록 항목 높이 118 안에 들어가는 정면샷 크기
        const float IconSize = 96f;
        const float IconLeft = 12f;

        StatType                requiredStat;
        Action<CreatureInstance> onSelect;

        // 배치 가능 여부 (null = 누구나). 픽셀 재구성소는 제로픽셀만
        Func<CreatureInstance, bool> isAllowed;
        string                       notAllowedText;
        // 능력치 보너스가 붙는지 (기본 = 요구 스탯 일치)
        Func<CreatureInstance, bool> isCompatible;
        // 제작소: 한 마리의 사이클 배율 (레시피 미지정 시 단축 비율 표시)
        Func<CreatureInstance, float> cycleFraction;

        // 배치 미리보기: 슬롯이 실제로 쓰는 계산 함수를 그대로 받는다
        Func<CreatureInstance, float> previewCycle;   // null = 레시피 미지정 합성 슬롯
        float                         baseCycle;
        string                        cycleVerb;

        public static void Show(FacilitySlot targetSlot)
        {
            var go    = new GameObject("CreaturePickerPopup");
            var popup = go.AddComponent<CreaturePickerPopup>();
            popup.requiredStat = targetSlot.Definition.requiredStat;
            popup.previewCycle = targetSlot.PreviewCycleSeconds;
            popup.baseCycle    = targetSlot.BaseCycleSeconds;
            popup.cycleVerb    = "생산";
            popup.isCompatible = targetSlot.MatchesStat;
            if (targetSlot.Definition.pixelExclusiveOnly)
            {
                popup.isAllowed      = targetSlot.IsAllowed;
                popup.notAllowedText = "제로픽셀 전용 시설";
            }
            popup.onSelect     = c =>
            {
                if (FactoryManager.Instance == null) return;
                if (!FactoryManager.Instance.AssignCreatureToSlot(c, targetSlot))
                    FactoryManager.Instance.ForceAssignToSlot(c, targetSlot);
            };
            popup.Build();
        }

        public static void Show(SynthesisSlot synthSlot)
        {
            var go    = new GameObject("CreaturePickerPopup");
            var popup = go.AddComponent<CreaturePickerPopup>();
            popup.requiredStat  = synthSlot.RequiredStat;   // 일반 = 재구성력, 고급 = 합성력
            popup.cycleFraction = synthSlot.CycleFraction;
            if (synthSlot.Recipe != null)
            {
                popup.previewCycle = synthSlot.PreviewCycleSeconds;
                popup.baseCycle    = synthSlot.Recipe.craftCycleSeconds;
            }
            popup.cycleVerb    = "제작";
            popup.onSelect     = c => FactoryManager.Instance?.AssignCreatureToSynthesisSlot(c, synthSlot.Index);
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

            // 어두운 배경
            var dimGO  = MakeRT("Dim", transform).gameObject;
            FillRT(dimGO.GetComponent<RectTransform>());
            var dimImg = dimGO.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            var dimBtn = dimGO.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            dimBtn.onClick.AddListener(Close);

            // 팝업 패널
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

            // ── 헤더 ──────────────────────────────────────────────
            var hdr   = MakeRT("Header", panel).gameObject;
            hdr.AddComponent<LayoutElement>().preferredHeight = 84f;
            hdr.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.14f);

            var hdrVL = hdr.AddComponent<VerticalLayoutGroup>();
            hdrVL.padding                = new RectOffset(12, 12, 10, 10);
            hdrVL.spacing                = 4f;
            hdrVL.childControlWidth      = true;
            hdrVL.childForceExpandWidth  = true;
            hdrVL.childControlHeight     = true;
            hdrVL.childForceExpandHeight = false;

            var titleGO  = MakeRT("Title", hdr.transform).gameObject;
            titleGO.AddComponent<LayoutElement>().preferredHeight = 34f;
            var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
            titleTxt.text      = "생명체 선택";
            titleTxt.fontSize  = 24f;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color     = Color.white;
            titleTxt.alignment = TextAlignmentOptions.Center;
            FontProvider.Apply(titleTxt);

            var subGO  = MakeRT("Sub", hdr.transform).gameObject;
            subGO.AddComponent<LayoutElement>().preferredHeight = 26f;
            var subTxt = subGO.AddComponent<TextMeshProUGUI>();
            subTxt.text      = (isAllowed != null ? "제로픽셀 전용  ·  " : $"요구 스탯: {StatKorName(requiredStat)}  ·  ") +
                               (previewCycle != null ? $"기본 {baseCycle:F0}초" : "레시피 미지정");
            subTxt.fontSize  = 17f;
            subTxt.color     = new Color(0.60f, 0.85f, 1f);
            subTxt.alignment = TextAlignmentOptions.Center;
            FontProvider.Apply(subTxt);

            // ── 스크롤 리스트 ──────────────────────────────────────
            var scrollGO = MakeRT("Scroll", panel).gameObject;
            var scrollLE = scrollGO.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

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
            var contentVLG = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentVLG.spacing                = 6f;
            contentVLG.padding                = new RectOffset(0, 0, 4, 4);
            contentVLG.childControlWidth      = true;
            contentVLG.childControlHeight     = true;
            contentVLG.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport   = vp;
            scrollRect.content    = content;
            scrollRect.horizontal = false;

            var inv = FactoryManager.Instance?.CreatureInventory;
            if (inv == null || inv.Count == 0)
            {
                var emGO  = MakeRT("Empty", content).gameObject;
                emGO.AddComponent<LayoutElement>().preferredHeight = 100f;
                var emTxt = emGO.AddComponent<TextMeshProUGUI>();
                emTxt.text      = "보유한 생명체가 없습니다";
                emTxt.fontSize  = 20f;
                emTxt.alignment = TextAlignmentOptions.Center;
                emTxt.color     = new Color(0.55f, 0.55f, 0.60f);
                FontProvider.Apply(emTxt);
            }
            else
            {
                // 배치 가능 → 요구 스탯이 맞는 생명체 먼저, 그 안에서 능력치 높은 순
                var sorted = new List<CreatureInstance>(inv);
                sorted.Sort((a, b) =>
                {
                    int ca = (Allowed(a) ? 0 : 2) + (Compatible(a) ? 0 : 1);
                    int cb = (Allowed(b) ? 0 : 2) + (Compatible(b) ? 0 : 1);
                    return ca != cb ? ca.CompareTo(cb) : b.GetStatPower().CompareTo(a.GetStatPower());
                });
                foreach (var c in sorted)
                    BuildCreatureItem(content, c);
            }

            // ── 취소 버튼 ─────────────────────────────────────────
            var cancelGO  = MakeRT("Cancel", panel).gameObject;
            cancelGO.AddComponent<LayoutElement>().preferredHeight = 74f;
            cancelGO.AddComponent<Image>().color = new Color(0.52f, 0.10f, 0.08f);
            var cancelBtn = cancelGO.AddComponent<Button>();
            cancelBtn.onClick.AddListener(Close);

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

        bool Allowed(CreatureInstance c)    => isAllowed == null || isAllowed(c);
        bool Compatible(CreatureInstance c) => isCompatible != null ? isCompatible(c)
                                                                    : c.definition.specialStat == requiredStat;

        void BuildCreatureItem(RectTransform parent, CreatureInstance creature)
        {
            bool allowed = Allowed(creature);
            bool compat  = allowed && Compatible(creature);

            var itemGO  = MakeRT($"Item_{creature.uniqueId}", parent).gameObject;
            var itemLE  = itemGO.AddComponent<LayoutElement>();
            itemLE.preferredHeight = 118f;

            var itemImg = itemGO.AddComponent<Image>();
            itemImg.color = compat
                ? new Color(0.14f, 0.18f, 0.27f)
                : new Color(0.11f, 0.12f, 0.17f);

            var sep = itemGO.AddComponent<Outline>();
            sep.effectColor    = new Color(0.25f, 0.28f, 0.38f, 0.5f);
            sep.effectDistance = new Vector2(0f, -1f);

            var itemBtn = itemGO.AddComponent<Button>();
            itemBtn.targetGraphic = itemImg;
            itemBtn.interactable  = allowed;   // 배치할 수 없는 생명체는 목록에만 보인다
            var captured = creature;
            itemBtn.onClick.AddListener(() => SelectCreature(captured));

            // 정면샷 (아이콘이 없으면 예전처럼 희귀도 원형 + 등급)
            var iconFrame = MakeRT("IconFrame", itemGO.transform);
            iconFrame.anchorMin        = new Vector2(0f, 0.5f);
            iconFrame.anchorMax        = new Vector2(0f, 0.5f);
            iconFrame.pivot            = new Vector2(0f, 0.5f);
            iconFrame.anchoredPosition = new Vector2(IconLeft, 0f);
            iconFrame.sizeDelta        = new Vector2(IconSize, IconSize);
            var frameImg = iconFrame.gameObject.AddComponent<Image>();
            frameImg.color         = new Color(0.07f, 0.09f, 0.14f);
            frameImg.raycastTarget = false;

            var sprite = creature.definition.icon;
            if (sprite == null)
                sprite = CreatureRoster.LoadIcon(CreatureRoster.Find(creature.definition.name, creature.definition.purifiedName));

            if (sprite != null)
            {
                var iconRT = MakeRT("Icon", iconFrame);
                FillRT(iconRT);
                iconRT.offsetMin = new Vector2(4f, 4f);
                iconRT.offsetMax = new Vector2(-4f, -4f);
                var iconImg = iconRT.gameObject.AddComponent<Image>();
                iconImg.sprite         = sprite;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget  = false;
                // 요구 스탯이 안 맞으면 흐리게
                iconImg.color = compat ? Color.white : new Color(1f, 1f, 1f, 0.40f);
            }

            // 등급 배지: 아이콘이 있으면 오른쪽 아래 작은 원, 없으면 가운데 큰 원
            var badge = MakeRT("Grade", iconFrame);
            if (sprite != null)
            {
                badge.anchorMin = badge.anchorMax = new Vector2(1f, 0f);
                badge.pivot            = new Vector2(1f, 0f);
                badge.anchoredPosition = new Vector2(4f, -4f);
                badge.sizeDelta        = new Vector2(34f, 34f);
            }
            else
            {
                badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0.5f, 0.5f);
                badge.anchoredPosition = Vector2.zero;
                badge.sizeDelta        = new Vector2(58f, 58f);
            }
            var badgeImg = badge.gameObject.AddComponent<Image>();
            badgeImg.sprite        = MakeCircleSprite(40);
            badgeImg.color         = RarityColor(creature.definition.rarity);
            badgeImg.raycastTarget = false;

            var gradeGO  = MakeRT("Label", badge).gameObject;
            FillRT(gradeGO.GetComponent<RectTransform>());
            var gradeTxt = gradeGO.AddComponent<TextMeshProUGUI>();
            gradeTxt.text          = GradeLabel(creature.grade);
            gradeTxt.fontSize      = sprite != null ? 15f : 18f;
            gradeTxt.fontStyle     = FontStyles.Bold;
            gradeTxt.alignment     = TextAlignmentOptions.Center;
            gradeTxt.color         = Color.white;
            gradeTxt.raycastTarget = false;
            FontProvider.Apply(gradeTxt);

            // 호환 표시
            var compatGO  = MakeRT("Compat", itemGO.transform).gameObject;
            var compatRT  = compatGO.GetComponent<RectTransform>();
            compatRT.anchorMin        = new Vector2(1f, 0.5f);
            compatRT.anchorMax        = new Vector2(1f, 0.5f);
            compatRT.pivot            = new Vector2(1f, 0.5f);
            compatRT.anchoredPosition = new Vector2(-14f, 0f);
            compatRT.sizeDelta        = new Vector2(40f, 40f);
            var compatTxt = compatGO.AddComponent<TextMeshProUGUI>();
            compatTxt.text      = compat ? "✓" : "—";
            compatTxt.fontSize  = 26f;
            compatTxt.fontStyle = compat ? FontStyles.Bold : FontStyles.Normal;
            compatTxt.alignment = TextAlignmentOptions.Center;
            compatTxt.color     = compat
                ? new Color(0.20f, 1f, 0.42f)
                : new Color(0.35f, 0.35f, 0.40f);
            FontProvider.Apply(compatTxt);

            // 이름 + 스탯
            var infoGO = MakeRT("Info", itemGO.transform).gameObject;
            var infoRT = infoGO.GetComponent<RectTransform>();
            infoRT.anchorMin = Vector2.zero;
            infoRT.anchorMax = Vector2.one;
            infoRT.offsetMin = new Vector2(IconLeft + IconSize + 16f, 8f);
            infoRT.offsetMax = new Vector2(-60f, -8f);

            var infoVL = infoGO.AddComponent<VerticalLayoutGroup>();
            infoVL.childControlWidth      = true;
            infoVL.childForceExpandWidth  = true;
            infoVL.childControlHeight     = true;
            infoVL.childForceExpandHeight = false;
            infoVL.spacing                = 4f;
            infoVL.childAlignment         = TextAnchor.MiddleLeft;

            var nameGO  = MakeRT("Name", infoGO.transform).gameObject;
            nameGO.AddComponent<LayoutElement>().preferredHeight = 30f;
            var nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
            nameTxt.text      = creature.definition.purifiedName;
            nameTxt.fontSize  = 20f;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.color     = compat ? Color.white : new Color(0.58f, 0.58f, 0.62f);
            nameTxt.alignment = TextAlignmentOptions.Left;
            FontProvider.Apply(nameTxt);

            var statGO  = MakeRT("Stat", infoGO.transform).gameObject;
            statGO.AddComponent<LayoutElement>().preferredHeight = 22f;
            var statTxt = statGO.AddComponent<TextMeshProUGUI>();
            string specialty  = creature.definition.hasSpecialty
                ? $"  ·  {FactorySceneSetup.ResourceKorName(creature.definition.specialtyResource)} 특화"
                : "";
            statTxt.text      = $"{StatKorName(creature.definition.specialStat)}  ·  " +
                                $"{RarityKorName(creature.definition.rarity)}  ·  {TierLabel(creature.definition.tier)}{specialty}";
            statTxt.fontSize  = 15f;
            statTxt.color     = compat
                ? new Color(0.42f, 0.84f, 0.58f)
                : new Color(0.42f, 0.42f, 0.48f);
            statTxt.alignment = TextAlignmentOptions.Left;
            FontProvider.Apply(statTxt);

            // 능력치 + 이 슬롯에서의 사이클
            var powerGO  = MakeRT("Power", infoGO.transform).gameObject;
            powerGO.AddComponent<LayoutElement>().preferredHeight = 24f;
            var powerTxt = powerGO.AddComponent<TextMeshProUGUI>();
            powerTxt.text      = PowerLine(creature);
            powerTxt.fontSize  = 16f;
            powerTxt.fontStyle = FontStyles.Bold;
            powerTxt.richText  = true;
            powerTxt.color     = compat
                ? new Color(0.78f, 0.88f, 1f)
                : new Color(0.45f, 0.45f, 0.52f);
            powerTxt.alignment = TextAlignmentOptions.Left;
            FontProvider.Apply(powerTxt);
        }

        // 예: "능력치 4 (+2 티어)  ·  생산 9.8초 (-34%)"
        //     "능력치 4  ·  스탯 불일치 — 보너스 없음  ·  생산 15.0초"
        string PowerLine(CreatureInstance creature)
        {
            int power = creature.GetStatPower();

            if (!Allowed(creature))
                return $"<color=#E0785A>{notAllowedText} — 배치 불가</color>";

            if (!Compatible(creature))
            {
                string baseTxt = previewCycle != null && baseCycle > 0f
                    ? $"  ·  {cycleVerb} {previewCycle(creature):F1}초"
                    : "";
                return $"능력치 {power}  ·  <color=#E0785A>스탯 불일치 — 보너스 없음</color>{baseTxt}";
            }

            int tierBonus = CreatureInstance.TierBonus(creature.definition.tier);
            string bonus  = tierBonus > 0 ? $" <color=#FFC94A>(+{tierBonus} 티어)</color>" : "";

            string cycle;
            if (previewCycle != null && baseCycle > 0f)
            {
                float sec = previewCycle(creature);
                int   pct = Mathf.RoundToInt((1f - sec / baseCycle) * 100f);
                cycle = $"  ·  {cycleVerb} {sec:F1}초 <color=#8FA3BF>(-{pct}%)</color>";
            }
            else
            {
                // 레시피가 아직 없는 합성 슬롯 — 초 대신 단축 비율만 표시
                float frac = cycleFraction != null ? cycleFraction(creature) : 1f;
                int pct = Mathf.RoundToInt((1f - frac) * 100f);
                cycle = $"  ·  {cycleVerb} 시간 <color=#8FA3BF>-{pct}%</color>";
            }
            return $"능력치 {power}{bonus}{cycle}";
        }

        void SelectCreature(CreatureInstance creature)
        {
            onSelect?.Invoke(creature);
            Close();
        }

        void Close() => Destroy(gameObject);

        // ── 헬퍼 ────────────────────────────────────────────────────

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

        static Sprite MakeCircleSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float h = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - h + 0.5f) * (x - h + 0.5f) + (y - h + 0.5f) * (y - h + 0.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(h - d)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Color RarityColor(CreatureRarity r) => r switch
        {
            CreatureRarity.Common    => new Color(0.38f, 0.86f, 0.38f),
            CreatureRarity.Rare      => new Color(0.28f, 0.50f, 1.00f),
            CreatureRarity.Epic      => new Color(0.70f, 0.28f, 1.00f),
            CreatureRarity.Legendary => new Color(1.00f, 0.72f, 0.08f),
            CreatureRarity.Pixel     => Color.white,
            _                        => Color.gray
        };

        static string GradeLabel(int g) => g switch { 0 => "C", 1 => "B", 2 => "A", _ => "?" };

        static string TierLabel(int tier) => tier switch { 1 => "1티어", 2 => "2티어", 0 => "특수", _ => $"{tier}티어" };

        static string StatKorName(StatType s) => s switch
        {
            StatType.PollutionDetection => "오염 감지",
            StatType.Dissolution        => "용해",
            StatType.Forging            => "단조",
            StatType.Compression        => "압축",
            StatType.Reconstruction     => "재구성",
            StatType.Synthesis          => "합성",
            StatType.Special            => "특수",
            _                           => s.ToString()
        };

        static string RarityKorName(CreatureRarity r) => r switch
        {
            CreatureRarity.Common    => "일반",
            CreatureRarity.Rare      => "희귀",
            CreatureRarity.Epic      => "에픽",
            CreatureRarity.Legendary => "전설",
            CreatureRarity.Pixel     => "픽셀",
            _                        => r.ToString()
        };
    }
}
