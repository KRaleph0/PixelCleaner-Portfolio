using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;
using PixelCleaners.UI;

public class FactorySceneSetup : MonoBehaviour
{
    System.Action _statusRefresh;

    // 출력 선택 팝업 참조
    GameObject _outputPopup;
    GameObject _outputDim;
    TMP_Text   _popupTitleTxt;
    Image      _popupBtnAImg, _popupBtnBImg;
    TMP_Text   _popupBtnATxt, _popupBtnBTxt;

    const int   MaxSlots  = 5;
    const float StatusH   = 210f;  // 상단 창고 현황 패널 높이 (10종 × 2줄)
    const float StripRightReserve = 72f;  // 오른쪽 위 디버그 토글(+) 버튼 자리
    const float NavH      = 180f;  // 하단 내비 높이
    const float RowH      = 120f;  // 시설 행 높이
    const float CircleW   = 100f;  // 원형 아이콘 폭
    const float CollectW  = 80f;   // 수거 버튼 폭

    static readonly FacilityType[] FacilityOrder =
    {
        FacilityType.PollutionCollector,
        FacilityType.DissolutionRefinery,
        FacilityType.ForgingRefinery,
        FacilityType.CompressionRefinery,
        FacilityType.PixelReconstructor,
    };

    void Start()
    {
        SetupCamera();
        if (GameObject.Find("NavCanvas") == null) SetupNavUI();
        StartCoroutine(BuildUINextFrame());
    }

    IEnumerator BuildUINextFrame()
    {
        yield return null;
        SetupFactoryUI();
        yield return null;
        _statusRefresh?.Invoke();
    }

    void OnDestroy()
    {
        if (_statusRefresh != null && FactoryManager.Instance != null)
            FactoryManager.Instance.OnInventoryChanged -= _statusRefresh;
    }

