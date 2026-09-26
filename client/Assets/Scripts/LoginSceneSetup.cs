using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

/// <summary>
/// 최초 실행 시 닉네임 입력 → 서버 등록 → MapScene 이동.
/// player_id가 이미 있으면 즉시 Map으로 리다이렉트.
/// </summary>
public class LoginSceneSetup : MonoBehaviour
{
    TMP_InputField nameInput;
    Button         startBtn;
    TMP_Text       startBtnTxt;
    TMP_Text       statusTxt;
    Button         offlineBtn;

    void Start()
    {
        // 이미 등록돼 있으면 UI를 만들지 않고 곧바로 넘어간다 (한 프레임 깜빡임 방지).
        if (PlayerSession.IsRegistered)
        {
            SceneController.GoMap();
            return;
        }

        SetupCamera();
        BuildUI();
    }

    // ── UI 빌드 ───────────────────────────────────────────────────

    void BuildUI()
    {
        var canvas = MakeCanvas("LoginCanvas", 0);
        var root   = canvas.transform;

        // 전체 배경
        var bg = MakeRectGO("Bg", root);
        FillRT(bg.GetComponent<RectTransform>());
        bg.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.14f);

        // ── 앱 타이틀 (상단) ─────────────────────────────────────
        var titleArea   = MakeRectGO("TitleArea", root);
        var titleAreaRT = titleArea.GetComponent<RectTransform>();
        titleAreaRT.anchorMin = new Vector2(0f, 0.56f);
        titleAreaRT.anchorMax = new Vector2(1f, 0.90f);
        titleAreaRT.offsetMin = titleAreaRT.offsetMax = Vector2.zero;

        var titleTmp = titleArea.AddComponent<TextMeshProUGUI>();
        titleTmp.text      = "PIXEL\nCLEANERS";
        titleTmp.fontSize  = 56f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color     = new Color(0.40f, 0.70f, 1.00f);
        FontProvider.Apply(titleTmp);

