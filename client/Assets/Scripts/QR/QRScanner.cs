using System;
using UnityEngine;

namespace PixelCleaners.QR
{
    // ZXing.Net (com.unity.nuget.zxing or ZXing.Net.Bindings.Unity) 패키지 설치 전
    // 컴파일 오류 방지를 위한 스텁 구현.
    // 패키지 설치 후 ZXING_PRESENT 심볼을 Scripting Define Symbols에 추가하면 실제 구현으로 전환됩니다.
    public class QRScanner : MonoBehaviour
    {
        [SerializeField] int scanEveryNFrames = 30;
        [SerializeField] int cameraWidth      = 1280;
        [SerializeField] int cameraHeight     = 720;

        public event Action<string> OnQRDecoded;

        bool scanning;

        public void StartScan()
        {
#if ZXING_PRESENT
            StartScanImpl();
#else
            Debug.LogWarning("[QRScanner] ZXing 패키지 미설치 — 스텁 모드. " +
                             "Player Settings > Scripting Define Symbols에 ZXING_PRESENT를 추가하세요.");
            scanning = true;
#endif
        }

        public void StopScan()
        {
            scanning = false;
#if ZXING_PRESENT
            StopScanImpl();
#endif
        }

        // 에디터 테스트용: 임의의 QR 문자열을 직접 주입합니다.
        public void InjectMockQR(string qrData)
        {
            if (!scanning) return;
            StopScan();
            OnQRDecoded?.Invoke(qrData);
        }

        void OnDestroy() => StopScan();

#if ZXING_PRESENT
        ZXing.IBarcodeReader reader;
        WebCamTexture webcamTexture;
        int frameCount;

        void StartScanImpl()
        {
            if (scanning) return;
            reader = new ZXing.BarcodeReader { AutoRotate = false };
            webcamTexture = new WebCamTexture(cameraWidth, cameraHeight, 30);
            webcamTexture.Play();
            scanning = true;
            frameCount = 0;
        }

        void StopScanImpl()
        {
            webcamTexture?.Stop();
            webcamTexture = null;
        }

        void Update()
        {
            if (!scanning || webcamTexture == null || !webcamTexture.didUpdateThisFrame) return;
            if (++frameCount < scanEveryNFrames) return;
            frameCount = 0;
            try
            {
                var result = reader.Decode(webcamTexture.GetPixels32(), webcamTexture.width, webcamTexture.height);
                if (result == null) return;
                StopScan();
                OnQRDecoded?.Invoke(result.Text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QRScanner] Decode error: {e.Message}");
            }
        }
#endif
    }
}
