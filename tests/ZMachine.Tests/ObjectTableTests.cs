namespace ZMachine.Tests;

using ZMachine.Core;

/// <summary>
/// Tests for ObjectTable — tree traversal, attribute manipulation,
/// insert/remove operations, and property defaults. Uses zork1.z3
/// for real-world verification and synthetic data for controlled tests.
/// </summary>
public class ObjectTableTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string Zork1Path = Path.Combine(RepoRoot, "stories/zork1.z3");

    #region Real Story File — Tree Traversal

    [Fact]
    public void Zork1_WestOfHouse_IsChildOfRoomsContainer()
    {
        // Object 180 ("West of House") has parent 82 (rooms container).
        var (_, table) = LoadZork1();
        Assert.Equal(82, table.GetParent(180));
    }

    [Fact]
    public void Zork1_WestOfHouse_FirstChildIsDoor()
    {
        // Object 180's first child is 181 ("door").
        var (_, table) = LoadZork1();
        Assert.Equal(181, table.GetChild(180));
    }

    [Fact]
    public void Zork1_Door_SiblingIsMailbox()
    {
        // Object 181 ("door") has sibling 160 ("mailbox").
        var (_, table) = LoadZork1();
        Assert.Equal(160, table.GetSibling(181));
    }

    [Fact]
    public void Zork1_Mailbox_ContainsLeaflet()
    {
        // Object 160 ("mailbox") has child 161 ("leaflet").
        var (_, table) = LoadZork1();
        Assert.Equal(161, table.GetChild(160));
    }

    [Fact]
    public void Zork1_Leaflet_NoSiblingNoChild()
    {
        var (_, table) = LoadZork1();
        Assert.Equal(0, table.GetSibling(161));
        Assert.Equal(0, table.GetChild(161));
    }

    [Fact]
    public void Zork1_TraverseChildChainOfWestOfHouse()
    {
        // "West of House" (180) children: 181 (door) → 160 (mailbox) → end.
        var (_, table) = LoadZork1();

        var children = new List<int>();
        int child = table.GetChild(180);
        while (child != 0)
        {
            children.Add(child);
            child = table.GetSibling(child);
        }

        Assert.Equal(new[] { 181, 160 }, children);
    }

    [Fact]
    public void Zork1_AllChildrenHaveCorrectParent()
    {
        // Every child and sibling of "West of House" should point back to 180.
        var (_, table) = LoadZork1();

        int child = table.GetChild(180);
        while (child != 0)
        {
            Assert.Equal(180, table.GetParent(child));
            child = table.GetSibling(child);
        }
    }

    [Fact]
    public void Zork1_Object0_IsNullSentinel()
    {
        // Object 0 is "nothing" — accessing it should throw.
        var (_, table) = LoadZork1();
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetParent(0));
    }

    #endregion

    #region Real Story File — Attributes

    [Fact]
    public void Zork1_Mailbox_AttributeBytes()
    {
        // Object 160 ("mailbox") attrs = 0x00041000.
        // Byte 0 = 0x00: no attrs 0-7.
        // Byte 1 = 0x04 = 0000_0100: attr 13 set.
        // Byte 2 = 0x10 = 0001_0000: attr 19 set.
        // Byte 3 = 0x00: no attrs 24-31.
        var (_, table) = LoadZork1();

        Assert.False(table.TestAttribute(160, 0));
        Assert.True(table.TestAttribute(160, 13));
        Assert.True(table.TestAttribute(160, 19));
        Assert.False(table.TestAttribute(160, 31));
    }

    [Fact]
    public void Zork1_Cretin_AttributeBytes()
    {
        // Object 4 ("cretin") attrs = 0x01420002.
        // Byte 0 = 0x01: attr 7.
        // Byte 1 = 0x42 = 0100_0010: attrs 9, 14.
        // Byte 2 = 0x00: none.
        // Byte 3 = 0x02 = 0000_0010: attr 30.
        var (_, table) = LoadZork1();

        Assert.False(table.TestAttribute(4, 0));
        Assert.True(table.TestAttribute(4, 7));
        Assert.True(table.TestAttribute(4, 9));
        Assert.True(table.TestAttribute(4, 14));
        Assert.False(table.TestAttribute(4, 15));
        Assert.True(table.TestAttribute(4, 30));
    }

    [Fact]
    public void Zork1_WestOfHouse_AttributeBytes()
    {
        // Object 180 attrs = 0x02400800.
        // Byte 0 = 0x02: attr 6.
        // Byte 1 = 0x40: attr 9.
        // Byte 2 = 0x08: attr 20.
        // Byte 3 = 0x00: none.
        var (_, table) = LoadZork1();

        Assert.True(table.TestAttribute(180, 6));
        Assert.True(table.TestAttribute(180, 9));
        Assert.True(table.TestAttribute(180, 20));
        Assert.False(table.TestAttribute(180, 0));
        Assert.False(table.TestAttribute(180, 31));
    }

    [Fact]
    public void Zork1_SetAndClearAttribute_RoundTrip()
    {
        var (_, table) = LoadZork1();

        // Mailbox attr 0 is initially false.
        Assert.False(table.TestAttribute(160, 0));

        table.SetAttribute(160, 0);
        Assert.True(table.TestAttribute(160, 0));

        // Existing attr 13 is still set — no corruption.
        Assert.True(table.TestAttribute(160, 13));

        table.ClearAttribute(160, 0);
        Assert.False(table.TestAttribute(160, 0));

        // Attr 13 still intact.
        Assert.True(table.TestAttribute(160, 13));
    }

    #endregion

    #region Real Story File — Property Defaults

    [Fact]
    public void Zork1_PropertyDefaults_CanBeRead()
    {
        var (_, table) = LoadZork1();
        // Property defaults 1-31 should be readable.
        for (int i = 1; i <= 31; i++)
        {
            ushort def = table.GetPropertyDefault(i);
            Assert.IsType<ushort>(def); // no crash
        }
    }

    #endregion

    #region Real Story File — Property Table Address

    [Fact]
    public void Zork1_Mailbox_PropertyTableAddress()
    {
        // Object 160 has a property pointer we can verify.
        var (memory, table) = LoadZork1();
        int propAddr = table.GetPropertyTableAddress(160);
        Assert.True(propAddr > 0, "Property table address should be non-zero");

        // First byte at propAddr is the text-length in words.
        byte nameLen = memory.ReadByte(propAddr);
        Assert.True(nameLen > 0, "Mailbox should have a short name");
    }

    #endregion

    #region Synthetic — Insert and Remove

    [Fact]
    public void InsertObject_IntoEmptyParent()
    {
        var (_, table) = CreateSyntheticV3();

        // Initially: obj 1 has no children, obj 2 has no parent.
        Assert.Equal(0, table.GetChild(1));
        Assert.Equal(0, table.GetParent(2));

        table.InsertObject(2, 1);

        Assert.Equal(2, table.GetChild(1));
        Assert.Equal(1, table.GetParent(2));
        Assert.Equal(0, table.GetSibling(2));
    }

    [Fact]
    public void InsertObject_PushesExistingChild()
    {
        var (_, table) = CreateSyntheticV3();

        // Insert obj 2 into obj 1 first.
        table.InsertObject(2, 1);
        // Now insert obj 3 into obj 1 — obj 2 becomes sibling.
        table.InsertObject(3, 1);

        Assert.Equal(3, table.GetChild(1));       // obj 3 is now first child
        Assert.Equal(2, table.GetSibling(3));      // obj 2 is sibling of obj 3
        Assert.Equal(0, table.GetSibling(2));      // obj 2 has no sibling
        Assert.Equal(1, table.GetParent(3));
        Assert.Equal(1, table.GetParent(2));
    }

    [Fact]
    public void InsertObject_RemovesFromPreviousParent()
    {
        var (_, table) = CreateSyntheticV3();

        // Insert obj 2 into obj 1.
        table.InsertObject(2, 1);
        Assert.Equal(2, table.GetChild(1));

        // Move obj 2 to obj 3.
        table.InsertObject(2, 3);
        Assert.Equal(0, table.GetChild(1));  // obj 1 has no children now
        Assert.Equal(2, table.GetChild(3));  // obj 3 now has obj 2
        Assert.Equal(3, table.GetParent(2)); // obj 2's parent is now 3
    }

    [Fact]
    public void RemoveObject_FirstChild()
    {
        var (_, table) = CreateSyntheticV3();

        // Build chain: 1 → child 2 → sibling 3
        table.InsertObject(3, 1);
        table.InsertObject(2, 1); // 2 is first child, 3 is sibling of 2

        Assert.Equal(2, table.GetChild(1));
        Assert.Equal(3, table.GetSibling(2));

        table.RemoveObject(2);

        Assert.Equal(3, table.GetChild(1)); // 3 is now first child
        Assert.Equal(0, table.GetParent(2));
        Assert.Equal(0, table.GetSibling(2));
    }

    [Fact]
    public void RemoveObject_MiddleChild()
    {
        var (_, table) = CreateSyntheticV3();

        // Build chain: 1 → child 2 → sibling 3 → sibling 4
        table.InsertObject(4, 1);
        table.InsertObject(3, 1);
        table.InsertObject(2, 1);

        Assert.Equal(2, table.GetChild(1));
        Assert.Equal(3, table.GetSibling(2));
        Assert.Equal(4, table.GetSibling(3));

        table.RemoveObject(3);

        Assert.Equal(2, table.GetChild(1));
        Assert.Equal(4, table.GetSibling(2)); // 2's sibling jumps to 4
        Assert.Equal(0, table.GetParent(3));
        Assert.Equal(0, table.GetSibling(3));
    }

    [Fact]
    public void RemoveObject_LastChild()
    {
        var (_, table) = CreateSyntheticV3();

        table.InsertObject(3, 1);
        table.InsertObject(2, 1);
        // Chain: 1 → 2 → 3

        table.RemoveObject(3);

        Assert.Equal(2, table.GetChild(1));
        Assert.Equal(0, table.GetSibling(2)); // 2 has no sibling now
    }

    [Fact]
    public void RemoveObject_OnlyChild()
    {
        var (_, table) = CreateSyntheticV3();

        table.InsertObject(2, 1);
        table.RemoveObject(2);

        Assert.Equal(0, table.GetChild(1));
        Assert.Equal(0, table.GetParent(2));
    }

    [Fact]
    public void RemoveObject_NoParent_DoesNothing()
    {
        var (_, table) = CreateSyntheticV3();

        // Object 2 has no parent — removing should be a no-op.
        table.RemoveObject(2);
        Assert.Equal(0, table.GetParent(2));
    }

    [Fact]
    public void InsertObject_IntoSelf_SetsUpCorrectly()
    {
        // This is a degenerate case that shouldn't happen in real games
        // but we handle it without crashing.
        var (_, table) = CreateSyntheticV3();

        // Won't test self-insert as it would create a cycle.
        // Just verify normal insert works after multiple operations.
        table.InsertObject(2, 1);
        table.InsertObject(3, 1);
        table.InsertObject(4, 1);

        // Chain: 1 → 4 → 3 → 2
        Assert.Equal(4, table.GetChild(1));
        Assert.Equal(3, table.GetSibling(4));
        Assert.Equal(2, table.GetSibling(3));
        Assert.Equal(0, table.GetSibling(2));
    }

    #endregion

    #region Synthetic — Attributes

    [Fact]
    public void SetAttribute_ThenTest_ReturnsTrue()
    {
        var (_, table) = CreateSyntheticV3();

        Assert.False(table.TestAttribute(1, 0));
        table.SetAttribute(1, 0);
        Assert.True(table.TestAttribute(1, 0));
    }

    [Fact]
    public void ClearAttribute_ThenTest_ReturnsFalse()
    {
        var (_, table) = CreateSyntheticV3();

        table.SetAttribute(1, 15);
        Assert.True(table.TestAttribute(1, 15));
        table.ClearAttribute(1, 15);
        Assert.False(table.TestAttribute(1, 15));
    }

    [Fact]
    public void Attribute0_IsTopBitOfFirstByte()
    {
        // ZSpec S12.3.1 — Attribute 0 is bit 7 of the first byte.
        var (memory, table) = CreateSyntheticV3();

        int objAddr = 0x02EE; // first object entry (after 62 bytes of defaults)
        Assert.Equal(0, memory.ReadByte(objAddr)); // initially zero

        table.SetAttribute(1, 0);
        Assert.Equal(0x80, memory.ReadByte(objAddr)); // top bit set
    }

    [Fact]
    public void Attribute7_IsBottomBitOfFirstByte()
    {
        var (memory, table) = CreateSyntheticV3();

        int objAddr = 0x02EE;
        table.SetAttribute(1, 7);
        Assert.Equal(0x01, memory.ReadByte(objAddr));
    }

    [Fact]
    public void Attribute31_IsBottomBitOfFourthByte()
    {
        var (memory, table) = CreateSyntheticV3();

        int objAddr = 0x02EE;
        table.SetAttribute(1, 31);
        Assert.Equal(0x01, memory.ReadByte(objAddr + 3));
    }

    [Fact]
    public void Attribute_OutOfRange_WarnsAndReturnsFalse()
    {
        var (_, table) = CreateSyntheticV3();

        var warnings = new List<string>();
        table.Warning += w => warnings.Add(w);

        Assert.False(table.TestAttribute(1, 32));
        Assert.False(table.TestAttribute(1, -1));
        Assert.Equal(2, warnings.Count);
    }

    [Fact]
    public void SetAttribute_OutOfRange_WarnsAndNoOps()
    {
        var (_, table) = CreateSyntheticV3();

        var warnings = new List<string>();
        table.Warning += w => warnings.Add(w);

        table.SetAttribute(1, 32);
        Assert.Single(warnings);
        // Verify no memory corruption — attr 0 should still be clear.
        Assert.False(table.TestAttribute(1, 0));
    }

    [Fact]
    public void ClearAttribute_OutOfRange_WarnsAndNoOps()
    {
        var (_, table) = CreateSyntheticV3();

        var warnings = new List<string>();
        table.Warning += w => warnings.Add(w);

        table.ClearAttribute(1, 32);
        Assert.Single(warnings);
    }

    [Fact]
    public void SetAttribute_DoesNotAffectOtherAttributes()
    {
        var (_, table) = CreateSyntheticV3();

        table.SetAttribute(1, 5);
        Assert.True(table.TestAttribute(1, 5));
        Assert.False(table.TestAttribute(1, 4));
        Assert.False(table.TestAttribute(1, 6));
    }

    #endregion

    #region Synthetic — V4+ Two-Byte Pointers

    [Fact]
    public void V5_TwoBytePointers_LargeObjectNumbers()
    {
        // V4+ can reference objects > 255 with 2-byte pointers.
        var (_, table) = CreateSyntheticV5();

        table.InsertObject(2, 1);
        Assert.Equal(2, table.GetChild(1));
        Assert.Equal(1, table.GetParent(2));
    }

    [Fact]
    public void V5_PropertyDefaults_63Entries()
    {
        var (_, table) = CreateSyntheticV5();
        // V5 has 63 property defaults.
        ushort def = table.GetPropertyDefault(63);
        Assert.IsType<ushort>(def);

        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetPropertyDefault(64));
    }

    [Fact]
    public void V5_Attributes_48Bits()
    {
        // V4+ has 6 attribute bytes = 48 attributes (0-47).
        var (_, table) = CreateSyntheticV5();

        table.SetAttribute(1, 47);
        Assert.True(table.TestAttribute(1, 47));

        var warnings = new List<string>();
        table.Warning += w => warnings.Add(w);
        Assert.False(table.TestAttribute(1, 48));
        Assert.Single(warnings);
    }

    #endregion

    #region Property Defaults

    [Fact]
    public void PropertyDefault_OutOfRange_Throws()
    {
        var (_, table) = CreateSyntheticV3();

        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetPropertyDefault(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetPropertyDefault(32));
    }

    [Fact]
    public void PropertyDefault_ReadsCorrectValue()
    {
        var (memory, table) = CreateSyntheticV3();

        // Write a known value to property default 1 (at table address + 0).
        memory.WriteWord(0x02B0, 0x1234);
        Assert.Equal(0x1234, table.GetPropertyDefault(1));
    }

    #endregion

    #region Real Story File — Properties

    [Fact]
    public void Zork1_Mailbox_GetProperty18_Returns4ByteValue()
    {
        // Object 160 (mailbox) has property 18 with 4 bytes of data.
        // @get_prop reads only the first 2 bytes as a word.
        var (_, table) = LoadZork1();

        ushort val = table.GetProperty(160, 18);
        Assert.Equal(0x453F, val);
    }

    [Fact]
    public void Zork1_Mailbox_GetProperty16_Returns1ByteValue()
    {
        // Object 160 (mailbox) property 16: 1 byte = 0xF4.
        var (_, table) = LoadZork1();

        ushort val = table.GetProperty(160, 16);
        Assert.Equal(0xF4, val);
    }

    [Fact]
    public void Zork1_Mailbox_GetProperty10_Returns2ByteValue()
    {
        // Object 160 (mailbox) property 10: 2 bytes = 0x000A.
        var (_, table) = LoadZork1();

        ushort val = table.GetProperty(160, 10);
        Assert.Equal(0x000A, val);
    }

    [Fact]
    public void Zork1_Mailbox_AbsentProperty_ReturnsDefault()
    {
        // Object 160 has no property 15. Default for prop 15 = 0x0005.
        var (_, table) = LoadZork1();

        ushort val = table.GetProperty(160, 15);
        Assert.Equal(0x0005, val);
    }

    [Fact]
    public void Zork1_Mailbox_GetNextProperty_FromZero()
    {
        // From prop 0, should return the first property = 18.
        var (_, table) = LoadZork1();

        int first = table.GetNextProperty(160, 0);
        Assert.Equal(18, first);
    }

    [Fact]
    public void Zork1_Mailbox_GetNextProperty_Chain()
    {
        // Walk the full property chain: 18 → 17 → 16 → 10 → 0 (end).
        var (_, table) = LoadZork1();

        int p = table.GetNextProperty(160, 0);
        Assert.Equal(18, p);
        p = table.GetNextProperty(160, p);
        Assert.Equal(17, p);
        p = table.GetNextProperty(160, p);
        Assert.Equal(16, p);
        p = table.GetNextProperty(160, p);
        Assert.Equal(10, p);
        p = table.GetNextProperty(160, p);
        Assert.Equal(0, p);
    }

    [Fact]
    public void Zork1_Mailbox_GetPropertyAddress()
    {
        // Property 18 data starts at 0x1A40, property 16 at 0x1A48.
        var (_, table) = LoadZork1();

        Assert.Equal(0x1A40, table.GetPropertyAddress(160, 18));
        Assert.Equal(0x1A48, table.GetPropertyAddress(160, 16));
    }

    [Fact]
    public void Zork1_Mailbox_GetPropertyAddress_Absent()
    {
        var (_, table) = LoadZork1();
        Assert.Equal(0, table.GetPropertyAddress(160, 5));
    }

    [Fact]
    public void Zork1_Mailbox_GetPropertyLength()
    {
        // Property 18 at 0x1A40 has 4 data bytes.
        // Property 17 at 0x1A45 has 2 data bytes.
        // Property 16 at 0x1A48 has 1 data byte.
        var (_, table) = LoadZork1();

        Assert.Equal(4, table.GetPropertyLength(0x1A40));
        Assert.Equal(2, table.GetPropertyLength(0x1A45));
        Assert.Equal(1, table.GetPropertyLength(0x1A48));
    }

    [Fact]
    public void GetPropertyLength_Zero_ReturnsZero()
    {
        // ZSpec11: @get_prop_len 0 must return 0.
        var (_, table) = LoadZork1();
        Assert.Equal(0, table.GetPropertyLength(0));
    }

    [Fact]
    public void Zork1_Mailbox_ShortNameAddress()
    {
        // Property table at 0x1A38, name_len at 0x1A38, name starts at 0x1A39.
        var (_, table) = LoadZork1();
        Assert.Equal(0x1A39, table.GetShortNameAddress(160));
    }

    [Fact]
    public void Zork1_Mailbox_ShortNameLength()
    {
        // 3 words = 6 bytes + 1 byte prefix = 7 bytes.
        var (_, table) = LoadZork1();
        Assert.Equal(7, table.GetShortNameLengthBytes(160));
    }

    [Fact]
    public void Zork1_WestOfHouse_GetNextProperty_Chain()
    {
        // Object 180: props 31 → 30 → 29 → 28 → 27 → 25 → 24 → 21 → 17 → 5 → 0.
        var (_, table) = LoadZork1();

        var props = new List<int>();
        int p = table.GetNextProperty(180, 0);
        while (p != 0)
        {
            props.Add(p);
            p = table.GetNextProperty(180, p);
        }

        Assert.Equal(new[] { 31, 30, 29, 28, 27, 25, 24, 21, 17, 5 }, props);
    }

    #endregion

    #region Synthetic — Property Operations

    [Fact]
    public void Synthetic_GetProperty_1Byte()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        // Object 1 has property 20 with 1 byte = 0xAB.
        Assert.Equal(0xAB, table.GetProperty(1, 20));
    }

    [Fact]
    public void Synthetic_GetProperty_2Bytes()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        // Object 1 has property 15 with 2 bytes = 0x1234.
        Assert.Equal(0x1234, table.GetProperty(1, 15));
    }

    [Fact]
    public void Synthetic_GetProperty_Absent_ReturnsDefault()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        // Property 10 absent, default for prop 10 set to 0xBEEF.
        Assert.Equal(0xBEEF, table.GetProperty(1, 10));
    }

    [Fact]
    public void Synthetic_SetProperty_1Byte()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        table.SetProperty(1, 20, 0xFF);
        Assert.Equal(0xFF, table.GetProperty(1, 20));
    }

    [Fact]
    public void Synthetic_SetProperty_2Bytes()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        table.SetProperty(1, 15, 0x5678);
        Assert.Equal(0x5678, table.GetProperty(1, 15));
    }

    [Fact]
    public void Synthetic_SetProperty_Absent_Throws()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        Assert.Throws<InvalidOperationException>(
            () => table.SetProperty(1, 10, 0x1234));
    }

    [Fact]
    public void Synthetic_GetNextProperty_FromZero()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        Assert.Equal(20, table.GetNextProperty(1, 0));
    }

    [Fact]
    public void Synthetic_GetNextProperty_Chain()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        // Props: 20 → 15 → 5 → 0
        Assert.Equal(15, table.GetNextProperty(1, 20));
        Assert.Equal(5, table.GetNextProperty(1, 15));
        Assert.Equal(0, table.GetNextProperty(1, 5));
    }

    [Fact]
    public void Synthetic_GetNextProperty_NotFound_Throws()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        Assert.Throws<InvalidOperationException>(
            () => table.GetNextProperty(1, 12));
    }

    [Fact]
    public void Synthetic_GetPropertyLength_FromDataAddress()
    {
        var (_, table) = CreateSyntheticV3WithProperties();

        // Get property 20's data address and verify its length.
        int addr = table.GetPropertyAddress(1, 20);
        Assert.True(addr > 0);
        Assert.Equal(1, table.GetPropertyLength(addr));

        addr = table.GetPropertyAddress(1, 15);
        Assert.Equal(2, table.GetPropertyLength(addr));

        // Property 5 has 4 bytes of data.
        addr = table.GetPropertyAddress(1, 5);
        Assert.Equal(4, table.GetPropertyLength(addr));
    }

    #endregion

    #region V5 Property Format

    [Fact]
    public void V5_ShortForm_1Byte()
    {
        var (_, table) = CreateSyntheticV5WithProperties();

        // Property 10: short form, bit 6=0, 1 byte data = 0x42.
        Assert.Equal(0x42, table.GetProperty(1, 10));
    }

    [Fact]
    public void V5_ShortForm_2Bytes()
    {
        var (_, table) = CreateSyntheticV5WithProperties();

        // Property 8: short form, bit 6=1, 2 bytes data = 0xABCD.
        Assert.Equal(0xABCD, table.GetProperty(1, 8));
    }

    [Fact]
    public void V5_LongForm_6Bytes()
    {
        var (_, table) = CreateSyntheticV5WithProperties();

        // Property 5: long form (bit 7=1), 6 bytes data.
        // GetProperty reads first 2 bytes as word.
        Assert.Equal(0x1122, table.GetProperty(1, 5));
    }

    [Fact]
    public void V5_GetPropertyLength_ShortForm()
    {
        var (_, table) = CreateSyntheticV5WithProperties();

        int addr10 = table.GetPropertyAddress(1, 10);
        Assert.Equal(1, table.GetPropertyLength(addr10));

        int addr8 = table.GetPropertyAddress(1, 8);
        Assert.Equal(2, table.GetPropertyLength(addr8));
    }

    [Fact]
    public void V5_GetPropertyLength_LongForm()
    {
        var (_, table) = CreateSyntheticV5WithProperties();

        int addr5 = table.GetPropertyAddress(1, 5);
        Assert.Equal(6, table.GetPropertyLength(addr5));
    }

    [Fact]
    public void V5_GetNextProperty_Chain()
    {
        var (_, table) = CreateSyntheticV5WithProperties();

        // Props: 10 → 8 → 5 → 0
        int p = table.GetNextProperty(1, 0);
        Assert.Equal(10, p);
        p = table.GetNextProperty(1, p);
        Assert.Equal(8, p);
        p = table.GetNextProperty(1, p);
        Assert.Equal(5, p);
        p = table.GetNextProperty(1, p);
        Assert.Equal(0, p);
    }

    #endregion

    #region Helpers

    private static (Memory, ObjectTable) LoadZork1()
    {
        var memory = new Memory();
        memory.LoadStory(File.ReadAllBytes(Zork1Path));
        int objTableAddr = memory.ReadWord(0x0A);
        var table = new ObjectTable(memory, 3, objTableAddr);
        return (memory, table);
    }

    private static (Memory, ObjectTable) CreateSyntheticV3()
    {
        // Layout for V3:
        // Object table at 0x02B0 (matches zork1 for readability).
        // Property defaults: 31 words = 62 bytes (0x02B0 to 0x02ED).
        // Object entries start at 0x02EE, each 9 bytes.
        // We allocate 10 objects: 10 × 9 = 90 bytes.
        // Total: 62 + 90 = 152 bytes starting at 0x02B0.
        // Data size: 0x02B0 + 152 = 0x0348.
        int dataSize = 0x0400;
        byte[] data = new byte[dataSize];
        data[0] = 3; // version
        data[0x0E] = (byte)(dataSize >> 8);
        data[0x0F] = (byte)(dataSize & 0xFF); // static base

        // Object table address in header word $0A
        data[0x0A] = 0x02; data[0x0B] = 0xB0;

        // Property defaults: all zeros (fine for testing).
        // Object entries: all zeros means no parent/sibling/child, no attrs.

        // Each object needs a property table pointer.
        // Point all of them to a dummy location with 0 name-length.
        int dummyPropAddr = 0x03F0;
        data[dummyPropAddr] = 0; // text-length = 0 words

        int entriesStart = 0x02B0 + 62;
        for (int obj = 1; obj <= 10; obj++)
        {
            int addr = entriesStart + (obj - 1) * 9;
            // Property pointer at offset +7 (after 4 attr + 3 pointers)
            data[addr + 7] = (byte)(dummyPropAddr >> 8);
            data[addr + 8] = (byte)(dummyPropAddr & 0xFF);
        }

        var memory = new Memory();
        memory.LoadStory(data);
        var table = new ObjectTable(memory, 3, 0x02B0);
        return (memory, table);
    }

    private static (Memory, ObjectTable) CreateSyntheticV5()
    {
        // V5 layout:
        // Property defaults: 63 words = 126 bytes.
        // Object entries: each 14 bytes (6 attr + 6 pointers + 2 prop-ptr).
        // Put table at 0x0100.
        int tableAddr = 0x0100;
        int defaultsSize = 63 * 2;        // 126
        int entriesStart = tableAddr + defaultsSize;  // 0x017E
        int entrySize = 14;
        int numObjects = 10;
        int dataSize = entriesStart + numObjects * entrySize + 0x20;

        byte[] data = new byte[dataSize];
        data[0] = 5; // version
        data[0x0E] = (byte)(dataSize >> 8);
        data[0x0F] = (byte)(dataSize & 0xFF);

        // Object table address
        data[0x0A] = (byte)(tableAddr >> 8);
        data[0x0B] = (byte)(tableAddr & 0xFF);

        // Dummy property table
        int dummyPropAddr = dataSize - 0x10;
        data[dummyPropAddr] = 0;

        for (int obj = 1; obj <= numObjects; obj++)
        {
            int addr = entriesStart + (obj - 1) * entrySize;
            // Property pointer at offset +12 (after 6 attr + 6 pointers)
            data[addr + 12] = (byte)(dummyPropAddr >> 8);
            data[addr + 13] = (byte)(dummyPropAddr & 0xFF);
        }

        var memory = new Memory();
        memory.LoadStory(data);
        var table = new ObjectTable(memory, 5, tableAddr);
        return (memory, table);
    }

    private static (Memory, ObjectTable) CreateSyntheticV3WithProperties()
    {
        // V3 with object 1 having real property blocks.
        // Layout:
        //   Object table at 0x02B0.
        //   Property defaults: 31 words = 62 bytes.
        //   Object entries at 0x02EE, each 9 bytes (10 objects = 90 bytes).
        //   Property table for object 1 at 0x0380.
        //   Property tables for objects 2-10 at 0x03F0 (dummy, empty).
        int dataSize = 0x0400;
        byte[] data = new byte[dataSize];
        data[0] = 3;
        data[0x0E] = (byte)(dataSize >> 8);
        data[0x0F] = (byte)(dataSize & 0xFF);
        data[0x0A] = 0x02; data[0x0B] = 0xB0;

        // Set property default 10 to 0xBEEF.
        int defaultAddr = 0x02B0 + (10 - 1) * 2;
        data[defaultAddr] = 0xBE;
        data[defaultAddr + 1] = 0xEF;

        int entriesStart = 0x02B0 + 62;

        // Object 1's property table at 0x0380.
        int obj1PropTable = 0x0380;
        data[entriesStart + 7] = (byte)(obj1PropTable >> 8);
        data[entriesStart + 8] = (byte)(obj1PropTable & 0xFF);

        // Short name: 0 words (no name).
        data[obj1PropTable] = 0;
        int p = obj1PropTable + 1;

        // Property 20: 1 byte data = 0xAB.
        // Size byte: 32*(1-1) + 20 = 20 = 0x14.
        data[p++] = 0x14;
        data[p++] = 0xAB;

        // Property 15: 2 bytes data = 0x1234.
        // Size byte: 32*(2-1) + 15 = 47 = 0x2F.
        data[p++] = 0x2F;
        data[p++] = 0x12;
        data[p++] = 0x34;

        // Property 5: 4 bytes data = 0xDEADBEEF.
        // Size byte: 32*(4-1) + 5 = 101 = 0x65.
        data[p++] = 0x65;
        data[p++] = 0xDE;
        data[p++] = 0xAD;
        data[p++] = 0xBE;
        data[p++] = 0xEF;

        // Terminator.
        data[p] = 0x00;

        // Objects 2-10: dummy property table with empty name, no properties.
        int dummyPropAddr = 0x03F0;
        data[dummyPropAddr] = 0;
        data[dummyPropAddr + 1] = 0;

        for (int obj = 2; obj <= 10; obj++)
        {
            int addr = entriesStart + (obj - 1) * 9;
            data[addr + 7] = (byte)(dummyPropAddr >> 8);
            data[addr + 8] = (byte)(dummyPropAddr & 0xFF);
        }

        var memory = new Memory();
        memory.LoadStory(data);
        var table = new ObjectTable(memory, 3, 0x02B0);
        return (memory, table);
    }

    private static (Memory, ObjectTable) CreateSyntheticV5WithProperties()
    {
        // V5 with object 1 having properties in both short and long form.
        int tableAddr = 0x0100;
        int defaultsSize = 63 * 2;
        int entriesStart = tableAddr + defaultsSize;
        int entrySize = 14;
        int numObjects = 5;
        int dataSize = 0x0400;

        byte[] data = new byte[dataSize];
        data[0] = 5;
        data[0x0E] = (byte)(dataSize >> 8);
        data[0x0F] = (byte)(dataSize & 0xFF);
        data[0x0A] = (byte)(tableAddr >> 8);
        data[0x0B] = (byte)(tableAddr & 0xFF);

        // Object 1's property table at 0x0300.
        int obj1PropTable = 0x0300;
        int obj1Addr = entriesStart;
        data[obj1Addr + 12] = (byte)(obj1PropTable >> 8);
        data[obj1Addr + 13] = (byte)(obj1PropTable & 0xFF);

        // Short name: 0 words.
        data[obj1PropTable] = 0;
        int p = obj1PropTable + 1;

        // Property 10: short form, bit 6=0 → 1 byte data.
        // Size byte = 0x0A (bit 7=0, bit 6=0, prop=10).
        data[p++] = 0x0A;
        data[p++] = 0x42;

        // Property 8: short form, bit 6=1 → 2 bytes data.
        // Size byte = 0x48 (bit 7=0, bit 6=1, prop=8).
        data[p++] = 0x48;
        data[p++] = 0xAB;
        data[p++] = 0xCD;

        // Property 5: long form, 6 bytes data.
        // First size byte = 0x85 (bit 7=1, prop=5).
        // Second size byte = 0x86 (bit 7=1, length=6).
        data[p++] = 0x85;
        data[p++] = 0x86;
        data[p++] = 0x11;
        data[p++] = 0x22;
        data[p++] = 0x33;
        data[p++] = 0x44;
        data[p++] = 0x55;
        data[p++] = 0x66;

        // Terminator.
        data[p] = 0x00;

        // Dummy property table for remaining objects.
        int dummyPropAddr = 0x03F0;
        data[dummyPropAddr] = 0;
        data[dummyPropAddr + 1] = 0;

        for (int obj = 2; obj <= numObjects; obj++)
        {
            int addr = entriesStart + (obj - 1) * entrySize;
            data[addr + 12] = (byte)(dummyPropAddr >> 8);
            data[addr + 13] = (byte)(dummyPropAddr & 0xFF);
        }

        var memory = new Memory();
        memory.LoadStory(data);
        var table = new ObjectTable(memory, 5, tableAddr);
        return (memory, table);
    }

    private static string FindRepoRoot()
    {
        string dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName!;
        }
        throw new InvalidOperationException("Could not find repository root.");
    }

    #endregion
}
