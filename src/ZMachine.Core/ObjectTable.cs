namespace ZMachine.Core;

/// <summary>
/// Provides access to the Z-Machine object table: a tree of objects with
/// parent/sibling/child pointers, attribute flags, and property lists.
/// The table layout differs between versions: V1-3 uses 1-byte tree
/// pointers (max 255 objects), V4+ uses 2-byte pointers (max 65535).
/// </summary>
/// <remarks>
/// ZSpec S12 — Object table layout.
/// ZSpec S12.2 — Property defaults table precedes object entries.
/// ZSpec S12.3 — Object entry structure (attributes, tree, property pointer).
/// </remarks>
public class ObjectTable
{
    private readonly Memory _memory;
    private readonly int _version;
    private readonly int _tableAddress;

    /// <summary>
    /// Number of property defaults: 31 for V1-3, 63 for V4+.
    /// These are the default values for properties 1-31 (or 1-63).
    /// </summary>
    private readonly int _defaultCount;

    /// <summary>
    /// Size of the property defaults table in bytes.
    /// Each default is a 16-bit word.
    /// </summary>
    private readonly int _defaultsSize;

    /// <summary>
    /// Byte address where object entries begin (after property defaults).
    /// </summary>
    private readonly int _entriesStart;

    /// <summary>Size of each object entry in bytes.</summary>
    private readonly int _entrySize;

    /// <summary>Number of attribute bytes per object.</summary>
    private readonly int _attrBytes;

    /// <summary>
    /// Size of each tree pointer (parent/sibling/child) in bytes.
    /// V1-3: 1 byte. V4+: 2 bytes.
    /// </summary>
    private readonly int _pointerSize;

    /// <summary>
    /// Creates an ObjectTable bound to the given memory at the specified
    /// table address. The table address is from header word $0A.
    /// </summary>
    public ObjectTable(Memory memory, int version, int tableAddress)
    {
        _memory = memory;
        _version = version;
        _tableAddress = tableAddress;

        if (_version <= 3)
        {
            // ZSpec S12.2 — V1-3: 31 property defaults (62 bytes).
            // ZSpec S12.3 — V1-3 entry: 4 attr + 1 parent + 1 sibling + 1 child + 2 prop-ptr = 9 bytes.
            _defaultCount = 31;
            _attrBytes = 4;
            _pointerSize = 1;
            _entrySize = _attrBytes + 3 * _pointerSize + 2; // 4 + 3 + 2 = 9
        }
        else
        {
            // ZSpec S12.2 — V4+: 63 property defaults (126 bytes).
            // ZSpec S12.3 — V4+ entry: 6 attr + 2 parent + 2 sibling + 2 child + 2 prop-ptr = 14 bytes.
            _defaultCount = 63;
            _attrBytes = 6;
            _pointerSize = 2;
            _entrySize = _attrBytes + 3 * _pointerSize + 2; // 6 + 6 + 2 = 14
        }

        _defaultsSize = _defaultCount * 2;
        _entriesStart = _tableAddress + _defaultsSize;
    }

    /// <summary>
    /// Returns the byte address of the given object's entry in the table.
    /// Object numbers are 1-based; object 0 is "nothing" (null sentinel).
    /// </summary>
    private int ObjectAddress(int obj)
    {
        if (obj <= 0)
            throw new ArgumentOutOfRangeException(nameof(obj),
                "Object 0 is the null sentinel and has no table entry.");

        int maxObj = _version <= 3 ? 255 : 65535;
        if (obj > maxObj)
            throw new ArgumentOutOfRangeException(nameof(obj),
                $"Object {obj} exceeds maximum {maxObj} for V{_version}.");

        return _entriesStart + (obj - 1) * _entrySize;
    }

    /// <summary>
    /// Returns the default value for the given property number.
    /// Property numbers are 1-based; property 0 is invalid.
    /// </summary>
    /// <remarks>
    /// ZSpec S12.2 — The property defaults table contains word-sized
    /// values for properties 1 to 31 (V1-3) or 1 to 63 (V4+).
    /// </remarks>
    public ushort GetPropertyDefault(int prop)
    {
        if (prop < 1 || prop > _defaultCount)
            throw new ArgumentOutOfRangeException(nameof(prop),
                $"Property {prop} out of range 1-{_defaultCount}.");

        return _memory.ReadWord(_tableAddress + (prop - 1) * 2);
    }

    #region Tree Pointers

    /// <summary>Returns the parent object number (0 = no parent).</summary>
    public int GetParent(int obj)
    {
        int addr = ObjectAddress(obj) + _attrBytes;
        return _pointerSize == 1
            ? _memory.ReadByte(addr)
            : _memory.ReadWord(addr);
    }

