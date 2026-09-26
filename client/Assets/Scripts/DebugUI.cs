using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 디버그 UI 표시 토글.
///
/// 각 씬은 디버그 버튼·상태 패널을 만들 때 <see cref="Register"/>로 등록한다.
/// 화면 오른쪽 위의 작은 "+" 버튼으로 전부 켜고 끈다. 기본은 숨김이며 PlayerPrefs에 저장된다.
/// 토글 버튼은 현재 씬에 등록된 디버그 UI가 있을 때만 보인다 (납품·가방 씬 등에는 나타나지 않음).
/// </summary>
public static class DebugUI
{
    const string PrefKey = "debug_ui_visible";

    static bool? visible;
    static readonly List<GameObject> targets = new();

    public static event Action<bool> OnChanged;

    public static bool Visible
    {
        get
        {
            if (visible == null) visible = PlayerPrefs.GetInt(PrefKey, 0) == 1;
            return visible.Value;
        }
    }

    /// <summary>디버그 전용 오브젝트 등록. 현재 표시 상태가 즉시 적용된다.</summary>
    public static void Register(GameObject go)
    {
        if (go == null) return;
        targets.RemoveAll(t => t == null);
        if (!targets.Contains(go)) targets.Add(go);
        go.SetActive(Visible);
    }

    public static void Toggle() => SetVisible(!Visible);

    public static void SetVisible(bool value)
    {
        visible = value;
        PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
        PlayerPrefs.Save();

        targets.RemoveAll(t => t == null);
        foreach (var t in targets) t.SetActive(value);
        OnChanged?.Invoke(value);
    }

    /// 파괴되지 않은 등록 대상이 있는지 (씬을 나가면 대상은 null이 된다)
    internal static bool HasTargets
    {
        get
        {
            targets.RemoveAll(t => t == null);
            return targets.Count > 0;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        visible = null;
        targets.Clear();
        OnChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateToggleButton()
    {
        if (UnityEngine.Object.FindFirstObjectByType<DebugToggleButton>() != null) return;
        var go = new GameObject("[DebugToggle]");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<DebugToggleButton>();
    }
}

/// <summary>화면 오른쪽 위의 작은 "+" 토글 버튼 (DontDestroyOnLoad).</summary>
public class DebugToggleButton : MonoBehaviour
{
    const float Size   = 56f;   // 기준 해상도 1080×1920
    const float Margin = 12f;

    Canvas        canvas;
    RectTransform buttonRT;
    Image         buttonBg;
    RectTransform labelRT;
    Rect          lastSafeArea;
    float         lastScale;

    static readonly Color ColOff = new Color(0.10f, 0.12f, 0.18f, 0.55f);
    static readonly Color ColOn  = new Color(0.75f, 0.20f, 0.12f, 0.85f);

    void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // 팝업(300)·미니게임(100) 위
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight  = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var btnGO = new GameObject("Toggle");
        btnGO.transform.SetParent(transform, false);
        buttonRT = btnGO.AddComponent<RectTransform>();
        buttonRT.anchorMin = buttonRT.anchorMax = buttonRT.pivot = Vector2.one;
        buttonRT.sizeDelta = new Vector2(Size, Size);

        buttonBg = btnGO.AddComponent<Image>();
        var btn  = btnGO.AddComponent<Button>();
        btn.targetGraphic = buttonBg;
        btn.onClick.AddListener(DebugUI.Toggle);

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(btnGO.transform, false);
        labelRT = lblGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;
        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text          = "+";
        tmp.fontSize      = 40f;
        tmp.fontStyle     = FontStyles.Bold;
        tmp.alignment     = TextAlignmentOptions.Center;
        tmp.color         = Color.white;
        tmp.raycastTarget = false;
        FontProvider.Apply(tmp);

        DebugUI.OnChanged += ApplyStyle;
        ApplyStyle(DebugUI.Visible);
        ApplySafeArea();
    }

    void OnDestroy() => DebugUI.OnChanged -= ApplyStyle;

    void LateUpdate()
    {
        // 디버그 UI가 있는 씬에서만 버튼을 보인다
        bool show = DebugUI.HasTargets;
        if (buttonRT.gameObject.activeSelf != show) buttonRT.gameObject.SetActive(show);

        // CanvasScaler는 첫 프레임 이후에 배율을 계산하므로 배율 변화도 감시한다
        if (Screen.safeArea != lastSafeArea || !Mathf.Approximately(canvas.scaleFactor, lastScale))
            ApplySafeArea();
    }

    // 켜짐: 빨간 배경 + 45° 회전해 "×" 모양 / 꺼짐: 반투명 "+"
    void ApplyStyle(bool on)
    {
        buttonBg.color = on ? ColOn : ColOff;
        labelRT.localRotation = Quaternion.Euler(0f, 0f, on ? 45f : 0f);
    }

    // 노치·상태바에 가리지 않도록 안전 영역 안쪽에 둔다
    void ApplySafeArea()
    {
        lastSafeArea = Screen.safeArea;
        lastScale    = canvas.scaleFactor;
        float scale  = lastScale > 0f ? lastScale : 1f;
        float right  = (Screen.width  - lastSafeArea.xMax) / scale;
        float top    = (Screen.height - lastSafeArea.yMax) / scale;
        buttonRT.anchoredPosition = new Vector2(-(Margin + right), -(Margin + top));
    }
}
