using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCleaners;
using PixelCleaners.GPS;

/// <summary>지도에 나타난 스폰 하나.</summary>
public struct MapSpawn
{
    public string         key;          // "{cellX}_{cellY}_{slot}" — 포획 기록 키
    public double         latitude;
    public double         longitude;
    public string         creatureId;
    public int            poolIndex;    // CreatureRoster.Spawnable 인덱스 = AR 스폰 풀 인덱스
    public CreatureRarity rarity;
    public int            grade;
    public long           endsTicks;    // 사라지는 시각 (UTC ticks)
}

/// <summary>
/// 포켓몬 GO식 지도 스폰.
///
/// 세계를 위경도 격자로 나누고, 칸마다 고정된 스폰 지점이 하나 있다.
/// 각 지점은 한 시간 주기 중 칸마다 정해진 분에 나타나 ActiveMinutes 동안 유지된다.
/// 무엇이 나올지(생명체·희귀도·등급·칸 안의 위치·출현 여부)는 칸 좌표와 주기 번호의 해시로 결정된다.
///
///   → 지도를 나갔다 와도, 앱을 껐다 켜도 같은 자리에 같은 생명체가 있다
///   → 모든 플레이어에게 같은 스폰이 보인다 (서버 불필요)
///   → 걸어 다니면 새로운 칸의 스폰이 보인다
///   → 오염 등급이 높은 지점일수록 출현 확률이 높다 (핫스팟 연동)
///
/// 저장할 상태는 "내가 잡은 스폰" 목록뿐이다. 잡은 스폰은 그 주기가 끝날 때까지 나에게만 숨겨진다.
/// </summary>
public static class MapSpawnSystem
{
    // ── 튜닝 ────────────────────────────────────────────────────────

    /// 격자 한 칸 크기(도). 서울 기준 위도 약 89m × 경도 약 71m
    public const double CellDegrees         = 0.0008;
    /// 스폰 주기(분). 주기가 바뀌면 같은 지점에 새 생명체가 나온다
    public const int    CycleMinutes        = 60;
    /// 한 번 나타난 스폰이 유지되는 시간(분)
    public const int    ActiveMinutes       = 30;
    /// 플레이어로부터 이 거리 안의 스폰만 지도에 올린다.
    /// 지도 카메라(50° 기울기, 세로 화면)에서 화면 모서리까지 약 207m → 약간의 여유
    public const float  VisibleRadiusMeters = 220f;

    /// 주기마다 칸에 스폰이 생길 확률 — 스폰 지점의 오염 등급별 (Normal / Polluted / Hotspot)
    static readonly float[] CellSpawnChance = { 0.35f, 0.60f, 0.90f };

    /// 세계 시드. 바꾸면 전 세계 스폰 배치가 통째로 바뀐다
    const ulong WorldSeed = 0x5049584C434C4E52UL;   // "PIXLCLNR"

    const double MetersPerDegree = 111320.0;

    // ── 상태 ────────────────────────────────────────────────────────

    // 잡은 스폰: key → 기록 만료 시각(= 그 스폰이 사라지는 시각)
    static readonly Dictionary<string, long> caught = new();

    /// 디버그용 시간 이동(분). 다음 주기 스폰을 미리 볼 때 쓴다
    public static long DebugTimeOffsetMinutes;

    /// 포획 기록이 바뀌었을 때 (지도 다시 그리기)
    public static event Action OnChanged;

    public static long NowTicks => DateTime.UtcNow.Ticks + DebugTimeOffsetMinutes * TimeSpan.TicksPerMinute;