    /// <summary>Returns the sibling object number (0 = no sibling).</summary>
    public int GetSibling(int obj)
    {
        int addr = ObjectAddress(obj) + _attrBytes + _pointerSize;
        return _pointerSize == 1
            ? _memory.ReadByte(addr)
            : _memory.ReadWord(addr);
    }

    /// <summary>Returns the child object number (0 = no child).</summary>
    public int GetChild(int obj)
    {
        int addr = ObjectAddress(obj) + _attrBytes + 2 * _pointerSize;
        return _pointerSize == 1
            ? _memory.ReadByte(addr)
            : _memory.ReadWord(addr);
    }

    /// <summary>Sets the parent of the given object.</summary>
    public void SetParent(int obj, int value)
    {
        int addr = ObjectAddress(obj) + _attrBytes;
        WritePointer(addr, value);
    }

    /// <summary>Sets the sibling of the given object.</summary>
    public void SetSibling(int obj, int value)
    {
        int addr = ObjectAddress(obj) + _attrBytes + _pointerSize;
        WritePointer(addr, value);
    }

    /// <summary>Sets the child of the given object.</summary>
    public void SetChild(int obj, int value)
    {
        int addr = ObjectAddress(obj) + _attrBytes + 2 * _pointerSize;
        WritePointer(addr, value);
    }

    private void WritePointer(int addr, int value)
    {
        if (_pointerSize == 1)
            _memory.WriteByte(addr, (byte)value);
        else
            _memory.WriteWord(addr, (ushort)value);
    }

    #endregion

    #region Tree Manipulation

    /// <summary>
    /// Removes an object from its parent's child chain. After removal,
    /// the object's parent and sibling are set to 0.
    /// </summary>
    /// <remarks>
    /// ZSpec S12 — Objects can be moved in the tree. Removal requires
    /// walking the parent's child chain to find and unlink the object.
    /// </remarks>
    public void RemoveObject(int obj)
    {
        int parent = GetParent(obj);
        if (parent == 0)
            return;

        int firstChild = GetChild(parent);

        if (firstChild == obj)
        {
            // Object is the first child — replace with its sibling.
            SetChild(parent, GetSibling(obj));
        }
        else
        {
            // Walk the sibling chain to find the predecessor.
            int prev = firstChild;
            while (prev != 0)
            {
                int nextSibling = GetSibling(prev);
                if (nextSibling == obj)
                {
                    // Unlink: predecessor's sibling becomes obj's sibling.
                    SetSibling(prev, GetSibling(obj));
                    break;
                }
                prev = nextSibling;
            }
        }

        SetParent(obj, 0);
        SetSibling(obj, 0);
    }

    /// <summary>
    /// Inserts an object as the first child of a destination object.
    /// The object is first removed from its current parent (if any).
    /// </summary>
    /// <remarks>
    /// ZSpec S12 — @insert_obj: remove from current parent, then make
    /// obj the first child of destination. The old first child of
    /// destination becomes obj's sibling.
    /// </remarks>
    public void InsertObject(int obj, int destination)
    {
        RemoveObject(obj);

        // The old first child of destination becomes obj's sibling.
        SetSibling(obj, GetChild(destination));

        // obj becomes the new first child of destination.
        SetChild(destination, obj);
        SetParent(obj, destination);
    }

    #endregion

    #region Attributes

    /// <summary>
    /// Tests whether the given attribute is set on the object.
    /// Attribute numbers are 0-based; V1-3 supports 0-31, V4+ supports 0-47.
    /// </summary>
    /// <remarks>
    /// ZSpec S12.3.1 — Attributes are stored as a bit array in the first
    /// 4 (V1-3) or 6 (V4+) bytes of the object entry. Attribute 0 is
    /// the top bit of the first byte.
    /// </remarks>
    public bool TestAttribute(int obj, int attribute)
    {
        ValidateAttribute(attribute);
        int addr = ObjectAddress(obj);
        int byteIndex = attribute / 8;
        int bitIndex = 7 - (attribute % 8);
        return (_memory.ReadByte(addr + byteIndex) & (1 << bitIndex)) != 0;
    }

    /// <summary>Sets the given attribute on the object.</summary>
    public void SetAttribute(int obj, int attribute)
    {
        ValidateAttribute(attribute);
        int addr = ObjectAddress(obj);
        int byteIndex = attribute / 8;
        int bitIndex = 7 - (attribute % 8);
        byte value = _memory.ReadByte(addr + byteIndex);
        _memory.WriteByte(addr + byteIndex, (byte)(value | (1 << bitIndex)));
    }

