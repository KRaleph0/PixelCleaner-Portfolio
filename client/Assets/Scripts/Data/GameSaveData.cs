using System;
using System.Collections.Generic;
using UnityEngine;
using PixelCleaners;
using PixelCleaners.Capture;

// JsonUtility 호환 직렬화 DTO — Dictionary 대신 List<KV> 사용

[Serializable]
public class GameSaveData
{
    public long savedAtUtcTicks;

    public List<ResourceEntry>    resources    = new();
    public List<ResourceEntry>    warehouse    = new();
    public List<CaptureToolEntry> captureTools = new();

    public List<CreatureSaveData>  creatureInventory = new();
    public List<FacilitySlotSave>  facilitySlots     = new();
    public List<SynthesisSlotSave> synthesisSlots    = new();

    public int                    deliveryScore;
    public List<AwakenItemEntry>  awakenItems = new();

    // 도감 — 한 번이라도 획득한 생명체
    public List<DiscoveredEntry>  discovered  = new();

    // 지도 — 내가 잡은 스폰 (MapSpawnSystem). 스폰 자체는 위치·시간으로 계산되므로 저장하지 않는다
    public List<CaughtSpawnEntry> caughtSpawns = new();
}

[Serializable]
public class CaughtSpawnEntry
{
    public string key;          // "{cellX}_{cellY}_{slot}"
    public long   expiresTicks; // 스폰이 사라지는 시각 (UTC) — 지나면 기록도 버린다
}

[Serializable]
public class DiscoveredEntry
{
    public string id;         // CreatureRoster ID
    public long   firstTicks; // 최초 획득 UTC ticks
}

[Serializable]
public class AwakenItemEntry
{
    public string tier;   // AwakenItemTier.ToString()
    public int    count;
}

[Serializable]
public class ResourceEntry
{
    public string type;   // ResourceType.ToString()
    public int    count;
}

[Serializable]
public class CaptureToolEntry
{
    public string tier;   // CaptureToolTier.ToString()
    public int    count;
}

[Serializable]
public class CreatureSaveData
{
    public string uniqueId;
    public string creatureId;   // CreatureRoster ID ("" = 로스터 외 생명체, 예: 디버그)
    public int    tier;
    public string purifiedName;
    public string capturedName;
    public int    stat;    // (int)StatType
    public int    rarity;  // (int)CreatureRarity
    public int    grade;
}

[Serializable]
public class FacilitySlotSave
{
    public string          slotName;
    public bool            hasCreature;
    public CreatureSaveData creature     = new();
    public int             cycleState;        // (int)CycleState
    public float           workElapsed;
    public float           workDuration;
    public float           productionCycle;
    public float           productionElapsed;
    public string          activeOutput;      // ResourceType.ToString()
}

[Serializable]
public class SynthesisSlotSave
{
    public int             index;
    public string          recipeName;        // "" = 없음
    public int             level;             // 제작소 레벨 = 배치 가능 생명체 수. 0 = 구버전 세이브(→ 1)
    public List<CreatureSaveData> creatures = new();
    public float           craftTimer;
    public float           workElapsed;       // 수면 사이클: 이번 활동에서 제작한 누적 시간(초)
    public bool            sleeping;

    // 구버전 호환 — 레벨 기능 이전에는 생명체 1마리만 저장했다
    public bool            hasCreature;
    public CreatureSaveData creature     = new();
}
