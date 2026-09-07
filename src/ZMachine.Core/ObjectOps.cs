namespace ZMachine.Core;

/// <summary>
/// Implements Z-Machine object manipulation opcodes: tree traversal,
/// property access, attribute testing, and short name printing. These
/// are thin wrappers around <see cref="ObjectTable"/> methods, adapting
/// them to opcode calling conventions (store results, branch conditions).
/// </summary>
/// <remarks>
/// ZSpec S12 — Object table structure.
/// ZSpec S15 — Object manipulation instructions.
/// </remarks>
public class ObjectOps
{
    private readonly ObjectTable _objectTable;
    private readonly TextDecoder _textDecoder;

    public ObjectOps(ObjectTable objectTable, TextDecoder textDecoder)
    {
        _objectTable = objectTable;
        _textDecoder = textDecoder;
    }

    /// <summary>
    /// @get_parent obj → (result). Returns the parent object number.
    /// </summary>
    public ushort GetParent(ushort obj)
    {
        return (ushort)_objectTable.GetParent(obj);
    }

    /// <summary>
    /// @get_child obj → (result) ?(label). Returns the first child
    /// object number and branches if the child is not zero.
    /// </summary>
    public (ushort Child, bool HasChild) GetChild(ushort obj)
    {
        int child = _objectTable.GetChild(obj);
        return ((ushort)child, child != 0);
    }

    /// <summary>
    /// @get_sibling obj → (result) ?(label). Returns the next sibling
    /// object number and branches if the sibling is not zero.
    /// </summary>
    public (ushort Sibling, bool HasSibling) GetSibling(ushort obj)
    {
        int sibling = _objectTable.GetSibling(obj);
        return ((ushort)sibling, sibling != 0);
    }

    /// <summary>
    /// @jin obj1 obj2 ?(label). Branches if obj1 is a direct child of obj2
    /// (i.e., parent of obj1 equals obj2).
    /// </summary>
    /// <remarks>ZSpec S15 — @jin: "Jump if object a is a direct child of b."</remarks>
    public bool JumpIn(ushort obj1, ushort obj2)
    {
        return _objectTable.GetParent(obj1) == obj2;
    }

    /// <summary>
    /// @insert_obj obj destination. Moves obj to be the first child of
    /// destination (removes from old parent first).
    /// </summary>
    public void InsertObj(ushort obj, ushort destination)
    {
        _objectTable.InsertObject(obj, destination);
    }

    /// <summary>
    /// @remove_obj obj. Detaches obj from the object tree (removes from
    /// its parent's child list, sets obj's parent to 0).
    /// </summary>
    public void RemoveObj(ushort obj)
    {
        _objectTable.RemoveObject(obj);
    }

    /// <summary>
    /// @get_prop obj prop → (result). Returns the property value. If the
    /// property is not present, returns the default value from the
    /// property defaults table.
    /// </summary>
    public ushort GetProp(ushort obj, ushort prop)
    {
        return _objectTable.GetProperty(obj, prop);
    }

    /// <summary>
    /// @get_prop_addr obj prop → (result). Returns the byte address of
    /// the property data, or 0 if the property is not present.
    /// </summary>
    public ushort GetPropAddr(ushort obj, ushort prop)
    {
        return (ushort)_objectTable.GetPropertyAddress(obj, prop);
    }

    /// <summary>
    /// @get_prop_len property-data-address → (result). Returns the length
    /// in bytes of the property data at the given address. The address
    /// is the data address (not the size byte). Passing 0 returns 0.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@get_prop_len" — "get_prop_len 0 must return 0."
    /// The address passed is the value returned by @get_prop_addr.
    /// </remarks>
    public ushort GetPropLen(ushort dataAddress)
    {
        return (ushort)_objectTable.GetPropertyLength(dataAddress);
    }

    /// <summary>
    /// @get_next_prop obj prop → (result). Returns the number of the
    /// next property after prop. If prop is 0, returns the first property.
    /// </summary>
    public ushort GetNextProp(ushort obj, ushort prop)
    {
        return (ushort)_objectTable.GetNextProperty(obj, prop);
    }

    /// <summary>
    /// @put_prop obj prop value. Sets a property value. The property
    /// must exist and be 1 or 2 bytes long.
    /// </summary>
    public void PutProp(ushort obj, ushort prop, ushort value)
    {
        _objectTable.SetProperty(obj, prop, value);
    }

    /// <summary>
    /// @test_attr obj attribute ?(label). Branches if the attribute is set.
    /// </summary>
    public bool TestAttr(ushort obj, ushort attribute)
    {
        return _objectTable.TestAttribute(obj, attribute);
    }

    /// <summary>
    /// @set_attr obj attribute. Sets the attribute flag.
    /// </summary>
    public void SetAttr(ushort obj, ushort attribute)
    {
        _objectTable.SetAttribute(obj, attribute);
    }

    /// <summary>
    /// @clear_attr obj attribute. Clears the attribute flag.
    /// </summary>
    public void ClearAttr(ushort obj, ushort attribute)
    {
        _objectTable.ClearAttribute(obj, attribute);
    }

    /// <summary>
    /// @print_obj obj. Returns the short name of the object as a decoded
    /// string. The caller is responsible for sending it to the output
    /// stream.
    /// </summary>
    /// <remarks>
    /// ZSpec S15 — @print_obj: "Print short name of object."
    /// The short name is a Z-string at the start of the object's
    /// property table.
    /// </remarks>
    public string PrintObj(ushort obj)
    {
        int nameAddr = _objectTable.GetShortNameAddress(obj);
        var (text, _) = _textDecoder.DecodeZString(nameAddr);
        return text;
    }
}
