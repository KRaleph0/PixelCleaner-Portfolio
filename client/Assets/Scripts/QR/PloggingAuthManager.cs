using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using PixelCleaners;

namespace PixelCleaners.QR
{
    public class PloggingAuthManager : MonoBehaviour
    {
        public static PloggingAuthManager Instance { get; private set; }

        [SerializeField] string serverBaseUrl = "http://localhost:8000";
        [SerializeField] QRScanner qrScanner;

        public event Action<AwakenItemTier> OnAuthSuccess;
        public event Action<string> OnAuthFailed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnEnable()
        {
            if (qrScanner != null) qrScanner.OnQRDecoded += OnQRDecoded;
        }

        void OnDisable()
        {
            if (qrScanner != null) qrScanner.OnQRDecoded -= OnQRDecoded;
        }

        void OnQRDecoded(string qrData) => StartCoroutine(SendAuthRequest(qrData));

        IEnumerator SendAuthRequest(string qrCode)
        {
            var payload = $"{{\"qr_code\":\"{qrCode}\"}}";
            var request = new UnityWebRequest($"{serverBaseUrl}/api/plogging/verify", "POST")
            {
                uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(payload)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var resp = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
                if (!resp.success)
                {
                    OnAuthFailed?.Invoke(resp.message);
                    yield break;
                }

                var tier = resp.tier switch
                {
                    "plogging" => AwakenItemTier.Plogging,
                    "advanced" => AwakenItemTier.Advanced,
                    _          => AwakenItemTier.Normal
                };
                if (FactoryManager.Instance != null) FactoryManager.Instance.AddAwakenItem(tier, 1);
                OnAuthSuccess?.Invoke(tier);
            }
            else
            {
                OnAuthFailed?.Invoke(request.error);
            }
        }

        [Serializable]
        class AuthResponse
        {
            public bool success;
            public string tier;
            public string message;
        }
    }
}