    // 도메인 리로드를 끈 에디터 Play에서도 이전 Play의 상태가 남지 않도록
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        caught.Clear();
        DebugTimeOffsetMinutes = 0;
        OnChanged = null;
    }

    // ── 조회 ────────────────────────────────────────────────────────

    /// <summary>플레이어 주변에서 지금 활성화된, 아직 잡지 않은 스폰.</summary>
    public static List<MapSpawn> GetSpawnsNear(Vector2 playerLatLon)
    {
        var result = new List<MapSpawn>();
        var pool   = CreatureRoster.Spawnable;
        if (pool.Count == 0) return result;

        long now    = NowTicks;
        long minute = now / TimeSpan.TicksPerMinute;
        PruneCaught(now);

        int cx0 = CellIndex(playerLatLon.y);
        int cy0 = CellIndex(playerLatLon.x);
        // 경도 방향 칸은 위도가 높을수록 좁아지므로 필요한 칸 수를 따로 계산
        double cellMetersY = CellDegrees * MetersPerDegree;
        double cellMetersX = cellMetersY * Math.Cos(playerLatLon.x * Math.PI / 180.0);
        int rangeY = (int)Math.Ceiling(VisibleRadiusMeters / cellMetersY) + 1;
        int rangeX = (int)Math.Ceiling(VisibleRadiusMeters / Math.Max(cellMetersX, 1.0)) + 1;

        for (int dy = -rangeY; dy <= rangeY; dy++)
        for (int dx = -rangeX; dx <= rangeX; dx++)
        {
            if (!TryGetSpawn(cx0 + dx, cy0 + dy, minute, pool, out var s)) continue;
            if (caught.ContainsKey(s.key)) continue;
            var pos = new Vector2((float)s.latitude, (float)s.longitude);
            if (LocationManager.DistanceMeters(playerLatLon, pos) > VisibleRadiusMeters) continue;
            result.Add(s);
        }
        return result;
    }

    // ── 포획 기록 ───────────────────────────────────────────────────

    /// <summary>포획 성공 — 이 스폰은 사라질 시각까지 나에게 보이지 않는다.</summary>
    public static void MarkCaught(string key, long endsTicks)
    {
        if (string.IsNullOrEmpty(key)) return;
        // 만료 시각을 모르면 한 주기 뒤로 넉넉히
        caught[key] = endsTicks > 0 ? endsTicks : NowTicks + CycleMinutes * TimeSpan.TicksPerMinute;
        OnChanged?.Invoke();
    }

    /// <summary>도주 — 포획과 똑같이 그 스폰을 사라질 시각까지 지도에서 지운다.</summary>
    public static void MarkFled(string key, long endsTicks) => MarkCaught(key, endsTicks);

    public static IEnumerable<KeyValuePair<string, long>> CaughtEntries
    {
        get { PruneCaught(NowTicks); return caught; }
    }

    /// 저장 복원용
    public static void RestoreCaught(IEnumerable<KeyValuePair<string, long>> entries)
    {
        caught.Clear();
        if (entries != null)
            foreach (var kv in entries)
                if (!string.IsNullOrEmpty(kv.Key)) caught[kv.Key] = kv.Value;
        PruneCaught(NowTicks);
        OnChanged?.Invoke();
    }

    // ── 스폰 계산 ───────────────────────────────────────────────────

    static bool TryGetSpawn(int cx, int cy, long minute, IReadOnlyList<CreatureEntry> pool, out MapSpawn spawn)
    {
        spawn = default;

        // 칸마다 주기 안의 출현 시각(분)이 다르다 — 모든 칸이 동시에 바뀌지 않도록
        int  offset    = (int)(Hash(cx, cy, -1) % (ulong)CycleMinutes);
        long shifted   = minute - offset;
        long slot      = FloorDiv(shifted, CycleMinutes);
        long intoCycle = shifted - slot * CycleMinutes;
        if (intoCycle >= ActiveMinutes) return false;

        ulong h = Hash(cx, cy, slot);

        // 칸 안의 위치 — 가장자리를 피해 15~85%
        double lat = (cy + 0.15 + 0.70 * Unit(h, 1)) * CellDegrees;
        double lon = (cx + 0.15 + 0.70 * Unit(h, 2)) * CellDegrees;

        var grade = HotspotDetector.Instance != null
            ? HotspotDetector.Instance.GradeAt(new Vector2((float)lat, (float)lon))
            : HotspotGrade.Normal;
        if (Unit(h, 3) >= CellSpawnChance[(int)grade]) return false;

        int poolIndex = PickCreature(pool, grade, Unit(h, 4));
        if (poolIndex < 0) return false;

        double r = Unit(h, 5);
        var rarity = r < 0.65 ? CreatureRarity.Common : r < 0.90 ? CreatureRarity.Rare : CreatureRarity.Epic;

        // AR 스포너(CreatureSpawner.RollGrade)와 같은 분포: A 5% / B 25% / C 70%
        double g = Unit(h, 6);
        int creatureGrade = g < 0.05 ? 2 : g < 0.30 ? 1 : 0;

        long endMinute = offset + slot * CycleMinutes + ActiveMinutes;
        spawn = new MapSpawn
        {
            key        = $"{cx}_{cy}_{slot}",
            latitude   = lat,
            longitude  = lon,
            creatureId = pool[poolIndex].id,
            poolIndex  = poolIndex,
            rarity     = rarity,
            grade      = creatureGrade,
            endsTicks  = endMinute * TimeSpan.TicksPerMinute,
        };
        return true;
    }

    /// 출현 가중치(SpawnWeight) 기반 선택. 픽셀 전용 생명체는 Hotspot 지점에서만.
    static int PickCreature(IReadOnlyList<CreatureEntry> pool, HotspotGrade grade, double roll01)
    {
        double total = 0;
        for (int i = 0; i < pool.Count; i++)
            if (Allowed(pool[i], grade)) total += pool[i].SpawnWeight;
        if (total <= 0) return -1;

        double roll = roll01 * total, cumul = 0;
        int last = -1;
        for (int i = 0; i < pool.Count; i++)
        {
            if (!Allowed(pool[i], grade)) continue;
            cumul += pool[i].SpawnWeight;
            last = i;
            if (roll < cumul) return i;
        }
        return last;
    }

    static bool Allowed(CreatureEntry e, HotspotGrade grade)
        => !e.isPixelExclusive || grade == HotspotGrade.Hotspot;

    static void PruneCaught(long now)
    {
        List<string> expired = null;
        foreach (var kv in caught)
            if (kv.Value <= now) (expired ??= new List<string>()).Add(kv.Key);
        if (expired != null)
            foreach (var k in expired) caught.Remove(k);
    }

    // ── 결정적 해시 ─────────────────────────────────────────────────
    // 기기·실행·플랫폼과 무관하게 같은 입력이면 같은 결과가 나와야 한다.
    // UnityEngine.Random / string.GetHashCode는 이 보장이 없어 쓰지 않는다.

    static int CellIndex(double degrees) => (int)Math.Floor(degrees / CellDegrees);

    static long FloorDiv(long a, long b) => a >= 0 ? a / b : -((-a + b - 1) / b);

    static ulong Hash(long a, long b, long c)
    {
        ulong h = WorldSeed;
        h = Mix(h ^ unchecked((ulong)a));
        h = Mix(h ^ unchecked((ulong)b));
        h = Mix(h ^ unchecked((ulong)c));
        return h;
    }

    /// SplitMix64
    static ulong Mix(ulong z)
    {
        unchecked
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    /// 해시에서 스트림별 독립적인 [0, 1) 값
    static double Unit(ulong h, int stream)
    {
        ulong v = Mix(unchecked(h + (ulong)stream * 0x632BE59BD9B4E019UL));
        return (v >> 11) * (1.0 / (1UL << 53));
    }
}
