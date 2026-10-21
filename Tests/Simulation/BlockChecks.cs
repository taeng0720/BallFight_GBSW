using System;
using System.Collections.Generic;
using System.Linq;
using HongBallFight;

static class BlockChecks
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static FighterData Fighter(BlockDocument document) => new FighterData { name = "블록 테스트", skill = "script", blocks_json = document.ToJson() };
    static void Run(BattleSimulation simulation, float seconds)
    {
        for (int i = 0; i < 120 * seconds && !simulation.Ended; i++) simulation.Tick(1f / 120);
    }
    public static void Check()
    {
        for (int i = 0; i < 5; i++)
        {
            var document = BlockCatalog.Template(i); var data = Fighter(document);
            Assert(data.Validate() == null, "Template validation failed: " + data.Validate());
            Assert(BlockDocument.Load(document.ToJson()).Generate() == document.Generate(), "Block serialization changed behavior");
            var match = new BattleSimulation(data, FighterData.Samples()[0], true);
            Run(match, 1); match.Player.Cooldown = 0; match.UsePlayerSkill(); Run(match, 2);
            Assert(match.World.Player.Error.Length == 0, "Template execution failed: " + match.World.Player.Error);
            Assert(match.World.Entities.Count <= 256, "Object budget exceeded");
        }
        foreach (var api in SkillApi.All.Where(a => !BlockCatalog.ValueFunctions.Contains(a.Name)))
        {
            var all = new BlockDocument();
            all.Events["Start"].Add(BlockCatalog.Create("CreateImage")); all.Events["Start"].Add(BlockCatalog.Create("Fire"));
            all.Events["Start"].Add(BlockCatalog.Create(api.Name));
            var data = Fighter(all); Assert(data.Validate() == null, "Action block compile failed: " + api.Name);
            var battle = new BattleSimulation(data, FighterData.Samples()[0], true); Run(battle, .02f);
            Assert(battle.World.Player.Error.Length == 0, api.Name + ": " + battle.World.Player.Error);
        }
        var orbit = new BattleSimulation(Fighter(BlockCatalog.Template(0)), FighterData.Samples()[0], true);
        Assert(orbit.World.Entities.Count == 6 && orbit.World.Entities.All(e => e.Orbital), "Orbit did not initialize");
        orbit.Player.Cooldown = 0; orbit.UsePlayerSkill();
        Assert(orbit.World.Entities.Count == 12 && orbit.World.Entities.Count(e => !e.Orbital) == 6, "Orbit launch failed");
        var doc = new BlockDocument();
        var loop = BlockCatalog.Create("repeat"); loop.Values[0] = BlockValue.Number("3"); loop.Children.Add(BlockCatalog.Create("Fire")); doc.Events["OnSkill"].Add(loop);
        var conditional = BlockCatalog.Create("if"); conditional.Values[0] = BlockValue.Reference("Self.HP"); conditional.Values[1] = BlockValue.Number("1000"); conditional.Children.Add(BlockCatalog.Create("Shield")); conditional.Else.Add(BlockCatalog.Create("Heal")); doc.Events["Start"].Add(conditional);
        var sim = new BattleSimulation(Fighter(doc), FighterData.Samples()[0], true);
        Assert(sim.Player.Shield == 0 && sim.World.Player.Error.Length == 0, "Else branch failed");
        sim.Player.Cooldown = 0; sim.UsePlayerSkill(); Assert(sim.World.Entities.Count == 3, "Repeat failed");
        var timer = new BlockDocument(); var every = BlockCatalog.Create("every"); every.Values[0] = BlockValue.Number("0.1"); every.Children.Add(BlockCatalog.Create("Fire")); timer.Events["Update"].Add(every);
        sim = new BattleSimulation(Fighter(timer), FighterData.Samples()[0], true); Run(sim, .5f);
        Assert(sim.World.Entities.Count >= 3 && sim.World.Entities.Count < 10, "Periodic block failed");
        var images = BlockCatalog.Template(2); var circle = BlockCatalog.Create("DrawCircle"); circle.Values[0] = BlockValue.TextValue("custom"); circle.Values[3] = BlockValue.Number("10"); images.Events["Start"].Add(circle);
        sim = new BattleSimulation(Fighter(images), FighterData.Samples()[0], true);
        Assert(sim.World.Player.Images.ContainsKey("custom") && sim.World.Player.Images["custom"].Revision > 1, "Procedural image did not render");
        var move = new BlockDocument(); var parent = BlockCatalog.Create("repeat"); var child = BlockCatalog.Create("Fire"); parent.Children.Add(child); move.Events["Start"].Add(parent);
        Assert(!move.Move(parent, parent.Children, 0, parent), "Allowed cyclic block drop");
        Assert(move.Move(child, move.Events["OnSkill"], 0, null) && parent.Children.Count == 0, "Block move failed");
        child.Values[0] = BlockValue.TextValue("orb\"); Damage(999);\n");
        Assert(Fighter(move).Validate() == null, "Escaped image names failed validation");
        var math = new BlockValue { Kind = "math", Text = "+", Inputs = new List<BlockValue> { BlockValue.Reference("Self.X"), BlockValue.Number("10") } };
        Assert(math.Emit() == "(Self.X+10)", "Expression socket compilation failed");
        var malformed = Fighter(new BlockDocument()); malformed.blocks_json = "{bad";
        Assert(malformed.Validate() != null, "Invalid block JSON accepted");
        Assert(SkillScript.Validate("void Update() { Unknown(); }") != null, "Unknown function accepted");
        Assert(SkillScript.Validate("void Update() { Fire(1); }") != null, "Wrong arity accepted");
        var host = new NullHost();
        bool rejected = false;
        try { SkillScript.Compile("void Update() { while (true) { } }").CreateRuntime(host).Invoke("Update"); } catch (ScriptError) { rejected = true; }
        Assert(rejected, "Infinite loop was not stopped");
        rejected = false;
        try { SkillScript.Compile("void Loop() { Loop(); } void Start() { Loop(); }").CreateRuntime(host).Invoke("Start"); } catch (ScriptError) { rejected = true; }
        Assert(rejected, "Recursion limit failed");
        sim = new BattleSimulation(Fighter(BlockCatalog.Template(0)), FighterData.Samples()[0], true);
        Run(sim, 181); Assert(!sim.Ended && sim.Cpu.Hp > 0 && sim.Cpu.X == BattleSimulation.Width * .75f, "Sandbox ended or dummy moved");
        sim.Player.X = sim.Cpu.X - 61; sim.Player.Y = sim.Cpu.Y; sim.Player.SetVelocity(1, 0); sim.Tick(1f / 120);
        Assert(sim.Cpu.X == BattleSimulation.Width * .75f && sim.Cpu.Y == BattleSimulation.Height / 2 && sim.Player.VelocityX < 0, "Dummy collision did not reflect player while keeping dummy fixed");
        Console.WriteLine("PASS: block templates, persistence, orbit launch, repeat, conditions, timer, generated image, drag model, expression slots, escaping, compiler diagnostics, execution budgets, sandbox");
    }
    sealed class NullHost : ISkillHost
    {
        public object Read(string name) => 0d;
        public object Call(string name, object[] arguments) => 0d;
    }
}
