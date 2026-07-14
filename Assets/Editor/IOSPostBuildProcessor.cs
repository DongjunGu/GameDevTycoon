using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

// iOS 빌드 직후 Xcode 프로젝트에 "Sign in with Apple" capability + entitlements 를 자동으로 추가한다.
// GameCenterLogin.cs(실제로는 Apple Sign-In 네이티브 브릿지, Assets/Plugins/iOS/GameCenterPlugin.mm)가
// ASAuthorizationAppleIDProvider 를 쓰므로, Xcode에서 이 capability가 없으면 실기기에서 로그인 시도 시
// entitlement 누락 에러가 난다. 매번 Mac에서 Xcode로 수동 추가하지 않아도 되도록 빌드 파이프라인에 포함.
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

        // entitlements 파일 경로는 빌드 루트 기준 상대경로 — 없으면 AddCapability 가 새로 만들어준다.
        proj.AddCapability(mainTargetGuid, PBXCapabilityType.SignInWithApple, "GameDevTycoon.entitlements", true);

        proj.WriteToFile(projPath);
    }
}
