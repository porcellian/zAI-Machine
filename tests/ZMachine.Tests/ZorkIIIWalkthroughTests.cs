using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Zork III: The Dungeon Master (V3) using a scripted
/// walkthrough that earns the full potential of 7 points and completes the
/// story in the Treasury of Zork. Uses seed 42, which fixes the hooded
/// figure's combat, the 50% chance of grabbing the amulet underwater and the
/// earthquake's timing. Exercises the Scenic Vista teleport table, the grue
/// repellent, the aqueduct (which must be crossed before the earthquake
/// collapses it), the time machine, the Royal Puzzle's sliding walls, the
/// mirror box and the Dungeon Master's rotating prison cells. The run also
/// records every opcode the interpreter executes.
/// </summary>
public class ZorkIIIWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Zork III with seed 42.
    /// The run is sensitive to the random number stream: the fight with the
    /// hooded figure and the amulet grab depend on it (the HELLOs exist only to
    /// shift it), and the earthquake, queued at the start of the game, must
    /// not strike before the aqueduct is crossed. Adding or removing any
    /// command will desynchronise later sections.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === Lamp and bread; wake the old man at the Engravings Room and feed him (reveals the secret door); one HELLO aligns the RNG for the fight ===
        commands.AddRange(["verbose", "take lamp", "turn on lamp", "south", "west", "west",
            "take bread", "east", "east", "east", "northeast", "wake man", "give bread to man",
            "hello"]);

        // === Land of Shadow: the sword appears (+1); attack the hooded figure (+1) until he is defenseless, then take his hood and cloak ===
        commands.AddRange(["southwest", "west", "west", "southwest", "attack figure with sword",
            "attack figure with sword", "attack figure with sword", "attack figure with sword",
            "attack figure with sword", "attack figure with sword", "attack figure with sword",
            "attack figure with sword", "attack figure with sword", "attack figure with sword",
            "attack figure with sword", "attack figure with sword", "attack figure with sword",
            "take hood", "take cloak"]);

        // === Cliff Ledge (+1): tie the rope to the chest and trust the "friend" at the top, who drops the rope back down ===
        commands.AddRange(["north", "west", "down", "tie rope to chest", "wait", "wait", "wait",
            "wait", "wait", "grab rope"]);

        // === Flathead Ocean: wait for the boat, HELLO SAILOR earns the vial; back to the Junction ===
        commands.AddRange(["down", "down", "south", "wait", "wait", "wait", "hello sailor",
            "take vial", "north", "northeast", "southeast"]);

        // === Lake Shore: drop everything, swim in (+1), dive for the golden amulet (50% grab, HELLOs tune the RNG) ===
        commands.AddRange(["east", "south", "south", "south", "hello", "hello", "drop all",
            "enter lake", "down", "take object", "up", "north", "take all"]);

        // === Scenic Vista table: II = Room 8 for the grue repellent (+1), III = Damp Passage to leave the lit torch ===
        commands.AddRange(["drop all", "enter lake", "west", "south", "take torch", "touch table",
            "take repellent", "wait", "touch table", "drop torch", "wait", "north"]);

        // === Repellent, swim to the Southern Shore, through the Dark Places to the Key Room; manhole, aqueduct and water slide back to the torch; recover the gear ===
        commands.AddRange(["spray repellent on me", "enter lake", "south", "south", "south",
            "east", "take key", "move cover", "down", "north", "north", "down", "take torch",
            "west", "south", "south", "south", "take all"]);

        // === To the Great Door and wait for the earthquake to open the cleft into the Museum ===
        commands.AddRange(["north", "north", "east", "east", "south", "south", "wait"]);

        // === Technology Museum: time machine to 776 for the ring (+1); hide it under the seat and return to 948 ===
        commands.AddRange(["east", "open stone door", "drop all", "north",
            "push gold machine south", "push gold machine east", "sit on seat", "turn dial to 776",
            "push button", "take ring", "wait", "wait", "open door", "west", "open wooden door",
            "north", "put ring under seat", "sit on seat", "turn dial to 948", "push button",
            "get up", "look under seat", "open door", "south", "take all"]);

        // === Royal Puzzle (+1 on the first push): reach the book, then shove the ladder wall beside the entrance and climb out ===
        commands.AddRange(["south", "down", "push south wall", "north", "push east wall", "south",
            "south", "east", "east", "north", "north", "push east wall", "west", "south", "south",
            "push south wall", "take book", "push south wall", "east", "east", "north", "north",
            "north", "push west wall", "north", "west", "push south wall", "push south wall",
            "push south wall", "push east wall", "south", "south", "push west wall",
            "push north wall", "east", "north", "push west wall", "push west wall", "east",
            "south", "push west wall", "push west wall", "push north wall", "push north wall",
            "push north wall", "west", "north", "up", "north"]);

        // === Engravings Room secret door; the sword blocks the Beam Room's beam; the button opens the mirror ===
        commands.AddRange(["west", "north", "north", "west", "west", "north", "east", "northeast",
            "open door", "north", "north", "drop sword", "south", "push button", "north", "north",
            "north"]);

        // === Inside the mirror box: point it north, slide to the end of the channel, turn it south and exit through the pine panel ===
        commands.AddRange(["raise short pole", "push red panel", "push red panel",
            "lower short pole", "push mahogany panel", "push mahogany panel",
            "push mahogany panel", "raise short pole", "push red panel", "push red panel",
            "push red panel", "push red panel", "lower short pole", "push pine panel", "north"]);

        // === Knock: the Dungeon Master accepts us; cell 4 on the sundial, he rotates it with us inside; the key opens the bronze door to the Treasury ===
        commands.AddRange(["knock on door", "north", "west", "north", "north", "turn dial to 4",
            "push button", "dungeon master, stay", "south", "open door", "south",
            "dungeon master, turn dial to 1", "dungeon master, push button",
            "unlock bronze door with key", "open bronze door", "south"]);
        return commands.ToArray();
    }

    [SkippableFact]
    public void ZorkIII_FullPotential()
    {
        var path = Path.Combine(StoriesDir, "zork3.z3");
        Skip.IfNot(File.Exists(path), "zork3.z3 not found in stories/");

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

                // The Treasury ends the game with a quit, but stop at a prompt
                // after the last command too in case the script ever falls short.
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
        File.WriteAllText(Path.Combine(testDir, "zork3_walkthrough_output.txt"), output);
        File.WriteAllText(Path.Combine(testDir, "zork3_walkthrough_opcodes.txt"),
            string.Join("\n", opcodeCounts.Select(kv => $"{kv.Key}\t{kv.Value}")));

        Assert.Contains("Treasury of Zork", output);
        Assert.Contains("you have at last completed your quest in ZORK", output);
        Assert.Contains("Your potential is 7 of a possible 7", output);
        Assert.DoesNotContain("You have died", output);

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
