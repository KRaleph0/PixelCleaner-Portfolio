using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using PixelCleaners;

namespace PixelCleaners.Capture
{
    /// <summary>
    /// 포획 미니게임 (스타듀밸리 낚시 방식).
    /// CaptureMiniGame.Launch()로 생성 → 완료 시 자기 파괴.
    /// Phase 1: 도구 선택 / Phase 2: 미니게임
    /// </summary>
    public class CaptureMiniGame : MonoBehaviour
    {
        // 트랙 고정 크기 (기준 해상도 1080×1920 기준 픽셀)
        const float TRACK_H = 860f;
        const float TRACK_W = 110f;

        // ── 상태 ──────────────────────────────────────────────────
        CreatureInstance        creature;
        CaptureToolTier         tool;
        Action<CaptureOutcome>  onComplete;

        float aimPos;        // 0=하단, 1=상단
        float creaturePos;
        float creatureVel;
        float captureGauge; // 0..100
        float timeLeft;
        float irregularTimer;
        bool  running;

        float safeZoneHalf;
        float creatureSpeed;
        bool  irregular;

        // ── UI 참조 ───────────────────────────────────────────────
        GameObject       toolSelectPanel;
        GameObject       gamePanel;
        RectTransform    safeZoneRect;
        RectTransform    creatureRect;
        RectTransform    aimBarRect;
        RectTransform    gaugeFillRect;
        Image            aimBarImage;
        Image            safeZoneImage;
        TMP_Text         timerText;

        // ── 진입점 ────────────────────────────────────────────────

        /// 도구 선택 단계 포함 (레거시 — 현재 미사용). 결과를 성공 여부로만 돌려준다
        public static void Launch(CreatureInstance c, Action<bool> callback)
        {
            var go   = new GameObject("CaptureMiniGame");
            var game = go.AddComponent<CaptureMiniGame>();
            game.creature   = c;
            game.onComplete = outcome => callback?.Invoke(outcome == CaptureOutcome.Success);
            game.BuildUI();
            game.ShowToolSelect();
        }

        /// 도구를 미리 선택해 바로 미니게임 진입.
        /// 실패하면 생명체 티어에 따른 확률(CaptureConfig.FleeChance)로 도주 여부가 정해진다.
        public static void Launch(CreatureInstance c, CaptureToolTier selectedTool, Action<CaptureOutcome> callback)
        {
            var go   = new GameObject("CaptureMiniGame");
            var game = go.AddComponent<CaptureMiniGame>();
            game.creature   = c;
            game.tool       = selectedTool;
            game.onComplete = callback;
            game.BuildUI();
            game.SkipToGame();
        }

        void SkipToGame()
        {
            toolSelectPanel.SetActive(false);
            gamePanel.SetActive(true);
            FactoryManager.Instance?.UseTool(tool);
            StartMiniGame();
        }

        // ── UI 구성 ───────────────────────────────────────────────
        void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // 반투명 배경
            var bg = R("Bg", transform);
            Fill(bg); bg.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            toolSelectPanel = BuildToolSelectPanel(transform);
            gamePanel       = BuildGamePanel(transform);
            gamePanel.SetActive(false);
        }

        GameObject BuildToolSelectPanel(Transform parent)
        {
            var panel = R("ToolSelect", parent).gameObject;
            Anchor(panel, 0.05f, 0.2f, 0.95f, 0.8f);
            panel.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.16f, 0.97f);

            var vl = panel.AddComponent<VerticalLayoutGroup>();
            vl.padding   = new RectOffset(28, 28, 28, 28);
            vl.spacing   = 18f;
            vl.childControlWidth = vl.childForceExpandWidth = true;
            vl.childControlHeight = false;

            var title = TMP("Title", panel.transform, "포획 도구 선택", 28f, FontStyles.Bold);
            title.alignment = TextAlignmentOptions.Center;