    /// <summary>Clears the given attribute on the object.</summary>
    public void ClearAttribute(int obj, int attribute)
    {
        ValidateAttribute(attribute);
        int addr = ObjectAddress(obj);
        int byteIndex = attribute / 8;
        int bitIndex = 7 - (attribute % 8);
        byte value = _memory.ReadByte(addr + byteIndex);
        _memory.WriteByte(addr + byteIndex, (byte)(value & ~(1 << bitIndex)));
    }

    private void ValidateAttribute(int attribute)
    {
        int maxAttr = _attrBytes * 8 - 1;
        if (attribute < 0 || attribute > maxAttr)
            throw new ArgumentOutOfRangeException(nameof(attribute),
                $"Attribute {attribute} out of range 0-{maxAttr}.");
    }

    #endregion

    #region Property Table

    /// <summary>
    /// Returns the byte address of the object's property table.
    /// The property table starts with a text-length byte followed by
    /// the short name Z-string, then the property entries.
    /// </summary>
    public int GetPropertyTableAddress(int obj)
    {
        int addr = ObjectAddress(obj) + _attrBytes + 3 * _pointerSize;
        return _memory.ReadWord(addr);
    }

    /// <summary>
    /// Returns the byte address where the property data begins (after
    /// the short name). This is the start of the first property block.
    /// </summary>
    private int GetPropertyDataStart(int obj)
    {
        int tableAddr = GetPropertyTableAddress(obj);
        int nameLen = _memory.ReadByte(tableAddr);
        return tableAddr + 1 + nameLen * 2;
    }

    /// <summary>
    /// Returns the address of the given property's data block, or 0 if
    /// the object does not have that property. Also outputs the data length.
    /// </summary>
    /// <remarks>
    /// ZSpec S12.4 — Properties are stored in descending numerical order
    /// and terminated by a zero size byte. The size byte format differs
    /// between V1-3 and V4+.
    /// </remarks>
    private int FindProperty(int obj, int prop, out int dataLen)
    {
        int addr = GetPropertyDataStart(obj);

        while (true)
        {
            int sizeByte = _memory.ReadByte(addr);
            if (sizeByte == 0)
            {
                dataLen = 0;
                return 0;
            }

            int propNum;
            int propDataAddr;

            if (_version <= 3)
            {
                // ZSpec S12.4.1 — V1-3: size byte = 32*(len-1) + prop_number.
                // Bottom 5 bits = property number, top 3 = data length - 1.
                propNum = sizeByte & 0x1F;
                dataLen = (sizeByte >> 5) + 1;
                propDataAddr = addr + 1;
            }
            else
            {
                // ZSpec S12.4.2 — V4+: bit 7 selects short or long form.
                propNum = sizeByte & 0x3F;

                if ((sizeByte & 0x80) != 0)
                {
                    // Long form: second byte holds length in bits 5-0.
                    int secondByte = _memory.ReadByte(addr + 1);
                    dataLen = secondByte & 0x3F;
                    // ZSpec S12.4.2 — length of 0 means 64 bytes.
                    if (dataLen == 0)
                        dataLen = 64;
                    propDataAddr = addr + 2;
                }
                else
                {
                    // Short form: bit 6 selects 1 or 2 byte data.
                    dataLen = (sizeByte & 0x40) != 0 ? 2 : 1;
                    propDataAddr = addr + 1;
                }
            }

            if (propNum == prop)
                return propDataAddr;

            // Properties are in descending order — if we've passed the
            // target, it doesn't exist.
            if (propNum < prop)
            {
                dataLen = 0;
                return 0;
            }

            addr = propDataAddr + dataLen;
        }
    }

    /// <summary>
    /// Returns the data address of the given property on the object,
    /// or 0 if the object does not have that property.
    /// </summary>
    public int GetPropertyAddress(int obj, int prop)
    {
        return FindProperty(obj, prop, out _);
    }

    /// <summary>
    /// Returns the value of the given property on the object. For 1-byte
    /// properties, returns the byte value; for 2-byte properties, the
    /// big-endian word. If the property is absent, returns the default
    /// from the property defaults table.
    /// </summary>
    /// <remarks>
    /// ZSpec S12.4 — @get_prop on a property with more than 2 bytes of
    /// data is undefined behavior. We read only the first 1 or 2 bytes.
    /// </remarks>
    public ushort GetProperty(int obj, int prop)
    {
        int dataAddr = FindProperty(obj, prop, out int dataLen);

        if (dataAddr == 0)
            return GetPropertyDefault(prop);

        return dataLen == 1
            ? _memory.ReadByte(dataAddr)
            : _memory.ReadWord(dataAddr);
    }

