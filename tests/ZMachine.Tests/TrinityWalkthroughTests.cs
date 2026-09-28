using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Trinity (V4) using a scripted walkthrough to achieve a
/// perfect score of 100/100. Uses seed 42, which fixes the randomised
/// jeep-radio channel (49) and the wire legend on the cardboard diagram
/// ("RD=POS BL=INF ST=GND WH=DET"), so the striped (ground) wire is the one
/// to cut. Exercises the V4 sundial hub (seven toadstool doors timed by
/// freezing the shadow with the lever), read_char "press any key" screens,
/// timed NPC daemons (roadrunner, rattlesnake, German shepherd, searchlight),
/// real-time countdown to 5:29:45, and the Trinity endgame. The run also
/// records every opcode the interpreter executes.
/// </summary>
public class TrinityWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Trinity with seed 42.
    /// Nearly every section is turn-sensitive: the sundial waits must land
    /// the shadow on the right toadstool, the orbit cut must happen as the
    /// satellite meets the door, and the Trinity site runs on a 15-second
    /// clock where the searchlight detection budget, the roadrunner's
    /// crumb-eating delay and the auto-sequencer all depend on exact turn
    /// counts. Adding or removing any command will desynchronise the run.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        // The first entry answers the title screen's read_char prompt.
        var commands = new List<string> { "key" };

        // === Kensington Gardens, London: the gnomon, umbrella, paper crane, and escape in the pram ("key" dismisses the arrival screen) ===
        commands.AddRange(["verbose", "northeast", "unscrew gnomon", "take gnomon", "southwest",
            "east", "take ball", "north", "north", "wait", "throw ball at umbrella",
            "take umbrella", "west", "west", "south", "take coin", "give coin to woman",
            "take bag", "take small coin", "feed birds", "east", "wait", "take paper bird",
            "northwest", "open perambulator", "get in perambulator", "open umbrella",
            "push perambulator south", "get in perambulator", "open umbrella", "take all", "south",
            "east", "east", "key"]);

        // === Arboretum: splinter and axe, mirror the gnomon in the pergola, fit it to the sundial (+5), then walk the pergola again to un-mirror ===
        commands.AddRange(["north", "northeast", "take log", "take splinter", "southeast",
            "southeast", "west", "north", "up", "take axe", "south", "down", "east", "northwest",
            "north", "north", "north", "put gnomon in hole", "south", "south", "south",
            "southwest", "east", "north", "up", "south", "down", "east", "northwest", "north",
            "north", "north"]);

        // === Symbol 6 (Moor door) -> Nagasaki: spade, crane for the girl; back in the hub, fell the oak across the chasm ===
        commands.AddRange(["turn ring to 5", "wait", "wait", "wait", "wait", "wait", "wait",
            "wait", "push lever", "south", "south", "northeast", "southeast", "drop bag",
            "drop small coin", "drop axe", "enter door", "open umbrella", "take all", "east",
            "take spade", "west", "wait", "give paper to girl", "give umbrella to girl",
            "climb bird", "enter door", "take all", "northwest", "west", "chop oak",
            "push oak north"]);

        // === Symbol 3 (Ossuary door) -> Nevada: trap the skink with lantern + splinter; skeleton key, icicle and magnetic lump ===
        commands.AddRange(["southwest", "east", "north", "north", "pull lever", "turn ring to 2",
            "wait", "wait", "wait", "wait", "wait", "wait", "wait", "push lever", "south", "south",
            "southwest", "northwest", "north", "north", "north", "search bones", "take key",
            "drop bag", "drop spade", "drop axe", "enter door", "take lantern", "turn on lantern",
            "west", "drop lantern", "west", "put splinter in crevice", "take skink",
            "put skink in pocket", "east", "take walkie-talkie", "take lantern", "east", "east",
            "take axe", "take spade", "south", "put key in hole", "turn key", "down",
            "turn off lantern", "throw spade at icicles", "take icicle", "east", "east", "east",
            "north", "north", "south", "south", "northeast", "east", "east", "put icicle on lump",
            "drop lantern", "drop walkie-talkie", "take lump"]);

        // === Symbol 2 (Waterfall door) -> Earth orbit in a soap bubble: kill the skink under the crescent moon (+3), anchor with the lump, cut free with the axe ===
        commands.AddRange(["west", "west", "southwest", "north", "north", "pull lever",
            "turn ring to 1", "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait",
            "push lever", "south", "south", "northwest", "north", "in", "wait", "wait", "wait",
            "south", "southwest", "enter door", "take skink", "squeeze skink", "wait", "wait",
            "wait", "cut film with axe", "put skink in pocket"]);

        // === Symbol 4 (Mesa door) -> Eniwetok: coconut; then honey from the hive and the magpie's birdcage at the cottage ===
        commands.AddRange(["east", "east", "north", "north", "pull lever", "turn ring to 3",
            "wait", "wait", "wait", "wait", "wait", "wait", "wait", "push lever", "south", "south",
            "northeast", "west", "north", "enter door", "down", "open box", "push button", "south",
            "northwest", "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait",
            "point at coconut", "take coconut", "southeast", "north", "up", "enter door", "south",
            "east", "reach into hive", "west", "west", "east", "east", "reach into hive", "west",
            "northeast", "open door", "east", "take cage"]);

        // === Cottage cauldron: skink, garlic, coconut milk and honey -> boom -> emerald ===
        commands.AddRange(["open back door", "east", "search refuse", "take garlic", "west",
            "take skink", "put skink in cauldron", "put garlic in cauldron", "drop coconut",
            "hit coconut with axe", "take coconut", "pour milk into cauldron",
            "put hand in cauldron", "drop coconut", "west", "east", "take emerald"]);

        // === Symbol 5 (Herb Garden door) -> Siberia: release the magpie, cage the trapped lemming ===
        commands.AddRange(["west", "southwest", "southwest", "east", "north", "north",
            "pull lever", "turn ring to 4", "wait", "wait", "wait", "wait", "wait", "wait", "wait",
            "wait", "wait", "wait", "wait", "push lever", "south", "south", "northeast", "west",
            "northeast", "east", "east", "enter white door", "down", "northeast", "northeast",
            "open cage", "examine fissure", "take lemming", "put lemming in cage", "close cage",
            "southwest", "southwest", "up", "enter door"]);

        // === Cemetery crypt: shroud, emerald into the green boot, silver coin; wear the winged boots ===
        commands.AddRange(["west", "west", "southwest", "southwest", "west", "west", "take spade",
            "east", "north", "open crypt with spade", "drop spade", "remove shroud",
            "put emerald in green boot", "remove bandage", "take coin from mouth", "wear shroud",
            "drop bandage", "drop axe", "take green boot", "take red boot", "wear green boot",
            "wear red boot"]);

        // === Recover the bag of crumbs from the Ossuary (lantern keeps the barrow wight away), exit through the Ice Cavern ===
        commands.AddRange(["south", "southeast", "northeast", "northeast", "east", "east",
            "take lantern", "take walkie-talkie", "west", "west", "southwest", "southwest",
            "northwest", "north", "turn on lantern", "north", "north", "take bag", "south",
            "turn key", "down", "turn off lantern", "drop small coin", "east"]);

        // === Symbol 7 (Islet door): pay the ferryman with the silver coin while wearing the shroud, follow the roadrunner through the door ===
        commands.AddRange(["east", "east", "north", "north", "pull lever", "turn ring to 6",
            "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait", "push lever", "south",
            "south", "southeast", "wait", "wait", "get in dory", "give silver coin to oarsman",
            "south", "enter door"]);

        // === Trinity shack: cardboard diagram, wait for the jeep to leave, descend; ruby into the red boot ===
        commands.AddRange(["drop lantern", "take book", "read book", "take cardboard", "drop book",
            "take lantern", "wait", "west", "down", "down", "examine diagram", "drop cardboard",
            "take ruby", "put ruby in red boot", "drop walkie-talkie"]);

        // === McDonald ranch: lemming into the rattlesnake's fangs from the closet, screwdriver, steak knife; bag of crumbs hidden in the closed cage ===
        commands.AddRange(["southeast", "southeast", "east", "south", "up", "open left door",
            "west", "north", "north", "close door", "open cage", "open door", "put bag in cage",
            "close cage", "south", "look under paper", "take screwdriver", "south", "west",
            "take knife", "east", "east", "east", "southeast", "northeast"]);

        // === Windmill collapse and dive for the binoculars; spy into the shelter and send the roadrunner for the steel key ===
        commands.AddRange(["drop cage", "drop screwdriver", "drop knife", "turn on lantern", "up",
            "take binoculars", "down", "take all", "up", "out", "drop lantern", "down",
            "northeast", "take all", "southwest", "west", "southwest", "west", "south", "south",
            "focus binoculars on entrance", "wait", "roadrunner, get key", "take key",
            "drop binoculars"]);

        // === Tower box padlock, jeep radio dial, walkie-talkie tuned to the same channel, circuit breaker off and on ===
        commands.AddRange(["north", "north", "north", "north", "unlock padlock with key",
            "take padlock", "drop padlock", "open box", "take walkie-talkie", "examine watch",
            "northwest", "northwest", "northwest", "in", "examine dial", "turn on walkie-talkie",
            "pull antenna", "set slider to 49", "out", "southeast", "southeast", "southeast",
            "open breaker", "close breaker"]);

        // === Crumbs at the blockhouse keep the roadrunner busy until it wakes the dog; the searchlight swings away while we climb the tower ===
        commands.AddRange(["southwest", "southwest", "southwest", "southwest", "open cage",
            "take bag", "drop bag", "north", "northeast", "east", "up", "up", "east"]);

        // === Open the enclosure, wait for the auto-sequencer, cut the ground (striped, per the seed-42 legend) wire ===
        commands.AddRange(["unscrew panel with screwdriver", "pull chain", "wait", "wait", "wait",
            "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait",
            "cut striped wire with knife"]);
        // Let the epilogue settle, then confirm the final score.
        commands.AddRange(["wait", "score"]);

        return commands.ToArray();
    }

    [SkippableFact]
    public void Trinity_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "trinity.z4");
        Skip.IfNot(File.Exists(path), "trinity.z4 not found in stories/");

        var input = new ScriptedInputStream(BuildWalkthroughCommands());
        var screen = new CaptureScreen();
        var machine = new Interpreter();
        machine.Load(path, input, screen);
        machine.SeedRandom(42);

        // Run manually rather than through TestHarness so each instruction
        // can be decoded and tallied for the walkthrough's opcode table.
        var disassembler = new Disassembler(machine.Memory);
        var opcodeCounts = new SortedDictionary<string, long>(StringComparer.Ordinal);
        // Code lives in static/high memory and never changes, so decoding each
        // address once keeps the tally from dominating the run time.
        var mnemonicAt = new Dictionary<int, string>();
        long executed = 0;
        int? exhaustedAt = null;
        try
        {
            while (machine.Running && executed < 60_000_000)
            {
                int pc = machine.State.PC;
                if (!mnemonicAt.TryGetValue(pc, out var mnemonic))
                    mnemonicAt[pc] = mnemonic = disassembler.DisassembleAt(pc).Mnemonic;
                opcodeCounts[mnemonic] = opcodeCounts.GetValueOrDefault(mnemonic) + 1;
                machine.Step();
                executed++;

                // Once the script is spent the game just re-prompts forever;
                // stop at the prompt that follows the last command's response
                // so idle polling isn't tallied.
                if (!input.HasMore)
                {
                    var text = screen.Output;
                    exhaustedAt ??= text.Length;
                    if (text.Length > exhaustedAt && text.EndsWith('>'))
                        break;
                }
            }
        }
        catch (ZMachineException ex)
        {
            Assert.Fail($"Interpreter crashed: {ex.Message}");
        }

        var output = screen.Output;
        var testDir = Path.Combine(FindRepoRoot(), "tests", "ZMachine.Tests");
        File.WriteAllText(Path.Combine(testDir, "trinity_walkthrough_output.txt"), output);
        File.WriteAllText(Path.Combine(testDir, "trinity_walkthrough_opcodes.txt"),
            string.Join("\n", opcodeCounts.Select(kv => $"{kv.Key}\t{kv.Value}")));

        Assert.Contains("You slide the blade of the steak knife under the striped wire", output);
        Assert.Contains("Your score is 100 points out of 100", output);
        Assert.Contains("This gives you the rank of Tourist", output);

        // Opcodes that only this V4 story exercises among the walkthroughs.
        Assert.Contains("read_char", opcodeCounts.Keys);
        Assert.Contains("scan_table", opcodeCounts.Keys);
        Assert.Contains("call_vs", opcodeCounts.Keys);
        Assert.True(opcodeCounts.Count >= 55,
            $"Expected at least 55 distinct opcodes, saw {opcodeCounts.Count}");
    }

    private static string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
