using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Sherlock: The Riddle of the Crown Jewels (V5) using a
/// scripted walkthrough to achieve a perfect score of 100/100. Uses seed 42
/// for deterministic randomization of pill color (yellow/slow heartbeat),
/// opal password ("Swordfish"), and Mycroft's Tower password ("Cleves").
/// Tests ReadChar-based prompts (press any key, dawn/dusk Y/N), timed
/// waiting sequences, and the full game arc across Victorian London.
/// </summary>
public class SherlockWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Sherlock with seed 42.
    /// ReadChar prompts: " " for "Press any key", "n" for Y/N dawn/dusk.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === Holmes' House ===
        commands.Add(" ");                              // ReadChar: "Press any key"
        commands.AddRange(["knock on door",
            "up", "north",
            "take newspaper",
            "show newspaper to holmes",
            "examine slipper",
            "get pipe", "get knife", "get tobacco",
            "read clue paper",
            "z",                                        // visitor leaves, Holmes departs
            "n",                                        // ReadChar: "continue waiting?" → no
            "west",
            "take magnifying glass", "take lamp", "take ampoule",
            "east", "south", "down", "north",
            "take matchbook",
            "south",
            "open front door",
            "out"]);

        // === Get Clues: Museum ===
        commands.AddRange(["blow whistle", "blow whistle",
            "get in cab",
            "great russell street",
            "get out",
            "z", "z", "z", "z", "z", "z",
            "n",                                        // ReadChar: dawn Y/N
            "read sign",
            "z", "z", "z", "z", "z",
            "z", "z", "z", "z", "z",
            "n",                                        // ReadChar: possible Y/N
            "enter museum",
            "east",
            "open old book",
            "tell librarian to shut up",
            "open old book",
            "read old book"]);

        // === Westminster Abbey ===
        commands.AddRange(["west", "south", "southwest",
            "south", "south", "south",
            "southwest", "east",
            "south", "southeast",
            "get pacquet", "get crayon",
            "northwest",
            "open door",
            "south", "west",
            "read sign",
            "east", "north", "north",
            "examine tomb",
            "open pacquet",
            "take brown paper",
            "put brown paper on tomb",
            "rub paper with crayon",
            "take brown paper",
            "east", "north", "north",
            "look",
            "heat brown paper over candles",
            "read back of brown paper",
            "south", "east",
            "examine tomb",
            "take blue paper",
            "put blue paper on tomb",
            "rub blue paper with crayon",
            "take blue paper",
            "south", "west",
            "take white paper",
            "examine henry tomb",
            "put white paper on it",
            "rub white paper with crayon",
            "take white paper",
            "east", "north", "west", "north",
            "heat white paper over candles",
            "heat blue paper over candles",
            "read back of white paper",
            "read back of blue paper",
            "drop brown paper", "drop blue paper", "drop white paper",
            "drop pacquet", "drop crayon",
            "south", "south",
            "west",
            "west",
            "look"]);

        // === Emerald: Madame Tussaud's ===
        commands.AddRange(["blow whistle", "blow whistle",
            "get in cab", "marylebone road", "get out",
            "drop lamp",
            "put tobacco in pipe",
            "open matchbook", "get match",
            "light match", "light pipe with match",
            "drop match",
            "north",
            "ask holmes about ash",
            "west",
            "look at statues",
            "examine guy fawkes",
            "take torch",
            "light newspaper with pipe",
            "light torch with newspaper",
            "examine charles",
            "take head",
            "melt head with torch",
            "take emerald",
            "examine emerald",
            "look at emerald with magnifying glass",
            "east", "south",
            "take lamp", "light lamp",
            "east",
            "douse lamp"]);

        // === Sapphire: Whitehall / Big Ben ===
        commands.AddRange(["blow whistle", "blow whistle",
            "get in cab", "whitehall", "get out",
            "east",
            "light lamp",
            "down",
            "look in rowboat", "take oar",
            "up",
            "douse lamp",
            "west", "south", "west",
            "haggle with vendor", "haggle with vendor",
            "buy telescope",
            "east", "southeast", "up",
            "open bag",
            "open blue bottle", "open brown bottle",
            "take cotton balls",
            "wear cotton balls",
            "z",
            "n",                                        // ReadChar: Big Ben Y/N
            "get sapphire",
            "get sapphire",
            "get sapphire",
            "look at sapphire with magnifying glass",
            "down", "northwest",
            "douse lamp",
            "remove cotton balls",
            "drop cotton balls", "drop matchbook"]);

        // === Ruby: Covent Garden ===
        // Seed 42: slow heartbeat → yellow pill (brown bottle)
        commands.AddRange(["blow whistle", "blow whistle",
            "get in cab", "covent garden", "get out",
            "remove hat", "drop hat",
            "take stethoscope",
            "wear stethoscope",
            "listen to girl",
            "open brown bottle",
            "take yellow pill",
            "give yellow pill to girl",
            "remove stethoscope",
            "put stethoscope in hat",
            "get hat", "wear hat",
            "north", "east", "south", "west",
            "ask for pigeon",
            "east",
            "blow whistle", "blow whistle",
            "get in cab", "trafalgar square", "get out",
            "look at statue",
            "examine statue with telescope",
            "show ruby to pigeon",
            "tell pigeon to get ruby",
            "throw pigeon",
            "drop telescope",
            "blow whistle", "blow whistle",
            "get in cab", "pinchin lane", "get out",
            "enter shop",
            "ask for pigeon",
            "look at ruby with magnifying glass",
            "exit"]);

        // === Opal: Embankment / London Bridge ===
        // Wait for high tide (~8:45pm, 12-hour cycle from 8:45am)
        commands.AddRange(["blow whistle", "blow whistle",
            "get in cab", "the embankment", "get out",
            "get in boat",
            "put oar in oarlock",
            "raise anchor",
            "launch boat",
            "row east", "row east",
            "drop anchor",
            "examine london bridge",
            "get moss"]);
        // 36 z's before dusk (~6:47pm)
        for (int i = 0; i < 36; i++) commands.Add("z");
        // 6 z's — one triggers dusk Y/N at ~7:30pm
        for (int i = 0; i < 6; i++) commands.Add("z");
        commands.Add("n");                              // ReadChar: dusk Y/N
        commands.Add("light lamp");
        // 3 z's toward 8pm
        commands.AddRange(["z", "z", "z"]);
        commands.Add("n");                              // ReadChar: possible darkness Y/N
        // 5 z's to ~8:45pm high tide
        for (int i = 0; i < 5; i++) commands.Add("z");
        commands.AddRange(["get moss",
            "get moss",
            "look at opal with magnifying glass",
            "raise anchor",
            "row west", "row west",
            "land boat",
            "get out"]);

        // === Topaz: Monument / Bank of England ===
        commands.AddRange(["blow whistle", "blow whistle",
            "get in cab", "the monument", "get out",
            "read plaque",
            "northwest", "northwest",
            "look at urchin",
            "give shilling to wiggins",
            "ask wiggins to steal key",
            "north",
            "give emerald to guard",
            "give sapphire to guard",
            "give ruby to guard",
            "give opal to guard",
            "enter bank",
            "examine vault door",
            "remove hat", "get stethoscope",
            "wear stethoscope", "listen to dial",
            "turn dial right",
            "turn dial right",
            "turn dial left",
            "turn dial right",
            "turn dial right",
            "remove stethoscope", "drop stethoscope",
            "enter vault",
            "unlock box 600 with key",
            "open box 600",
            "take topaz",
            "look at topaz with magnifying glass",
            "leave vault",
            "leave bank"]);

        // === Garnet: Diogenes Club + Tower of London ===
        // Seed 42: Mycroft's password is "Cleves"
        commands.AddRange(["west", "west", "west",
            "south", "west",
            "ask for mycroft",
            "give ring to butler",
            "east",
            "blow whistle", "blow whistle",
            "get in cab", "tower of london", "get out",
            "east", "east",
            "say cleves",
            "north", "north",
            "southeast", "up",
            "take mace",
            "down", "northwest", "northeast",
            "examine keg",
            "hit bung with mace",
            "look in keg",
            "ask wiggins to get garnet",
            "look at garnet with magnifying glass",
            "southwest", "east", "down",
            "wear armour",
            "up", "west",
            "south", "south", "south",
            "get paddle",
            "pull chain",
            "remove armour",
            "south",
            "enter boat",
            "raise anchor",
            "launch boat",
            "paddle west", "paddle west", "paddle west",
            "land boat",
            "east", "east",
            "down", "west",
            "put ampoule in hat",
            "wear hat"]);

        // Wait until 2:00am Monday (~26.5h from ~11:30pm Saturday)
        for (int i = 0; i < 160; i++) commands.Add("z");

        // === Endgame: Bar of Gold → Moriarty's Lair → Buckingham Palace ===
        // Seed 42: opal password is "Swordfish"
        commands.AddRange(["ask for akbar",
            "say swordfish",
            "give garnet to akbar",
            "remove hat",
            "get ampoule",
            "hold breath",
            "break ampoule",
            "get knife",
            "cut rope with knife",
            "tie moriarty and akbar with rope",
            "get key",
            "get crown jewels",
            "get whistle",
            "unlock door with key",
            "open door",
            "out",
            "blow whistle", "blow whistle",
            "get in cab", "buckingham palace", "get out",
            "give crown jewels to guard",
            "look"]);

        return commands.ToArray();
    }

    [SkippableFact]
    public void Sherlock_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "sherlock.z5");
        Skip.IfNot(File.Exists(path), "sherlock.z5 not found in stories/");

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
            "ZMachine.Tests", "sherlock_walkthrough_output.txt");
        File.WriteAllText(debugPath, output);

        Assert.Contains("Your score is 100 out of 100", output);
        Assert.Contains("Consulting Detective", output);
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
