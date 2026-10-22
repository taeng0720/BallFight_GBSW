using System;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public static class Ui
    {
        public static readonly Color Background = new Color32(242, 244, 248, 255);
        public static readonly Color Surface = Color.white;
        public static readonly Color Soft = new Color32(235, 238, 245, 255);
        public static readonly Color Border = new Color32(222, 226, 236, 255);
        public static readonly Color Stage = new Color32(246, 248, 252, 255);
        public static readonly Color TextColor = new Color32(30, 35, 48, 255);
        public static readonly Color Muted = new Color32(120, 128, 148, 255);
        public static readonly Color Accent = new Color32(76, 91, 240, 255);
        public static readonly Color Danger = new Color32(226, 82, 82, 255);

        static Font font;
        static Sprite rounded, circle;
        public static Font Font => font ? font : font = Resources.Load<Font>("Fonts/NotoSansKR") ?? Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
        public static Sprite Rounded => rounded ? rounded : rounded = Resources.Load<Sprite>("UI/Rounded");
        public static Sprite Circle => circle ? circle : circle = Resources.Load<Sprite>("UI/Circle");

        public static RectTransform Box(Transform parent, string name, Rect rect, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            return rt;
        }
        public static RectTransform Group(Transform parent, string name, Rect rect)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size;
            return rt;
        }
        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero; return rt;
        }
        public static RectTransform Card(Transform parent, string name, Rect rect, Color color, float radius = 14, bool shadow = false)
        {
            var rt = Box(parent, name, rect, color);
            var image = rt.GetComponent<Image>(); image.sprite = Rounded; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 24f / Mathf.Max(1, radius);
            if (shadow) { var effect = rt.gameObject.AddComponent<Shadow>(); effect.effectColor = new Color(.1f, .12f, .25f, .08f); effect.effectDistance = new Vector2(0, -3); }
            return rt;
        }
        public static Text Label(Transform parent, string value, Rect rect, int size = 18, Color? color = null, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size;
            var text = go.AddComponent<Text>(); text.font = Font; text.text = value; text.fontSize = size; text.lineSpacing = 1.1f;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color ?? TextColor; text.supportRichText = false; text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft; return text;
        }
        public static Text Title(Transform parent, string value, Rect rect, int size = 26) => Label(parent, value, rect, size, TextColor, true);
        public static Button Button(Transform parent, string value, Rect rect, Action action = null, bool primary = false)
        {
            var rt = Card(parent, value, rect, primary ? Accent : Soft, Mathf.Min(10, rect.height / 2));
            var image = rt.GetComponent<Image>(); image.raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.colors = Tint();
            var text = Label(rt, value, new Rect(6, 0, rect.width - 12, rect.height), rect.height < 36 ? 15 : 17, primary ? Color.white : TextColor, primary);
            text.name = "Label"; text.alignment = TextAnchor.MiddleCenter;
            if (action != null) button.onClick.AddListener(() => action());
            return button;
        }
        public static void SetButtonStyle(Button button, bool primary)
        {
            button.targetGraphic.color = primary ? Accent : Soft;
            var text = button.GetComponentInChildren<Text>(true); text.color = primary ? Color.white : TextColor; text.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
        }
        public static void SetText(Button button, string value) => button.GetComponentInChildren<Text>(true).text = value;
        public static ColorBlock Tint()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white; colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(.92f, .93f, .97f); colors.pressedColor = new Color(.8f, .82f, .9f);
            colors.disabledColor = new Color(1, 1, 1, .45f); colors.fadeDuration = .08f;
            return colors;
        }
        public static InputField Input(Transform parent, string title, Rect rect, bool password = false)
        {
            var group = Group(parent, title, rect);
            Label(group, title, new Rect(2, 0, rect.width, 22), 14, Muted).name = "Title";
            var rt = Card(group, "Field", new Rect(0, 26, rect.width, 42), Soft, 10);
            var image = rt.GetComponent<Image>(); image.raycastTarget = true;
            var input = rt.gameObject.AddComponent<InputField>(); input.targetGraphic = image; input.colors = Tint();
            var text = Label(rt, "", new Rect(14, 0, rect.width - 28, 42), 17); text.name = "Value"; input.textComponent = text;
            input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, .3f); input.caretColor = TextColor;
            input.characterLimit = password ? 128 : 80;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            return input;
        }
        public static RectTransform Scroll(Transform parent, string name, Rect rect)
        {
            var viewport = Box(parent, name, rect, Color.clear); viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Group(viewport, "Content", new Rect(0, 0, rect.width - 12, rect.height));
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            return content;
        }
        public static void SetContentHeight(RectTransform content, float height)
        {
            var viewport = (RectTransform)content.parent;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(viewport.rect.height, height));
        }
        public static RectTransform Portrait(Transform parent, string name, Rect rect, Color color)
        {
            var rt = Box(parent, name, rect, color); rt.GetComponent<Image>().sprite = Circle;
            var effect = rt.gameObject.AddComponent<Shadow>(); effect.effectColor = new Color(0, 0, 0, .14f); effect.effectDistance = new Vector2(0, -Mathf.Max(2, rect.height * .05f));
            rt.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            float ring = Mathf.Max(2, rect.width * .05f);
            var face = Box(rt, "Face", new Rect(ring, ring, rect.width - ring * 2, rect.height - ring * 2), Color.white);
            face.gameObject.SetActive(false);
            var number = Label(rt, "", new Rect(0, 0, rect.width, rect.height), Mathf.RoundToInt(rect.height * .42f), Color.white, true);
            number.name = "Number"; number.alignment = TextAnchor.MiddleCenter;
            return rt;
        }
        public static void SetPortrait(RectTransform portrait, FighterData fighter, string number = "")
        {
            portrait.GetComponent<Image>().color = ParseColor(fighter?.color);
            var face = fighter != null ? FighterPortraits.Face(fighter.name) : null;
            var faceImage = Child<Image>(portrait, "Face");
            if (faceImage) { faceImage.sprite = face; faceImage.gameObject.SetActive(face); }
            var label = Child<Text>(portrait, "Number");
            if (label) { label.text = number; label.gameObject.SetActive(!face); }
        }
        public static Color ParseColor(string hex) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var color) ? color : Muted;

        public static T Child<T>(Transform root, string name) where T : Component
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
                if (item != root && item.name == name) return item.GetComponent<T>();
            return null;
        }
        public static void Clear(Transform container, params Transform[] keep)
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                var child = container.GetChild(i);
                if (System.Array.IndexOf(keep, child) >= 0) continue;
                child.gameObject.SetActive(false); UnityEngine.Object.Destroy(child.gameObject);
            }
        }
        public static RectTransform Clone(RectTransform template, Vector2 position)
        {
            var copy = UnityEngine.Object.Instantiate(template, template.parent, false);
            copy.gameObject.SetActive(true); copy.anchoredPosition = position; return copy;
        }
    }
}
