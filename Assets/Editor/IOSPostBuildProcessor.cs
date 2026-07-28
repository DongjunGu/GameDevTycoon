// UnityEditor.iOS.Xcode는 iOS Build Support 모듈이 설치된 환경에서만 어셈블리가 존재한다.
// #if UNITY_IOS로 감싸지 않으면(=빌드 타겟과 무관하게 항상 컴파일 시도) 그 모듈이 없는 팀원 환경에서
// CS0234로 컴파일이 깨져 Unity가 Safe Mode로 진입한다 — UNITY_IOS는 빌드 타겟이 iOS일 때만 정의되므로
// 이렇게 감싸면 다른 타겟(Android/Windows 등) 환경에서는 이 파일 전체가 컴파일 대상에서 제외된다.
#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IOSPostBuildProcessor
{
    [PostProcessBuild]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string pathToBuiltProject)
    {
        if (buildTarget != BuildTarget.iOS) return;

        string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);

        string mainTargetGuid = proj.GetUnityMainTargetGuid();

        string entitlementsFileName = "GameDevTycoon.entitlements";
        // SRCROOT = .xcodeproj 번들을 담고 있는 바깥 폴더 (번들 내부 아님!)
        string projDir = Path.GetDirectoryName(Path.GetDirectoryName(projPath));
        string entitlementsFullPath = Path.Combine(projDir, entitlementsFileName);

        proj.AddCapability(mainTargetGuid, PBXCapabilityType.SignInWithApple, entitlementsFileName, true);

        if (!File.Exists(entitlementsFullPath))
        {
            var entitlements = new PlistDocument();
            entitlements.root.SetBoolean("com.apple.developer.applesignin", true);
            entitlements.WriteToFile(entitlementsFullPath);
        }

        proj.WriteToFile(projPath);
    }
}
#endif