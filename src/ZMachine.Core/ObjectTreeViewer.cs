using System.Text;

namespace ZMachine.Core;

/// <summary>
/// Read-only viewer for the Z-Machine object tree. Walks the object table
/// to build a hierarchy and provides detail extraction (attributes,
/// properties, short names), search/filter, and plain-text export.
/// </summary>
/// <remarks>
/// ZSpec S12 — Object table layout and tree structure.
/// ZSpec S12.3 — Object entries: attributes, parent/sibling/child, property pointer.
/// ZSpec S12.4 — Property blocks and size bytes.
/// </remarks>
public class ObjectTreeViewer
{
    private readonly Memory _memory;
    private readonly ObjectTable _objectTable;
    private readonly TextDecoder _textDecoder;
    private readonly int _version;
    private readonly int _maxObjects;

    /// <summary>
    /// Creates a viewer for the object tree in the given memory image.
    /// </summary>
    public ObjectTreeViewer(Memory memory)
    {
        _memory = memory;
        _version = memory.Version;
        _maxObjects = _version <= 3 ? 255 : 65535;

        int objTableAddr = memory.ReadWord(0x0A);
        int abbrAddr = memory.ReadWord(0x18);
        int alphabetAddr = _version >= 5 ? memory.ReadWord(0x34) : 0;

        _objectTable = new ObjectTable(memory, _version, objTableAddr);
        _textDecoder = new TextDecoder(memory, _version, abbrAddr, alphabetAddr);
    }

    /// <summary>
    /// Returns the total number of objects in the story file, determined
    /// by scanning for the highest valid object number.
    /// </summary>
    /// <remarks>
    /// ZSpec S12 — The spec doesn't store an explicit object count. The
    /// standard technique is to note that the property data for object 1
    /// immediately follows the last object entry, so the object count is
    /// (prop_table_of_obj_1 - entries_start) / entry_size.
    /// </remarks>
    public int GetObjectCount()
    {
        int objTableAddr = _memory.ReadWord(0x0A);
        int defaultsSize = _version <= 3 ? 31 * 2 : 63 * 2;
        int entriesStart = objTableAddr + defaultsSize;
        int entrySize = _version <= 3 ? 9 : 14;

        int propTableOfObj1 = _objectTable.GetPropertyTableAddress(1);
        if (propTableOfObj1 <= entriesStart)
            return 0;

        return (propTableOfObj1 - entriesStart) / entrySize;
    }

    /// <summary>
    /// Returns the decoded short name of the given object.
    /// </summary>
    public string GetShortName(int obj)
    {
        int nameAddr = _objectTable.GetShortNameAddress(obj);
        int nameWordCount = _memory.ReadByte(nameAddr - 1);
        if (nameWordCount == 0)
            return "";

        var (text, _) = _textDecoder.DecodeZString(nameAddr);
        return text;
    }

    /// <summary>
    /// Returns detailed information about a single object: its number,
    /// short name, tree pointers, attribute list, and property list.
    /// </summary>
    public ObjectDetail GetObjectDetail(int obj)
    {
        string name = GetShortName(obj);

        int parent = _objectTable.GetParent(obj);
        int sibling = _objectTable.GetSibling(obj);
        int child = _objectTable.GetChild(obj);

        string parentName = parent > 0 ? GetShortName(parent) : "";
        string siblingName = sibling > 0 ? GetShortName(sibling) : "";
        string childName = child > 0 ? GetShortName(child) : "";

        var attributes = GetAttributes(obj);
        var properties = GetProperties(obj);

        return new ObjectDetail(
            obj, name,
            parent, parentName,
            sibling, siblingName,
            child, childName,
            attributes, properties);
    }

    /// <summary>
    /// Returns all root objects (objects whose parent is 0) as tree nodes,
    /// each with its full subtree of children.
    /// </summary>
    public List<ObjectNode> GetTree()
    {
        int count = GetObjectCount();
        var roots = new List<ObjectNode>();

        for (int i = 1; i <= count; i++)
        {
            if (_objectTable.GetParent(i) == 0)
                roots.Add(BuildNode(i));
        }

        return roots;
    }

    /// <summary>
    /// Returns a flat list of all objects matching the given filter.
    /// Matches by name substring (case-insensitive) or exact object
    /// number if the query is numeric.
    /// </summary>
    public List<ObjectNode> Search(string query)
    {
        var results = new List<ObjectNode>();
        int count = GetObjectCount();

        bool isNumber = int.TryParse(query, out int targetNum);

        for (int i = 1; i <= count; i++)
        {
            if (isNumber && i == targetNum)
            {
                results.Add(new ObjectNode(i, GetShortName(i), []));
                continue;
            }

            string name = GetShortName(i);
            if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
                results.Add(new ObjectNode(i, name, []));
        }

        return results;
    }

