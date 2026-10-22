using System;
using System.Collections.Generic;
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
        readonly Image playerImage, cpuImage, playerFace, cpuFace;
        readonly Color playerColor, cpuColor;
        readonly HashSet<int> activeEntities = new HashSet<int>();
        readonly List<int> staleEntities = new List<int>();
        readonly Dictionary<int, Image> entityImages = new Dictionary<int, Image>();
        readonly Text playerLabel, cpuLabel;
        readonly RectTransform playerAim, cpuAim;
        public bool ShowAim;
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
            playerAim = Aim("1번 방향"); cpuAim = Aim("2번 방향");
            player = Fighter("1번", simulation.Player, "1", out playerFace, out playerLabel);
            cpu = Fighter("2번", simulation.Cpu, simulation.Sandbox ? "샌드백" : "2", out cpuFace, out cpuLabel);
            playerImage = player.GetComponent<Image>(); cpuImage = cpu.GetComponent<Image>();
            playerColor = Ui.ParseColor(simulation.Player.Data.color); cpuColor = Ui.ParseColor(simulation.Cpu.Data.color);
            if (simulation.Sandbox) { cpuLabel.fontSize = 13; cpuLabel.color = Ui.TextColor; }
            effects = new ImpactEffects(content, simulation, sx, sy); effects.Collect();
            Render();
        }
        RectTransform Fighter(string name, BattleSimulation.Fighter fighter, string number, out Image face, out Text label)
        {
            float diameter = BattleSimulation.Radius * 2;
            var ball = Ui.Portrait(content, name, new Rect(0, 0, diameter * sx, diameter * sy), Ui.ParseColor(fighter.Data.color));
            ball.pivot = Vector2.one * .5f;
            face = Ui.Child<Image>(ball, "Face"); face.sprite = FighterPortraits.Face(fighter.Data.name);
            label = Ui.Child<Text>(ball, "Number"); label.text = number;
            label.fontSize = Mathf.Max(12, Mathf.RoundToInt(23 * sx));
            label.rectTransform.sizeDelta += new Vector2(40, 0); label.rectTransform.anchoredPosition -= new Vector2(20, 0);
            return ball;
        }
        RectTransform Aim(string name)
        {
            var line = Ui.Box(content, name, new Rect(0, 0, 44 * sx, 3), Ui.Ink);
            line.pivot = new Vector2(0, .5f); return line;
        }
        void RenderAim(RectTransform aim, BattleSimulation.Fighter fighter)
        {
            var direction = new Vector2(fighter.VelocityX, -fighter.VelocityY);
            bool visible = ShowAim && !fighter.Defeated && direction.sqrMagnitude > .01f;
            aim.gameObject.SetActive(visible);
            if (!visible) return;
            direction.Normalize();
            var center = new Vector2(fighter.X * sx, -fighter.Y * sy);
            aim.anchoredPosition = center + new Vector2(direction.x * sx, direction.y * sy) * BattleSimulation.Radius;
            aim.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }
        void RenderFighter(RectTransform visual, Image image, Image face, Text label, Color color, BattleSimulation.Fighter fighter, SpriteBank bank)
        {
            visual.gameObject.SetActive(!fighter.Defeated);
            if (fighter.Defeated) return;
            var state = simulation.World.State(fighter); string key = string.IsNullOrEmpty(state.Sprite) ? fighter.Data.portrait : state.Sprite;
            bool plainBall = string.IsNullOrEmpty(key) || key == "orb";
            bool showFace = plainBall && face.sprite;
            image.sprite = bank.Get(key, state); image.preserveAspect = true;
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
            RenderFighter(player, playerImage, playerFace, playerLabel, playerColor, simulation.Player, playerBank); RenderFighter(cpu, cpuImage, cpuFace, cpuLabel, cpuColor, simulation.Cpu, cpuBank);
            RenderAim(playerAim, simulation.Player); RenderAim(cpuAim, simulation.Cpu);
            activeEntities.Clear();
            foreach (var entity in simulation.World.Entities)
            {
                if (simulation.Ended || entity.Dead || entity.Owner.Defeated) continue; activeEntities.Add(entity.Id);
                if (!entities.TryGetValue(entity.Id, out var visual))
                {
                    visual = Ui.Box(content, "Skill object", new Rect(0, 0, 24, 24), Color.white); visual.pivot = Vector2.one * .5f;
                    entities[entity.Id] = visual; entityImages[entity.Id] = visual.GetComponent<Image>();
                }
                var bank = entity.Owner == simulation.Player ? playerBank : cpuBank;
                var image = entityImages[entity.Id]; image.sprite = bank.Get(entity.Image, simulation.World.State(entity.Owner)); image.preserveAspect = true; image.color = new Color(entity.R, entity.G, entity.B, entity.A);
                visual.sizeDelta = new Vector2(entity.Size * sx, entity.Size * sy);
                visual.anchoredPosition = new Vector2(entity.X * sx, -entity.Y * sy);
                visual.localRotation = Quaternion.Euler(0, 0, -entity.Rotation);
            }
            staleEntities.Clear();
            foreach (int id in entities.Keys) if (!activeEntities.Contains(id)) staleEntities.Add(id);
            foreach (int id in staleEntities) { UnityEngine.Object.Destroy(entities[id].gameObject); entities.Remove(id); entityImages.Remove(id); }
            effects.RenderTrails();
        }
        public void Dispose()
        {
            effects.Dispose(); playerBank.Dispose(); cpuBank.Dispose();
            if (content) { content.gameObject.SetActive(false); UnityEngine.Object.Destroy(content.gameObject); }
        }
    }
}
