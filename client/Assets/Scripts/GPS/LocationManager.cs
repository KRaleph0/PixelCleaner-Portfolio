using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;

namespace PixelCleaners.GPS
{
    public class LocationManager : MonoBehaviour
    {
        public static LocationManager Instance { get; private set; }

        [SerializeField] float updateIntervalSeconds = 1f;
        [SerializeField] float desiredAccuracyMeters = 5f;
        [SerializeField] float updateDistanceMeters = 1f;

#if UNITY_EDITOR
        [Header("Editor Mock Location (Seoul City Hall)")]
        [SerializeField] float mockLatitude = 37.5665f;
        [SerializeField] float mockLongitude = 126.9780f;
#endif

        public bool IsReady { get; private set; }
        public Vector2 CurrentLocation { get; private set; }

        public event Action<Vector2> OnLocationUpdated;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() => StartCoroutine(InitLocation());

        IEnumerator InitLocation()
        {
#if UNITY_EDITOR
            IsReady = true;
            CurrentLocation = new Vector2(mockLatitude, mockLongitude);
            OnLocationUpdated?.Invoke(CurrentLocation);
            yield break;
#endif

#pragma warning disable CS0162
#if UNITY_ANDROID
            yield return RequestAndroidLocationPermission();
            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.LogWarning("[LocationManager] 위치 권한이 거부되었습니다.");
                yield break;
            }
#endif

            // GPS 서비스가 꺼져 있으면 최대 30초 대기 (사용자가 설정에서 켤 수 있도록)
            float gpsWait = 0f;
            while (!Input.location.isEnabledByUser && gpsWait < 30f)
            {
                yield return new WaitForSeconds(1f);
                gpsWait += 1f;
            }

            if (!Input.location.isEnabledByUser)
            {
                Debug.LogWarning("[LocationManager] GPS가 꺼져 있습니다. 설정에서 위치 서비스를 활성화하세요.");
                yield break;
            }

            Input.location.Start(desiredAccuracyMeters, updateDistanceMeters);

            int timeout = 20;
            while (Input.location.status == LocationServiceStatus.Initializing && timeout > 0)
            {
                yield return new WaitForSeconds(1f);
                timeout--;
            }

            if (Input.location.status != LocationServiceStatus.Running)
            {
                Debug.LogWarning("[LocationManager] 위치 서비스 시작 실패.");
                yield break;
            }

            IsReady = true;
            StartCoroutine(PollLocation());
#pragma warning restore CS0162
        }

#if UNITY_ANDROID
        IEnumerator RequestAndroidLocationPermission()
        {
            if (Permission.HasUserAuthorizedPermission(Permission.FineLocation))
                yield break;

            bool done    = false;
            bool granted = false;

            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => { granted = true; done = true; };
            callbacks.PermissionDenied  += _ => { done = true; };

            Permission.RequestUserPermission(Permission.FineLocation, callbacks);

            // 사용자가 다이얼로그에서 응답할 때까지 무제한 대기
            yield return new WaitUntil(() => done);

            if (!granted)
            {
                // COARSE_LOCATION으로 폴백 시도
                if (!Permission.HasUserAuthorizedPermission(Permission.CoarseLocation))
                {
                    done = false;
                    var fallback = new PermissionCallbacks();
                    fallback.PermissionGranted += _ => { done = true; };
                    fallback.PermissionDenied  += _ => { done = true; };
                    Permission.RequestUserPermission(Permission.CoarseLocation, fallback);
                    yield return new WaitUntil(() => done);
                }
            }
        }
#endif

        IEnumerator PollLocation()
        {
            double lastTimestamp = 0;
            while (true)
            {
                yield return new WaitForSeconds(updateIntervalSeconds);
                if (Input.location.status != LocationServiceStatus.Running) continue;

                var data = Input.location.lastData;
                // 타임스탬프가 바뀐 경우에만 이벤트 발생 (중복 방지)
                if (data.timestamp <= lastTimestamp) continue;
                lastTimestamp = data.timestamp;

                CurrentLocation = new Vector2(data.latitude, data.longitude);
                OnLocationUpdated?.Invoke(CurrentLocation);
            }
        }

        // Haversine great-circle distance in meters
        public static float DistanceMeters(Vector2 a, Vector2 b)
        {
            const float R = 6371000f;
            float lat1 = a.x * Mathf.Deg2Rad;
            float lat2 = b.x * Mathf.Deg2Rad;
            float dLat = (b.x - a.x) * Mathf.Deg2Rad;
            float dLon = (b.y - a.y) * Mathf.Deg2Rad;
            float h = Mathf.Sin(dLat * 0.5f) * Mathf.Sin(dLat * 0.5f)
                    + Mathf.Cos(lat1) * Mathf.Cos(lat2)
                    * Mathf.Sin(dLon * 0.5f) * Mathf.Sin(dLon * 0.5f);
            return R * 2f * Mathf.Atan2(Mathf.Sqrt(h), Mathf.Sqrt(1f - h));
        }

        void OnDestroy()
        {
#if !UNITY_EDITOR
            Input.location.Stop();
#endif
        }
    }
}
