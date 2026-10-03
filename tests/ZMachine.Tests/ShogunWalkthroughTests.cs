using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through James Clavell's Shogun (V6) using a scripted walkthrough
/// to reach the perfect score of 420/420, every one of the eighteen scored
/// scenes finished at its maximum. Uses seed 42. Shogun exercises the V6
/// opcodes that were added or renumbered for it (@mouse_window, @push_stack,
/// @pop_stack, @print_form, @make_menu, V6 @pull) and the ZSCII 11 sentence
/// space. Between scenes the game waits for a keypress ("k") and then shows
/// a menu whose first entry, CONTINUE, is chosen with Enter ("\r"). The run
/// also records every opcode the interpreter executes.
/// </summary>
public class ShogunWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Shogun with seed 42. Most scenes
    /// run on timed events, so the WAITs matter. The Osaka street maze in
    /// scene 10 is walked through the game's 37x16 grid, where one move runs
    /// along a corridor until the next junction. Adding or removing a turn
    /// will usually miss an event later on.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === Title menu: press Enter to begin ===
        commands.AddRange(["\r"]);

        // === Scene 1, Bridge (55): steer clear of the reef, write the rutter, rally the crew ===
        commands.AddRange(["verbose", "notify", "take wheel", "turn wheel to starboard",
            "hendrik, take the wheel", "down", "aft", "port", "open desk", "take key",
            "unlock chest with key", "open chest", "take quill", "take your rutter",
            "write in your rutter with quill", "put your rutter in chest", "close chest",
            "lock chest with key", "out", "starboard", "ask roper about apples", "take apple", "out", "aft",
            "examine desk", "open desk", "take flagon", "give flagon to spillbergen", "eat apple", "out",
            "forward", "fore", "vinck, go on deck", "ginsel, go on deck", "maetsukker, go on deck",
            "hit maetsukker", "up", "up", "wait", "wait", "wait", "wait", "ring bell", "unlash wheel",
            "turn wheel to port", "straighten wheel", "wait", "wait", "wait", "wait", "wait",
            "crew, repair the foresails", "look at reef", "look at reef", "look at reef", "look at reef",
            "look at reef", "look at reef", "look at reef", "look at reef", "look at reef", "look at reef",
            "look at reef", "look at reef", "look at reef", "look at reef", "look at reef",
            "turn wheel to starboard", "straighten wheel", "take wheel", "look at reef", "look at reef",
            "vinck, take the wheel", "look", "look", "look", "look", "look", "look", "look", "look", "look",
            "look", "look", "turn wheel to port", "\r"]);

        // === Scene 2, Mura's House (20): food, clothes, fight the priest's henchmen, row out to the Erasmus ===
        commands.AddRange(["eat food", "wait", "point to me", "take tray", "give tray to onna", "wait",
            "eat food", "wear clothes", "open door", "out", "take boots", "wear boots", "east",
            "hit henchmen", "wait", "wait", "take crucifix", "wait", "wait", "english", "holland", "yes",
            "wait", "east", "enter boat", "take oars", "row to erasmus", "row east", "up", "bow to samurai",
            "aft", "\r"]);

        // === Scene 3, Village Square (10): introduce yourself to Yabu and break the crucifix ===
        commands.AddRange(["bow to yabu", "i am john blackthorne", "holland", "i am the pilot", "magellan",
            "take crucifix", "break crucifix", "bow to yabu", "\r"]);

        // === Scene 4, Pit (25): stop Vinck, take the ladder and dagger, accept Omi's terms, bathe ===
        commands.AddRange(["wait", "wait", "wait", "take straw", "wait", "stop vinck", "take ladder",
            "hit samurai", "take dagger", "wait", "wait", "up", "omi, hai", "lie down", "wait", "stand up",
            "north", "undress", "give clothes to mura", "give boots to mura", "enter tub", "wash", "k",
            "\r"]);

        // === Scene 5, Waterfront (20): answer Rodrigues's pilot test and warn him about the archers ===
        commands.AddRange(["wait", "wait", "wait", "yes", "trinity house", "50 degrees",
            "tell rodrigues about erasmus", "wait", "wait", "enter boat", "wait", "look at archers",
            "warn rodrigues", "wait", "wait", "up", "aft", "port", "k", "\r"]);

        // === Scene 6, Galley (55): save the galley in the storm, then rescue Rodrigues from the ledge ===
        commands.AddRange(["untie safety line", "forward", "grab gunwale", "oarsmen, ship the oars",
            "oarsmen, row", "aft", "tie safety line", "take helm", "turn helm to starboard", "look",
            "straighten helm", "take broken oar", "throw broken oar to rodrigues", "look",
            "untie safety line", "forward", "drop anchor", "launch skiff", "point at shore",
            "point at shore", "enter skiff", "row skiff to shore", "get out", "south", "south", "south",
            "look", "look down", "down", "look", "look down", "down", "wait", "wait", "take kimono",
            "tear kimono", "tie kimono", "give rope to samurai", "tie loincloths to rope", "look down",
            "point at narrow ledge", "wait", "raise rope", "k", "\r"]);

        // === Scene 7, Outer Corridor (15): kneel, point at Alvito, say 'teki' ===
        commands.AddRange(["kneel", "wait", "wait", "wait", "point at alvito", "say teki", "wait",
            "approach alvito", "wait", "k", "\r"]);

        // === Scene 8, Cell Block (30): let the apelike man strike first, learn Japanese from the priest ===
        commands.AddRange(["wait", "hit man", "hit man", "hit man", "take cup", "eat gruel", "wait", "wait",
            "no", "speak spanish", "priest, teach me japanese", "wait", "wait", "wait", "wait", "wait",
            "wait", "wait", "look", "stand in line", "wait", "wear clothes", "wait", "wait", "wait", "wait",
            "wait", "duck", "wait", "enter palanquin", "k", "\r"]);

        // === Scene 9, Glade of Maples (15): greet Mariko in Japanese, ask about the prophecy ===
        commands.AddRange(["konnichi wa", "in prison", "wait", "smile at boy", "wait", "wait", "wait",
            "ask mariko about prophecy", "k", "\r"]);

        // === Scene 10, Courtyard (65): unmask Toranaga, feign madness, pass the gate, ambush, Osaka maze, galley fight ===
        commands.AddRange(["wait", "wait", "wait", "wait", "wait", "wait", "look at kiritsubo", "wait",
            "south", "south", "look", "wait", "act insane", "take parchment",
            "put parchment through curtains", "south", "south", "south", "stop", "south", "south", "south",
            "wait", "wait", "wait", "wait", "east", "k", "south", "east", "south", "west", "south", "east",
            "north", "west", "south", "west", "north", "west", "south", "west", "north", "west", "south",
            "west", "north", "west", "south", "west", "south", "get out", "take mariko", "wait", "wait",
            "wait", "stand up", "stop messenger", "hit messenger", "wait", "wait", "k", "east", "north",
            "east", "south", "west", "south", "east", "south", "east", "north", "east", "south", "east",
            "north", "east", "north", "east", "south", "west", "south", "east", "south", "east", "north",
            "east", "south", "east", "north", "east", "south", "west", "west", "south", "east", "south",
            "east", "north", "east", "north", "east", "south", "east", "north", "east", "yes", "east",
            "look", "east", "yes", "help mariko", "mizu", "wait", "throw large knife at leader", "duck",
            "untie small knife", "throw small knife at young samurai", "wait", "k", "\r"]);

        // === Scene 11, Plateau (20): earthquake rescues, the sword, the ditch ===
        commands.AddRange(["wait", "wait", "wait", "grab toranaga", "wait", "enter side fissure",
            "pull mariko", "lift mariko", "up", "kneel", "offer killing sword to toranaga",
            "piss in fissure", "k", "\r"]);

        // === Scene 12, Bath House (15): bathe with Mariko, slip past Yoshinaka's patrol ===
        commands.AddRange(["undress", "drop all", "enter tub", "enter tub", "wash", "wait", "wait", "wait",
            "wait", "wait", "out", "north", "west", "kiss mariko", "out", "south", "west", "k", "\r"]);

        // === Scene 13, Reception Room (15): check the sword belt, the camellia, answer Ishido ===
        commands.AddRange(["examine swords", "bow", "give blossom to ochiba", "yes", "wait", "wait", "wait",
            "i am hatamoto", "wait", "wait", "wait", "wait", "\r"]);

        // === Scene 14, Forecourt (5): duel the Gray captain ===
        commands.AddRange(["draw sword", "hit captain", "hit captain", "hit captain", "hit captain",
            "hit captain", "wait", "wait", "wait", "k", "\r"]);

        // === Scene 15, Formal Garden (5): catch Mariko when she falls ===
        commands.AddRange(["enter tea house", "wait", "out", "take ribbon", "wear sandals", "east", "south",
            "look", "wait", "wait", "wait", "wait", "catch mariko", "\r"]);

        // === Scene 16, Mariko's Quarters (30): Yabu's plan, the cellars, the ninja attack ===
        commands.AddRange(["south", "ask yabu about plan", "south", "south", "west", "west", "down", "down",
            "down", "hide behind crates", "look", "north", "look", "wait", "wait", "wait", "wait", "south",
            "south", "west", "wait", "wait", "fire pistol at ninja leader", "mariko, follow me", "east",
            "close inner door", "lock inner door", "north", "north", "north", "close secret door",
            "lock secret door", "lock secret door", "lock secret door", "wait", "wait", "wait", "wait",
            "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait",
            "approach mariko", "bless mariko", "k", "\r"]);

        // === Scene 17, Yokohama (5): carry Vinck back ===
        commands.AddRange(["wait", "wait", "wait", "wait", "help vinck", "\r"]);

        // === Scene 18, Stable (15): Mariko's letter and Yabu's treachery; then the epilogue ===
        commands.AddRange(["open scroll", "read scroll", "tell toranaga about message", "wait", "no",
            "tell toranaga about ninja", "i am kasigi yabu", "wait", "wait", "k", "\r", "k"]);

        return commands.ToArray();
    }

    [SkippableFact]
    public void Shogun_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "Shogun", "Story.data.z6");
        Skip.IfNot(File.Exists(path), "Shogun/Story.data.z6 not found in stories/");

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
            while (machine.Running && executed < 80_000_000)
            {
                int pc = machine.State.PC;
                if (!mnemonicAt.TryGetValue(pc, out var mnemonic))
                    mnemonicAt[pc] = mnemonic = disassembler.DisassembleAt(pc).Mnemonic;
                opcodeCounts[mnemonic] = opcodeCounts.GetValueOrDefault(mnemonic) + 1;
                machine.Step();
                executed++;

                // After the last keypress the game sits in its end-of-game
                // menu. Stop once that menu has been printed so idle polling
                // isn't tallied.
                if (!input.HasMore)
                {
                    var text = screen.Output;
                    exhaustedAt ??= text.Length;
                    if (text.IndexOf("RESTART the game", exhaustedAt.Value, StringComparison.Ordinal) >= 0)
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
        File.WriteAllText(Path.Combine(testDir, "shogun_walkthrough_output.txt"), output);
        File.WriteAllText(Path.Combine(testDir, "shogun_walkthrough_opcodes.txt"),
            string.Join("\n", opcodeCounts.Select(kv => $"{kv.Key}\t{kv.Value}")));

        Assert.Contains("and 420 out of 420 overall", output);
        Assert.Contains("You have now achieved a rank of Regent.", output);
        Assert.Contains("Ishido lingered three days and died very old.", output);
        Assert.DoesNotContain("You have died", output);
        Assert.DoesNotContain("this scene is no longer winnable", output);
        Assert.Equal(18, CountOccurrences(output, "[Congratulations, you have finished this scene"));

        // The V6-specific opcodes fixed for Shogun must actually run.
        Assert.Contains("mouse_window", opcodeCounts.Keys);
        Assert.Contains("read_char", opcodeCounts.Keys);
        Assert.True(opcodeCounts.Count >= 60,
            $"Expected at least 60 distinct opcodes, saw {opcodeCounts.Count}");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        for (int i = text.IndexOf(value, StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
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
