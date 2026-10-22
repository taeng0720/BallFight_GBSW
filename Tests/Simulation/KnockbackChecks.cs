using System;
using System.Linq;
using HongBallFight;

static class KnockbackChecks
{
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Step(BattleSimulation game, float seconds) { for (int i = 0; i < (int)(seconds * 120); i++) game.Tick(1f / 120); }
    public static void Check()
    {
        var game = new BattleSimulation(FighterData.Samples()[1], new FighterData { hp = 1000, cooldown = 20 }, true);
        game.Player.X = 480; game.Player.Y = game.Cpu.Y;
        game.Player.VelocityX = game.Player.VelocityY = 0; game.Player.Cooldown = 0;
        game.UseSkill(1);
        for (int i = 0; i < 36 && !game.Player.WhipHit; i++) game.Tick(1f / 120);
        Assert(game.Player.WhipHit && game.Cpu.Hp == 978 && game.Cpu.KnockbackX == 2600, "Sandbox dummy was not launched at full speed");
        float x = game.Cpu.X; Step(game, .025f);
        Assert(game.Cpu.X - x > 60, "Sandbox position reset suppressed knockback");
        for (int i = 0; i < 60 && game.Cpu.WallSlamArmed; i++) game.Tick(1f / 120);
        Assert(game.Cpu.Hp == 967 && game.Cpu.KnockbackX < -1300 && game.Cpu.KnockbackTime > 0, "Wall stopped flight or lost bonus damage");
        Assert(game.DrainImpacts().Count(e => e.Kind == ImpactKind.Wall) == 1, "Wall bonus fired more than once");
        game.Player.X = 31; game.Player.Y = 31; Step(game, .8f);
        Assert(game.Cpu.Hp == 967 && game.Cpu.KnockbackTime == 0, "Rebound repeated damage or never ended");
        game.SetDummyLocked(true); x = game.Cpu.X;
        game.Cpu.KnockbackX = 2600; game.Cpu.KnockbackTime = .7f; Step(game, .05f);
        Assert(game.SandboxDummyLocked && game.Cpu.X == x && game.Cpu.KnockbackTime == 0, "Explicit dummy lock failed");
        game.SetDummyLocked(false); game.Cpu.KnockbackX = -2600; game.Cpu.KnockbackTime = .7f; Step(game, .025f);
        Assert(game.Cpu.X < x - 60, "Unlocking dummy did not restore flight");

        game = new BattleSimulation(FighterData.Samples()[0], new FighterData { hp = 1000, cooldown = 20 });
        game.Player.X = 300; game.Player.Y = game.Cpu.Y = 180; game.Cpu.X = 400;
        game.Player.VelocityX = game.Player.VelocityY = 0;
        game.Cpu.VelocityX = -500; game.Cpu.VelocityY = 0; game.Cpu.KnockbackX = 2600; game.Cpu.KnockbackTime = .7f;
        game.Tick(1f / 120);
        Assert(game.Cpu.SlowFactor == .55f && Math.Abs(game.Cpu.X - (400 + 2600f / 120)) < .001f, "Aura or opposing movement weakened launch speed");

        game = new BattleSimulation(FighterData.Samples()[1], new FighterData { hp = 1000, cooldown = 20 }, true);
        game.Player.X = 480; game.Player.Y = game.Cpu.Y; game.Player.VelocityX = game.Player.VelocityY = 0;
        game.Cpu.Shield = 100; game.Player.Cooldown = 0; game.UseSkill(1);
        for (int i = 0; i < 36 && !game.Player.WhipHit; i++) game.Tick(1f / 120);
        Assert(game.Cpu.Hp == 1000 && game.Cpu.KnockbackX == 2600 && game.DrainImpacts().Any(e => e.Kind == ImpactKind.Whip && e.Absorbed == 22), "Shield erased whip impact or launch");
        game = new BattleSimulation(FighterData.Samples()[0], new FighterData { hp = 1000, cooldown = 20 });
        game.Player.X = 800; game.Player.Y = 300; game.Player.VelocityX = game.Player.VelocityY = 0;
        game.Cpu.X = 300; game.Cpu.Y = 180; game.Cpu.SetVelocity(-1, 0);
        game.Cpu.KnockbackX = 600; game.Cpu.KnockbackY = 0; game.Cpu.KnockbackTime = .1f;
        Step(game, .2f);
        Assert(game.Cpu.KnockbackTime == 0 && game.Cpu.VelocityX > 0 && Math.Abs(game.Cpu.VelocityY) < .001f, "Fighter returned to its old direction after knockback");
        Console.WriteLine("PASS: high-speed dummy launch, sustained wall rebound, one-time slam, dummy lock toggle, aura-independent flight, shielded whip feedback and keep-launch-direction");
    }
}
