using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class ArenaStage : IDisposable
    {
        readonly RectTransform content;
        readonly BattleSimulation simulation;
        readonly SpriteBank playerBank, cpuBank;
        readonly RectTransform player, cpu;
        readonly Image playerFace, cpuFace;
        readonly Text playerLabel, cpuLabel;
        readonly Dictionary<int, RectTransform> entities = new Dictionary<int, RectTransform>();
        readonly float sx, sy;
        readonly DuelVisuals duelVisuals;
        readonly ImpactEffects effects;
        public bool HitStopped => effects.Feedback.Stopped;

        public ArenaStage(RectTransform stage, BattleSimulation simulation)
        {
            this.simulation = simulation;
            var size = stage.rect.size;
            sx = size.x / BattleSimulation.Width; sy = size.y / BattleSimulation.Height;
            content = Ui.Group(stage, "Runtime", new Rect(0, 0, size.x, size.y));
            duelVisuals = new DuelVisuals(content, simulation, sx, sy);
            playerBank = new SpriteBank(simulation.Player.Data); cpuBank = new SpriteBank(simulation.Cpu.Data);
            player = Fighter("1번", simulation.Player, "1", out playerFace, out playerLabel);
            cpu = Fighter("2번", simulation.Cpu, simulation.Sandbox ? "샌드백" : "2", out cpuFace, out cpuLabel);
            if (simulation.Sandbox) { cpuLabel.fontSize = 13; cpuLabel.color = Ui.TextColor; }
            effects = new ImpactEffects(content, simulation, sx, sy); effects.Collect();
            Render();
        }
        RectTransform Fighter(string name, BattleSimulation.Fighter fighter, string number, out Image face, out Text label)
        {
            var ball = Ui.Portrait(content, name, new Rect(0, 0, 62 * sx, 62 * sy), Ui.ParseColor(fighter.Data.color));
            ball.pivot = Vector2.one * .5f;
            face = Ui.Child<Image>(ball, "Face"); face.sprite = FighterPortraits.Face(fighter.Data.name);
            label = Ui.Child<Text>(ball, "Number"); label.text = number;
            label.fontSize = Mathf.Max(12, Mathf.RoundToInt(23 * sx));
            label.rectTransform.sizeDelta += new Vector2(40, 0); label.rectTransform.anchoredPosition -= new Vector2(20, 0);
            return ball;
        }
        void RenderFighter(RectTransform visual, Image face, Text label, BattleSimulation.Fighter fighter, SpriteBank bank)
        {
            visual.gameObject.SetActive(!fighter.Defeated);
            if (fighter.Defeated) return;
            var state = simulation.World.State(fighter); string key = string.IsNullOrEmpty(state.Sprite) ? fighter.Data.portrait : state.Sprite;
            bool plainBall = string.IsNullOrEmpty(key) || key == "orb";
            bool showFace = plainBall && face.sprite;
            var image = visual.GetComponent<Image>(); image.sprite = bank.Get(key, state); image.preserveAspect = true;
            var color = Ui.ParseColor(fighter.Data.color);
            int number = fighter == simulation.Player ? 1 : 2;
            image.color = plainBall ? Color.Lerp(color, Color.white, effects.Flash(number) * .75f) : Color.white;
            face.gameObject.SetActive(showFace);
            face.color = Color.Lerp(Color.white, new Color(1, .75f, .75f), effects.Flash(number));
            label.gameObject.SetActive(!showFace && (plainBall || simulation.Sandbox && fighter == simulation.Cpu));
            visual.anchoredPosition = new Vector2(fighter.X * sx, -fighter.Y * sy);
            visual.localScale = effects.Scale(number) * (fighter.Shield > 0 ? 1.1f : 1);
        }
        public void CollectImpacts() { effects.Collect(); }
        public void AdvancePresentation(float realDeltaTime) { effects.Advance(realDeltaTime); }
        public void Render()
        {
            content.anchoredPosition = new Vector2(effects.Feedback.OffsetX, -effects.Feedback.OffsetY);
            duelVisuals.Render();
            RenderFighter(player, playerFace, playerLabel, simulation.Player, playerBank); RenderFighter(cpu, cpuFace, cpuLabel, simulation.Cpu, cpuBank);
            var active = new HashSet<int>();
            foreach (var entity in simulation.World.Entities)
            {
                if (simulation.Ended || entity.Dead || entity.Owner.Defeated) continue; active.Add(entity.Id);
                if (!entities.TryGetValue(entity.Id, out var visual)) { visual = Ui.Box(content, "Skill object", new Rect(0, 0, 24, 24), Color.white); visual.pivot = Vector2.one * .5f; entities[entity.Id] = visual; }
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
            if (content) { content.gameObject.SetActive(false); UnityEngine.Object.Destroy(content.gameObject); }
        }
    }
}
