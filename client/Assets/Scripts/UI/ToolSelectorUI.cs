using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;
using PixelCleaners.Capture;

namespace PixelCleaners.UI
{
    /// <summary>
    /// AR 씬 하단에 항상 표시되는 포획틀 선택 바.
    /// CaptureInteraction이 SelectedTool을 읽어 미니게임에 전달한다.
    /// </summary>
    public class ToolSelectorUI : MonoBehaviour
    {
        public static ToolSelectorUI Instance { get; private set; }
        public static CaptureToolTier SelectedTool { get; private set; } = CaptureToolTier.Basic;

        static readonly Color ColSelected    = new Color(0.25f, 0.55f, 0.9f);
        static readonly Color ColAvailable   = new Color(0.15f, 0.20f, 0.32f);
        static readonly Color ColUnavailable = new Color(0.10f, 0.10f, 0.14f);

        readonly List<(Image bg, Button btn, TMP_Text countTxt, CaptureToolTier tier)> slots = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (FactoryManager.Instance != null)
                FactoryManager.Instance.OnInventoryChanged -= Refresh;
        }

        /// <summary>canvasParent 아래에 선택 바를 빌드한다. bottomOffset = nav 바 높이.</summary>
        public void Build(Transform canvasParent, float bottomOffset = 180f)
        {
            // ── 패널 ──────────────────────────────────────────────
            var panel = new GameObject("ToolSelectorPanel");
            panel.transform.SetParent(canvasParent, false);
            var pr = panel.AddComponent<RectTransform>();
            pr.anchorMin        = new Vector2(0f, 0f);
            pr.anchorMax        = new Vector2(1f, 0f);
            pr.pivot            = new Vector2(0.5f, 0f);
            pr.anchoredPosition = new Vector2(0f, bottomOffset);
            pr.sizeDelta        = new Vector2(0f, 115f);
            panel.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.93f);

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.padding           = new RectOffset(10, 10, 8, 8);
            layout.spacing           = 8f;
            layout.childControlWidth  = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;

            // ── "포획틀" 라벨 ──────────────────────────────────────
            AddLabel(panel.transform, "포획틀", 80f);

            // ── 도구 버튼 4개 ──────────────────────────────────────
            foreach (CaptureToolTier tier in Enum.GetValues(typeof(CaptureToolTier)))
                AddToolButton(panel.transform, tier);

            Refresh();

            if (FactoryManager.Instance != null)
                FactoryManager.Instance.OnInventoryChanged += Refresh;
        }

        void AddLabel(Transform parent, string text, float width)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = width; le.preferredWidth = width; le.flexibleWidth = 0f;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = 18f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = new Color(0.7f, 0.85f, 1f);
            FontProvider.Apply(tmp);
        }

        void AddToolButton(Transform parent, CaptureToolTier tier)
        {
            var go = new GameObject($"Tool_{tier}");
            go.transform.SetParent(parent, false);
            var bg  = go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();

            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.padding           = new RectOffset(4, 4, 6, 6);
            vl.spacing           = 2f;
            vl.childControlWidth  = true;
            vl.childForceExpandWidth = true;
            vl.childControlHeight = false;
            vl.childAlignment     = TextAnchor.MiddleCenter;

            var nameTxt  = MakeTMP(go.transform, CaptureConfig.ToolKorName(tier), 15f, 28f);
            nameTxt.fontStyle = FontStyles.Bold;
            var countTxt = MakeTMP(go.transform, "∞", 13f, 22f);
            countTxt.color = new Color(0.7f, 0.95f, 0.7f);

            var t = tier;
            btn.onClick.AddListener(() => Select(t));

            slots.Add((bg, btn, countTxt, tier));
        }

        static TMP_Text MakeTMP(Transform parent, string text, float size, float height)
        {
            var go = new GameObject("TMP");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, height);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;
            FontProvider.Apply(tmp);
            return tmp;
        }

        void Select(CaptureToolTier tier)
        {
            SelectedTool = tier;
            Refresh();
        }

        public void Refresh()
        {
            foreach (var (bg, btn, countTxt, tier) in slots)
            {
                bool has = FactoryManager.Instance == null || FactoryManager.Instance.HasTool(tier);
                bool sel = SelectedTool == tier;

                btn.interactable = has;
                bg.color = sel ? ColSelected : (has ? ColAvailable : ColUnavailable);

                if (FactoryManager.Instance != null)
                {
                    int cnt = FactoryManager.Instance.CaptureTools[tier];
                    countTxt.text = cnt < 0 ? "∞" : $"×{cnt}";
                }
                countTxt.color = sel ? Color.white : new Color(0.65f, 0.9f, 0.65f);
            }
        }
    }
}
