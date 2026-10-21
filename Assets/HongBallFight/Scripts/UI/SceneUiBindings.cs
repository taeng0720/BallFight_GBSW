using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class SceneUiBindings : MonoBehaviour
    {
        public RectTransform Root;
        public CanvasScaler Scaler;
        public Vector2 DesignSize = new Vector2(1280, 800);
        public RectTransform CreatorSandbox;
        public RectTransform Arena;
        public RectTransform[] Pages;

        public RectTransform Page(string tab)
        {
            string key = PageName(tab);
            RectTransform result = null;
            foreach (var page in Pages)
            {
                if (!page) continue;
                bool active = page.name == key; page.gameObject.SetActive(active);
                if (active) result = page;
            }
            return result;
        }
        public static string PageName(string tab) => tab == "메인" ? "MainMenu" : tab == "대전" ? "CharacterSelection" : tab == "제작" ? "CharacterForm" : tab == "코드" ? "BlockWorkshop" : tab == "계정" ? "Account" : "Administration";
        public static string Key(string kind, string name, Rect rect) => kind + ":" + name + ":" + rect.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ":" + rect.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        public static RectTransform Find(Transform parent, string key)
        {
            foreach (Transform child in parent)
            {
                var slot = child.GetComponent<SceneUiSlot>();
                if (!slot || slot.Key != key) continue;
                child.gameObject.SetActive(true); return (RectTransform)child;
            }
            return null;
        }
        public static void ResetScope(Transform parent)
        {
            var children = new List<Transform>(); foreach (Transform child in parent) children.Add(child);
            foreach (var child in children)
            {
                if (child.GetComponent<SceneUiSlot>()) { ResetScope(child); child.gameObject.SetActive(false); }
                else if (child.GetComponent<RuntimeUiNode>()) { child.gameObject.SetActive(false); if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject); }
                else ResetScope(child);
            }
        }
    }
}
