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