            var info = TMP("Info", panel.transform,
                $"{creature.definition.capturedName}  [{creature.definition.rarity}]", 20f);
            info.color     = RarityColor(creature.definition.rarity);
            info.alignment = TextAlignmentOptions.Center;

            // 2×2 도구 버튼 그리드
            var grid = R("Grid", panel.transform).gameObject;
            grid.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 240f);
            var gl = grid.AddComponent<GridLayoutGroup>();
            gl.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;
            gl.cellSize        = new Vector2(455f, 110f);
            gl.spacing         = new Vector2(14f, 14f);

            foreach (CaptureToolTier t in System.Enum.GetValues(typeof(CaptureToolTier)))
            {
                bool has = FactoryManager.Instance == null || FactoryManager.Instance.HasTool(t);
                MakeToolButton(t, has, grid.transform);
            }

            return panel;
        }

        void MakeToolButton(CaptureToolTier tier, bool available, Transform parent)
        {
            var go = R($"Tool_{tier}", parent).gameObject;
            go.AddComponent<Image>().color = available
                ? new Color(0.18f, 0.24f, 0.38f)
                : new Color(0.10f, 0.10f, 0.14f);

            var btn = go.AddComponent<Button>();
            btn.interactable = available;
            if (available)
            {
                var t = tier;
                btn.onClick.AddListener(() => OnToolSelected(t));
            }

            var vl = go.AddComponent<VerticalLayoutGroup>();
            vl.childAlignment    = TextAnchor.MiddleCenter;
            vl.childControlWidth = vl.childForceExpandWidth = true;
            vl.childControlHeight = false;
            vl.padding  = new RectOffset(8, 8, 8, 8);
            vl.spacing  = 4f;

            var nameTxt  = TMP("Name",  go.transform, CaptureConfig.ToolKorName(tier), 17f, FontStyles.Bold);
            nameTxt.alignment = TextAlignmentOptions.Center;

            float bonus      = CaptureConfig.ToolBonus(tier);
            string bonusStr  = bonus > 0f ? $"Safe zone +{bonus * 100f:F0}%" : "보정 없음";
            var bonusTxt     = TMP("Bonus", go.transform, bonusStr, 14f);
            bonusTxt.alignment = TextAlignmentOptions.Center;
            bonusTxt.color   = available ? new Color(0.6f, 0.9f, 0.65f) : Color.gray;

            if (!available)
                TMP("Lock", go.transform, "보유 없음", 13f).color = Color.gray;
        }

        GameObject BuildGamePanel(Transform parent)
        {
            var panel = R("Game", parent).gameObject;
            Fill(panel.GetComponent<RectTransform>());

            // ── 헤더: 크리처명 + 타이머 ─────────────────────────
            var header = R("Header", panel.transform).gameObject;
            Anchor(header, 0f, 0.88f, 1f, 1f);
            header.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.1f, 0.92f);
            var hl = header.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(28, 28, 20, 20);
            hl.spacing = 12f;
            hl.childControlWidth = hl.childForceExpandWidth = true;
            hl.childControlHeight = false;

            var nameTxt = TMP("CName", header.transform, creature.definition.capturedName, 22f, FontStyles.Bold);
            nameTxt.color = RarityColor(creature.definition.rarity);

            timerText           = TMP("Timer", header.transform, "15s", 22f);
            timerText.alignment = TextAlignmentOptions.Right;

            // ── 포획 게이지 ──────────────────────────────────────
            var gaugeBg = R("GaugeBg", panel.transform).gameObject;
            Anchor(gaugeBg, 0.05f, 0.83f, 0.95f, 0.875f);
            gaugeBg.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f);

            var gaugeLabel = R("GaugeLabel", gaugeBg.transform).gameObject;
            Fill(gaugeLabel.GetComponent<RectTransform>());
            var gl2 = gaugeLabel.AddComponent<TextMeshProUGUI>();
            gl2.text      = "포획 게이지";
            gl2.fontSize  = 14f;
            gl2.alignment = TextAlignmentOptions.Center;
            gl2.color     = new Color(0.8f, 0.8f, 0.8f, 0.6f);
            FontProvider.Apply(gl2);

            gaugeFillRect = R("GaugeFill", gaugeBg.transform);
            gaugeFillRect.anchorMin = Vector2.zero;
            gaugeFillRect.anchorMax = new Vector2(0f, 1f);
            gaugeFillRect.pivot     = new Vector2(0f, 0.5f);
            gaugeFillRect.offsetMin = gaugeFillRect.offsetMax = Vector2.zero;
            gaugeFillRect.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.85f, 0.45f);

            // ── 수직 트랙 ────────────────────────────────────────
            var track = R("Track", panel.transform);
            track.anchorMin        = new Vector2(0.5f, 0.5f);
            track.anchorMax        = new Vector2(0.5f, 0.5f);
            track.pivot            = new Vector2(0.5f, 0.5f);
            track.anchoredPosition = new Vector2(0f, -40f);
            track.sizeDelta        = new Vector2(TRACK_W, TRACK_H);
            track.gameObject.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f);

            // Safe zone (크리처 중심의 포획 가능 영역)
            safeZoneRect = R("SafeZone", track);
            safeZoneRect.anchorMin = safeZoneRect.anchorMax = new Vector2(0.5f, 0.5f);
            safeZoneRect.pivot     = new Vector2(0.5f, 0.5f);
            safeZoneRect.sizeDelta = new Vector2(TRACK_W, 0f);
            safeZoneImage          = safeZoneRect.gameObject.AddComponent<Image>();
            safeZoneImage.color    = new Color(0.2f, 0.8f, 0.3f, 0.28f);

            // 크리처 표시
            creatureRect = R("Creature", track);
            creatureRect.anchorMin = creatureRect.anchorMax = new Vector2(0.5f, 0.5f);
            creatureRect.pivot     = new Vector2(0.5f, 0.5f);
            creatureRect.sizeDelta = new Vector2(TRACK_W - 10f, 40f);
            creatureRect.gameObject.AddComponent<Image>().color = new Color(1f, 0.85f, 0.2f);

            // 조준 바 (자이로스코프로 조작)
            aimBarRect = R("AimBar", track);
            aimBarRect.anchorMin = aimBarRect.anchorMax = new Vector2(0.5f, 0.5f);
            aimBarRect.pivot     = new Vector2(0.5f, 0.5f);
            aimBarRect.sizeDelta = new Vector2(TRACK_W + 16f, 8f);
            aimBarImage          = aimBarRect.gameObject.AddComponent<Image>();
            aimBarImage.color    = Color.white;

            // 조작 안내 (하단)
            var hint = R("Hint", panel.transform).gameObject;
            Anchor(hint, 0.05f, 0.04f, 0.95f, 0.10f);
            var hintTxt = hint.AddComponent<TextMeshProUGUI>();
            hintTxt.text      = "화면을 터치하거나 폰을 기울여 조준";
            hintTxt.fontSize  = 20f;
            hintTxt.alignment = TextAlignmentOptions.Center;
            hintTxt.color     = new Color(0.7f, 0.7f, 0.7f);
            FontProvider.Apply(hintTxt);

            return panel;
        }

        // ── 페이즈 전환 ───────────────────────────────────────────
        void ShowToolSelect()
        {
            toolSelectPanel.SetActive(true);
            gamePanel.SetActive(false);
        }

        void OnToolSelected(CaptureToolTier t)
        {
            tool = t;
            FactoryManager.Instance?.UseTool(t);
            toolSelectPanel.SetActive(false);
            gamePanel.SetActive(true);
            StartMiniGame();
        }

        void StartMiniGame()
        {
            var diff      = CaptureConfig.GetDifficulty(creature.definition.rarity);
            float bonus   = CaptureConfig.ToolBonus(tool);
            safeZoneHalf  = Mathf.Clamp01((diff.SafeZoneRatio + bonus) * 0.5f);
            creatureSpeed = diff.Speed;
            irregular     = diff.Irregular;

            aimPos         = 0.5f;
            creaturePos    = 0.5f;
            creatureVel    = creatureSpeed;
            captureGauge   = 0f;
            timeLeft       = diff.TimeLimit;
            irregularTimer = 0f;
            running        = true;

            EnableSensors();
        }

        // ── 업데이트 루프 ─────────────────────────────────────────
        void Update()
        {
            if (!running) return;

            UpdateCreature();
            UpdateAim();

            bool inZone = Mathf.Abs(aimPos - creaturePos) <= safeZoneHalf;
            captureGauge = Mathf.Clamp(captureGauge + (inZone ? 40f : -15f) * Time.deltaTime, 0f, 100f);
            timeLeft    -= Time.deltaTime;

            RefreshGameUI(inZone);

            if (captureGauge >= 100f) EndGame(true);
            else if (timeLeft <= 0f)  EndGame(false);
        }

        void UpdateCreature()
        {
            if (irregular)
            {
                irregularTimer -= Time.deltaTime;
                if (irregularTimer <= 0f)
                {
                    creatureVel    = UnityEngine.Random.value > 0.5f ? creatureSpeed : -creatureSpeed;
                    irregularTimer = UnityEngine.Random.Range(0.3f, 1.0f);
                }
            }

            creaturePos += creatureVel * Time.deltaTime;
            if (creaturePos >= 1f) { creaturePos = 1f; creatureVel = -Mathf.Abs(creatureVel); }
            if (creaturePos <= 0f) { creaturePos = 0f; creatureVel =  Mathf.Abs(creatureVel); }
        }

        void UpdateAim()
        {
            float target = ReadTilt();
            aimPos = Mathf.MoveTowards(aimPos, target, Time.deltaTime * 6.0f);
        }

        float ReadTilt()
        {
            // 터치 입력 (모바일 기본): 손가락 Y 위치를 직접 사용
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return Touchscreen.current.primaryTouch.position.ReadValue().y / Screen.height;

            // 자이로/가속도 센서 폴백
            Vector3 g = Vector3.zero;
            if (GravitySensor.current != null && GravitySensor.current.enabled)
                g = GravitySensor.current.gravity.ReadValue();
            else if (Accelerometer.current != null && Accelerometer.current.enabled)
                g = Accelerometer.current.acceleration.ReadValue();

            if (g.sqrMagnitude > 0.1f)
            {
                const float RANGE = 0.55f;
                float tilt = Mathf.Clamp(g.z / 9.8f, -RANGE, RANGE);
                return Mathf.InverseLerp(RANGE, -RANGE, tilt);
            }

            // 에디터 폴백: 마우스 Y
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue().y / Screen.height;

            return 0.5f;
        }

        void RefreshGameUI(bool inZone)
        {
            // 트랙 내 위치 (anchoredPosition = 중심 대비 오프셋)
            float cy = (creaturePos - 0.5f) * TRACK_H;
            float ay = (aimPos      - 0.5f) * TRACK_H;

            creatureRect.anchoredPosition = new Vector2(0f, cy);
            aimBarRect.anchoredPosition   = new Vector2(0f, ay);

            // 세이프존이 트랙 경계를 넘지 않도록 클램프
            float szHalfPx = safeZoneHalf * TRACK_H;
            float szTop    = Mathf.Min(cy + szHalfPx,  TRACK_H * 0.5f);
            float szBot    = Mathf.Max(cy - szHalfPx, -TRACK_H * 0.5f);
            safeZoneRect.anchoredPosition = new Vector2(0f, (szTop + szBot) * 0.5f);
            safeZoneRect.sizeDelta        = new Vector2(TRACK_W, szTop - szBot);

            safeZoneImage.color = inZone
                ? new Color(0.2f, 0.95f, 0.3f, 0.52f)
                : new Color(0.2f, 0.8f, 0.3f, 0.25f);
            aimBarImage.color = inZone ? new Color(0.3f, 1f, 0.45f) : Color.white;

            // 게이지 앵커로 fill
            gaugeFillRect.anchorMax = new Vector2(captureGauge / 100f, 1f);

            timerText.text  = $"{Mathf.CeilToInt(timeLeft)}s";
            timerText.color = timeLeft < 4f ? new Color(1f, 0.3f, 0.3f) : Color.white;
        }

        // ── 종료 ──────────────────────────────────────────────────
        void EndGame(bool success)
        {
            running = false;
            DisableSensors();

            // 결과 문구("놓쳤다" / "도망쳤다")를 띄우기 전에 도주 여부를 정해야 한다
            CaptureOutcome outcome;
            if (success)
                outcome = CaptureOutcome.Success;
            else
            {
                int tier = creature?.definition != null ? creature.definition.tier : 1;
                outcome = UnityEngine.Random.value < CaptureConfig.FleeChance(tier)
                    ? CaptureOutcome.Fled
                    : CaptureOutcome.Escaped;
            }
            StartCoroutine(ShowResultAndFinish(outcome));
        }

        IEnumerator ShowResultAndFinish(CaptureOutcome outcome)
        {
            // 결과 오버레이 — Image와 TMP는 별도 GO로 분리
            var overlay = R("Result", transform).gameObject;
            Anchor(overlay, 0.05f, 0.35f, 0.95f, 0.65f);
            overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

            var txtGO = R("ResultText", overlay.transform).gameObject;
            Fill(txtGO.GetComponent<RectTransform>());
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.text = outcome switch
            {
                CaptureOutcome.Success => "포획 성공!",
                CaptureOutcome.Escaped => "놓쳤다!\n<size=60%>다시 시도할 수 있다</size>",
                _                      => "도망쳤다!",
            };
            txt.richText  = true;
            txt.fontSize  = 52f;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = outcome switch
            {
                CaptureOutcome.Success => new Color(0.3f, 1f, 0.5f),
                CaptureOutcome.Escaped => new Color(1f, 0.75f, 0.3f),
                _                      => new Color(1f, 0.35f, 0.35f),
            };
            FontProvider.Apply(txt);

            yield return new WaitForSeconds(1.6f);
            onComplete?.Invoke(outcome);
            Destroy(gameObject);
        }

        // ── 센서 ──────────────────────────────────────────────────
        void EnableSensors()
        {
            if (GravitySensor.current  != null) InputSystem.EnableDevice(GravitySensor.current);
            if (Accelerometer.current  != null) InputSystem.EnableDevice(Accelerometer.current);
        }

        void DisableSensors() { /* 다른 시스템이 공유할 수 있어 비활성화하지 않음 */ }

        void OnDestroy() { running = false; }

        // ── UI 헬퍼 ───────────────────────────────────────────────
        static RectTransform R(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Anchor(GameObject go, float x0, float y0, float x1, float y1)
        {
            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static TMP_Text TMP(string name, Transform parent, string text, float size,
                            FontStyles style = FontStyles.Normal)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, size + 10f);
            var t  = go.AddComponent<TextMeshProUGUI>();
            t.text      = text;
            t.fontSize  = size;
            t.fontStyle = style;
            t.color     = Color.white;
            t.alignment = TextAlignmentOptions.Left;
            FontProvider.Apply(t);
            return t;
        }

        static Color RarityColor(CreatureRarity r) => r switch
        {
            CreatureRarity.Common    => new Color(0.5f, 1f, 0.5f),
            CreatureRarity.Rare      => new Color(0.4f, 0.6f, 1f),
            CreatureRarity.Epic      => new Color(0.8f, 0.4f, 1f),
            CreatureRarity.Legendary => new Color(1f, 0.75f, 0.1f),
            CreatureRarity.Pixel     => Color.white,
            _                        => Color.white
        };
    }
}
