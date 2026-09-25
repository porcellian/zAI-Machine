namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.Tests.Harness;

public class EnchanterExploreTest
{
    [SkippableFact]
    public void Explore_HoM_Timing()
    {
        var path = Path.Combine(FindRepoRoot(), "stories", "enchante.z3");
        Skip.IfNot(File.Exists(path), "enchante.z3 not found");

        var commands = new List<string>();

        // === PHASES 1-12: Exact walkthrough commands ===

        // Phase 1
        commands.AddRange([
            "ne", "north",
            "take lantern", "take jug",
            "open oven", "take bread",
            "south",
            "memorize frotz", "frotz lantern",
            "ne", "se", "ne", "fill jug"
        ]);
        // Phase 2
        commands.AddRange([
            "sw", "nw", "sw", "sw",
            "se", "se", "ne", "south",
            "gnusto rezrov",
            "sw", "nw", "nw",
            "ne", "ne", "se", "se", "east", "east",
            "memorize rezrov", "rezrov gate", "east"
        ]);
        // Phase 3
        commands.AddRange([
            "south", "south", "east", "south",
            "open door", "north",
            "examine wall", "pull block", "east",
            "take scroll", "gnusto exex"
        ]);
        // Phase 4
        commands.AddRange([
            "west", "south", "up",
            "drop lantern", "east",
            "move lighted portrait",
            "take candle", "take scroll",
            "gnusto ozmoo",
            "drop candle", "west", "take lantern"
        ]);
        // Phase 5 (no drink - save water)
        commands.AddRange(["eat bread"]);
        commands.AddRange([
            "north", "north", "east",
            "memorize ozmoo", "drop all", "east"
        ]);
        commands.AddRange(["ozmoo me", "z"]);
        // Phase 6
        commands.AddRange([
            "down", "west", "take all",
            "west", "south",
            "cut rope with dagger",
            "open box", "take scroll", "gnusto melbor"
        ]);
        // Phase 7
        commands.AddRange([
            "south",
            "eat bread", "drink water",
            "west", "up",
            "get in bed", "sleep",
            "get out of bed",
            "memorize rezrov", "rezrov bedpost",
            "take scroll", "gnusto vaxum"
        ]);
        // Phase 8 (no drink - save water)
        commands.AddRange(["eat bread"]);
        commands.AddRange([
            "down",
            "north", "north", "north", "north",
            "up",
            "memorize rezrov", "rezrov egg",
            "take scroll", "take egg"
        ]);
        // Phase 9
        commands.AddRange([
            "down",
            "east", "east", "east", "east", "east",
            "memorize rezrov", "rezrov gate", "north",
            "take scroll", "gnusto krebf",
            "memorize krebf", "krebf shredded scroll",
            "gnusto zifmia"
        ]);
        // Phase 10
        commands.AddRange([
            "east",
            "memorize nitfol", "nitfol frogs",
            "look under lily pad",
            "take damp scroll", "gnusto cleesh"
        ]);
        // Phase 11
        commands.AddRange([
            "west", "south",
            "memorize melbor", "melbor me",
            "south",
            "examine ashes", "examine tracks",
            "reach in hole",
            "gnusto gondar"
        ]);
        // Phase 12
        commands.AddRange(["eat bread", "drink water"]);
        commands.AddRange([
            "memorize melbor", "melbor me",
            "south", "south", "south", "south", "south",
            "se",
            "memorize nitfol", "nitfol turtle",
            "turtle, follow me",
            "nw", "north", "east", "up"
        ]);
        commands.AddRange([
            "memorize exex", "exex turtle",
            "turtle, go se and get scroll",
            "yell at turtle",
            "take scroll"
        ]);

        // === PHASE 13: Navigate to HoM4, summon adventurer ===
        commands.AddRange(["eat bread", "drink water"]);
        commands.AddRange([
            "down", "west",
            "north", "north",
            "memorize melbor", "melbor me",
            "north", "north", "north",
            "west"
        ]);
        commands.AddRange([
            "memorize zifmia", "memorize vaxum",
            "z", "z", "z",
            "zifmia adventurer",
            "vaxum adventurer",
            "show egg to adventurer"
        ]);

        // === PHASE 14 (FIXED): Guarded Door → Map Room ===
        // Drop egg+dagger only (keep jug for water later)
        // Eat/drink before Map Room to reduce exhaustion
        commands.AddRange([
            "east", "east",                   // North Gate → Guarded Door
            "drop egg", "drop dagger",        // Free weight (keep jug)
            "adventurer, open door",
            "north",                          // Map Room
            "look", "inventory", "score",
            // Drop KULCAD scroll temporarily to free weight
            "drop brittle scroll",
            "take map", "take pencil",
            // Skip FILFRE — can't gnusto it (too powerful)
            // Pick up KULCAD again
            "take brittle scroll",
            "inventory", "score"
        ]);

        // === PHASE 15: Sleep in Bedroom, then Translucent Rooms ===
        // We're exhausted from all the travel. Sleep to reset tiredness
        // before entering Translucent Rooms which require many moves.
        // Map Room → S → GD → W → NG → W×5 → NW Tower → S×2 →
        // IG → S → West Hall → S → SW Tower → up → Bedroom (sleep)
        // Then: down → E → South Hall → S → Dungeon → down
        commands.AddRange([
            "south", "west",                  // GD → North Gate
            "west", "west", "west", "west", "west", // HoM4..1 → NW Tower
            "south", "south",                 // Pebbled Path → Inside Gate
            "south", "south",                 // West Hall → SW Tower
            "up",                             // Bedroom
            "eat bread",
            "get in bed", "sleep",
            "get out of bed",
            "down",                           // SW Tower
            "east", "south",                  // South Hall → Dungeon
            "drop jug",                       // Empty jug — free weight for GUNCHO
            "drop bread",                     // Drop remaining bread too
            "inventory", "score"
        ]);

        // === PHASE 16: Translucent Rooms maze ===
        // Use MELBOR to become invisible to the Terror.
        // At Dungeon, memorize melbor, cast it, then enter Translucent Rooms.
        commands.AddRange([
            "memorize melbor", "melbor me",   // Invisible to Terror
            "down",                           // Translucent Room B
            "look at map",
            "south", "east", "ne", "se",     // B → R → M → K → F (4 moves)
            "draw line from f to p",          // Opens SW passage
            "sw",                             // F → P
            "look",
            "take scroll",                    // GUNCHO
            "ne",                             // P → F
            "nw",                             // F → K
            "sw",                             // K → M
            "west",                           // M → R
            "north",                          // R → B
            "up",                             // B → Dungeon
            "look", "inventory", "score"
        ]);

        // === PHASE 17: Navigate to Winding Stair ===
        // Dungeon → up → South Hall → N → Closet → N → Courtyard(W)
        // → E → Courtyard(C) → MELBOR → E → Temple → E → EC
        // → E → Junction → E → Landing → E → Winding Stair
        commands.AddRange([
            "eat bread", "drink water",
            "up",                             // South Hall
            "north", "north",                 // Closet → Courtyard(W)
            "east",                           // Courtyard(C)
            "memorize melbor", "melbor me",   // Invisible
            "east", "east", "east",           // Temple → EC → Junction
            "look",
            "memorize gondar", "memorize cleesh", "memorize guncho",
            "east", "east",                   // Landing → Winding Stair
            "look", "inventory", "score"
        ]);

        // === PHASE 18: Endgame ===
        commands.AddRange([
            "kulcad stairs",                  // From scroll — dispels illusion
            "look",
            "take scroll",                    // IZYUK scroll revealed
            "izyuk me",                       // From scroll — grants flight
            "look",
            "east",                           // Fly to Krill's chamber
            "look",
            "gondar dragon",                  // Protective circle
            "cleesh being",                   // Neutralize the shape/being
            "guncho krill",                   // Banish Krill
            "look", "score"
        ]);

        RunExplore(path, commands, "enchanter_explore_hom_timing.txt");
    }

    private void RunExplore(string path, List<string> commands, string outputFile)
    {
        var input = new ScriptedInputStream(commands.ToArray());
        var screen = new CaptureScreen();
        var machine = new Interpreter();
        machine.Load(path, input, screen);
        machine.SeedRandom(42);

        try
        {
            int count = 0;
            while (machine.Running && count < 15_000_000)
            {
                machine.Step();
                count++;
            }
        }
        catch (ZMachineException) { }

        File.WriteAllText(
            Path.Combine(FindRepoRoot(), "tests", "ZMachine.Tests", outputFile),
            screen.Output);
    }

    static string FindRepoRoot()
    {
        var d = Directory.GetCurrentDirectory();
        while (d != null) { if (Directory.Exists(Path.Combine(d, ".git"))) return d; d = Directory.GetParent(d)?.FullName; }
        return Directory.GetCurrentDirectory();
    }
}
