using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AndroidBuildSetup
{
    const string BundleId    = "com.pixelcleaner.game";
    const string ProductName = "PixelCleaner";

    // ── 빌드 설정 적용 ──────────────────────────────────────────

    [MenuItem("PixelCleaners/Android 빌드 설정 적용")]
    public static void ApplySettings()
    {
        // 플랫폼 전환 (이미 Android면 스킵)
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
        }

        PlayerSettings.companyName                  = "PixelCleaners";
        PlayerSettings.productName                  = ProductName;
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);

        // ARCore 요구사항 — API 24 이상 (Android 7.0)
        PlayerSettings.Android.minSdkVersion        = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion     = AndroidSdkVersions.AndroidApiLevelAuto;

        // IL2CPP + ARM64 (ARCore 필수)
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures  = AndroidArchitecture.ARM64;

        // 세로 모드 고정
        PlayerSettings.defaultInterfaceOrientation  = UIOrientation.Portrait;

        Debug.Log("[AndroidBuild] ⚠ 수동 필수: Project Settings → Player → Android → Other Settings\n" +
                  "   → Application Entry Point 를 'Activity' 로 변경 (GameActivity → Activity)\n" +
                  "   이 설정 없으면 Theme.AppCompat 크래시 발생 (Unity 공식 버그)");

        // 인터넷 허용
        PlayerSettings.Android.forceInternetPermission = true;

        AssetDatabase.SaveAssets();
        Debug.Log("[AndroidBuild] 설정 완료. 다음 수동 단계를 진행하세요:\n" +
                  "1. Project Settings → XR Plug-in Management → Android → ARCore 체크\n" +
                  "2. File → Build Settings → Add Open Scenes (또는 씬 생성 메뉴 재실행)");
    }

    // ── 테스트 APK 빌드 ─────────────────────────────────────────

    [MenuItem("PixelCleaners/테스트 APK 빌드")]
    public static void BuildTestAPK()
    {
        ApplySettings();

        string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds/Android");
        Directory.CreateDirectory(outDir);
        string apkPath = Path.Combine(outDir, $"{ProductName}_test.apk");

        // Build Settings(EditorBuildSettings)를 그대로 따른다.
        // 예전에는 씬 목록을 여기에 따로 적어 두어 DeliveryScene이 빠졌고,
        // APK에서 납품 센터가 SceneController의 대체 팝업(랭킹 등 기능 없음)으로 떴다.
        // 순서도 Build Settings 순서 — index 0이 앱 진입점이다.
        var scenes = System.Array.ConvertAll(
            System.Array.FindAll(EditorBuildSettings.scenes,
                s => s.enabled && AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(s.path) != null),
            s => s.path);

        var missing = System.Array.FindAll(EditorBuildSettings.scenes,
            s => s.enabled && AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(s.path) == null);
        foreach (var m in missing)
            Debug.LogWarning($"[AndroidBuild] Build Settings에 있지만 파일이 없어 제외: {m.path}");

        if (scenes.Length == 0)
        {
            Debug.LogError("[AndroidBuild] 포함할 씬이 없습니다. " +
                           "Unity 메뉴 → PixelCleaners → 씬 생성 (전체 7개) 를 먼저 실행하세요.");
            return;
        }
        Debug.Log($"[AndroidBuild] 포함된 씬 {scenes.Length}개: {string.Join(", ", scenes)}");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes           = scenes,
            locationPathName = apkPath,
            target           = BuildTarget.Android,
            options          = BuildOptions.Development | BuildOptions.AllowDebugging,
        });

        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[AndroidBuild] 빌드 성공: {apkPath}");
        else
            Debug.LogError($"[AndroidBuild] 빌드 실패: {report.summary.totalErrors} 에러");
    }

    [MenuItem("PixelCleaners/테스트 APK 빌드", validate = true)]
    static bool ValidateBuild() => !EditorApplication.isPlaying;
}
