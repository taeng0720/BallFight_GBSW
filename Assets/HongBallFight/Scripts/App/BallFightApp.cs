using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace HongBallFight
{
    public partial class BallFightApp : MonoBehaviour
    {
        public static readonly Color Ink = Color.white;
        public static readonly Color Panel = new Color32(250, 250, 250, 255);
        public static readonly Color Muted = new Color32(125, 125, 125, 255);
        public static readonly Color Accent = new Color32(38, 38, 38, 255);
        public static readonly Color TextColor = new Color32(35, 35, 35, 255);
        public static readonly Color Border = new Color32(226, 226, 226, 255);
        FighterRepository repository;
        RectTransform root, body;
        Font font;
        Text message;
        string tab = "대전";
        string leftId, rightId;
        FighterData editing = new FighterData();
        FighterData reviewing;
        readonly Dictionary<string, InputField> fields = new Dictionary<string, InputField>();
        string selectedSkill = "burst";
        BallArena arena;
        CreatorSandbox sandbox;
        RectTransform sandboxRoot;
        static string nextTab = "메인";
        static FighterData matchLeft, matchRight;
        readonly List<SpriteBank> selectionBanks = new List<SpriteBank>();
        bool IsCreator => BakingLayout ? layoutSceneName == "CharacterCreator" : SceneManager.GetActiveScene().name == "CharacterCreator";
        static Sprite circle;
        public static Sprite Circle
        {
            get
            {
                if (circle) return circle;
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                var pixels = new Color[128 * 128];
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                    pixels[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01(63 - Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f))));
                texture.SetPixels(pixels); texture.Apply();
                circle = Sprite.Create(texture, new Rect(0, 0, 128, 128), Vector2.one * .5f);
                return circle;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!FindFirstObjectByType<BallFightApp>()) new GameObject("Hong Ball Fight").AddComponent<BallFightApp>();
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            InitializeCanvas();
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            repository = FindFirstObjectByType<FighterRepository>();
            if (!repository) repository = new GameObject("Fighter Repository").AddComponent<FighterRepository>();
            repository.Changed += Repaint;
            try { editing = JsonUtility.FromJson<FighterData>(PlayerPrefs.GetString("HongBallFight.CreatorDraft.v2", JsonUtility.ToJson(editing))) ?? new FighterData(); } catch { editing = new FighterData(); }
            tab = IsCreator ? "제작" : nextTab;
            if (SceneManager.GetActiveScene().name == "Arena")
            {
                var arenaRoot = SceneUi.Arena ? SceneUi.Arena : root;
                SceneUiBindings.ResetScope(arenaRoot);
                arena = arenaRoot.GetComponent<BallArena>() ?? arenaRoot.gameObject.AddComponent<BallArena>();
                arena.Begin(this, arenaRoot, matchLeft ?? FighterData.Samples()[0], matchRight ?? FighterData.Samples()[1], () => Navigate("대전"));
            }
            else Repaint();
            repository.Refresh();
        }

        void Update()
        {
            UpdateStudio();
            if (message && repository) message.text = repository.Busy ? "서버와 통신 중…" : repository.Message;
            if (!arena && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame && EventSystem.current.currentSelectedGameObject == null)
            { Navigate("제작"); }
        }
        void OnDestroy() { if (repository) repository.Changed -= Repaint; imageBank?.Dispose(); foreach (var bank in selectionBanks) bank.Dispose(); BlockDrag.Cancel(); }

        public RectTransform Box(Transform parent, string name, Rect rect, Color color)
        {
            string key = SceneUiBindings.Key("Box", name, rect);
            var existing = TransientUi || BakingLayout ? null : SceneUiBindings.Find(parent, key);
            if (existing) return existing;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            MarkSlot(rt, key); return rt;
        }
        public Text Label(Transform parent, string value, Rect rect, int size = 18, Color? color = null)
        {
            string key = SceneUiBindings.Key("Text", "", rect);
            var existing = TransientUi || BakingLayout ? null : SceneUiBindings.Find(parent, key);
            if (existing)
            {
                var label = existing.GetComponent<Text>();
                if (existing.GetComponent<SceneUiSlot>().RuntimeText) label.text = value;
                return label;
            }
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y); rt.sizeDelta = rect.size;
            var text = rt.gameObject.AddComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = color ?? TextColor; text.supportRichText = false; text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft; MarkSlot(rt, key, DataUi || string.IsNullOrEmpty(value) || value.Contains("▾")); return text;
        }
        public Button Button(Transform parent, string value, Rect rect, Action action, bool primary = false)
        {
            var rt = Box(parent, "Button", rect, primary ? Accent : new Color32(237, 237, 237, 255));
            if (BakingLayout || !rt.GetComponent<SceneUiSlot>()) rt.name = value;
            rt.GetComponent<Image>().raycastTarget = true;
            var button = rt.GetComponent<Button>();
            if (!button)
            {
                button = rt.gameObject.AddComponent<Button>(); button.targetGraphic = rt.GetComponent<Image>();
                var colors = button.colors; colors.highlightedColor = new Color(.92f, .92f, .92f); colors.pressedColor = new Color(.8f, .8f, .8f); button.colors = colors;
            }
            var text = Label(rt, value, new Rect(8, 0, rect.width - 16, rect.height), 17, primary ? Color.white : TextColor); text.alignment = TextAnchor.MiddleCenter;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => action()); return button;
        }
        InputField Input(Transform parent, string title, string value, Rect rect, bool password = false)
        {
            Label(parent, title, new Rect(rect.x, rect.y, rect.width, 24), 15, Muted);
            var rt = Box(parent, title, new Rect(rect.x, rect.y + 28, rect.width, 40), new Color32(239, 239, 239, 255));
            rt.GetComponent<Image>().raycastTarget = true;
            var input = rt.GetComponent<InputField>() ?? rt.gameObject.AddComponent<InputField>(); input.targetGraphic = rt.GetComponent<Image>();
            input.onEndEdit.RemoveAllListeners(); input.onValueChanged.RemoveAllListeners();
            var text = Label(rt, "", new Rect(12, 0, rect.width - 24, 40), 18); input.textComponent = text;
            input.characterLimit = password ? 128 : 80;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard; input.SetTextWithoutNotify(value);
            return input;
        }
        RectTransform Scroll(Transform parent, Rect rect, float height)
        {
            var viewport = Box(parent, "Scroll", rect, Color.clear); viewport.GetComponent<Image>().raycastTarget = true;
            if (!viewport.GetComponent<RectMask2D>()) viewport.gameObject.AddComponent<RectMask2D>();
            var content = Box(viewport, "Content", new Rect(0, 0, rect.width - 12, height), Color.clear);
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(viewport.rect.height, height));
            var scroll = viewport.GetComponent<ScrollRect>() ?? viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            return content;
        }

        void Repaint()
        {
            if (arena) return;
            foreach (var bank in selectionBanks) bank.Dispose(); selectionBanks.Clear();
            bool creatorLayout = IsCreator || tab == "코드";
            float height = creatorLayout ? 1080 : 800;
            screen = OpenPage();
            if (sandboxRoot) sandboxRoot.gameObject.SetActive(creatorLayout);
            if (tab != "메인") Label(screen, "볼 파이트", new Rect(32, 24, 500, 40), 25);
            var tabs = new[] { "메인", "대전", "제작", "계정", "관리자" };
            for (int i = 0; tab != "메인" && i < tabs.Length; i++)
            {
                string next = tabs[i]; Button(screen, next == "대전" ? "시작" : next == "제작" ? "캐릭터 제작" : next, new Rect(32 + i * 154, 101, 142, 42), () => Navigate(next), tab == next);
            }
            if (tab != "메인") Button(screen, "새로고침", new Rect(1110, 101, 138, 42), repository.Refresh);
            else
            {
                Button(screen, "계정", new Rect(1020, 30, 100, 34), () => Navigate("계정"));
                Button(screen, "관리", new Rect(1132, 30, 100, 34), () => Navigate("관리자"));
            }
            if (creatorLayout && !sandboxRoot)
            {
                sandboxRoot = SceneUi.CreatorSandbox ? SceneUi.CreatorSandbox : Box(root, "Creator live sandbox", new Rect(32, 160, 1216, 270), Panel);
                SceneUi.CreatorSandbox = sandboxRoot;
                if (!BakingLayout) SceneUiBindings.ResetScope(sandboxRoot);
                sandboxRoot.gameObject.SetActive(true);
                sandbox = sandboxRoot.GetComponent<CreatorSandbox>() ?? sandboxRoot.gameObject.AddComponent<CreatorSandbox>(); sandbox.Setup(this, sandboxRoot, tab == "코드" ? StudioFighter : editing);
            }
            body = Box(screen, "Page", new Rect(32, creatorLayout ? 450 : 160, 1216, 562), tab == "메인" ? Ink : Panel);
            fields.Clear();
            blockStatus = null;
            if (tab == "메인") MainMenu();
            if (tab == "대전") Selection();
            if (tab == "제작") Editor(false);
            if (tab == "관리자") Admin();
            if (tab == "계정") Account();
            if (tab == "코드") Studio();
            message = tab == "메인" || tab == "대전" ? null : Label(screen, repository.Message, new Rect(32, creatorLayout ? 1020 : 735, 1216, 48), 16, Muted);
        }

        void Selection()
        {
            Label(body, "캐릭터 선택", new Rect(24, 14, 800, 45), 27);
            Label(body, "1번과 2번을 선택하세요.", new Rect(24, 62, 1150, 28), 17, Muted);
            if (repository.Fighters.Count == 0) { Label(body, "공개된 캐릭터가 없습니다. 제작 탭에서 첫 등록을 요청하세요.", new Rect(24, 120, 1100, 40)); return; }
            if (!repository.Fighters.Exists(f => f.id == leftId)) leftId = repository.Fighters[0].id;
            if (!repository.Fighters.Exists(f => f.id == rightId)) rightId = repository.Fighters[Math.Min(1, repository.Fighters.Count - 1)].id;
            var content = Scroll(body, new Rect(24, 104, 1168, 330), Mathf.Ceil(repository.Fighters.Count / 2f) * 270);
            DataUi = true;
            for (int i = 0; i < repository.Fighters.Count; i++)
            {
                var fighter = repository.Fighters[i];
                var card = Box(content, fighter.name, new Rect((i % 2) * 580, (i / 2) * 270, 564, 252), Ink);
                ColorUtility.TryParseHtmlString(fighter.color, out var color);
                var bank = new SpriteBank(fighter); selectionBanks.Add(bank);
                var avatar = Box(card, "Avatar", new Rect(18, 20, 56, 56), string.IsNullOrEmpty(fighter.portrait) ? color : Color.white); avatar.GetComponent<Image>().sprite = bank.Get(fighter.portrait); avatar.GetComponent<Image>().preserveAspect = true;
                Label(card, fighter.name, new Rect(90, 15, 265, 36), 24);
                Label(card, FighterData.SkillName(fighter.skill), new Rect(90, 52, 440, 26), 15, Muted);
                Label(card, fighter.Summary + $"\n체력 {fighter.hp}  ·  공격력 {fighter.attack}", new Rect(18, 92, 528, 70), 17, Muted);
                Button(card, leftId == fighter.id ? "1번 ✓" : "1번", new Rect(18, 187, 254, 42), () => { leftId = fighter.id; Repaint(); }, leftId == fighter.id);
                Button(card, rightId == fighter.id ? "2번 ✓" : "2번", new Rect(290, 187, 254, 42), () => { rightId = fighter.id; Repaint(); }, rightId == fighter.id);
            }
            DataUi = false;
            Button(body, "시작", new Rect(886, 478, 306, 58), BeginBattle, true);
            Label(body, "두 캐릭터가 자동으로 대결합니다.", new Rect(24, 478, 840, 58), 17, Muted);
        }

        void BeginBattle()
        {
            var left = repository.Fighters.Find(f => f.id == leftId); var right = repository.Fighters.Find(f => f.id == rightId);
            if (left == null || right == null) return;
            matchLeft = left.Copy(); matchRight = right.Copy(); SceneManager.LoadScene("Arena");
        }

        void Editor(bool admin)
        {
            var draft = admin ? reviewing : editing;
            if (draft == null) return;
            selectedSkill = draft.skill;
            Label(body, admin ? "등록 검토 / 밸런스 조정" : "나만의 파이터 제작", new Rect(24, 14, 900, 44), 27);
            if (!admin) Button(body, "새 캐릭터", new Rect(1060, 18, 130, 36), () =>
            {
                editing = new FighterData(); studioDocument = null; PlayerPrefs.SetString("HongBallFight.CreatorDraft.v2", JsonUtility.ToJson(editing)); PlayerPrefs.Save();
                if (sandbox) sandbox.Load(editing); Repaint();
            });
            if (!admin) Button(body, "불러오기", new Rect(912, 18, 130, 36), () => Choose("기본 캐릭터", FighterData.Samples().ConvertAll(f => f.name).ToArray(), i =>
            {
                editing = FighterData.Samples()[i]; editing.id = null; studioDocument = null;
                PlayerPrefs.SetString("HongBallFight.CreatorDraft.v2", JsonUtility.ToJson(editing)); PlayerPrefs.Save();
                if (sandbox) sandbox.Load(editing); Repaint();
            }));
            Label(body, admin ? "수치를 조정해 승인하거나, 사유와 함께 반려하세요." : "요청을 보내면 관리자가 검토합니다. 승인 전에는 대전에 등장하지 않습니다.", new Rect(24, 62, 1150, 28), 17, Muted);
            fields["name"] = Input(body, "캐릭터 이름", draft.name, new Rect(24, 108, 350, 70));
            fields["color"] = Input(body, "공 색상 · HEX", draft.color, new Rect(400, 108, 220, 70));
            fields["hp"] = Input(body, "체력 · 30~1000", draft.hp.ToString(), new Rect(24, 200, 165, 70));
            fields["attack"] = Input(body, "공격력 · 1~100", draft.attack.ToString(), new Rect(209, 200, 165, 70));
            fields["speed"] = Input(body, "속도 · 100~500", draft.speed.ToString(CultureInfo.InvariantCulture), new Rect(400, 200, 220, 70));
            fields["skill_power"] = Input(body, "스킬 위력 · 1~150", draft.skill_power.ToString(), new Rect(24, 292, 350, 70));
            fields["cooldown"] = Input(body, "쿨타임 · 2~20초", draft.cooldown.ToString(CultureInfo.InvariantCulture), new Rect(400, 292, 220, 70));
            Label(body, "스킬 선택", new Rect(24, 385, 600, 25), 16, Muted);
            var skills = new[] { "burst", "heal", "shield", "script", "army_aura", "pasta_whip" };
            Button(body, FighterData.SkillName(selectedSkill) + "  ▾", new Rect(24, 419, 596, 42), () => Choose("동작 선택", Array.ConvertAll(skills, FighterData.SkillName), i =>
                {
                    if (!ReadForm(draft, out var result, true, false)) return;
                    result.skill = skills[i]; if (admin) reviewing = result; else editing = result;
                    if (sandbox) sandbox.Load(result);
                    if (!admin) { PlayerPrefs.SetString("HongBallFight.CreatorDraft.v2", JsonUtility.ToJson(editing)); PlayerPrefs.Save(); }
                    Repaint();
                }));
            Box(body, "Divider", new Rect(653, 110, 1, 414), Border);
            Label(body, "동작", new Rect(688, 108, 480, 34), 24);
            Label(body, draft.skill == "army_aura" ? "장희철 · 패시브\n주변 적에게 슬로우와 지속 피해\n다음 계급의 계급장이 제자리에 생성\n먹으면 진급 · 3초 뒤 다음 계급장" : draft.skill == "pasta_whip" ? "구본윤 · 파스타 회초리\n2초마다 긴 면을 휘둘러 공격\n강한 넉백\n밀려난 적이 벽에 닿으면 추가 피해" : "기본 동작을 고르거나\n블록을 연결해 직접 만들 수 있습니다.", new Rect(688, 153, 480, 180), 18, Muted);
            Button(body, "블록으로 스킬 만들기  →", new Rect(688, 343, 480, 38), () =>
            {
                if (!ReadForm(draft, out var result, true, false)) return;
                if (admin && fields.ContainsKey("note")) result.review_note = fields["note"].text;
                if (admin) reviewing = result; else editing = result;
                studioAdmin = admin; studioDocument = null; tab = "코드"; Repaint();
            }, true);
            foreach (var field in fields.Values) field.onEndEdit.AddListener(_ =>
            {
                if (ReadForm(admin ? reviewing : editing, out var live, false, false))
                {
                    if (admin) reviewing = live; else editing = live;
                    if (sandbox) sandbox.Load(live);
                    if (!admin) { PlayerPrefs.SetString("HongBallFight.CreatorDraft.v2", JsonUtility.ToJson(editing)); PlayerPrefs.Save(); }
                }
            });
            if (admin)
            {
                fields["note"] = Input(body, "검토 사유", draft.review_note ?? "", new Rect(688, 390, 480, 70));
                Button(body, "반려", new Rect(688, 484, 148, 46), () => repository.ReviewRequest(reviewing, reviewing, false, fields["note"].text));
                Button(body, "수정값으로 승인", new Rect(852, 484, 316, 46), () =>
                {
                    if (ReadForm(reviewing, out var result)) repository.ReviewRequest(reviewing, result, true, fields["note"].text);
                }, true);
            }
            else Button(body, "관리자에게 등록 요청  →", new Rect(688, 484, 480, 46), () =>
            {
                if (ReadForm(editing, out var result)) { editing = result; repository.Submit(result); }
            }, true);
        }
        void CaptureDraft()
        {
            if (tab == "코드") SaveStudio();
            if (tab == "제작" && fields.ContainsKey("hp") && ReadForm(editing, out var result, false, false)) editing = result;
        }
        bool ReadForm(FighterData original, out FighterData result, bool notify = true, bool validateCode = true)
        {
            result = original.Copy();
            if (!fields.ContainsKey("hp")) return false;
            result.name = fields["name"].text; result.color = fields["color"].text; result.skill = selectedSkill;
            if (!int.TryParse(fields["hp"].text, out result.hp) || !int.TryParse(fields["attack"].text, out result.attack)
                || !float.TryParse(fields["speed"].text, NumberStyles.Float, CultureInfo.InvariantCulture, out result.speed)
                || !int.TryParse(fields["skill_power"].text, out result.skill_power)
                || !float.TryParse(fields["cooldown"].text, NumberStyles.Float, CultureInfo.InvariantCulture, out result.cooldown))
            { if (notify) repository.SetMessage("수치에는 올바른 숫자를 입력하세요."); return false; }
            string actualSkill = result.skill;
            if (!validateCode) result.skill = "burst";
            var error = result.Validate(); result.skill = actualSkill;
            if (error != null) { if (notify) repository.SetMessage(error); return false; }
            return true;
        }

        void Admin()
        {
            if (!repository.Online || !repository.IsAdmin)
            {
                Label(body, "관리자 전용 공간", new Rect(24, 24, 1100, 48), 28);
                Label(body, "서버에 등록된 관리자 계정으로 로그인하면\n캐릭터 등록 승인·반려 및 기존 캐릭터의 밸런스 수정이 가능합니다.", new Rect(24, 85, 1100, 90), 20, Muted);
                Label(body, repository.Online ? "계정 탭에서 로그인하세요." : "현재 로컬 초안입니다. DB 연결 후 관리자 검토가 활성화됩니다.", new Rect(24, 200, 1100, 44), 18, Accent);
                return;
            }
            if (reviewing != null && repository.Requests.Exists(f => f.id == reviewing.id && f.status == "pending")) { Editor(true); return; }
            reviewing = null;
            Label(body, "등록 요청 / 밸런스 패치", new Rect(24, 14, 1000, 48), 27);
            var pending = repository.Requests.FindAll(f => f.status == "pending");
            var content = Scroll(body, new Rect(24, 80, 1168, 450), Math.Max(450, (pending.Count + repository.Fighters.Count + 1) * 66));
            int y = 0;
            foreach (var request in pending)
            {
                Label(content, $"검토 대기   /   {request.name}   /   HP {request.hp}   ATK {request.attack}", new Rect(0, y, 920, 48), 18);
                Button(content, "검토", new Rect(970, y + 3, 170, 42), () => { reviewing = request.Copy(); Repaint(); }); y += 66;
            }
            Label(content, "공개 캐릭터 · 수정 요청을 만든 후 검토에서 승인하세요.", new Rect(0, y, 1100, 44), 17, Accent); y += 66;
            foreach (var fighter in repository.Fighters)
            {
                Label(content, $"{fighter.name}   /   v{fighter.revision}   /   HP {fighter.hp}   ATK {fighter.attack}", new Rect(0, y, 920, 48), 18);
                Button(content, "밸런스 수정", new Rect(970, y + 3, 170, 42), () =>
                {
                    editing = fighter.Copy(); editing.target_id = fighter.id; editing.base_revision = fighter.revision; tab = "제작"; Repaint();
                }); y += 66;
            }
        }

        void Account()
        {
            Label(body, repository.LoggedIn ? (repository.IsAdmin ? "관리자 계정" : "제작자 계정") : "제작자 로그인", new Rect(24, 14, 700, 48), 27);
            if (!repository.LoggedIn)
            {
                var email = Input(body, "이메일", "", new Rect(24, 90, 440, 70));
                var password = Input(body, "비밀번호 · 8자 이상", "", new Rect(24, 185, 440, 70), true);
                Button(body, "로그인", new Rect(24, 290, 210, 46), () => repository.Authenticate(email.text, password.text, false), true);
                Button(body, "회원가입", new Rect(254, 290, 210, 46), () => repository.Authenticate(email.text, password.text, true));
            }
            else Button(body, "로그아웃", new Rect(24, 90, 230, 46), repository.Logout);
            Label(body, "내 등록 요청", new Rect(516, 20, 650, 40), 23, Accent);
            var content = Scroll(body, new Rect(516, 78, 676, 452), Math.Max(452, repository.Requests.Count * 88));
            for (int i = 0; i < repository.Requests.Count; i++)
            {
                var request = repository.Requests[i];
                var status = request.status == "approved" ? "승인" : request.status == "rejected" ? "반려" : "검토 대기";
                Label(content, request.name + "  ·  " + status, new Rect(0, i * 88, 650, 34), 19);
                Label(content, request.review_note ?? "", new Rect(0, i * 88 + 34, 650, 40), 15, Muted);
            }
        }
    }
}
