using System;
using HongBallFight;

static class SimulationCheck
{
    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static void Main()
    {
        BlockChecks.Check();
        DuelChecks.Check();
        ImpactChecks.Check();
        foreach (var left in FighterData.Samples()) foreach (var right in FighterData.Samples())
        {
            var battle = new BattleSimulation(left, right);
            Assert(!battle.UsePlayerSkill(), "Skill used before cooldown");
            for (int i = 0; i < 120 * 181 && !battle.Ended; i++)
            {
                battle.Tick(1f / 120);
                battle.UsePlayerSkill();
                foreach (var fighter in new[] { battle.Player, battle.Cpu })
                {
                    Assert(float.IsFinite(fighter.X) && float.IsFinite(fighter.Y), "Nonfinite position");
                    Assert(fighter.X >= BattleSimulation.Radius && fighter.X <= BattleSimulation.Width - BattleSimulation.Radius && fighter.Y >= BattleSimulation.Radius && fighter.Y <= BattleSimulation.Height - BattleSimulation.Radius, "Ball escaped arena");
                    Assert(fighter.Hp >= 0 && fighter.Hp <= fighter.Data.hp, "HP outside bounds");
                    Assert(fighter.Shield >= 0 && fighter.Shield <= fighter.Data.skill_power, "Shield stacked");
                }
            }
            Assert(battle.Ended && battle.Result.Length > 0, "Battle did not resolve");
            float finalHp = battle.Player.Hp, finalTime = battle.Elapsed;
            Assert(!battle.UsePlayerSkill(), "Skill used after battle");
            battle.Tick(1f / 120);
            Assert(battle.Player.Hp == finalHp && battle.Elapsed == finalTime, "Finished battle advanced");
        }
        var source = FighterData.Samples()[0];
        int startingHp = source.hp;
        var snapshot = new BattleSimulation(source, source);
        source.hp = 1;
        Assert(snapshot.Player.Data.hp == startingHp, "Live battle changed with catalog");
        Assert(new FighterData { speed = float.NaN }.Validate() != null, "NaN accepted");
        Assert(new FighterData { cooldown = float.PositiveInfinity }.Validate() != null, "Infinite cooldown accepted");
        Assert(new FighterData { skill = "unregistered" }.Validate() != null, "Unknown skill accepted");
        Assert(new FighterData { color = "red" }.Validate() != null, "Invalid color accepted");
        var shield = new BattleSimulation(new FighterData { skill = "shield", skill_power = 30 }, new FighterData { skill = "burst", skill_power = 24 });
        shield.Player.Cooldown = 0;
        Assert(shield.UsePlayerSkill() && shield.Player.Shield == 30, "Shield did not activate");
        shield.Cpu.Cooldown = 0;
        float before = shield.Player.Hp;
        shield.Tick(1f / 120);
        Assert(shield.Player.Hp == before && shield.Player.Shield == 6, "Shield did not absorb damage");
        var heal = new BattleSimulation(new FighterData { skill = "heal", hp = 150 }, new FighterData());
        heal.Player.Cooldown = 0; heal.Player.Hp = 145;
        heal.UsePlayerSkill();
        Assert(heal.Player.Hp == 150, "Heal exceeded max HP");
        Console.WriteLine("PASS: four seeded matchups, arena bounds, automatic skills, HP limits, shield absorption, healing, match completion, match snapshot, input validation");
    }
}
