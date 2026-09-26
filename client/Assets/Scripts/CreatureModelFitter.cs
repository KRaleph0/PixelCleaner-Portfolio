using UnityEngine;

/// <summary>
/// 생명체 모델을 목표 높이로 맞추고 발밑을 원점에 두는 래퍼를 만든다.
///
/// AI 생성 모델·KayKit 모델은 원본 크기와 기준점이 제각각이라, 모델을 자식으로 넣고
/// 렌더러 경계를 측정해 스케일·위치를 보정한다. 결과 루트는 항상
///   - 높이 = targetHeight
///   - 발바닥 중앙 = 루트 원점
///   - 정면 = 루트 +Z (BillboardFace가 루트를 카메라 쪽으로 돌린다)
///   - 탭 판정용 CapsuleCollider는 루트에만 존재
/// 이므로 루트의 localScale을 건드리는 연출(지도 맥동 등)도 바닥 기준으로 동작한다.
/// </summary>
public static class CreatureModelFitter
{
    /// AR 씬 표시 높이 (m). 스폰 거리 2m에서 화면 세로의 약 1/4~1/3.
    public const float ArHeight  = 0.7f;
    /// 지도 씬 표시 높이 (월드 유닛). zoom 18 타일 1장 ≈ 0.6 유닛.
    public const float MapHeight = 0.1f;

    /// <param name="modelPrefab">null이면 fallbackColor 구체로 대체</param>
    public static GameObject Build(GameObject modelPrefab, string name, float targetHeight,
                                   float yawOffset, Color fallbackColor)
    {
        // 측정은 월드 원점·무회전·스케일 1 상태에서 해야 루트 로컬 좌표와 일치한다.
        var root = new GameObject(name);

        GameObject model;
        if (modelPrefab != null)
        {
            model = Object.Instantiate(modelPrefab, root.transform);
            model.name = "Model";
            UpgradeToUrpMaterials(model);
        }
        else
        {
            model = MakeSphere(fallbackColor);
            model.transform.SetParent(root.transform, false);
        }
        // 모델 루트 자체의 회전은 반드시 보존한다. Blender를 거친 FBX(Meshy 등)는 Z-up 메시를
        // 루트의 -90° X 회전으로 세워 두는데, 이 값을 덮어쓰면 모델이 도로 눕는다.
        // 정면 보정은 부모 기준 Y축 회전을 앞에 곱한다.
        model.transform.localRotation = Quaternion.Euler(0f, yawOffset, 0f) * model.transform.localRotation;

        // 모델 자체 콜라이더는 제거 — 남아 있으면 레이캐스트가 자식에 맞아
        // hit.transform == 루트 비교가 실패해 탭이 먹지 않는다.
        foreach (var c in model.GetComponentsInChildren<Collider>(true))
        {
            c.enabled = false;
            // 에디터 도구(아이콘 촬영)에서도 호출되므로 Play 여부에 따라 구분
            if (Application.isPlaying) Object.Destroy(c);
            else                       Object.DestroyImmediate(c);
        }

        if (!TryGetBounds(model, out var b) || b.size.y <= 0.0001f)
        {
            Debug.LogWarning($"[ModelFitter] {name}: 렌더러 경계를 얻지 못해 크기 보정을 건너뜁니다.");
            AddCollider(root, targetHeight, targetHeight * 0.3f);
            return root;
        }

        float s = targetHeight / b.size.y;
        var bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);

        // 모델 원점 기준 균일 스케일 후 발밑 중앙이 루트 원점에 오도록 이동.
        // 루트 좌표의 점 q = p + M·v 는 스케일 후 p' + s·(q − p) 가 되므로
        // 발밑 b → 0 이 되려면 p' = s·(p − b). (p = 모델 원래 위치, 보통 0)
        model.transform.localScale    = model.transform.localScale * s;
        model.transform.localPosition = (model.transform.localPosition - bottomCenter) * s;

        float radius = Mathf.Max(b.size.x, b.size.z) * s * 0.5f;
        AddCollider(root, targetHeight, radius);
        return root;
    }

    static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    static void AddCollider(GameObject root, float height, float radius)
    {
        var cap       = root.AddComponent<CapsuleCollider>();
        cap.direction = 1; // Y
        cap.height    = height;
        // 캡슐은 반지름이 높이의 절반을 넘으면 구가 된다 — 탭 판정은 넉넉한 편이 낫다
        cap.radius    = Mathf.Clamp(radius, height * 0.2f, height * 0.5f);
        cap.center    = new Vector3(0f, height * 0.5f, 0f);
    }

    static GameObject MakeSphere(Color color)
    {
        var go   = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name  = "Model";
        var rend = go.GetComponent<Renderer>();

        // URP 빌드에서 Standard 셰이더는 분홍으로 렌더됨 → URP 셰이더 우선
        var shader = Shader.Find("Universal Render Pipeline/Lit")
                  ?? Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Mobile/Diffuse");
        var mat = shader != null ? new Material(shader) : new Material(rend.sharedMaterial);
        mat.color = color;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        rend.material = mat;
        return go;
    }

    /// <summary>FBX 임포트 시 Standard 셰이더가 남아 있으면 URP/Lit으로 교체 (빌드 분홍색 방지).</summary>
    public static void UpgradeToUrpMaterials(GameObject go)
    {
        var urpShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpShader == null) return;

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                if (!mats[i].shader.name.StartsWith("Standard")) continue;
                var newMat = new Material(urpShader);
                newMat.mainTexture = mats[i].mainTexture;
                if (mats[i].HasProperty("_Color"))
                {
                    var c = mats[i].GetColor("_Color");
                    newMat.color = c;
                    if (newMat.HasProperty("_BaseColor")) newMat.SetColor("_BaseColor", c);
                }
                mats[i] = newMat;
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }
}
