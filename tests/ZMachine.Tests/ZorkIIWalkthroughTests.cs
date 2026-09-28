using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Zork II: The Wizard of Frobozz (V3) using a scripted
/// walkthrough to achieve a perfect score of 400/400. Uses seed 42, which
/// fixes the Carousel Room's random exits, the Low Room's spinning compass,
/// and the timing of the Wizard's random spells (Fence, Feeble, Fall) that
/// the script waits out. Exercises NPC following (princess, robot, dragon),
/// the Bank of Zork's wall-walking puzzle, the volcano balloon, the demon's
/// fee, wand spells, the oddly-angled maze, and the ending in the dark
/// crypt. The run also records every opcode the interpreter executes.
/// </summary>
public class ZorkIIWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Zork II with seed 42.
    /// The run is highly sensitive to the random number stream: the carousel
    /// destinations, the princess's pace, the Low Room's compass and every
    /// appearance of the Wizard depend on it, and several WAITs exist only to
    /// sidestep a spell. Adding or removing any command will desynchronise
    /// later sections.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === Barrow: lamp and sword; through the cavern and garden to the spinning Carousel Room ===
        commands.AddRange(["verbose", "take lamp", "take sword", "turn on lamp", "south", "south",
            "south", "southwest", "south", "southeast", "south", "south", "west"]);

        // === Wait for the carousel's random exit to drop us in Marble Hall (seed-dependent) ===
        commands.AddRange(["wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait",
            "wait", "wait"]);

        // === Goad the dragon south to the Ice Room, where it melts the glacier and drowns (+5); ruby in the Lava Room ===
        commands.AddRange(["north", "north", "north", "west", "north", "hit dragon with sword",
            "south", "hit dragon with sword", "south", "hit dragon with sword", "west", "west",
            "take ruby"]);

        // === Dragon's Lair: open the chest (statuette), then follow the princess to the garden for the gold key and rose ===
        commands.AddRange(["wait", "east", "east", "north", "north", "north", "open chest",
            "take statuette", "south", "east", "wait", "east", "down", "south", "east", "east",
            "north", "wait", "wait", "wait", "wait"]);

        // === Stash treasures in the gazebo; Tiny Room door: mat under the door, letter opener pokes out the key; blue sphere ===
        commands.AddRange(["turn off lamp", "in", "drop ruby", "drop statuette", "drop rose",
            "take mat", "take letter opener", "take matchbook", "out", "south", "turn on lamp",
            "west", "west", "north", "north", "up", "open lid", "put mat under door",
            "put letter opener in keyhole", "pull mat", "take key", "take letter opener",
            "unlock door with iron key", "open door", "north", "take sphere"]);

        // === Bank of Zork: portrait, Small Room, Vault; leave the depository through the curtain carrying the loot ===
        commands.AddRange(["south", "down", "west", "north", "west", "west", "drop sword",
            "drop letter opener", "drop rusty key", "drop matchbook", "northeast", "east", "south",
            "take portrait", "north", "enter curtain", "walk through south wall", "enter curtain",
            "wait", "wait", "take bills", "walk through north wall", "drop bills", "drop portrait",
            "east", "east", "take bills", "take portrait", "enter curtain", "south", "take sphere",
            "take matchbook"]);

        // === Carousel to the Riddle Room; answer "well"; stash bank loot in the Pearl Room ===
        commands.AddRange(["east", "east", "south", "south", "southeast", "southeast", "south",
            "southeast", "answer \"well\"", "east", "drop portrait", "drop bills", "west",
            "northwest"]);

        // === Teapot of water from the Deep Ford; pearl necklace; water in the bucket lifts us up the well ===
        commands.AddRange(["east", "southwest", "east", "south", "east", "north", "north", "in",
            "take teapot", "take newspaper", "out", "south", "west", "west", "north",
            "fill teapot with water", "south", "south", "southeast", "southeast", "southeast",
            "east", "take necklace", "east", "get in bucket", "pour water in bucket",
            "get out of bucket"]);

        // === Tea Room cakes: shrink (green), evaporate the pool of tears (red) for the candy, regrow (blue) ===
        commands.AddRange(["east", "drop teapot", "drop newspaper", "take green cake",
            "take red cake", "take blue cake", "take orange cake", "turn off lamp", "wait", "wait",
            "wait", "wait", "wait", "wait", "wait", "eat green cake", "turn on lamp", "east",
            "throw red cake in pool", "take candy", "take flask", "west", "eat blue cake",
            "turn off lamp"]);

        // === Robot frees us from the cage (red sphere) and pushes the triangular button to stop the carousel; ride the bucket down ===
        commands.AddRange(["northwest", "take paper", "robot, go east", "east", "robot, go south",
            "south", "take sphere", "robot, lift cage", "drop paper", "take sphere",
            "robot, go north", "north", "robot, push triangular button", "west", "southeast",
            "west", "southeast", "drop orange cake", "take teapot", "turn on lamp", "west",
            "get in bucket", "fill teapot with water", "get out of bucket", "west", "west"]);

        // === Violin from the steel box; candy for the lizard; spheres on the stands; the steel box shatters the aquarium (clear sphere) ===
        commands.AddRange(["northwest", "open box", "drop teapot", "take violin", "southwest",
            "southwest", "give candy to lizard", "unlock door with gold key", "open door", "south",
            "west", "put red sphere on ruby stand", "put blue sphere on sapphire stand",
            "drop necklace", "drop violin", "drop flask", "east", "north", "north", "northeast",
            "take box", "southwest", "southwest", "south", "west", "west", "throw box at aquarium",
            "take sphere", "east", "put sphere on diamond stand"]);

        // === Collect string (fuse), brick, matchbook and brochure (fuel), then head for the volcano ===
        commands.AddRange(["turn off lamp", "wait", "wait", "wait", "turn on lamp", "east",
            "north", "north", "take string", "northeast", "north", "take brick", "north", "north",
            "west", "north", "west", "west", "take matchbook", "northeast", "east",
            "take brochure", "east", "south", "east", "east", "south", "south", "west", "west",
            "south"]);

        // === Balloon: blow the Dusty Room box for the crown, descend to the Narrow Ledge for the zorkmid coin and stamp ===
        commands.AddRange(["get in basket", "open receptacle", "put brochure in receptacle",
            "light match", "light brochure with match", "wait", "wait", "wait", "wait", "land",
            "tie wire to hook", "get out of basket", "south", "put brick in hole",
            "put string in brick", "light match", "light string with match", "north", "south",
            "take crown", "take card", "north", "get in basket", "close receptacle", "untie wire",
            "wait", "wait", "wait", "wait", "land", "tie wire to hook", "get out of basket",
            "take coin", "south", "take purple book", "open purple book", "take stamp", "north",
            "get in basket", "untie wire", "wait", "wait", "wait"]);

        // === Gather the stashed treasures on the way to the Wizard's workroom ===
        commands.AddRange(["north", "drop purple book", "drop card", "east", "east", "southeast",
            "southeast", "east", "take bills", "take portrait", "west", "northwest", "east",
            "north", "north", "turn off lamp", "in", "take ruby", "take statuette", "out", "south",
            "south", "turn on lamp", "west", "southwest", "southwest", "south", "west"]);

        // === Summon the demon with the black sphere, pay ten treasures, and have him take the Wizard's wand ===
        commands.AddRange(["turn off lamp", "drop lamp", "drop matchbook", "take black sphere",
            "south", "put black sphere in circle", "give crown to demon", "give coin to demon",
            "give stamp to demon", "give bills to demon", "give portrait to demon",
            "give ruby to demon", "give statuette to demon", "give gold key to demon", "north",
            "take violin", "take necklace", "south", "give violin to demon",
            "give necklace to demon", "demon, give me the wand", "take wand"]);

        // === Wand + "float" lifts the menhir; gigantic dog collar from the Kennel ===
        commands.AddRange(["north", "take lamp", "turn on lamp", "east", "north", "north",
            "northeast", "south", "point wand at menhir", "say \"float\"", "southwest",
            "take collar"]);

        // === Oddly-angled rooms (SE, NE, NW, SW), collar Cerberus, open the crypt and, in darkness, the secret door ===
        commands.AddRange(["northeast", "south", "down", "down", "southeast", "northeast",
            "northwest", "southwest", "east", "down", "put collar on dog", "east", "open door",
            "south", "close door", "turn off lamp", "open secret door", "south"]);

        return commands.ToArray();
    }

    [SkippableFact]
    public void ZorkII_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "zork2.z3");
        Skip.IfNot(File.Exists(path), "zork2.z3 not found in stories/");

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
        File.WriteAllText(Path.Combine(testDir, "zork2_walkthrough_output.txt"), output);
        File.WriteAllText(Path.Combine(testDir, "zork2_walkthrough_opcodes.txt"),
            string.Join("\n", opcodeCounts.Select(kv => $"{kv.Key}\t{kv.Value}")));

        Assert.Contains("With courage and cunning you have conquered the Wizard of Frobozz", output);
        Assert.Contains("Your score would be 400 (total of 400 points)", output);
        Assert.Contains("This score gives you the rank of Master Adventurer.", output);

        Assert.Contains("read", opcodeCounts.Keys);
        Assert.Contains("random", opcodeCounts.Keys);
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
