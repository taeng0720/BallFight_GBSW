using System;
using HongBallFight;

static class DuelChecks
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Run(BattleSimulation game, float seconds) { for (int i = 0; i < (int)(seconds * 120); i++) game.Tick(1f / 120); }
    static FighterData Dummy() => new FighterData { hp = 1000, attack = 1, cooldown = 20 };
    static void Place(BattleSimulation.Fighter fighter, float x, float y) { fighter.X = x; fighter.Y = y; fighter.VelocityX = fighter.VelocityY = 0; }
    public static void Check()
    {
        var samples = FighterData.Samples();
        Assert(samples.Count == 2 && samples[0].name == "장희철" && samples[1].name == "구본윤", "New fighters missing");
        Assert(samples[0].skill_power == 6 && samples[0].attack == 5 && samples[1].skill_power == 22 && samples[1].attack == 4, "Damage balance not applied");
        Assert(samples[0].speed == 300 && samples[1].speed == 330 && BattleSimulation.Width * BattleSimulation.Height < 1168 * 430, "Speed or smaller map not applied");
        var game = new BattleSimulation(samples[0], Dummy());
        Place(game.Player, 350, 180); Place(game.Cpu, 450, 180); game.Cpu.VelocityX = 100; game.Tick(.05f);
        Assert(Math.Abs(game.Cpu.X - 452.75f) < .001f && game.Cpu.SlowFactor == .55f, "Aura did not slow actual movement");
        game.Cpu.VelocityX = 0; float hp = game.Cpu.Hp;
        Run(game, .4f); Assert(game.Cpu.Hp == hp, "Aura DOT fired too early");
        Run(game, .1f); Assert(game.Cpu.Hp == hp - 6, "Aura damage buff was not applied");
        Place(game.Cpu, 750, 180); hp = game.Cpu.Hp; Run(game, 1);
        Assert(game.Cpu.Hp == hp && game.Cpu.SlowFactor == 1, "Aura persisted outside radius");
        game.Player.Cooldown = 0; Assert(!game.UseSkill(1), "Passive fighter used an active skill");
        game = new BattleSimulation(samples[0], Dummy(), true);
        Place(game.Player, 31, 31); Run(game, 2.9f); Assert(game.RankPickups.Count == 0, "ID spawned before 3 seconds");
        Run(game, .15f); Assert(game.RankPickups.Count == 1, "ID missing after 3 seconds");
        var pickup = game.RankPickups[0]; float x = pickup.X, y = pickup.Y; int id = pickup.Id;
        Assert(pickup.Rank == 1 && pickup.RankName == "중사" && pickup.StripeCount == 2, "Badge does not match next promotion rank");
        Run(game, 30);
        Assert(game.RankPickups.Count == 1 && game.RankPickups[0].Id == id && pickup.X == x && pickup.Y == y, "Uncollected ID moved, expired or spawned duplicates");
        Assert(BattleSimulation.PickupWidth > 42 && BattleSimulation.PickupHeight > 30, "Badge size did not increase");
        Place(game.Player, pickup.X, pickup.Y); game.Tick(1f / 120);
        Assert(game.Player.Rank == 1 && game.Player.ContactDamage == 7 && game.RankPickups.Count == 0, "Collecting ID did not promote or remove badge");
        Place(game.Player, 31, 31); Run(game, 2.9f); Assert(game.RankPickups.Count == 0, "New ID spawned before consumption cooldown");
        Run(game, .15f); Assert(game.RankPickups.Count == 1 && game.RankPickups[0].Rank == 2 && game.RankPickups[0].StripeCount == 3, "Next badge did not advance to next rank");
        Place(game.Player, game.Cpu.X - 110, game.Cpu.Y); hp = game.Cpu.Hp; Run(game, .52f);
        Assert(game.Cpu.Hp == hp - 8.5f, "Rank did not increase DOT damage");
        game.Player.Rank = 14; game.RankPickups.Clear(); game.Player.PickupTimer = 0; Place(game.Player, 31, 31); Run(game, 5);
        Assert(game.RankPickups.Count == 0 && new BattleSimulation.RankPickup { Rank = 14 }.StripeCount == 4, "Maximum rank or badge stripes are incorrect");
        game = new BattleSimulation(samples[1], Dummy()); Place(game.Player, 200, 180); Place(game.Cpu, 400, 180);
        Run(game, 1.95f); Assert(game.Player.WhipTime == 0 && game.Cpu.Hp == 1000, "Pasta attack started before cooldown");
        Run(game, .1f); Assert(game.Player.WhipTime > 0, "1번 did not automatically swing after 2 seconds");
        Run(game, .1f); Assert(game.Cpu.Hp == 978 && game.Cpu.KnockbackTime > 0, "Whip damage nerf or knockback failed");
        game = new BattleSimulation(samples[1], Dummy()); Place(game.Player, 580, 180); Place(game.Cpu, 760, 180); game.Player.Cooldown = 0;
        game.UseSkill(1); Run(game, .23f);
        Assert(game.Cpu.Hp == 967 && !game.Cpu.WallSlamArmed, "Wall slam did not add exactly 11 damage");
        hp = game.Cpu.Hp; Place(game.Cpu, BattleSimulation.Width - BattleSimulation.Radius + 1, 180); game.Tick(1f / 120);
        Assert(game.Cpu.Hp == hp, "Ordinary wall collision applied bonus damage");
        game = new BattleSimulation(samples[1], Dummy()); Place(game.Player, 100, 180); Place(game.Cpu, 700, 180); game.Player.Cooldown = 0; game.UseSkill(1); Run(game, .6f);
        Assert(game.Cpu.Hp == 1000 && !game.Player.WhipHit, "Pasta attack hit outside its range");
        game = new BattleSimulation(Dummy(), samples[1]); Place(game.Player, 400, 180); Place(game.Cpu, 600, 180); Run(game, 2.15f);
        Assert(game.Player.Hp == 978 && game.Player.KnockbackX < -2400, "2번 did not use the same automatic attack");
        KnockbackChecks.Check();
        Console.WriteLine("PASS: smaller faster duel, damage rebalance, aura, stationary persistent single badge, collection cooldown, matching rank stripes, promotion scaling, automatic swings, knockback and wall bonus");
    }
}
