using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Ballyhoo using a walkthrough to achieve a perfect score
/// of 200 points. Tests full-game interpreter correctness across hundreds
/// of turns covering object manipulation, NPC interaction, timed events,
/// complex parser constructs, and multi-step puzzles.
/// </summary>
public class BallyhooPerfectScoreTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Walkthrough commands extracted from the Ballyhoo walkthrough.
    /// Covers the complete game from start to finish for 200/200 points.
    /// </summary>
    private static readonly string[] WalkthroughCommands =
    [
        // === Section 1: Opening — help Thumb, get mask, spy on Munrab ===
        "south",
        "z", "z",
        "help thumb",
        "west",
        "take mask",
        "south",
        "z", "z", "z", "z", "z", "z", "z",   // wait 7 turns for Thumb
        "west",
        "examine taft",
        "take taft",
        "hide behind taft",
        "z", "z", "z", "z", "z", "z", "z", "z",  // listen to conversations

        // === Section 2: Tightrope — get pole, cross, get balloon ===
        "east", "east",
        "take pole",
        "north", "north", "north",
        "drop mask",
        "up",
        "east", "east", "east", "east", "east", "east",  // cross tightrope
        "take balloon",
        "west", "west", "west", "west", "west", "west",  // return
        "down", "down",
        "take all",
        "south", "south", "west", "south",

        // === Section 3: Past Harry — helium trick, Clown Alley ===
        "look in cage",
        "look at man",
        "untie balloon",
        "inhale helium",
        "hello harry",
        "south",
        "west",
        "wear mask",
        "knock on door",
        "south",
        "close door",
        "search ashes",
        "take scrap",
        "z",

        // === Section 4: Under bleachers — ticket, keys, bucket ===
        "go under wall",
        "east", "north", "east", "north", "northeast",
        "search garbage",
        "take ticket",
        "punch blue dot",
        "southwest", "south",
        "put ticket in slot",
        "east",
        "south", "southeast",
        "look in cage",
        "take keys with pole",
        "unlock door",
        "open door",
        "north",
        "take bucket",
        "take headphones",

        // === Section 5: Whip, stool, lion puzzle, cigarette case ===
        "south", "northwest", "north",
        "west", "west", "south", "west", "south", "east",
        "examine trailer",
        "unlock compartment",
        "open compartment",
        "take whip",
        "north", "east", "north",
        "put ticket in slot",
        "east", "east", "east",
        "north", "northeast",
        "take stool",
        "northwest", "south",
        "west", "west", "west",
        "north", "north",
        "unlock cage",
        "open cage",
        "west",
        "look at lions",
        "whip smooth lion",
        "whip smooth lion",
        "whip smooth lion",
        "lift grate",
        "throw meat in passage",
        "east",
        "west",
        "lower grate",
        "search stand",

        // === Section 6: Give case to Harry/Jenny, hypnosis ===
        "east",
        "south", "south", "south", "west",
        "give cigarette case to harry",
        "north", "east",
        "put ticket in slot",
        "east", "east", "south",
        "show case to jenny",
        "give case to jenny",
        "north", "north",
        "give ticket to rimshaw",
        "rimshaw, hypnotize me",

        // === Section 7: Hypnosis scene ===
        "z", "z", "z", "z",
        "buy candy",
        "give $1.85 to hawker",
        "stand",
        "east", "up", "east", "down", "east", "up", "east", "down", "south",
        "stand in line",
        "z", "z",
        "get out of long line",
        "stand in short line",
        "z", "z",
        "get out of long line",
        "yes",
        "stand in long line",
        "bite banana",
        "drop banana",
        "north",
        "ask hawker about candy",
        "up", "west", "down", "west", "up", "west", "down", "west",

        // === Section 8: Wake up — granola bar, radio ===
        "stand",
        "south", "west",
        "go under wall",
        "search garbage",
        "take granola bar",
        "south", "east", "east", "north", "northeast",
        "show granola bar to tina",
        "tina, hello",
        "shake hands",
        "northwest",
        "take radio",

        // === Section 9: Recording Rimshaw's voice ===
        "south", "west", "west", "south", "southeast",
        "drop all",
        "take radio", "take headphones",
        "up",
        "set radio to 1170",
        "turn radio off",
        "rewind tape",
        "z",
        "play tape",
        "z", "z",
        "stop tape",
        "rewind tape",
        "turn radio on",
        "record",
        "z", "z", "z", "z", "z",
        "stop tape",
        "rewind tape",
        "z",
        "turn radio off",
        "down",
        "take all",
        "northwest",

        // === Section 10: Mahler, mousetrap, mouse ===
        "unlock cage",
        "open cage",
        "west",
        "play tape",
        "search straw",
        "open trap door",
        "take ribbon",
        "east",
        "close cage",
        "lock cage",
        "north", "west", "west", "south", "west",
        "touch wood with pole",
        "take mousetrap",
        "drop mousetrap",
        "take cheese",
        "put cheese on trap",
        "east", "west", "east", "west",
        "catch mouse with bucket",
        "take mouse",

        // === Section 11: Elephant stampede & White Wagon ===
        "east", "north", "east",
        "put ticket in slot",
        "east",
        "south",
        "show mouse to elephant",
        "show mouse to elephant",
        "z",
        "southwest",
        "drop all",
        "up",
        "turn crank",
        "look in wagon",
        "knock on door",
        "in",
        "lock door",
        "search desk",
        "take spreadsheet",
        "move desk",
        "up",
        "read spreadsheet",
        "down",
        "take all",
        "west",
        "ask harry about eddie",

        // === Section 12: Sideshow & Blackjack ===
        "east", "northeast", "southeast",
        "slide ticket under front",
        "east",
        "take ticket",
        "bet 25 cents",
        "stand",
        "open panel",
        "stand",
        "open panel",

        // === Section 13: Blue Room & Elephant Prod ===
        "west", "northwest", "north", "west",
        "examine thumb",
        "south", "northeast", "southeast",
        "slide ticket under front",
        "east",
        "look under table",
        "take suitcase",
        "z",
        "open panel",
        "west",
        "drop all",
        "up", "up",
        "z",
        "east",
        "z",
        "take shaft",
        "pull shaft",
        "down", "down",
        "take all",
        "drop keys", "drop whip", "drop stool",

        // === Section 14: Detective, ransom note ===
        "northwest", "north", "west",
        "fill bucket with water",
        "south", "northeast", "north",
        "pour water on detective",
        "ask detective about chelsea",
        "drop bucket",
        "take ransom note",
        "take card",
        "read ransom note",

        // === Section 15: Wardrobe, confront Eddie/Chuckles ===
        "east", "south", "up",
        "take all",
        "up", "north",
        "west", "west", "west",
        "south", "west", "south", "east",
        "eddie, hello",
        "show ribbon to chuckles",
        "show scrap to chuckles",
        "show note to chuckles",
        "show spreadsheet to chuckles",
        "show card to chuckles",
        "search pocket",
        "take veil",
        "wear veil",
        "wear dress",
        "wear jacket",

        // === Section 16: Rescue Chelsea ===
        "knock on door",
        "east",
        "close door",
        "take crowbar",
        "move moose",
        "open door",
        "west", "west",
        "open door with crowbar",
        "south",
        "take thumb",
        "north", "east", "east",
        "put thumb in hole",
        "z",
        "take chelsea",

        // === Section 17: Finale — net, Mahler, tightrope ===
        "west", "north",
        "east", "northeast",
        "north", "west", "north", "north",
        "clap hands",
        "roustabout, get net",
        "remove veil",
        "remove jacket",
        "remove dress",
        "drop all",
        "west",
        "take stand",
        "east",
        "drop stand",
        "take radio",
        "climb stand",
        "up",
        "drop radio",
        "down",
        "take pole",
        "climb stand",
        "up",
        "take radio",
        "up",
        "turn on radio",
        "east", "east", "east",
        "west", "west", "west", "west",
        "drop radio", "drop pole",
        "down",
        "south", "south", "south", "east",
        "call wpdl",
        "west",
        "north", "north", "north",
        "climb stand",
        "up",
        "take all",
        "east", "east", "east", "east", "east",
        "z", "z", "z",
    ];

    /// <summary>
    /// Plays through the complete Ballyhoo walkthrough and verifies
    /// the game reaches a winning state with the maximum score.
    /// </summary>
    [SkippableFact]
    public void Ballyhoo_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "ballyhoo.z3");
        Skip.IfNot(File.Exists(path), "ballyhoo.z3 not found in stories/");

        TestHarness harness;
        try
        {
            harness = TestHarness.Run(path, WalkthroughCommands,
                instructionLimit: 2_000_000_000);
        }
        catch (ZMachineException ex)
        {
            Assert.Fail($"Interpreter crashed: {ex.Message}");
            return;
        }

        var output = harness.Screen.Output;

        // Write output to file for debugging
        var debugPath = Path.Combine(FindRepoRoot(), "tests",
            "ZMachine.Tests", "ballyhoo_output.txt");
        File.WriteAllText(debugPath, output);

        // Check we didn't hit the instruction limit
        Assert.False(harness.HitInstructionLimit,
            $"Hit instruction limit at {harness.InstructionsExecuted} instructions");

        // Verify perfect score (200/200) and winning ending
        Assert.Contains("Your score is 200 of a possible 200", output);
        Assert.Contains("Hip hip hooray!", output);
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
