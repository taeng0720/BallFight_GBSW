using System;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class CreatorSandbox : MonoBehaviour
    {
        BallFightApp app;
        RectTransform container;
        ArenaStage stage;
        BattleSimulation simulation;
        FighterData current;
        Text stats, console;
        Button skill, dummyLock;
        float accumulator, speed = 1;
        bool paused;
        readonly System.Collections.Generic.List<Button> speedButtons = new System.Collections.Generic.List<Button>();
        public void Setup(BallFightApp app, RectTransform parent, FighterData data)
        {
            this.app = app; container = parent; speedButtons.Clear();
            stats = app.Label(parent, "", new Rect(758, 2, 442, 34), 16, BallFightApp.Accent);
            skill = app.Button(parent, "1번 동작 테스트", new Rect(758, 42, 210, 34), () => simulation?.UseSkill(1), true);
            app.Button(parent, "쿨타임 초기화", new Rect(984, 42, 210, 34), () => { if (simulation != null) simulation.Player.Cooldown = 0; });
            app.Button(parent, "벽에 부딪히기", new Rect(758, 86, 210, 34), () =>
            {
                if (simulation == null) return; simulation.Player.X = 30; simulation.Player.VelocityX = -simulation.Player.MoveSpeed;
            });
            app.Button(parent, "샌드백에 부딪히기", new Rect(984, 86, 210, 34), () =>
            {
                if (simulation == null) return; simulation.Player.X = simulation.Cpu.X - 61; simulation.Player.Y = simulation.Cpu.Y; simulation.Player.SetVelocity(1, 0);
            });
            app.Button(parent, "테스트 재시작", new Rect(758, 130, 210, 34), () => { if (current != null) Load(current); });
            app.Button(parent, "일시정지 / 재개", new Rect(984, 130, 210, 34), () => paused = !paused);
            var speeds = new[] { .5f, 1, 2, 4 };
            for (int i = 0; i < speeds.Length; i++)
            {
                float selected = speeds[i];
                var button = app.Button(parent, selected + "배", new Rect(758 + i * 113, 174, 97, 32), () =>
                {
                    speed = selected;
                    for (int b = 0; b < speedButtons.Count; b++)
                    {
                        bool active = speeds[b] == speed;
                        speedButtons[b].targetGraphic.color = active ? BallFightApp.Accent : new Color32(237, 237, 237, 255);
                        speedButtons[b].GetComponentInChildren<Text>().color = active ? Color.white : BallFightApp.TextColor;
                    }
                });
                if (selected == 1) { button.targetGraphic.color = BallFightApp.Accent; button.GetComponentInChildren<Text>().color = Color.white; }
                speedButtons.Add(button);
            }
            dummyLock = app.Button(parent, "샌드백 고정: 꺼짐", new Rect(758, 212, 436, 28), () => { if (simulation != null) simulation.SetDummyLocked(!simulation.SandboxDummyLocked); });
            console = app.Label(parent, "", new Rect(758, 242, 442, 28), 12, BallFightApp.Muted);
            Load(data);
        }
        public string Load(FighterData data)
        {
            var error = data.Validate(); if (error != null) return error;
            try
            {
                current = data.Copy(); stage?.Dispose();
                var dummy = new FighterData { name = "샌드백", hp = 1000, attack = 1, speed = 100, color = "#B1BED1" };
                simulation = new BattleSimulation(current, dummy, true); accumulator = 0;
                stage = new ArenaStage(app, container, new Rect(31, 0, 675, 270), simulation); return null;
            }
            catch (Exception failure) { return failure.Message; }
        }
        void Update()
        {
            if (simulation == null) return;
            stage.AdvancePresentation(Time.unscaledDeltaTime);
            if (!paused && !stage.HitStopped)
            {
                accumulator += Mathf.Min(Time.deltaTime, .1f) * speed;
                while (accumulator >= 1f / 120)
                {
                    simulation.Tick(1f / 120); accumulator -= 1f / 120; stage.CollectImpacts();
                    if (stage.HitStopped) { accumulator = 0; break; }
                }
            }
            else accumulator = 0;
            stage.Render();
            bool passive = simulation.Player.Data.skill == "army_aura";
            skill.interactable = !passive && simulation.Player.Cooldown <= 0;
            skill.GetComponentInChildren<Text>().text = passive ? "패시브 · " + simulation.Player.RankName : "1번 동작 테스트";
            stats.text = $"1번 {simulation.Player.Hp:0} / 샌드백 {simulation.Cpu.Hp:0} / {simulation.Player.Cooldown:0.0}초 · {speed:0.#}배{(paused ? " 정지" : "")}";
            string error = simulation.World.Player.Error;
            dummyLock.GetComponentInChildren<Text>().text = "샌드백 고정: " + (simulation.SandboxDummyLocked ? "켜짐" : "꺼짐");
            console.text = error.Length > 0 ? error : simulation.World.Logs.Count > 0 ? simulation.World.Logs[simulation.World.Logs.Count - 1] : "샌드백은 넉백으로 날아갑니다. 체력이 0이면 회복됩니다.";
        }
        void OnDestroy() { stage?.Dispose(); }
    }
}
