using System;

namespace HongBallFight
{
    public sealed partial class BattleSimulation
    {
        public sealed class Fighter
        {
            public FighterData Data { get; }
            public float Hp { get; internal set; }
            public float Shield { get; internal set; }
            public float Cooldown { get; internal set; }
            public float X { get; internal set; }
            public float Y { get; internal set; }
            public float VelocityX { get; internal set; }
            public float VelocityY { get; internal set; }
            public float Flash { get; internal set; }
            public float MoveSpeed { get; internal set; }
            public float SlowFactor { get; internal set; } = 1;
            public int Rank { get; internal set; }
            public float AuraTimer { get; internal set; }
            public float PickupTimer { get; internal set; } = 3;
            public float WhipTime { get; internal set; }
            public float WhipStartAngle { get; internal set; }
            public float WhipAngle { get; internal set; }
            public bool WhipHit { get; internal set; }
            public float KnockbackX { get; internal set; }
            public float KnockbackY { get; internal set; }
            public float KnockbackTime { get; internal set; }
            public float WallSlamDamage { get; internal set; }
            public bool WallSlamArmed { get; internal set; }
            public bool Defeated { get; internal set; }
            public string RankName => RankNames[Math.Min(Rank, RankNames.Length - 1)];
            public float ContactDamage => Data.attack + (Data.skill == "army_aura" ? Rank * 2 : 0);
            internal Fighter(FighterData data, float x, float y, float dx, float dy)
            {
                Data = data.Copy(); Hp = data.hp; Cooldown = data.cooldown; X = x; Y = y; MoveSpeed = data.speed;
                SetVelocity(dx, dy);
            }
            internal void SetVelocity(float x, float y)
            {
                float length = (float)Math.Sqrt(x * x + y * y);
                VelocityX = length > .001f ? x / length * MoveSpeed : MoveSpeed;
                VelocityY = length > .001f ? y / length * MoveSpeed : 0;
            }
        }
        public const float Width = 900, Height = 360, Radius = 31;
        public Fighter Player { get; }
        public Fighter Cpu { get; }
        public SkillWorld World { get; }
        public bool Sandbox { get; }
        public bool SandboxDummyLocked { get; private set; }
        public float Elapsed { get; private set; }
        public bool Ended { get; private set; }
        public string Result { get; private set; } = "";
        public string LastEvent { get; private set; } = "1번과 2번이 자동으로 대결합니다.";
        float contactCooldown;