        // 서브타이틀
        var sub   = MakeRectGO("Sub", root);
        var subRT = sub.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0f, 0.50f);
        subRT.anchorMax = new Vector2(1f, 0.57f);
        subRT.offsetMin = subRT.offsetMax = Vector2.zero;
        var subTmp = sub.AddComponent<TextMeshProUGUI>();
        subTmp.text      = "AR 환경 정화 프로젝트";
        subTmp.fontSize  = 22f;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color     = new Color(0.50f, 0.62f, 0.78f);
        FontProvider.Apply(subTmp);

        // ── 입력 카드 ─────────────────────────────────────────────
        var card   = MakeRectGO("Card", root);
        var cardRT = card.GetComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.06f, 0.30f);
        cardRT.anchorMax = new Vector2(0.94f, 0.49f);
        cardRT.offsetMin = cardRT.offsetMax = Vector2.zero;
        card.AddComponent<Image>().color = new Color(0.12f, 0.15f, 0.20f);
        var vlg = card.AddComponent<VerticalLayoutGroup>();
        vlg.padding            = new RectOffset(28, 28, 22, 22);
        vlg.spacing            = 14f;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = false;

        var lbl    = MakeRectGO("Label", card.transform);
        lbl.AddComponent<LayoutElement>().preferredHeight = 36f;
        var lblTmp = lbl.AddComponent<TextMeshProUGUI>();
        lblTmp.text      = "닉네임";
        lblTmp.fontSize  = 20f;
        lblTmp.alignment = TextAlignmentOptions.Left;
        lblTmp.color     = new Color(0.60f, 0.76f, 0.92f);
        FontProvider.Apply(lblTmp);

        // TMP_InputField
        var inputGO = MakeRectGO("NameInput", card.transform);
        inputGO.AddComponent<LayoutElement>().preferredHeight = 76f;
        inputGO.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f);

        nameInput                = inputGO.AddComponent<TMP_InputField>();
        nameInput.characterLimit = 12;

        var textArea = MakeRectGO("Text Area", inputGO.transform);
        FillRT(textArea.GetComponent<RectTransform>());
        textArea.AddComponent<RectMask2D>();

        var ph    = MakeRectGO("Placeholder", textArea.transform);
        FillRT(ph.GetComponent<RectTransform>());
        var phTmp = ph.AddComponent<TextMeshProUGUI>();
        phTmp.text      = "닉네임 입력 (최대 12자)";
        phTmp.fontSize  = 24f;
        phTmp.fontStyle = FontStyles.Italic;
        phTmp.alignment = TextAlignmentOptions.Center;
        phTmp.color     = new Color(0.38f, 0.40f, 0.50f);
        FontProvider.Apply(phTmp);

        var inputText    = MakeRectGO("InputText", textArea.transform);
        FillRT(inputText.GetComponent<RectTransform>());
        var inputTmp     = inputText.AddComponent<TextMeshProUGUI>();
        inputTmp.fontSize  = 26f;
        inputTmp.fontStyle = FontStyles.Bold;
        inputTmp.alignment = TextAlignmentOptions.Center;
        inputTmp.color     = Color.white;
        FontProvider.Apply(inputTmp);

        nameInput.textViewport = textArea.GetComponent<RectTransform>();
        nameInput.textComponent = inputTmp;
        nameInput.placeholder   = phTmp;

        // ── 시작하기 버튼 ─────────────────────────────────────────
        var btnGO   = MakeRectGO("StartBtn", root);
        var btnRT   = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.06f, 0.19f);
        btnRT.anchorMax = new Vector2(0.94f, 0.29f);
        btnRT.offsetMin = btnRT.offsetMax = Vector2.zero;
        btnGO.AddComponent<Image>().color = new Color(0.20f, 0.55f, 0.30f);
        startBtn = btnGO.AddComponent<Button>();
        startBtn.onClick.AddListener(OnStartClicked);

        var btnLbl = MakeRectGO("Label", btnGO.transform);
        FillRT(btnLbl.GetComponent<RectTransform>());
        startBtnTxt           = btnLbl.AddComponent<TextMeshProUGUI>();
        startBtnTxt.text      = "시작하기";
        startBtnTxt.fontSize  = 30f;
        startBtnTxt.fontStyle = FontStyles.Bold;
        startBtnTxt.alignment = TextAlignmentOptions.Center;
        startBtnTxt.color     = Color.white;
        FontProvider.Apply(startBtnTxt);

        // ── 상태 텍스트 (에러/로딩) ───────────────────────────────
        var statusGO   = MakeRectGO("Status", root);
        var statusRT   = statusGO.GetComponent<RectTransform>();
        statusRT.anchorMin = new Vector2(0.06f, 0.10f);
        statusRT.anchorMax = new Vector2(0.94f, 0.19f);
        statusRT.offsetMin = statusRT.offsetMax = Vector2.zero;
        statusTxt           = statusGO.AddComponent<TextMeshProUGUI>();
        statusTxt.text      = "";
        statusTxt.fontSize  = 20f;
        statusTxt.alignment = TextAlignmentOptions.Center;
        statusTxt.color     = new Color(0.92f, 0.40f, 0.30f);
        FontProvider.Apply(statusTxt);

        // ── 오프라인 시작 (등록 실패 시에만 노출) ─────────────────
        var offGO = MakeRectGO("OfflineBtn", root);
        var offRT = offGO.GetComponent<RectTransform>();
        offRT.anchorMin = new Vector2(0.06f, 0.03f);
        offRT.anchorMax = new Vector2(0.94f, 0.10f);
        offRT.offsetMin = offRT.offsetMax = Vector2.zero;
        offGO.AddComponent<Image>().color = new Color(0.22f, 0.24f, 0.32f);
        offlineBtn = offGO.AddComponent<Button>();
        offlineBtn.onClick.AddListener(OnOfflineClicked);

        var offLbl = MakeRectGO("Label", offGO.transform);
        FillRT(offLbl.GetComponent<RectTransform>());
        var offTmp = offLbl.AddComponent<TextMeshProUGUI>();
        offTmp.text      = "서버 없이 시작 (랭킹 미집계)";
        offTmp.fontSize  = 20f;
        offTmp.alignment = TextAlignmentOptions.Center;
        offTmp.color     = new Color(0.72f, 0.76f, 0.84f);
        FontProvider.Apply(offTmp);

        offGO.SetActive(false);
    }

    // 서버가 죽어 있어도 데모가 진행되도록 하는 탈출구.
    // 로컬 임시 ID로 시작하고, 이후 납품 제출이 404를 받으면 자동 재등록된다.
    void OnOfflineClicked()
    {
        string nickname = nameInput != null ? nameInput.text.Trim() : "";
        PlayerSession.SaveOffline(string.IsNullOrEmpty(nickname) ? "나" : nickname);
        SceneController.GoMap();
    }

    // ── 시작 버튼 클릭 ────────────────────────────────────────────

    void OnStartClicked()
    {
        string nickname = nameInput != null ? nameInput.text.Trim() : "";

        if (string.IsNullOrEmpty(nickname))
        {
            ShowStatus("닉네임을 입력해 주세요", isError: true);
            return;
        }

        if (statusTxt != null) statusTxt.text = "";   // 이전 시도의 에러 메시지 제거
        SetLoading(true);
        StartCoroutine(DoRegister(nickname));
    }

    IEnumerator DoRegister(string nickname)
    {
        bool success = false;

        yield return ApiClient.Register(nickname,
            res => {
                // 서버가 display_name을 비워 보내면 입력한 닉네임을 그대로 쓴다.
                PlayerSession.Save(res.player_id,
                    string.IsNullOrEmpty(res.display_name) ? nickname : res.display_name);
                success = true;
            },
            errCode => ShowStatus("서버 연결 실패 — 재시도하거나 아래로 시작하세요", isError: true)
        );

        if (success)
        {
            SceneController.GoMap();
        }
        else
        {
            SetLoading(false);
            if (offlineBtn != null) offlineBtn.gameObject.SetActive(true);
        }
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    void SetLoading(bool loading)
    {
        if (startBtn    != null) startBtn.interactable  = !loading;
        if (startBtnTxt != null) startBtnTxt.text       = loading ? "연결 중..." : "시작하기";
        if (nameInput   != null) nameInput.interactable = !loading;
        // 여기서 statusTxt를 지우면 등록 실패 직후 SetLoading(false)가
        // 방금 띄운 에러 메시지를 곧바로 덮어쓴다. 초기화는 재시도 시점에 한다.
    }

    void ShowStatus(string msg, bool isError = false)
    {
        if (statusTxt == null) return;
        statusTxt.text  = msg;
        statusTxt.color = isError ? new Color(0.92f, 0.40f, 0.30f) : new Color(0.50f, 0.85f, 0.55f);
    }

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
}
