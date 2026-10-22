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
            public Text Hp;
            public Gauge Health, Skill;
        }
        sealed class Gauge
        {
            readonly RectTransform fill;
            readonly Vector2 origin;
            readonly float width;
            readonly bool mirrored;
            public Gauge(RectTransform fill, bool mirrored)
            {
                this.fill = fill; this.mirrored = mirrored;
                origin = fill.anchoredPosition; width = fill.sizeDelta.x;
            }
            public void Set(float ratio)
            {
                float size = width * Mathf.Clamp01(ratio);
                fill.gameObject.SetActive(size > .5f);
                fill.sizeDelta = new Vector2(size, fill.sizeDelta.y);
                fill.anchoredPosition = origin + new Vector2(mirrored ? width - size : 0, 0);
            }
        }

        const float CountdownSeconds = 3, StartBannerSeconds = .7f, Step = 1f / 120, GlyphOffset = .07f;
        ArenaStage stage;
        Text countdown;
        FighterHud leftHud, rightHud;
        UiRegistry ui;
        float accumulator, finishAge, countdownLeft = CountdownSeconds, bannerLeft, resultAge = -1;
        bool resultShown, recorded;
        public BattleSimulation Simulation { get; private set; }

        public void Begin(UiRegistry ui, FighterData left, FighterData right, Action exit)
        {
            this.ui = ui;
            Simulation = new BattleSimulation(left, right, false, UnityEngine.Random.Range(1, int.MaxValue));
            ui.OnClick("arena.back", exit); ui.OnClick("arena.resultBack", exit);
            ui.OnClick("arena.restart", Restart); ui.OnClick("arena.resultRestart", Restart);
            countdown = ui.Text("arena.countdown"); CenterOnPivot(countdown);
            CenterOnPivot(ui.Text("arena.resultTitle")); CenterOnPivot(ui.Text("arena.resultLabel"));
            leftHud = BindHud(ui.Rect("arena.left"), Simulation.Player, false);
            rightHud = BindHud(ui.Rect("arena.right"), Simulation.Cpu, true);
            ui.Show("arena.result", false);
            stage = new ArenaStage(ui.Rect("arena.stage"), Simulation) { ShowAim = true };
            countdown.transform.SetAsLastSibling();
            RefreshHud();
        }
        static void Restart() => SceneManager.LoadScene("Arena");

        static void CenterOnPivot(Text text)
        {
            var rt = text.rectTransform;
            var center = rt.anchoredPosition + Vector2.Scale(new Vector2(.5f, .5f) - rt.pivot, rt.sizeDelta);
            rt.pivot = new Vector2(.5f, .5f);
            rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, text.fontSize * 4), Mathf.Max(rt.sizeDelta.y, text.fontSize * 1.6f));
            rt.anchoredPosition = center + Vector2.up * text.fontSize * GlyphOffset;
            text.alignment = TextAnchor.MiddleCenter; text.lineSpacing = 1;
            text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        }
        void ShowCountdown(string value, float scale, float alpha)
        {
            countdown.text = value;
            countdown.transform.localScale = Vector3.one * scale;
            var color = countdown.color; color.a = alpha; countdown.color = color;
        }

        static FighterHud BindHud(RectTransform hud, BattleSimulation.Fighter fighter, bool mirrored)
        {
            var color = Ui.ParseColor(fighter.Data.color);
            var name = Ui.Child<Text>(hud, "Name"); name.text = fighter.Data.name; name.color = color;
            var skill = Ui.Child<RectTransform>(hud, "Skill Fill"); skill.GetComponent<Image>().color = color;
            return new FighterHud
            {
                Hp = Ui.Child<Text>(hud, "Hp"),
                Health = new Gauge(Ui.Child<RectTransform>(hud, "Fill"), mirrored),
                Skill = new Gauge(skill, mirrored)
            };
        }

        void Update()
        {
            if (Simulation == null) return;
            float realDelta = Time.unscaledDeltaTime;
            stage.AdvancePresentation(realDelta);
            if (countdownLeft > 0)
            {
                countdownLeft -= realDelta;
                if (countdownLeft > 0)
                {
                    float t = countdownLeft - Mathf.Floor(countdownLeft);
                    ShowCountdown(Mathf.CeilToInt(countdownLeft).ToString(), Mathf.Lerp(1, 1.7f, t * t), Mathf.Clamp01(.35f + t * 2));
                }
                else
                {
                    ShowCountdown("시작!", 1.25f, 1);
                    bannerLeft = StartBannerSeconds; stage.ShowAim = false;
                }
                stage.Render(); RefreshHud(); return;
            }
            if (bannerLeft > 0)
            {
                bannerLeft -= realDelta;
                float t = Mathf.Clamp01(bannerLeft / StartBannerSeconds);
                ShowCountdown("시작!", Mathf.Lerp(1, 1.25f, t * t), Mathf.Clamp01(t * 3));
                countdown.gameObject.SetActive(bannerLeft > 0);
            }

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
            if (Simulation.Ended) { finishAge += realDelta; Record(); }
            stage.Render(); RefreshHud();
        }
        void Record()
        {
            if (recorded) return;
            recorded = true;
            int winner = Simulation.Player.Defeated == Simulation.Cpu.Defeated ? 0 : Simulation.Cpu.Defeated ? 1 : 2;
            GameRecords.AddMatch(Simulation.Player.Data, Simulation.Cpu.Data, winner, Simulation.Elapsed);
        }
        static float SkillCharge(BattleSimulation.Fighter fighter)
        {
            if (fighter.Defeated) return 0;
            bool passive = fighter.Data.skill == "army_aura";
            if (passive && fighter.Rank >= BattleSimulation.RankNames.Length - 1) return 1;
            float max = passive ? 3 : fighter.Data.cooldown;
            return 1 - fighter.Cooldown / Mathf.Max(.01f, max);
        }
        static void RefreshFighter(FighterHud hud, BattleSimulation.Fighter fighter)
        {
            hud.Health.Set(fighter.Defeated ? 0 : fighter.Hp / fighter.Data.hp);
            hud.Skill.Set(SkillCharge(fighter));
            hud.Hp.text = Mathf.CeilToInt(fighter.Hp) + " / " + fighter.Data.hp;
        }
        void RefreshHud()
        {
            RefreshFighter(leftHud, Simulation.Player);
            RefreshFighter(rightHud, Simulation.Cpu);
            if (finishAge >= .4f && !resultShown)
            {
                resultShown = true; resultAge = 0; ui.Show("arena.result", true); ui.Rect("arena.result").SetAsLastSibling();
                bool draw = Simulation.Player.Defeated == Simulation.Cpu.Defeated;
                var winner = Simulation.Cpu.Defeated ? Simulation.Player : Simulation.Cpu;
                var title = ui.Text("arena.resultTitle");
                title.text = draw ? "무승부" : winner.Data.name;
                title.color = draw ? Ui.Ink : Ui.ParseColor(winner.Data.color);
                ui.Show("arena.resultLabel", !draw);
                ui.Text("arena.resultDetail").text = $"경기 시간 {(int)Simulation.Elapsed / 60:00}:{(int)Simulation.Elapsed % 60:00}";
            }
            if (resultAge >= 0 && resultAge < .3f)
            {
                resultAge += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(resultAge / .3f);
                ui.Rect("arena.resultTitle").localScale = Vector3.one * Mathf.Lerp(1.35f, 1, 1 - (1 - t) * (1 - t));
            }
        }
        void OnDestroy() { stage?.Dispose(); }
    }
}
