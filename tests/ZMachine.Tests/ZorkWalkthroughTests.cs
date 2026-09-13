using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Zork I: The Great Underground Empire using a walkthrough
/// to collect all 19 treasures and enter the stone barrow for 350/350.
/// Tests full-game interpreter correctness across a much larger story file
/// than Ballyhoo, covering combat, inventory management, complex puzzles,
/// timed events, and the full V3 parser.
/// </summary>
public class ZorkWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    private static readonly string[] WalkthroughCommands =
    [
        // === A) The Jeweled Egg ===
        "north", "north", "climb tree", "take egg", "down",

        // === B) Enter house, get sword + lantern only ===
        "south", "east", "open window", "west",
        "west",
        "open case",
        "take sword", "take lantern",
        "move rug", "open trap door", "down",
        "turn lantern on",
        "north",
        "kill troll with sword", "kill troll with sword",
        "kill troll with sword", "kill troll with sword",
        "kill troll with sword", "kill troll with sword",
        "kill troll with sword", "kill troll with sword",
        "kill troll with sword", "kill troll with sword",
        "kill troll with sword", "kill troll with sword",
        "kill troll with sword", "kill troll with sword",
        "kill troll with sword",

        // === C) Dead Adventurer (Maze) ===
        // Carrying: egg, sword, lantern (3 items — light enough even if wounded)
        "west", "south", "east", "up",
        "take coins",
        "southwest", "east", "south", "southeast",

        // === D) Cyclops ===
        "odysseus",
        "up",
        "give egg to thief",
        "down",
        "east", "east",
        "put egg in case",
        "put coins in case",
        "turn lantern off",

        // === E) Exorcism — get rope and bottle first ===
        "east",
        "take bottle",
        "turn lantern on",
        "up",
        "take rope",
        "turn lantern off",
        "down",
        "west",
        "open trap door",
        "down",
        "turn lantern on",
        "north",
        "east",
        "north",
        "northeast",
        "east",
        "north",
        "take matchbook",
        "south",
        "south",
        "down",
        "west",
        "southeast",
        "east",
        "tie rope to railing",
        "down",
        "south",
        "take bell",
        "south",
        "take candles", "take book",
        "down",
        "down",
        // Exorcism ritual
        "open matchbook",
        "ring bell",
        "take candles",
        "light match",
        "light candles with match",
        "wave candles",
        "read book",
        // Aftermath
        "pour water on bell",
        "take bell",
        "extinguish candles",
        "drop bottle",
        // Land of Living Dead
        "south",
        "take skull",
        // Return to Cellar
        "north", "up", "north", "north", "north",
        "west", "west", "south",
        // Drop heavy items in Cellar
        "drop bell", "drop candles", "drop matchbook",

        // === F) Quick Detour — Painting ===
        "south", "east",
        "take painting",
        "west", "north",
        "up",
        "put painting in case",
        "put skull in case",
        "put sword in case",
        "drop book",

        // === H) Treasure Chest in Reservoir ===
        // (Moved before thief fight to increase score and weaken thief)
        "down",
        "take candles", "take matchbook",
        "north",
        "east",
        "north",
        "northeast",
        "east",
        "north", "north",
        "push red button",
        "turn lantern off",
        "take wrench", "take screwdriver",
        "push yellow button",
        "south", "south",
        "turn bolt with wrench",
        "wait", "wait", "wait", "wait", "wait",
        "west",
        "turn lantern on",
        "north",
        "drop all but lantern",
        "north", "north",
        "take trident",
        "south", "south",
        "take trunk",
        "south",
        "southwest", "southwest",
        "west", "south",
        "up",
        "put all in case",

        // === I) Coal Mines ===
        "take lantern",
        "east",
        "open sack", "take garlic",
        "west",
        "down",
        "north", "east", "north", "northeast",
        "north",
        "take wrench", "take screwdriver", "take candles", "take matchbook",
        "north", "north", "up",
        "north", "north",
        "west", "north", "west", "north",
        "take figurine",
        "east",
        "put screwdriver in basket",
        "drop matchbook", "drop candles",
        "north", "down",
        "take bracelet",
        "east", "northeast", "southeast", "southwest",
        "down", "down",
        "west",
        "drop all but lantern",
        "east", "south",
        "take coal",
        "north", "up", "up",
        "north", "east", "south",
        "north", "up", "south",
        "take candles", "take matchbook",
        "light match",
        "light candles with match",
        "put candles in basket",
        "put coal in basket",
        "lower basket",
        "north", "down",
        "east", "northeast", "southeast", "southwest",
        "down", "down",
        "west",
        "drop all",
        "west",
        "take candles", "take coal", "take screwdriver",
        "south",
        "open lid",
        "put coal in machine",
        "close lid",
        "turn switch with screwdriver",
        "open lid",
        "take diamond",
        "north",
        "put candles in basket",
        "put diamond in basket",
        "put screwdriver in basket",
        "east",
        "take all but timber",
        "east", "up", "up",
        "north", "east", "south",
        "north", "up", "south",
        "raise basket",
        "take candles", "take diamond",
        "west", "south", "east", "south",
        "down",
        "up",
        "put all in case",

        // === J) Eerie Silence (Loud Room) ===
        "take lantern", "take wrench", "take candles",
        "down",
        "north", "east",
        "north", "northeast", "east",
        "turn bolt with wrench",
        "south", "down",
        "take bar",
        "west",
        "southeast", "east", "down",
        "turn lantern off",
        "drop lantern", "drop candles",
        "take torch",
        "south", "east",
        "open coffin",
        "take sceptre",
        "west", "south", "down",
        "north", "north", "north",
        "west", "west", "south",
        "up",
        "put all but sceptre in case",

        // === K) End of the Rainbow ===
        "take torch", "take sword",
        "east", "east", "east", "east",
        "down", "down", "north",
        "wave sceptre",
        "take pot",
        "east", "east",
        "north", "north",
        "take shovel",
        "northeast",
        "dig in sand with shovel", "dig in sand with shovel",
        "dig in sand with shovel", "dig in sand with shovel",
        "take scarab",
        "southwest",
        "south", "south",
        "west", "west",
        "southwest",
        "up", "up",
        "northwest",
        "west", "west", "west",
        "put all but torch in case",

        // === G) Fighting the Thief ===
        // (Moved after H-K: more treasures in case weakens the thief)
        // Heal fully before fight
        "wait", "wait", "wait", "wait", "wait",
        "wait", "wait", "wait", "wait", "wait",
        "wait", "wait", "wait", "wait", "wait",
        "take sword", "take coins", "take skull",
        "west", "west",
        "up",
        // Distract with coins, then attack
        "give coins to thief",
        "kill thief with sword",
        // Distract with skull, then attack
        "give skull to thief",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "take sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "take sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "take sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "take sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "take sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        "kill thief with sword",
        // Collect loot from lair
        "take all",
        "drop sword",
        "take chalice",
        "down", "east", "east",
        "put all in case",
        // Canary + Bauble
        "take canary",
        "east", "east", "north", "north",
        "up",
        "wind canary",
        "down",
        "take bauble",
        "south", "east",
        "west", "west",
        "put all in case",

        // === L) Row Your Boat ===
        "take torch", "take wrench",
        // Reopen dam sluice to drain reservoir (closed in section J)
        "down",
        "north", "east",
        "north", "northeast", "east",
        "turn bolt with wrench",
        "wait", "wait", "wait", "wait", "wait",
        // Get pump from Reservoir North
        "west",
        "north", "north",
        "take pump",
        // Get plastic from Dam Base
        "south", "south", "east",
        "down",
        "take plastic",
        // Navigate to White Cliffs Beach
        "up", "south", "down",
        "east", "east",
        "drop all but pump and torch",
        "inflate plastic with pump",
        "get in boat",
        "launch",
        "wait",
        "take buoy",
        "east",
        "open buoy",
        "take emerald",
        "drop buoy",
        "get out of boat",
        "deflate boat",
        "take plastic",
        "south", "south",
        "west", "west",
        "southwest",
        "up", "up",
        "northwest",
        "west", "west", "west",
        "put emerald in case",
        // Get items back from White Cliffs Beach
        "down",
        "north", "east", "east", "east", "east", "east",
        "take all but label",

        // === M) Ramses Coffin ===
        "west", "west", "west",
        "southeast", "east", "down",
        "south", "east",
        "drop all but torch",
        "take coffin",
        "west",
        "south",
        "pray",
        "east", "south", "east",
        "west", "west",
        "put all in case",

        // === O) Into the Tomb ===
        "take parchment",
        "read parchment",
        "east", "east",
        "southwest", "northwest", "southwest",
        "in",
    ];

    [SkippableFact]
    public void Zork_Walkthrough()
    {
        var path = Path.Combine(StoriesDir, "zork1.z3");
        Skip.IfNot(File.Exists(path), "zork1.z3 not found in stories/");

        TestHarness harness;
        try
        {
            harness = TestHarness.Run(path, WalkthroughCommands,
                instructionLimit: 100_000_000,
                randomSeed: 8);
        }
        catch (ZMachineException ex)
        {
            Assert.Fail($"Interpreter crashed: {ex.Message}");
            return;
        }

        var output = harness.Screen.Output;

        var debugPath = Path.Combine(FindRepoRoot(), "tests",
            "ZMachine.Tests", "zork_output.txt");
        File.WriteAllText(debugPath, output);

        Assert.False(harness.HitInstructionLimit,
            $"Hit instruction limit at {harness.InstructionsExecuted} instructions");

        Assert.DoesNotContain("You have died", output);
        Assert.Contains("Master Adventurer", output);
        Assert.Contains("Your score is 350 (total of 350 points)", output);
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
