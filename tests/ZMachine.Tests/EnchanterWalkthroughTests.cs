using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Enchanter (V3) using a scripted walkthrough to achieve
/// a perfect score of 400/400. Uses seed 42 for deterministic randomization.
/// </summary>
public class EnchanterWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // ================================================================
        // PHASE 1: Supplies from shack, FROTZ lantern, fill jug
        // Fork → NE → Shack → supplies → FROTZ → Brook → fill jug
        // ================================================================
        commands.AddRange([
            "ne", "north",
            "take lantern", "take jug",
            "open oven", "take bread",
            "south",
            "memorize frotz", "frotz lantern",
            "ne", "se", "ne", "fill jug"
        ]);

        // ================================================================
        // PHASE 2: REZROV from the crone, enter castle
        // Brook → back to Fork → Village → Crone → REZROV → castle gate
        // ================================================================
        commands.AddRange([
            "sw", "nw", "sw", "sw",
            "se", "se", "ne", "south",
            "gnusto rezrov",
            "sw", "nw", "nw",
            "ne", "ne", "se", "se", "east", "east",
            "memorize rezrov", "rezrov gate", "east"
        ]);

        // ================================================================
        // PHASE 3: Get EXEX from secret passage
        // Inside Gate → S → West Hall → S → SW Tower → E → South Hall
        // → S → Dungeon → Cell → Secret Passage
        // ================================================================
        commands.AddRange([
            "south", "south", "east", "south",
            "open door", "north",
            "examine wall", "pull block", "east",
            "take scroll", "gnusto exex"
        ]);

        // ================================================================
        // PHASE 4: Get OZMOO from gallery (enter dark)
        // Secret Passage → W → Cell → S → Dungeon → up → South Hall
        // Drop lantern → E → Gallery (dark) → portrait
        // ================================================================
        commands.AddRange([
            "west", "south", "up",
            "drop lantern", "east",
            "move lighted portrait",
            "take candle", "take scroll",
            "gnusto ozmoo",
            "drop candle", "west", "take lantern"
        ]);

        // ================================================================
        // PHASE 5: Temple sacrifice (do this EARLY while still fresh!)
        // South Hall → N → Closet → N → Courtyard(W) → E → Courtyard(C)
        // Memorize OZMOO, drop all, enter Temple → captured → survive
        // ================================================================
        commands.AddRange(["eat bread"]);
        commands.AddRange([
            "north",          // Closet
            "north",          // Courtyard(W)
            "east",           // Courtyard(C)
            "memorize ozmoo",
            "drop all",
            "east"            // Temple → CAPTURED
        ]);
        commands.AddRange(["ozmoo me", "z"]);

        // ================================================================
        // PHASE 6: Post-sacrifice — retrieve items, get MELBOR
        // Altar → down → Temple → W → Courtyard(C) → take all
        // → W → Courtyard(W) → S → Closet → cut rope → MELBOR scroll
        // ================================================================
        commands.AddRange([
            "down",           // Temple (shapes ignore us)
            "west",           // Courtyard(C) — items dropped here
            "take all",
            "west", "south",  // Courtyard(W) → Closet
            "cut rope with dagger",
            "open box",
            "take scroll",
            "gnusto melbor"
        ]);

        // ================================================================
        // PHASE 7: Sleep in Bedroom, get VAXUM
        // Closet → S → South Hall → W → SW Tower → up → Bedroom
        // By now (~85 moves) character should be tired enough to sleep
        // ================================================================
        commands.AddRange([
            "south",          // South Hall
            "eat bread", "drink water",
            "west", "up",     // SW Tower → Bedroom
            "get in bed", "sleep",
            "get out of bed",
            "memorize rezrov", "rezrov bedpost",
            "take scroll", "gnusto vaxum"
        ]);

        // ================================================================
        // PHASE 8: Get egg/shredded scroll from Jewel Room
        // Bedroom → down → SW Tower → N×4 → NW Tower → up → Jewel Room
        // ================================================================
        commands.AddRange(["eat bread"]);
        commands.AddRange([
            "down",
            "north", "north", "north", "north",
            "up",
            "memorize rezrov", "rezrov egg",
            "take scroll", "take egg"
        ]);

        // ================================================================
        // PHASE 9: Get KREBF + ZIFMIA from Forest
        // Jewel Room → down → E×5 → North Gate → REZROV gate → Forest
        // ================================================================
        commands.AddRange([
            "down",
            "east", "east", "east", "east", "east",
            "memorize rezrov", "rezrov gate", "north",
            "take scroll", "gnusto krebf",
            "memorize krebf", "krebf shredded scroll",
            "gnusto zifmia"
        ]);

        // ================================================================
        // PHASE 10: Get CLEESH from Swamp frogs
        // Forest → E → Swamp → NITFOL frogs → lily pad
        // ================================================================
        commands.AddRange([
            "east",
            "memorize nitfol", "nitfol frogs",
            "look under lily pad",
            "take damp scroll", "gnusto cleesh"
        ]);

        // ================================================================
        // PHASE 11: Get GONDAR from Library rat hole
        // Swamp → W → Forest → S → North Gate → MELBOR → S → Library
        // Cast MELBOR before entering Library — shapes patrol this area
        // and will capture visible intruders within ~3 turns
        // ================================================================
        commands.AddRange([
            "west", "south",              // Forest → North Gate
            "memorize melbor", "melbor me", // Invisible before entering Library
            "south",                      // Library (shapes arrive but can't see us)
            "examine ashes", "examine tracks",
            "reach in hole",
            "gnusto gondar"
        ]);

        // ================================================================
        // PHASE 12: Shore → turtle → SE Tower → Engine Room
        // Library → S → Junction (MELBOR first!) → S×3 → South Gate
        // → S → Meadow → SE → Beach → NITFOL turtle → follow
        // → back → SE Tower → up → Engine Room → EXEX turtle puzzle
        // ================================================================
        commands.AddRange(["eat bread", "drink water"]);
        commands.AddRange([
            "memorize melbor", "melbor me",
            "south",          // Junction (invisible — shapes ignore us)
            "south",          // Banquet Hall
            "south",          // East Hall
            "south",          // South Gate
            "south",          // Meadow
            "se",             // Beach (turtle!)
            "memorize nitfol", "nitfol turtle",
            "turtle, follow me",
            "nw",             // Meadow
            "north",          // South Gate
            "east",           // SE Tower base
            "up"              // Engine Room
        ]);

        // EXEX enlarges the turtle so it can survive the Engine Room
        // hazards. Send it SE to fetch the KULCAD scroll, then yell
        // to overcome the noise so it hears and returns.
        commands.AddRange([
            "memorize exex", "exex turtle",
            "turtle, go se and get scroll",
            "yell at turtle",
            "take scroll"     // KULCAD — too powerful to gnusto, keep as scroll
        ]);

        // ================================================================
        // PHASE 13: Adventurer at Hall of Mirrors
        // Engine Room → down → SE Tower → W → South Gate → N×3
        // → Junction → Library → North Gate → W → HoM4
        // MELBOR before Junction (shapes), then summon adventurer
        // ================================================================
        commands.AddRange(["eat bread", "drink water"]);
        commands.AddRange([
            "down",           // SE Tower
            "west",           // South Gate
            "north", "north", // East Hall → Banquet Hall
            "memorize melbor", "melbor me",
            "north",          // Junction (invisible)
            "north",          // Library (invisible)
            "north",          // North Gate
            "west"            // HoM4
        ]);

        // With seed 42, adventurer appears on 3rd z-wait after memorize.
        // ZIFMIA is consumed even on failure, so don't waste it — wait
        // until adventurer is visible, then summon, befriend, show egg.
        commands.AddRange([
            "memorize zifmia", "memorize vaxum",
            "z", "z", "z",                   // Adventurer appears on 3rd z
            "zifmia adventurer",              // Summon through mirror
            "vaxum adventurer",               // Befriend
            "show egg to adventurer"          // Motivate treasure-hunter to follow
        ]);

        // ================================================================
        // PHASE 14: Go to Guarded Door → Map Room
        // Don't drop egg/dagger — keep adventurer light (2 items) so he
        // picks up BOTH pencil and map on entry. Taking from adventurer
        // bypasses the floor weight check. FILFRE can't be gnusto'd.
        // Adventurer leaves after giving map — wait one turn for return.
        // ================================================================
        commands.AddRange([
            "east", "east",               // North Gate → Guarded Door
            "adventurer, open door",
            "north",                      // Map Room (adventurer picks up pencil+map)
            "take map",                   // Adventurer gives map, then leaves
            "z",                          // Wait — adventurer returns
            "take pencil",                // Adventurer gives pencil
            "drop egg", "drop dagger"     // Lighten load, keep jug for one more drink
        ]);

        // ================================================================
        // PHASE 15: Navigate to Translucent Rooms
        // Map Room → S → GD → W → NG → W×5 → NW Tower → S×2 → IG
        // → S×2 → SW Tower → E → South Hall → S → Dungeon → down
        // Route avoids shape-patrolled areas (Library/Junction)
        // ================================================================
        commands.AddRange([
            "south", "west",
            "west", "west", "west", "west", "west",
            "south", "south",
            "south", "south",
            "east", "south",
            "eat bread", "drink water",
            "down"
        ]);

        // ================================================================
        // PHASE 16: Translucent maze — navigate with map/pencil
        // Map layout (from "look at map"):
        //   B       J          B=entry(up→Dungeon)
        //   !      / \         P=GUNCHO scroll (isolated)
        //   !     /   \        Draw F→P to reach it
        //   !   K       V      Erase B→R, M→V to trap terror
        //   !          / \     Draw J→B for escape route
        //   R-------M   F     Return: P→F→V→J→B→up
        //    \     /
        //     H       P
        // Drawing F→P awakens the Unseen Terror, which blocks movement
        // for 1 turn. Wait for it to pass, then go SW to P.
        // Erase trap lines from P (away from Terror), draw escape, return.
        // ================================================================
        commands.AddRange([
            "look at map",
            "south", "east", "ne", "se",     // B→R→M→V→F
            "draw line from f to p",          // Opens SW, awakens Terror
            "z",                              // Wait 1 turn for Terror to pass
            "sw",                             // F→P
            "take scroll", "gnusto guncho",   // GUNCHO scroll at P
            "erase line from b to r",         // Trap Terror (we're safe at P)
            "erase line from m to v",         // Seal more exits
            "draw line from j to b",          // Create escape: J→B
            "ne", "nw", "nw", "west",        // P→F→V→J→B
            "up"                              // B→Dungeon
        ]);

        // ================================================================
        // PHASE 17: Navigate to Winding Stair for endgame
        // Dungeon → up → South Hall → W → SW Tower → up → Bedroom
        // Sleep to reset exhaustion, then navigate to Winding Stair.
        // ================================================================
        commands.AddRange([
            "up",                             // South Hall
            "west", "up",                     // SW Tower → Bedroom
            "get in bed", "sleep",
            "get out of bed",
            "down",                           // SW Tower
            "east",                           // South Hall
            "north", "north",                 // Closet → Courtyard(W)
            "east",                           // Courtyard(C)
            "memorize melbor", "melbor me",   // Invisible for Temple/Junction
            "east", "east", "east",           // Temple → EC → Junction
            "memorize gondar", "memorize cleesh", "memorize guncho",
            "east", "east"                    // Landing → Winding Stair
        ]);

        // ================================================================
        // PHASE 18: Endgame — KULCAD stairs, IZYUK fly, defeat Krill
        // Cast KULCAD from physical scroll (too powerful to gnusto).
        // IZYUK scroll revealed beneath, grants flight.
        // ================================================================
        commands.AddRange([
            "kulcad stairs",                  // From scroll — dispels illusion
            "take scroll",                    // IZYUK scroll revealed
            "izyuk me",                       // From scroll — grants flight
            "east",                           // Fly to Krill's chamber
            "gondar dragon",                  // Protective circle
            "cleesh being",                   // Neutralize the shape/being
            "guncho krill"                    // Banish Krill — victory!
        ]);

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