    /// <summary>
    /// Sets the value of the given property on the object. Writes 1 or 2
    /// bytes depending on the property's stored data length.
    /// </summary>
    /// <remarks>
    /// ZSpec S12.4 — @put_prop stores the value in the property's data
    /// block. The property must exist on the object.
    /// </remarks>
    public void SetProperty(int obj, int prop, ushort value)
    {
        int dataAddr = FindProperty(obj, prop, out int dataLen);

        if (dataAddr == 0)
            throw new InvalidOperationException(
                $"Object {obj} does not have property {prop}.");

        if (dataLen == 1)
            _memory.WriteByte(dataAddr, (byte)(value & 0xFF));
        else
            _memory.WriteWord(dataAddr, value);
    }

    /// <summary>
    /// Returns the property number of the next property after the given
    /// one, or the first property if prop is 0. Returns 0 if there are
    /// no more properties.
    /// </summary>
    /// <remarks>
    /// ZSpec S12.4 — @get_next_prop with prop=0 returns the first property.
    /// Properties are stored in descending order.
    /// </remarks>
    public int GetNextProperty(int obj, int prop)
    {
        int addr = GetPropertyDataStart(obj);

        if (prop == 0)
        {
            // Return the first property's number.
            int sizeByte = _memory.ReadByte(addr);
            if (sizeByte == 0)
                return 0;

            return _version <= 3
                ? sizeByte & 0x1F
                : sizeByte & 0x3F;
        }

        // Walk until we find prop, then return the next one.
        while (true)
        {
            int sizeByte = _memory.ReadByte(addr);
            if (sizeByte == 0)
                throw new InvalidOperationException(
                    $"Property {prop} not found on object {obj}.");

            int propNum;
            int dataLen;
            int propDataAddr;

            if (_version <= 3)
            {
                propNum = sizeByte & 0x1F;
                dataLen = (sizeByte >> 5) + 1;
                propDataAddr = addr + 1;
            }
            else
            {
                propNum = sizeByte & 0x3F;
                if ((sizeByte & 0x80) != 0)
                {
                    int secondByte = _memory.ReadByte(addr + 1);
                    dataLen = secondByte & 0x3F;
                    if (dataLen == 0)
                        dataLen = 64;
                    propDataAddr = addr + 2;
                }
                else
                {
                    dataLen = (sizeByte & 0x40) != 0 ? 2 : 1;
                    propDataAddr = addr + 1;
                }
            }

            if (propNum == prop)
            {
                // Found it — return the next property's number.
                int nextAddr = propDataAddr + dataLen;
                int nextSizeByte = _memory.ReadByte(nextAddr);
                if (nextSizeByte == 0)
                    return 0;

                return _version <= 3
                    ? nextSizeByte & 0x1F
                    : nextSizeByte & 0x3F;
            }

            addr = propDataAddr + dataLen;
        }
    }

    /// <summary>
    /// Returns the number of data bytes for the property at the given
    /// data address. The address must point to a property's data block
    /// (not the size byte). Passing address 0 returns 0.
    /// </summary>
    /// <remarks>
    /// ZSpec S12.4 — @get_prop_len: the size byte(s) precede the data.
    /// ZSpec11 "@get_prop_len" — get_prop_len 0 must return 0.
    /// </remarks>
    public int GetPropertyLength(int address)
    {
        // ZSpec11: "@get_prop_len 0 must return 0"
        if (address == 0)
            return 0;

        if (_version <= 3)
        {
            // The size byte is the byte before the data address.
            int sizeByte = _memory.ReadByte(address - 1);
            return (sizeByte >> 5) + 1;
        }
        else
        {
            // V4+: the byte before the data might be the first or second
            // size byte. If bit 7 is set, it's the second size byte.
            int prevByte = _memory.ReadByte(address - 1);
            if ((prevByte & 0x80) != 0)
            {
                // Second size byte: bits 5-0 = length.
                int len = prevByte & 0x3F;
                return len == 0 ? 64 : len;
            }
            else
            {
                // First (only) size byte: bit 6 selects 1 or 2.
                return (prevByte & 0x40) != 0 ? 2 : 1;
            }
        }
    }

    /// <summary>
    /// Returns the byte length of the object's short name Z-string,
    /// including the length-in-words prefix byte.
    /// </summary>
    public int GetShortNameLengthBytes(int obj)
    {
        int tableAddr = GetPropertyTableAddress(obj);
        int nameLen = _memory.ReadByte(tableAddr);
        return 1 + nameLen * 2;
    }

    /// <summary>
    /// Returns the address of the short name Z-string for the given object.
    /// The Z-string starts at the property table address + 1 (after the
    /// length-in-words byte).
    /// </summary>
    public int GetShortNameAddress(int obj)
    {
        return GetPropertyTableAddress(obj) + 1;
    }

    #endregion
}
