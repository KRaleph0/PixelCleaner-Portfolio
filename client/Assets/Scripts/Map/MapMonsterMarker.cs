using UnityEngine;
using UnityEngine.InputSystem;
using PixelCleaners;

/// <summary>
/// 지도 씬에 배치되는 몬스터 마커.
/// 탭하면 PendingCapture를 설정하고 AR 씬으로 이동.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MapMonsterMarker : MonoBehaviour
{
    CreatureSO    definition; // null 허용 (에셋 미생성 시)
    CreatureRarity rarity;
    int            grade;
    int            poolIndex = -1; // AR 풀 인덱스 (-1=랜덤)
    float          baseScale = 0.05f; // Init 시 localScale.x로 덮어씀

    static readonly Color[] RarityColors =
    {
        new Color(0.4f, 1f,   0.4f),   // Common
        new Color(0.3f, 0.5f, 1f),     // Rare
        new Color(0.8f, 0.3f, 1f),     // Epic
        new Color(1f,   0.75f, 0.1f),  // Legendary
        Color.white                     // Pixel
    };

    string spawnKey;        // MapSpawnSystem 스폰 키
    long   spawnEndsTicks;

    public void Init(CreatureSO def, CreatureRarity r, int g, int pi = -1,
                     string key = null, long endsTicks = 0)
    {
        definition     = def;
        rarity         = r;
        grade          = g;
        poolIndex      = pi;
        spawnKey       = key;
        spawnEndsTicks = endsTicks;
        baseScale  = transform.localScale.x; // 외부에서 설정한 크기 저장

        var color = RarityColors[Mathf.Clamp((int)r, 0, RarityColors.Length - 1)];
        // 구체 폴백: 루트 Renderer에 색상 적용 / FBX: Renderer 없으므로 스킵
        var rend = GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = rend.material;
            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        }
    }

    void Update()
    {
        // 맥동 애니메이션 — baseScale 기준 (0.18f 하드코딩 제거)
        float pulse  = 1f + 0.12f * Mathf.Sin(Time.time * 3.5f + (float)rarity);
        transform.localScale = Vector3.one * (baseScale * pulse);

        HandleInput();
    }

    void HandleInput()
    {
        // 터치 입력
        var ts = Touchscreen.current;
        if (ts != null)
        {
            foreach (var touch in ts.touches)
            {
                if (!touch.phase.ReadValue().Equals(UnityEngine.InputSystem.TouchPhase.Began)) continue;
                if (TryRaycast(touch.position.ReadValue())) return;
            }
        }

        // 에디터 마우스 폴백
        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            TryRaycast(mouse.position.ReadValue());
    }

    bool TryRaycast(Vector2 screenPos)
    {
        var cam = Camera.main;
        if (cam == null) return false;

        var ray = cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out var hit, 100f)) return false;
        if (hit.transform != transform) return false;

        OnTapped();
        return true;
    }

    void OnTapped()
    {
        PendingCapture.Set(definition, rarity, grade, poolIndex, spawnKey, spawnEndsTicks);
        SceneController.GoAR();
    }
}
