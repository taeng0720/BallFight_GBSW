using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HongBallFight.Editor
{
    public static class WebGLBuild
    {
        [MenuItem("Hong Ball Fight/Build WebGL")]
        public static void WebGL()
        {
            PlayerSettings.companyName = "HongBallFight";
            PlayerSettings.productName = "Hong Ball Fight";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 800;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/HongBallFight/Scenes/MainMenu.unity", "Assets/HongBallFight/Scenes/CharacterCreator.unity", "Assets/HongBallFight/Scenes/Arena.unity" },
                locationPathName = "Builds/WebGL", target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed: " + report.summary.result);
            Debug.Log("WebGL build complete: " + Path.GetFullPath("Builds/WebGL"));
        }
    }
}
