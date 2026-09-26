using PixelCleaners;

namespace PixelCleaners.Capture
{
    public enum CaptureToolTier { Basic = 0, Enhanced = 1, Precision = 2, Pixel = 3 }

    /// <summary>포획 미니게임 결과.</summary>
    public enum CaptureOutcome
    {
        /// 포획 성공
        Success,
        /// 실패했지만 도망가지 않음 — 그 자리에서 다시 도전할 수 있다
        Escaped,
        /// 실패 후 도주 — 지도로 강제 이동, 지도의 해당 스폰 삭제
        Fled,
    }

    public static class CaptureConfig
    {
        // ── 도주 확률 (포켓몬 GO식) ─────────────────────────────────
        // 미니게임에 실패했을 때 이 확률로 도망간다. 높은 티어일수록 잘 도망간다.
        public const float FleeChanceTier1   = 0.20f;
        public const float FleeChanceTier2   = 0.35f;
        public const float FleeChanceSpecial = 0.50f;   // 특수(tier 0) — 제로픽셀

        /// <param name="tier">CreatureSO.tier (1·2 = 티어, 0 = 특수)</param>
        public static float FleeChance(int tier) => tier switch
        {
            2 => FleeChanceTier2,
            0 => FleeChanceSpecial,
            _ => FleeChanceTier1
        };

        public struct DifficultyData
        {
            public float SafeZoneRatio; // safe zone 높이 비율 (트랙 전체 대비 0-1)
            public float Speed;         // 크리처 이동 속도 (정규화 단위/초)
            public float TimeLimit;
            public bool  Irregular;     // Pixel 등급 불규칙 이동
        }

        public static DifficultyData GetDifficulty(CreatureRarity rarity) => rarity switch
        {
            CreatureRarity.Common    => new DifficultyData { SafeZoneRatio = 0.40f, Speed = 0.20f, TimeLimit = 15f },
            CreatureRarity.Rare      => new DifficultyData { SafeZoneRatio = 0.30f, Speed = 0.32f, TimeLimit = 12f },
            CreatureRarity.Epic      => new DifficultyData { SafeZoneRatio = 0.20f, Speed = 0.48f, TimeLimit = 10f },
            CreatureRarity.Legendary => new DifficultyData { SafeZoneRatio = 0.12f, Speed = 0.65f, TimeLimit =  8f },
            CreatureRarity.Pixel     => new DifficultyData { SafeZoneRatio = 0.08f, Speed = 0.70f, TimeLimit = 10f, Irregular = true },
            _                        => new DifficultyData { SafeZoneRatio = 0.40f, Speed = 0.20f, TimeLimit = 15f }
        };

        public static float ToolBonus(CaptureToolTier tier) => tier switch
        {
            CaptureToolTier.Enhanced  => 0.10f,
            CaptureToolTier.Precision => 0.25f,
            CaptureToolTier.Pixel     => 0.40f,
            _                         => 0f
        };

        public static string ToolKorName(CaptureToolTier tier) => tier switch
        {
            CaptureToolTier.Basic     => "기본 포획구",
            CaptureToolTier.Enhanced  => "강화 포획구",
            CaptureToolTier.Precision => "정밀 포획구",
            CaptureToolTier.Pixel     => "픽셀 포획구",
            _                         => "포획구"
        };
    }
}
