using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

namespace PixelCleaners.UI
{
    /// <summary>
    /// 포획 성공 팝업 — 정화 / 제거 선택.
    /// 정면샷, 오염 상태 이름 → 정화 이름, 등급·희귀도, 능력치·티어·수치, 도감 신규 여부를 보여준다.
    /// ARSceneSetup이 캔버스에 붙이고 <see cref="Build"/>를 호출한다.
    /// </summary>
    public class CaptureUI : MonoBehaviour
    {
        public static CaptureUI Instance { get; private set; }

        GameObject root;
        Image      icon;
        TMP_Text   iconFallback;
        GameObject newBadge;
        TMP_Text   nameTxt;
        TMP_Text   purifiedTxt;
        Image      gradeBg;
        TMP_Text   gradeTxt;
        TMP_Text   rarityTxt;
        TMP_Text   statTxt;
        TMP_Text   facilityTxt;
        TMP_Text   powerTxt;
        TMP_Text   purifyLabel;

        Action onPurify;
        Action onDiscard;

        static readonly Color ColPanel = new Color(0.08f, 0.10f, 0.15f, 0.97f);
        static readonly Color ColSub   = new Color(0.60f, 0.70f, 0.85f);
        static readonly Color ColGold  = new Color(1.00f, 0.79f, 0.29f);

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── UI 구성 ─────────────────────────────────────────────────

        public void Build(Transform canvas)
        {
            root = Rect("CapturePopup", canvas).gameObject;
            Fill((RectTransform)root.transform);

            // 어두운 배경 — 반드시 정화/제거 중 하나를 골라야 하므로 눌러도 닫히지 않는다
            var dim = Rect("Dim", root.transform);
            Fill(dim);
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panel = Anchored("Panel", root.transform, 0.08f, 0.18f, 0.92f, 0.84f);
            panel.gameObject.AddComponent<Image>().color = ColPanel;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor    = new Color(0.35f, 0.55f, 0.90f, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);

            var title = Anchored("Title", panel, 0.05f, 0.92f, 0.95f, 0.99f);
            Text(title, "포획 성공!", 30f, new Color(0.35f, 1f, 0.55f), FontStyles.Bold);

            // 도감 신규 배지
            var badge = Anchored("NewBadge", panel, 0.03f, 0.84f, 0.38f, 0.91f);
            badge.gameObject.AddComponent<Image>().color = new Color(0.55f, 0.38f, 0.05f, 0.95f);
            Text(badge, "NEW · 도감 등록", 19f, ColGold, FontStyles.Bold);
            newBadge = badge.gameObject;

            // 정면샷
            var iconArea = Anchored("Icon", panel, 0.25f, 0.53f, 0.75f, 0.90f);
            icon = iconArea.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget  = false;
            iconFallback = Text(iconArea, "?", 120f, ColSub, FontStyles.Bold);

            // 등급 배지 (희귀도 색 원 + C/B/A)
            var grade = Rect("Grade", iconArea);
            grade.anchorMin = grade.anchorMax = grade.pivot = new Vector2(1f, 0f);
            grade.anchoredPosition = new Vector2(10f, 0f);
            grade.sizeDelta        = new Vector2(64f, 64f);
            gradeBg = grade.gameObject.AddComponent<Image>();
            gradeBg.sprite = CircleSprite(48);
            gradeTxt = Text(grade, "C", 30f, Color.white, FontStyles.Bold);

            nameTxt     = Text(Anchored("Name",     panel, 0.04f, 0.45f, 0.96f, 0.53f), "", 36f, Color.white, FontStyles.Bold);
            purifiedTxt = Text(Anchored("Purified", panel, 0.04f, 0.40f, 0.96f, 0.45f), "", 20f, ColSub);
            rarityTxt   = Text(Anchored("Rarity",   panel, 0.04f, 0.345f, 0.96f, 0.40f), "", 21f, Color.white);
            statTxt     = Text(Anchored("Stat",     panel, 0.04f, 0.29f, 0.96f, 0.345f), "", 24f, Color.white, FontStyles.Bold);
            facilityTxt = Text(Anchored("Facility", panel, 0.04f, 0.245f, 0.96f, 0.29f), "", 19f, ColSub);
            powerTxt    = Text(Anchored("Power",    panel, 0.04f, 0.20f, 0.96f, 0.245f), "", 20f, Color.white);

            purifyLabel = MakeButton(panel, 0.04f, 0.03f, 0.49f, 0.18f, new Color(0.18f, 0.62f, 0.32f), OnPurifyClicked);
            var discard = MakeButton(panel, 0.51f, 0.03f, 0.96f, 0.18f, new Color(0.72f, 0.24f, 0.20f), OnDiscardClicked);
            discard.text = "제거\n<size=65%>각성제 +1</size>";

            root.SetActive(false);
        }

