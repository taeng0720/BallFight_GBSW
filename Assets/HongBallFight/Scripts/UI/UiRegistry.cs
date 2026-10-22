using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class UiRegistry : MonoBehaviour
    {
        [Serializable]
        public sealed class Entry
        {
            public string Key;
            public GameObject Target;
        }
        public Vector2 DesignSize = new Vector2(1280, 800);
        public int LayoutVersion;
        public bool Portrait;
        public RectTransform Root;
        public List<Entry> Entries = new List<Entry>();
        Dictionary<string, GameObject> lookup;

        public void Register(string key, Component target) => Entries.Add(new Entry { Key = key, Target = target.gameObject });
        public bool Has(string key) { Build(); return lookup.TryGetValue(key, out var target) && target; }
        public GameObject Get(string key)
        {
            Build();
            if (lookup.TryGetValue(key, out var target) && target) return target;
            Debug.LogWarning("UI 오브젝트를 찾을 수 없습니다: " + key + " (Game UI > UiRegistry 확인)", this);
            return null;
        }
        public T Get<T>(string key) where T : Component { var go = Get(key); return go ? go.GetComponent<T>() : null; }
        public RectTransform Rect(string key) => Get<RectTransform>(key);
        public Text Text(string key) => Get<Text>(key);
        public Button Button(string key) => Get<Button>(key);
        public InputField Input(string key) => Get<InputField>(key);
        public void OnClick(string key, Action action)
        {
            var button = Button(key);
            if (button) button.onClick.AddListener(() => action());
        }
        public void Show(string key, bool visible) { var go = Get(key); if (go) go.SetActive(visible); }
        void Build()
        {
            if (lookup != null) return;
            lookup = new Dictionary<string, GameObject>();
            foreach (var entry in Entries) if (entry != null && !string.IsNullOrEmpty(entry.Key)) lookup[entry.Key] = entry.Target;
        }
    }
}
