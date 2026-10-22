using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HongBallFight.Editor
{
    [InitializeOnLoad]
    public static class SceneLayoutBuilder
    {
        static readonly string[] Scenes = { "MainMenu", "Arena" };
        const string Folder = "Assets/HongBallFight/Scenes/";

        static SceneLayoutBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                string asked = "HBF.SceneLayoutAsked.v" + SceneLayouts.Version;
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(asked, false)) return;
                SessionState.SetBool(asked, true);
                if (!IsCurrent() && EditorUtility.DisplayDialog("Hong Ball Fight", "씬의 UI 배치가 최신이 아닙니다.\nMainMenu · Arena 씬을 새 배치로 다시 만들까요?\n씬에서 직접 편집한 내용은 초기화됩니다.", "다시 배치", "나중에"))
                    BuildAll(false);
            };
        }

        public static bool IsCurrent()
        {
            foreach (string name in Scenes)
            {
                string path = Folder + name + ".unity";
                if (!File.Exists(path) || !File.ReadAllText(path).Contains("LayoutVersion: " + SceneLayouts.Version)) return false;
            }
            return true;
        }
        public static void EnsureCurrent() { if (!IsCurrent()) BuildAll(false); }

        [MenuItem("Hong Ball Fight/씬 UI 다시 배치 (편집 내용 초기화)")]
        static void RebuildFromMenu() => BuildAll(true);

        static void BuildAll(bool confirm)
        {
            if (confirm && !EditorUtility.DisplayDialog("Hong Ball Fight", "씬을 기본 배치로 다시 만듭니다.\n씬에서 직접 편집한 내용은 사라집니다. 계속할까요?", "다시 배치", "취소")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string previous = EditorSceneManager.GetActiveScene().path;
            foreach (string name in Scenes)
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = SceneLayouts.EnsureCamera();
                new GameObject("Hong Ball Fight").AddComponent<BallFightApp>();
                SceneLayouts.EnsureEventSystem();
                SceneLayouts.Build(name, camera);
                EditorSceneManager.SaveScene(scene, Folder + name + ".unity");
            }
            EditorSceneManager.OpenScene(File.Exists(previous) ? previous : Folder + "MainMenu.unity");
            Debug.Log("Hong Ball Fight: 씬에 카메라와 UI를 배치했습니다. Hierarchy의 Game UI 아래에서 편집하세요.");
        }
    }
}
