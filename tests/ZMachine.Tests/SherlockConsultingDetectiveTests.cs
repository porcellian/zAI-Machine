using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Sherlock: The Riddle of the Crown Jewels (V5) with an
/// independently solved walkthrough that earns 100/100 and the rank of
/// Consulting Detective. Uses seed 42, which fixes Mycroft's Tower password
/// ("Cleves"). Exercises the game's real-time clock: Big Ben's hourly
/// chimes, the Thames tides, the Abbey's opening hours, Akbar's 2:00 a.m.
/// appointment and the 9:00 a.m. Monday deadline. It also covers timed
/// input, the "[Press any key]" and Y/N interruptions handled with
/// @read_char, and the V5 status line. The run records every opcode the
/// interpreter executes.
/// </summary>
/// <remarks>
/// This is a second, independent Sherlock solution alongside
/// <see cref="SherlockWalkthroughTests"/>; it writes to its own output files.
/// </remarks>
public class SherlockConsultingDetectiveTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Sherlock with seed 42.
    /// Single-letter lines ("z", "k", "n", "y") answer @read_char prompts:
    /// the title screen, Big Ben's hourly "[Press any key]" and the
    /// "continue waiting?" question that interrupts WAIT. Their positions
    /// depend on where in-game events fall, so adding or removing commands
    /// before them will desynchronise the script.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === Baker Street: a keypress for the title screen; the newspaper rouses Holmes (+6); the Prime Minister brings the clue verse ===
        commands.AddRange(["z", "verbose", "knock on door", "west", "up", "north",
            "take newspaper", "give newspaper to holmes", "wait", "n", "read paper", "wait", "n"]);

        // === Holmes's lamp, magnifying glass, pipe and tobacco, the parlour matchbook (+7); light the lamp in the fog ===
        commands.AddRange(["west", "take lamp", "take glass", "east", "take knife", "take pipe",
            "take tobacco", "south", "down", "north", "take matchbook", "south", "open door",
            "east", "light lamp"]);

        // === Walk to Westminster Abbey and wait for the 7:00 opening; crayon and paper in the North Cloister (+2) ===
        commands.AddRange(["south", "south", "south", "southeast", "southeast", "east", "east",
            "k", "read sign", "knock on door", "open door", "wait", "wait", "wait", "n", "wait",
            "wait", "wait", "open door", "east", "look", "south", "southeast", "take crayon",
            "take pacquet", "turn off lamp", "northwest", "north"]);

        // === Rub the tombs of Newton, Elizabeth and Henry V; heat the rubbings over the Evangelist Chapel candles to reveal the invisible ink (+5) ===
        commands.AddRange(["open pacquet", "take white paper", "put white paper on tomb",
            "rub white paper with crayon", "take white paper", "northeast", "east",
            "hold white paper over candles", "turn white paper over", "south", "east",
            "take yellow paper", "put yellow paper on tomb", "rub yellow paper with crayon",
            "take yellow paper", "south", "west", "take blue paper",
            "put blue paper on henry's tomb", "rub blue paper with crayon", "take blue paper",
            "east", "northwest", "north", "hold yellow paper over candles",
            "turn yellow paper over", "hold blue paper over candles", "turn blue paper over"]);

        // === Haggle for the telescope (+1); spot the ruby in Nelson's blind eye; Sherman's half-trained pigeon fetches it (+6) ===
        commands.AddRange(["west", "southwest", "west", "northeast", "west", "haggle with vendor",
            "haggle with vendor", "buy telescope", "east", "north", "north",
            "look at statue through telescope", "north", "east", "east", "south", "west",
            "ask sherman for pigeon", "east", "north", "west", "west", "south",
            "look at ruby through telescope", "show ruby to pigeon", "pigeon, get ruby",
            "release pigeon", "north", "east", "east", "south", "west", "ask sherman about ruby"]);

        // === A shilling buys Wiggins, who picks the bank guard's pocket for the safety deposit key (+1) ===
        commands.AddRange(["east", "north", "east", "give shilling to wiggins",
            "wiggins, steal keys"]);

        // === Big Ben: cotton in the ears, then tug the sapphire off the clapper as the bell swings in at 11:00 (+5) ===
        commands.AddRange(["west", "west", "west", "south", "south", "south", "southeast", "up",
            "put white paper in pacquet", "put yellow paper in pacquet",
            "put blue paper in pacquet", "open bag", "open blue bottle", "take cotton balls",
            "put cotton balls in ears", "wait", "wait", "wait", "wait", "wait", "take sapphire",
            "wait", "n", "take sapphire", "wait", "n", "wait", "n", "wait", "n", "wait", "n",
            "wait", "n", "wait", "n", "wait", "n", "wait", "n", "wait", "n", "wait", "n", "wait",
            "n", "take cotton balls", "put cotton balls in blue bottle", "close blue bottle",
            "close bag"]);

        // === Madame Tussaud's: a lit pipe passes the match-sniffing dog (+1); the burning clue paper lights Guy Fawkes's torch (+4), which melts Charles I's wax head (+5) ===
        commands.AddRange(["down", "northwest", "north", "north", "northeast", "east", "south",
            "look", "north", "west", "southwest", "north", "north", "west", "put tobacco in pipe",
            "open matchbook", "take match", "light match", "light tobacco with match",
            "drop matchbook", "drop lamp", "north", "west", "take torch",
            "light clue paper with pipe", "light torch with clue paper", "take head",
            "melt head with torch", "take emerald"]);

        // === Captain Bligh's oar from Scotland Yard's Black Museum (+1) ===
        commands.AddRange(["east", "south", "take lamp", "take matchbook", "east", "south",
            "south", "south", "east", "light lamp", "down", "take oar", "up", "turn off lamp"]);

        // === Covent Garden: the stethoscope (+1) finds the flower girl's slow heart; belladonna revives her and she gives a carnation (+5) ===
        commands.AddRange(["west", "north", "northeast", "north", "drop crayon", "drop matchbook",
            "drop telescope", "take off hat", "take stethoscope", "wear hat", "wear stethoscope",
            "listen to girl", "open bag", "open brown bottle", "take yellow pill",
            "put yellow pill in girl's mouth", "remove stethoscope"]);

        // === Anchor under London Bridge until low tide at 8:40 p.m. and take the moss, which crumbles to an opal (+5); row back ===
        commands.AddRange(["south", "east", "south", "get in boat", "pull chain", "launch boat",
            "look", "look", "drop anchor", "wait until 8:40", "y", "light lamp", "take moss",
            "put bligh's oar in oarlock", "pull chain", "look", "row north", "launch boat",
            "row west", "row west", "row west", "row north", "get out of boat"]);

        // === The ruby's inscription earns Holmes's ring (+1); four gems bribe the bank guard (+3); crack the vault by ear (+3); topaz in box 600 (+5); Holmes is kidnapped ===
        commands.AddRange(["east", "east", "north", "northwest", "northwest",
            "examine ruby with glass", "give ruby to guard", "give sapphire to guard",
            "give emerald to guard", "give opal to guard", "north", "take off hat",
            "take stethoscope", "wear hat", "wear stethoscope", "listen to dial",
            "turn dial right", "turn dial right", "turn dial left", "turn dial right",
            "turn dial right", "remove stethoscope", "west", "unlock box 600 with key",
            "take topaz", "east", "south"]);

        // === Mycroft takes the ring and gives the Tower password (+1); the Byward Tower (+3) ===
        commands.AddRange(["west", "west", "west", "south", "west", "ask butler about mycroft",
            "give ring to butler", "east", "north", "east", "east", "east", "southeast",
            "southeast", "south", "east", "east", "east", "say cleves"]);

        // === Mace (+1); Wiggins fetches the garnet from the drained malmsey butt (+5); paddle (+1); the suit of armour adds the weight to raise the portcullis (+1); paddle back ===
        commands.AddRange(["north", "north", "southeast", "up", "take mace", "down", "northwest",
            "northeast", "hit bung with mace", "wiggins, get in butt", "out", "south", "south",
            "south", "take paddle", "north", "north", "north", "east", "down", "light lamp",
            "wear armour", "up", "west", "south", "south", "south", "pull chain", "south",
            "remove armour", "drop mace", "get in boat", "raise anchor", "launch boat",
            "paddle west", "paddle west", "paddle west", "paddle north", "get out of boat"]);

        // === The etherium ampoule from Holmes's bedroom goes under the hat; on to the Bar of Gold ===
        commands.AddRange(["north", "west", "southwest", "north", "west", "west", "north", "west",
            "up", "north", "west", "take ampoule", "take off hat", "put ampoule in hat",
            "wear hat", "east", "south", "down", "east", "south", "east", "east", "east", "east",
            "east", "southeast", "southeast", "south", "down", "west"]);

        // === Wait for Monday 2:00 a.m.; with a carnation and "Swordfish" Akbar appears, and the garnet buys passage to Moriarty's lair (+5) ===
        commands.AddRange(["wait until 6:00", "wait until 10:00", "wait until 2:00",
            "wait until 6:00", "wait until 10:00", "wait until 1:50", "look", "wait until 2:00",
            "say swordfish", "give garnet to akbar"]);

        // === Hold breath, break the ampoule, untie Holmes, and tie up Moriarty and Akbar before passing out ===
        commands.AddRange(["take off hat", "take ampoule", "hold breath", "break ampoule",
            "untie holmes", "tie moriarty with rope", "tie akbar with rope"]);

        // === Crown Jewels (+10); two blasts on the whistle summon a hansom (+5); deliver the regalia before 9:00 (+1) ===
        commands.AddRange(["take jewels", "take whistle", "take key", "unlock door with key",
            "open door", "north", "blow whistle", "blow whistle", "get in cab",
            "buckingham palace", "get out of cab", "give jewels to guard"]);
        return commands.ToArray();
    }

    [SkippableFact]
    public void Sherlock_ConsultingDetective()
    {
        var path = Path.Combine(StoriesDir, "sherlock.z5");
        Skip.IfNot(File.Exists(path), "sherlock.z5 not found in stories/");

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
        long afterScript = 0;
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

                // After the win the game asks "(1) Quit, (2) Restart..." via
                // @read_char, which an exhausted script answers with Enter
                // forever. Stop once that menu appears (checking only
                // periodically, since building the transcript is costly).
                if (!input.HasMore && ++afterScript % 1000 == 1)
                {
                    var text = screen.Output;
                    exhaustedAt ??= text.Length;
                    if (text.Length > exhaustedAt &&
                        (text.Contains("(5) Undo ?") || text.TrimEnd().EndsWith('>')))
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
        File.WriteAllText(Path.Combine(testDir, "sherlock_cd_walkthrough_output.txt"), output);
        File.WriteAllText(Path.Combine(testDir, "sherlock_cd_walkthrough_opcodes.txt"),
            string.Join("\n", opcodeCounts.Select(kv => $"{kv.Key}\t{kv.Value}")));

        Assert.Contains("Coronation Day festivities", output);
        Assert.Contains("Your score is 100 out of 100, which earns you a ranking of Consulting Detective.", output);
        Assert.DoesNotContain("the end is near", output);

        Assert.Contains("read_char", opcodeCounts.Keys);
        Assert.Contains("aread", opcodeCounts.Keys);
        Assert.True(opcodeCounts.Count >= 50,
            $"Expected at least 50 distinct opcodes, saw {opcodeCounts.Count}");
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
