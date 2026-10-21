using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HongBallFight
{
    public class BallArena : MonoBehaviour
    {
        ArenaStage stage;
        Text clock, result, feed, leftStats, rightStats;
        RectTransform leftHp, rightHp;
        float accumulator;
        float finishAge;
        public BattleSimulation Simulation { get; private set; }
        const float Step = 1f / 120;
        public void Begin(BallFightApp app, RectTransform parent, FighterData left, FighterData right, Action exit)
        {
            Simulation = new BattleSimulation(left, right, false, UnityEngine.Random.Range(1, int.MaxValue));
            app.Label(parent, "볼 파이트", new Rect(56, 24, 800, 42), 23);
            app.Button(parent, "캐릭터 선택", new Rect(1066, 24, 158, 42), exit);
            stage = new ArenaStage(app, parent, new Rect(190, 192, BattleSimulation.Width, BattleSimulation.Height), Simulation);
            leftStats = app.Label(parent, "", new Rect(56, 83, 440, 72), 19);
            rightStats = app.Label(parent, "", new Rect(784, 83, 440, 72), 19);
            rightStats.alignment = TextAnchor.MiddleRight;
            app.Box(parent, "1번 체력", new Rect(56, 158, 440, 4), BallFightApp.Border);
            app.Box(parent, "2번 체력", new Rect(784, 158, 440, 4), BallFightApp.Border);
            ColorUtility.TryParseHtmlString(left.color, out var lc); ColorUtility.TryParseHtmlString(right.color, out var rc);
            leftHp = app.Box(parent, "1번 남은 체력", new Rect(56, 158, 440, 4), lc);
            rightHp = app.Box(parent, "2번 남은 체력", new Rect(784, 158, 440, 4), rc);
            clock = app.Label(parent, "", new Rect(550, 91, 180, 48), 22, BallFightApp.Muted); clock.alignment = TextAnchor.MiddleCenter;
            result = app.Label(parent, "", new Rect(180, 325, 920, 100), 34); result.alignment = TextAnchor.MiddleCenter;
            feed = app.Label(parent, "", new Rect(190, 580, 900, 40), 16, BallFightApp.Muted); feed.alignment = TextAnchor.MiddleCenter;
            app.Button(parent, "다시하기", new Rect(490, 698, 300, 48), () => SceneManager.LoadScene("Arena"));
            RefreshHud();
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
            string status = fighter.Data.skill == "army_aura" ? " · " + fighter.RankName : fighter.Data.skill == "pasta_whip" ? " · " + fighter.Cooldown.ToString("0.0") + "초" : "";
            return Mathf.CeilToInt(fighter.Hp) + " / " + fighter.Data.hp + status;
        }
        void RefreshHud()
        {
            var left = Simulation.Player; var right = Simulation.Cpu;
            leftStats.text = "1번 · " + left.Data.name + "\n" + Detail(left);
            rightStats.text = "2번 · " + right.Data.name + "\n" + Detail(right);
            leftHp.sizeDelta = new Vector2(left.Defeated ? 0 : 440 * left.Hp / left.Data.hp, 4);
            rightHp.sizeDelta = new Vector2(right.Defeated ? 0 : 440 * right.Hp / right.Data.hp, 4);
            clock.text = $"{(int)Simulation.Elapsed / 60:00}:{(int)Simulation.Elapsed % 60:00}";
            result.text = finishAge >= .4f ? Simulation.Result : "";
            string error = Simulation.World.Player.Error;
            if (error.Length == 0) error = Simulation.World.Cpu.Error;
            feed.text = error.Length > 0 ? "블록 실행 오류: " + error : Simulation.LastEvent;
        }
        void OnDestroy() { stage?.Dispose(); }
    }
}
