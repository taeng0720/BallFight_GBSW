using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HongBallFight.Editor
{
    [InitializeOnLoad]
    public static class SceneLayoutBuilder
    {
        static readonly string[] Scenes = { "MainMenu", "CharacterCreator", "Arena" };
        const string Folder = "Assets/HongBallFight/Scenes/";
        const string RegistryGuid = "a3d5f7e9b1c24d6f8e0a2b4c6d8e0f13";

        static SceneLayoutBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("HBF.SceneLayoutAsked", false)) return;
                SessionState.SetBool("HBF.SceneLayoutAsked", true);
                bool missing = false;
                foreach (string name in Scenes)
                {
                    string path = Folder + name + ".unity";
                    if (File.Exists(path) && !File.ReadAllText(path).Contains(RegistryGuid)) missing = true;
                }
                if (missing && EditorUtility.DisplayDialog("Hong Ball Fight", "씬에 UI가 아직 배치되지 않았습니다.\n지금 MainMenu · CharacterCreator · Arena 씬에 카메라와 UI를 배치할까요?\n(배치 후 씬에서 직접 편집할 수 있습니다)", "배치하기", "나중에"))
                    BuildAll(false);
            };
        }

        [MenuItem("Hong Ball Fight/씬 UI 다시 배치 (편집 내용 초기화)")]
        static void RebuildFromMenu() => BuildAll(true);

        static void BuildAll(bool confirm)
        {
            if (confirm && !EditorUtility.DisplayDialog("Hong Ball Fight", "세 씬을 기본 배치로 다시 만듭니다.\n씬에서 직접 편집한 내용은 사라집니다. 계속할까요?", "다시 배치", "취소")) return;
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
            EditorSceneManager.OpenScene(string.IsNullOrEmpty(previous) ? Folder + "MainMenu.unity" : previous);
            Debug.Log("Hong Ball Fight: 씬 3개에 카메라와 UI를 배치했습니다. Hierarchy의 Game UI 아래에서 편집하세요.");
        }
    }
}
