using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace PixelCleaners
{
    public class SceneController : MonoBehaviour
    {
        public static SceneController Instance { get; private set; }

        public const string Login    = "LoginScene";
        public const string AR       = "ARScene";
        public const string Map      = "MapScene";
        public const string Factory  = "FactoryScene";
        public const string Creature = "CreatureScene";
        public const string Bag      = "BagScene";
        public const string Delivery = "DeliveryScene";

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void GoLogin()    => Load(Login);
        public static void GoAR()       => Load(AR);
        public static void GoMap()      => Load(Map);
        public static void GoFactory()  => Load(Factory);
        public static void GoCreature() => Load(Creature);
        public static void GoBag()      => Load(Bag);
        public static void GoDelivery() => Load(Delivery);

        /// <summary>씬이 Build Settings에 등록돼 있는지 확인한다.</summary>
        public static bool IsSceneInBuild(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.Contains(sceneName)) return true;
            }
            return false;
        }

        static void Load(string sceneName)
        {
            if (IsSceneInBuild(sceneName))
            {
                SceneManager.LoadScene(sceneName);
                return;
            }

            // 씬 없음 → 오버레이 폴백 (프로토타입 대응)
            Debug.LogWarning($"[SceneController] '{sceneName}' 미등록 → 오버레이로 대체");
            ShowOverlay(sceneName);
        }

        // ── 인-게임 오버레이 (씬 파일 없을 때 폴백) ──────────────────

        static GameObject overlayRoot;

        static void ShowOverlay(string sceneName)
        {
            // 기존 오버레이 제거
            if (overlayRoot != null) Destroy(overlayRoot);

            overlayRoot = new GameObject("SceneOverlay");
            DontDestroyOnLoad(overlayRoot);

            var canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;
            overlayRoot.AddComponent<GraphicRaycaster>();

            // 배경
            var bg = new GameObject("Bg");
            bg.transform.SetParent(overlayRoot.transform, false);
            var bgRT = bg.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bg.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.97f);

            // 콘텐츠 (VLG)
            var content = new GameObject("Content");
            content.transform.SetParent(overlayRoot.transform, false);
            var cRT = content.AddComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 0.1f); cRT.anchorMax = new Vector2(1f, 0.95f);
            cRT.offsetMin = new Vector2(24f, 0f);  cRT.offsetMax = new Vector2(-24f, 0f);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 20f; vlg.padding = new RectOffset(0, 0, 20, 20);
            vlg.childControlWidth = true; vlg.childControlHeight = false;

            // 타이틀
            string title = sceneName == Login    ? "로그인"
                         : sceneName == Creature ? "생명체"
                         : sceneName == Bag      ? "가방"
                         : sceneName == Delivery ? "납품 센터"
                         : sceneName;
            AddLabel(content.transform, title, 36f, FontStyles.Bold);

            // 씬 내용 렌더링
            if (sceneName == Creature)  BuildCreatureList(content.transform);
            else if (sceneName == Bag)  BuildBagList(content.transform);
            else if (sceneName == Delivery) BuildDeliveryOverlay(content.transform);

            // 닫기 버튼
            var closeBtn = new GameObject("CloseBtn");
            closeBtn.transform.SetParent(overlayRoot.transform, false);
            var closeBtnRT = closeBtn.AddComponent<RectTransform>();
            closeBtnRT.anchorMin        = new Vector2(0f, 0f);
            closeBtnRT.anchorMax        = new Vector2(1f, 0f);
            closeBtnRT.pivot            = new Vector2(0.5f, 0f);
            closeBtnRT.anchoredPosition = new Vector2(0f, 20f);
            closeBtnRT.sizeDelta        = new Vector2(0f, 140f);
            closeBtn.AddComponent<Image>().color = new Color(0.25f, 0.28f, 0.38f);
            var btn = closeBtn.AddComponent<Button>();
            btn.onClick.AddListener(() => {
                if (overlayRoot != null) Destroy(overlayRoot);
                overlayRoot = null;
            });

            var closeLbl = new GameObject("Label");
            closeLbl.transform.SetParent(closeBtn.transform, false);
            var closeLblRT = closeLbl.AddComponent<RectTransform>();
            closeLblRT.anchorMin = Vector2.zero; closeLblRT.anchorMax = Vector2.one;
            closeLblRT.offsetMin = closeLblRT.offsetMax = Vector2.zero;
            var closeTmp = closeLbl.AddComponent<TextMeshProUGUI>();
            closeTmp.text = "닫기"; closeTmp.fontSize = 28f;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.color = Color.white;
            FontProvider.Apply(closeTmp);
        }

        static void BuildCreatureList(Transform parent)
        {
            var inv = FactoryManager.Instance?.CreatureInventory;
            if (inv == null || inv.Count == 0)
            {
                AddLabel(parent, "포획한 생명체가 없습니다.", 22f);
                return;
            }
            foreach (var c in inv)
                AddLabel(parent,
                    $"{c.definition.purifiedName}  [{c.definition.rarity}]  효율 {c.GetEfficiency():F1}x",
                    22f);
        }

        static void BuildBagList(Transform parent)
        {
            var fm = FactoryManager.Instance;
            if (fm == null) return;

            AddLabel(parent, "포획 도구", 26f, FontStyles.Bold);
            var tools = fm.CaptureTools;
            foreach (var kv in tools)
            {
                string cnt = kv.Value < 0 ? "∞" : $"{kv.Value}개";
                AddLabel(parent,
                    $"{Capture.CaptureConfig.ToolKorName(kv.Key)}  {cnt}", 20f);
            }

            AddLabel(parent, "", 10f); // 간격
            AddLabel(parent, "각성제", 26f, FontStyles.Bold);
            var items = fm.AwakenItems;
            AddLabel(parent, $"일반 각성제  {items[AwakenItemTier.Normal]}개", 20f);
            AddLabel(parent, $"상급 각성제  {items[AwakenItemTier.Advanced]}개", 20f);
            AddLabel(parent, $"플로깅 각성제  {items[AwakenItemTier.Plogging]}개", 20f);
        }

        static void BuildDeliveryOverlay(Transform parent)
        {
            var fm = FactoryManager.Instance;
            if (fm == null) return;
            fm.Warehouse.TryGetValue(ResourceType.DeliveryItem,         out int normal);
            fm.Warehouse.TryGetValue(ResourceType.AdvancedDeliveryItem, out int advanced);
            int cnt = normal + advanced;
            AddLabel(parent, $"납품 물품  {normal}개", 22f);
            AddLabel(parent, $"고급 납품 물품  {advanced}개", 22f);
            AddLabel(parent, $"누적 포인트  {fm.DeliveryScore:N0} pt", 22f);
            if (cnt > 0)
            {
                var btnGO = new GameObject("DeliverBtn");
                btnGO.transform.SetParent(parent, false);
                btnGO.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 80f);
                btnGO.AddComponent<Image>().color = new Color(0.2f, 0.55f, 0.3f);
                var btn = btnGO.AddComponent<Button>();
                btn.onClick.AddListener(() => {
                    fm.Deliver();
                    if (overlayRoot != null) { Destroy(overlayRoot); overlayRoot = null; }
                });
                var lblGO = new GameObject("Label");
                lblGO.transform.SetParent(btnGO.transform, false);
                var lrt = lblGO.AddComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                var lTmp = lblGO.AddComponent<TextMeshProUGUI>();
                lTmp.text = $"납품하기 ({cnt}개 → +{fm.PendingDeliveryPoints:N0}pt)";
                lTmp.fontSize = 22f; lTmp.alignment = TextAlignmentOptions.Center;
                lTmp.color = Color.white;
                FontProvider.Apply(lTmp);
            }
        }

        static void AddLabel(Transform parent, string text, float size,
                              FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Lbl");
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, size + 12f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
            tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Left;
            FontProvider.Apply(tmp);
        }
    }
}
