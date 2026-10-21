using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HongBallFight
{
    public partial class BallFightApp
    {
        public SceneUiBindings SceneUi;
        [NonSerialized] public bool BakingLayout;
        [NonSerialized] public bool TransientUi;
        [NonSerialized] public bool DataUi;
        string layoutSceneName;
        RectTransform screen;

        void InitializeCanvas()
        {
            font = Resources.Load<Font>("Fonts/NotoSansKR");
            if (!font) font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Arial" }, 24);
            if (SceneUi && SceneUi.Root)
            {
                root = SceneUi.Root;
                if (SceneUi.Scaler) SceneUi.Scaler.referenceResolution = SceneUi.DesignSize;
                return;
            }
            var canvasObject = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(canvasObject.transform, false); background.GetComponent<Image>().color = Ink; background.GetComponent<Image>().raycastTarget = false;
            var bg = background.GetComponent<RectTransform>(); bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
            root = Box(canvasObject.transform, "Application", new Rect(0, 0, 1280, 800), Ink);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.anchoredPosition = Vector2.zero;
            SceneUi = canvasObject.AddComponent<SceneUiBindings>(); SceneUi.Root = root; SceneUi.Scaler = scaler; SceneUi.Pages = Array.Empty<RectTransform>();
            if (BakingLayout || !FindFirstObjectByType<EventSystem>()) new GameObject("Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        RectTransform OpenPage()
        {
            var page = SceneUi.Page(tab);
            if (!page)
            {
                page = Box(root, SceneUiBindings.PageName(tab), new Rect(0, 0, 1280, SceneUi.DesignSize.y), Color.clear);
                var pages = new List<RectTransform>(SceneUi.Pages) { page }; SceneUi.Pages = pages.ToArray();
                SceneUi.Page(tab);
            }
            if (!BakingLayout) SceneUiBindings.ResetScope(page);
            return page;
        }
        void MarkSlot(RectTransform rect, string key, bool runtimeText = false)
        {
            if (!BakingLayout || TransientUi) { rect.gameObject.AddComponent<RuntimeUiNode>(); return; }
            var slot = rect.gameObject.AddComponent<SceneUiSlot>(); slot.Key = key; slot.RuntimeText = runtimeText;
        }
        public void BakeLayout(string sceneName)
        {
            BakingLayout = true; layoutSceneName = sceneName; InitializeCanvas();
            SceneUi.DesignSize = new Vector2(1280, sceneName == "CharacterCreator" ? 1080 : 800);
            root.sizeDelta = SceneUi.DesignSize; SceneUi.Scaler.referenceResolution = SceneUi.DesignSize;
            repository = new GameObject("Preview Repository").AddComponent<FighterRepository>(); repository.Fighters = FighterData.Samples();
            editing = FighterData.Samples()[1];
            if (sceneName == "Arena")
            {
                SceneUi.Arena = Box(root, "Arena", new Rect(0, 0, 1280, 800), Color.clear);
                arena = SceneUi.Arena.gameObject.AddComponent<BallArena>();
                arena.Begin(this, SceneUi.Arena, FighterData.Samples()[0], FighterData.Samples()[1], () => { });
            }
            else
            {
                string[] tabs = sceneName == "CharacterCreator" ? new[] { "제작", "코드" } : new[] { "메인", "대전", "계정", "관리자" };
                foreach (string value in tabs) { tab = value; Repaint(); }
                SceneUi.Page(tabs[0]);
            }
            DestroyImmediate(repository.gameObject); repository = null;
            BakingLayout = false;
        }
    }
}
