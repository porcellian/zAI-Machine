namespace ZMachine.Tests;

using ZMachine.Core;
using ZMachine.Tests.Harness;

/// <summary>
/// Integration tests for the ZMachine execution loop. Boots real story files
/// with scripted input and verifies the output text at each step.
/// </summary>
public class ZMachineIntegrationTests
{
    private const string Zork1Path = "stories/zork1.z3";

    /// <summary>
    /// Verifies that Zork I boots, prints opening text, and responds
    /// to basic commands: look and inventory.
    /// </summary>
    [Fact]
    public void Zork1_BootAndFirstMoves()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["look", "inventory", "quit"]);
        string output = harness.Screen.Output;

        Assert.Contains("ZORK", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("West of House", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("white house", output, StringComparison.OrdinalIgnoreCase);

        bool hasInventory = output.Contains("carrying", StringComparison.OrdinalIgnoreCase)
                         || output.Contains("empty-handed", StringComparison.OrdinalIgnoreCase)
                         || output.Contains("nothing", StringComparison.OrdinalIgnoreCase);
        Assert.True(hasInventory, "Expected inventory response");
    }

    /// <summary>
    /// Verifies that the machine stops after @quit.
    /// </summary>
    [Fact]
    public void Zork1_Quit_StopsMachine()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path, ["quit", "y"]);

        Assert.False(harness.Machine.Running);
    }

    /// <summary>
    /// Verifies the machine can execute multiple turns without crashing.
    /// </summary>
    [Fact]
    public void Zork1_FiveMoves()
    {
        if (!File.Exists(Zork1Path))
            return;

        var harness = TestHarness.Run(Zork1Path,
            ["look", "open mailbox", "read leaflet", "go north", "inventory", "quit", "y"]);
        string output = harness.Screen.Output;

        Assert.Contains("West of House", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mailbox", output, StringComparison.OrdinalIgnoreCase);
    }
}
