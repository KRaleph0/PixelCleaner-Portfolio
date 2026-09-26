using System;
using UnityEngine;

namespace PixelCleaners
{
    public enum CreatureRarity { Common, Rare, Epic, Legendary, Pixel }

    public enum StatType
    {
        PollutionDetection,
        Dissolution,
        Forging,
        Compression,
        Reconstruction,
        Synthesis,
        Special          // 특수 — 제로픽셀 전용, 픽셀 재구성소(픽셀 파편). 세이브가 int로 저장하므로 항상 맨 뒤에 추가
    }

    [Serializable]
    public class CreatureInstance
    {
        public CreatureSO definition;
        public int grade; // 0=30%, 1=100%, 2=200%+
        public bool isPurified;
        public string uniqueId;

        public CreatureInstance(CreatureSO def, int grade)
        {
            definition = def;
            this.grade = Mathf.Clamp(grade, 0, 2);
            isPurified = false;
            uniqueId = Guid.NewGuid().ToString();
        }

        public float GetEfficiency() => grade switch
        {
            0 => 0.3f,
            1 => 1.0f,
            2 => 2.0f,
            _ => 1.0f
        };

        /// 2티어 생명체의 능력치 보너스. 쿨타임은 0.90^power 이므로
        /// +2 = 사이클 ×0.81 = 생산량 약 +23% (능력치 구간과 무관하게 동일 비율).
        /// 2티어 B등급(Common 기준 power 4)이 1티어 A등급과 같아진다.
        public const int Tier2StatBonus = 2;

        public static int TierBonus(int tier) => tier == 2 ? Tier2StatBonus : 0;

        // 희귀도 × 등급 × 티어 기반 스탯 파워 (지수 쿨타임 감소에 사용)
        // 범위: Common-C-1티어 = 1 ~ Pixel-A-2티어 = 23
        public int GetStatPower()
        {
            int rarityBase = definition.rarity switch
            {
                CreatureRarity.Common    => 1,
                CreatureRarity.Rare      => 4,
                CreatureRarity.Epic      => 8,
                CreatureRarity.Legendary => 13,
                CreatureRarity.Pixel     => 18,
                _                        => 1
            };
            int gradeBonus = grade switch { 0 => 0, 1 => 1, 2 => 3, _ => 0 };
            return rarityBase + gradeBonus + TierBonus(definition.tier);
        }
    }
}
