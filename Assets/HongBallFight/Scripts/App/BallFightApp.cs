using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace HongBallFight
{
    public partial class BallFightApp : MonoBehaviour
    {
        static string nextTab = "대전";
        static FighterData matchLeft, matchRight;
        static string leftId, rightId;

        UiRegistry ui;
        FighterRepository repository;
        Text message;
        string tab = "대전";
        float resetArmedUntil = -1;
        BallArena arena;
        readonly List<SpriteBank> selectionBanks = new List<SpriteBank>();
        static string SceneName => SceneManager.GetActiveScene().name;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!FindFirstObjectByType<BallFightApp>()) new GameObject("Hong Ball Fight").AddComponent<BallFightApp>();
        }

        void Start()
        {
            Application.targetFrameRate = 60;
            ui = PickLayout(SceneLayouts.IsPortrait);
            SceneLayouts.EnsureEventSystem();
            repository = FindFirstObjectByType<FighterRepository>();
            if (!repository) repository = new GameObject("Fighter Repository").AddComponent<FighterRepository>();
            repository.Changed += Repaint;

            if (SceneName == "Arena")
            {
                arena = gameObject.AddComponent<BallArena>();
                arena.Begin(ui, matchLeft ?? FighterData.Samples()[0], matchRight ?? FighterData.Samples()[1], () => Navigate("대전"));
                return;
            }
            tab = Array.IndexOf(SceneLayouts.Tabs, nextTab) >= 0 ? nextTab : "대전";
            if (ui.Has("footer.message")) message = ui.Text("footer.message");
            Bind();
            Repaint();
            repository.Refresh();
        }

        void Update()
        {
            if (message && repository) message.text = repository.Busy ? "서버와 통신 중…" : repository.Message;
            if (resetArmedUntil > 0 && Time.unscaledTime > resetArmedUntil && ui && ui.Has("stats.reset"))
            {
                resetArmedUntil = -1;
                var reset = ui.Button("stats.reset"); Ui.SetText(reset, "기록 초기화"); Ui.SetButtonStyle(reset, false);
            }
            if (arena || !ui || reloading) return;
            bool typing = EventSystem.current && EventSystem.current.currentSelectedGameObject && EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
            if (ui.Portrait == SceneLayouts.IsPortrait || typing) { orientationChangedAt = -1; return; }
            if (orientationChangedAt < 0) orientationChangedAt = Time.unscaledTime;
            else if (Time.unscaledTime - orientationChangedAt > .4f) { reloading = true; nextTab = tab; SceneManager.LoadScene(SceneName); }
        }
        float orientationChangedAt = -1;
        bool reloading;

        static UiRegistry PickLayout(bool portrait)
        {
            UiRegistry chosen = null;
            foreach (var layout in FindObjectsByType<UiRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool usable = layout.LayoutVersion >= SceneLayouts.Version && layout.Portrait == portrait && !chosen;
                if (usable) { chosen = layout; continue; }
                layout.gameObject.SetActive(false);
                if (layout.LayoutVersion < SceneLayouts.Version) Destroy(layout.gameObject);
            }
            if (!chosen) chosen = SceneLayouts.Build(SceneName, SceneLayouts.EnsureCamera(), portrait);
            chosen.gameObject.SetActive(true);
            return chosen;
        }
        void OnDestroy() { if (repository) repository.Changed -= Repaint; foreach (var bank in selectionBanks) bank.Dispose(); }

        void Navigate(string destination)
        {
            if (SceneName != "MainMenu") { nextTab = destination; SceneManager.LoadScene("MainMenu"); return; }
            tab = destination; Repaint();
            if (destination == "아이디어" && repository.Online) repository.Refresh();
        }

        void Bind()
        {
            foreach (string t in SceneLayouts.Tabs) { string next = t; ui.OnClick("nav." + t, () => Navigate(next)); }
            ui.OnClick("selection.start", BeginBattle);
            ui.OnClick("stats.reset", ResetStats);
            ui.OnClick("idea.submit", SubmitIdea);
        }

        void Repaint()
        {
            if (arena || !ui) return;
            foreach (string t in SceneLayouts.Tabs) ui.Show("page." + t, t == tab);
            foreach (string t in SceneLayouts.Tabs) Ui.SetButtonStyle(ui.Button("nav." + t), tab == t);
            foreach (var bank in selectionBanks) bank.Dispose(); selectionBanks.Clear();
            if (tab == "대전") Selection();
            if (tab == "승률") Stats();
            if (tab == "아이디어") Ideas();
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
                SetText(card, "Skill", fighter.Tagline);
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

        void Stats()
        {
            var matches = GameRecords.Matches;
            bool empty = matches.Count == 0;
            ui.Show("stats.empty", empty); ui.Show("stats.content", !empty);
            ui.Text("stats.summary").text = empty ? "대전 결과가 자동으로 기록됩니다." : $"총 {matches.Count}경기 기록 · 같은 캐릭터끼리의 경기는 승률에서 제외됩니다.";

            var standings = GameRecords.Standings();
            FillList(ui.Rect("stats.standings"), ui.Rect("stats.standingRow"), standings.Count, 10, (row, i) =>
            {
                var standing = standings[i];
                var fighter = repository.Fighters.Find(f => f.id == standing.Key || f.name == standing.Name) ?? new FighterData { name = standing.Name, color = standing.Color };
                SetText(row, "Rank", (i + 1).ToString());
                SetAvatar(Ui.Child<RectTransform>(row, "Avatar"), fighter, "");
                SetText(row, "Name", standing.Name);
                SetText(row, "Record", $"{standing.Games}전 {standing.Wins}승 {standing.Losses}패" + (standing.Draws > 0 ? $" {standing.Draws}무" : ""));
                SetText(row, "Rate", Mathf.RoundToInt(standing.Rate * 100) + "%");
                var track = Ui.Child<RectTransform>(row, "Track"); var fill = Ui.Child<RectTransform>(row, "Fill");
                if (track && fill)
                {
                    fill.GetComponent<Image>().color = Ui.ParseColor(fighter.color);
                    float width = track.sizeDelta.x * standing.Rate;
                    fill.gameObject.SetActive(width > 1);
                    fill.anchoredPosition = track.anchoredPosition;
                    fill.sizeDelta = new Vector2(Mathf.Max(width, track.sizeDelta.y), track.sizeDelta.y);
                }
            });

            var rivalries = GameRecords.Rivalries();
            FillList(ui.Rect("stats.rivalries"), ui.Rect("stats.rivalryRow"), rivalries.Count, 8, (row, i) =>
            {
                var rivalry = rivalries[i];
                SetText(row, "A", rivalry.AName); SetText(row, "B", rivalry.BName);
                SetText(row, "Score", $"{rivalry.AWins} : {rivalry.BWins}");
                SetText(row, "Draws", rivalry.Draws > 0 ? $"무 {rivalry.Draws}" : "");
            });

            int recent = Math.Min(matches.Count, 30);
            FillList(ui.Rect("stats.recent"), ui.Rect("stats.matchRow"), recent, 6, (row, i) =>
            {
                var match = matches[i];
                SetText(row, "Players", $"{match.leftName}  vs  {match.rightName}");
                SetText(row, "Result", match.winner == 1 ? match.leftName + " 승" : match.winner == 2 ? match.rightName + " 승" : "무승부");
                SetText(row, "Time", $"{(int)match.seconds / 60:00}:{(int)match.seconds % 60:00} · {GameRecords.FormatTime(match.time)}");
            });
        }

        void ResetStats()
        {
            var button = ui.Button("stats.reset");
            if (Time.unscaledTime > resetArmedUntil)
            {
                resetArmedUntil = Time.unscaledTime + 3;
                Ui.SetText(button, "한 번 더 누르기"); Ui.SetButtonStyle(button, true);
                repository.SetMessage("기록을 지우려면 3초 안에 한 번 더 누르세요.");
                return;
            }
            resetArmedUntil = -1;
            GameRecords.ClearMatches(); repository.SetMessage("대전 기록을 초기화했습니다.");
            Repaint();
        }

        void SubmitIdea()
        {
            var name = ui.Input("idea.name"); var author = ui.Input("idea.author"); var description = ui.Input("idea.description");
            var submit = ui.Button("idea.submit"); submit.interactable = false;
            repository.SubmitIdea(name.text, author.text, description.text, success =>
            {
                if (submit) submit.interactable = true;
                if (success && name && description) { name.text = ""; description.text = ""; }
            });
        }
        void Ideas()
        {
            var ideas = repository.Ideas;
            ui.Show("idea.empty", ideas.Count == 0);
            ui.Text("idea.count").text = ideas.Count == 0 ? "" : $"{ideas.Count}개";
            FillList(ui.Rect("idea.list"), ui.Rect("idea.row"), ideas.Count, 12, (row, i) =>
            {
                var idea = ideas[i];
                SetText(row, "Name", idea.name);
                SetText(row, "Description", idea.description);
                SetText(row, "Meta", (string.IsNullOrEmpty(idea.author) ? "익명" : idea.author) + " · " + idea.When);
            });
        }

        static void FillList(RectTransform list, RectTransform template, int count, float gap, Action<RectTransform, int> fill)
        {
            Ui.Clear(list, template); template.gameObject.SetActive(false);
            float step = template.sizeDelta.y + gap;
            for (int i = 0; i < count; i++) fill(Ui.Clone(template, template.anchoredPosition + new Vector2(0, -i * step)), i);
            Ui.SetContentHeight(list, count * step - template.anchoredPosition.y);
        }
    }
}
