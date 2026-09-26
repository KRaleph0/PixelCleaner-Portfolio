using UnityEngine;

namespace PixelCleaners.AR
{
    /// <summary>
    /// 매 프레임 카메라 방향으로 Y축 회전. AR 생명체 에셋에 자동 부착.
    /// </summary>
    public class BillboardFace : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var dir = cam.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;

            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
