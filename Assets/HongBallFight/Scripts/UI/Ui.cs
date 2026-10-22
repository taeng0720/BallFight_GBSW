using System;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public static class Ui
    {
        public static readonly Color Background = Color.white;
        public static readonly Color Soft = new Color32(243, 244, 246, 255);
        public static readonly Color Border = new Color32(214, 217, 222, 255);
        public static readonly Color Ink = new Color32(20, 22, 26, 255);
        public static readonly Color TextColor = Ink;
        public static readonly Color Muted = new Color32(118, 124, 134, 255);
        public static readonly Color Danger = new Color32(214, 64, 64, 255);
        public static readonly Color HpGreen = new Color32(58, 196, 74, 255);
        public static readonly Color HpBorder = new Color32(34, 48, 36, 255);

        static Font font;
        static Sprite circle;
        public static Font Font => font ? font : font = Resources.Load<Font>("Fonts/NotoSansKR") ?? Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 24);
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
        public static RectTransform Frame(Transform parent, string name, Rect rect, Color fill, float border = 3, Color? borderColor = null)
        {
            var frame = Box(parent, name, rect, borderColor ?? Ink);
            Box(frame, "Fill", new Rect(border, border, rect.width - border * 2, rect.height - border * 2), fill);
            return frame;
        }
        public static Text Label(Transform parent, string value, Rect rect, int size = 18, Color? color = null, bool bold = false)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size;
            var text = go.AddComponent<Text>(); text.font = Font; text.text = value; text.fontSize = size; text.lineSpacing = 1.1f;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color ?? TextColor; text.supportRichText = false; text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft; text.verticalOverflow = VerticalWrapMode.Overflow; return text;
        }
        public static Text Title(Transform parent, string value, Rect rect, int size = 26) => Label(parent, value, rect, size, TextColor, true);
        public static Button Button(Transform parent, string value, Rect rect, Action action = null, bool primary = false)
        {
            float border = rect.height <= 40 ? 2 : 3;
            var frame = Box(parent, value, rect, Ink); frame.GetComponent<Image>().raycastTarget = true;
            var face = Box(frame, "Face", new Rect(border, border, rect.width - border * 2, rect.height - border * 2), primary ? Ink : Color.white);
            var button = frame.gameObject.AddComponent<Button>(); button.targetGraphic = face.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None }; button.colors = Tint();
            int size = rect.height < 36 ? 15 : rect.height < 46 ? 17 : 19;
            var text = Label(frame, value, new Rect(4, 0, rect.width - 8, rect.height), size, primary ? Color.white : Ink, true);
            text.name = "Label"; text.alignment = TextAnchor.MiddleCenter;
            if (action != null) button.onClick.AddListener(() => action());
            return button;
        }
        public static void SetButtonStyle(Button button, bool primary)
        {
            button.targetGraphic.color = primary ? Ink : Color.white;
            button.GetComponentInChildren<Text>(true).color = primary ? Color.white : Ink;
        }
        public static void SetText(Button button, string value) => button.GetComponentInChildren<Text>(true).text = value;
        public static ColorBlock Tint()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white; colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(.9f, .9f, .9f); colors.pressedColor = new Color(.75f, .75f, .75f);
            colors.disabledColor = new Color(1, 1, 1, .45f); colors.fadeDuration = .06f;
            return colors;
        }
        public static InputField Input(Transform parent, string title, Rect rect, bool password = false, bool multiline = false)
        {
            var group = Group(parent, title, rect);
            Label(group, title, new Rect(0, 0, rect.width, 22), 14, Muted, true).name = "Title";
            float height = multiline ? rect.height - 28 : 44;
            var field = Box(group, "Field", new Rect(0, 28, rect.width, height), Ink); field.GetComponent<Image>().raycastTarget = true;
            var face = Box(field, "Face", new Rect(2, 2, rect.width - 4, height - 4), Color.white);
            var input = field.gameObject.AddComponent<InputField>(); input.targetGraphic = face.GetComponent<Image>(); input.colors = Tint();
            var text = Label(field, "", multiline ? new Rect(14, 10, rect.width - 28, height - 20) : new Rect(14, 0, rect.width - 28, height), 17);
            text.name = "Value"; input.textComponent = text;
            if (multiline) { text.alignment = TextAnchor.UpperLeft; input.lineType = InputField.LineType.MultiLineNewline; }
            input.selectionColor = new Color(.6f, .65f, .75f, .45f); input.caretColor = Ink;
            input.characterLimit = password ? 128 : multiline ? 300 : 80;
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
