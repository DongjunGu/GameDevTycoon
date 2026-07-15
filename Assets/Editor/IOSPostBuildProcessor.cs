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
        // pbxproj 파일이 있는 폴더(SRCROOT) 기준 실제 경로를 명시적으로 계산
        string projDir = Path.GetDirectoryName(projPath); // .xcodeproj가 있는 폴더
        string entitlementsFullPath = Path.Combine(projDir, entitlementsFileName);

        proj.AddCapability(mainTargetGuid, PBXCapabilityType.SignInWithApple, entitlementsFileName, true);

        // AddCapability가 파일을 못 만들었을 경우를 대비해 직접 확인 후 생성
        if (!File.Exists(entitlementsFullPath))
        {
            var entitlements = new PlistDocument();
            entitlements.root.SetBoolean("com.apple.developer.applesignin", true);
            entitlements.WriteToFile(entitlementsFullPath);
        }

        proj.WriteToFile(projPath);
    }
}