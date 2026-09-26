using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

/// <summary>
/// 생명체 씬.
///   도감 탭 — 로스터 13종. 획득(정화 후 인벤토리 진입)하면 해금되어 정면샷·정보가 공개된다.
///            미발견은 검은 실루엣 + ???. 발견한 카드를 누르면 상세 정보.
///   보유 탭 — 공장에 배치되지 않은 보유 생명체 목록.
/// </summary>
public class CreatureSceneSetup : MonoBehaviour
{
    enum Tab { Dex, Owned }

    Tab           current = Tab.Dex;
    Transform     canvasTr;
    GameObject    dexScroll, ownedScroll;
    RectTransform dexContent, ownedContent;
    Image         dexTabBg, ownedTabBg;
    TMP_Text      dexTabTxt, ownedTabTxt;
    GameObject    detailPopup;

    // 씬을 나갈 때 해제하기 위해 보관 (FactoryManager는 씬이 바뀌어도 살아 있다)
    Action         inventoryHandler;
    Action<string> discoveredHandler;

    static readonly Color ColBg          = new Color(0.08f, 0.10f, 0.14f);
    static readonly Color ColTabOn       = new Color(0.22f, 0.32f, 0.52f);
    static readonly Color ColTabOff      = new Color(0.12f, 0.14f, 0.20f);
    static readonly Color ColCard        = new Color(0.14f, 0.16f, 0.22f);
    static readonly Color ColCardLocked  = new Color(0.10f, 0.11f, 0.15f);
    static readonly Color ColTier2       = new Color(1.00f, 0.79f, 0.29f);
    static readonly Color ColSpecial     = new Color(0.78f, 0.55f, 1.00f);
    static readonly Color ColSub         = new Color(0.55f, 0.65f, 0.80f);
    static readonly Color ColLockedText  = new Color(0.35f, 0.37f, 0.45f);
    // 스프라이트의 투명도는 유지한 채 색만 검게 → 실루엣
    static readonly Color ColSilhouette  = new Color(0.02f, 0.03f, 0.05f, 0.92f);

    void Start()
    {
        SetupCamera();
        SetupUI();

        inventoryHandler  = Refresh;
        discoveredHandler = _ => Refresh();
        if (FactoryManager.Instance != null)
        {
            FactoryManager.Instance.OnInventoryChanged   += inventoryHandler;
            FactoryManager.Instance.OnCreatureDiscovered += discoveredHandler;
        }

        SwitchTab(Tab.Dex);
    }

    void OnDestroy()
    {
        if (FactoryManager.Instance == null) return;
        if (inventoryHandler  != null) FactoryManager.Instance.OnInventoryChanged   -= inventoryHandler;
        if (discoveredHandler != null) FactoryManager.Instance.OnCreatureDiscovered -= discoveredHandler;
    }

    void SetupCamera()
    {
        if (Camera.main != null) return;
        var cam = new GameObject("Camera").AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = ColBg;
        cam.orthographic    = true;
        cam.tag             = "MainCamera";
        cam.transform.position = new Vector3(0f, 0f, -10f);
    }

    // ── UI 골격 ─────────────────────────────────────────────────────

