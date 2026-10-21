using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class ArenaStage : IDisposable
    {
        readonly BallFightApp app;
        readonly RectTransform stage;
        readonly BattleSimulation simulation;
        readonly SpriteBank playerBank, cpuBank;
        readonly RectTransform player, cpu;
        readonly Text playerEyes, cpuEyes;
        readonly Dictionary<int, RectTransform> entities = new Dictionary<int, RectTransform>();
        readonly float sx, sy;
        readonly DuelVisuals duelVisuals;
        readonly ImpactEffects effects;
        readonly Vector2 home;
        public bool HitStopped => effects.Feedback.Stopped;
        public ArenaStage(BallFightApp app, RectTransform parent, Rect rect, BattleSimulation simulation)
        {
            this.app = app; this.simulation = simulation;
            stage = app.Box(parent, "Arena stage", rect, Color.white);
            if (!app.BakingLayout) SceneUiBindings.ResetScope(stage);
            if (!stage.GetComponent<RectMask2D>()) stage.gameObject.AddComponent<RectMask2D>();
            home = stage.anchoredPosition;
            rect.size = stage.sizeDelta;
            sx = rect.width / BattleSimulation.Width; sy = rect.height / BattleSimulation.Height;
            app.Box(stage, "Top edge", new Rect(0, 0, rect.width, 2), BallFightApp.Border);
            app.Box(stage, "Bottom edge", new Rect(0, rect.height - 2, rect.width, 2), BallFightApp.Border);
            app.Box(stage, "Left edge", new Rect(0, 0, 2, rect.height), BallFightApp.Border);
            app.Box(stage, "Right edge", new Rect(rect.width - 2, 0, 2, rect.height), BallFightApp.Border);
            app.TransientUi = true;
            duelVisuals = new DuelVisuals(app, stage, simulation, sx, sy);
            app.TransientUi = false;
            playerBank = new SpriteBank(simulation.Player.Data); cpuBank = new SpriteBank(simulation.Cpu.Data);
            player = app.Box(stage, "1번", new Rect(0, 0, 62 * sx, 62 * sy), Color.white);
            cpu = app.Box(stage, "2번", new Rect(0, 0, 62 * sx, 62 * sy), Color.white);
            player.pivot = cpu.pivot = Vector2.one * .5f;
            playerEyes = app.Label(player, "1", new Rect(0, 0, 62 * sx, 62 * sy), Mathf.Max(12, Mathf.RoundToInt(23 * sx)), Color.white); playerEyes.alignment = TextAnchor.MiddleCenter;
            cpuEyes = app.Label(cpu, simulation.Sandbox ? "샌드백" : "2", new Rect(-20, 0, 62 * sx + 40, 62 * sy), simulation.Sandbox ? 13 : Mathf.Max(12, Mathf.RoundToInt(23 * sx)), simulation.Sandbox ? BallFightApp.TextColor : Color.white); cpuEyes.alignment = TextAnchor.MiddleCenter;
            app.TransientUi = true;
            effects = new ImpactEffects(app, stage, simulation, sx, sy); effects.Collect();
            app.TransientUi = false;
            Render();
        }
        void RenderFighter(RectTransform visual, Text eyes, BattleSimulation.Fighter fighter, SpriteBank bank)
        {
            visual.gameObject.SetActive(!fighter.Defeated);
            if (fighter.Defeated) return;
            var state = simulation.World.State(fighter); string key = string.IsNullOrEmpty(state.Sprite) ? fighter.Data.portrait : state.Sprite;
            var image = visual.GetComponent<Image>(); image.sprite = bank.Get(key, state); image.preserveAspect = true;
            ColorUtility.TryParseHtmlString(fighter.Data.color, out var color);
            int number = fighter == simulation.Player ? 1 : 2;
            image.color = string.IsNullOrEmpty(key) || key == "orb" ? Color.Lerp(color, Color.white, effects.Flash(number) * .75f) : Color.white;
            eyes.gameObject.SetActive(string.IsNullOrEmpty(key) || simulation.Sandbox && fighter == simulation.Cpu);
            visual.anchoredPosition = new Vector2(fighter.X * sx, -fighter.Y * sy);
            visual.localScale = effects.Scale(number) * (fighter.Shield > 0 ? 1.1f : 1);
        }
        public void CollectImpacts() { effects.Collect(); }
        public void AdvancePresentation(float realDeltaTime) { effects.Advance(realDeltaTime); }
        public void Render()
        {
            stage.anchoredPosition = home + new Vector2(effects.Feedback.OffsetX, effects.Feedback.OffsetY);
            duelVisuals.Render();
            RenderFighter(player, playerEyes, simulation.Player, playerBank); RenderFighter(cpu, cpuEyes, simulation.Cpu, cpuBank);
            var active = new HashSet<int>();
            foreach (var entity in simulation.World.Entities)
            {
                if (simulation.Ended || entity.Dead || entity.Owner.Defeated) continue; active.Add(entity.Id);
                if (!entities.TryGetValue(entity.Id, out var visual)) { visual = app.Box(stage, "Skill object", new Rect(0, 0, 24, 24), Color.white); visual.pivot = Vector2.one * .5f; entities[entity.Id] = visual; }
                var bank = entity.Owner == simulation.Player ? playerBank : cpuBank;
                var image = visual.GetComponent<Image>(); image.sprite = bank.Get(entity.Image, simulation.World.State(entity.Owner)); image.preserveAspect = true; image.color = new Color(entity.R, entity.G, entity.B, entity.A);
                visual.sizeDelta = new Vector2(entity.Size * sx, entity.Size * sy);
                visual.anchoredPosition = new Vector2(entity.X * sx, -entity.Y * sy);
                visual.localRotation = Quaternion.Euler(0, 0, -entity.Rotation);
            }
            foreach (int id in entities.Keys.Where(id => !active.Contains(id)).ToArray()) { UnityEngine.Object.Destroy(entities[id].gameObject); entities.Remove(id); }
            effects.RenderTrails();
        }
        public void Dispose()
        {
            effects.Dispose(); playerBank.Dispose(); cpuBank.Dispose();
            if (stage)
            {
                stage.gameObject.SetActive(false);
                if (stage.GetComponent<SceneUiSlot>()) SceneUiBindings.ResetScope(stage);
                else UnityEngine.Object.Destroy(stage.gameObject);
            }
        }
    }
}
