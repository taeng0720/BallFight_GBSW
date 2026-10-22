using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HongBallFight
{
    public static class SceneLayouts
    {
        public const float Width = 1280, Height = 800;
        public const int Version = 7;
        public static readonly string[] Tabs = { "대전", "승률", "아이디어" };
        static readonly string[] TabTitles = { "캐릭터 선택", "승률", "아이디어" };
        static UiRegistry ui;

        public static Camera EnsureCamera()
        {
            var camera = Camera.main;
            if (camera) return camera;
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            go.transform.position = new Vector3(0, 0, -10);
            camera = go.GetComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ui.Background;
            return camera;
        }
        public static void EnsureEventSystem()
        {
            if (!Object.FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static UiRegistry Build(string sceneName, Camera camera)
        {
            var canvasObject = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UiRegistry));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            ui = canvasObject.GetComponent<UiRegistry>(); ui.DesignSize = new Vector2(Width, Height); ui.LayoutVersion = Version;
            Ui.Stretch(Ui.Box(canvasObject.transform, "Background", default, Ui.Background));
            var root = Ui.Group(canvasObject.transform, "Application", new Rect(0, 0, Width, Height));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.anchoredPosition = Vector2.zero;
            ui.Root = root;

            if (sceneName == "Arena") Arena(root);
            else
            {
                TopBar(root);
                Selection(Panel(Page(root, "대전", "Selection"))); Stats(Panel(Page(root, "승률", "Stats"))); Ideas(Panel(Page(root, "아이디어", "Ideas")));
                Reg("footer.message", Ui.Label(root, "", new Rect(32, 744, 1216, 36), 14, Ui.Muted), "Footer Message");
            }
            var result = ui; ui = null; return result;
        }

        static T Reg<T>(string key, T component, string name = null) where T : Component
        {
            if (name != null) component.gameObject.name = name;
            ui.Register(key, component); return component;
        }
        static RectTransform Page(RectTransform root, string tab, string name) => Reg("page." + tab, Ui.Stretch(Ui.Group(root, name, default)));
        static RectTransform Panel(RectTransform page) => Ui.Group(page, "Panel", new Rect(32, 100, 1216, 628));
        static RectTransform Card(Transform parent, string name, Rect rect) => Ui.Frame(parent, name, rect, Color.white, 2, Ui.Border);
        static Text Heading(Transform parent, string value, Rect rect, int size = 20) => Ui.Title(parent, value, rect, size);
        static Text Centered(Text text) { text.alignment = TextAnchor.MiddleCenter; return text; }
        static Text Right(Text text) { text.alignment = TextAnchor.MiddleRight; return text; }
        static RectTransform Template(RectTransform template) { template.gameObject.SetActive(false); return template; }

        static void TopBar(RectTransform root)
        {
            var bar = Reg("topbar", Ui.Group(root, "Top Bar", new Rect(0, 0, Width, 90)));
            Ui.Title(bar, "볼 파이트", new Rect(32, 20, 220, 44), 28).name = "Title";
            for (int i = 0; i < Tabs.Length; i++) Reg("nav." + Tabs[i], Ui.Button(bar, TabTitles[i], new Rect(788 + i * 156, 22, 148, 42)));
            Ui.Box(bar, "Rule", new Rect(32, 80, 1216, 3), Ui.Ink);
        }

        static void Selection(RectTransform panel)
        {
            Ui.Title(panel, "캐릭터 선택", new Rect(0, 12, 600, 40), 28).name = "Title";
            Ui.Label(panel, "1번과 2번 캐릭터를 고르면 두 공이 자동으로 대결합니다.", new Rect(0, 54, 760, 26), 16, Ui.Muted).name = "Subtitle";
            var list = Reg("selection.list", Ui.Scroll(panel, "Fighter List", new Rect(0, 100, 784, 520)));
            Reg("selection.empty", Ui.Label(panel, "공개된 캐릭터가 없습니다.", new Rect(0, 110, 760, 40)), "Empty").gameObject.SetActive(false);

            var template = Reg("selection.card", Ui.Group(list, "Fighter Card (Template)", new Rect(3, 3, 370, 204)));
            Ui.Box(template, "Outline", new Rect(-3, -3, 376, 210), Ui.Ink);
            var body = Card(template, "Body", new Rect(0, 0, 370, 204));
            Ui.Portrait(body, "Avatar", new Rect(18, 18, 60, 60), Ui.Muted);
            Heading(body, "이름", new Rect(92, 16, 260, 32), 22).name = "Name";
            Ui.Label(body, "스킬", new Rect(92, 48, 260, 26), 14, Ui.Muted).name = "Skill";
            Ui.Label(body, "능력치", new Rect(18, 84, 334, 52), 14).name = "Stats";
            Ui.Button(body, "1번으로", new Rect(18, 146, 160, 42)).name = "Pick1";
            Ui.Button(body, "2번으로", new Rect(192, 146, 160, 42)).name = "Pick2";
            Template(template);

            var matchup = Ui.Frame(panel, "Matchup", new Rect(812, 100, 404, 520), Color.white, 3);
            Heading(matchup, "매치업", new Rect(24, 16, 340, 36), 22).name = "Title";
            MatchSlot(matchup, "selection.slot1", "1번", 64);
            Centered(Ui.Title(matchup, "VS", new Rect(0, 202, 404, 48), 28)).name = "VS";
            MatchSlot(matchup, "selection.slot2", "2번", 260);
            Reg("selection.start", Ui.Button(matchup, "대전 시작", new Rect(24, 440, 356, 60), null, true));
        }
        static void MatchSlot(RectTransform parent, string key, string tag, float y)
        {
            var slot = Reg(key, Card(parent, tag + " 슬롯", new Rect(24, y, 356, 128)));
            Ui.Portrait(slot, "Avatar", new Rect(18, 34, 60, 60), Ui.Muted);
            Ui.Label(slot, tag, new Rect(94, 22, 240, 22), 13, Ui.Muted, true).name = "Tag";
            Heading(slot, "-", new Rect(94, 44, 240, 34), 24).name = "Name";
            Ui.Label(slot, "", new Rect(94, 80, 240, 24), 14, Ui.Muted).name = "Stats";
        }

        static void Stats(RectTransform panel)
        {
            Ui.Title(panel, "승률", new Rect(0, 12, 600, 40), 28).name = "Title";
            Reg("stats.summary", Ui.Label(panel, "", new Rect(0, 54, 900, 26), 16, Ui.Muted), "Summary");
            Reg("stats.reset", Ui.Button(panel, "기록 초기화", new Rect(1076, 14, 140, 42)));
            Reg("stats.empty", Ui.Label(panel, "아직 기록이 없습니다. 대전을 하면 결과가 자동으로 기록됩니다.", new Rect(0, 120, 900, 40), 17, Ui.Muted), "Empty");

            var content = Reg("stats.content", Ui.Stretch(Ui.Group(panel, "Records", default)));
            Heading(content, "캐릭터별 승률", new Rect(0, 96, 580, 32)).name = "Standings Title";
            var standings = Reg("stats.standings", Ui.Scroll(content, "Standings", new Rect(0, 136, 600, 484)));
            var row = Reg("stats.standingRow", Card(standings, "Standing Row (Template)", new Rect(0, 0, 584, 76)));
            Centered(Ui.Title(row, "1", new Rect(8, 0, 40, 76), 20)).name = "Rank";
            Ui.Portrait(row, "Avatar", new Rect(52, 14, 48, 48), Ui.Muted);
            Heading(row, "이름", new Rect(114, 10, 250, 28), 18).name = "Name";
            Ui.Label(row, "0전 0승 0패", new Rect(114, 40, 250, 24), 14, Ui.Muted).name = "Record";
            Right(Ui.Title(row, "0%", new Rect(390, 10, 176, 28), 22)).name = "Rate";
            Ui.Box(row, "Track", new Rect(390, 48, 176, 10), Ui.Soft);
            Ui.Box(row, "Fill", new Rect(390, 48, 176, 10), Ui.Ink);
            Template(row);

            Ui.Box(content, "Divider", new Rect(624, 96, 2, 524), Ui.Border);
            Heading(content, "상대 전적", new Rect(648, 96, 568, 32)).name = "Rivalry Title";
            var rivalries = Reg("stats.rivalries", Ui.Scroll(content, "Rivalries", new Rect(648, 136, 568, 200)));
            var pair = Reg("stats.rivalryRow", Card(rivalries, "Rivalry Row (Template)", new Rect(0, 0, 556, 56)));
            Heading(pair, "A", new Rect(16, 0, 190, 56), 16).name = "A";
            Centered(Ui.Title(pair, "0 : 0", new Rect(206, 0, 144, 56), 22)).name = "Score";
            Right(Heading(pair, "B", new Rect(350, 0, 190, 56), 16)).name = "B";
            Centered(Ui.Label(pair, "", new Rect(206, 38, 144, 16), 11, Ui.Muted)).name = "Draws";
            Template(pair);

            Heading(content, "최근 경기", new Rect(648, 352, 568, 32)).name = "Recent Title";
            var recent = Reg("stats.recent", Ui.Scroll(content, "Recent Matches", new Rect(648, 392, 568, 228)));
            var match = Reg("stats.matchRow", Ui.Box(recent, "Match Row (Template)", new Rect(0, 0, 556, 48), Ui.Soft));
            Ui.Label(match, "A vs B", new Rect(16, 0, 300, 48), 15).name = "Players";
            Heading(match, "결과", new Rect(316, 0, 120, 48), 15).name = "Result";
            Right(Ui.Label(match, "", new Rect(436, 0, 104, 48), 12, Ui.Muted)).name = "Time";
            Template(match);
        }

        static void Ideas(RectTransform panel)
        {
            Ui.Title(panel, "아이디어", new Rect(0, 12, 600, 40), 28).name = "Title";
            Ui.Label(panel, "새 캐릭터나 스킬 아이디어를 자유롭게 남겨 주세요.", new Rect(0, 54, 460, 26), 16, Ui.Muted).name = "Subtitle";
            Reg("idea.name", Ui.Input(panel, "캐릭터 이름", new Rect(0, 100, 460, 72)));
            Reg("idea.author", Ui.Input(panel, "작성자 (선택)", new Rect(0, 188, 460, 72)));
            Reg("idea.description", Ui.Input(panel, "스킬 · 특징 설명", new Rect(0, 276, 460, 200), multiline: true));
            Reg("idea.submit", Ui.Button(panel, "아이디어 제출", new Rect(0, 500, 460, 56), null, true));

            Ui.Box(panel, "Divider", new Rect(500, 96, 2, 524), Ui.Border);
            Heading(panel, "올라온 아이디어", new Rect(532, 92, 400, 40), 22).name = "List Title";
            Reg("idea.count", Right(Ui.Label(panel, "", new Rect(940, 92, 276, 40), 14, Ui.Muted)), "Count");
            var list = Reg("idea.list", Ui.Scroll(panel, "Idea List", new Rect(532, 140, 684, 480)));
            Reg("idea.empty", Ui.Label(panel, "아직 아이디어가 없습니다. 첫 아이디어를 남겨 보세요!", new Rect(532, 140, 660, 40), 16, Ui.Muted), "Empty");
            var row = Reg("idea.row", Card(list, "Idea Row (Template)", new Rect(0, 0, 672, 126)));
            Heading(row, "이름", new Rect(18, 10, 636, 30), 18).name = "Name";
            var description = Ui.Label(row, "설명", new Rect(18, 44, 636, 52), 15); description.name = "Description"; description.alignment = TextAnchor.UpperLeft;
            Ui.Label(row, "", new Rect(18, 98, 636, 22), 13, Ui.Muted).name = "Meta";
            Template(row);
        }

        static void Arena(RectTransform root)
        {
            Reg("arena.back", Ui.Button(root, "← 캐릭터 선택", new Rect(32, 20, 180, 48)), "Back");
            Reg("arena.restart", Ui.Button(root, "다시하기", new Rect(1068, 20, 180, 48)), "Restart");
            Ui.Box(root, "Arena Border", new Rect(334, 14, 612, 612), Ui.Ink);
            var stage = Reg("arena.stage", Ui.Box(root, "Stage", new Rect(340, 20, 600, 600), Color.white));
            stage.gameObject.AddComponent<RectMask2D>();
            Reg("arena.countdown", Centered(Ui.Label(root, "3", new Rect(540, 260, 200, 120), 96, Ui.Ink, true)), "Countdown");
            Hud(root, "arena.left", "1번 정보", new Rect(334, 636, 298, 116), false);
            Hud(root, "arena.right", "2번 정보", new Rect(648, 636, 298, 116), true);

            var result = Reg("arena.result", Ui.Group(root, "Result", new Rect(340, 20, 600, 600)));
            Ui.Box(result, "Dim", new Rect(0, 0, 600, 600), new Color(1, 1, 1, .82f)).GetComponent<Image>().raycastTarget = true;
            Reg("arena.resultTitle", Centered(Ui.Label(result, "이름", new Rect(0, 170, 600, 120), 76, Ui.Ink, true)), "Winner");
            Reg("arena.resultLabel", Centered(Ui.Label(result, "승리!", new Rect(0, 290, 600, 70), 44, Ui.Ink, true)), "Label");
            Reg("arena.resultDetail", Centered(Ui.Label(result, "", new Rect(0, 362, 600, 32), 18, Ui.Muted)), "Detail");
            Reg("arena.resultRestart", Ui.Button(result, "다시하기", new Rect(140, 428, 152, 56), null, true), "Restart");
            Reg("arena.resultBack", Ui.Button(result, "캐릭터 선택", new Rect(308, 428, 152, 56)), "Back");
            result.gameObject.SetActive(false);
        }
        static void Hud(RectTransform root, string key, string name, Rect rect, bool mirrored)
        {
            var hud = Reg(key, Ui.Group(root, name, rect));
            float w = rect.width;
            var title = Ui.Title(hud, "이름", new Rect(0, 0, w, 44), 32); title.name = "Name";
            title.alignment = mirrored ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            Ui.Box(hud, "Bar", new Rect(0, 52, w, 42), Ui.HpBorder);
            Ui.Box(hud, "Fill", new Rect(3, 55, w - 6, 36), Ui.HpGreen);
            var hp = Centered(Ui.Title(hud, "0 / 0", new Rect(0, 52, w, 42), 24)); hp.name = "Hp"; hp.color = Color.white;
            var shadow = hp.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0, 0, 0, .35f); shadow.effectDistance = new Vector2(1, -1);
            Ui.Box(hud, "Skill Track", new Rect(0, 100, w, 8), Ui.Soft);
            Ui.Box(hud, "Skill Fill", new Rect(0, 100, w, 8), Ui.Ink);
        }
    }
}