    void SetupUI()
    {
        canvasTr = MakeCanvas("CreatureCanvas").transform;

        var title = MakeRect("Title", canvasTr);
        title.anchorMin        = new Vector2(0f, 1f);
        title.anchorMax        = Vector2.one;
        title.pivot            = new Vector2(0.5f, 1f);
        title.anchoredPosition = new Vector2(0f, -40f);
        title.sizeDelta        = new Vector2(0f, 80f);
        AddText(title, "생명체", 40f, Color.white, FontStyles.Bold);

        // 탭 바
        var tabBar = MakeRect("TabBar", canvasTr);
        tabBar.anchorMin        = new Vector2(0f, 1f);
        tabBar.anchorMax        = Vector2.one;
        tabBar.pivot            = new Vector2(0.5f, 1f);
        tabBar.anchoredPosition = new Vector2(0f, -130f);
        tabBar.sizeDelta        = new Vector2(-32f, 80f);
        var hl = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 8f;
        hl.childControlWidth = hl.childControlHeight = true;
        hl.childForceExpandWidth = hl.childForceExpandHeight = true;

        (dexTabBg,   dexTabTxt)   = MakeTabButton(tabBar, () => SwitchTab(Tab.Dex));
        (ownedTabBg, ownedTabTxt) = MakeTabButton(tabBar, () => SwitchTab(Tab.Owned));

        // 도감: 3열 카드 그리드
        (dexScroll, dexContent) = MakeScroll("DexScroll");
        var grid = dexContent.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize        = new Vector2(320f, 400f);
        grid.spacing         = new Vector2(14f, 14f);
        grid.padding         = new RectOffset(18, 18, 12, 24);
        grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment  = TextAnchor.UpperCenter;

        // 보유: 세로 목록
        (ownedScroll, ownedContent) = MakeScroll("OwnedScroll");
        var vlg = ownedContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(16, 16, 12, 24);
        vlg.childControlWidth = true;  vlg.childForceExpandWidth  = true;
        vlg.childControlHeight = true; vlg.childForceExpandHeight = false;
    }

    void SwitchTab(Tab tab)
    {
        current = tab;
        dexScroll.SetActive(tab == Tab.Dex);
        ownedScroll.SetActive(tab == Tab.Owned);
        dexTabBg.color   = tab == Tab.Dex   ? ColTabOn : ColTabOff;
        ownedTabBg.color = tab == Tab.Owned ? ColTabOn : ColTabOff;
        Refresh();
    }

    // ── 갱신 ────────────────────────────────────────────────────────

    void Refresh()
    {
        if (dexContent == null) return;   // 씬 전환 중 파괴된 경우
        var fm = FactoryManager.Instance;

        var owned = CountOwned();
        int discoveredCount = 0;
        foreach (var e in CreatureRoster.All)
            if (fm != null && fm.IsDiscovered(e.id)) discoveredCount++;

        int invCount = fm != null ? fm.CreatureInventory.Count : 0;
        dexTabTxt.text   = $"도감  {discoveredCount}/{CreatureRoster.All.Count}";
        ownedTabTxt.text = $"보유  {invCount}";

        if (current == Tab.Dex) BuildDex(owned);
        else                    BuildOwned();
    }

    /// 인벤토리 + 공장 시설 슬롯 + 합성 슬롯에 있는 생명체를 ID별로 센다
    static Dictionary<string, int> CountOwned()
    {
        var counts = new Dictionary<string, int>();
        void Add(CreatureInstance c)
        {
            if (c?.definition == null || string.IsNullOrEmpty(c.definition.name)) return;
            counts.TryGetValue(c.definition.name, out int n);
            counts[c.definition.name] = n + 1;
        }

        var fm = FactoryManager.Instance;
        if (fm != null) foreach (var c in fm.CreatureInventory) Add(c);
        foreach (var slot in FindObjectsByType<FacilitySlot>(FindObjectsSortMode.None))
            Add(slot.AssignedCreature);
        if (SynthesisManager.Instance != null)
            foreach (var s in SynthesisManager.Instance.Slots)
                foreach (var c in s.Creatures) Add(c);
        return counts;
    }

    // ── 도감 탭 ─────────────────────────────────────────────────────

    void BuildDex(Dictionary<string, int> owned)
    {
        Clear(dexContent);
        var fm  = FactoryManager.Instance;
        var all = CreatureRoster.All;
        for (int i = 0; i < all.Count; i++)
        {
            var e = all[i];
            bool found = fm != null && fm.IsDiscovered(e.id);
            owned.TryGetValue(e.id, out int count);
            BuildDexCard(e, i + 1, found, count);
        }
    }

