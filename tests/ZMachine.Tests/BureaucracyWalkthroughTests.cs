using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Bureaucracy (V4) using a scripted walkthrough to achieve
/// a perfect score of 21/21. Uses seed 42 for deterministic randomization
/// of airline names, bank teller windows, paranoid question order, maze
/// room numbers, and .HAK file names. Tests ReadChar-based form filling,
/// timed input, NPC interactions, and the full game arc.
/// </summary>
public class BureaucracyWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Bureaucracy with seed 42.
    /// Uses List because ReadChar-based prompts require "\r"-terminated
    /// strings and " " padding commands for "press any key" prompts.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === FORM (ReadChar): fill out the licence form ===
        commands.Add("a");                              // first field typed blind
        commands.Add(" ");                              // ReadChar: confirm/advance
        commands.AddRange(["1\r", "a\r", "a\r", "1\r", "m\r",
            "a\r", "1\r", "a\r", "a\r", "1\r", "a\r", "1\r", "a\r", "a\r"]);

        // === STEP 1: Get stuff from house ===
        commands.AddRange(["west", "get all", "east", "open door",
            "give beezer", "get treats"]);

        // === STEP 2: Gather mail ===
        commands.AddRange(["open door", "east", "open mailbox", "get leaflet"]);
        // Mansion: ring bell, get macaw's mail
        commands.AddRange(["south", "ring bell", "north", "east", "south",
            "open screen door", "west", "south",
            "get painting", "north", "show painting to macaw", "get mail", "drop painting"]);
        // Llama farm: feed llama, get mail from trough
        commands.AddRange(["east", "north", "west", "south", "south",
            "open bag", "open mailbox", "put bag in mailbox", "get mail"]);
        // Fortified house: paranoid questions (seed 42 order)
        commands.AddRange(["south", "z",
            "north", "west", "z",
            "say \"unfortunately, there's a radio connected to my brain\"", "z",
            "east", "south",
            "say \"actually, it's the bbc controlling us from london\"", "south"]);
        commands.AddRange(["ohio", "300", "garbage", "novocaine", "traffic helicopters"]);
        // Gaol escape
        commands.AddRange(["saw door", "pull lever", "sit on generator",
            "press button", "get power saw", "plug power saw into generator",
            "give power saw to weirdo", "drop hacksaw",
            "north", "z", "z", "z", "up", "get mail", "north"]);
        // Nerd's house: last mail
        commands.AddRange(["north", "north", "north", "north", "north",
            "east", "knock on door", "south", "give leaflet to man", "get mail"]);

        // === STEP 3: Restaurant, bookstore, travel agency, bank ===
        commands.AddRange(["north", "west", "south", "east",
            "open back door",
            "z",
            "yes",
            "rare", "no", "no", "no", "no", "fries", "no", "yes", "juice", "apple", "no",
            "z", "z", "z",
            "yes",
            "rare", "no", "no", "no", "no", "fries", "no", "yes", "juice", "apple", "no",
            "yes",
            "z", "z", "z", "z", "z",
            "eat food",
            "south"]);
        // Bookstore
        commands.AddRange(["west", "north", "west",
            "look at software",
            "g", "g", "g", "g", "g",
            "yes",
            "open small case", "get game cart", "give cart to clerk"]);
        // Travel agency
        commands.AddRange(["east", "north", "west",
            "give letter", "get ticket"]);
        // Bank visit 1: deposit check (seed 42: #1=withdrawal, #4=deposit)
        commands.AddRange(["east", "north",
            "north",
            "west", "no",
            "west", "no",
            "west", "no",
            "west", "yes",
            "fill out slip",
            "a\r", "a\r", "75\r", "n\r", "a\r",       // ReadChar form (seed 42 field order)
            "east", "east", "east",
            "open envelope", "get check",
            "give slip to teller", "give check to teller",
            "show passport to teller"]);
        // Bank visit 2: withdrawal (after bank reopens)
        commands.AddRange(["south",
            "south",
            "z", "z", "z", "z", "z",
            "z", "z", "z", "z", "z",
            "north",
            "north",
            "west", "west", "west", "west",
            "show passport to teller",
            "yes",
            "fill out slip",
            "n\r", "a\r", "75\r", "a\r", "a\r",       // ReadChar form (2nd visit order)
            "give slip to teller",
            "show passport to teller"]);
        // Call cab and ride to airport
        commands.AddRange(["go home",
            "west",
            "read address book", "last",
            "call 214-1563",
            "a",
            "airport",
            "1", "15",
            "z", "z",
            "east", "east",
            "z", "z", "z",
            "in",
            "yes",
            "show passport to driver",
            "yes",
            "z", "z",
            "give $17.50 to driver"]);

        // === STEP 4: Airport and flight ===
        // Seed 42: Air Zalagasa desk is east 2, north 2 from cab entrance
        commands.AddRange(["east", "east",
            "north",
            "north",
            "z", "z", "z", "z", "z",
            "z", "z", "z", "z", "z",
            "give ticket to clerk",
            "direct",
            "south",
            "climb pillar",
            "g", "g", "g", "g", "g",
            "open grate",
            "up", "up", "up", "up",
            "open grate",
            "enter grate",
            "controllers, stop flight 42",
            "enter grate",
            "down", "down", "down", "down",
            "pull red and black wires",
            "connect red and black wires",
            "down", "down",
            "get up",
            "sit in seat c",
            "z", "z", "z", "z", "z",
            "z", "z", "z", "z", "z",
            "chicken",
            "yes",
            "yes",
            "go to row 8",
            "sit in seat d",
            "press light button",
            "go to rear",
            "z", "z",
            "answer phone",
            "yes", "yes", "yes",
            "z", "z", "z",
            "stinglai ka'abi",
            "z",
            "lift handle",
            "pull handle",
            "z", "z", "z",
            "knock on window",
            "pull ripcord",
            "z", "z", "z", "z", "z",
            "look"]);

        // === STEP 5: Jungle → Persecution Complex maze ===
        commands.AddRange(["remove parachute",
            "put recipe cart in computer",
            " ",                                        // ReadChar: press any key to boot
            " ",                                        // ReadChar: strike any key to shutdown
            "get out",
            "turn left and middle handles",
            "turn left and right handles",
            "turn left and middle handles",
            "open locker",
            "enter locker",
            "get keycard",
            "exit",
            // Maze: seed 42 path E,E,D,W,D,W,W (room-number-difference algorithm)
            "east",                                     // Room 21
            "east",                                     // Room 45
            "down",                                     // Room 70
            "west",                                     // Room 84
            "down",                                     // Room 119
            "west",                                     // Room 134
            "west",                                     // Airlock
            "put keycard in airlock",
            "open door",
            "g", "g", "g", "g",                         // blood pressure → rage → door opens
            "north"]);                                  // enter Persecution Complex

        // === STEP 6: Sabotage the nerd's computer ===
        commands.AddRange(["west", "west", "west", "west",
            "plug computer in",
            " ",                                        // ReadChar: "Press any key to boot"
            "random-q-hacker\r",                        // ID (ReadChar)
            "rainbow-turtle\r",                         // password (ReadChar, blind)
            "run\r",
            "plane.exe\r",
            "dir\r",                                    // 1st DIR: file list
            "dir\r",                                    // 2nd DIR: "ABOUT TO USE FIDUC.HAK"
            "cop\r",
            "dvh2.hak\r",                               // source file
            "fiduc.hak\r",                              // target (seed 42)
            "y\r",                                      // confirm overwrite
            "quit\r",
            " ",                                        // ReadChar: "Strike any key" to disconnect
            // Endgame: fly home
            "west",
            "up",
            "z", "z", "z", "z", "z",
            "z", "z", "z", "z", "z",
            "z", "z", "z", "z", "z",
            "z", "z", "z", "z", "z",
            "west",
            "go home",
            "read letter"]);

        return commands.ToArray();
    }

    [SkippableFact]
    public void Bureaucracy_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "bureaucracy.z4");
        Skip.IfNot(File.Exists(path), "bureaucracy.z4 not found in stories/");

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
            "ZMachine.Tests", "bureau_walkthrough_output.txt");
        File.WriteAllText(debugPath, output);

        Assert.False(harness.HitInstructionLimit,
            $"Hit instruction limit at {harness.InstructionsExecuted} instructions");

        Assert.Contains("Your score is 21 out of a possible 21", output);
        Assert.Contains("making you a Bureaucrat", output);
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
