using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Enchanter (V3) using a scripted walkthrough to achieve a
/// perfect score of 400/400. Uses seed 42 so that timed events — the
/// guards' capture, the sacrifice, and the adventurer's appearances in the
/// Hall of Mirrors — occur on predictable turns. Exercises spell memory,
/// sleep/fatigue, NPC commands ("turtle, go se. take scroll. go nw"),
/// one-shot scroll casting, and the full game arc to Krill's defeat.
/// </summary>
public class EnchanterWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Enchanter with seed 42.
    /// Several steps are turn-sensitive (the waits before the sacrifice and
    /// before summoning the adventurer), so removing or adding any command
    /// that consumes a turn can desynchronise the later sections.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === Shack, water, light, and REZROV from the crone ===
        commands.AddRange(["verbose",
            "ne", "north",
            "take lantern", "take jug", "open oven", "take bread",
            "south",
            "memorize frotz", "frotz lantern",           // +20 making light
            "ne", "se", "ne",
            "fill jug", "drink water",                   // +15 first drink
            "sw", "nw", "sw", "sw",
            "se", "se", "ne", "south",                   // crone's hovel
            "gnusto rezrov",
            "sw", "nw", "nw", "ne", "ne", "se", "se",
            "east", "east",
            "memorize rezrov", "rezrov gate",            // +20 iron gate
            "east"]);

        // === VAXUM (bedpost) and OZMOO (behind the lighted portrait) ===
        commands.AddRange(["south", "south", "up",
            "memorize rezrov", "rezrov bedpost",
            "take gold leaf scroll", "gnusto vaxum",     // +20
            "down", "east", "east", "west",
            // The lighted portrait is only visible when the Gallery is dark.
            "drop lantern", "east",
            "look behind lighted portrait",
            "take black scroll", "gnusto ozmoo",         // +25
            "west", "take lantern"]);

        // === Egg, KREBF, ZIFMIA, CLEESH ===
        commands.AddRange(["west", "north", "north", "north", "north", "up",
            "memorize rezrov", "rezrov egg",             // +10
            "take shredded scroll", "take egg",
            "down", "east",
            "eat bread",                                 // +10 first meal
            "east", "east", "east", "east",
            "memorize rezrov", "rezrov gate",
            "north",
            "take crumpled scroll", "gnusto krebf",
            "memorize krebf", "krebf shredded scroll",   // +5 → faded (zifmia) scroll
            "drink water",
            "east",
            "look under lily pad", "gnusto cleesh"]);

        // === First night in the Bedroom ===
        commands.AddRange(["west", "south",
            "west", "west", "west", "west", "west",
            "south", "south", "south", "south", "up",
            "get in bed", "get out of bed"]);

        // === GONDAR and surviving the sacrifice ===
        // OZMOO must be cast before capture: the cell blocks all magic.
        commands.AddRange(["memorize ozmoo",
            "down", "north", "north", "north", "north",
            "east", "east", "east", "east", "east",
            "drop egg", "drop faded scroll",
            "ozmoo me",
            "south",
            "follow tracks", "reach into hole",          // +25 gondar scroll
            "wait", "wait", "wait",                      // captured, sacrificed: +35
            "down",
            "open south door", "south", "take all",      // confiscated possessions
            "gnusto gondar",
            "drink water", "eat bread"]);

        // === MELBOR (jewelled box) and EXEX (dungeon cell) ===
        commands.AddRange(["north", "west", "west", "south",
            "cut rope with dagger", "open box",          // +25
            "drop dagger",
            "take vellum scroll", "gnusto melbor",
            "south", "south",
            "open door", "north",
            "examine graffiti", "move block",
            "east",
            "take stained scroll", "gnusto exex",
            "take spoon",
            "west", "south", "up"]);

        // === KULCAD via the turtle ===
        commands.AddRange(["east", "east", "south", "se",
            "drink water",
            "memorize nitfol", "memorize exex",
            "nitfol turtle", "turtle, follow me",
            "nw", "north", "east", "up",
            "exex turtle",
            "turtle, go se. take scroll. go nw",         // +25
            "take brittle scroll",
            "eat bread"]);

        // === Second night ===
        commands.AddRange(["down", "west", "west", "west", "west", "up",
            "get in bed", "get out of bed"]);

        // === The adventurer and the Guarded Door ===
        commands.AddRange(["memorize vaxum",
            "down", "north", "north", "north", "north",
            "east", "east", "east", "east", "east",
            "take faded scroll",
            "east", "drop spoon",                        // treasure lures him to the door
            "west", "west",
            "wait",                                      // he appears in the "mirror"
            "zifmia adventurer",                         // +10
            "vaxum adventurer",
            "east", "east",
            "adventurer, open door",
            "north",                                     // +35 behind the door
            "drink water", "drop jug",
            "take map", "take pencil",
            "eat bread"]);

        // === GUNCHO from the Translucent Rooms ===
        commands.AddRange(["south",
            "west", "west", "west", "west", "west", "west",
            "south", "south", "south", "south",
            "east", "down", "down",
            "south", "east",                             // room M
            "draw line between p and f",
            "draw line between m and p",
            "erase line between m and v",
            "erase line between p and f",
            "se",
            "drop pencil", "drop map",
            "take powerful scroll",
            "nw", "west", "north",                       // +50 Terror re-trapped
            "up", "up"]);

        // === Winding Stair and Krill ===
        commands.AddRange(["east", "east",
            "memorize melbor", "melbor me",              // guards ignore us now
            "north", "north", "north",
            "memorize gondar", "memorize vaxum",
            "east", "east",
            "kulcad stair",                              // +20 stair illusion
            "izyuk me",
            "east",
            "gondar dragon",
            "vaxum monster",
            "guncho krill"]);                            // +50

        return commands.ToArray();
    }

    [SkippableFact]
    public void Enchanter_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "enchante.z3");
        Skip.IfNot(File.Exists(path), "enchante.z3 not found in stories/");

        TestHarness harness;
        try
        {
            harness = TestHarness.Run(path, BuildWalkthroughCommands(),
                instructionLimit: 15_000_000, randomSeed: 42);
        }
        catch (ZMachineException ex)
        {
            Assert.Fail($"Interpreter crashed: {ex.Message}");
            return;
        }

        var output = harness.Screen.Output;

        var debugPath = Path.Combine(FindRepoRoot(), "tests",
            "ZMachine.Tests", "enchanter_walkthrough_output.txt");
        File.WriteAllText(debugPath, output);

        Assert.Contains("Your score is 400 of a possible 400", output);
        Assert.Contains("Member of the Circle of Enchanters", output);
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