    /// <summary>
    /// Exports the full object tree as indented plain text, suitable
    /// for offline analysis. Each line shows the object number and name.
    /// </summary>
    public string Export()
    {
        var sb = new StringBuilder();
        int count = GetObjectCount();

        sb.AppendLine($"Object tree: {count} objects, Z-Machine V{_version}");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine();

        var roots = GetTree();
        foreach (var root in roots)
            ExportNode(sb, root, 0);

        sb.AppendLine();
        sb.AppendLine(new string('=', 60));
        sb.AppendLine("Full object details:");
        sb.AppendLine();

        for (int i = 1; i <= count; i++)
        {
            var detail = GetObjectDetail(i);
            ExportDetail(sb, detail);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    #region Tree Building

    /// <summary>
    /// Recursively builds an ObjectNode for the given object,
    /// following child and sibling pointers.
    /// </summary>
    private ObjectNode BuildNode(int obj)
    {
        string name = GetShortName(obj);
        var children = new List<ObjectNode>();

        int child = _objectTable.GetChild(obj);
        while (child > 0)
        {
            children.Add(BuildNode(child));
            child = _objectTable.GetSibling(child);
        }

        return new ObjectNode(obj, name, children);
    }

    #endregion

    #region Attribute and Property Extraction

    /// <summary>
    /// Returns the list of attribute numbers that are set on the object.
    /// </summary>
    private List<int> GetAttributes(int obj)
    {
        int maxAttr = _version <= 3 ? 31 : 47;
        var set = new List<int>();

        for (int a = 0; a <= maxAttr; a++)
        {
            if (_objectTable.TestAttribute(obj, a))
                set.Add(a);
        }

        return set;
    }

    /// <summary>
    /// Returns all properties defined on the object, walking the property
    /// list via GetNextProperty.
    /// </summary>
    private List<PropertyInfo> GetProperties(int obj)
    {
        var props = new List<PropertyInfo>();
        int prop = _objectTable.GetNextProperty(obj, 0);

        while (prop != 0)
        {
            int dataAddr = _objectTable.GetPropertyAddress(obj, prop);
            int dataLen = _objectTable.GetPropertyLength(dataAddr);

            var dataBytes = new byte[dataLen];
            for (int i = 0; i < dataLen; i++)
                dataBytes[i] = _memory.ReadByte(dataAddr + i);

            string hex = BitConverter.ToString(dataBytes).Replace("-", " ");

            // ZSpec S12.4 — 1-byte and 2-byte properties have word interpretations.
            string interpreted = dataLen switch
            {
                1 => $"{dataBytes[0]}",
                2 => $"{(_memory.ReadWord(dataAddr))}",
                _ => ""
            };

            props.Add(new PropertyInfo(prop, dataAddr, dataLen, hex, interpreted));
            prop = _objectTable.GetNextProperty(obj, prop);
        }

        return props;
    }

    #endregion

    #region Export Formatting

    /// <summary>Recursively writes an indented tree node.</summary>
    private static void ExportNode(StringBuilder sb, ObjectNode node, int depth)
    {
        string indent = new(' ', depth * 2);
        sb.AppendLine($"{indent}[{node.Number}] {node.Name}");

        foreach (var child in node.Children)
            ExportNode(sb, child, depth + 1);
    }

    /// <summary>Writes the full detail block for one object.</summary>
    private static void ExportDetail(StringBuilder sb, ObjectDetail detail)
    {
        sb.AppendLine($"--- Object {detail.Number}: \"{detail.Name}\" ---");
        sb.AppendLine($"  Parent:  {detail.Parent} \"{detail.ParentName}\"");
        sb.AppendLine($"  Sibling: {detail.Sibling} \"{detail.SiblingName}\"");
        sb.AppendLine($"  Child:   {detail.Child} \"{detail.ChildName}\"");

        if (detail.Attributes.Count > 0)
            sb.AppendLine($"  Attributes: {string.Join(", ", detail.Attributes)}");
        else
            sb.AppendLine("  Attributes: (none)");

        if (detail.Properties.Count > 0)
        {
            sb.AppendLine("  Properties:");
            foreach (var p in detail.Properties)
            {
                string val = string.IsNullOrEmpty(p.Interpreted)
                    ? p.DataHex
                    : $"{p.DataHex} (= {p.Interpreted})";
                sb.AppendLine($"    P{p.Number:D2} [{p.DataLength}B] @ ${p.DataAddress:X4}: {val}");
            }
        }
        else
        {
            sb.AppendLine("  Properties: (none)");
        }
    }

    #endregion
}

/// <summary>
/// A node in the object tree with its number, name, and child nodes.
/// </summary>
public record ObjectNode(int Number, string Name, List<ObjectNode> Children);

/// <summary>
/// Full detail for a single object: tree pointers with names,
/// attribute list, and property list.
/// </summary>
public record ObjectDetail(
    int Number,
    string Name,
    int Parent,
    string ParentName,
    int Sibling,
    string SiblingName,
    int Child,
    string ChildName,
    List<int> Attributes,
    List<PropertyInfo> Properties);

/// <summary>
/// A single property entry: number, data address, data length,
/// hex representation, and word-interpreted value (for 1–2 byte props).
/// </summary>
public record PropertyInfo(
    int Number,
    int DataAddress,
    int DataLength,
    string DataHex,
    string Interpreted);