        // ── 표시 ────────────────────────────────────────────────────

        public void ShowCaptureChoice(CreatureInstance creature, Action purifyCallback, Action discardCallback)
        {
            if (root == null) return;
            onPurify  = purifyCallback;
            onDiscard = discardCallback;

            var def   = creature.definition;
            var entry = CreatureRoster.Find(def.name, def.purifiedName);

            // 정면샷
            var sprite = def.icon != null ? def.icon : CreatureRoster.LoadIcon(entry);
            icon.sprite  = sprite;
            icon.enabled = sprite != null;
            iconFallback.gameObject.SetActive(sprite == null);

            // 도감 신규 여부 — 정화해야 도감에 등록된다
            bool isNew = entry != null && FactoryManager.Instance != null &&
                         !FactoryManager.Instance.IsDiscovered(entry.id);
            newBadge.SetActive(isNew);

            nameTxt.text     = def.capturedName;
            purifiedTxt.text = $"정화하면  →  {def.purifiedName}";

            gradeBg.color = CreatureLabels.RarityColor(def.rarity);
            gradeTxt.text = CreatureLabels.GradeLabel(creature.grade);
            rarityTxt.text = $"{CreatureLabels.RarityKor(def.rarity)}  ·  {CreatureLabels.GradeLabel(creature.grade)}등급";
            rarityTxt.color = CreatureLabels.RarityColor(def.rarity);

            statTxt.text  = $"{CreatureLabels.StatKor(def.specialStat)}  ·  {CreatureLabels.TierLabel(def.tier)}";
            statTxt.color = CreatureLabels.TierColor(def.tier);
            facilityTxt.richText = true;
            facilityTxt.text = def.hasSpecialty
                ? $"{CreatureLabels.FacilityKor(def)}  ·  " +
                  $"<color=#6FD58A>{FactorySceneSetup.ResourceKorName(def.specialtyResource)} 생산 특화</color>"
                : $"{CreatureLabels.FacilityKor(def)}에서 활약";

            int bonus = CreatureInstance.TierBonus(def.tier);
            powerTxt.richText = true;
            powerTxt.text = bonus > 0
                ? $"능력치 {creature.GetStatPower()}  <color=#FFC94A>(티어 보너스 +{bonus})</color>"
                : $"능력치 {creature.GetStatPower()}";

            purifyLabel.text = isNew
                ? "정화\n<size=65%>도감 등록 · 공장 배치</size>"
                : "정화\n<size=65%>공장 배치</size>";

            root.SetActive(true);
        }

        void OnPurifyClicked()
        {
            root.SetActive(false);
            onPurify?.Invoke();
        }

        void OnDiscardClicked()
        {
            root.SetActive(false);
            onDiscard?.Invoke();
        }

        // ── 헬퍼 ────────────────────────────────────────────────────

        static TMP_Text MakeButton(RectTransform parent, float x0, float y0, float x1, float y1,
                                   Color color, UnityEngine.Events.UnityAction onClick)
        {
            var rt  = Anchored("Button", parent, x0, y0, x1, y1);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var label = Text(rt, "", 30f, Color.white, FontStyles.Bold);
            label.richText = true;
            return label;
        }

        static TMP_Text Text(RectTransform parent, string text, float size, Color color,
                             FontStyles style = FontStyles.Normal)
        {
            var rt = Rect("Text", parent);
            Fill(rt);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text          = text;
            tmp.fontSize      = size;
            tmp.fontStyle     = style;
            tmp.color         = color;
            tmp.alignment     = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            FontProvider.Apply(tmp);
            return tmp;
        }

        static RectTransform Anchored(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
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

        static Sprite CircleSprite(int size)
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
            return Sprite.Create(tex, new UnityEngine.Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
