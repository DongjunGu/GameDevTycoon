using UnityEditor;
using UnityEngine;
using System.IO;

public class BuildScript
{
    public static void BuildIOS()
    {
        string buildPath = "build/iOS/GameDevTycoon";

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetScenes(),
            locationPathName = buildPath,
            target = BuildTarget.iOS,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(options);
    }

    private static string[] GetScenes()
    {
        var scenes = EditorBuildSettings.scenes;
        var enabledScenes = new System.Collections.Generic.List<string>();
        foreach (var scene in scenes)
        {
            if (scene.enabled)
                enabledScenes.Add(scene.path);
        }
        return enabledScenes.ToArray();
    }
}
