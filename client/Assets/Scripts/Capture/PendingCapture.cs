using PixelCleaners;

/// <summary>지도 씬 → AR 씬으로 포획 대상 크리처 정보를 전달하는 정적 홀더.</summary>
public static class PendingCapture
{
    public static CreatureSO     Definition { get; private set; }
    public static CreatureRarity Rarity     { get; private set; }
    public static int            Grade      { get; private set; }
    public static bool           HasPending { get; private set; }
    /// <summary>크리처 풀 인덱스. -1 = 랜덤 (지도에서 지정되지 않은 경우)</summary>
    public static int            PoolIndex  { get; private set; }
    /// <summary>지도 스폰 키 (MapSpawnSystem). 포획 성공 시 이 스폰을 잡은 것으로 기록한다. null = 지도 외 출처</summary>
    public static string         SpawnKey       { get; private set; }
    /// <summary>지도 스폰이 사라지는 시각 (UTC ticks)</summary>
    public static long           SpawnEndsTicks { get; private set; }

    public static void Set(CreatureSO def, CreatureRarity rarity, int grade, int poolIndex = -1,
                           string spawnKey = null, long spawnEndsTicks = 0)
    {
        Definition     = def;
        Rarity         = rarity;
        Grade          = grade;
        PoolIndex      = poolIndex;
        SpawnKey       = spawnKey;
        SpawnEndsTicks = spawnEndsTicks;
        HasPending     = true;
    }

    public static void Clear()
    {
        Definition     = null;
        PoolIndex      = -1;
        SpawnKey       = null;
        SpawnEndsTicks = 0;
        HasPending     = false;
    }
}
