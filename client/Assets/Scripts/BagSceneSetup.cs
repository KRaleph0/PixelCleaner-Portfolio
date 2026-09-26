using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;
using PixelCleaners.Capture;

/// <summary>
/// 가방(인벤토리) 씬. 보유 아이템 및 포획 도구를 표시합니다.
/// </summary>
public class BagSceneSetup : MonoBehaviour
{
    Transform contentRoot;
    System.Action refreshHandler;  // OnDestroy에서 구독 해제용

    void Start()
    {
        SetupCamera();
        SetupUI();
    }

    void OnDestroy()
    {
        if (FactoryManager.Instance != null && refreshHandler != null)
            FactoryManager.Instance.OnInventoryChanged -= refreshHandler;
    }

    void SetupCamera()
    {
        if (Camera.main != null) return;
        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
        cam.orthographic    = true;
        cam.tag             = "MainCamera";
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    void SetupUI()
    {
        var canvasGO = MakeCanvas("BagCanvas");
        var canvasTr = canvasGO.transform;

        // 타이틀
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasTr, false);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin        = new Vector2(0f, 1f);
        titleRect.anchorMax        = new Vector2(1f, 1f);
        titleRect.pivot            = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -40f);
        titleRect.sizeDelta        = new Vector2(0f, 80f);
        var titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text      = "가방";
        titleTmp.fontSize  = 40f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color     = Color.white;
        FontProvider.Apply(titleTmp);

        // 콘텐츠 영역
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(canvasTr, false);
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.offsetMin = new Vector2(16f, 180f);   // nav 바 위
        contentRect.offsetMax = new Vector2(-16f, -130f); // 타이틀 아래
        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing           = 16f;
        vlg.padding           = new RectOffset(0, 0, 8, 8);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        contentRoot = contentGO.transform;

        BuildSection(contentRoot, "포획 도구");
        BuildToolRows(contentRoot);
        BuildSection(contentRoot, "각성제");
        BuildAwakenRows(contentRoot);

        if (FactoryManager.Instance != null)
        {
            refreshHandler = () => RefreshAll(contentRoot);
            FactoryManager.Instance.OnInventoryChanged += refreshHandler;
        }
    }

    void RefreshAll(Transform content)
    {
        foreach (Transform child in content)
            Destroy(child.gameObject);

        BuildSection(content, "포획 도구");
        BuildToolRows(content);
        BuildSection(content, "각성제");
        BuildAwakenRows(content);
    }

    void BuildToolRows(Transform parent)
    {
        if (FactoryManager.Instance == null) return;
        var tools = FactoryManager.Instance.CaptureTools;

        AddItemRow(parent, "일반 구슬",    tools[CaptureToolTier.Basic] < 0 ? "∞" : $"{tools[CaptureToolTier.Basic]}개");
        AddItemRow(parent, "강화 구슬",    $"{tools[CaptureToolTier.Enhanced]}개");
        AddItemRow(parent, "정밀 구슬",    $"{tools[CaptureToolTier.Precision]}개");
        AddItemRow(parent, "픽셀 구슬",    $"{tools[CaptureToolTier.Pixel]}개");
    }

    void BuildAwakenRows(Transform parent)
    {
        if (FactoryManager.Instance == null) return;
        var items = FactoryManager.Instance.AwakenItems;

        AddItemRow(parent, "일반 각성제",    $"{items[AwakenItemTier.Normal]}개");
        AddItemRow(parent, "상급 각성제",    $"{items[AwakenItemTier.Advanced]}개");
        AddItemRow(parent, "플로깅 각성제", $"{items[AwakenItemTier.Plogging]}개");
    }

    void BuildSection(Transform parent, string title)
    {
        var go = new GameObject("Section");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 52f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.14f, 0.19f);

        var tmp = new GameObject("Label");
        tmp.transform.SetParent(go.transform, false);
        var tr = tmp.AddComponent<RectTransform>();
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(16f, 0f); tr.offsetMax = Vector2.zero;
        var t = tmp.AddComponent<TextMeshProUGUI>();
        t.text      = title;
        t.fontSize  = 26f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Left;
        t.color     = new Color(0.7f, 0.85f, 1f);
        FontProvider.Apply(t);
    }

    void AddItemRow(Transform parent, string label, string value)
    {
        var row = new GameObject("Row");
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 64f);
        row.AddComponent<Image>().color = new Color(0.15f, 0.17f, 0.22f);

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding           = new RectOffset(20, 20, 8, 8);
        layout.childControlHeight = true;

        var labelTmp = AddTMP(row.transform, label, 22f);
        labelTmp.alignment = TextAlignmentOptions.Left;
        labelTmp.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 0f);

        var valueTmp = AddTMP(row.transform, value, 22f, FontStyles.Bold);
        valueTmp.alignment = TextAlignmentOptions.Right;
        valueTmp.color     = new Color(1f, 0.9f, 0.5f);
    }

    static TMP_Text AddTMP(Transform parent, string text, float size,
                            FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("TMP");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.fontStyle = style;
        tmp.color     = Color.white;
        FontProvider.Apply(tmp);
        return tmp;
    }

    static GameObject MakeCanvas(string name)
    {
        var go     = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        return go;
    }
}
