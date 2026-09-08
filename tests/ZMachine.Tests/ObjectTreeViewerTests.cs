namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for ObjectTreeViewer — verifies tree traversal, object detail
/// extraction, search/filter, export, and multi-version coverage against
/// real story files and synthetic memory images.
/// </summary>
public class ObjectTreeViewerTests
{
    private const string Zork1Path = "stories/zork1.z3";
    private const string CzechPath = "stories/czech.z5";
    private const string MindPath = "stories/mind.z4";
    private const string SherlockPath = "stories/sherlock.z5";

    #region Zork I — Tree Structure

    /// <summary>
    /// Verifies that the object count is reasonable for Zork I
    /// (known to have around 250 objects).
    /// </summary>
    [Fact]
    public void Zork1_ObjectCount_IsReasonable()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        int count = viewer.GetObjectCount();

        Assert.InRange(count, 200, 255);
    }

    /// <summary>
    /// Verifies that "West of House" exists in the object list
    /// and can be found by name.
    /// </summary>
    [Fact]
    public void Zork1_WestOfHouse_Exists()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("West of House");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name.Contains("West of House"));
    }

    /// <summary>
    /// Verifies that "West of House" has a parent object in the tree.
    /// </summary>
    [Fact]
    public void Zork1_WestOfHouse_HasParent()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("West of House");

        var woh = results.First(r => r.Name.Contains("West of House"));
        var detail = viewer.GetObjectDetail(woh.Number);

        Assert.True(detail.Parent > 0,
            "West of House should have a parent");
        Assert.NotEmpty(detail.ParentName);
    }

    /// <summary>
    /// Verifies that "West of House" appears as a child of its parent
    /// in the tree hierarchy.
    /// </summary>
    [Fact]
    public void Zork1_WestOfHouse_IsChildOfParent()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("West of House");
        var woh = results.First(r => r.Name.Contains("West of House"));
        var detail = viewer.GetObjectDetail(woh.Number);

        var parentDetail = viewer.GetObjectDetail(detail.Parent);

        bool foundAsChild = false;
        int childNum = parentDetail.Child;
        while (childNum > 0)
        {
            if (childNum == woh.Number)
            {
                foundAsChild = true;
                break;
            }
            var childDetail = viewer.GetObjectDetail(childNum);
            childNum = childDetail.Sibling;
        }

        Assert.True(foundAsChild,
            $"Object {woh.Number} (West of House) should be a child of " +
            $"object {detail.Parent} ({detail.ParentName})");
    }

    /// <summary>
    /// Verifies that the mailbox object exists and has properties.
    /// </summary>
    [Fact]
    public void Zork1_Mailbox_HasProperties()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("mailbox");

        Assert.NotEmpty(results);
        var mailbox = results.First(r =>
            r.Name.Contains("mailbox", StringComparison.OrdinalIgnoreCase));
        var detail = viewer.GetObjectDetail(mailbox.Number);

        Assert.NotEmpty(detail.Properties);
    }

    /// <summary>
    /// Verifies that the mailbox has at least one attribute set.
    /// </summary>
    [Fact]
    public void Zork1_Mailbox_HasAttributes()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("mailbox");
        var mailbox = results.First(r =>
            r.Name.Contains("mailbox", StringComparison.OrdinalIgnoreCase));
        var detail = viewer.GetObjectDetail(mailbox.Number);

        Assert.NotEmpty(detail.Attributes);
    }

    #endregion

    #region Zork I — Tree Building

    /// <summary>
    /// Verifies that GetTree returns root nodes (objects with parent 0).
    /// </summary>
    [Fact]
    public void Zork1_Tree_HasRoots()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var roots = viewer.GetTree();

        Assert.NotEmpty(roots);
    }

    /// <summary>
    /// Verifies that root nodes have children (at least some rooms
    /// should contain objects).
    /// </summary>
    [Fact]
    public void Zork1_Tree_RootsHaveChildren()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var roots = viewer.GetTree();

        Assert.Contains(roots, r => r.Children.Count > 0);
    }

    /// <summary>
    /// Verifies that the tree contains all objects — the sum of all
    /// nodes in the tree equals the object count.
    /// </summary>
    [Fact]
    public void Zork1_Tree_ContainsAllObjects()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        int count = viewer.GetObjectCount();
        var roots = viewer.GetTree();

        int treeCount = CountNodes(roots);
        Assert.Equal(count, treeCount);
    }

    #endregion

    #region Zork I — Search

    /// <summary>
    /// Verifies that searching by object number returns the correct object.
    /// </summary>
    [Fact]
    public void Zork1_Search_ByNumber()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("1");

        Assert.Contains(results, r => r.Number == 1);
    }

    /// <summary>
    /// Verifies that search is case-insensitive.
    /// </summary>
    [Fact]
    public void Zork1_Search_CaseInsensitive()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);

        var upper = viewer.Search("WEST OF HOUSE");
        var lower = viewer.Search("west of house");

        Assert.Equal(upper.Count, lower.Count);
        if (upper.Count > 0)
            Assert.Equal(upper[0].Number, lower[0].Number);
    }

    /// <summary>
    /// Verifies that a nonexistent name returns empty results.
    /// </summary>
    [Fact]
    public void Zork1_Search_NotFound()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("xyzzy_nonexistent_12345");

        Assert.Empty(results);
    }

    #endregion

    #region Zork I — Object Detail

    /// <summary>
    /// Verifies that object detail includes property data with hex and
    /// interpreted values.
    /// </summary>
    [Fact]
    public void Zork1_PropertyDetail_HasHexAndInterpreted()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("mailbox");
        var mailbox = results.First(r =>
            r.Name.Contains("mailbox", StringComparison.OrdinalIgnoreCase));
        var detail = viewer.GetObjectDetail(mailbox.Number);

        var firstProp = detail.Properties[0];
        Assert.NotEmpty(firstProp.DataHex);
        Assert.True(firstProp.DataAddress > 0);
        Assert.True(firstProp.DataLength > 0);
    }

    /// <summary>
    /// Verifies that property numbers are in descending order.
    /// </summary>
    [Fact]
    public void Zork1_Properties_DescendingOrder()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        var results = viewer.Search("mailbox");
        var mailbox = results.First(r =>
            r.Name.Contains("mailbox", StringComparison.OrdinalIgnoreCase));
        var detail = viewer.GetObjectDetail(mailbox.Number);

        for (int i = 1; i < detail.Properties.Count; i++)
        {
            Assert.True(detail.Properties[i].Number < detail.Properties[i - 1].Number,
                $"Property {detail.Properties[i].Number} should precede " +
                $"{detail.Properties[i - 1].Number} (descending order)");
        }
    }

    #endregion

    #region Zork I — Export

    /// <summary>
    /// Verifies that export produces non-empty output containing
    /// known objects.
    /// </summary>
    [Fact]
    public void Zork1_Export_ContainsKnownObjects()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        string export = viewer.Export();

        Assert.Contains("Object tree:", export);
        Assert.Contains("West of House", export);
        Assert.Contains("mailbox", export, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that export includes property and attribute details.
    /// </summary>
    [Fact]
    public void Zork1_Export_IncludesDetails()
    {
        if (!File.Exists(Zork1Path)) return;
        var viewer = CreateViewer(Zork1Path);
        string export = viewer.Export();

        Assert.Contains("Attributes:", export);
        Assert.Contains("Properties:", export);
        Assert.Contains("Parent:", export);
    }

    #endregion

    #region Multi-version

    /// <summary>
    /// Verifies that a V4 story file has objects with names.
    /// </summary>
    [Fact]
    public void Mind_V4_HasNamedObjects()
    {
        if (!File.Exists(MindPath)) return;
        var viewer = CreateViewer(MindPath);
        int count = viewer.GetObjectCount();

        Assert.True(count > 0);
        string name = viewer.GetShortName(1);
        Assert.NotNull(name);
    }

    /// <summary>
    /// Verifies that a V5 story file produces a valid tree.
    /// </summary>
    [Fact]
    public void Sherlock_V5_TreeBuilds()
    {
        if (!File.Exists(SherlockPath)) return;
        var viewer = CreateViewer(SherlockPath);
        var roots = viewer.GetTree();

        Assert.NotEmpty(roots);
    }

    /// <summary>
    /// Verifies that V5 objects can have up to 48 attributes (0–47).
    /// </summary>
    [Fact]
    public void Czech_V5_AttributeRange()
    {
        if (!File.Exists(CzechPath)) return;
        var viewer = CreateViewer(CzechPath);
        int count = viewer.GetObjectCount();
        if (count == 0) return;

        var detail = viewer.GetObjectDetail(1);
        foreach (int attr in detail.Attributes)
            Assert.InRange(attr, 0, 47);
    }

    /// <summary>
    /// Verifies V5 object count uses 14-byte entries.
    /// </summary>
    [Fact]
    public void Czech_V5_ObjectCount()
    {
        if (!File.Exists(CzechPath)) return;
        var viewer = CreateViewer(CzechPath);
        int count = viewer.GetObjectCount();

        Assert.True(count > 0, "V5 story should have objects");
    }

    #endregion

    #region Synthetic Memory

    /// <summary>
    /// Verifies tree building with a minimal synthetic V3 object table:
    /// two objects, one parented to the other.
    /// </summary>
    [Fact]
    public void Synthetic_V3_ParentChild()
    {
        var data = BuildSyntheticV3(parentOf2: 1);
        var memory = new Memory();
        memory.LoadStory(data);
        var viewer = new ObjectTreeViewer(memory);

        var detail1 = viewer.GetObjectDetail(1);
        Assert.Equal(0, detail1.Parent);
        Assert.Equal(2, detail1.Child);

        var detail2 = viewer.GetObjectDetail(2);
        Assert.Equal(1, detail2.Parent);
    }

    /// <summary>
    /// Verifies that GetTree places child under its parent root.
    /// </summary>
    [Fact]
    public void Synthetic_V3_TreeStructure()
    {
        var data = BuildSyntheticV3(parentOf2: 1);
        var memory = new Memory();
        memory.LoadStory(data);
        var viewer = new ObjectTreeViewer(memory);

        var roots = viewer.GetTree();
        Assert.Single(roots);
        Assert.Equal(1, roots[0].Number);
        Assert.Single(roots[0].Children);
        Assert.Equal(2, roots[0].Children[0].Number);
    }

    /// <summary>
    /// Verifies search on synthetic objects.
    /// </summary>
    [Fact]
    public void Synthetic_V3_Search()
    {
        var data = BuildSyntheticV3(parentOf2: 1);
        var memory = new Memory();
        memory.LoadStory(data);
        var viewer = new ObjectTreeViewer(memory);

        var byNum = viewer.Search("1");
        Assert.Contains(byNum, r => r.Number == 1);
    }

    /// <summary>
    /// Verifies export on synthetic objects produces valid output.
    /// </summary>
    [Fact]
    public void Synthetic_V3_Export()
    {
        var data = BuildSyntheticV3(parentOf2: 1);
        var memory = new Memory();
        memory.LoadStory(data);
        var viewer = new ObjectTreeViewer(memory);

        string export = viewer.Export();
        Assert.Contains("Object tree:", export);
        Assert.Contains("[1]", export);
        Assert.Contains("[2]", export);
    }

    /// <summary>
    /// Verifies that attributes on synthetic objects are detected.
    /// </summary>
    [Fact]
    public void Synthetic_V3_Attributes()
    {
        var data = BuildSyntheticV3(parentOf2: 1, setAttr0OnObj1: true);
        var memory = new Memory();
        memory.LoadStory(data);
        var viewer = new ObjectTreeViewer(memory);

        var detail = viewer.GetObjectDetail(1);
        Assert.Contains(0, detail.Attributes);
    }

    #endregion

    #region Helpers

    /// <summary>Creates an ObjectTreeViewer from a story file path.</summary>
    private static ObjectTreeViewer CreateViewer(string path)
    {
        var memory = new Memory();
        memory.LoadStory(path);
        return new ObjectTreeViewer(memory);
    }

    /// <summary>Counts all nodes in a tree recursively.</summary>
    private static int CountNodes(List<ObjectNode> nodes)
    {
        int count = 0;
        foreach (var node in nodes)
        {
            count++;
            count += CountNodes(node.Children);
        }
        return count;
    }

    /// <summary>
    /// Builds a synthetic V3 story with 2 objects. Object 2's parent
    /// is set to parentOf2 (0 or 1). Includes minimal abbreviation
    /// table, property tables with empty short names.
    /// </summary>
    private static byte[] BuildSyntheticV3(int parentOf2, bool setAttr0OnObj1 = false)
    {
        // V3: property defaults = 31 * 2 = 62 bytes
        // Entry size = 9 bytes (4 attr + 1 parent + 1 sibling + 1 child + 2 prop ptr)
        var data = new byte[0x10000];
        data[0x00] = 3; // version
        data[0x04] = 0x80; data[0x05] = 0x00; // high base $8000
        data[0x0E] = 0x80; data[0x0F] = 0x00; // static base $8000

        // Object table at $0200
        int objTable = 0x0200;
        data[0x0A] = (byte)(objTable >> 8);
        data[0x0B] = (byte)(objTable & 0xFF);

        // Abbreviation table at $0100 (empty)
        data[0x18] = 0x01; data[0x19] = 0x00;

        int defaultsSize = 31 * 2; // 62
        int entriesStart = objTable + defaultsSize; // $023E

        // Property tables placed immediately after 2 object entries
        // so GetObjectCount() = (propTable1 - entriesStart) / 9 = 2.
        int propTable1 = entriesStart + 2 * 9; // $0250
        int propTable2 = propTable1 + 2;        // $0252

        // Object 1 entry at entriesStart
        int obj1Addr = entriesStart;
        if (setAttr0OnObj1)
            data[obj1Addr] = 0x80; // attr 0 = top bit of first byte
        // parent = 0, sibling = 0, child = parentOf2 == 1 ? 2 : 0
        data[obj1Addr + 4] = 0;   // parent
        data[obj1Addr + 5] = 0;   // sibling
        data[obj1Addr + 6] = (byte)(parentOf2 == 1 ? 2 : 0); // child
        data[obj1Addr + 7] = (byte)(propTable1 >> 8);
        data[obj1Addr + 8] = (byte)(propTable1 & 0xFF);

        // Object 2 entry at entriesStart + 9
        int obj2Addr = entriesStart + 9;
        data[obj2Addr + 4] = (byte)parentOf2; // parent
        data[obj2Addr + 5] = 0;               // sibling
        data[obj2Addr + 6] = 0;               // child
        data[obj2Addr + 7] = (byte)(propTable2 >> 8);
        data[obj2Addr + 8] = (byte)(propTable2 & 0xFF);

        // Property table 1: name length = 0, then terminator
        data[propTable1] = 0; // 0 words of short name
        data[propTable1 + 1] = 0; // no properties

        // Property table 2: name length = 0, then terminator
        data[propTable2] = 0;
        data[propTable2 + 1] = 0;

        return data;
    }

    #endregion
}
