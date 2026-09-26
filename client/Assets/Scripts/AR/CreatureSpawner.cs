using System.Collections.Generic;
using UnityEngine;
using PixelCleaners;
using PixelCleaners.GPS;

namespace PixelCleaners.AR
{
    public class CreatureSpawner : MonoBehaviour
    {
        public static CreatureSpawner Instance { get; private set; }

        [SerializeField] List<CreatureSO> creaturePool = new();
        [SerializeField] float spawnIntervalSeconds = 30f;
        [SerializeField] float spawnDistanceFromCamera = 2f;

        readonly List<GameObject> activeCreatures = new();
        float spawnTimer;
        bool inHotspot;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            // 지도에서 특정 크리처를 탭해서 왔을 때
            if (PendingCapture.HasPending)
            {
                // 지도 스폰 키 — 포획에 성공하면 이 스폰을 잡은 것으로 기록한다
                string key  = PendingCapture.SpawnKey;
                long   ends = PendingCapture.SpawnEndsTicks;

                if (PendingCapture.Definition != null)
                    SpawnSpecific(PendingCapture.Definition, PendingCapture.Grade, key, ends);
                else if (PendingCapture.PoolIndex >= 0 && PendingCapture.PoolIndex < creaturePool.Count)
                    SpawnSpecific(creaturePool[PendingCapture.PoolIndex], PendingCapture.Grade, key, ends);
                else
                    ForceSpawn(); // 인덱스 없으면 풀에서 랜덤
                PendingCapture.Clear();
                return;
            }

            if (HotspotDetector.Instance == null) return;
            HotspotDetector.Instance.OnHotspotGradeChanged += OnHotspotChanged;
            OnHotspotChanged(HotspotDetector.Instance.CurrentGrade);
        }

        void SpawnSpecific(CreatureSO def, int grade, string spawnKey = null, long spawnEndsTicks = 0)
        {
            var pos = GetSpawnPosition() ?? transform.position + Vector3.forward * 2f;
            SpawnAt(def, grade, pos, spawnKey, spawnEndsTicks);
        }

        void SpawnAt(CreatureSO def, int grade, Vector3 pos, string spawnKey = null, long spawnEndsTicks = 0)
        {
            var go = Instantiate(def.prefab, pos, Quaternion.identity);
            go.SetActive(true);
            go.AddComponent<BillboardFace>();
            var interaction = go.GetComponent<CaptureInteraction>() ?? go.AddComponent<CaptureInteraction>();
            interaction.Initialize(new CreatureInstance(def, grade), spawnKey, spawnEndsTicks);
            activeCreatures.Add(go);
        }

        void OnDestroy()
        {
            if (HotspotDetector.Instance != null)
                HotspotDetector.Instance.OnHotspotGradeChanged -= OnHotspotChanged;
        }

        void OnHotspotChanged(HotspotGrade grade)
        {
            inHotspot = grade != HotspotGrade.Normal;
            spawnTimer = 0f;
        }

        void Update()
        {
            if (!inHotspot || activeCreatures.Count > 0) return;

            spawnTimer += Time.deltaTime;
            if (spawnTimer < spawnIntervalSeconds) return;

            spawnTimer = 0f;
            TrySpawn();
        }

        void TrySpawn()
        {
            var detector = HotspotDetector.Instance;
            if (detector == null || Random.value > detector.GetSpawnChance()) return;

            var def = PickCreature(detector.CurrentGrade);
            if (def == null) return;

            var pos = GetSpawnPosition();
            if (pos == null) return;

            SpawnAt(def, RollGrade(), pos.Value);
        }

        Vector3? GetSpawnPosition()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            return cam.transform.position + forward.normalized * spawnDistanceFromCamera;
        }

        CreatureSO PickCreature(HotspotGrade grade)
        {
            var pool = grade == HotspotGrade.Hotspot
                ? creaturePool
                : creaturePool.FindAll(c => !c.isPixelExclusive);
            if (pool.Count == 0) return null;

            float total = 0f;
            foreach (var c in pool) total += c.baseSpawnWeight;

            float roll = Random.Range(0f, total);
            float cumul = 0f;
            foreach (var c in pool)
            {
                cumul += c.baseSpawnWeight;
                if (roll <= cumul) return c;
            }
            return pool[^1];
        }

        static int RollGrade()
        {
            float r = Random.value;
            if (r < 0.05f) return 2;
            if (r < 0.30f) return 1;
            return 0;
        }

        public void RemoveCreature(GameObject go) => activeCreatures.Remove(go);

        /// 포획 시도 후 도망가지 않고 남은 생명체를 다시 추적 (중복 스폰 방지)
        public void TrackCreature(GameObject go)
        {
            if (go != null && !activeCreatures.Contains(go)) activeCreatures.Add(go);
        }

        public void AddToPool(CreatureSO so) => creaturePool.Add(so);

        public void ForceSpawn()
        {
            spawnTimer = spawnIntervalSeconds;
            inHotspot = true;

            // TrySpawn()은 랜덤 확률 체크가 있어 ForceSpawn에서는 직접 스폰
            var def = PickCreature(HotspotGrade.Hotspot);
            if (def == null) return;

            var cam = Camera.main;
            Vector3 pos;
            if (cam != null)
            {
                var forward = cam.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                pos = cam.transform.position + forward.normalized * spawnDistanceFromCamera;
            }
            else
            {
                pos = transform.position + Vector3.forward * spawnDistanceFromCamera;
            }

            SpawnAt(def, RollGrade(), pos);
        }
    }
}
