using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PixelCleaners;

namespace PixelCleaners.UI
{
    /// <summary>
    /// 일반·고급 제작소 카드 한 줄 갱신: 레시피명·진행바·상태(수면 포함) + 생명체 칸 5개.
    ///
    /// 생명체 칸 i 의 상태
    ///   i &lt; 배치 수        → 정면샷 + "탭: 해제" (수면 중이면 "탭=각성제")
    ///   i &lt; 레벨(정원)     → "배치"
    ///   i == 레벨          → "+ 확장" (탭하면 확장 팝업)
    ///   그 외              → 잠김 "—"
    /// </summary>
    public class SynthesisCardUpdater : MonoBehaviour
    {
        TMP_Text   nameTxt;
        TMP_Text   statusTxt;
        Image      progressImg;
        TMP_Text   titleTxt;
        string     title;
        int        slotIndex;

        TMP_Text[] crewLabels;
        Image[]    crewIcons;
        Image[]    crewBgs;
        Color      accent;

        static readonly Color ColFilled   = new Color(0.16f, 0.20f, 0.28f);
        static readonly Color ColEmpty    = new Color(0.16f, 0.18f, 0.26f);
        static readonly Color ColUpgrade  = new Color(0.14f, 0.22f, 0.16f);
        static readonly Color ColLocked   = new Color(0.09f, 0.10f, 0.16f);
        static readonly Color ColSleeping = new Color(0.45f, 0.28f, 0.08f);

        public void Init(TMP_Text recipeName, TMP_Text status, Image progress, int synthSlotIndex,
                         TMP_Text titleLabel, string titleText,
                         TMP_Text[] labels, Image[] icons, Image[] bgs, Color accentColor)
        {
            nameTxt     = recipeName;
            statusTxt   = status;
            progressImg = progress;
            slotIndex   = synthSlotIndex;
            titleTxt    = titleLabel;
            title       = titleText;
            crewLabels  = labels;
            crewIcons   = icons;
            crewBgs     = bgs;
            accent      = accentColor;
        }

        void Update()
        {
            if (SynthesisManager.Instance == null || nameTxt == null) return;
            if (slotIndex >= SynthesisManager.SlotCount) return;
            var slot = SynthesisManager.Instance.GetSlot(slotIndex);

            if (titleTxt != null) titleTxt.text = $"{title}  Lv{slot.Level}";

            UpdateCrew(slot);
            UpdateRecipe(slot);
        }

        void UpdateCrew(SynthesisSlot slot)
        {
            if (crewLabels == null) return;
            bool canAfford = FactoryManager.Instance != null &&
                             FactoryManager.Instance.CanAffordSynthesisUpgrade(slotIndex);

            for (int i = 0; i < crewLabels.Length; i++)
            {
                var label = crewLabels[i];
                if (label == null) continue;
                var icon = crewIcons != null && i < crewIcons.Length ? crewIcons[i] : null;
                var bg   = crewBgs   != null && i < crewBgs.Length   ? crewBgs[i]   : null;

                if (i < slot.Creatures.Count)
                {
                    if (slot.IsSleeping)
                    {
                        CreatureSlotView.Apply(icon, label, slot.Creatures[i],
                                               HasAwakenItem ? "수면중\n탭=각성제" : "수면중", "",
                                               new Color(1f, 0.72f, 0.30f), Color.gray);
                        if (bg != null) bg.color = ColSleeping;
                    }
                    else
                    {
                        CreatureSlotView.Apply(icon, label, slot.Creatures[i], "탭: 해제", "",
                                               new Color(0.25f, 0.90f, 0.55f), Color.gray);
                        if (bg != null) bg.color = ColFilled;
                    }
                }
                else if (i < slot.Capacity)
                {
                    CreatureSlotView.Apply(icon, label, null, "", "배치",
                                           Color.white, new Color(0.40f, 0.42f, 0.52f));
                    if (bg != null) bg.color = ColEmpty;
                }
                else if (i == slot.Capacity && slot.CanUpgrade)
                {
                    // 재료가 모이면 초록으로 강조
                    CreatureSlotView.Apply(icon, label, null, "", $"+ 확장\nLv{slot.Level + 1}",
                                           Color.white, canAfford ? new Color(0.45f, 0.95f, 0.55f) : accent);
                    if (bg != null) bg.color = canAfford ? ColUpgrade : ColLocked;
                }
                else
                {
                    CreatureSlotView.Apply(icon, label, null, "", "—",
                                           Color.white, new Color(0.22f, 0.24f, 0.30f));
                    if (bg != null) bg.color = ColLocked;
                }
            }
        }

        void UpdateRecipe(SynthesisSlot slot)
        {
            if (slot.Recipe == null)
            {
                nameTxt.text  = "레시피 선택";
                nameTxt.color = new Color(0.42f, 0.42f, 0.52f);
                if (statusTxt != null) statusTxt.text = "";
                if (progressImg != null) progressImg.rectTransform.anchorMax = new Vector2(0f, 1f);
                return;
            }

            nameTxt.text  = slot.Recipe.name;
            nameTxt.color = Color.white;

            if (statusTxt != null)
            {
                if (slot.RequiresCreature && !slot.HasCreature)
                {
                    statusTxt.text  = "생명체 필요 · 정지";
                    statusTxt.color = new Color(0.90f, 0.65f, 0.20f);
                }
                else if (slot.IsSleeping)
                {
                    statusTxt.text  = HasAwakenItem ? "수면 중\n생명체 탭 = 각성제" : "수면 중\n각성제 필요";
                    statusTxt.color = new Color(1f, 0.62f, 0.20f);
                }
                else if (slot.CanCraft)
                {
                    string crew = slot.Creatures.Count > 1 ? $"제작 중 · {slot.Creatures.Count}마리" : "제작 중";
                    statusTxt.text  = $"{crew}\n<size=85%>활동 {FormatWork(slot.WorkRemaining)}</size>";
                    statusTxt.color = new Color(0.25f, 0.90f, 0.55f);
                }
                else
                {
                    statusTxt.text  = "재료 부족 · 대기";
                    statusTxt.color = new Color(0.90f, 0.40f, 0.30f);
                }
            }

            if (progressImg != null)
                progressImg.rectTransform.anchorMax = new Vector2(slot.Progress, 1f);
        }

        static bool HasAwakenItem =>
            FactoryManager.Instance != null && FactoryManager.Instance.TotalAwakenItems > 0;

        static string FormatWork(float sec)
        {
            int h = (int)(sec / 3600f);
            int m = (int)((sec % 3600f) / 60f);
            return h > 0 ? $"{h}시간 {m:D2}분" : $"{m}분";
        }
    }
}
