using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace HongBallFight
{
    public partial class BallFightApp : MonoBehaviour
    {
        const string DraftKey = "HongBallFight.CreatorDraft.v2";
        static readonly string[] Tabs = { "메인", "대전", "제작", "계정", "관리자" };
        static string nextTab = "메인";
        static FighterData matchLeft, matchRight, pendingReview;

        UiRegistry ui;
        RectTransform root;
        FighterRepository repository;
        Text message;
        string tab = "메인";
        string leftId, rightId;
        FighterData editing = new FighterData();
        FighterData reviewing;
        bool reviewMode, reviewSubmitted;
        readonly Dictionary<string, InputField> fields = new Dictionary<string, InputField>();
        string selectedSkill = "burst";
        BallArena arena;
        CreatorSandbox sandbox;
        readonly List<SpriteBank> selectionBanks = new List<SpriteBank>();
        static string SceneName => SceneManager.GetActiveScene().name;
        bool IsCreator => SceneName == "CharacterCreator";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!FindFirstObjectByType<BallFightApp>()) new GameObject("Hong Ball Fight").AddComponent<BallFightApp>();
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            ui = FindFirstObjectByType<UiRegistry>();
            if (!ui) ui = SceneLayouts.Build(SceneName, SceneLayouts.EnsureCamera());
            SceneLayouts.EnsureEventSystem();
            root = ui.Root;
            repository = FindFirstObjectByType<FighterRepository>();
            if (!repository) repository = new GameObject("Fighter Repository").AddComponent<FighterRepository>();
            repository.Changed += Repaint;
            try { editing = JsonUtility.FromJson<FighterData>(PlayerPrefs.GetString(DraftKey, JsonUtility.ToJson(editing))) ?? new FighterData(); } catch { editing = new FighterData(); }

            if (SceneName == "Arena")
            {
                arena = gameObject.AddComponent<BallArena>();
                arena.Begin(ui, matchLeft ?? FighterData.Samples()[0], matchRight ?? FighterData.Samples()[1], () => Navigate("대전"));
                repository.Refresh(); return;
            }
            if (IsCreator)
            {
                tab = "제작";
                if (pendingReview != null) { reviewing = pendingReview; reviewMode = true; }
                sandbox = gameObject.AddComponent<CreatorSandbox>();
                sandbox.Setup(ui, reviewMode ? reviewing : editing);
            }
            else tab = nextTab == "제작" || nextTab == "코드" ? "메인" : nextTab;
            if (ui.Has("footer.message")) message = ui.Text("footer.message");
            Bind();
            Repaint();
            repository.Refresh();
        }

        void Update()
        {
            UpdateStudio();
            if (message && repository) message.text = repository.Busy ? "서버와 통신 중…" : repository.Message;
            if (!arena && !IsCreator && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame
                && EventSystem.current && EventSystem.current.currentSelectedGameObject == null)
                Navigate("제작");
        }
        void OnDestroy() { if (repository) repository.Changed -= Repaint; imageBank?.Dispose(); foreach (var bank in selectionBanks) bank.Dispose(); BlockDrag.Cancel(); }
        void SaveDraft() { PlayerPrefs.SetString(DraftKey, JsonUtility.ToJson(editing)); PlayerPrefs.Save(); }

        void Bind()
        {
            foreach (string t in Tabs) { string next = t; if (ui.Has("nav." + t)) ui.OnClick("nav." + t, () => Navigate(next)); }
            if (ui.Has("nav.refresh")) ui.OnClick("nav.refresh", repository.Refresh);
            if (ui.Has("dialog.close")) ui.OnClick("dialog.close", ClosePopup);
            if (ui.Has("page.메인"))
            {
                ui.OnClick("home.account", () => Navigate("계정"));
                ui.OnClick("home.admin", () => Navigate("관리자"));
                ui.OnClick("home.start", () => Navigate("대전"));
                ui.OnClick("home.create", () => Navigate("제작"));
            }
            if (ui.Has("page.대전")) ui.OnClick("selection.start", BeginBattle);
            if (ui.Has("page.계정"))
            {
                ui.OnClick("account.login", () => repository.Authenticate(ui.Input("account.email").text, ui.Input("account.password").text, false));
                ui.OnClick("account.signup", () => repository.Authenticate(ui.Input("account.email").text, ui.Input("account.password").text, true));
                ui.OnClick("account.logout", repository.Logout);
            }
            if (ui.Has("page.제작")) BindForm();
            if (ui.Has("page.코드")) BindStudio();
        }

        void Repaint()
        {
            if (arena || !ui) return;
            if (reviewMode && reviewSubmitted && !repository.Busy && !repository.Requests.Exists(f => f.id == reviewing.id && f.status == "pending"))
            { reviewSubmitted = false; Navigate("관리자"); return; }
            foreach (string t in new[] { "메인", "대전", "제작", "계정", "관리자", "코드" }) if (ui.Has("page." + t)) ui.Show("page." + t, t == tab);
            if (ui.Has("topbar")) ui.Show("topbar", tab != "메인");
            if (ui.Has("footer.message")) ui.Show("footer.message", tab != "메인");
            foreach (string t in Tabs) if (ui.Has("nav." + t)) Ui.SetButtonStyle(ui.Button("nav." + t), tab == t || tab == "코드" && t == "제작");
            foreach (var bank in selectionBanks) bank.Dispose(); selectionBanks.Clear();
            if (tab == "메인") Home();
            if (tab == "대전") Selection();
            if (tab == "제작") Form();
            if (tab == "코드") Studio();
            if (tab == "계정") Account();
            if (tab == "관리자") Admin();
        }

        void Home()
        {
            var fighters = repository.Fighters.Count >= 2 ? repository.Fighters : FighterData.Samples();
            Ui.SetPortrait(ui.Rect("home.ball1"), fighters[0], "1");
            Ui.SetPortrait(ui.Rect("home.ball2"), fighters[1], "2");
        }

        void Selection()
        {
            var list = ui.Rect("selection.list"); var template = ui.Rect("selection.card");
            Ui.Clear(list, template); template.gameObject.SetActive(false);
            bool empty = repository.Fighters.Count == 0;
            ui.Show("selection.empty", empty);
            if (!empty)
            {
                if (!repository.Fighters.Exists(f => f.id == leftId)) leftId = repository.Fighters[0].id;
                if (!repository.Fighters.Exists(f => f.id == rightId)) rightId = repository.Fighters[Math.Min(1, repository.Fighters.Count - 1)].id;
            }
            const float gap = 16;
            var size = template.sizeDelta; var origin = template.anchoredPosition;
            int columns = Mathf.Max(1, Mathf.FloorToInt((list.rect.width + gap) / (size.x + gap)));
            for (int i = 0; i < repository.Fighters.Count; i++)
            {
                var fighter = repository.Fighters[i];
                var card = Ui.Clone(template, origin + new Vector2(i % columns * (size.x + gap), -(i / columns) * (size.y + gap)));
                card.name = fighter.name;
                var outline = Ui.Child<RectTransform>(card, "Outline");
                if (outline) outline.gameObject.SetActive(fighter.id == leftId || fighter.id == rightId);
                SetAvatar(Ui.Child<RectTransform>(card, "Avatar"), fighter, "");
                SetText(card, "Name", fighter.name);
                SetText(card, "Skill", FighterData.SkillName(fighter.skill) + "  ·  " + fighter.Summary);
                SetText(card, "Stats", $"체력 {fighter.hp}    공격 {fighter.attack}    속도 {fighter.speed:0}");
                PickButton(Ui.Child<Button>(card, "Pick1"), fighter, true);
                PickButton(Ui.Child<Button>(card, "Pick2"), fighter, false);
            }
            Ui.SetContentHeight(list, Mathf.Ceil(repository.Fighters.Count / (float)columns) * (size.y + gap) - origin.y);
            MatchSlot(ui.Rect("selection.slot1"), repository.Fighters.Find(f => f.id == leftId), "1");
            MatchSlot(ui.Rect("selection.slot2"), repository.Fighters.Find(f => f.id == rightId), "2");
        }
        void PickButton(Button button, FighterData fighter, bool left)
        {
            if (!button) return;
            bool chosen = (left ? leftId : rightId) == fighter.id;
            Ui.SetText(button, (left ? "1번" : "2번") + (chosen ? " ✓" : "으로"));
            Ui.SetButtonStyle(button, chosen);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { if (left) leftId = fighter.id; else rightId = fighter.id; Repaint(); });
        }
        void MatchSlot(RectTransform slot, FighterData fighter, string number)
        {
            if (!slot) return;
            SetAvatar(Ui.Child<RectTransform>(slot, "Avatar"), fighter, number);
            SetText(slot, "Name", fighter?.name ?? "-");
            SetText(slot, "Stats", fighter != null ? $"체력 {fighter.hp} · 공격 {fighter.attack}" : "");
        }
        void SetAvatar(RectTransform avatar, FighterData fighter, string number)
        {
            if (!avatar) return;
            Ui.SetPortrait(avatar, fighter, number);
            if (fighter == null || FighterPortraits.Face(fighter.name) || string.IsNullOrEmpty(fighter.portrait)) return;
            var bank = new SpriteBank(fighter); selectionBanks.Add(bank);
            var face = Ui.Child<Image>(avatar, "Face");
            if (face) { face.sprite = bank.Get(fighter.portrait); face.preserveAspect = true; face.gameObject.SetActive(true); }
        }
        static void SetText(Transform parent, string child, string value) { var text = Ui.Child<Text>(parent, child); if (text) text.text = value; }

        void BeginBattle()
        {
            var left = repository.Fighters.Find(f => f.id == leftId); var right = repository.Fighters.Find(f => f.id == rightId);
            if (left == null || right == null) return;
            matchLeft = left.Copy(); matchRight = right.Copy(); SceneManager.LoadScene("Arena");
        }

        static readonly string[] Skills = { "burst", "heal", "shield", "script", "army_aura", "pasta_whip" };
        FighterData Draft => reviewMode ? reviewing : editing;
        void SetDraft(FighterData value) { if (reviewMode) reviewing = value; else { editing = value; SaveDraft(); } }

        void BindForm()
        {
            foreach (string key in new[] { "name", "color", "hp", "attack", "speed", "skill_power", "cooldown" })
            {
                var field = ui.Input("form." + key); fields[key] = field;
                field.onEndEdit.AddListener(_ =>
                {
                    if (!ReadForm(Draft, out var live, false, false)) return;
                    SetDraft(live); if (sandbox) sandbox.Load(live);
                });
            }
            ui.OnClick("form.new", () =>
            {
                editing = new FighterData(); studioDocument = null; SaveDraft();
                if (sandbox) sandbox.Load(editing); Repaint();
            });
            ui.OnClick("form.load", () => Choose("기본 캐릭터", FighterData.Samples().ConvertAll(f => f.name).ToArray(), i =>
            {
                editing = FighterData.Samples()[i]; editing.id = null; studioDocument = null; SaveDraft();
                if (sandbox) sandbox.Load(editing); Repaint();
            }));
            ui.OnClick("form.skill", () => Choose("스킬 선택", Array.ConvertAll(Skills, FighterData.SkillName), i =>
            {
                if (!ReadForm(Draft, out var result, true, false)) return;
                result.skill = Skills[i]; SetDraft(result);
                if (sandbox) sandbox.Load(result);
                Repaint();
            }));
            ui.OnClick("form.blocks", () =>
            {
                if (!ReadForm(Draft, out var result, true, false)) return;
                if (reviewMode) result.review_note = ui.Input("form.note").text;
                SetDraft(result);
                studioAdmin = reviewMode; studioDocument = null; tab = "코드"; Repaint();
                if (sandbox) sandbox.Load(result);
            });
            ui.OnClick("form.submit", () => { if (ReadForm(editing, out var result)) { editing = result; repository.Submit(result); } });
            ui.OnClick("form.reject", () => { reviewSubmitted = true; repository.ReviewRequest(reviewing, reviewing, false, ui.Input("form.note").text); });
            ui.OnClick("form.approve", () =>
            {
                if (ReadForm(reviewing, out var result)) { reviewSubmitted = true; repository.ReviewRequest(reviewing, result, true, ui.Input("form.note").text); }
            });
        }
        void Form()
        {
            var draft = Draft;
            if (draft == null) return;
            selectedSkill = draft.skill;
            ui.Text("form.title").text = reviewMode ? "등록 검토 / 밸런스 조정" : "나만의 파이터 제작";
            ui.Text("form.subtitle").text = reviewMode ? "수치를 조정해 승인하거나, 사유와 함께 반려하세요." : "요청을 보내면 관리자가 검토합니다. 승인 전에는 대전에 등장하지 않습니다.";
            ui.Show("form.new", !reviewMode); ui.Show("form.load", !reviewMode); ui.Show("form.submit", !reviewMode); ui.Show("form.adminGroup", reviewMode);
            fields["name"].SetTextWithoutNotify(draft.name);
            fields["color"].SetTextWithoutNotify(draft.color);
            fields["hp"].SetTextWithoutNotify(draft.hp.ToString());
            fields["attack"].SetTextWithoutNotify(draft.attack.ToString());
            fields["speed"].SetTextWithoutNotify(draft.speed.ToString(CultureInfo.InvariantCulture));
            fields["skill_power"].SetTextWithoutNotify(draft.skill_power.ToString());
            fields["cooldown"].SetTextWithoutNotify(draft.cooldown.ToString(CultureInfo.InvariantCulture));
            if (reviewMode) ui.Input("form.note").SetTextWithoutNotify(draft.review_note ?? "");
            Ui.SetText(ui.Button("form.skill"), FighterData.SkillName(selectedSkill) + "  ▾");
            ui.Text("form.info").text = draft.skill == "army_aura" ? "장희철 · 패시브\n주변 적에게 슬로우와 지속 피해\n다음 계급의 계급장이 제자리에 생성\n먹으면 진급 · 3초 뒤 다음 계급장"
                : draft.skill == "pasta_whip" ? "구본윤 · 파스타 회초리\n2초마다 긴 면을 휘둘러 공격\n강한 넉백 · 맞은 방향으로 계속 날아감\n밀려난 적이 벽에 닿으면 추가 피해"
                : "기본 스킬을 고르거나\n블록을 연결해 직접 만들 수 있습니다.";
        }
        void CaptureDraft()
        {
            if (tab == "코드") SaveStudio();
            if (tab == "제작" && !reviewMode && fields.ContainsKey("hp") && ReadForm(editing, out var result, false, false)) editing = result;
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
            bool open = repository.Online && repository.IsAdmin;
            ui.Show("admin.locked", !open); ui.Show("admin.listGroup", open);
            if (!open) { ui.Text("admin.notice").text = repository.Online ? "계정 탭에서 로그인하세요." : "현재 로컬 초안입니다. DB 연결 후 관리자 검토가 활성화됩니다."; return; }
            var list = ui.Rect("admin.list"); var row = ui.Rect("admin.row"); var heading = ui.Rect("admin.heading");
            Ui.Clear(list, row, heading); row.gameObject.SetActive(false); heading.gameObject.SetActive(false);
            float y = 0, step = row.sizeDelta.y + 10;
            foreach (var request in repository.Requests.FindAll(f => f.status == "pending"))
            {
                AdminRow(row, ref y, step, $"검토 대기   ·   {request.name}   ·   HP {request.hp}   ATK {request.attack}", "검토", true,
                    () => { pendingReview = request.Copy(); Navigate("제작"); });
            }
            Ui.Clone(heading, new Vector2(heading.anchoredPosition.x, -y)); y += heading.sizeDelta.y + 10;
            foreach (var fighter in repository.Fighters)
            {
                AdminRow(row, ref y, step, $"{fighter.name}   ·   v{fighter.revision}   ·   HP {fighter.hp}   ATK {fighter.attack}", "밸런스 수정", false, () =>
                {
                    editing = fighter.Copy(); editing.target_id = fighter.id; editing.base_revision = fighter.revision; SaveDraft(); Navigate("제작");
                });
            }
            Ui.SetContentHeight(list, y);
        }
        static void AdminRow(RectTransform template, ref float y, float step, string label, string action, bool primary, Action click)
        {
            var row = Ui.Clone(template, new Vector2(template.anchoredPosition.x, -y)); y += step;
            SetText(row, "Label", label);
            var button = Ui.Child<Button>(row, "Action");
            Ui.SetText(button, action); Ui.SetButtonStyle(button, primary); button.onClick.AddListener(() => click());
        }

        void Account()
        {
            ui.Text("account.title").text = repository.LoggedIn ? (repository.IsAdmin ? "관리자 계정" : "제작자 계정") : "제작자 로그인";
            ui.Show("account.loginGroup", !repository.LoggedIn); ui.Show("account.logout", repository.LoggedIn);
            var list = ui.Rect("account.list"); var row = ui.Rect("account.row");
            Ui.Clear(list, row); row.gameObject.SetActive(false);
            ui.Show("account.empty", repository.Requests.Count == 0);
            float step = row.sizeDelta.y + 12;
            for (int i = 0; i < repository.Requests.Count; i++)
            {
                var request = repository.Requests[i];
                var status = request.status == "approved" ? "승인" : request.status == "rejected" ? "반려" : "검토 대기";
                var item = Ui.Clone(row, new Vector2(row.anchoredPosition.x, -i * step));
                var name = Ui.Child<Text>(item, "Name"); name.text = request.name + "  ·  " + status;
                if (request.status == "rejected") name.color = Ui.Danger;
                SetText(item, "Note", request.review_note ?? "");
            }
            Ui.SetContentHeight(list, repository.Requests.Count * step);
        }
    }
}
