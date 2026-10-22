using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HongBallFight
{
    public class BallArena : MonoBehaviour
    {
        sealed class FighterHud
        {
            public Text Hp, Detail;
            public RectTransform Track, Fill;
            public bool Mirrored;
        }
        ArenaStage stage;
        Text clock, feed;
        FighterHud leftHud, rightHud;
        UiRegistry ui;
        float accumulator;
        float finishAge;
        bool resultShown;
        public BattleSimulation Simulation { get; private set; }
        const float Step = 1f / 120;

        public void Begin(UiRegistry ui, FighterData left, FighterData right, Action exit)
        {
            this.ui = ui;
            Simulation = new BattleSimulation(left, right, false, UnityEngine.Random.Range(1, int.MaxValue));
            ui.OnClick("arena.back", exit); ui.OnClick("arena.resultBack", exit);
            ui.OnClick("arena.restart", Restart); ui.OnClick("arena.resultRestart", Restart);
            clock = ui.Text("arena.clock"); feed = ui.Text("arena.feed");
            leftHud = BindHud(ui.Rect("arena.left"), Simulation.Player, "1", false);
            rightHud = BindHud(ui.Rect("arena.right"), Simulation.Cpu, "2", true);
            ui.Show("arena.result", false);
            stage = new ArenaStage(ui.Rect("arena.stage"), Simulation);
            RefreshHud();
        }
        static void Restart() => SceneManager.LoadScene("Arena");

        static FighterHud BindHud(RectTransform card, BattleSimulation.Fighter fighter, string number, bool mirrored)
        {
            Ui.SetPortrait(Ui.Child<RectTransform>(card, "Avatar"), fighter.Data, number);
            Ui.Child<Text>(card, "Name").text = number + "번 · " + fighter.Data.name;
            var hud = new FighterHud
            {
                Hp = Ui.Child<Text>(card, "Hp"), Detail = Ui.Child<Text>(card, "Detail"),
                Track = Ui.Child<RectTransform>(card, "Track"), Fill = Ui.Child<RectTransform>(card, "Fill"), Mirrored = mirrored
            };
            hud.Fill.GetComponent<Image>().color = Ui.ParseColor(fighter.Data.color);
            return hud;
        }

        void Update()
        {
            if (Simulation == null) return;
            stage.AdvancePresentation(Time.unscaledDeltaTime);
            if (!Simulation.Ended && !stage.HitStopped)
            {
                accumulator += Mathf.Min(Time.deltaTime, .05f);
                while (accumulator >= Step && !Simulation.Ended)
                {
                    Simulation.Tick(Step); accumulator -= Step; stage.CollectImpacts();
                    if (stage.HitStopped) { accumulator = 0; break; }
                }
            }
            else accumulator = 0;
            if (Simulation.Ended) finishAge += Time.unscaledDeltaTime;
            stage.Render(); RefreshHud();
        }
        static string Detail(BattleSimulation.Fighter fighter)
        {
            if (fighter.Defeated) return "패배";
            string skill = FighterData.SkillName(fighter.Data.skill);
            if (fighter.Data.skill == "army_aura") return skill + " · " + fighter.RankName;
            if (fighter.Cooldown > 0) return skill + " · " + fighter.Cooldown.ToString("0.0") + "초";
            return skill + " · 준비 완료";
        }
        static void RefreshFighter(FighterHud hud, BattleSimulation.Fighter fighter)
        {
            float ratio = fighter.Defeated ? 0 : Mathf.Clamp01(fighter.Hp / fighter.Data.hp);
            var track = hud.Track; float height = track.sizeDelta.y, width = track.sizeDelta.x * ratio;
            hud.Fill.gameObject.SetActive(width > 1);
            hud.Fill.sizeDelta = new Vector2(Mathf.Max(width, height), height);
            hud.Fill.anchoredPosition = track.anchoredPosition + new Vector2(hud.Mirrored ? track.sizeDelta.x - hud.Fill.sizeDelta.x : 0, 0);
            hud.Hp.text = Mathf.CeilToInt(fighter.Hp) + " / " + fighter.Data.hp;
            hud.Detail.text = Detail(fighter) + (fighter.Shield > 0 ? " · 보호막 " + Mathf.CeilToInt(fighter.Shield) : "");
        }
        void RefreshHud()
        {
            RefreshFighter(leftHud, Simulation.Player);
            RefreshFighter(rightHud, Simulation.Cpu);
            string time = $"{(int)Simulation.Elapsed / 60:00}:{(int)Simulation.Elapsed % 60:00}";
            clock.text = time;
            if (finishAge >= .4f && !resultShown)
            {
                resultShown = true; ui.Show("arena.result", true); ui.Rect("arena.result").SetAsLastSibling();
                ui.Text("arena.resultTitle").text = Simulation.Result; ui.Text("arena.resultDetail").text = "경기 시간 " + time;
            }
            string error = Simulation.World.Player.Error;
            if (error.Length == 0) error = Simulation.World.Cpu.Error;
            feed.text = error.Length > 0 ? "블록 실행 오류: " + error : Simulation.LastEvent;
        }
        void OnDestroy() { stage?.Dispose(); }
    }
}