        public BattleSimulation(FighterData left, FighterData right, bool sandbox = false, int seed = 73)
        {
            Sandbox = sandbox;
            rankRandom = new Random(seed);
            if (left.Validate() != null || right.Validate() != null) throw new ArgumentException("Invalid fighter");
            Player = new Fighter(left, Width * .25f, Height / 2 - 20, .88f, .47f);
            Cpu = new Fighter(right, Width * .75f, Height / 2 + 20, -.91f, -.41f);
            if (Sandbox) { Cpu.Y = Height / 2; Cpu.VelocityX = Cpu.VelocityY = 0; }
            World = new SkillWorld(this); World.Initialize(); CheckEnd();
        }
        public void Tick(float dt)
        {
            if (Ended) return;
            if (!float.IsFinite(dt) || dt <= 0 || dt > .05f) throw new ArgumentOutOfRangeException(nameof(dt));
            Elapsed += dt; contactCooldown -= dt;
            ApplyAuraSlow();
            Move(Player, dt);
            if (!Sandbox) Move(Cpu, dt);
            else
            {
                Cpu.VelocityX = Cpu.VelocityY = 0;
                if (SandboxDummyLocked) ResetDummy(); else Move(Cpu, dt);
            }
            float dx = Cpu.X - Player.X, dy = Cpu.Y - Player.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance < Radius * 2)
            {
                float nx = distance > .0001f ? dx / distance : 1;
                float ny = distance > .0001f ? dy / distance : 0;
                float overlap = Radius * 2 - distance;
                float separation = Sandbox ? 1 : .5f;
                Player.X -= nx * overlap * separation; Player.Y -= ny * overlap * separation;
                if (!Sandbox) { Cpu.X += nx * overlap * .5f; Cpu.Y += ny * overlap * .5f; }
                float relative = (Cpu.VelocityX - Player.VelocityX) * nx + (Cpu.VelocityY - Player.VelocityY) * ny;
                if (relative < 0)
                {
                    if (Sandbox)
                    {
                        float approach = Player.VelocityX * nx + Player.VelocityY * ny;
                        Player.SetVelocity(Player.VelocityX - 2 * approach * nx, Player.VelocityY - 2 * approach * ny);
                    }
                    else
                    {
                        Player.SetVelocity(Player.VelocityX + relative * nx, Player.VelocityY + relative * ny);
                        Cpu.SetVelocity(Cpu.VelocityX - relative * nx, Cpu.VelocityY - relative * ny);
                    }
                    if (contactCooldown <= 0)
                    {
                        if (!Sandbox) DealDamage(Player, Cpu.ContactDamage, ImpactKind.Contact);
                        DealDamage(Cpu, Player.ContactDamage, ImpactKind.Contact); contactCooldown = .24f;
                        World.Notify(Player, "OnCollision"); World.Notify(Cpu, "OnCollision");
                        LastEvent = $"충돌!  {Player.Data.name} -{Cpu.Data.attack}  /  {Cpu.Data.name} -{Player.Data.attack}";
                    }
                }
            }
            ClampPosition(Player); ClampPosition(Cpu);
            UpdateBuiltInFighters(dt);
            World.Step(dt);
            if (!Sandbox && Player.Hp > 0 && Cpu.Hp > 0)
            {
                UseSkill(1); if (Player.Hp > 0 && Cpu.Hp > 0) UseSkill(2);
            }
            CheckEnd();
        }
        void Move(Fighter fighter, float dt)
        {
            fighter.Cooldown = Math.Max(0, fighter.Cooldown - dt); fighter.Flash = Math.Max(0, fighter.Flash - dt);
            bool launched = fighter.KnockbackTime > 0;
            fighter.X += (launched ? fighter.KnockbackX : fighter.VelocityX * fighter.SlowFactor) * dt;
            fighter.Y += (launched ? fighter.KnockbackY : fighter.VelocityY * fighter.SlowFactor) * dt;
            bool wallX = fighter.X < Radius || fighter.X > Width - Radius;
            bool wallY = fighter.Y < Radius || fighter.Y > Height - Radius;
            if (wallX || wallY)
            {
                World.Notify(fighter, "OnWallHit");
                if (fighter.WallSlamArmed && fighter.KnockbackTime > 0)
                {
                    fighter.WallSlamArmed = false;
                    DealDamage(fighter, fighter.WallSlamDamage, ImpactKind.Wall);
                    LastEvent = fighter.Data.name + " · 벽 충돌 +" + fighter.WallSlamDamage.ToString("0");
                }
                else if (launched) EmitImpact(fighter, 0, ImpactKind.Ricochet);
                if (launched)
                {
                    if (wallX) fighter.KnockbackX = (fighter.X < Radius ? Math.Abs(fighter.KnockbackX) : -Math.Abs(fighter.KnockbackX)) * .65f;
                    if (wallY) fighter.KnockbackY = (fighter.Y < Radius ? Math.Abs(fighter.KnockbackY) : -Math.Abs(fighter.KnockbackY)) * .65f;
                }
            }
            if (fighter.X < Radius) fighter.VelocityX = Math.Abs(fighter.VelocityX);
            if (fighter.X > Width - Radius) fighter.VelocityX = -Math.Abs(fighter.VelocityX);
            if (fighter.Y < Radius) fighter.VelocityY = Math.Abs(fighter.VelocityY);
            if (fighter.Y > Height - Radius) fighter.VelocityY = -Math.Abs(fighter.VelocityY);
            ClampPosition(fighter);
            fighter.KnockbackTime = Math.Max(0, fighter.KnockbackTime - dt);
            float friction = (float)Math.Exp(-1.6f * dt);
            fighter.KnockbackX *= friction; fighter.KnockbackY *= friction;
            if (fighter.KnockbackTime <= 0)
            {
                if (launched && (fighter.KnockbackX != 0 || fighter.KnockbackY != 0)) fighter.SetVelocity(fighter.KnockbackX, fighter.KnockbackY);
                fighter.WallSlamArmed = false; fighter.KnockbackX = fighter.KnockbackY = 0;
            }
        }
        void ClampPosition(Fighter fighter)
        {
            fighter.X = Math.Clamp(fighter.X, Radius, Width - Radius);
            fighter.Y = Math.Clamp(fighter.Y, Radius, Height - Radius);
        }
        internal void DealDamage(Fighter target, float damage, ImpactKind kind = ImpactKind.Hit)
        {
            if (target.Hp <= 0 || damage <= 0 || !float.IsFinite(damage)) return;
            float before = target.Hp;
            float absorbed = Math.Min(target.Shield, damage); target.Shield -= absorbed;
            target.Hp = Math.Max(0, target.Hp - damage + absorbed); target.Flash = .15f;
            EmitImpact(target, before - target.Hp, absorbed > 0 && before == target.Hp && kind != ImpactKind.Whip ? ImpactKind.Shield : kind, absorbed);
            if (damage > absorbed) World?.Notify(target, "OnDamage", damage - absorbed);
            if (target.Hp <= 0) World?.Notify(target, "OnDeath");
        }
        void Skill(Fighter source, Fighter target)
        {
            source.Cooldown = source.Data.cooldown;
            if (source.Data.skill == "pasta_whip") StartWhip(source, target);
            else if (source.Data.skill == "script") World.UseSkill(source);
            else if (source.Data.skill == "heal") source.Hp = Math.Min(source.Data.hp, source.Hp + source.Data.skill_power);
            else if (source.Data.skill == "shield") source.Shield = source.Data.skill_power;
            else DealDamage(target, source.Data.skill_power);
            source.Flash = .3f;
            LastEvent = source.Data.name + " · " + FighterData.SkillName(source.Data.skill) + " 발동!";
        }
        public bool UseSkill(int number)
        {
            if (number != 1 && number != 2) throw new ArgumentOutOfRangeException(nameof(number));
            var source = number == 1 ? Player : Cpu; var target = number == 1 ? Cpu : Player;
            if (Ended || source.Hp <= 0 || target.Hp <= 0 || source.Cooldown > 0 || source.Data.skill == "army_aura") return false;
            Skill(source, target); CheckEnd(); return true;
        }
        public bool UsePlayerSkill() => UseSkill(1);
        public void SetDummyLocked(bool locked)
        {
            if (!Sandbox) throw new InvalidOperationException("제작 테스트장에서만 샌드백을 고정할 수 있습니다.");
            SandboxDummyLocked = locked;
            if (locked) ResetDummy();
        }
        void ResetDummy()
        {
            Cpu.X = Width * .75f; Cpu.Y = Height / 2;
            Cpu.KnockbackX = Cpu.KnockbackY = Cpu.KnockbackTime = 0; Cpu.WallSlamArmed = false;
        }
        void CheckEnd()
        {
            if (Ended) return;
            if (Sandbox) { if (Player.Hp <= 0) Player.Hp = Player.Data.hp; if (Cpu.Hp <= 0) Cpu.Hp = Cpu.Data.hp; return; }
            if (Player.Hp > 0 && Cpu.Hp > 0 && Elapsed < 180) return;
            Ended = true;
            float left = Player.Hp / Player.Data.hp, right = Cpu.Hp / Cpu.Data.hp;
            if (Math.Abs(left - right) < .00001f) { Defeat(Player); Defeat(Cpu); }
            else Defeat(left < right ? Player : Cpu);
            Result = Math.Abs(left - right) < .00001f ? "무승부" : (left > right ? Player.Data.name : Cpu.Data.name) + " 승리!";
            LastEvent = "대전이 종료되었습니다. 선택 화면에서 다시 대전할 수 있습니다.";
        }
    }
}
