using System;
using System.Collections.Generic;

namespace HongBallFight
{
    public enum ImpactKind { Hit, Contact, Dot, Wall, Shield, Defeat, Promotion, Swing, Whip, Ricochet }
    public sealed class CombatImpact
    {
        public int Number;
        public float X, Y, Damage, Absorbed;
        public ImpactKind Kind;
    }
    public sealed partial class BattleSimulation
    {
        readonly Queue<CombatImpact> impacts = new Queue<CombatImpact>();
        internal void EmitImpact(Fighter fighter, float damage, ImpactKind kind, float absorbed = 0)
        {
            if (impacts.Count >= 128) impacts.Dequeue();
            impacts.Enqueue(new CombatImpact { Number = fighter == Player ? 1 : 2, X = fighter.X, Y = fighter.Y, Damage = damage, Absorbed = absorbed, Kind = kind });
        }
        void Defeat(Fighter fighter)
        {
            if (fighter.Defeated) return;
            fighter.Defeated = true; EmitImpact(fighter, 0, ImpactKind.Defeat);
        }
        public List<CombatImpact> DrainImpacts()
        {
            var result = new List<CombatImpact>(impacts); impacts.Clear(); return result;
        }
    }
    public sealed class ImpactFeedback
    {
        public float StopRemaining { get; private set; }
        public float ShakeStrength { get; private set; }
        public float ShakeRemaining { get; private set; }
        public bool Stopped => StopRemaining > 0;
        public float OffsetX => ShakeRemaining > 0 ? (float)Math.Sin(clock * 91) * ShakeStrength * Math.Min(1, ShakeRemaining / .12f) : 0;
        public float OffsetY => ShakeRemaining > 0 ? (float)Math.Cos(clock * 117) * ShakeStrength * .65f * Math.Min(1, ShakeRemaining / .12f) : 0;
        float clock;
        public void Accept(CombatImpact impact)
        {
            if (impact.Kind == ImpactKind.Promotion || impact.Kind == ImpactKind.Swing) return;
            float power = Math.Clamp((float)Math.Sqrt(Math.Max(0, impact.Damage + impact.Absorbed * .2f)), 0, 10);
            float shake = Math.Clamp(power * .48f, .25f, 4.8f), stop = Math.Clamp(.01f + power * .0055f, .012f, .065f), duration = .12f + power * .009f;
            if (impact.Kind == ImpactKind.Dot) { shake *= .18f; stop *= .16f; duration = .07f; }
            if (impact.Kind == ImpactKind.Shield) { shake *= .45f; stop *= .4f; }
            if (impact.Kind == ImpactKind.Wall) { shake = Math.Min(5, shake * 1.2f); stop *= 1.15f; }
            if (impact.Kind == ImpactKind.Whip) { shake = Math.Max(5f, shake); stop = Math.Max(.11f, stop); duration = .26f; }
            if (impact.Kind == ImpactKind.Ricochet) { shake = 1.4f; stop = .018f; duration = .10f; }
            if (impact.Kind == ImpactKind.Defeat) { shake = 5; stop = .085f; duration = .28f; }
            StopRemaining = Math.Max(StopRemaining, stop);
            ShakeStrength = Math.Max(ShakeStrength, shake); ShakeRemaining = Math.Max(ShakeRemaining, duration);
        }
        public void Advance(float realDeltaTime)
        {
            if (!float.IsFinite(realDeltaTime) || realDeltaTime < 0) throw new ArgumentOutOfRangeException(nameof(realDeltaTime));
            clock += realDeltaTime; StopRemaining = Math.Max(0, StopRemaining - realDeltaTime); ShakeRemaining = Math.Max(0, ShakeRemaining - realDeltaTime);
            ShakeStrength *= (float)Math.Exp(-10 * realDeltaTime);
            if (ShakeRemaining == 0) ShakeStrength = 0;
        }
    }
}
