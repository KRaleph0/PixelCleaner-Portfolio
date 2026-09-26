using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

/// <summary>
/// 납품 씬. 납품 탭(창고 → 포인트 환산) + 랭킹 탭(글로벌 리더보드).
/// </summary>
public class DeliverySceneSetup : MonoBehaviour
{
    const string PlayerName = "나";

    // ── UI 참조 ─────────────────────────────────────────────────
    TMP_Text deliveryItemTxt;
    TMP_Text advancedItemTxt;
    TMP_Text deliveryScoreTxt;
    TMP_Text deliverBtnTxt;
    GameObject deliveryPanel;
    GameObject rankingPanel;

    Button tabDelivery;
    Button tabRanking;

    Transform rankListContent;

    System.Action refreshHandler;

    void Start()
    {
        SetupCamera();
        BuildUI();
        if (GameObject.Find("NavCanvas") == null) SetupNavUI();

        StartCoroutine(PlayerSession.EnsureRegistered());
        Refresh();

        if (FactoryManager.Instance != null)
        {
            refreshHandler = Refresh;
            FactoryManager.Instance.OnInventoryChanged += refreshHandler;
        }
    }

    void OnDestroy()
    {
        if (FactoryManager.Instance != null && refreshHandler != null)
            FactoryManager.Instance.OnInventoryChanged -= refreshHandler;
    }

    // ── UI 전체 빌드 ──────────────────────────────────────────────

    void BuildUI()
    {
        var canvas = MakeCanvas("DeliveryCanvas", 0);
        var root   = canvas.transform;

        // 타이틀
        var titleGO = MakeRectGO("Title", root);
        var titleRT = titleGO.GetComponent<RectTransform>();
        titleRT.anchorMin        = new Vector2(0f, 1f);
        titleRT.anchorMax        = new Vector2(1f, 1f);
        titleRT.pivot            = new Vector2(0.5f, 1f);
        titleRT.anchoredPosition = new Vector2(0f, -40f);
        titleRT.sizeDelta        = new Vector2(0f, 80f);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text      = "납품 센터";
        titleTmp.fontSize  = 40f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color     = Color.white;
        FontProvider.Apply(titleTmp);

        // 탭 바
        var tabBar   = MakeRectGO("TabBar", root);
        var tabBarRT = tabBar.GetComponent<RectTransform>();
        tabBarRT.anchorMin        = new Vector2(0f, 1f);
        tabBarRT.anchorMax        = new Vector2(1f, 1f);
        tabBarRT.pivot            = new Vector2(0.5f, 1f);
        tabBarRT.anchoredPosition = new Vector2(0f, -126f);
        tabBarRT.sizeDelta        = new Vector2(0f, 80f);
        tabBar.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.17f);
        var tabHL = tabBar.AddComponent<HorizontalLayoutGroup>();
        tabHL.childControlWidth      = true;
        tabHL.childForceExpandWidth  = true;
        tabHL.childControlHeight     = true;
        tabHL.childForceExpandHeight = true;

        tabDelivery = MakeTabButton("납품", tabBar.transform, true);
        tabRanking  = MakeTabButton("랭킹", tabBar.transform, false);
        tabDelivery.onClick.AddListener(() => SwitchTab(true));
        tabRanking.onClick.AddListener(()  => SwitchTab(false));

        // 콘텐츠 영역
        var contentAreaGO = MakeRectGO("ContentArea", root);
        var contentAreaRT = contentAreaGO.GetComponent<RectTransform>();
        contentAreaRT.anchorMin = new Vector2(0f, 0f);
        contentAreaRT.anchorMax = new Vector2(1f, 1f);
        contentAreaRT.offsetMin = new Vector2(0f, 180f);
        contentAreaRT.offsetMax = new Vector2(0f, -210f);

        deliveryPanel = BuildDeliveryPanel(contentAreaGO.transform);
        rankingPanel  = BuildRankingPanel(contentAreaGO.transform);

