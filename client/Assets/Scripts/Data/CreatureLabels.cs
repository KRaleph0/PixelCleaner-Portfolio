using UnityEngine;

namespace PixelCleaners
{
    /// <summary>생명체 정보를 화면에 표시할 때 쓰는 공용 한글 이름·색상.</summary>
    public static class CreatureLabels
    {
        public static string StatKor(StatType s) => s switch
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

        /// 이 능력치가 필요한 시설
        public static string StatFacilityKor(StatType s) => s switch
        {
            StatType.PollutionDetection => "오염 집합소",
            StatType.Dissolution        => "용해 정제소",
            StatType.Forging            => "단조 정제소",
            StatType.Compression        => "압축 정제소",
            StatType.Reconstruction     => "일반 제작소",
            StatType.Synthesis          => "고급 제작소",
            StatType.Special            => "픽셀 재구성소",
            _                           => ""
        };

        /// 이 생명체가 활약하는 시설 (특수 능력치 = 픽셀 재구성소)
        public static string FacilityKor(CreatureSO def)
            => def != null ? StatFacilityKor(def.specialStat) : "";

        public static string TierLabel(int tier) => tier switch
        {
            1 => "1티어",
            2 => "2티어",
            0 => "특수",
            _ => $"{tier}티어"
        };

        public static string GradeLabel(int grade) => grade switch { 0 => "C", 1 => "B", 2 => "A", _ => "?" };

        public static string RarityKor(CreatureRarity r) => r switch
        {
            CreatureRarity.Common    => "일반",
            CreatureRarity.Rare      => "희귀",
            CreatureRarity.Epic      => "에픽",
            CreatureRarity.Legendary => "전설",
            CreatureRarity.Pixel     => "픽셀",
            _                        => r.ToString()
        };

        public static Color RarityColor(CreatureRarity r) => r switch
        {
            CreatureRarity.Common    => new Color(0.38f, 0.86f, 0.38f),
            CreatureRarity.Rare      => new Color(0.28f, 0.50f, 1.00f),
            CreatureRarity.Epic      => new Color(0.70f, 0.28f, 1.00f),
            CreatureRarity.Legendary => new Color(1.00f, 0.72f, 0.08f),
            CreatureRarity.Pixel     => Color.white,
            _                        => Color.gray
        };

        public static Color TierColor(int tier) => tier switch
        {
            2 => new Color(1.00f, 0.79f, 0.29f),
            0 => new Color(0.78f, 0.55f, 1.00f),
            _ => new Color(0.60f, 0.75f, 0.95f)
        };
    }
}
