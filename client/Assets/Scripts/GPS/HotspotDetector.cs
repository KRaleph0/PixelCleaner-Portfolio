using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelCleaners.GPS
{
    public enum HotspotGrade { Normal, Polluted, Hotspot }

    [Serializable]
    public struct HotspotData
    {
        public string label;
        public float latitude;
        public float longitude;
        public HotspotGrade grade;
        [Tooltip("0 = use defaultCheckRadius")]
        public float radiusMeters;
    }

    public class HotspotDetector : MonoBehaviour
    {
        public static HotspotDetector Instance { get; private set; }

        [SerializeField] List<HotspotData> hotspots = new();
        [SerializeField] float defaultCheckRadius = 50f;

        // Spawn chance per grade: Normal 5%, Polluted 20%, Hotspot 45%
        static readonly float[] k_SpawnChance = { 0.05f, 0.20f, 0.45f };

        public HotspotGrade CurrentGrade { get; private set; } = HotspotGrade.Normal;
        public HotspotData? ActiveHotspot { get; private set; }

        public event Action<HotspotGrade> OnHotspotGradeChanged;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            if (LocationManager.Instance == null) return;
            LocationManager.Instance.OnLocationUpdated += Evaluate;
            // LocationManager may have already fired the initial event (editor mock)
            if (LocationManager.Instance.IsReady)
                Evaluate(LocationManager.Instance.CurrentLocation);
        }

        void OnDestroy()
        {
            if (LocationManager.Instance != null)
                LocationManager.Instance.OnLocationUpdated -= Evaluate;
        }

        void Evaluate(Vector2 location)
        {
            var nearest  = NearestHotspot(location);
            var newGrade = nearest.HasValue ? nearest.Value.grade : HotspotGrade.Normal;
            ActiveHotspot = nearest;

            if (newGrade != CurrentGrade)
            {
                CurrentGrade = newGrade;
                OnHotspotGradeChanged?.Invoke(CurrentGrade);
            }
        }

        /// <summary>임의 지점의 오염 등급 (플레이어 위치와 무관). 지도 스폰 밀도 계산에 쓴다.</summary>
        public HotspotGrade GradeAt(Vector2 latLon)
        {
            var nearest = NearestHotspot(latLon);
            return nearest.HasValue ? nearest.Value.grade : HotspotGrade.Normal;
        }

        HotspotData? NearestHotspot(Vector2 location)
        {
            HotspotData? nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var spot in hotspots)
            {
                var spotPos = new Vector2(spot.latitude, spot.longitude);
                float dist = LocationManager.DistanceMeters(location, spotPos);
                float radius = spot.radiusMeters > 0f ? spot.radiusMeters : defaultCheckRadius;
                if (dist <= radius && dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = spot;
                }
            }
            return nearest;
        }

        public float GetSpawnChance() => k_SpawnChance[(int)CurrentGrade];

        public int HotspotCount => hotspots.Count;
        public IReadOnlyList<HotspotData> Hotspots => hotspots;

        public void AddHotspot(HotspotData spot) => hotspots.Add(spot);

        public void ForceGrade(HotspotGrade grade)
        {
            if (grade == CurrentGrade) return;
            CurrentGrade = grade;
            OnHotspotGradeChanged?.Invoke(CurrentGrade);
        }
    }
}
