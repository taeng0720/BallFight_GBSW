using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HongBallFight
{
    public sealed class ImpactEffects : IDisposable
    {
        sealed class Particle
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;
            public Color Color;
            public float Spin;
            public float Angle;
        }
        sealed class Burst
        {
            public RectTransform Root, Ring, Flash;
            public Image RingImage, FlashImage;
            public Text Label;
            public Color Color;
            public float Age, Lifetime, Size;
            public readonly List<Particle> Particles = new List<Particle>();
        }
        readonly BattleSimulation simulation;
        readonly RectTransform layer;
        readonly float sx, sy;
        readonly List<Burst> bursts = new List<Burst>();
        readonly float[] pulse = new float[2], strength = new float[2];
        readonly RectTransform[][] trails = new RectTransform[2][];
        readonly ImpactAudio audio;
        int serial;
        public readonly ImpactFeedback Feedback = new ImpactFeedback();
        static Sprite ring;
        public static Sprite Ring
        {
            get
            {
                if (ring) return ring;
                var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false); var colors = new Color[16384];
                for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f));
                    colors[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01(63 - d) * Mathf.Clamp01(d - 58));
                }
                texture.SetPixels(colors); texture.Apply(); ring = Sprite.Create(texture, new Rect(0, 0, 128, 128), Vector2.one * .5f); return ring;
            }
        }
        public ImpactEffects(RectTransform parent, BattleSimulation simulation, float sx, float sy)
        {
            this.simulation = simulation; this.sx = sx; this.sy = sy;
            layer = Ui.Box(parent, "타격 효과", new Rect(0, 0, BattleSimulation.Width * sx, BattleSimulation.Height * sy), Color.clear);
            audio = layer.gameObject.AddComponent<ImpactAudio>();
            for (int f = 0; f < 2; f++)
            {
                trails[f] = new RectTransform[3];
                for (int i = 0; i < 3; i++)
                {
                    var trail = Ui.Box(parent, "이동 잔상", new Rect(0, 0, 1, 1), Color.clear); trail.GetComponent<Image>().sprite = Ui.Circle;
                    trail.pivot = Vector2.one * .5f; trail.SetAsFirstSibling(); trails[f][i] = trail;
                }
            }
        }
        public float Flash(int number) => Mathf.Clamp01(pulse[number - 1] / .11f) * strength[number - 1];
        public Vector3 Scale(int number)
        {
            float squash = Mathf.Sin(Mathf.Clamp01(pulse[number - 1] / .14f) * Mathf.PI) * strength[number - 1] * .16f;
            return new Vector3(1 + squash, 1 - squash * .7f, 1);
        }
        public void Collect()
        {
            foreach (var impact in simulation.DrainImpacts())
            {
                Feedback.Accept(impact); audio.Play(impact);
                if (impact.Kind == ImpactKind.Swing) continue;
                if (impact.Kind != ImpactKind.Promotion)
                {
                    pulse[impact.Number - 1] = .14f;
                    strength[impact.Number - 1] = impact.Kind == ImpactKind.Whip ? 1 : impact.Kind == ImpactKind.Dot ? .3f : Mathf.Clamp(Mathf.Sqrt(impact.Damage + impact.Absorbed * .25f) / 6, .3f, 1);
                }
                AddBurst(impact);
            }
        }
        RectTransform Circle(Transform parent, string name, float size, Sprite sprite, Color color)
        {
            var rect = Ui.Box(parent, name, new Rect(0, 0, size * sx, size * sy), color);
            rect.pivot = Vector2.one * .5f; rect.GetComponent<Image>().sprite = sprite; return rect;
        }
        void AddBurst(CombatImpact impact)
        {
            if (bursts.Count >= 28) { UnityEngine.Object.Destroy(bursts[0].Root.gameObject); bursts.RemoveAt(0); }
            bool defeat = impact.Kind == ImpactKind.Defeat, dot = impact.Kind == ImpactKind.Dot, promotion = impact.Kind == ImpactKind.Promotion, whip = impact.Kind == ImpactKind.Whip;
            var fighter = impact.Number == 1 ? simulation.Player : simulation.Cpu;
            ColorUtility.TryParseHtmlString(fighter.Data.color, out var tint);
            if (promotion) tint = new Color32(192, 158, 72, 255);
            var burst = new Burst
            {
                Root = Ui.Box(layer, defeat ? "패배 폭발" : "타격 섬광", new Rect(impact.X * sx, impact.Y * sy, 1, 1), Color.clear),
                Lifetime = defeat ? .7f : promotion ? .5f : dot ? .2f : .34f,
                Size = defeat ? 160 : whip ? 130 : promotion ? 100 : dot ? 36 : 60 + Mathf.Min(40, impact.Damage), Color = tint
            };
            burst.Ring = Circle(burst.Root, "충격파", 36, Ring, tint); burst.RingImage = burst.Ring.GetComponent<Image>();
            burst.Flash = Circle(burst.Root, "빛", defeat ? 100 : whip ? 80 : dot ? 20 : 50, Ui.Circle, Color.white); burst.FlashImage = burst.Flash.GetComponent<Image>();
            int count = defeat ? 24 : whip ? 12 : dot ? 3 : promotion ? 10 : 7;
            var random = new System.Random(++serial * 103);
            for (int i = 0; i < count; i++)
            {
                float angle = (float)(i * Math.PI * 2 / count + random.NextDouble() * .4), speed = (defeat ? 150 : whip ? 180 : dot ? 40 : 95) + (float)random.NextDouble() * (defeat ? 230 : whip ? 150 : 90);
                var rect = Ui.Box(burst.Root, "파편", new Rect(0, 0, (defeat ? 7 : 3) * sx, (defeat ? 11 : 12) * sy), tint);
                rect.pivot = Vector2.one * .5f; rect.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90);
                burst.Particles.Add(new Particle { Rect = rect, Image = rect.GetComponent<Image>(), Velocity = new Vector2(Mathf.Cos(angle) * speed * sx, Mathf.Sin(angle) * speed * sy), Color = i % 3 == 0 ? new Color(1, .83f, .4f) : tint, Spin = (float)random.NextDouble() * 600 - 300, Angle = angle * Mathf.Rad2Deg - 90 });
            }
            if (impact.Damage >= 10 || promotion)
            {
                burst.Label = Ui.Label(burst.Root, promotion ? fighter.RankName : "−" + impact.Damage.ToString("0.#"), new Rect(-80 * sx, -62 * sy, 160 * sx, 36 * sy), Mathf.Max(12, Mathf.RoundToInt(20 * sx)), promotion ? tint : Ui.TextColor);
                burst.Label.alignment = TextAnchor.MiddleCenter;
            }
            bursts.Add(burst);
        }
        public void Advance(float realDeltaTime)
        {
            Collect(); Feedback.Advance(realDeltaTime);
            for (int i = 0; i < 2; i++) pulse[i] = Mathf.Max(0, pulse[i] - realDeltaTime);
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                var burst = bursts[i]; burst.Age += realDeltaTime;
                if (burst.Age >= burst.Lifetime) { UnityEngine.Object.Destroy(burst.Root.gameObject); bursts.RemoveAt(i); continue; }
                float t = burst.Age / burst.Lifetime, fade = Mathf.Pow(1 - t, 1.5f);
                float size = Mathf.Lerp(25, burst.Size, 1 - (1 - t) * (1 - t)); burst.Ring.sizeDelta = new Vector2(size * sx, size * sy);
                burst.RingImage.color = new Color(burst.Color.r, burst.Color.g, burst.Color.b, fade * .75f);
                burst.FlashImage.color = new Color(1, 1, 1, Mathf.Max(0, 1 - burst.Age / .08f) * .9f);
                foreach (var particle in burst.Particles)
                {
                    particle.Rect.anchoredPosition = particle.Velocity * burst.Age * (1 - t * .25f) + Vector2.down * burst.Age * burst.Age * 100 * sy;
                    particle.Rect.localRotation = Quaternion.Euler(0, 0, particle.Angle + particle.Spin * burst.Age);
                    particle.Rect.localScale = Vector3.one * (1 - t * .7f);
                    particle.Image.color = new Color(particle.Color.r, particle.Color.g, particle.Color.b, fade);
                }
                if (burst.Label)
                {
                    burst.Label.rectTransform.anchoredPosition = new Vector2(-80 * sx, 62 * sy + burst.Age * 35 * sy);
                    var color = burst.Label.color; color.a = fade; burst.Label.color = color;
                }
            }
        }
        public void RenderTrails()
        {
            for (int f = 0; f < 2; f++)
            {
                var fighter = f == 0 ? simulation.Player : simulation.Cpu;
                bool launched = fighter.KnockbackTime > 0;
                var direction = launched ? new Vector2(fighter.KnockbackX, fighter.KnockbackY).normalized : new Vector2(fighter.VelocityX, fighter.VelocityY).normalized;
                ColorUtility.TryParseHtmlString(fighter.Data.color, out var tint);
                for (int i = 0; i < 3; i++)
                {
                    var trail = trails[f][i]; bool visible = !simulation.Ended && !fighter.Defeated && (fighter.KnockbackTime > 0 || fighter.MoveSpeed >= 280);
                    trail.gameObject.SetActive(visible); if (!visible) continue;
                    float size = 53 - i * 9; trail.sizeDelta = new Vector2(size * sx, size * sy);
                    float spacing = launched ? 30 : 13;
                    trail.anchoredPosition = new Vector2((fighter.X - direction.x * (i + 1) * spacing) * sx, -(fighter.Y - direction.y * (i + 1) * spacing) * sy);
                    trail.GetComponent<Image>().color = new Color(tint.r, tint.g, tint.b, (launched ? .28f : .07f) / (i + 1));
                }
            }
            layer.SetAsLastSibling();
        }
        public void Dispose() { if (layer) UnityEngine.Object.Destroy(layer.gameObject); }
    }
}
