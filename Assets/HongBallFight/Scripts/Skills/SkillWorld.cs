using System;
using System.Collections.Generic;
using System.Linq;

namespace HongBallFight
{
    public sealed class SkillWorld
    {
        public sealed class Entity
        {
            public int Id;
            public BattleSimulation.Fighter Owner;
            public string Image;
            public float X, Y, VelocityX, VelocityY, Size = 24, Life = 5, Damage, Homing, Rotation, Spin;
            public float R = 1, G = 1, B = 1, A = 1, HitDelay;
            public bool Orbital, Dead;
            public float OrbitRadius, OrbitAngle, OrbitSpeed;
            public int Bounce, Piercing;
        }
        public sealed class PixelImage
        {
            public string Name;
            public int Width, Height, Revision;
            public byte[] Pixels;
        }
        public sealed class ScriptState : ISkillHost
        {
            readonly SkillWorld world;
            internal readonly BattleSimulation.Fighter Self;
            internal readonly BattleSimulation.Fighter Enemy;
            internal SkillScript.Runtime Runtime;
            readonly Dictionary<string, float> timers = new Dictionary<string, float>();
            internal readonly Dictionary<string, PixelImage> Images = new Dictionary<string, PixelImage>();
            public string Error { get; private set; } = "";
            public string Sprite { get; private set; } = "";
            internal float HitDamage, HitProjectile;
            internal int Spawns, PixelWrites;
            internal ScriptState(SkillWorld world, BattleSimulation.Fighter self, BattleSimulation.Fighter enemy) { this.world = world; Self = self; Enemy = enemy; }
            internal void Initialize()
            {
                if (Self.Data.skill != "script") return;
                try { Runtime = SkillScript.Compile(Self.Data.script).CreateRuntime(this); Invoke("Start"); }
                catch (Exception error) { Fail(error.Message); }
            }
            internal void Fail(string message) { Error = message; Runtime = null; world.Log(Self.Data.name + ": " + message); }
            internal void Invoke(string name)
            {
                if (Runtime == null || Self.Hp <= 0 && name != "OnDeath") return;
                try { Runtime.Invoke(name); } catch (Exception error) { Fail(error.Message); }
            }
            public object Read(string name)
            {
                switch (name)
                {
                    case "Time.deltaTime": return (double)world.deltaTime;
                    case "Time.time": return (double)world.simulation.Elapsed;
                    case "Self.HP": return (double)Self.Hp; case "Self.MaxHP": return (double)Self.Data.hp;
                    case "Self.X": return (double)Self.X; case "Self.Y": return (double)Self.Y;
                    case "Self.Speed": return (double)Self.MoveSpeed; case "Self.Attack": return (double)Self.Data.attack;
                    case "Self.SkillPower": return (double)Self.Data.skill_power; case "Self.Shield": return (double)Self.Shield;
                    case "Self.Cooldown": return (double)Self.Cooldown;
                    case "Enemy.HP": return (double)Enemy.Hp; case "Enemy.MaxHP": return (double)Enemy.Data.hp;
                    case "Enemy.X": return (double)Enemy.X; case "Enemy.Y": return (double)Enemy.Y;
                    case "Arena.Width": return (double)BattleSimulation.Width; case "Arena.Height": return (double)BattleSimulation.Height;
                    case "Mathf.PI": return Math.PI; case "Hit.Damage": return (double)HitDamage; case "Hit.Projectile": return (double)HitProjectile;
                    default: throw new Exception("선언되지 않은 변수 또는 속성: " + name);
                }
            }
            float N(object[] args, int i, float min = -100000, float max = 100000) => Math.Clamp((float)SkillScript.Number(args[i]), min, max);
            string S(object[] args, int i)
            {
                string value = Convert.ToString(args[i], System.Globalization.CultureInfo.InvariantCulture);
                if (value.Length > 256) throw new Exception("문자열 인자는 256자 이하여야 합니다.");
                return value;
            }
            Entity Find(object[] args, bool includeDead = false)
            {
                int id = (int)N(args, 0, 0, int.MaxValue);
                return world.Entities.FirstOrDefault(e => e.Id == id && e.Owner == Self && (!e.Dead || includeDead));
            }
            float Aim() => (float)(Math.Atan2(Enemy.Y - Self.Y, Enemy.X - Self.X) * 180 / Math.PI);
            Entity Spawn(string image, float x, float y, float size, float life)
            {
                if (Spawns++ >= 64 || world.Entities.Count(e => e.Owner == Self && !e.Dead) >= 128) return null;
                var entity = new Entity { Id = ++world.nextId, Owner = Self, Image = image, X = x, Y = y, Size = size, Life = life };
                world.Entities.Add(entity); return entity;
            }
            Entity Fire(string image, float angle, float speed, float damage)
            {
                double radians = angle * Math.PI / 180;
                var entity = Spawn(image, Self.X + (float)Math.Cos(radians) * 38, Self.Y + (float)Math.Sin(radians) * 38, 24, 6);
                if (entity != null) { entity.VelocityX = (float)Math.Cos(radians) * speed; entity.VelocityY = (float)Math.Sin(radians) * speed; entity.Damage = damage; entity.Rotation = angle; }
                return entity;
            }
            PixelImage Image(string name)
            {
                if (!Images.TryGetValue(name, out var image)) throw new Exception("CreateImage로 먼저 생성하세요: " + name);
                return image;
            }
            void Pixel(PixelImage image, int x, int y, float r, float g, float b, float a)
            {
                if (++PixelWrites > 32768) throw new Exception("한 프레임의 이미지 쓰기 한도 초과");
                if (x < 0 || y < 0 || x >= image.Width || y >= image.Height) return;
                int offset = (y * image.Width + x) * 4;
                image.Pixels[offset] = (byte)(r * 255); image.Pixels[offset + 1] = (byte)(g * 255); image.Pixels[offset + 2] = (byte)(b * 255); image.Pixels[offset + 3] = (byte)(a * 255);
            }
            public object Call(string name, object[] args)
            {
                var api = SkillApi.Find(name);
                if (api == null || args.Length != api.Arguments) throw new Exception("잘못된 API 호출: " + name);
                switch (name)
                {
                    case "Fire": return (double)(Fire(S(args, 0), Aim(), N(args, 1, 0, 1000), N(args, 2, 0, 150))?.Id ?? 0);
                    case "FireAngle": return (double)(Fire(S(args, 0), N(args, 1), N(args, 2, 0, 1000), N(args, 3, 0, 150))?.Id ?? 0);
                    case "FireSpread": case "FireRing":
                    {
                        int count = (int)N(args, 1, 1, 32), id = 0;
                        float spread = name == "FireRing" ? 360 : N(args, 2, 0, 360);
                        float speed = N(args, name == "FireRing" ? 2 : 3, 0, 1000), damage = N(args, name == "FireRing" ? 3 : 4, 0, 150);
                        for (int i = 0; i < count; i++)
                        {
                            float angle = name == "FireRing" ? i * 360f / count : Aim() + (count == 1 ? 0 : -spread / 2 + spread * i / (count - 1));
                            id = Fire(S(args, 0), angle, speed, damage)?.Id ?? 0;
                        }
                        return (double)id;
                    }
                    case "Spawn": return (double)(Spawn(S(args, 0), N(args, 1, 0, BattleSimulation.Width), N(args, 2, 0, BattleSimulation.Height), N(args, 3, 4, 160), N(args, 4, .05f, 30))?.Id ?? 0);
                    case "Orbit":
                    {
                        int count = (int)N(args, 1, 1, 32), id = 0;
                        for (int i = 0; i < count; i++)
                        {
                            var entity = Spawn(S(args, 0), Self.X, Self.Y, 24, N(args, 5, .05f, 30));
                            if (entity == null) continue;
                            entity.Orbital = true; entity.OrbitRadius = N(args, 2, 35, 200); entity.OrbitAngle = i * 360f / count;
                            entity.OrbitSpeed = N(args, 3, -720, 720); entity.Damage = N(args, 4, 0, 150); id = entity.Id;
                            PlaceOrbital(entity); entity.Piercing = 100;
                        }
                        return (double)id;
                    }
                    case "LaunchOrbitals":
                        foreach (var entity in world.Entities.Where(e => e.Owner == Self && e.Orbital && !e.Dead))
                        {
                            double angle = Math.Atan2(Enemy.Y - entity.Y, Enemy.X - entity.X);
                            entity.Orbital = false; entity.Piercing = 0;
                            entity.VelocityX = (float)Math.Cos(angle) * N(args, 0, 0, 1000); entity.VelocityY = (float)Math.Sin(angle) * N(args, 0, 0, 1000);
                        }
                        return 0d;
                    case "DestroyAll": foreach (var entity in world.Entities.Where(e => e.Owner == Self)) entity.Dead = true; return 0d;
                    case "GetCount": return (double)world.Entities.Count(e => e.Owner == Self && !e.Dead);
                    case "Exists": return Find(args) != null;
                    case "Destroy": { var entity = Find(args); if (entity != null) entity.Dead = true; return 0d; }
                    case "SetVelocity": { var e = Find(args); if (e != null) { e.Orbital = false; e.VelocityX = N(args, 1, -1000, 1000); e.VelocityY = N(args, 2, -1000, 1000); } return 0d; }
                    case "SetPosition": { var e = Find(args); if (e != null) { e.X = N(args, 1, 0, BattleSimulation.Width); e.Y = N(args, 2, 0, BattleSimulation.Height); } return 0d; }
                    case "SetSize": { var e = Find(args); if (e != null) e.Size = N(args, 1, 4, 160); return 0d; }
                    case "SetDamage": { var e = Find(args); if (e != null) e.Damage = N(args, 1, 0, 150); return 0d; }
                    case "SetLifetime": { var e = Find(args); if (e != null) e.Life = N(args, 1, .05f, 30); return 0d; }
                    case "SetHoming": { var e = Find(args); if (e != null) e.Homing = N(args, 1, 0, 10); return 0d; }
                    case "SetBounce": { var e = Find(args); if (e != null) e.Bounce = (int)N(args, 1, 0, 20); return 0d; }
                    case "SetPiercing": { var e = Find(args); if (e != null) e.Piercing = (int)N(args, 1, 0, 20); return 0d; }
                    case "SetRotation": { var e = Find(args); if (e != null) e.Rotation = N(args, 1); return 0d; }
                    case "SetSpin": { var e = Find(args); if (e != null) e.Spin = N(args, 1, -1440, 1440); return 0d; }
                    case "SetTint": { var e = Find(args); if (e != null) { e.R = N(args, 1, 0, 1); e.G = N(args, 2, 0, 1); e.B = N(args, 3, 0, 1); e.A = N(args, 4, 0, 1); } return 0d; }
                    case "SetImage": { var e = Find(args); if (e != null) e.Image = S(args, 1); return 0d; }
                    case "GetX": return (double)(Find(args, true)?.X ?? 0); case "GetY": return (double)(Find(args, true)?.Y ?? 0);
                    case "AimAngle": return (double)Aim();
                    case "EnemyDistance": return Math.Sqrt(Math.Pow(Enemy.X - Self.X, 2) + Math.Pow(Enemy.Y - Self.Y, 2));
                    case "Explode":
                    {
                        float x = N(args, 0, 0, BattleSimulation.Width), y = N(args, 1, 0, BattleSimulation.Height), radius = N(args, 2, 1, 250);
                        if (Math.Pow(Enemy.X - x, 2) + Math.Pow(Enemy.Y - y, 2) <= Math.Pow(radius + BattleSimulation.Radius, 2)) world.simulation.DealDamage(Enemy, N(args, 3, 0, 150));
                        var e = Spawn("orb", x, y, radius * 2, .2f); if (e != null) { e.R = 1; e.G = .4f; e.B = .1f; e.A = .25f; }
                        return 0d;
                    }
                    case "Damage": world.simulation.DealDamage(Enemy, N(args, 0, 0, 150)); return 0d;
                    case "Heal": Self.Hp = Math.Min(Self.Data.hp, Self.Hp + N(args, 0, 0, 150)); return 0d;
                    case "Shield": Self.Shield = N(args, 0, 0, 150); return 0d;
                    case "Dash": Self.MoveSpeed = N(args, 0, 100, 700); Self.SetVelocity(Enemy.X - Self.X, Enemy.Y - Self.Y); return 0d;
                    case "SetSpeed": Self.MoveSpeed = N(args, 0, 100, 700); Self.SetVelocity(Self.VelocityX, Self.VelocityY); return 0d;
                    case "SetDirection": { double angle = N(args, 0) * Math.PI / 180; Self.SetVelocity((float)Math.Cos(angle), (float)Math.Sin(angle)); return 0d; }
                    case "Teleport": Self.X = N(args, 0, BattleSimulation.Radius, BattleSimulation.Width - BattleSimulation.Radius); Self.Y = N(args, 1, BattleSimulation.Radius, BattleSimulation.Height - BattleSimulation.Radius); return 0d;
                    case "SetSprite": Sprite = S(args, 0); return 0d;
                    case "Every":
                    {
                        string key = S(args, 0); float period = N(args, 1, .05f, 180);
                        if (!timers.TryGetValue(key, out var next)) { if (timers.Count >= 64) throw new Exception("타이머 수 초과"); timers[key] = world.simulation.Elapsed + period; return false; }
                        if (world.simulation.Elapsed < next) return false;
                        timers[key] = world.simulation.Elapsed + period; return true;
                    }
                    case "CreateImage":
                    {
                        string key = S(args, 0); if (Images.ContainsKey(key)) return 0d;
                        if (Images.Count >= 16) throw new Exception("생성 이미지 한도는 16개입니다.");
                        int w = (int)N(args, 1, 1, 128), h = (int)N(args, 2, 1, 128);
                        var image = new PixelImage { Name = key, Width = w, Height = h, Pixels = new byte[w * h * 4], Revision = 1 };
                        Images[key] = image;
                        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) Pixel(image, x, y, N(args, 3, 0, 1), N(args, 4, 0, 1), N(args, 5, 0, 1), N(args, 6, 0, 1));
                        return 0d;
                    }
                    case "SetPixel":
                    {
                        var image = Image(S(args, 0)); Pixel(image, (int)N(args, 1), (int)N(args, 2), N(args, 3, 0, 1), N(args, 4, 0, 1), N(args, 5, 0, 1), N(args, 6, 0, 1)); image.Revision++; return 0d;
                    }
                    case "DrawCircle": case "DrawRect":
                    {
                        var image = Image(S(args, 0)); float cx = N(args, 1), cy = N(args, 2), w = N(args, 3, 0, 256), h = name == "DrawRect" ? N(args, 4, 0, 256) : w;
                        int color = name == "DrawRect" ? 5 : 4;
                        for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
                        {
                            bool inside = name == "DrawRect" ? x >= cx && x < cx + w && y >= cy && y < cy + h : (x - cx) * (x - cx) + (y - cy) * (y - cy) <= w * w;
                            if (inside) Pixel(image, x, y, N(args, color, 0, 1), N(args, color + 1, 0, 1), N(args, color + 2, 0, 1), N(args, color + 3, 0, 1));
                        }
                        image.Revision++; return 0d;
                    }
                    case "Random.Range": return N(args, 0) + world.random.NextDouble() * (N(args, 1) - N(args, 0));
                    case "Mathf.Sin": return Math.Sin(N(args, 0)); case "Mathf.Cos": return Math.Cos(N(args, 0));
                    case "Mathf.Abs": return (double)Math.Abs(N(args, 0)); case "Mathf.Sqrt": return Math.Sqrt(Math.Max(0, N(args, 0)));
                    case "Mathf.Min": return (double)Math.Min(N(args, 0), N(args, 1)); case "Mathf.Max": return (double)Math.Max(N(args, 0), N(args, 1));
                    case "Mathf.Clamp": return (double)Math.Clamp(N(args, 0), N(args, 1), N(args, 2));
                    case "Mathf.Lerp": return (double)(N(args, 0) + (N(args, 1) - N(args, 0)) * N(args, 2, 0, 1));
                    case "Mathf.Floor": return Math.Floor(N(args, 0)); case "Mathf.Ceil": return Math.Ceiling(N(args, 0)); case "Mathf.Pow": return Math.Pow(N(args, 0, -1000, 1000), N(args, 1, -20, 20));
                    case "Distance": return Math.Sqrt(Math.Pow(N(args, 2) - N(args, 0), 2) + Math.Pow(N(args, 3) - N(args, 1), 2));
                    case "Log": world.Log(Self.Data.name + ": " + S(args, 0)); return 0d;
                    default: throw new Exception("구현되지 않은 API: " + name);
                }
            }
        }
        readonly BattleSimulation simulation;
        readonly Random random = new Random(41);
        readonly Queue<(ScriptState state, string name, float damage, int projectile)> events = new Queue<(ScriptState, string, float, int)>();
        int nextId;
        float deltaTime;
        public readonly List<Entity> Entities = new List<Entity>();
        public readonly List<string> Logs = new List<string>();
        public ScriptState Player { get; }
        public ScriptState Cpu { get; }
        public SkillWorld(BattleSimulation simulation)
        {
            this.simulation = simulation;
            Player = new ScriptState(this, simulation.Player, simulation.Cpu); Cpu = new ScriptState(this, simulation.Cpu, simulation.Player);
        }
        public void Initialize() { Player.Initialize(); Cpu.Initialize(); DrainEvents(); }
        public ScriptState State(BattleSimulation.Fighter fighter) => fighter == simulation.Player ? Player : Cpu;
        public void Log(string value) { Logs.Add(value.Length > 512 ? value.Substring(0, 512) : value); if (Logs.Count > 30) Logs.RemoveAt(0); }
        public void Notify(BattleSimulation.Fighter fighter, string name, float damage = 0, int projectile = 0)
        {
            if (events.Count < 128) events.Enqueue((State(fighter), name, damage, projectile));
        }
        void DrainEvents()
        {
            int budget = 32;
            while (events.Count > 0)
            {
                var item = events.Dequeue();
                if (--budget < 0) { item.state.Fail("이벤트 연쇄 호출 한도 초과"); events.Clear(); break; }
                item.state.HitDamage = item.damage; item.state.HitProjectile = item.projectile; item.state.Invoke(item.name);
            }
        }
        public void UseSkill(BattleSimulation.Fighter fighter) { State(fighter).Invoke("OnSkill"); DrainEvents(); }
        internal static void PlaceOrbital(Entity entity)
        {
            double angle = entity.OrbitAngle * Math.PI / 180;
            entity.X = entity.Owner.X + (float)Math.Cos(angle) * entity.OrbitRadius; entity.Y = entity.Owner.Y + (float)Math.Sin(angle) * entity.OrbitRadius;
        }
        public void Step(float dt)
        {
            deltaTime = dt;
            foreach (var state in new[] { Player, Cpu }) { state.Spawns = 0; state.PixelWrites = 0; state.Invoke("Update"); state.Invoke("FixedUpdate"); }
            int count = Entities.Count;
            for (int i = 0; i < count; i++)
            {
                var e = Entities[i]; if (e.Dead) continue;
                e.Life -= dt; e.HitDelay -= dt; e.Rotation += e.Spin * dt;
                if (e.Life <= 0) { e.Dead = true; continue; }
                float oldX = e.X, oldY = e.Y;
                var enemy = e.Owner == simulation.Player ? simulation.Cpu : simulation.Player;
                if (e.Orbital) { e.OrbitAngle += e.OrbitSpeed * dt; PlaceOrbital(e); }
                else
                {
                    if (e.Homing > 0)
                    {
                        double aim = Math.Atan2(enemy.Y - e.Y, enemy.X - e.X);
                        float speed = (float)Math.Sqrt(e.VelocityX * e.VelocityX + e.VelocityY * e.VelocityY), t = Math.Min(1, dt * e.Homing);
                        e.VelocityX += ((float)Math.Cos(aim) * speed - e.VelocityX) * t; e.VelocityY += ((float)Math.Sin(aim) * speed - e.VelocityY) * t;
                    }
                    e.X += e.VelocityX * dt; e.Y += e.VelocityY * dt;
                    if (e.X < 0 || e.X > BattleSimulation.Width || e.Y < 0 || e.Y > BattleSimulation.Height)
                    {
                        if (e.Bounce-- <= 0) { e.Dead = true; continue; }
                        if (e.X < 0 || e.X > BattleSimulation.Width) e.VelocityX *= -1;
                        if (e.Y < 0 || e.Y > BattleSimulation.Height) e.VelocityY *= -1;
                        e.X = Math.Clamp(e.X, 0, BattleSimulation.Width); e.Y = Math.Clamp(e.Y, 0, BattleSimulation.Height);
                    }
                }
                float dx = e.X - oldX, dy = e.Y - oldY, length = dx * dx + dy * dy;
                float along = length > .001f ? Math.Clamp(((enemy.X - oldX) * dx + (enemy.Y - oldY) * dy) / length, 0, 1) : 0;
                float hitX = oldX + along * dx - enemy.X, hitY = oldY + along * dy - enemy.Y;
                if (e.Damage > 0 && e.HitDelay <= 0 && enemy.Hp > 0 && hitX * hitX + hitY * hitY <= Math.Pow(BattleSimulation.Radius + e.Size / 2, 2))
                {
                    simulation.DealDamage(enemy, e.Damage); Notify(e.Owner, "OnProjectileHit", e.Damage, e.Id);
                    e.HitDelay = .35f; if (e.Piercing-- <= 0) e.Dead = true;
                }
            }
            DrainEvents(); Entities.RemoveAll(e => e.Dead);
        }
    }
}