        rankingPanel.SetActive(false);
    }

    // ── 납품 패널 ─────────────────────────────────────────────────

    GameObject BuildDeliveryPanel(Transform parent)
    {
        var panel = MakeRectGO("DeliveryPanel", parent);
        FillRT(panel.GetComponent<RectTransform>());
        var vlg = panel.AddComponent<VerticalLayoutGroup>();
        vlg.padding            = new RectOffset(24, 24, 20, 20);
        vlg.spacing            = 16f;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;

        AddSectionHeader(panel.transform, "창고 납품 물품");
        var itemRow  = AddInfoRow(panel.transform, "납품 물품", "0 개");
        var itemTxts = itemRow.GetComponentsInChildren<TMP_Text>();
        deliveryItemTxt = itemTxts.Length > 1 ? itemTxts[1] : itemTxts[0];

        var advRow  = AddInfoRow(panel.transform, "고급 납품 물품", "0 개");
        var advTxts = advRow.GetComponentsInChildren<TMP_Text>();
        advancedItemTxt = advTxts.Length > 1 ? advTxts[1] : advTxts[0];
        advancedItemTxt.color = new Color(1f, 0.82f, 0.35f);

        AddSectionHeader(panel.transform, "누적 납품 포인트");
        var scoreRow  = AddInfoRow(panel.transform, "포인트", "0 pt");
        var scoreTxts = scoreRow.GetComponentsInChildren<TMP_Text>();
        deliveryScoreTxt = scoreTxts.Length > 1 ? scoreTxts[1] : scoreTxts[0];

        var btnGO = MakeRectGO("DeliverBtn", panel.transform);
        btnGO.AddComponent<LayoutElement>().preferredHeight = 100f;
        btnGO.AddComponent<Image>().color = new Color(0.20f, 0.55f, 0.30f);
        var btn = btnGO.AddComponent<Button>();

        var btnLabel = MakeRectGO("Label", btnGO.transform);
        FillRT(btnLabel.GetComponent<RectTransform>());
        deliverBtnTxt = btnLabel.AddComponent<TextMeshProUGUI>();
        deliverBtnTxt.text      = "납품하기";
        deliverBtnTxt.fontSize  = 28f;
        deliverBtnTxt.fontStyle = FontStyles.Bold;
        deliverBtnTxt.alignment = TextAlignmentOptions.Center;
        deliverBtnTxt.color     = Color.white;
        FontProvider.Apply(deliverBtnTxt);

        btn.onClick.AddListener(OnDeliverClicked);

        var desc    = MakeRectGO("Desc", panel.transform);
        desc.AddComponent<LayoutElement>().preferredHeight = 48f;
        var descTmp = desc.AddComponent<TextMeshProUGUI>();
        descTmp.text      = $"납품 물품 {FactoryManager.PointsPerDelivery}pt · " +
                            $"고급 납품 물품 {FactoryManager.PointsPerAdvancedDelivery}pt";
        descTmp.fontSize  = 20f;
        descTmp.alignment = TextAlignmentOptions.Center;
        descTmp.color     = new Color(0.7f, 0.7f, 0.7f);
        FontProvider.Apply(descTmp);

        return panel;
    }

    // ── 랭킹 패널 ─────────────────────────────────────────────────

    GameObject BuildRankingPanel(Transform parent)
    {
        var panel    = MakeRectGO("RankingPanel", parent);
        FillRT(panel.GetComponent<RectTransform>());

        var scrollGO = MakeRectGO("Scroll", panel.transform);
        FillRT(scrollGO.GetComponent<RectTransform>());
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var viewport = MakeRectGO("Viewport", scrollGO.transform);
        FillRT(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = viewport.GetComponent<RectTransform>();

        var contentGO = MakeRectGO("Content", viewport.transform);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0f, 0f);
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.padding            = new RectOffset(24, 24, 16, 16);
        vlg.spacing            = 8f;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content    = contentRT;
        rankListContent   = contentGO.transform;

        return panel;
    }

    // ── 탭 전환 ───────────────────────────────────────────────────

    void SwitchTab(bool toDelivery)
    {
        deliveryPanel.SetActive(toDelivery);
        rankingPanel.SetActive(!toDelivery);
        ApplyTabStyle(tabDelivery, toDelivery);
        ApplyTabStyle(tabRanking, !toDelivery);

        if (!toDelivery) RefreshRankList();
    }

    static void ApplyTabStyle(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = active ? new Color(0.20f, 0.35f, 0.60f) : new Color(0.12f, 0.15f, 0.22f);
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
            tmp.color = active ? Color.white : new Color(0.6f, 0.6f, 0.6f);
    }

    // ── 납품 버튼 클릭 ────────────────────────────────────────────

    void OnDeliverClicked()
    {
        var fm = FactoryManager.Instance;
        if (fm == null) return;
        int delivered = fm.Deliver();
        if (delivered <= 0) return;

        Refresh();

        int score = fm.DeliveryScore;
        StartCoroutine(ApiClient.Submit(score,
            res => Debug.Log($"[Delivery] 서버 점수: {res.accepted_score}, 순위: {res.rank}위"),
            errCode => {
                if (errCode == 404)
                    StartCoroutine(PlayerSession.ReRegister(() =>
                        StartCoroutine(ApiClient.Submit(score, null))));
            }
        ));
    }

    // ── UI 갱신 ───────────────────────────────────────────────────

    void Refresh()
    {
        var fm = FactoryManager.Instance;
        if (fm == null) return;

        fm.Warehouse.TryGetValue(ResourceType.DeliveryItem,         out int itemCount);
        fm.Warehouse.TryGetValue(ResourceType.AdvancedDeliveryItem, out int advancedCount);
        int totalCount = itemCount + advancedCount;

        if (deliveryItemTxt  != null) deliveryItemTxt.text  = $"{itemCount} 개";
        if (advancedItemTxt  != null) advancedItemTxt.text  = $"{advancedCount} 개";
        if (deliveryScoreTxt != null) deliveryScoreTxt.text = $"{fm.DeliveryScore:N0} pt";
        if (deliverBtnTxt != null)
        {
            bool canDeliver = totalCount > 0;
            deliverBtnTxt.text = canDeliver
                ? $"납품하기 ({totalCount}개 → +{fm.PendingDeliveryPoints:N0}pt)"
                : "납품 물품 없음";
            var btn = deliverBtnTxt.GetComponentInParent<Button>();
            if (btn != null)
            {
                btn.interactable = canDeliver;
                var img = btn.GetComponent<Image>();
                if (img != null)
                    img.color = canDeliver ? new Color(0.20f, 0.55f, 0.30f) : new Color(0.25f, 0.27f, 0.32f);
            }
        }
    }

    // ── 글로벌 랭킹 ──────────────────────────────────────────────

    void RefreshRankList()
    {
        if (rankListContent == null) return;
        ClearRankList();
        AddStatusLabel("불러오는 중...");
        StartCoroutine(LoadRankFromServer());
    }

    IEnumerator LoadRankFromServer()
    {
        // 내 기록 먼저 조회 (하이라이트 판별용)
        ApiClient.RankEntry myEntry = null;
        yield return ApiClient.GetMyRank(e => myEntry = e);

        // 글로벌 랭킹
        ApiClient.RankingResponse rankData = null;
        yield return ApiClient.GetRanking(10, 0, r => rankData = r);

        ClearRankList();

        if (rankData == null || rankData.entries == null)
        {
            AddStatusLabel("서버 연결 실패");
            yield break;
        }
        if (rankData.entries.Length == 0)
        {
            AddStatusLabel("아직 납품 기록이 없습니다.");
            yield break;
        }

        // 서버가 내 정체를 알려주지 않아 목록에서 나를 정확히 찾을 수 없다.
        //  - RankEntry에 player_id가 없다
        //  - /ranking 은 동점에 1,2위를 순차로 주는데 /ranking/me 는 둘 다 1위로 답한다
        //    → rank로 대조하면 동점일 때 남의 줄이 강조된다
        // 그래서 점수+닉네임으로 대조하고, 그마저 겹칠 경우를 대비해 첫 줄만 강조한다.
        // 근본 해결은 서버가 RankEntry에 player_id를 실어 주는 것 (§18 참고).
        bool meFound = false;
        foreach (var e in rankData.entries)
        {
            bool isMe = !meFound
                        && myEntry != null
                        && myEntry.rank > 0
                        && !string.IsNullOrEmpty(myEntry.display_name)
                        && e.score == myEntry.score
                        && e.display_name == myEntry.display_name;
            if (isMe) meFound = true;

            AddRankRow(rankListContent, e.rank, e.display_name, e.score, isMe);
        }
    }

    void ClearRankList()
    {
        if (rankListContent == null) return;
        foreach (Transform child in rankListContent) Destroy(child.gameObject);
    }

    void AddStatusLabel(string text)
    {
        var lbl = MakeRectGO("Status", rankListContent);
        lbl.AddComponent<LayoutElement>().preferredHeight = 60f;
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = 22f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = new Color(0.6f, 0.6f, 0.6f);
        FontProvider.Apply(tmp);
    }

    void AddRankRow(Transform parent, int rank, string name, int score, bool highlight)
    {
        var row = MakeRectGO($"Rank_{rank}", parent);
        row.AddComponent<LayoutElement>().preferredHeight = 72f;
        row.AddComponent<Image>().color = highlight
            ? new Color(0.18f, 0.30f, 0.50f)
            : new Color(0.13f, 0.15f, 0.20f);

        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.padding           = new RectOffset(20, 20, 8, 8);
        hl.childControlHeight = true;

        var rankLbl = MakeRectGO("Rank", row.transform);
        rankLbl.AddComponent<LayoutElement>().preferredWidth = 80f;
        var rankTmp = rankLbl.AddComponent<TextMeshProUGUI>();
        rankTmp.text      = $"{rank}위";
        rankTmp.fontSize  = 22f;
        rankTmp.alignment = TextAlignmentOptions.Center;
        rankTmp.color     = rank == 1 ? new Color(1f, 0.85f, 0.2f)
                          : rank == 2 ? new Color(0.8f, 0.8f, 0.85f)
                          : rank == 3 ? new Color(0.9f, 0.6f, 0.3f)
                          : Color.white;
        FontProvider.Apply(rankTmp);

        var nameLbl = MakeRectGO("Name", row.transform);
        nameLbl.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var nameTmp = nameLbl.AddComponent<TextMeshProUGUI>();
        nameTmp.text      = name;
        nameTmp.fontSize  = 22f;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.color     = highlight ? new Color(0.6f, 0.85f, 1f) : Color.white;
        FontProvider.Apply(nameTmp);

        var scoreLbl = MakeRectGO("Score", row.transform);
        scoreLbl.AddComponent<LayoutElement>().preferredWidth = 180f;
        var scoreTmp = scoreLbl.AddComponent<TextMeshProUGUI>();
        scoreTmp.text      = $"{score:N0} pt";
        scoreTmp.fontSize  = 22f;
        scoreTmp.fontStyle = FontStyles.Bold;
        scoreTmp.alignment = TextAlignmentOptions.Right;
        scoreTmp.color     = new Color(1f, 0.85f, 0.4f);
        FontProvider.Apply(scoreTmp);
    }

    // ── 네비게이션 ────────────────────────────────────────────────

    void SetupNavUI()
    {
        var canvas = MakeCanvas("NavCanvas", 5);
        var panel  = MakeRectGO("NavPanel", canvas.transform);
        var rt     = panel.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, 180f);
        panel.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f, 0.95f);

        var layout = panel.AddComponent<HorizontalLayoutGroup>();
        layout.spacing               = 8f;
        layout.padding               = new RectOffset(16, 16, 8, 8);
        layout.childControlWidth     = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight    = true;
        layout.childForceExpandHeight = true;

        MakeNavButton("지도",   panel.transform, SceneController.GoMap);
        MakeNavButton("공장",   panel.transform, SceneController.GoFactory);
        MakeNavButton("납품",   panel.transform, null);
        MakeNavButton("생명체", panel.transform, SceneController.GoCreature);
        MakeNavButton("가방",   panel.transform, SceneController.GoBag);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    void SetupCamera()
    {
        if (Camera.main != null) return;
        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.10f, 0.14f);
        cam.orthographic    = true;
        cam.tag             = "MainCamera";
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    static GameObject MakeCanvas(string name, int sortOrder)
    {
        var go     = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    static GameObject MakeRectGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void FillRT(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AddSectionHeader(Transform parent, string title)
    {
        var go = MakeRectGO("Section", parent);
        go.AddComponent<LayoutElement>().preferredHeight = 52f;
        go.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.19f);
        var lbl = MakeRectGO("Label", go.transform);
        var lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(16f, 0f); lrt.offsetMax = Vector2.zero;
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text      = title;
        tmp.fontSize  = 24f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.color     = new Color(0.7f, 0.85f, 1f);
        FontProvider.Apply(tmp);
    }

    static GameObject AddInfoRow(Transform parent, string label, string value)
    {
        var row = MakeRectGO("Row", parent);
        row.AddComponent<LayoutElement>().preferredHeight = 68f;
        row.AddComponent<Image>().color = new Color(0.15f, 0.17f, 0.22f);
        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.padding           = new RectOffset(20, 20, 8, 8);
        hl.childControlHeight = true;

        var lbl = MakeRectGO("Label", row.transform);
        lbl.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var lblTmp = lbl.AddComponent<TextMeshProUGUI>();
        lblTmp.text = label; lblTmp.fontSize = 22f;
        lblTmp.alignment = TextAlignmentOptions.Left;
        lblTmp.color = Color.white;
        FontProvider.Apply(lblTmp);

        var val = MakeRectGO("Value", row.transform);
        val.AddComponent<LayoutElement>().preferredWidth = 240f;
        var valTmp = val.AddComponent<TextMeshProUGUI>();
        valTmp.text      = value;
        valTmp.fontSize  = 24f;
        valTmp.fontStyle = FontStyles.Bold;
        valTmp.alignment = TextAlignmentOptions.Right;
        valTmp.color     = new Color(1f, 0.9f, 0.5f);
        FontProvider.Apply(valTmp);

        return row;
    }

    static Button MakeTabButton(string label, Transform parent, bool active)
    {
        var go = MakeRectGO(label, parent);
        go.AddComponent<Image>().color = active ? new Color(0.20f, 0.35f, 0.60f) : new Color(0.12f, 0.15f, 0.22f);
        var btn = go.AddComponent<Button>();
        var lbl = MakeRectGO("Label", go.transform);
        FillRT(lbl.GetComponent<RectTransform>());
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 26f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = active ? Color.white : new Color(0.6f, 0.6f, 0.6f);
        FontProvider.Apply(tmp);
        return btn;
    }

    static Button MakeNavButton(string label, Transform parent, System.Action onClick)
    {
        var go = MakeRectGO(label, parent);
        go.AddComponent<Image>().color = onClick == null ? new Color(0.25f, 0.35f, 0.55f) : new Color(0.15f, 0.20f, 0.32f);
        var btn = go.AddComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        else                 btn.interactable = false;

        var lbl = MakeRectGO("Label", go.transform);
        FillRT(lbl.GetComponent<RectTransform>());
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 17f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = onClick == null ? new Color(1f, 1f, 1f, 0.5f) : Color.white;
        FontProvider.Apply(tmp);
        return btn;
    }
}
