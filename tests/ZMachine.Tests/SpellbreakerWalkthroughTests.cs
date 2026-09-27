using ZMachine.Core;
using ZMachine.Tests.Harness;

namespace ZMachine.Tests;

/// <summary>
/// Plays through Spellbreaker (V3) using a scripted walkthrough to achieve a
/// perfect score of 600/600. Uses seed 42, which fixes the spell misfires
/// the script works around, Belboz's security question (the fireworks
/// master, "Dimithio"), the brown rock's evasions on the Plain, and the
/// odd cube's position in the vault piles. Exercises spell memory, hub
/// cubes and BLORPLE, the gold box portal, shape changing, death and
/// resurrection in the Boneyard, time travel with paradox checks, and the
/// final confrontation with the shadow.
/// </summary>
public class SpellbreakerWalkthroughTests
{
    private static readonly string StoriesDir =
        Path.Combine(FindRepoRoot(), "stories");

    /// <summary>
    /// Builds the walkthrough commands for Spellbreaker with seed 42.
    /// The script is sensitive to the random number stream (several casts
    /// are repeated because the first attempt misfires) and to turn counts
    /// (the roc, the flooding Oubliette and past Ruins, the vault alarm
    /// after the third JINDAK, and the endgame freeze), so adding or
    /// removing commands can desynchronise later sections.
    /// </summary>
    private static string[] BuildWalkthroughCommands()
    {
        var commands = new List<string>();

        // === Belwit Square: blow away the smoke (LESOCH) and take the first cube ===
        commands.AddRange([
            "verbose", "wait", "wait", "wait", "wait", "south", "take bread", "take fish",
            "learn lesoch", "south", "learn lesoch", "learn lesoch", "lesoch", "lesoch",
            "take cube"]);

        // === Cliff (THROCK scroll) and the Ruins (zipper, flimsy GIRGOL scroll) ===
        commands.AddRange([
            "frotz knife", "learn blorple", "blorple cube", "south", "take scroll",
            "gnusto throck", "learn blorple", "blorple cube", "east", "south", "take zipper",
            "open zipper", "put knife in zipper", "take all from zipper", "learn blorple",
            "blorple cube"]);

        // === Fall from Packed Earth; the roc carries us to its nest (CASKLY scroll) ===
        commands.AddRange([
            "down", "down", "wait", "wait", "wait", "wait", "take stained scroll", "gnusto caskly",
            "learn blorple", "blorple cube"]);

        // === Trigger the avalanche, freeze it with GIRGOL, climb to the hut (soft cube) ===
        commands.AddRange([
            "south", "up", "learn lesoch", "learn lesoch", "lesoch", "girgol", "up", "up", "up",
            "up", "take coin", "west", "learn caskly", "learn caskly", "caskly hut", "caskly hut",
            "write \"earth\" on cube", "take featureless cube"]);

        // === Grow the pollen weed with THROCK so the ogre sneezes: ESPNIS scroll, gold box, water cube ===
        commands.AddRange([
            "learn blorple", "blorple featureless cube", "south",
            "write \"soft\" on featureless cube", "open zipper", "put fish in zipper",
            "put bread in zipper", "put coin in zipper", "take shears", "take weed", "take weed",
            "learn blorple", "blorple \"earth\" cube", "west", "north", "plant weed",
            "learn throck", "learn throck", "throck weed", "down", "take all", "gnusto espnis",
            "open box", "put shears in zipper", "take cube from box", "learn blorple",
            "blorple featureless cube"]);

        // === Mid-Ocean: drop the fish so the grouper leaves the cube; LISKON from the bottle ===
        commands.AddRange([
            "sleep", "learn blorple", "learn blorple", "learn blorple", "north",
            "blorple \"soft\" cube", "south", "drop book", "write \"water\" on featureless cube",
            "blorple \"water\" cube", "take fish from zipper", "south", "drop fish", "take cube",
            "take bottle", "blorple \"soft\" cube", "south", "take book",
            "put \"water\" cube in zipper", "open bottle", "take damp scroll", "read damp scroll",
            "gnusto liskon", "learn blorple", "learn liskon", "learn liskon"]);

        // === Shrink the serpent, animate and sleep the idol (air cube), pry out the opal ===
        commands.AddRange([
            "blorple \"earth\" cube", "east", "north", "liskon serpent", "north", "north",
            "learn malyon", "learn espnis", "malyon idol", "wait", "espnis idol", "wait",
            "climb idol", "take cube", "drop bottle", "pry eye with knife", "take opal", "down",
            "write \"air\" on featureless cube", "put opal in zipper", "put gold box in zipper"]);

        // === Glacier (TINSOT scroll) and the Bazaar: trade the opal for the blue carpet ===
        commands.AddRange([
            "learn blorple", "learn blorple", "blorple \"air\" cube", "north", "take white scroll",
            "gnusto tinsot", "blorple \"air\" cube", "west", "east", "take coin from zipper",
            "take opal from zipper", "buy blue carpet", "give opal to merchant",
            "take blue carpet", "west"]);

        // === Flood the Oubliette with TINSOT, ride the ice floe up to the Dungeon (bone cube) ===
        commands.AddRange([
            "put coin in zipper", "put \"soft\" cube in zipper", "put \"air\" cube in zipper",
            "take \"water\" cube from zipper", "learn blorple", "learn tinsot", "learn tinsot",
            "learn tinsot", "learn tinsot", "put book in zipper", "close zipper",
            "blorple \"water\" cube", "north", "tinsot channel", "tinsot channel",
            "tinsot channel", "wait", "wait", "tinsot water", "rezrov trap door",
            "climb on ice floe", "up", "open zipper", "take cube",
            "write \"bone\" on featureless cube", "put \"bone\" cube in zipper",
            "put \"water\" cube in zipper", "up"]);

        // === Launch the carpet from the Guard Tower so the roc flees; empty nest (string cube) ===
        commands.AddRange([
            "drop carpet", "sit on carpet", "up", "west", "west", "west", "west", "down",
            "get off carpet", "take cube", "sit on carpet", "up",
            "write \"string\" on featureless cube", "east", "east", "east", "east", "down",
            "get off carpet", "take carpet", "down"]);

        // === Belboz's question (answer: Dimithio) earns the wrought iron key ===
        commands.AddRange([
            "take book from zipper", "learn blorple", "learn blorple", "blorple \"string\" cube",
            "south", "ask belboz about key", "answer dimithio"]);

        // === LISKON ourselves and ride the pipes past the crack (changing cube) ===
        commands.AddRange([
            "take \"water\" cube from zipper", "learn blorple", "learn liskon", "learn liskon",
            "blorple \"water\" cube", "north", "liskon me", "put book in zipper", "close zipper",
            "down", "west", "west", "take cube from crack", "west", "up"]);

        // === Compass rose and the octagonal maze; REZROV the alabaster plug (void cube) ===
        commands.AddRange([
            "write \"change\" on featureless cube", "open zipper", "put \"string\" cube in zipper",
            "put \"water\" cube in zipper", "put key in zipper", "put carpet in zipper",
            "take book from zipper", "learn blorple", "learn blorple", "blorple \"change\" cube",
            "north", "take rose", "blorple \"change\" cube", "west", "sleep",
            "put rose in carving", "take rose", "north", "touch west rune with rose", "west",
            "touch northeast rune with rose", "northeast", "touch northwest rune with rose",
            "northwest", "rezrov plug", "west", "put \"earth\" cube in zipper",
            "put \"change\" cube in zipper", "drop rose", "take cube", "write \"void\" on cube",
            "sleep", "learn blorple", "learn blorple", "blorple \"void\" cube", "east"]);

        // === Second trip to the Dungeon: CASKLY the moldy book to learn SNAVIG ===
        commands.AddRange([
            "put \"void\" cube in zipper", "take \"water\" cube from zipper", "learn blorple",
            "learn caskly", "learn caskly", "learn tinsot", "learn tinsot", "learn tinsot",
            "put book in zipper", "close zipper", "blorple \"water\" cube", "north",
            "tinsot channel", "tinsot channel", "tinsot channel", "wait", "wait", "tinsot water",
            "climb on ice floe", "up", "east", "north", "rezrov cabinet", "take moldy book",
            "caskly moldy book", "open zipper", "put \"water\" cube in zipper",
            "take book from zipper", "gnusto snavig"]);

        // === SNAVIG into a grouper, reach the nest, TAKE ALL as we drown (light cube) ===
        commands.AddRange([
            "take bread from zipper", "learn blorple", "learn blorple", "learn snavig",
            "learn snavig", "learn snavig", "put book in zipper",
            "take \"water\" cube from zipper", "put knife in zipper", "put burin in zipper",
            "close zipper", "blorple \"water\" cube", "south", "drop bread", "snavig grouper",
            "snavig grouper", "down", "wait", "wait", "wait", "take all", "wait"]);

        // === Volcano Base: TINSOT cools a lava fragment ===
        commands.AddRange([
            "open zipper", "take burin from zipper", "write \"light\" on featureless cube",
            "put \"water\" cube in zipper", "take knife from zipper", "take book from zipper",
            "learn blorple", "learn blorple", "north", "blorple \"light\" cube", "west", "wait",
            "learn tinsot", "tinsot fragment", "take fragment", "put fragment in zipper"]);

        // === Feed the green rock, corner the brown rock via the 4-1 diagonal (dark cube) ===
        commands.AddRange([
            "take \"void\" cube from zipper", "learn blorple", "learn blorple",
            "blorple \"void\" cube", "south", "take fragment from zipper",
            "give fragment to green rock", "climb green rock", "rock, north", "rock, southwest",
            "rock, east", "rock, east", "rock, south", "rock, south", "climb brown rock",
            "take cube", "write \"dark\" on featureless cube"]);

        // === SNAVIG into a grue, climb the pillar in the light pool (fire cube) ===
        commands.AddRange([
            "learn blorple", "learn blorple", "learn snavig", "learn snavig",
            "blorple \"dark\" cube", "down", "drop knife", "down", "snavig grue", "down",
            "climb pillar", "take cube", "wait", "wait", "wait", "blorple cube", "north",
            "write \"fire\" on featureless cube"]);

        // === Throw the gold box onto the outcropping and follow it through the Water Room (magic cube) ===
        commands.AddRange([
            "take gold box from zipper", "throw gold box at outcropping",
            "take \"water\" cube from zipper", "learn blorple", "learn blorple",
            "blorple \"water\" cube", "east", "take cube", "put \"water\" cube in zipper",
            "put \"dark\" cube in zipper", "put \"light\" cube in zipper",
            "write \"magic\" on featureless cube", "take gold box", "frotz burin",
            "put gold box in zipper", "put \"fire\" cube in zipper"]);

        // === Outer Vault: three JINDAK weighings single out cube x7 (sand cube) ===
        commands.AddRange([
            "learn blorple", "learn jindak", "learn jindak", "learn jindak", "learn blorple",
            "put book in zipper", "blorple \"void\" cube", "east", "rezrov door", "north",
            "take \"x5\" cube and \"x6\" cube", "put \"x5\" cube and \"x6\" cube in second pile",
            "take \"x9\" cube, \"x10\" cube, \"x11\" cube and \"x12\" cube", "jindak",
            "drop \"x10\" cube, \"x11\" cube and \"x12\" cube", "take \"x3\" cube and \"x4\" cube",
            "put \"x3\" cube and \"x9\" cube in second pile",
            "take \"x5\" cube, \"x7\" cube and \"x8\" cube", "put \"x5\" cube in first pile",
            "jindak", "put \"x4\" cube and \"x8\" cube in first pile",
            "take \"x10\" cube and \"x11\" cube",
            "put \"x10\" cube and \"x11\" cube in second pile", "jindak", "blorple \"x7\" cube"]);

        // === The past: leave our spell book in the locked cabinet (it becomes the moldy book) ===
        commands.AddRange([
            "take carpet from zipper",
            "take \"fire\" cube, \"light\" cube and \"dark\" cube from zipper",
            "put \"fire\" cube, \"light\" cube and \"dark\" cube on carpet",
            "take \"water\" cube, \"change\" cube and \"earth\" cube from zipper",
            "put \"water\" cube, \"change\" cube and \"earth\" cube on carpet",
            "take \"string\" cube, \"bone\" cube and \"air\" cube from zipper",
            "put \"string\" cube, \"bone\" cube and \"air\" cube on carpet",
            "take \"soft\" cube from zipper", "put \"soft\" cube and \"void\" cube on carpet",
            "take book from zipper", "learn blorple", "learn blorple", "learn blorple",
            "learn espnis", "take key from zipper", "down", "write \"sand\" on \"x7\" cube",
            "unlock cabinet with key", "open cabinet", "take vellum scroll", "put book in cabinet",
            "close cabinet", "lock cabinet with key", "put key in zipper", "rezrov door",
            "blorple \"sand\" cube"]);

        // === The past Ruins: copy GIRGOL to vellum, leave the zipper holding the flimsy scroll ===
        commands.AddRange([
            "take all from zipper", "drop key, gold box, gold coin and shears", "up", "open sack",
            "copy girgol to vellum scroll", "take flimsy scroll", "put flimsy scroll in zipper",
            "close zipper", "take sack", "drop zipper", "blorple \"magic\" cube"]);

        // === Endgame: provoke the freeze early, GIRGOL mid-leap, swap the magic cube for the sack ===
        commands.AddRange([
            "east", "wait", "wait", "wait", "wait", "wait", "espnis shadow", "wait", "wait",
            "wait", "wait", "wait", "girgol", "take \"magic\" cube", "put sack in hypercube"]);

        return commands.ToArray();
    }

    [SkippableFact]
    public void Spellbreaker_PerfectScore()
    {
        var path = Path.Combine(StoriesDir, "spellbre.z3");
        Skip.IfNot(File.Exists(path), "spellbre.z3 not found in stories/");

        TestHarness harness;
        try
        {
            harness = TestHarness.Run(path, BuildWalkthroughCommands(),
                instructionLimit: 40_000_000, randomSeed: 42);
        }
        catch (ZMachineException ex)
        {
            Assert.Fail($"Interpreter crashed: {ex.Message}");
            return;
        }

        var output = harness.Screen.Output;

        var debugPath = Path.Combine(FindRepoRoot(), "tests",
            "ZMachine.Tests", "spellbreaker_walkthrough_output.txt");
        File.WriteAllText(debugPath, output);

        Assert.Contains("Your score is 600 of a possible 600", output);
        Assert.Contains("This puts you in the class of Scientist", output);
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
