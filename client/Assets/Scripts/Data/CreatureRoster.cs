using System.Collections.Generic;
using UnityEngine;

namespace PixelCleaners
{
    /// <summary>생명체 13종 중 한 종의 기획 데이터.</summary>
    public class CreatureEntry
    {
        /// 영문 ID. 전용 모델 파일명과 같다: Assets/Resources/Characters/{id}.fbx
        public string   id;
        public string   purifiedName;      // 정화명 (도감·공장 표시)
        public string   capturedName;      // 포획명 (오염 상태)
        public StatType stat;
        /// 1·2 = 티어, 0 = 특수(픽셀)
        public int      tier;
        public bool     isPixelExclusive;
        /// 특화 생산 자원 — 컨셉 표시용 (어느 공장에 넣으면 되는지 직관적으로 보여준다). 속도 보너스 없음. null = 표시 안 함
        public ResourceType? specialty;
        /// 모델을 불러올 수 없을 때 쓰는 구체 색상
        public Color    fallbackColor;
        /// 모델 정면이 +Z가 아닐 때 Y축 보정 각도(도). 다시 내보내지 않고 여기서 돌린다.
        public float    modelYawOffset;
        /// 형태 컨셉 (기획 메모)
        public string   concept;

        public float SpawnWeight => isPixelExclusive ? 0.02f : 0.15f;
    }

    /// <summary>
    /// 생명체 13종 로스터 — 생명체 정의의 단일 출처.
    /// AR 스폰 풀과 지도 몬스터가 모두 <see cref="Spawnable"/>을 같은 순서로 쓰기 때문에
    /// 지도에서 탭한 생명체와 AR에 나타나는 생명체가 항상 일치한다.
    ///
    /// 모델 추가 방법: Assets/Resources/Characters/{id}.fbx 로 넣기만 하면 된다.
    /// 코드 수정 없이 다음 실행부터 전용 모델로 스폰된다.
    ///
    /// 지도 전용 저폴리 모델: {id}_Map.prefab (Decimate 도구가 생성). 없으면 AR 모델을 그대로 쓴다.
    /// </summary>
    public static class CreatureRoster
    {
        public enum ModelVariant { AR, Map }

        /// 지도 전용 모델 파일명 접미사 — 예: CanBug_Map.prefab
        public const string MapSuffix = "_Map";

        // 능력치(= 일하는 시설)는 형태 컨셉에 맞춘다. 특화 생산 자원은 그 컨셉을 보여주는 표시용 (캔 몸통 → 캔 → 단조 정제소)
        public static readonly IReadOnlyList<CreatureEntry> All = new List<CreatureEntry>
        {
            // ── 오염 감지력 — 오염 집합소 (쓰레기) ──────────────────────
            new() { id = "BinCore",   purifiedName = "빈코어",     capturedName = "클램프",
                    stat = StatType.PollutionDetection, tier = 1, specialty = ResourceType.Garbage,
                    fallbackColor = Color.yellow,
                    concept = "쓰레기통 몸통" },
            new() { id = "WastePix",  purifiedName = "웨이스트픽스", capturedName = "패커",
                    stat = StatType.PollutionDetection, tier = 2, specialty = ResourceType.Garbage,
                    fallbackColor = new Color(0.9f, 0.8f, 0.3f),
                    concept = "비닐봉투 몸통" },

            // ── 용해력 — 용해 정제소 (플라스틱 / 유리) ───────────────────
            new() { id = "PlasVox",   purifiedName = "플라스복스", capturedName = "크래쉬",
                    stat = StatType.Dissolution, tier = 1, specialty = ResourceType.Plastic,
                    fallbackColor = Color.red,
                    concept = "페트병 몸통" },
            new() { id = "GlassNode", purifiedName = "글라스노드", capturedName = "머지",
                    stat = StatType.Dissolution, tier = 2, specialty = ResourceType.Glass,
                    fallbackColor = Color.magenta,
                    concept = "유리병 몸통" },

            // ── 단조력 — 단조 정제소 (금속 / 캔) ─────────────────────────
            new() { id = "LeafByte",  purifiedName = "리프바이트", capturedName = "코로드",
                    stat = StatType.Forging, tier = 1, specialty = ResourceType.Metal,
                    fallbackColor = Color.blue,
                    concept = "미정 (디지털 계열)" },
            new() { id = "CanBug",    purifiedName = "캔버그",     capturedName = "슬러지",
                    stat = StatType.Forging, tier = 2, specialty = ResourceType.Can,
                    fallbackColor = new Color(0.3f, 0.5f, 0.9f),
                    concept = "캔 몸통" },

            // ── 압축력 — 압축 정제소 (종이 / 섬유) ───────────────────────
            new() { id = "PaperBit",  purifiedName = "페이퍼빗",   capturedName = "스태틱",
                    stat = StatType.Compression, tier = 2, specialty = ResourceType.Paper,
                    fallbackColor = new Color(0.9f, 0.4f, 0.4f),
                    concept = "구겨진 종이 뭉치" },
            new() { id = "ThreadVox", purifiedName = "스레드복스", capturedName = "탱글",
                    stat = StatType.Compression, tier = 1, specialty = ResourceType.Textile,
                    fallbackColor = new Color(0.85f, 0.55f, 0.75f),
                    concept = "실타래 몸통" },

            // ── 재구성력 — 일반 제작소 (2차 자원 레시피) ─────────────────
            // 픽셀 파편은 제로픽셀만 생산한다 (픽셀 재구성소 전용)
            new() { id = "GreenBit",  purifiedName = "그린빗",     capturedName = "스캐터",
                    stat = StatType.Reconstruction, tier = 1,
                    fallbackColor = Color.green,
                    concept = "픽셀 큐브 + 집게팔" },
            new() { id = "EcoPix",    purifiedName = "에코픽스",   capturedName = "노이즈",
                    stat = StatType.Reconstruction, tier = 2,
                    fallbackColor = new Color(0.4f, 0.8f, 0.4f),
                    concept = "픽셀 큐브 + 하단 흡입구" },

            // ── 합성력 — 고급 제작소 (납품 물품·포획구 레시피) ────────────
            // 저장 매체 컨셉: 1티어 플로피 디스크 → 2티어 하드 드라이브 (용량이 커지는 느낌)
            new() { id = "CleanLog",  purifiedName = "클린로그",   capturedName = "프래그",
                    stat = StatType.Synthesis, tier = 1,
                    fallbackColor = Color.cyan,
                    concept = "플로피 디스크" },
            new() { id = "EcoByte",   purifiedName = "에코바이트", capturedName = "링크르",
                    stat = StatType.Synthesis, tier = 2,
                    fallbackColor = new Color(0.8f, 0.4f, 0.8f),
                    concept = "하드 드라이브" },

            // ── 특수 — 픽셀 재구성소 (픽셀 파편) ─────────────────────────
            // 픽셀 핫스팟에서만 나온다. 픽셀 재구성소(희귀 재화 픽셀 파편)는 이 생명체만 배치할 수 있다 (FacilitySO.pixelExclusiveOnly)
            new() { id = "ZeroPixel", purifiedName = "제로픽셀",   capturedName = "코어버그",
                    stat = StatType.Special, tier = 0, isPixelExclusive = true,
                    specialty = ResourceType.PixelFragment,
                    fallbackColor = Color.white,
                    concept = "미정 — 세계 오염의 근원" },
        };

