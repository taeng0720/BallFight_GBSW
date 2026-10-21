using System;
using System.Linq;
using HongBallFight;

static class ImpactChecks
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Check()
    {
        var light = new ImpactFeedback(); light.Accept(new CombatImpact { Kind = ImpactKind.Hit, Damage = 5 });
        var heavy = new ImpactFeedback(); heavy.Accept(new CombatImpact { Kind = ImpactKind.Hit, Damage = 32 });
        Assert(heavy.StopRemaining > light.StopRemaining && heavy.ShakeStrength > light.ShakeStrength, "Damage did not scale shake and hitstop");
        var whip = new ImpactFeedback(); whip.Accept(new CombatImpact { Kind = ImpactKind.Whip, Damage = 1 });
        Assert(whip.StopRemaining >= .06f && whip.ShakeStrength > heavy.ShakeStrength, "Low-damage whip lost its strong impact");
        var ricochet = new ImpactFeedback(); ricochet.Accept(new CombatImpact { Kind = ImpactKind.Ricochet });
        Assert(ricochet.StopRemaining < whip.StopRemaining && ricochet.ShakeStrength < whip.ShakeStrength, "Ricochet feedback overwhelmed the initial whip");
        var dot = new ImpactFeedback(); dot.Accept(new CombatImpact { Kind = ImpactKind.Dot, Damage = 32 });
        Assert(dot.ShakeStrength < light.ShakeStrength && dot.StopRemaining < light.StopRemaining, "DOT feedback was too strong");
        var air = new ImpactFeedback(); air.Accept(new CombatImpact { Kind = ImpactKind.Swing }); Assert(!air.Stopped && air.ShakeStrength == 0, "Missed swing caused impact feedback");
        var stacked = new ImpactFeedback();
        for (int i = 0; i < 100; i++) stacked.Accept(new CombatImpact { Kind = ImpactKind.Hit, Damage = 10000 });
        Assert(stacked.StopRemaining <= .065f && stacked.ShakeStrength <= 4.8f, "Feedback accumulated without a cap");
        heavy.Advance(.02f); Assert(heavy.Stopped, "Heavy hit stop did not persist"); heavy.Advance(.5f);
        Assert(!heavy.Stopped && heavy.OffsetX == 0 && heavy.OffsetY == 0, "Realtime feedback did not recover");
        var game = new BattleSimulation(new FighterData { hp = 300 }, new FighterData { hp = 100 });
        game.DealDamage(game.Cpu, 400); game.Tick(1f / 120);
        var events = game.DrainImpacts();
        Assert(game.Ended && game.Cpu.Defeated && !game.Player.Defeated, "Loser was not marked for burst rendering");
        Assert(events.Count(e => e.Kind == ImpactKind.Defeat) == 1 && events.Single(e => e.Kind == ImpactKind.Hit).Damage == 100, "Defeat duplicated or overkill damage distorted feedback");
        game.Tick(1f / 120); Assert(game.DrainImpacts().Count == 0, "Defeat burst fired twice");
        var shielded = new BattleSimulation(new FighterData(), new FighterData()); shielded.Cpu.Shield = 50; shielded.DealDamage(shielded.Cpu, 10);
        var blocked = shielded.DrainImpacts().Single(); Assert(blocked.Kind == ImpactKind.Shield && blocked.Damage == 0 && blocked.Absorbed == 10, "Shield feedback reported HP damage");
        var timeout = new BattleSimulation(new FighterData { hp = 1000, cooldown = 20 }, new FighterData { hp = 1000, cooldown = 20 });
        timeout.Player.X = 100; timeout.Cpu.X = 700; timeout.Player.VelocityX = timeout.Player.VelocityY = timeout.Cpu.VelocityX = timeout.Cpu.VelocityY = 0;
        timeout.Cpu.Hp = 900;
        for (int i = 0; i < 120 * 181 && !timeout.Ended; i++) timeout.Tick(1f / 120);
        Assert(timeout.Cpu.Defeated && timeout.Cpu.Hp > 0 && !timeout.Player.Defeated, "Timeout loser did not receive a burst event");
        var queue = new BattleSimulation(new FighterData(), new FighterData { hp = 1000 });
        for (int i = 0; i < 200; i++) queue.DealDamage(queue.Cpu, 1);
        Assert(queue.DrainImpacts().Count == 128, "Impact event queue is unbounded");
        Console.WriteLine("PASS: damage-scaled shake/hitstop, subtle DOT, capped feedback, realtime recovery, shield feedback, lethal/timeout loser burst, one-time defeat, bounded events");
    }
}