    void BuildDexCard(CreatureEntry e, int number, bool found, int ownedCount)
    {
        var card = MakeRect($"Card_{e.id}", dexContent);
        var bg   = card.gameObject.AddComponent<Image>();
        bg.color = found ? ColCard : ColCardLocked;

        if (found && e.tier != 1)
        {
            var ol = card.gameObject.AddComponent<Outline>();
            ol.effectColor    = e.tier == 2 ? ColTier2 : ColSpecial;
            ol.effectDistance = new Vector2(2f, -2f);
        }

        // 번호 / 보유 수
        var no = AnchoredRect("No", card, 0.05f, 0.90f, 0.60f, 0.99f);
        AddText(no, $"No.{number:00}", 18f, found ? ColSub : ColLockedText, FontStyles.Bold, TextAlignmentOptions.Left);
        if (found && ownedCount > 0)
        {
            var cnt = AnchoredRect("Owned", card, 0.45f, 0.90f, 0.95f, 0.99f);
            AddText(cnt, $"보유 {ownedCount}", 17f, ColSub, FontStyles.Normal, TextAlignmentOptions.Right);
        }

        // 정면샷 / 실루엣
        var iconArea = AnchoredRect("Icon", card, 0.08f, 0.30f, 0.92f, 0.89f);
        var sprite   = CreatureRoster.LoadIcon(e);
        if (sprite != null)
        {
            var img = iconArea.gameObject.AddComponent<Image>();
            img.sprite         = sprite;
            img.preserveAspect = true;
            img.raycastTarget  = false;
            img.color          = found ? Color.white : ColSilhouette;
        }
        else
        {
            // 아이콘 미촬영 — PixelCleaners → 생명체 아이콘 촬영
            AddText(iconArea, "?", 96f, found ? ColSub : ColLockedText, FontStyles.Bold);
        }

        // 이름 / 능력치·티어
        var nameRT = AnchoredRect("Name", card, 0.03f, 0.16f, 0.97f, 0.30f);
        AddText(nameRT, found ? e.purifiedName : "???", 26f, found ? Color.white : ColLockedText, FontStyles.Bold);

        var subRT = AnchoredRect("Sub", card, 0.03f, 0.03f, 0.97f, 0.16f);
        string sub = found ? $"{StatKorName(e.stat)} · {TierLabel(e.tier)}" : "미발견";
        AddText(subRT, sub, 18f, found ? TierColor(e.tier) : ColLockedText);

        if (found)
        {
            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var captured = e;
            btn.onClick.AddListener(() => ShowDetail(captured, number, ownedCount));
        }
    }

    // ── 상세 팝업 ───────────────────────────────────────────────────

