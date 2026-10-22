using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HongBallFight
{
    public static class SceneLayouts
    {
        public const float Width = 1280;
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
            float height = sceneName == "CharacterCreator" ? 1080 : 800;
            var canvasObject = new GameObject("Game UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UiRegistry));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, height); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            ui = canvasObject.GetComponent<UiRegistry>(); ui.DesignSize = new Vector2(Width, height);
            Ui.Stretch(Ui.Box(canvasObject.transform, "Background", default, Ui.Background));
            var root = Ui.Group(canvasObject.transform, "Application", new Rect(0, 0, Width, height));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.anchoredPosition = Vector2.zero;
            ui.Root = root;

            if (sceneName == "Arena") Arena(root);
            else if (sceneName == "CharacterCreator")
            {
                TopBar(root); Sandbox(root); Form(Page(root, "제작", "Form")); Studio(Page(root, "코드", "Block Studio"));
                Footer(root, 1006); Dialog(root);
            }
            else
            {
                Home(Page(root, "메인", "Home")); TopBar(root);
                Selection(Page(root, "대전", "Selection")); Account(Page(root, "계정", "Account")); Admin(Page(root, "관리자", "Admin"));
                Footer(root, 740); Dialog(root);
            }
            var result = ui; ui = null; return result;
        }

        static T Reg<T>(string key, T component, string name = null) where T : Component
        {
            if (name != null) component.gameObject.name = name;
            ui.Register(key, component); return component;
        }
        static RectTransform Page(RectTransform root, string tab, string name)
        {
            var page = Ui.Stretch(Ui.Group(root, name, default));
            return Reg("page." + tab, page);
        }
        static RectTransform PageCard(RectTransform page, float y, float height) => Ui.Card(page, "Panel", new Rect(32, y, 1216, height), Ui.Surface, 18, true);
        static Text Centered(Text text) { text.alignment = TextAnchor.MiddleCenter; return text; }

        static void TopBar(RectTransform root)
        {
            var bar = Reg("topbar", Ui.Group(root, "Top Bar", new Rect(0, 0, Width, 80)));
            Ui.Box(bar, "Logo", new Rect(32, 24, 32, 32), Ui.Accent).GetComponent<Image>().sprite = Ui.Circle;
            Ui.Title(bar, "볼 파이트", new Rect(76, 18, 200, 44), 23).name = "Title";
            var tabs = new[] { "메인", "대전", "제작", "계정", "관리자" };
            var titles = new[] { "홈", "대전", "캐릭터 제작", "계정", "관리자" };
            var nav = Ui.Card(bar, "Navigation", new Rect(300, 16, tabs.Length * 128 + 8, 50), Ui.Soft, 14);
            for (int i = 0; i < tabs.Length; i++) Reg("nav." + tabs[i], Ui.Button(nav, titles[i], new Rect(4 + i * 128, 4, 124, 42)));
            Reg("nav.refresh", Ui.Button(bar, "새로고침", new Rect(1128, 20, 120, 42)));
        }
        static void Footer(RectTransform root, float y) => Reg("footer.message", Ui.Label(root, "", new Rect(40, y, 1200, 40), 14, Ui.Muted), "Footer Message");

        static void Dialog(RectTransform root)
        {
            var dialog = Reg("dialog", Ui.Stretch(Ui.Box(root, "Dialog", default, new Color(.08f, .1f, .16f, .55f))));
            dialog.GetComponent<Image>().raycastTarget = true;
            var panel = Reg("dialog.panel", Ui.Card(dialog, "Panel", new Rect(0, 0, 680, 520), Ui.Surface, 20, true));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f); panel.anchoredPosition = Vector2.zero;
            Reg("dialog.body", Ui.Stretch(Ui.Group(panel, "Body", default)));
            Reg("dialog.title", Ui.Title(panel, "", new Rect(24, 12, 560, 34), 21), "Title");
            var close = Reg("dialog.close", Ui.Button(panel, "×", new Rect(0, 0, 36, 36)), "Close");
            var rt = (RectTransform)close.transform; rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.one; rt.anchoredPosition = new Vector2(-16, -12);
            dialog.gameObject.SetActive(false);
        }

        static void Home(RectTransform page)
        {
            Reg("home.account", Ui.Button(page, "계정", new Rect(1032, 24, 100, 40)));
            Reg("home.admin", Ui.Button(page, "관리자", new Rect(1144, 24, 104, 40)));
            Reg("home.ball1", Ui.Portrait(page, "1번 공", new Rect(486, 150, 104, 104), Ui.Muted));
            Centered(Ui.Label(page, "VS", new Rect(590, 150, 100, 104), 26, Ui.Muted, true)).name = "VS";
            Reg("home.ball2", Ui.Portrait(page, "2번 공", new Rect(690, 150, 104, 104), Ui.Muted));
            Centered(Ui.Title(page, "볼 파이트", new Rect(240, 290, 800, 84), 60)).name = "Title";
            Centered(Ui.Label(page, "두 개의 공이 부딪히며 싸우는 자동 대전 게임", new Rect(240, 374, 800, 32), 18, Ui.Muted)).name = "Caption";
            Reg("home.start", Ui.Button(page, "게임 시작", new Rect(460, 452, 360, 60), null, true));
            Reg("home.create", Ui.Button(page, "캐릭터 만들기", new Rect(460, 526, 360, 52)));
            Centered(Ui.Label(page, "C 키를 누르면 바로 캐릭터 제작으로 이동합니다", new Rect(240, 730, 800, 30), 14, Ui.Muted)).name = "Hint";
        }
        static void Selection(RectTransform page)
        {
            var card = PageCard(page, 100, 628);
            Ui.Title(card, "캐릭터 선택", new Rect(28, 20, 600, 40)).name = "Title";
            Ui.Label(card, "1번과 2번 캐릭터를 고르면 두 공이 자동으로 대결합니다.", new Rect(28, 60, 760, 26), 16, Ui.Muted).name = "Subtitle";
            var list = Reg("selection.list", Ui.Scroll(card, "Fighter List", new Rect(28, 104, 772, 500)));
            Reg("selection.empty", Ui.Label(card, "공개된 캐릭터가 없습니다. 캐릭터 제작에서 첫 등록을 요청하세요.", new Rect(28, 120, 760, 40)), "Empty").gameObject.SetActive(false);

            var template = Reg("selection.card", Ui.Group(list, "Fighter Card (Template)", new Rect(2, 2, 366, 200)));
            Ui.Card(template, "Outline", new Rect(-2, -2, 370, 204), Ui.Accent, 16);
            Ui.Card(template, "Body", new Rect(0, 0, 366, 200), Ui.Background, 14);
            Ui.Portrait(template, "Avatar", new Rect(18, 18, 56, 56), Ui.Muted);
            Ui.Title(template, "이름", new Rect(88, 16, 264, 32), 21).name = "Name";
            Ui.Label(template, "스킬", new Rect(88, 46, 264, 26), 14, Ui.Muted).name = "Skill";
            Ui.Label(template, "능력치", new Rect(18, 90, 330, 30), 15).name = "Stats";
            Ui.Button(template, "1번으로", new Rect(18, 142, 157, 40)).name = "Pick1";
            Ui.Button(template, "2번으로", new Rect(191, 142, 157, 40)).name = "Pick2";
            template.gameObject.SetActive(false);

            var panel = Ui.Card(card, "Matchup", new Rect(824, 104, 364, 500), Ui.Background, 14);
            Ui.Title(panel, "매치업", new Rect(20, 14, 320, 32), 18).name = "Title";
            MatchSlot(panel, "selection.slot1", "1번", 56);
            Centered(Ui.Label(panel, "VS", new Rect(0, 186, 364, 40), 22, Ui.Muted, true)).name = "VS";
            MatchSlot(panel, "selection.slot2", "2번", 232);
            Reg("selection.start", Ui.Button(panel, "대전 시작", new Rect(20, 420, 324, 60), null, true));
        }
        static void MatchSlot(RectTransform panel, string key, string tag, float y)
        {
            var slot = Reg(key, Ui.Card(panel, tag + " 슬롯", new Rect(20, y, 324, 124), Ui.Surface, 12));
            Ui.Portrait(slot, "Avatar", new Rect(18, 32, 60, 60), Ui.Muted);
            Ui.Label(slot, tag, new Rect(94, 20, 210, 22), 13, Ui.Muted).name = "Tag";
            Ui.Title(slot, "-", new Rect(94, 42, 210, 34), 22).name = "Name";
            Ui.Label(slot, "", new Rect(94, 78, 210, 24), 14, Ui.Muted).name = "Stats";
        }
        static void Account(RectTransform page)
        {
            var card = PageCard(page, 100, 628);
            Reg("account.title", Ui.Title(card, "제작자 로그인", new Rect(28, 18, 440, 40)), "Title");
            var login = Reg("account.loginGroup", Ui.Group(card, "Login", new Rect(0, 0, 500, 400)));
            Reg("account.email", Ui.Input(login, "이메일", new Rect(28, 90, 440, 70)));
            Reg("account.password", Ui.Input(login, "비밀번호 · 8자 이상", new Rect(28, 180, 440, 70), true));
            Reg("account.login", Ui.Button(login, "로그인", new Rect(28, 284, 212, 48), null, true));
            Reg("account.signup", Ui.Button(login, "회원가입", new Rect(256, 284, 212, 48)));
            Reg("account.logout", Ui.Button(card, "로그아웃", new Rect(28, 90, 230, 48))).gameObject.SetActive(false);
            Ui.Box(card, "Divider", new Rect(500, 24, 1, 576), Ui.Border);
            Ui.Title(card, "내 등록 요청", new Rect(532, 18, 650, 40), 20).name = "List Title";
            var list = Reg("account.list", Ui.Scroll(card, "Request List", new Rect(532, 72, 656, 532)));
            Reg("account.empty", Ui.Label(card, "아직 보낸 요청이 없습니다.", new Rect(536, 72, 600, 40), 16, Ui.Muted), "Empty");
            var row = Reg("account.row", Ui.Card(list, "Request Row (Template)", new Rect(0, 0, 636, 80), Ui.Background, 12));
            Ui.Title(row, "이름 · 상태", new Rect(18, 8, 600, 32), 18).name = "Name";
            Ui.Label(row, "검토 메모", new Rect(18, 40, 600, 30), 14, Ui.Muted).name = "Note";
            row.gameObject.SetActive(false);
        }
        static void Admin(RectTransform page)
        {
            var card = PageCard(page, 100, 628);
            var locked = Reg("admin.locked", Ui.Stretch(Ui.Group(card, "Locked", default)));
            Ui.Title(locked, "관리자 전용 공간", new Rect(28, 24, 1100, 44)).name = "Title";
            Ui.Label(locked, "서버에 등록된 관리자 계정으로 로그인하면\n캐릭터 등록 승인·반려 및 기존 캐릭터의 밸런스 수정이 가능합니다.", new Rect(28, 80, 1100, 70), 18, Ui.Muted).name = "Description";
            var notice = Ui.Card(locked, "Notice", new Rect(28, 170, 640, 56), Ui.Background, 12);
            Reg("admin.notice", Ui.Label(notice, "", new Rect(18, 0, 610, 56), 16, Ui.Accent), "Notice Text");

            var group = Reg("admin.listGroup", Ui.Stretch(Ui.Group(card, "Requests", default)));
            Ui.Title(group, "등록 요청 / 밸런스 패치", new Rect(28, 18, 1000, 40)).name = "Title";
            var list = Reg("admin.list", Ui.Scroll(group, "Request List", new Rect(28, 80, 1160, 520)));
            var row = Reg("admin.row", Ui.Card(list, "Row (Template)", new Rect(0, 0, 1140, 56), Ui.Background, 10));
            Ui.Label(row, "내용", new Rect(18, 0, 900, 56), 17).name = "Label";
            Ui.Button(row, "검토", new Rect(970, 8, 160, 40)).name = "Action";
            row.gameObject.SetActive(false);
            var heading = Reg("admin.heading", Ui.Label(list, "공개 캐릭터 · 수정 요청을 만든 후 검토에서 승인하세요.", new Rect(4, 0, 1100, 44), 15, Ui.Muted), "Heading (Template)");
            heading.gameObject.SetActive(false);
            group.gameObject.SetActive(false);
        }

        static void Sandbox(RectTransform root)
        {
            var card = Reg("sandbox", Ui.Card(root, "Test Arena", new Rect(32, 96, 1216, 302), Ui.Surface, 18, true));
            var stage = Reg("sandbox.stage", Ui.Box(card, "Stage", new Rect(16, 16, 675, 270), Ui.Stage));
            stage.gameObject.AddComponent<RectMask2D>();
            const float x = 715, half = 236, right = x + 249;
            Reg("sandbox.stats", Ui.Title(card, "", new Rect(x, 14, 485, 30), 15), "Stats");
            Reg("sandbox.skill", Ui.Button(card, "1번 스킬 테스트", new Rect(x, 52, half, 36), null, true));
            Reg("sandbox.resetCooldown", Ui.Button(card, "쿨타임 초기화", new Rect(right, 52, half, 36)));
            Reg("sandbox.wall", Ui.Button(card, "벽에 부딪히기", new Rect(x, 96, half, 36)));
            Reg("sandbox.dummy", Ui.Button(card, "샌드백에 부딪히기", new Rect(right, 96, half, 36)));
            Reg("sandbox.restart", Ui.Button(card, "테스트 재시작", new Rect(x, 140, half, 36)));
            Reg("sandbox.pause", Ui.Button(card, "일시정지 / 재개", new Rect(right, 140, half, 36)));
            var speeds = new[] { "0.5배", "1배", "2배", "4배" };
            for (int i = 0; i < speeds.Length; i++) Reg("sandbox.speed" + i, Ui.Button(card, speeds[i], new Rect(x + i * 123, 184, 115, 32), null, i == 1));
            Reg("sandbox.lock", Ui.Button(card, "샌드백 고정: 꺼짐", new Rect(x, 224, 485, 32)));
            Reg("sandbox.console", Ui.Label(card, "", new Rect(x, 262, 485, 28), 12, Ui.Muted), "Console");
        }
        static void Form(RectTransform page)
        {
            var card = PageCard(page, 414, 580);
            Reg("form.title", Ui.Title(card, "나만의 파이터 제작", new Rect(28, 18, 800, 40)), "Title");
            Reg("form.load", Ui.Button(card, "불러오기", new Rect(920, 20, 128, 38)));
            Reg("form.new", Ui.Button(card, "새 캐릭터", new Rect(1060, 20, 128, 38)));
            Reg("form.subtitle", Ui.Label(card, "", new Rect(28, 58, 1000, 26), 16, Ui.Muted), "Subtitle");
            Reg("form.name", Ui.Input(card, "캐릭터 이름", new Rect(28, 104, 350, 70)));
            Reg("form.color", Ui.Input(card, "공 색상 · HEX", new Rect(398, 104, 222, 70)));
            Reg("form.hp", Ui.Input(card, "체력 · 30~1000", new Rect(28, 194, 165, 70)));
            Reg("form.attack", Ui.Input(card, "공격력 · 1~100", new Rect(213, 194, 165, 70)));
            Reg("form.speed", Ui.Input(card, "속도 · 100~500", new Rect(398, 194, 222, 70)));
            Reg("form.skill_power", Ui.Input(card, "스킬 위력 · 1~150", new Rect(28, 284, 350, 70)));
            Reg("form.cooldown", Ui.Input(card, "쿨타임 · 2~20초", new Rect(398, 284, 222, 70)));
            Ui.Label(card, "스킬", new Rect(30, 376, 600, 22), 14, Ui.Muted).name = "Skill Title";
            Reg("form.skill", Ui.Button(card, "스킬  ▾", new Rect(28, 402, 592, 42)), "Skill");
            Ui.Box(card, "Divider", new Rect(652, 104, 1, 440), Ui.Border);
            Ui.Title(card, "스킬 설명", new Rect(688, 100, 480, 34), 20).name = "Info Title";
            var info = Ui.Card(card, "Skill Info", new Rect(688, 142, 500, 180), Ui.Background, 12);
            Reg("form.info", Ui.Label(info, "", new Rect(20, 12, 460, 156), 17, Ui.Muted), "Info Text");
            Reg("form.blocks", Ui.Button(card, "블록으로 스킬 만들기  →", new Rect(688, 336, 500, 42)));
            Reg("form.submit", Ui.Button(card, "관리자에게 등록 요청  →", new Rect(688, 486, 500, 48), null, true));
            var admin = Reg("form.adminGroup", Ui.Group(card, "Review", new Rect(688, 392, 500, 150)));
            Reg("form.note", Ui.Input(admin, "검토 사유", new Rect(0, 0, 500, 70)));
            Reg("form.reject", Ui.Button(admin, "반려", new Rect(0, 94, 150, 48)));
            Reg("form.approve", Ui.Button(admin, "수정값으로 승인", new Rect(166, 94, 334, 48), null, true));
            admin.gameObject.SetActive(false);
        }
        static void Studio(RectTransform page)
        {
            var card = PageCard(page, 414, 580);
            Ui.Title(card, "스킬 블록 공방", new Rect(16, 6, 260, 38), 22).name = "Title";
            Reg("studio.check", Ui.Button(card, "블록 검사", new Rect(282, 8, 145, 34)));
            Reg("studio.apply", Ui.Button(card, "테스트 적용", new Rect(438, 8, 160, 34), null, true));
            Reg("studio.examples", Ui.Button(card, "예제 선택", new Rect(610, 8, 138, 34)));
            Reg("studio.event", Ui.Button(card, "스킬 사용 시  ▾", new Rect(762, 8, 286, 34)), "Event");
            Reg("studio.back", Ui.Button(card, "설정으로", new Rect(1060, 8, 138, 34)));
            var categories = new[] { "전투", "생성", "꾸미기", "제어", "값", "이미지" };
            for (int i = 0; i < categories.Length; i++)
                Reg("studio.cat" + i, Ui.Button(card, categories[i], new Rect(16 + i % 3 * 82, 53 + i / 3 * 38, 76, 32), null, i == 0));
            Reg("studio.palette", Ui.Scroll(card, "Block Palette", new Rect(16, 135, 242, 318)));
            var images = Reg("studio.imagePanel", Ui.Group(card, "Image Tools", new Rect(16, 135, 242, 330)));
            Reg("studio.imageKey", Ui.Input(images, "이미지 이름(영문)", new Rect(0, 0, 242, 70)));
            Reg("studio.imageLoad", Ui.Button(images, "이미지 불러오기", new Rect(0, 77, 242, 34), null, true));
            Reg("studio.imageCreate", Ui.Button(images, "도형 이미지 만들기", new Rect(0, 120, 242, 34)));
            Reg("studio.imagePortrait", Ui.Button(images, "캐릭터 이미지 선택", new Rect(0, 163, 242, 34)));
            Reg("studio.imageManage", Ui.Button(images, "등록 이미지 관리", new Rect(0, 206, 242, 34)));
            Ui.Label(images, "PNG · JPG · WEBP\n가져올 때 최대 256px로 축소\n기본 이미지: orb / star / missile\n이미지 이름을 발사 블록에서 선택", new Rect(0, 247, 242, 74), 12, Ui.Muted).name = "Help";
            images.gameObject.SetActive(false);
            var trash = Reg("studio.trash", Ui.Card(card, "Trash", new Rect(16, 464, 242, 36), new Color32(250, 228, 228, 255), 10));
            Centered(Ui.Label(trash, "블록을 여기에 놓아 삭제", new Rect(8, 0, 226, 36), 14, Ui.Danger));
            Reg("studio.workspace", Ui.Scroll(card, "Workspace", new Rect(282, 55, 916, 445)));
            Reg("studio.status", Ui.Label(card, "", new Rect(16, 506, 1182, 48), 15, Ui.Muted), "Status");
        }

        static void Arena(RectTransform root)
        {
            Reg("arena.back", Ui.Button(root, "←  캐릭터 선택", new Rect(32, 24, 180, 42)), "Back");
            Reg("arena.restart", Ui.Button(root, "다시하기", new Rect(1068, 24, 180, 42)), "Restart");
            var timer = Ui.Card(root, "Timer", new Rect(570, 22, 140, 46), Ui.Surface, 23, true);
            Reg("arena.clock", Centered(Ui.Title(timer, "00:00", new Rect(0, 0, 140, 46), 22)), "Clock");
            Hud(root, "arena.left", "1번 정보", new Rect(32, 84, 520, 96), false);
            Hud(root, "arena.right", "2번 정보", new Rect(728, 84, 520, 96), true);
            Ui.Card(root, "Arena Frame", new Rect(132, 196, 1016, 416), Ui.Surface, 20, true);
            var stage = Reg("arena.stage", Ui.Box(root, "Stage", new Rect(140, 204, 1000, 400), Ui.Stage));
            stage.gameObject.AddComponent<RectMask2D>();
            Reg("arena.feed", Centered(Ui.Label(root, "", new Rect(140, 624, 1000, 32), 15, Ui.Muted)), "Feed");

            var result = Reg("arena.result", Ui.Card(root, "Result", new Rect(430, 300, 420, 200), Ui.Surface, 20, true));
            Reg("arena.resultTitle", Centered(Ui.Title(result, "승리!", new Rect(0, 24, 420, 52), 32)), "Title");
            Reg("arena.resultDetail", Centered(Ui.Label(result, "", new Rect(0, 78, 420, 28), 15, Ui.Muted)), "Detail");
            Reg("arena.resultRestart", Ui.Button(result, "다시하기", new Rect(24, 128, 178, 48), null, true));
            Reg("arena.resultBack", Ui.Button(result, "캐릭터 선택", new Rect(218, 128, 178, 48)));
            result.gameObject.SetActive(false);
        }
        static void Hud(RectTransform root, string key, string name, Rect rect, bool mirrored)
        {
            var card = Reg(key, Ui.Card(root, name, rect, Ui.Surface, 16, true));
            float w = rect.width, ballX = mirrored ? w - 78 : 18, textX = mirrored ? 18 : 94, textWidth = w - 112;
            Ui.Portrait(card, "Avatar", new Rect(ballX, 18, 60, 60), Ui.Muted);
            var title = Ui.Title(card, "이름", new Rect(textX, 12, textWidth, 30), 20); title.name = "Name";
            var hp = Ui.Label(card, "0 / 0", new Rect(textX, 12, textWidth, 30), 16, Ui.Muted); hp.name = "Hp";
            Ui.Card(card, "Track", new Rect(textX, 46, textWidth, 14), Ui.Soft, 7);
            Ui.Card(card, "Fill", new Rect(textX, 46, textWidth, 14), Ui.Accent, 7);
            var detail = Ui.Label(card, "", new Rect(textX, 64, textWidth, 24), 14, Ui.Muted); detail.name = "Detail";
            title.alignment = detail.alignment = mirrored ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            hp.alignment = mirrored ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
        }
    }
}
