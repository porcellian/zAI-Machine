using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Sorcerer (V3) using a scripted walkthrough to achieve a
/// perfect score of 400/400. Uses seed 42, which fixes several random
/// elements the script depends on: the trunk code ("orc"), the coal mine
/// dial combination (3), and the casino jackpot on the 25th pull. Exercises
/// spell memory limits, fatigue and forced sleep, time travel (the younger
/// self replays the player's recorded commands), and the full game arc to
/// the exorcism of Jeearr.
/// </summary>
public class SorcererWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Sorcerer with seed 42.
    /// Many sections are turn-sensitive (river flood, glass maze flights,
    /// the coal mine vilstu budget and time loop), so adding or removing
    /// any command that consumes a turn can break later sections.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === Guild Hall: light, mail-order vilstu, gaspar, amulet, journal ===
        commands.AddRange(["verbose", "wait", "frotz spell book", "get up", "west", "south",
            "south", "west", "take matchbook", "east", "open receptacle",
            "put matchbook in receptacle", "close receptacle", "north", "west",
            "take scroll", "gnusto gaspar", "east", "north", "west", "open desk",
            "look behind hanging", "take key", "take all from desk",
            "open journal", "read journal", "open tiny box", "take amulet",
            "drop key", "drop tiny box", "drop journal", "drop infotater", "east"]);

        // === Guild Hall: meef, berzio, vilstu delivery, trunk (seed-42 code "orc" = red, gray, purple, gray, red) ===
        commands.AddRange(["south", "south", "east", "take scroll", "gnusto meef", "west", "west",
            "take ochre vial", "open ochre vial", "drink ochre potion",
            "drop ochre vial", "east", "open receptacle", "take orange vial",
            "read orange vial", "down", "push red button", "push gray button",
            "push purple button", "push gray button", "push red button",
            "take moldy scroll"]);

        // === Aimfiz to Belboz; outrun the hellhound; fort (fooble) and river cave (fweep, guano) ===
        commands.AddRange(["learn gaspar", "learn yomin", "learn izyuk", "learn pulver", "up",
            "gaspar", "aimfiz belboz", "ne", "east", "ne", "se", "east",
            "lower flag", "examine flag", "take aqua vial", "west", "nw",
            "pulver river", "down", "ne", "take all", "gnusto fweep", "down", "sw",
            "down", "west", "sw", "sw"]);

        // === Crater; sleep; fly past the eroding river bank to put guano in the cannon (yonk) ===
        commands.AddRange(["north", "sleep", "up", "up", "learn izyuk", "east", "izyuk me", "ne",
            "se", "east", "east", "put guano in cannon", "look in cannon",
            "take ordinary scroll", "read ordinary scroll", "gnusto yonk", "west",
            "west", "learn izyuk", "izyuk me", "nw", "sw", "west", "down", "down",
            "south"]);

        // === Zorkmid tree across the chasm; Bozbarland (fooble -> malyon prize) ===
        commands.AddRange(["learn izyuk", "west", "izyuk me", "west", "west", "north",
            "take coin", "south", "east", "learn izyuk", "izyuk me", "east",
            "east", "south", "sw", "west", "give coin to gnome", "west", "west",
            "south", "open aqua vial", "drink aqua potion", "take ball",
            "throw ball at bunny", "gnusto malyon", "drop aqua vial", "north"]);

        // === Casino jackpot on pull 25 (seed 42) pays the toll gnome ===
        commands.AddRange(["west", "pull lever", "pull lever", "pull lever", "pull lever",
            "pull lever", "pull lever", "pull lever", "pull lever", "pull lever",
            "pull lever", "pull lever", "pull lever", "pull lever", "pull lever",
            "pull lever", "pull lever", "pull lever", "pull lever", "pull lever",
            "pull lever", "pull lever", "pull lever", "pull lever", "pull lever",
            "pull lever", "take coin", "east", "east", "east", "ne", "north", "ne",
            "ne", "east", "east", "wake gnome", "give coin to gnome", "east",
            "east"]);

        // === Glass maze by bat (fweep): swanzo scroll down the chimney; lure the dorn beast into a hole ===
        commands.AddRange(["wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait", "wait",
            "wait", "sleep", "north", "north", "learn fweep", "learn fweep",
            "learn fweep", "learn fweep", "learn fweep", "fweep", "east", "north",
            "east", "south", "south", "west", "down", "east", "east", "north",
            "north", "wait", "wait", "fweep", "up", "up", "south", "east", "wait",
            "wait", "wait", "wait", "wait", "take scroll", "put scroll in hole",
            "fweep", "west", "west", "south", "east", "down", "down", "west",
            "west", "wait", "wait", "wait", "fweep", "up", "up", "north", "north",
            "down", "east", "south", "east", "north", "down", "west", "south",
            "west", "wait", "fweep", "up", "west", "wait", "wait", "wait", "wait",
            "wait", "wait", "take all", "south", "south", "east",
            "drop ordinary scroll", "take parchment scroll", "gnusto swanzo",
            "take ordinary scroll", "sleep"]);

        // === Yonk + malyon animate the dragon carving; into the coal mine ===
        commands.AddRange(["west", "west", "west", "west", "west", "sw", "sw", "south", "south",
            "learn malyon", "yonk malyon", "malyon dragon", "south"]);

        // === Coal mine time loop: vilstu, give book to twin, rope + beam, golmac, tell younger self the combination ===
        commands.AddRange(["frotz amulet", "learn meef", "learn meef", "learn swanzo",
            "drop amber vial", "open orange vial", "drink orange potion", "east",
            "drop orange vial", "look", "give book to twin", "east",
            "turn dial to 3", "open door", "east", "take rope", "up", "ne",
            "north", "sw", "take beam", "nw", "west", "tie rope to beam",
            "put beam across chute", "put rope in chute", "climb down rope",
            "take scroll", "golmac me", "open lamp", "take smelly scroll", "east",
            "younger self, the combination is 3", "wait", "down"]);

        // === Lagoon crate (grue suit), vines, grue lair ===
        commands.AddRange(["gnusto vardik", "wait", "wait", "learn meef", "learn meef",
            "drop book", "east", "down", "meef spenseweeds", "open crate",
            "take suit and repellent", "up", "west", "take book", "ne", "north",
            "meef vines", "west", "west"]);

        // === Belboz: vardik shields the mind, swanzo exorcises Jeearr ===
        commands.AddRange(["learn vardik", "learn swanzo", "open white door", "vardik me",
            "swanzo belboz"]);

        return commands.ToArray();
    }

    [SkippableFact]
    public void Sorcerer_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "sorcerer.z3");
        Skip.IfNot(File.Exists(path), "sorcerer.z3 not found in stories/");

        TestHarness harness;
        try
        {
            harness = TestHarness.Run(path, BuildWalkthroughCommands(),
                instructionLimit: 25_000_000, randomSeed: 42);
        }
        catch (ZMachineException ex)
        {
            Assert.Fail($"Interpreter crashed: {ex.Message}");
            return;
        }

        var output = harness.Screen.Output;

        var debugPath = Path.Combine(FindRepoRoot(), "tests",
            "ZMachine.Tests", "sorcerer_walkthrough_output.txt");
        File.WriteAllText(debugPath, output);

        Assert.Contains("Your score is 400 of a possible 400", output);
        Assert.Contains("Leader of the Circle of Enchanters", output);
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