    void SetupCamera()
    {
        if (Camera.main != null) return;
        var camGO = new GameObject("FactoryCamera");
        var cam   = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.10f, 0.14f);
        cam.orthographic    = false;
        cam.tag             = "MainCamera";
        camGO.transform.position = new Vector3(0f, 0f, -10f);
    }

    // ── 공장 UI ─────────────────────────────────────────────────────

    void SetupFactoryUI()
    {
        var canvasGO = MakeCanvas("FactoryCanvas");
        var canvas   = canvasGO.transform;

        BuildStatusStrip(canvas);

        var scrollRect = BuildScrollView(canvas);
        var content    = scrollRect.content;

        // FacilitySlot 그룹화
        var slotsByType = new Dictionary<FacilityType, List<FacilitySlot>>();
        foreach (var slot in FindObjectsByType<FacilitySlot>(FindObjectsSortMode.None))
        {
            if (slot.Definition == null) continue;
            var t = slot.Definition.facilityType;
            if (!slotsByType.TryGetValue(t, out var list))
                slotsByType[t] = list = new List<FacilitySlot>();
            list.Add(slot);
        }

        foreach (var type in FacilityOrder)
            if (slotsByType.TryGetValue(type, out var list))
                BuildFacilityRow(content, type, list);

        for (int n = 0; n < SynthesisManager.SynthesisCount; n++)
            BuildRecipeCard(content, n, RecipeCategory.Synthesis, $"일반 제작소 {n + 1}");
        for (int n = 0; n < SynthesisManager.CraftCount; n++)
            BuildRecipeCard(content, SynthesisManager.SynthesisCount + n, RecipeCategory.Craft, $"고급 제작소 {n + 1}");

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        // 팝업은 스크롤보다 나중에 추가 → 항상 위에 렌더링
        BuildOutputPopup(canvas);
        BuildDebugButton(canvas);
    }

    // ── 상단 물품 현황 패널 ──────────────────────────────────────────

    void BuildStatusStrip(Transform canvasParent)
    {
        var strip   = MakeRectGO("StatusStrip", canvasParent);
        var stripRT = strip.GetComponent<RectTransform>();
        stripRT.anchorMin        = new Vector2(0f, 1f);
        stripRT.anchorMax        = Vector2.one;
        stripRT.pivot            = new Vector2(0.5f, 1f);
        stripRT.anchoredPosition = Vector2.zero;
        stripRT.sizeDelta        = new Vector2(0f, StatusH);
        strip.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.13f);

        // 헤더 — 오른쪽은 디버그 토글(+) 버튼 자리
        var headerGO = MakeRectGO("Header", strip.transform);
        var headerRT = headerGO.GetComponent<RectTransform>();
        headerRT.anchorMin        = new Vector2(0f, 1f);
        headerRT.anchorMax        = Vector2.one;
        headerRT.pivot            = new Vector2(0f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta        = new Vector2(-StripRightReserve, 30f);
        var headerTxt = headerGO.AddComponent<TextMeshProUGUI>();
        headerTxt.richText  = true;
        headerTxt.text      = "  창고 현황   <size=80%><color=#8FA3BF>숫자 = 창고 보유량 · " +
                              "<color=#6FD58A>+N</color> = 공장에서 수거 대기</color></size>";
        headerTxt.fontSize  = 17f;
        headerTxt.fontStyle = FontStyles.Bold;
        headerTxt.color     = new Color(0.55f, 0.72f, 0.95f);
        headerTxt.alignment = TextAlignmentOptions.MidlineLeft;
        FontProvider.Apply(headerTxt);

        // 구분선
        var divGO = MakeRectGO("Divider", strip.transform);
        var divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin        = new Vector2(0f, 1f);
        divRT.anchorMax        = Vector2.one;
        divRT.pivot            = new Vector2(0f, 1f);
        divRT.anchoredPosition = new Vector2(0f, -30f);
        divRT.sizeDelta        = new Vector2(0f, 2f);
        divGO.AddComponent<Image>().color = new Color(0.25f, 0.35f, 0.55f, 0.45f);

        // 10종 × 5열 2줄 (스크롤 없음). 납품 물품은 납품 씬에서 표시한다
        var gridGO = MakeRectGO("Grid", strip.transform);
        var gridRT = gridGO.GetComponent<RectTransform>();
        gridRT.anchorMin = Vector2.zero;
        gridRT.anchorMax = Vector2.one;
        gridRT.offsetMin = new Vector2(4f, 4f);
        gridRT.offsetMax = new Vector2(-(4f + StripRightReserve), -34f);

        var gridVL = gridGO.AddComponent<VerticalLayoutGroup>();
        gridVL.spacing                = 4f;
        gridVL.childControlWidth      = true;
        gridVL.childForceExpandWidth  = true;
        gridVL.childControlHeight     = true;
        gridVL.childForceExpandHeight = true;

        var colBase      = new Color(0.48f, 0.62f, 0.82f);   // 기초·1차
        var colSecondary = new Color(0.72f, 0.56f, 0.92f);   // 2차 (일반 제작소)

        // (자원, 이름, 이름 색, 공장 재고가 있는 자원인지)
        var resDefs = new (ResourceType type, string name, Color nameColor, bool hasFactoryStock)[]
        {
            (ResourceType.Garbage,           "쓰레기",     colBase,      true),
            (ResourceType.Plastic,           "플라스틱",   colBase,      true),
            (ResourceType.Glass,             "유리",       colBase,      true),
            (ResourceType.Metal,             "금속",       colBase,      true),
            (ResourceType.Can,               "캔",         colBase,      true),
            (ResourceType.Paper,             "종이",       colBase,      true),
            (ResourceType.Textile,           "섬유",       colBase,      true),
            (ResourceType.PixelFragment,     "픽셀파편",   colBase,      true),
            (ResourceType.RecycledComposite, "재생복합재", colSecondary, false),
            (ResourceType.RecycledAlloy,     "재생합금",   colSecondary, false),
        };

        const int Columns = 5;
        var cntTxts     = new TMP_Text[resDefs.Length];
        var pendingTxts = new TMP_Text[resDefs.Length];

        Transform row = null;
        for (int i = 0; i < resDefs.Length; i++)
        {
            if (i % Columns == 0)
            {
                var rowGO = MakeRectGO($"Row_{i / Columns}", gridGO.transform);
                var rowHL = rowGO.AddComponent<HorizontalLayoutGroup>();
                rowHL.spacing                = 5f;
                rowHL.childControlWidth      = true;
                rowHL.childForceExpandWidth  = true;
                rowHL.childControlHeight     = true;
                rowHL.childForceExpandHeight = true;
                row = rowGO.transform;
            }

            var cell = MakeRectGO($"Cell_{resDefs[i].type}", row);
            cell.AddComponent<Image>().color = new Color(0.09f, 0.11f, 0.17f);
            var cellVL = cell.AddComponent<VerticalLayoutGroup>();
            cellVL.padding                = new RectOffset(2, 2, 2, 2);
            cellVL.childControlWidth      = true;
            cellVL.childForceExpandWidth  = true;
            cellVL.childControlHeight     = true;
            cellVL.childForceExpandHeight = false;

            var nameGO  = MakeRectGO("Name", cell.transform);
            nameGO.AddComponent<LayoutElement>().preferredHeight = 18f;
            var nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
            nameTxt.text      = resDefs[i].name;
            nameTxt.fontSize  = 12f;
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.color     = resDefs[i].nameColor;
            FontProvider.Apply(nameTxt);

            var cntGO  = MakeRectGO("Count", cell.transform);
            cntGO.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var cntTxt = cntGO.AddComponent<TextMeshProUGUI>();
            cntTxt.text      = "0";
            cntTxt.fontSize  = 28f;
            cntTxt.fontStyle = FontStyles.Bold;
            cntTxt.alignment = TextAlignmentOptions.Center;
            cntTxt.color     = new Color(0.30f, 0.33f, 0.44f);
            FontProvider.Apply(cntTxt);
            cntTxts[i] = cntTxt;

            // 공장에서 수거 대기 중인 양 (기초·1차만)
            var pendGO  = MakeRectGO("Pending", cell.transform);
            pendGO.AddComponent<LayoutElement>().preferredHeight = 15f;
            var pendTxt = pendGO.AddComponent<TextMeshProUGUI>();
            pendTxt.text      = "";
            pendTxt.fontSize  = 12f;
            pendTxt.fontStyle = FontStyles.Bold;
            pendTxt.alignment = TextAlignmentOptions.Center;
            pendTxt.color     = new Color(0.44f, 0.84f, 0.54f);
            FontProvider.Apply(pendTxt);
            pendingTxts[i] = pendTxt;
        }

        // 두 번째 줄이 한 줄 칸 수보다 적으면 빈 칸으로 채워 첫 줄과 칸 너비를 맞춘다
        for (int i = resDefs.Length; i % Columns != 0; i++)
            MakeRectGO("Spacer", row);

        void Refresh()
        {
            if (FactoryManager.Instance == null) return;
            var factory = FactoryManager.Instance.Resources;
            var wh      = FactoryManager.Instance.Warehouse;
            for (int i = 0; i < resDefs.Length; i++)
            {
                if (cntTxts[i] == null) continue;

                // 창고 보유량 — 합성·제작·납품에 실제로 쓸 수 있는 양.
                // (예전에는 공장 재고와 합산해서, 수거해도 숫자가 변하지 않아 수거가 안 되는 것처럼 보였다)
                wh.TryGetValue(resDefs[i].type, out int stored);
                cntTxts[i].text  = stored.ToString();
                cntTxts[i].color = stored > 0
                    ? new Color(0.95f, 0.88f, 0.35f)
                    : new Color(0.30f, 0.33f, 0.44f);

                int pending = 0;
                if (resDefs[i].hasFactoryStock) factory.TryGetValue(resDefs[i].type, out pending);
                pendingTxts[i].text = pending > 0 ? $"+{pending}" : "";
            }
        }

        _statusRefresh = Refresh;
        StartCoroutine(RefreshWhenReady(Refresh));
    }

    IEnumerator RefreshWhenReady(System.Action refresh)
    {
        // FactoryManager가 준비될 때까지 최대 3초 대기
        float waited = 0f;
        while (FactoryManager.Instance == null && waited < 3f)
        {
            yield return null;
            waited += Time.deltaTime;
        }
        refresh?.Invoke();
        if (FactoryManager.Instance != null)
            FactoryManager.Instance.OnInventoryChanged += refresh;
    }

    // ── 시설 행 (가로 배치: 원형 + 슬롯 5개 + 수거) ─────────────────

    void BuildFacilityRow(RectTransform parent, FacilityType type, List<FacilitySlot> typeSlots)
    {
        int  numSlots  = Mathf.Min(Mathf.Max(typeSlots.Count, 1), MaxSlots);
        bool hasChoice = typeSlots.Count > 0 && typeSlots[0].Definition.hasChoice;

        // 행 컨테이너
        var row   = MakeRectGO($"Row_{type}", parent);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = rowLE.preferredHeight = RowH;
        row.AddComponent<Image>().color = new Color(0.11f, 0.13f, 0.18f);

        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.padding               = new RectOffset(8, 8, 8, 8);
        hl.spacing               = 8f;
        hl.childControlWidth     = true;
        hl.childForceExpandWidth = false;
        hl.childControlHeight    = true;
        hl.childForceExpandHeight = true;

        // ── 왼쪽: 원형 아이콘 ─────────────────────────────────────────
        var circleGO  = MakeRectGO("Circle", row.transform);
        var circleLE  = circleGO.AddComponent<LayoutElement>();
        circleLE.preferredWidth = CircleW;
        circleLE.flexibleWidth  = 0f;

        var circleBg = circleGO.AddComponent<Image>();
        circleBg.sprite = MakeCircleSprite(64);
        circleBg.color  = new Color(0.14f, 0.20f, 0.34f);

        // 시설명 (상단 작은 글씨)
        var facLblGO  = MakeRectGO("FacName", circleGO.transform);
        var facLblRT  = facLblGO.GetComponent<RectTransform>();
        facLblRT.anchorMin        = new Vector2(0f, 0.72f);
        facLblRT.anchorMax        = new Vector2(1f, 1f);
        facLblRT.offsetMin        = new Vector2(4f, -4f);
        facLblRT.offsetMax        = new Vector2(-4f, -4f);
        var facLblTxt    = facLblGO.AddComponent<TextMeshProUGUI>();
        facLblTxt.text   = FacilityKorName(type);
        facLblTxt.fontSize  = 10f;
        facLblTxt.alignment = TextAlignmentOptions.Center;
        facLblTxt.color     = new Color(0.50f, 0.65f, 0.85f);
        FontProvider.Apply(facLblTxt);

        // 자원명 (중앙)
        var resNameGO  = MakeRectGO("ResName", circleGO.transform);
        var resNameRT  = resNameGO.GetComponent<RectTransform>();
        resNameRT.anchorMin = new Vector2(0f, 0.35f);
        resNameRT.anchorMax = new Vector2(1f, 0.72f);
        resNameRT.offsetMin = resNameRT.offsetMax = Vector2.zero;
        var resNameTxt    = resNameGO.AddComponent<TextMeshProUGUI>();
        resNameTxt.text   = ResourceKorName(typeSlots.Count > 0 ? typeSlots[0].ActiveOutput : default);
        resNameTxt.fontSize  = 13f;
        resNameTxt.fontStyle = FontStyles.Bold;
        resNameTxt.alignment = TextAlignmentOptions.Center;
        resNameTxt.color     = new Color(0.88f, 0.94f, 1f);
        FontProvider.Apply(resNameTxt);

        // 재고 카운트 (하단)
        var cntGO  = MakeRectGO("ResCount", circleGO.transform);
        var cntRT  = cntGO.GetComponent<RectTransform>();
        cntRT.anchorMin = new Vector2(0f, 0.05f);
        cntRT.anchorMax = new Vector2(1f, 0.35f);
        cntRT.offsetMin = cntRT.offsetMax = Vector2.zero;
        var resCountTxt    = cntGO.AddComponent<TextMeshProUGUI>();
        resCountTxt.text   = "0개";
        resCountTxt.fontSize  = 12f;
        resCountTxt.fontStyle = FontStyles.Bold;
        resCountTxt.alignment = TextAlignmentOptions.Center;
        resCountTxt.color     = new Color(0.95f, 0.82f, 0.25f);
        FontProvider.Apply(resCountTxt);

        // 선택 가능한 시설: 원 탭 → 팝업
        if (hasChoice)
        {
            var btn = circleGO.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = circleBg;
            var capturedType    = type;
            var capturedPrimary   = typeSlots[0].Definition.outputResource;
            var capturedSecondary = typeSlots[0].Definition.secondaryOutput;
            var capturedResName   = resNameTxt;
            btn.onClick.AddListener(() =>
                ShowOutputPopup(capturedType, capturedPrimary, capturedSecondary, capturedResName));
        }

        // ── 가운데: 크리처 슬롯 5개 ──────────────────────────────────
        var slotBgs    = new Image[numSlots];
        var slotLabels = new TMP_Text[numSlots];
        var slotIcons  = new Image[numSlots];
        var slotBtns   = new UnityEngine.UI.Button[numSlots];

        for (int i = 0; i < numSlots; i++)
        {
            var slotGO = MakeRectGO($"Slot_{i}", row.transform);
            var slotLE = slotGO.AddComponent<LayoutElement>();
            slotLE.preferredWidth = 0f;
            slotLE.flexibleWidth  = 1f;

            var slotImg = slotGO.AddComponent<Image>();
            slotImg.color = new Color(0.18f, 0.20f, 0.28f);
            slotBgs[i] = slotImg;

            var slotOl = slotGO.AddComponent<Outline>();
            slotOl.effectColor    = new Color(0.36f, 0.40f, 0.56f, 0.55f);
            slotOl.effectDistance = new Vector2(1f, 1f);

            slotIcons[i] = CreatureSlotView.CreateIcon(slotGO.transform);

            var lblGO = MakeRectGO("Label", slotGO.transform);
            FillRTWithInset(lblGO.GetComponent<RectTransform>(), 4f, 4f, 4f, 4f);
            var lblTxt    = lblGO.AddComponent<TextMeshProUGUI>();
            lblTxt.text   = "배치";
            lblTxt.fontSize  = 13f;
            lblTxt.alignment = TextAlignmentOptions.Center;
            lblTxt.color     = new Color(0.40f, 0.42f, 0.52f);
            FontProvider.Apply(lblTxt);
            slotLabels[i] = lblTxt;

            var btn = slotGO.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = slotImg;
            slotBtns[i] = btn;
        }

        // ── 오른쪽: 수거 버튼 ─────────────────────────────────────────
        var collectGO = MakeRectGO("CollectBtn", row.transform);
        var collectLE = collectGO.AddComponent<LayoutElement>();
        collectLE.preferredWidth = 130f;
        collectLE.flexibleWidth  = 0f;

        var collectImg = collectGO.AddComponent<Image>();
        collectImg.color = new Color(0.22f, 0.26f, 0.38f);

        var collectBtn = collectGO.AddComponent<UnityEngine.UI.Button>();
        collectBtn.targetGraphic = collectImg;
        var capturedFacType = type;
        collectBtn.onClick.AddListener(() =>
        {
            if (FactoryManager.Instance == null) return;
            FactoryManager.Instance.CollectResource(FindFirstActiveOutput(capturedFacType));
        });

        var collectVL = collectGO.AddComponent<VerticalLayoutGroup>();
        collectVL.childControlWidth   = true;
        collectVL.childControlHeight  = true;
        collectVL.childForceExpandHeight = true;
        collectVL.padding = new RectOffset(4, 4, 4, 4);
        collectVL.spacing = 2f;

        var collectCntGO  = MakeRectGO("Count", collectGO.transform);
        collectCntGO.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var collectCntTxt = collectCntGO.AddComponent<TextMeshProUGUI>();
        collectCntTxt.text      = "0";
        collectCntTxt.fontSize  = 26f;
        collectCntTxt.fontStyle = FontStyles.Bold;
        collectCntTxt.alignment = TextAlignmentOptions.Center;
        collectCntTxt.color     = Color.white;
        FontProvider.Apply(collectCntTxt);

        var collectLblGO  = MakeRectGO("Label", collectGO.transform);
        collectLblGO.AddComponent<LayoutElement>().preferredHeight = 22f;
        var collectLblTxt = collectLblGO.AddComponent<TextMeshProUGUI>();
        collectLblTxt.text      = "수거";
        collectLblTxt.fontSize  = 15f;
        collectLblTxt.fontStyle = FontStyles.Bold;
        collectLblTxt.alignment = TextAlignmentOptions.Center;
        collectLblTxt.color     = new Color(0.78f, 0.88f, 1f);
        FontProvider.Apply(collectLblTxt);

        var updater = row.AddComponent<WorkshopCardUpdater>();
        updater.Init(typeSlots, slotBgs, slotLabels, slotBtns, resCountTxt, collectImg, collectCntTxt, slotIcons);
    }

    // ── 출력 선택 팝업 ────────────────────────────────────────────────

    void BuildOutputPopup(Transform canvasParent)
    {
        // 별도 Canvas에 높은 sortingOrder로 항상 최상단 렌더링
        var popupCanvas = MakeCanvas("OutputPopupCanvas", 20);

        // 딤 배경 (전체 화면 반투명)
        _outputDim = MakeRectGO("Dim", popupCanvas.transform);
        _outputDim.SetActive(false);
        var dimRT = _outputDim.GetComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero; dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = dimRT.offsetMax = Vector2.zero;
        var dimImg = _outputDim.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.60f);
        var dimBtn = _outputDim.AddComponent<UnityEngine.UI.Button>();
        dimBtn.targetGraphic = dimImg;
        dimBtn.onClick.AddListener(() => { _outputPopup.SetActive(false); _outputDim.SetActive(false); });

        // 팝업 패널 — 화면 중앙
        _outputPopup = MakeRectGO("Popup", popupCanvas.transform);
        _outputPopup.SetActive(false);

        var rt = _outputPopup.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(640f, 0f);   // 높이 자동

        var bg = _outputPopup.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.12f, 0.20f, 0.97f);
        var ol = _outputPopup.AddComponent<Outline>();
        ol.effectColor    = new Color(0.40f, 0.58f, 0.90f, 0.85f);
        ol.effectDistance = new Vector2(1.5f, 1.5f);

        var vl = _outputPopup.AddComponent<VerticalLayoutGroup>();
        vl.padding                = new RectOffset(16, 16, 14, 14);
        vl.spacing                = 10f;
        vl.childControlWidth      = true;
        vl.childForceExpandWidth  = true;
        vl.childControlHeight     = true;
        vl.childForceExpandHeight = false;
        _outputPopup.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // 제목
        var titleGO = MakeRectGO("Title", _outputPopup.transform);
        titleGO.AddComponent<LayoutElement>().preferredHeight = 32f;
        _popupTitleTxt = titleGO.AddComponent<TextMeshProUGUI>();
        _popupTitleTxt.text      = "물품 선택";
        _popupTitleTxt.fontSize  = 20f;
        _popupTitleTxt.fontStyle = FontStyles.Bold;
        _popupTitleTxt.alignment = TextAlignmentOptions.Center;
        _popupTitleTxt.color     = new Color(0.70f, 0.85f, 1f);
        FontProvider.Apply(_popupTitleTxt);

        // 버튼 A / B
        (_popupBtnAImg, _popupBtnATxt) = MakePopupChoiceBtn("BtnA");
        (_popupBtnBImg, _popupBtnBTxt) = MakePopupChoiceBtn("BtnB");

        // 닫기
        var closeGO = MakeRectGO("CloseBtn", _outputPopup.transform);
        closeGO.AddComponent<LayoutElement>().preferredHeight = 40f;
        var closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.18f, 0.20f, 0.28f);
        var closeBtn = closeGO.AddComponent<UnityEngine.UI.Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(() => { _outputPopup.SetActive(false); _outputDim.SetActive(false); });
        var closeLblGO = MakeRectGO("Label", closeGO.transform);
        FillRTWithInset(closeLblGO.GetComponent<RectTransform>(), 0, 0, 0, 0);
        var closeTxt = closeLblGO.AddComponent<TextMeshProUGUI>();
        closeTxt.text      = "닫기";
        closeTxt.fontSize  = 17f;
        closeTxt.alignment = TextAlignmentOptions.Center;
        closeTxt.color     = new Color(0.58f, 0.62f, 0.72f);
        FontProvider.Apply(closeTxt);
    }

    (Image img, TMP_Text txt) MakePopupChoiceBtn(string name)
    {
        var btnGO = MakeRectGO(name, _outputPopup.transform);
        btnGO.AddComponent<LayoutElement>().preferredHeight = 58f;
        var img = btnGO.AddComponent<Image>();
        img.color = new Color(0.14f, 0.18f, 0.30f);
        var btn = btnGO.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;
        var lblGO = MakeRectGO("Label", btnGO.transform);
        FillRTWithInset(lblGO.GetComponent<RectTransform>(), 8f, 8f, 8f, 8f);
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "-";
        tmp.fontSize  = 20f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        FontProvider.Apply(tmp);
        return (img, tmp);
    }

    void ShowOutputPopup(FacilityType type, ResourceType primary, ResourceType secondary,
                         TMP_Text resNameTxtRef)
    {
        if (_outputPopup == null) return;
        _outputDim.SetActive(true);
        _outputPopup.SetActive(true);
        _popupTitleTxt.text = FacilityKorName(type);
        _popupBtnATxt.text  = ResourceKorName(primary);
        _popupBtnBTxt.text  = ResourceKorName(secondary);

        var capturedType = type;
        var capturedP    = primary;
        var capturedS    = secondary;
        var capturedRef  = resNameTxtRef;

        void HighlightA(bool aActive)
        {
            _popupBtnAImg.color = aActive
                ? new Color(0.18f, 0.45f, 0.75f)
                : new Color(0.14f, 0.18f, 0.30f);
            _popupBtnBImg.color = aActive
                ? new Color(0.14f, 0.18f, 0.30f)
                : new Color(0.18f, 0.45f, 0.75f);
        }

        HighlightA(FindFirstActiveOutput(type) == primary);

        var btnA = _popupBtnAImg.GetComponent<UnityEngine.UI.Button>();
        var btnB = _popupBtnBImg.GetComponent<UnityEngine.UI.Button>();
        btnA.onClick.RemoveAllListeners();
        btnB.onClick.RemoveAllListeners();
        btnA.onClick.AddListener(() =>
        {
            FactoryManager.Instance?.SetRefineryOutput(capturedType, capturedP);
            if (capturedRef != null) capturedRef.text = ResourceKorName(capturedP);
            HighlightA(true);
        });
        btnB.onClick.AddListener(() =>
        {
            FactoryManager.Instance?.SetRefineryOutput(capturedType, capturedS);
            if (capturedRef != null) capturedRef.text = ResourceKorName(capturedS);
            HighlightA(false);
        });
    }

    // ── 일반 / 고급 제작소 행 (시설 행과 동일한 5슬롯 레이아웃) ────

    void BuildRecipeCard(RectTransform parent, int slotIndex, RecipeCategory cat, string title)
    {
        bool isSynth = cat == RecipeCategory.Synthesis;

        // ── 행 컨테이너 ──────────────────────────────────────────────
        var row   = MakeRectGO($"RecipeRow_{title}", parent);
        var rowLE = row.AddComponent<LayoutElement>();
        rowLE.minHeight = rowLE.preferredHeight = RowH;
        row.AddComponent<Image>().color = isSynth
            ? new Color(0.11f, 0.14f, 0.22f)
            : new Color(0.13f, 0.11f, 0.20f);
        var ol = row.AddComponent<Outline>();
        ol.effectColor    = isSynth
            ? new Color(0.35f, 0.55f, 0.90f, 0.80f)
            : new Color(0.65f, 0.45f, 0.90f, 0.80f);
        ol.effectDistance = new Vector2(1.5f, 1.5f);

        var hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.padding                = new RectOffset(8, 8, 8, 8);
        hl.spacing                = 8f;
        hl.childControlWidth      = true;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight     = true;
        hl.childForceExpandHeight = true;

        // ── 왼쪽: 원형 (레시피 선택) ─────────────────────────────────
        var circleGO  = MakeRectGO("Circle", row.transform);
        var circleLE  = circleGO.AddComponent<LayoutElement>();
        circleLE.preferredWidth = CircleW;
        circleLE.flexibleWidth  = 0f;

        var circleBg = circleGO.AddComponent<Image>();
        circleBg.sprite = MakeCircleSprite(64);
        circleBg.color  = isSynth
            ? new Color(0.12f, 0.18f, 0.34f)
            : new Color(0.22f, 0.12f, 0.30f);

        var typeLblGO  = MakeRectGO("TypeName", circleGO.transform);
        var typeLblRT  = typeLblGO.GetComponent<RectTransform>();
        typeLblRT.anchorMin = new Vector2(0f, 0.72f);
        typeLblRT.anchorMax = new Vector2(1f, 1f);
        typeLblRT.offsetMin = new Vector2(4f, -4f);
        typeLblRT.offsetMax = new Vector2(-4f, -4f);
        var typeLblTxt    = typeLblGO.AddComponent<TextMeshProUGUI>();
        typeLblTxt.text   = title;
        typeLblTxt.fontSize  = 10f;
        typeLblTxt.alignment = TextAlignmentOptions.Center;
        typeLblTxt.color     = isSynth
            ? new Color(0.50f, 0.65f, 0.95f)
            : new Color(0.75f, 0.55f, 0.95f);
        FontProvider.Apply(typeLblTxt);

        // 원 중앙: 레시피명 (updater가 갱신)
        var recipeGO  = MakeRectGO("RecipeName", circleGO.transform);
        var recipeRT  = recipeGO.GetComponent<RectTransform>();
        recipeRT.anchorMin = new Vector2(0f, 0.25f);
        recipeRT.anchorMax = new Vector2(1f, 0.72f);
        recipeRT.offsetMin = recipeRT.offsetMax = Vector2.zero;
        var nameTxt    = recipeGO.AddComponent<TextMeshProUGUI>();
        nameTxt.text   = "선택";
        nameTxt.fontSize  = 12f;
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.alignment = TextAlignmentOptions.Center;
        nameTxt.color     = new Color(0.42f, 0.42f, 0.52f);
        FontProvider.Apply(nameTxt);

        var circleBtn = circleGO.AddComponent<UnityEngine.UI.Button>();
        circleBtn.targetGraphic = circleBg;
        int capturedIdx = slotIndex;
        var capturedCat = cat;
        circleBtn.onClick.AddListener(() =>
        {
            if (SynthesisManager.Instance != null)
                RecipePickerPopup.Show(SynthesisManager.Instance.GetSlot(capturedIdx), capturedCat);
        });

        // ── 가운데: 생명체 칸 5개 — 제작소 레벨만큼 사용, 다음 칸은 확장 버튼 ──
        var crewLabels = new TMP_Text[MaxSlots];
        var crewIcons  = new Image[MaxSlots];
        var crewBgs    = new Image[MaxSlots];

        for (int i = 0; i < MaxSlots; i++)
        {
            var slotGO = MakeRectGO($"CrewSlot_{i}", row.transform);
            var slotLE = slotGO.AddComponent<LayoutElement>();
            slotLE.preferredWidth = 0f;
            slotLE.flexibleWidth  = 1f;

            var slotImg = slotGO.AddComponent<Image>();
            slotImg.color = new Color(0.09f, 0.10f, 0.16f);
            crewBgs[i] = slotImg;

            var slotOl = slotGO.AddComponent<Outline>();
            slotOl.effectColor = isSynth
                ? new Color(0.35f, 0.42f, 0.65f, 0.55f)
                : new Color(0.55f, 0.35f, 0.65f, 0.55f);
            slotOl.effectDistance = new Vector2(1f, 1f);

            // 아이콘을 먼저 만들어 글자 뒤에 그려지게 한다
            crewIcons[i] = CreatureSlotView.CreateIcon(slotGO.transform);

            var lblGO = MakeRectGO("Label", slotGO.transform);
            FillRTWithInset(lblGO.GetComponent<RectTransform>(), 4f, 4f, 4f, 4f);
            var lblTxt = lblGO.AddComponent<TextMeshProUGUI>();
            lblTxt.fontSize           = 13f;
            lblTxt.alignment          = TextAlignmentOptions.Center;
            lblTxt.enableWordWrapping = true;
            lblTxt.text               = "—";
            FontProvider.Apply(lblTxt);
            crewLabels[i] = lblTxt;

            var btn = slotGO.AddComponent<UnityEngine.UI.Button>();
            btn.targetGraphic = slotImg;
            int capturedSynth = slotIndex;
            int capturedCrew  = i;
            btn.onClick.AddListener(() => OnCrewSlotTapped(capturedSynth, capturedCrew, title));
        }

        // ── 오른쪽: 상태 패널 (진행바 + 상태 텍스트) ─────────────────
        var statusGO = MakeRectGO("StatusPanel", row.transform);
        var statusLE = statusGO.AddComponent<LayoutElement>();
        statusLE.preferredWidth = 130f;
        statusLE.flexibleWidth  = 0f;

        statusGO.AddComponent<Image>().color = new Color(0.09f, 0.11f, 0.18f);
        var statusOl = statusGO.AddComponent<Outline>();
        statusOl.effectColor    = isSynth
            ? new Color(0.35f, 0.55f, 0.90f, 0.35f)
            : new Color(0.65f, 0.45f, 0.90f, 0.35f);
        statusOl.effectDistance = new Vector2(1f, 1f);

        var statusVL = statusGO.AddComponent<VerticalLayoutGroup>();
        statusVL.padding                = new RectOffset(6, 6, 8, 8);
        statusVL.spacing                = 6f;
        statusVL.childControlWidth      = true;
        statusVL.childForceExpandWidth  = true;
        statusVL.childControlHeight     = true;
        statusVL.childForceExpandHeight = false;

        var stGO  = MakeRectGO("Status", statusGO.transform);
        stGO.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var stTxt = stGO.AddComponent<TextMeshProUGUI>();
        stTxt.fontSize           = 13f;
        stTxt.alignment          = TextAlignmentOptions.Center;
        stTxt.enableWordWrapping = true;
        stTxt.color              = new Color(0.48f, 0.52f, 0.65f);
        FontProvider.Apply(stTxt);

        var barBgGO = MakeRectGO("BarBg", statusGO.transform);
        barBgGO.AddComponent<LayoutElement>().preferredHeight = 14f;
        barBgGO.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.16f);
        var fillGO = MakeRectGO("Fill", barBgGO.transform);
        var fillRT  = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0f, 1f);
        fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;
        fillRT.pivot     = new Vector2(0f, 0.5f);
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = isSynth
            ? new Color(0.25f, 0.55f, 0.90f)
            : new Color(0.55f, 0.28f, 0.90f);

        var sc = row.AddComponent<SynthesisCardUpdater>();
        sc.Init(nameTxt, stTxt, fillImg, slotIndex, typeLblTxt, title,
                crewLabels, crewIcons, crewBgs,
                isSynth ? new Color(0.50f, 0.65f, 0.95f) : new Color(0.75f, 0.55f, 0.95f));
    }

    /// <summary>
    /// 제작소 생명체 칸 탭.
    ///   배치된 칸 → (수면 중이면 각성제로 깨우기, 각성제가 없으면) 해제
    ///   빈 칸 → 생명체 선택 / 다음 잠긴 칸 → 확장 팝업
    /// </summary>
    static void OnCrewSlotTapped(int synthIndex, int crewIndex, string title)
    {
        if (SynthesisManager.Instance == null || FactoryManager.Instance == null) return;
        var ss = SynthesisManager.Instance.GetSlot(synthIndex);

        if (crewIndex < ss.Creatures.Count)
        {
            // 시설 슬롯과 같은 규칙: 수면 중이면 각성제 우선, 없을 때만 회수
            if (ss.IsSleeping && FactoryManager.Instance.TryReactivateSynthesisSlot(synthIndex))
                return;
            FactoryManager.Instance.UnassignCreatureFromSynthesisSlot(synthIndex, crewIndex);
        }
        else if (crewIndex < ss.Capacity)
            CreaturePickerPopup.Show(ss);
        else if (crewIndex == ss.Capacity && ss.CanUpgrade)
            SynthesisUpgradePopup.Show(ss, title);
    }

    // ── 스크롤뷰 ─────────────────────────────────────────────────────

    static ScrollRect BuildScrollView(Transform canvasParent)
    {
        var scrollGO = MakeRectGO("ScrollView", canvasParent);
        scrollGO.AddComponent<ScrollRect>();
        scrollGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(0f, NavH);
        scrollRT.offsetMax = new Vector2(0f, -StatusH);

        var vpGO = MakeRectGO("Viewport", scrollGO.transform);
        vpGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        vpGO.AddComponent<Mask>().showMaskGraphic = false;
        var vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = new Vector2(8f, 8f);
        vpRT.offsetMax = new Vector2(-8f, -8f);

        var contentGO = MakeRectGO("Content", vpGO.transform);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = Vector2.one;
        contentRT.pivot     = new Vector2(0.5f, 1f);
        contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing            = 8f;
        vlg.padding            = new RectOffset(8, 8, 8, 8);
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandHeight = false;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollGO.GetComponent<ScrollRect>();
        sr.viewport   = vpRT;
        sr.content    = contentRT;
        sr.horizontal = false;
        return sr;
    }

    // ── 디버그 버튼 ──────────────────────────────────────────────────

    static void BuildDebugButton(Transform canvasParent)
    {
        // 맨 위는 디버그 토글(+) 버튼 자리라 그 아래부터 쌓는다
        MakeDebugBtn(canvasParent, "DebugAddCreature",
            new Vector2(-16f, -84f), new Color(0.20f, 0.45f, 0.20f, 0.92f),
            "[DEBUG]\n생명체 추가", AddTestCreature);
        MakeDebugBtn(canvasParent, "DebugAddResources",
            new Vector2(-16f, -164f), new Color(0.25f, 0.30f, 0.55f, 0.92f),
            "[DEBUG]\n자원 +10", AddTestResources);
        MakeDebugBtn(canvasParent, "DebugSleepAll",
            new Vector2(-16f, -244f), new Color(0.55f, 0.35f, 0.10f, 0.92f),
            "[DEBUG]\n전체 수면 · 각성제+3", DebugSleepAll);
    }

    /// 시연용: 일하는 시설·제작소를 모두 재우고 각성제를 준다 (24시간을 기다리지 않고 수면·각성 확인)
    static void DebugSleepAll()
    {
        foreach (var slot in FindObjectsByType<FacilitySlot>(FindObjectsSortMode.None))
            if (slot.AssignedCreature != null) slot.Cycle.DebugForceSleep();
        if (SynthesisManager.Instance != null)
            foreach (var ss in SynthesisManager.Instance.Slots) ss.DebugForceSleep();
        FactoryManager.Instance?.AddAwakenItem(AwakenItemTier.Normal, 3);
    }

    static void MakeDebugBtn(Transform parent, string goName, Vector2 anchoredPos,
                              Color color, string label, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin        = new Vector2(1f, 1f);
        rect.anchorMax        = new Vector2(1f, 1f);
        rect.pivot            = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(200f, 70f);
        go.AddComponent<Image>().color = color;
        DebugUI.Register(go);   // 디버그 토글(+)로 표시
        go.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(action);
        var lbl = new GameObject("Label");
        lbl.transform.SetParent(go.transform, false);
        var lrt = lbl.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 17f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        FontProvider.Apply(tmp);
    }

    static void AddTestCreature()
    {
        if (FactoryManager.Instance == null) return;
        // 시설마다 한 종씩 — 능력치·티어·특화·아이콘이 실제 포획한 생명체와 똑같이 붙는다
        // 제로픽셀 = 픽셀 재구성소 전용, 그린빗 = 일반 제작소(재구성력), 에코바이트 = 고급 제작소(합성력)
        string[] ids = { "BinCore", "PlasVox", "CanBug", "PaperBit", "ZeroPixel", "GreenBit", "EcoByte" };
        int idx   = FactoryManager.Instance.CreatureInventory.Count % ids.Length;
        var entry = CreatureRoster.Find(ids[idx], null);
        if (entry == null) return;
        var so = CreatureRoster.CreateDefinition(entry);
        FactoryManager.Instance.AddCreatureToInventory(new CreatureInstance(so, grade: 1));
    }

    static void AddTestResources()
    {
        if (FactoryManager.Instance == null) return;
        foreach (var t in new[] {
            ResourceType.Garbage, ResourceType.Plastic, ResourceType.Glass,
            ResourceType.Metal, ResourceType.Can, ResourceType.Paper,
            ResourceType.Textile, ResourceType.PixelFragment })
            FactoryManager.Instance.AddResource(t, 10);
    }

    // ── 내비게이션 ───────────────────────────────────────────────────

    void SetupNavUI()
    {
        var canvas = MakeCanvas("NavCanvas", 5);
        var panel  = MakeRectGO("NavPanel", canvas.transform);
        var rt     = panel.GetComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = new Vector2(1f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta        = new Vector2(0f, NavH);
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
        MakeNavButton("납품",   panel.transform, SceneController.GoDelivery);
        MakeNavButton("생명체", panel.transform, SceneController.GoCreature);
        MakeNavButton("가방",   panel.transform, SceneController.GoBag);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    static ResourceType FindFirstActiveOutput(FacilityType type)
    {
        foreach (var slot in FindObjectsByType<FacilitySlot>(FindObjectsSortMode.None))
            if (slot.Definition != null && slot.Definition.facilityType == type)
                return slot.ActiveOutput;
        return default;
    }

    static string FacilityKorName(FacilityType t) => t switch
    {
        FacilityType.PollutionCollector  => "오염 집합소",
        FacilityType.DissolutionRefinery => "용해 정제소",
        FacilityType.ForgingRefinery     => "단조 정제소",
        FacilityType.CompressionRefinery => "압축 정제소",
        FacilityType.PixelReconstructor  => "픽셀 재구성소",
        _                                => t.ToString()
    };

    public static string ResourceKorName(ResourceType r) => r switch
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

    static void FillRTWithInset(RectTransform rt, float l, float r, float b, float t)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
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

    static GameObject MakeCanvas(string name, int sortOrder = 0)
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

    static UnityEngine.UI.Button MakeNavButton(string label, Transform parent, System.Action onClick)
    {
        var go = MakeRectGO(label, parent);
        go.AddComponent<Image>().color = new Color(0.15f, 0.20f, 0.32f);
        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        go.AddComponent<PixelUI.Button>();
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var tr = textGO.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 17f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
        FontProvider.Apply(tmp);
        return btn;
    }
}
