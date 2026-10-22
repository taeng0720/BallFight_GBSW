using System;
using System.Collections.Generic;
using System.Linq;

namespace HongBallFight
{
    public sealed partial class BattleSimulation
    {
        public const float AuraRadius = 155, AuraSlowFactor = .55f, AuraInterval = .5f;
        public const float WhipReach = 190, WhipDuration = .16f, WhipKnockback = 3600;
        public const int WhipStrands = 2, WhipSegments = 8;
        public const float PickupWidth = 64, PickupHeight = 80, PickupRadius = 35;
        public static readonly string[] RankNames = { "하사", "중사", "상사", "원사", "준위", "소위", "중위", "대위", "소령", "중령", "대령", "준장", "소장", "중장", "대장" };
        public sealed class RankPickup
        {
            public int Id;
            public Fighter Owner;
            public float X, Y;
            public int Rank;
            public string RankName => RankNames[Rank];
            public int StripeCount => Rank < 4 ? Rank + 1 : Rank == 4 ? 1 : Rank < 8 ? Rank - 4 : Rank < 11 ? Rank - 7 : Rank - 10;
        }
        public readonly List<RankPickup> RankPickups = new List<RankPickup>();
        readonly Random rankRandom;
        int nextPickup;
        static float DistanceSquared(float x1, float y1, float x2, float y2) => (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
        static bool InAura(Fighter owner, Fighter enemy) => owner.Hp > 0 && owner.Data.skill == "army_aura" && DistanceSquared(owner.X, owner.Y, enemy.X, enemy.Y) <= (AuraRadius + Radius) * (AuraRadius + Radius);
        void ApplyAuraSlow()
        {
            Player.SlowFactor = !Sandbox && InAura(Cpu, Player) ? AuraSlowFactor : 1;
            Cpu.SlowFactor = InAura(Player, Cpu) ? AuraSlowFactor : 1;
        }
        void UpdateBuiltInFighters(float dt)
        {
            UpdateArmy(Player, Cpu, dt); if (!Sandbox) UpdateArmy(Cpu, Player, dt);
            UpdateWhip(Player, Cpu, dt); if (!Sandbox) UpdateWhip(Cpu, Player, dt);
            for (int i = RankPickups.Count - 1; i >= 0; i--)
            {
                var pickup = RankPickups[i];
                bool collected = pickup.Owner.Hp > 0 && DistanceSquared(pickup.X, pickup.Y, pickup.Owner.X, pickup.Owner.Y) <= (Radius + PickupRadius) * (Radius + PickupRadius);
                if (collected)
                {
                    pickup.Owner.Rank = pickup.Rank;
                    pickup.Owner.PickupTimer = 3; pickup.Owner.Cooldown = 3;
                    pickup.Owner.Flash = .35f;
                    EmitImpact(pickup.Owner, 0, ImpactKind.Promotion);
                    LastEvent = pickup.Owner.Data.name + " · " + pickup.Owner.RankName + " 진급";
                }
                if (collected || pickup.Owner.Hp <= 0) RankPickups.RemoveAt(i);
            }
        }
        void UpdateArmy(Fighter owner, Fighter enemy, float dt)
        {
            if (owner.Hp <= 0 || owner.Data.skill != "army_aura") return;
            bool waiting = false;
            foreach (var pickup in RankPickups) if (pickup.Owner == owner) { waiting = true; break; }
            if (!waiting && owner.Rank < RankNames.Length - 1) owner.PickupTimer -= dt;
            if (!waiting && owner.PickupTimer <= 0 && owner.Rank < RankNames.Length - 1)
            {
                owner.PickupTimer = 0;
                float x = Width / 2, y = Height / 2;
                for (int attempt = 0; attempt < 16; attempt++)
                {
                    x = 64 + (float)rankRandom.NextDouble() * (Width - 128);
                    y = 64 + (float)rankRandom.NextDouble() * (Height - 128);
                    if (DistanceSquared(owner.X, owner.Y, x, y) > 120 * 120) break;
                }
                RankPickups.Add(new RankPickup { Id = ++nextPickup, Owner = owner, X = x, Y = y, Rank = owner.Rank + 1 });
            }
            owner.Cooldown = waiting || owner.Rank == RankNames.Length - 1 ? 0 : Math.Max(0, owner.PickupTimer);
            if (!InAura(owner, enemy) || enemy.Hp <= 0) { owner.AuraTimer = 0; return; }
            owner.AuraTimer += dt;
            while (owner.AuraTimer >= AuraInterval)
            {
                owner.AuraTimer -= AuraInterval;
                DealDamage(enemy, owner.Data.skill_power + owner.Rank * 2.5f, ImpactKind.Dot);
            }
        }
        void StartWhip(Fighter source, Fighter target)
        {
            source.WhipTime = WhipDuration; source.WhipHit = false;
            source.WhipStartAngle = (float)(Math.Atan2(target.Y - source.Y, target.X - source.X) * 180 / Math.PI) - 100;
            source.WhipAngle = source.WhipStartAngle;
            EmitImpact(source, 0, ImpactKind.Swing);
        }
        void UpdateWhip(Fighter source, Fighter target, float dt)
        {
            if (source.WhipTime <= 0 || source.Hp <= 0) return;
            float before = (1 - source.WhipTime / WhipDuration) * 200;
            source.WhipTime = Math.Max(0, source.WhipTime - dt);
            float after = (1 - source.WhipTime / WhipDuration) * 200;
            source.WhipAngle = source.WhipStartAngle + after;
            if (source.WhipHit || target.Hp <= 0) return;
            float dx = target.X - source.X, dy = target.Y - source.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance > WhipReach + Radius) return;
            float angle = (float)(Math.Atan2(dy, dx) * 180 / Math.PI) - source.WhipStartAngle;
            while (angle < -90) angle += 360; while (angle > 270) angle -= 360;
            float margin = (float)(Math.Asin(Math.Min(1, (Radius + 12) / Math.Max(1, distance))) * 180 / Math.PI);
            if (angle < before - margin || angle > after + margin) return;
            source.WhipHit = true;
            DealDamage(target, source.Data.skill_power, ImpactKind.Whip);
            float nx = distance > .001f ? dx / distance : 1, ny = distance > .001f ? dy / distance : 0;
            target.KnockbackX = nx * WhipKnockback; target.KnockbackY = ny * WhipKnockback;
            target.KnockbackTime = .70f; target.WallSlamArmed = true; target.WallSlamDamage = source.Data.skill_power * .5f;
            LastEvent = source.Data.name + " · 파스타 회초리";
        }
    }
}