        // ── 모델 해석 ────────────────────────────────────────────────

        static readonly Dictionary<string, GameObject> modelCache = new();
        static readonly Dictionary<string, Sprite>     iconCache  = new();
        static List<CreatureEntry> spawnable;

        /// 정면샷 아이콘 폴더: Assets/Resources/CreatureIcons/{id}.png
        /// 에디터 메뉴 "PixelCleaners → 생명체 아이콘 촬영"이 생성한다.
        public const string IconFolder = "CreatureIcons";

        // Enter Play Mode에서 도메인 리로드를 끈 경우에도 새로 넣은 모델·아이콘이 반영되도록 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            modelCache.Clear();
            iconCache.Clear();
            spawnable = null;
        }

        /// <summary>정면샷 아이콘. 아직 촬영하지 않았으면 null (UI는 글자로 대체 표시).</summary>
        public static Sprite LoadIcon(CreatureEntry e)
        {
            if (e == null) return null;
            if (!iconCache.TryGetValue(e.id, out var sprite))
                iconCache[e.id] = sprite = Resources.Load<Sprite>($"{IconFolder}/{e.id}");
            return sprite;
        }

        static GameObject LoadCached(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            if (!modelCache.TryGetValue(fileName, out var prefab))
                modelCache[fileName] = prefab = CharacterAssets.Load(fileName);
            return prefab;
        }

        /// <summary>
        /// (지도라면 지도 전용 모델 {id}_Map →) 전용 모델 {id} 순으로 찾는다. 없으면 null.
        /// </summary>
        public static GameObject LoadModel(CreatureEntry e, ModelVariant variant = ModelVariant.AR)
        {
            if (variant == ModelVariant.Map)
            {
                var map = LoadCached(e.id + MapSuffix);
                if (map != null) return map;
            }
            return LoadCached(e.id);
        }

        /// <summary>
        /// 실제로 스폰할 생명체 목록. 순서는 <see cref="All"/>과 같다.
        /// 모델이 있는 종만 포함한다. 모델 에셋이 통째로 없는 빌드라면 전 종을 구체로 스폰한다.
        /// </summary>
        public static IReadOnlyList<CreatureEntry> Spawnable
        {
            get
            {
                if (spawnable != null) return spawnable;

                spawnable = new List<CreatureEntry>();
                foreach (var e in All)
                    if (LoadModel(e) != null) spawnable.Add(e);

                if (spawnable.Count == 0)
                {
                    Debug.LogWarning("[CreatureRoster] 불러올 수 있는 모델이 없어 구체로 폴백합니다.");
                    spawnable.AddRange(All);
                }
                return spawnable;
            }
        }

        /// <summary>로스터 항목으로 런타임 CreatureSO를 만든다 (prefab은 호출자가 지정).</summary>
        public static CreatureSO CreateDefinition(CreatureEntry e)
        {
            var so = ScriptableObject.CreateInstance<CreatureSO>();
            so.name             = e.id;
            so.capturedName     = e.capturedName;
            so.purifiedName     = e.purifiedName;
            so.specialStat      = e.stat;
            so.rarity           = CreatureRarity.Common;
            so.baseSpawnWeight  = e.SpawnWeight;
            so.isPixelExclusive = e.isPixelExclusive;
            so.tier             = e.tier;
            so.hasSpecialty      = e.specialty.HasValue;
            so.specialtyResource = e.specialty ?? default;
            so.icon             = LoadIcon(e);
            return so;
        }

        /// <summary>ID로 찾고, 없으면 정화명으로 찾는다 (ID가 없던 구버전 세이브 대응).</summary>
        public static CreatureEntry Find(string id, string purifiedName)
        {
            foreach (var e in All)
                if (!string.IsNullOrEmpty(id) && e.id == id) return e;
            foreach (var e in All)
                if (!string.IsNullOrEmpty(purifiedName) && e.purifiedName == purifiedName) return e;
            return null;
        }
    }
}
