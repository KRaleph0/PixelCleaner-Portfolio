using System;

namespace PixelCleaners
{
    public enum AwakenItemTier { Normal, Advanced, Plogging }

    [Serializable]
    public class AwakenItem
    {
        public AwakenItemTier tier;

        // 재활성 효과: 항상 24시간 고정
        public const float ActivityHours = 24f;

        // 등급별 보관 유효기간 (이 시간 안에 사용하지 않으면 소멸)
        public float ShelfLifeHours => tier switch
        {
            AwakenItemTier.Normal   => 24f,
            AwakenItemTier.Advanced => 48f,
            AwakenItemTier.Plogging => 168f,
            _                       => 24f
        };

        public AwakenItem(AwakenItemTier tier) => this.tier = tier;
    }
}
