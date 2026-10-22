using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class DuelVisuals
    {
        sealed class FighterView
        {
            public RectTransform Aura;
            public Text Status;
            public RectTransform[] Noodles;
        }
        readonly RectTransform parent;
        readonly BattleSimulation simulation;
        readonly float sx, sy;
        readonly Dictionary<BattleSimulation.Fighter, FighterView> fighters = new Dictionary<BattleSimulation.Fighter, FighterView>();
        readonly Dictionary<int, RectTransform> pickups = new Dictionary<int, RectTransform>();
        static Sprite aura;
        static Sprite AuraSprite
        {
            get
            {
                if (aura) return aura;
                var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false); var colors = new Color[65536];
                for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(127.5f, 127.5f));
                    float alpha = r > 126 ? 0 : r > 123 ? .5f : .055f;
                    colors[y * 256 + x] = new Color(1, 1, 1, alpha);
                }
                texture.SetPixels(colors); texture.Apply(); aura = Sprite.Create(texture, new Rect(0, 0, 256, 256), Vector2.one * .5f); return aura;
            }
        }
        public DuelVisuals(RectTransform parent, BattleSimulation simulation, float sx, float sy)
        {
            this.parent = parent; this.simulation = simulation; this.sx = sx; this.sy = sy;
            foreach (var fighter in new[] { simulation.Player, simulation.Cpu })
            {
                var view = new FighterView(); fighters[fighter] = view;
                if (fighter.Data.skill == "army_aura")
                {
                    view.Aura = Ui.Box(parent, "진급 오라", new Rect(0, 0, BattleSimulation.AuraRadius * 2 * sx, BattleSimulation.AuraRadius * 2 * sy), new Color32(107, 138, 101, 255));
                    view.Aura.GetComponent<Image>().sprite = AuraSprite;
                }
                view.Status = Ui.Label(parent, "", new Rect(0, 0, 160 * sx, 26 * sy), Math.Max(10, Mathf.RoundToInt(14 * sx)), Ui.Muted); view.Status.alignment = TextAnchor.MiddleCenter;
                if (fighter.Data.skill == "pasta_whip")
                {
                    view.Noodles = new RectTransform[BattleSimulation.WhipStrands * BattleSimulation.WhipSegments];
                    for (int i = 0; i < view.Noodles.Length; i++)
                    {
                        var line = Ui.Box(parent, "파스타 면", new Rect(0, 0, 1, 3.5f * sy), i / BattleSimulation.WhipSegments % 2 == 0 ? new Color32(207, 163, 70, 255) : new Color32(231, 195, 108, 255));
                        line.pivot = new Vector2(0, .5f); view.Noodles[i] = line;
                    }
                }
            }
        }
        Vector2 NoodlePoint(BattleSimulation.Fighter fighter, float t, int strand)
        {
            float phase = 1 - fighter.WhipTime / BattleSimulation.WhipDuration;
            float angle = fighter.WhipAngle * Mathf.Deg2Rad;
            float length = 28 + (BattleSimulation.WhipReach - 28) * t;
            float bend = (strand - (BattleSimulation.WhipStrands - 1) * .5f) * 6 + Mathf.Sin(t * Mathf.PI) * Mathf.Sin(phase * Mathf.PI) * 12;
            return new Vector2((fighter.X + Mathf.Cos(angle) * length - Mathf.Sin(angle) * bend) * sx, -(fighter.Y + Mathf.Sin(angle) * length + Mathf.Cos(angle) * bend) * sy);
        }
        public void Render()
        {
            foreach (var item in fighters)
            {
                var fighter = item.Key; var view = item.Value;
                if (view.Aura)
                {
                    view.Aura.gameObject.SetActive(fighter.Hp > 0 && !fighter.Defeated);
                    view.Aura.anchoredPosition = new Vector2((fighter.X - BattleSimulation.AuraRadius) * sx, -(fighter.Y - BattleSimulation.AuraRadius) * sy);
                }
                view.Status.text = (fighter.Data.skill == "army_aura" ? fighter.RankName : "") + (fighter.SlowFactor < 1 ? " 느려짐" : "");
                view.Status.gameObject.SetActive(!fighter.Defeated);
                view.Status.rectTransform.anchoredPosition = new Vector2((fighter.X - 80) * sx, -(fighter.Y + BattleSimulation.Radius + 8) * sy);
                if (view.Noodles == null) continue;
                for (int i = 0; i < view.Noodles.Length; i++)
                {
                    var line = view.Noodles[i]; bool visible = !simulation.Ended && fighter.WhipTime > 0 && fighter.Hp > 0 && !fighter.Defeated; line.gameObject.SetActive(visible); if (!visible) continue;
                    int segment = i % BattleSimulation.WhipSegments, strand = i / BattleSimulation.WhipSegments;
                    var a = NoodlePoint(fighter, segment / (float)BattleSimulation.WhipSegments, strand); var b = NoodlePoint(fighter, (segment + 1) / (float)BattleSimulation.WhipSegments, strand);
                    var delta = b - a; line.anchoredPosition = a; line.sizeDelta = new Vector2(delta.magnitude + 1, 3.5f * sy);
                    line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                }
            }
            var active = new HashSet<int>();
            foreach (var pickup in simulation.RankPickups)
            {
                if (simulation.Ended || pickup.Owner.Defeated) continue;
                active.Add(pickup.Id);
                if (!pickups.TryGetValue(pickup.Id, out var visual))
                {
                    float w = BattleSimulation.PickupWidth, h = BattleSimulation.PickupHeight;
                    visual = Ui.Box(parent, "진급 계급장", new Rect(0, 0, w * sx, h * sy), new Color32(235, 240, 232, 255));
                    Ui.Box(visual, "계급장 테두리", new Rect(0, 0, w * sx, 3 * sy), new Color32(111, 135, 100, 255));
                    Ui.Box(visual, "계급장 아래", new Rect(0, (h - 3) * sy, w * sx, 3 * sy), new Color32(111, 135, 100, 255));
                    var ink = pickup.Rank < 5 ? new Color32(95, 120, 84, 255) : new Color32(173, 139, 61, 255);
                    for (int stripe = 0; stripe < pickup.StripeCount; stripe++)
                    {
                        float lineY = 10 + stripe * 8;
                        Ui.Box(visual, "계급 줄 " + (stripe + 1), new Rect(12 * sx, lineY * sy, 40 * sx, 5 * sy), ink);
                    }
                    if (pickup.Rank >= 4)
                    {
                        var symbol = Ui.Label(visual, pickup.Rank >= 11 ? "★" : pickup.Rank >= 8 ? "◆" : "◇", new Rect(8 * sx, 41 * sy, 48 * sx, 17 * sy), Math.Max(11, Mathf.RoundToInt(16 * sx)), ink);
                        symbol.alignment = TextAnchor.MiddleCenter;
                    }
                    var title = Ui.Label(visual, pickup.RankName, new Rect(0, 60 * sy, w * sx, 18 * sy), Math.Max(10, Mathf.RoundToInt(14 * sx)), ink); title.alignment = TextAnchor.MiddleCenter;
                    pickups[pickup.Id] = visual;
                }
                visual.anchoredPosition = new Vector2((pickup.X - BattleSimulation.PickupWidth / 2) * sx, -(pickup.Y - BattleSimulation.PickupHeight / 2) * sy);
            }
            foreach (int id in pickups.Keys.Where(id => !active.Contains(id)).ToArray()) { UnityEngine.Object.Destroy(pickups[id].gameObject); pickups.Remove(id); }
        }
    }
}
