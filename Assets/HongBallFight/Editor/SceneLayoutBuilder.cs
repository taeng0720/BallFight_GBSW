using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HongBallFight.Editor
{
    public static class SceneLayoutBuilder
    {
        [MenuItem("Hong Ball Fight/Rebuild Editable Scenes")]
        public static void BakeAll()
        {
            var previous = SceneManager.GetActiveScene();
            var report = new List<string>();
            Directory.CreateDirectory("Temp/SceneLayouts"); Directory.CreateDirectory("Assets/HongBallFight/Art/Sprites");
            foreach (string name in new[] { "MainMenu", "CharacterCreator", "Arena" })
            {
                string target = "Assets/HongBallFight/Scenes/" + name + ".unity";
                string backup = "Temp/SceneLayouts/" + name + ".before.unity";
                if (File.Exists(target) && !File.Exists(backup)) File.Copy(target, backup);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    SceneManager.SetActiveScene(scene);
                    var app = new GameObject(name + " Screen Controller").AddComponent<BallFightApp>(); app.BakeLayout(name);
                    PersistSprites(scene);
                    var slots = app.SceneUi.GetComponentsInChildren<SceneUiSlot>(true);
                    var keys = new HashSet<string>();
                    foreach (var slot in slots)
                    {
                        string key = slot.transform.parent.GetInstanceID() + "/" + slot.Key;
                        if (!keys.Add(key)) throw new Exception(name + " duplicate UI key: " + slot.Key);
                    }
                    string staged = "Temp/SceneLayouts/" + name + ".unity";
                    if (!EditorSceneManager.SaveScene(scene, staged)) throw new Exception("Cannot serialize " + name);
                    File.Copy(staged, target, true);
                    report.Add(name + ": " + slots.Length + " editable UI objects");
                }
                finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
            }
            AssetDatabase.Refresh();
            Debug.Log(string.Join("\n", report));
        }
        static void PersistSprites(Scene scene)
        {
            var seen = new HashSet<int>();
            foreach (var root in scene.GetRootGameObjects()) foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                var sprite = image.sprite;
                if (!sprite || !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(sprite)) || !seen.Add(sprite.GetInstanceID())) continue;
                string path = AssetDatabase.GenerateUniqueAssetPath("Assets/HongBallFight/Art/Sprites/UiSprite.asset");
                var texture = sprite.texture;
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture))) AssetDatabase.CreateAsset(texture, path);
                else path = AssetDatabase.GetAssetPath(texture);
                AssetDatabase.AddObjectToAsset(sprite, path);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
