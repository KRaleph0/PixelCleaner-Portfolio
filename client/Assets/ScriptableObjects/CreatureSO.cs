using UnityEngine;

namespace PixelCleaners
{
    [CreateAssetMenu(fileName = "New Creature", menuName = "PixelCleaners/Creature")]
    public class CreatureSO : ScriptableObject
    {
        [Header("Names")]
        public string capturedName;
        public string purifiedName;

        [Header("Stats")]
        public StatType specialStat;
        public CreatureRarity rarity;
        [Tooltip("1·2 = 티어, 0 = 특수. 2티어는 능력치 보너스를 받는다 (CreatureInstance.TierBonus)")]
        [Range(0, 2)] public int tier = 1;
        [Tooltip("특화 생산 자원 — 컨셉 표시용 (캔 몸통 → 캔). 생산 속도에는 영향 없음")]
        public bool         hasSpecialty;
        public ResourceType specialtyResource;

        [Header("Spawn")]
        [Range(0f, 1f)] public float baseSpawnWeight = 0.1f;
        public bool isPixelExclusive;

        [Header("Assets")]
        public GameObject prefab;
        public Sprite icon;
    }
}