    void ShowDetail(CreatureEntry e, int number, int ownedCount)
    {
        if (detailPopup != null) Destroy(detailPopup);

        var root = MakeRect("DetailPopup", canvasTr);
        Fill(root);
        detailPopup = root.gameObject;

        var dim = MakeRect("Dim", root);
        Fill(dim);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.75f);
        dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseDetail);

        var panel = AnchoredRect("Panel", root, 0.06f, 0.16f, 0.94f, 0.86f);
        panel.gameObject.AddComponent<Image>().color = new Color(0.11f, 0.13f, 0.19f, 0.98f);
        if (e.tier != 1)
        {
            var ol = panel.gameObject.AddComponent<Outline>();
            ol.effectColor    = e.tier == 2 ? ColTier2 : ColSpecial;
            ol.effectDistance = new Vector2(3f, -3f);
        }

        var no = AnchoredRect("No", panel, 0.06f, 0.93f, 0.94f, 0.985f);
        AddText(no, $"No.{number:00}", 22f, ColSub, FontStyles.Bold, TextAlignmentOptions.Left);

        var iconArea = AnchoredRect("Icon", panel, 0.18f, 0.50f, 0.82f, 0.93f);
        var sprite   = CreatureRoster.LoadIcon(e);
        if (sprite != null)
        {
            var img = iconArea.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
        }
        else AddText(iconArea, "?", 140f, ColSub, FontStyles.Bold);

        var info = AnchoredRect("Info", panel, 0.08f, 0.14f, 0.92f, 0.50f);
        var vl = info.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 10f;
        vl.childControlWidth = true;  vl.childForceExpandWidth  = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        InfoLine(info, e.purifiedName, 40f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center, 56f);
        InfoLine(info, $"오염 상태 이름  {e.capturedName}", 22f, ColSub, FontStyles.Normal, TextAlignmentOptions.Center, 34f);

        string tierNote = e.tier == 2 ? $"  <color=#FFC94A>(능력치 +{CreatureInstance.Tier2StatBonus})</color>" : "";
        InfoLine(info, $"{StatKorName(e.stat)} · {TierLabel(e.tier)}{tierNote}", 26f, TierColor(e.tier),
                 FontStyles.Bold, TextAlignmentOptions.Center, 40f);

        if (!string.IsNullOrEmpty(e.concept) && !e.concept.StartsWith("미정"))
            InfoLine(info, $"형태  {e.concept}", 22f, ColSub, FontStyles.Normal, TextAlignmentOptions.Center, 34f);

        if (e.specialty.HasValue)
            InfoLine(info, $"특화 생산  <color=#6FD58A>{FactorySceneSetup.ResourceKorName(e.specialty.Value)}</color>",
                     22f, ColSub, FontStyles.Normal, TextAlignmentOptions.Center, 34f);

        InfoLine(info, $"보유  {ownedCount}마리", 22f, ColSub, FontStyles.Normal, TextAlignmentOptions.Center, 34f);

        var fm = FactoryManager.Instance;
        if (fm != null && fm.Discovered.TryGetValue(e.id, out long ticks))
        {
            string date = new DateTime(ticks, DateTimeKind.Utc).ToLocalTime().ToString("yyyy.MM.dd");
            InfoLine(info, $"최초 획득  {date}", 22f, ColSub, FontStyles.Normal, TextAlignmentOptions.Center, 34f);
        }

        var close = AnchoredRect("Close", panel, 0.25f, 0.03f, 0.75f, 0.11f);
        var closeImg = close.gameObject.AddComponent<Image>();
        closeImg.color = new Color(0.25f, 0.28f, 0.38f);
        var closeBtn = close.gameObject.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseDetail);
        AddText(close, "닫기", 26f, Color.white, FontStyles.Bold);
    }

    void CloseDetail()
    {
        if (detailPopup != null) Destroy(detailPopup);
        detailPopup = null;
    }

    // ── 보유 탭 ─────────────────────────────────────────────────────

    void BuildOwned()
    {
        Clear(ownedContent);
        var inv = FactoryManager.Instance != null ? FactoryManager.Instance.CreatureInventory : null;
        if (inv == null || inv.Count == 0)
        {
            var empty = MakeRect("Empty", ownedContent);
            empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
            AddText(empty, "대기 중인 생명체가 없습니다.\n공장에 배치된 생명체는 공장에서 확인하세요.", 22f, Color.gray);
            return;
        }

        foreach (var c in inv)
            BuildOwnedRow(c);
    }

    void BuildOwnedRow(CreatureInstance c)
    {
        var row = MakeRect("CreatureRow", ownedContent);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 104f;
        row.gameObject.AddComponent<Image>().color = ColCard;

        var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(14, 16, 10, 10);
        hl.spacing = 16f;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true;  hl.childForceExpandWidth  = false;
        hl.childControlHeight = true; hl.childForceExpandHeight = true;

        var iconRT = MakeRect("Icon", row);
        var iconLE = iconRT.gameObject.AddComponent<LayoutElement>();
        iconLE.preferredWidth = iconLE.minWidth = 84f;
        var entry = CreatureRoster.Find(c.definition.name, c.definition.purifiedName);
        var sprite = c.definition.icon != null ? c.definition.icon : CreatureRoster.LoadIcon(entry);
        if (sprite != null)
        {
            var img = iconRT.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
        }

        var textCol = MakeRect("Text", row);
        textCol.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var vl = textCol.gameObject.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.MiddleLeft;
        vl.childControlWidth = true;  vl.childForceExpandWidth  = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        int tier = c.definition.tier;
        InfoLine(textCol, c.definition.purifiedName, 26f, Color.white, FontStyles.Bold, TextAlignmentOptions.Left, 38f);
        InfoLine(textCol,
            $"{StatKorName(c.definition.specialStat)} · {TierLabel(tier)} · {GradeLabel(c.grade)}등급 · 능력치 {c.GetStatPower()}",
            19f, TierColor(tier), FontStyles.Normal, TextAlignmentOptions.Left, 30f);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────

    (GameObject scroll, RectTransform content) MakeScroll(string name)
    {
        var scroll = MakeRect(name, canvasTr);
        scroll.anchorMin = Vector2.zero;
        scroll.anchorMax = Vector2.one;
        scroll.offsetMin = new Vector2(0f, 180f);    // 하단 내비 위
        scroll.offsetMax = new Vector2(0f, -220f);   // 탭 바 아래
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        scroll.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        sr.horizontal = false;

        var viewport = MakeRect("Viewport", scroll);
        Fill(viewport);
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var content = MakeRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot     = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport;
        sr.content  = content;
        return (scroll.gameObject, content);
    }

    (Image bg, TMP_Text label) MakeTabButton(RectTransform parent, UnityEngine.Events.UnityAction onClick)
    {
        var rt  = MakeRect("Tab", parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = ColTabOff;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        var txt = AddText(rt, "", 26f, Color.white, FontStyles.Bold);
        return (img, txt);
    }

    static void InfoLine(Transform parent, string text, float size, Color color, FontStyles style,
                         TextAlignmentOptions align, float height)
    {
        var rt = MakeRect("Line", parent);
        rt.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        var t = AddText(rt, text, size, color, style, align);
        t.richText = true;
    }

    static TMP_Text AddText(Transform parent, string text, float size, Color color,
                            FontStyles style = FontStyles.Normal,
                            TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var rt = MakeRect("Text", parent);
        Fill(rt);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text          = text;
        tmp.fontSize      = size;
        tmp.fontStyle     = style;
        tmp.color         = color;
        tmp.alignment     = align;
        tmp.raycastTarget = false;
        FontProvider.Apply(tmp);
        return tmp;
    }

    static RectTransform AnchoredRect(string name, Transform parent, float xMin, float yMin, float xMax, float yMax)
    {
        var rt = MakeRect(name, parent);
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static RectTransform MakeRect(string name, Transform parent)
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

    static void Clear(Transform t)
    {
        // Destroy는 프레임 끝에 처리되므로, 먼저 떼어내야 레이아웃이 옛 카드까지 세지 않는다
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    static Color TierColor(int tier) => tier == 2 ? ColTier2 : tier == 0 ? ColSpecial : ColSub;

    static string TierLabel(int tier) => tier switch { 1 => "1티어", 2 => "2티어", 0 => "특수", _ => $"{tier}티어" };

    static string GradeLabel(int g) => g switch { 0 => "C", 1 => "B", 2 => "A", _ => "?" };

    static string StatKorName(StatType s) => s switch
    {
        StatType.PollutionDetection => "오염 감지력",
        StatType.Dissolution        => "용해력",
        StatType.Forging            => "단조력",
        StatType.Compression        => "압축력",
        StatType.Reconstruction     => "재구성력",
        StatType.Synthesis          => "합성력",
        StatType.Special            => "특수",
        _                           => s.ToString()
    };

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
