using System;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class CreatorSandbox : MonoBehaviour
    {
        RectTransform stageRect;
        ArenaStage stage;
        BattleSimulation simulation;
        FighterData current;
        Text stats, console;
        Button skill, dummyLock;
        readonly Button[] speedButtons = new Button[4];
        static readonly float[] Speeds = { .5f, 1, 2, 4 };
        float accumulator, speed = 1;
        bool paused;

        public void Setup(UiRegistry ui, FighterData data)
        {
            stageRect = ui.Rect("sandbox.stage");
            stats = ui.Text("sandbox.stats"); console = ui.Text("sandbox.console");
            skill = ui.Button("sandbox.skill"); dummyLock = ui.Button("sandbox.lock");
            ui.OnClick("sandbox.skill", () => simulation?.UseSkill(1));
            ui.OnClick("sandbox.resetCooldown", () => { if (simulation != null) simulation.Player.Cooldown = 0; });
            ui.OnClick("sandbox.wall", () =>
            {
                if (simulation == null) return; simulation.Player.X = 30; simulation.Player.VelocityX = -simulation.Player.MoveSpeed;
            });
            ui.OnClick("sandbox.dummy", () =>
            {
                if (simulation == null) return; simulation.Player.X = simulation.Cpu.X - 61; simulation.Player.Y = simulation.Cpu.Y; simulation.Player.SetVelocity(1, 0);
            });
            ui.OnClick("sandbox.restart", () => { if (current != null) Load(current); });
            ui.OnClick("sandbox.pause", () => paused = !paused);
            ui.OnClick("sandbox.lock", () => { if (simulation != null) simulation.SetDummyLocked(!simulation.SandboxDummyLocked); });
            for (int i = 0; i < Speeds.Length; i++)
            {
                float selected = Speeds[i];
                speedButtons[i] = ui.Button("sandbox.speed" + i);
                ui.OnClick("sandbox.speed" + i, () => { speed = selected; RefreshSpeedButtons(); });
            }
            RefreshSpeedButtons();
            Load(data);
        }
        void RefreshSpeedButtons()
        {
            for (int i = 0; i < Speeds.Length; i++) if (speedButtons[i]) Ui.SetButtonStyle(speedButtons[i], Speeds[i] == speed);
        }
        public string Load(FighterData data)
        {
            var error = data.Validate(); if (error != null) return error;
            try
            {
                current = data.Copy(); stage?.Dispose();
                var dummy = new FighterData { name = "샌드백", hp = 1000, attack = 1, speed = 100, color = "#B1BED1" };
                simulation = new BattleSimulation(current, dummy, true); accumulator = 0;
                stage = new ArenaStage(stageRect, simulation); return null;
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
            Ui.SetText(skill, passive ? "패시브 · " + simulation.Player.RankName : "1번 스킬 테스트");
            stats.text = $"1번 {simulation.Player.Hp:0} / 샌드백 {simulation.Cpu.Hp:0} / {simulation.Player.Cooldown:0.0}초 · {speed:0.#}배{(paused ? " 정지" : "")}";
            string error = simulation.World.Player.Error;
            Ui.SetText(dummyLock, "샌드백 고정: " + (simulation.SandboxDummyLocked ? "켜짐" : "꺼짐"));
            console.text = error.Length > 0 ? error : simulation.World.Logs.Count > 0 ? simulation.World.Logs[simulation.World.Logs.Count - 1] : "샌드백은 넉백으로 날아갑니다. 체력이 0이면 회복됩니다.";
        }
        void OnDestroy() { stage?.Dispose(); }
    }
}
