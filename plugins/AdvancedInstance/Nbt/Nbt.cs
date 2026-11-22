using System.IO;
using System.IO.Compression;
using System.Text;

namespace AdvancedInstance.Nbt;

/// <summary>NBT 标签类型（与原版一致，数值不能改）。</summary>
public enum NbtTagType : byte
{
    End = 0,
    Byte = 1,
    Short = 2,
    Int = 3,
    Long = 4,
    Float = 5,
    Double = 6,
    ByteArray = 7,
    String = 8,
    List = 9,
    Compound = 10,
    IntArray = 11,
    LongArray = 12
}

/// <summary>
///     一个 NBT 标签。这里的实现只求「能把文件完整读进来、原样写回去」，
///     所以每种类型都保留原始数据，不做任何语义转换。
/// </summary>
public abstract class NbtTag
{
    protected NbtTag(string name) => Name = name;

    public string Name { get; set; }

    public abstract NbtTagType Type { get; }
}

public sealed class NbtByte : NbtTag
{
    public NbtByte(string name, byte value) : base(name) => Value = value;
    public byte Value { get; set; }
    public override NbtTagType Type => NbtTagType.Byte;
}

public sealed class NbtShort : NbtTag
{
    public NbtShort(string name, short value) : base(name) => Value = value;
    public short Value { get; set; }
    public override NbtTagType Type => NbtTagType.Short;
}

public sealed class NbtInt : NbtTag
{
    public NbtInt(string name, int value) : base(name) => Value = value;
    public int Value { get; set; }
    public override NbtTagType Type => NbtTagType.Int;
}

public sealed class NbtLong : NbtTag
{
    public NbtLong(string name, long value) : base(name) => Value = value;
    public long Value { get; set; }
    public override NbtTagType Type => NbtTagType.Long;
}

public sealed class NbtFloat : NbtTag
{
    public NbtFloat(string name, float value) : base(name) => Value = value;
    public float Value { get; set; }
    public override NbtTagType Type => NbtTagType.Float;
}

public sealed class NbtDouble : NbtTag
{
    public NbtDouble(string name, double value) : base(name) => Value = value;
    public double Value { get; set; }
    public override NbtTagType Type => NbtTagType.Double;
}

public sealed class NbtString : NbtTag
{
    public NbtString(string name, string value) : base(name) => Value = value;
    public string Value { get; set; }
    public override NbtTagType Type => NbtTagType.String;
}

public sealed class NbtByteArray : NbtTag
{
    public NbtByteArray(string name, byte[] value) : base(name) => Value = value;
    public byte[] Value { get; set; }
    public override NbtTagType Type => NbtTagType.ByteArray;
}

public sealed class NbtIntArray : NbtTag
{
    public NbtIntArray(string name, int[] value) : base(name) => Value = value;
    public int[] Value { get; set; }
    public override NbtTagType Type => NbtTagType.IntArray;
}

public sealed class NbtLongArray : NbtTag
{
    public NbtLongArray(string name, long[] value) : base(name) => Value = value;
    public long[] Value { get; set; }
    public override NbtTagType Type => NbtTagType.LongArray;
}

/// <summary>列表：元素类型取第一个元素（空列表也要记住 ElementType，否则写回去会变类型）。</summary>
public sealed class NbtList : NbtTag
{
    public NbtList(string name, NbtTagType elementType) : base(name) => ElementType = elementType;

    public NbtTagType ElementType { get; }

    public List<NbtTag> Items { get; } = [];

    public override NbtTagType Type => NbtTagType.List;
}

/// <summary>复合标签：保持插入顺序（写回去时尽量与原文件一致）。</summary>
public sealed class NbtCompound : NbtTag
{
    public NbtCompound(string name) : base(name)
    {
    }

    private readonly List<NbtTag> _children = [];

    public IReadOnlyList<NbtTag> Children => _children;

    public override NbtTagType Type => NbtTagType.Compound;

    public int Count => _children.Count;

    public NbtTag? this[string name] => _children.FirstOrDefault(child => child.Name == name);

    public void Add(NbtTag tag) => _children.Add(tag);

    public void AddOrReplace(NbtTag tag)
    {
        var index = _children.FindIndex(child => child.Name == tag.Name);
        if (index >= 0) _children[index] = tag;
        else _children.Add(tag);
    }

    public bool Remove(string name)
    {
        var index = _children.FindIndex(child => child.Name == name);
        if (index < 0) return false;
        _children.RemoveAt(index);
        return true;
    }

    #region 强类型取值

    public NbtCompound? GetCompound(string name) => this[name] as NbtCompound;

    public NbtList? GetList(string name) => this[name] as NbtList;

    public string? GetString(string name) => (this[name] as NbtString)?.Value;

    public int? GetInt(string name) => this[name] switch
    {
        NbtInt value => value.Value,
        NbtShort value => value.Value,
        NbtByte value => value.Value,
        _ => null
    };

    public long? GetLong(string name) => this[name] switch
    {
        NbtLong value => value.Value,
        NbtInt value => value.Value,
        _ => null
    };

    public byte? GetByte(string name) => (this[name] as NbtByte)?.Value;

    public void SetString(string name, string value)
    {
        if (this[name] is NbtString existing) existing.Value = value;
        else AddOrReplace(new NbtString(name, value));
    }

    public void SetInt(string name, int value)
    {
        switch (this[name])
        {
            case NbtInt existing:
                existing.Value = value;
                return;
            case NbtShort:
                AddOrReplace(new NbtInt(name, value));
                return;
            case NbtByte:
                AddOrReplace(new NbtInt(name, value));
                return;
            default:
                AddOrReplace(new NbtInt(name, value));
                return;
        }
    }

    public void SetLong(string name, long value)
    {
        if (this[name] is NbtLong existing) existing.Value = value;
        else AddOrReplace(new NbtLong(name, value));
    }

    public void SetByte(string name, byte value)
    {
        if (this[name] is NbtByte existing) existing.Value = value;
        else AddOrReplace(new NbtByte(name, value));
    }

    #endregion
}

/// <summary>NBT 文件的压缩方式。</summary>
public enum NbtCompression
{
    Unknown = 0,
    None = 1,
    GZip = 2
}

/// <summary>
///     NBT 文件读写。自动识别 gzip / 未压缩（原版 <c>level.dat</c> 与 Litematica 投影都用 gzip）。
///     <para>
///         写回时默认沿用读入时的压缩方式，避免「改一个字段把整份文件换个壳」。
///     </para>
/// </summary>
public static class NbtFile
{
    /// <summary>读一个 NBT 文件（返回根复合标签）。</summary>
    public static NbtCompound Read(string path, out NbtCompression compression)
    {
        var raw = File.ReadAllBytes(path);
        compression = DetectCompression(raw);

        using var memory = new MemoryStream(raw, false);
        Stream stream = compression == NbtCompression.GZip
            ? new GZipStream(memory, CompressionMode.Decompress)
            : memory;

        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, true);
            var type = (NbtTagType)reader.ReadByte();
            if (type != NbtTagType.Compound)
                throw new InvalidDataException($"根标签不是复合标签（读到 {(byte)type}）");

            var name = ReadString(reader);
            var root = new NbtCompound(name);
            ReadChildren(reader, root);
            return root;
        }
        finally
        {
            if (compression == NbtCompression.GZip) stream.Dispose();
        }
    }

    /// <summary>写一个 NBT 文件。</summary>
    public static void Write(string path, NbtCompound root, NbtCompression compression)
    {
        // 先写进内存，全部成功再落盘：避免写一半失败把原文件毁了
        using var memory = new MemoryStream();
        Stream target = compression == NbtCompression.GZip
            ? new GZipStream(memory, CompressionLevel.Optimal, true)
            : memory;
        try
        {
            using var writer = new BinaryWriter(target, Encoding.UTF8, true);
            writer.Write((byte)NbtTagType.Compound);
            WriteString(writer, root.Name);
            WriteChildren(writer, root);
        }
        finally
        {
            if (compression == NbtCompression.GZip) target.Dispose();
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, memory.ToArray());
    }

    /// <summary>探测压缩方式（gzip 魔数 1F 8B）。</summary>
    public static NbtCompression DetectCompression(ReadOnlySpan<byte> raw)
    {
        if (raw.Length < 2) return NbtCompression.Unknown;
        return raw[0] == 0x1F && raw[1] == 0x8B ? NbtCompression.GZip : NbtCompression.None;
    }

    #region 读取实现

    private static string ReadString(BinaryReader reader)
    {
        var length = reader.ReadUInt16();
        if (length == 0) return "";
        var bytes = reader.ReadBytes(length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void ReadChildren(BinaryReader reader, NbtCompound parent)
    {
        while (true)
        {
            var type = (NbtTagType)reader.ReadByte();
            if (type == NbtTagType.End) return;

            var name = ReadString(reader);
            parent.Add(ReadTag(reader, type, name));
        }
    }

    private static NbtTag ReadTag(BinaryReader reader, NbtTagType type, string name) => type switch
    {
        NbtTagType.Byte => new NbtByte(name, reader.ReadByte()),
        NbtTagType.Short => new NbtShort(name, reader.ReadInt16()),
        NbtTagType.Int => new NbtInt(name, reader.ReadInt32()),
        NbtTagType.Long => new NbtLong(name, reader.ReadInt64()),
        NbtTagType.Float => new NbtFloat(name, reader.ReadSingle()),
        NbtTagType.Double => new NbtDouble(name, reader.ReadDouble()),
        NbtTagType.String => new NbtString(name, ReadString(reader)),
        NbtTagType.ByteArray => ReadByteArray(reader, name),
        NbtTagType.IntArray => ReadIntArray(reader, name),
        NbtTagType.LongArray => ReadLongArray(reader, name),
        NbtTagType.List => ReadList(reader, name),
        NbtTagType.Compound => ReadCompound(reader, name),
        _ => throw new InvalidDataException($"未知的 NBT 标签类型：{(byte)type}")
    };

    private static NbtByteArray ReadByteArray(BinaryReader reader, string name)
    {
        var length = reader.ReadInt32();
        return new NbtByteArray(name, reader.ReadBytes(Math.Max(0, length)));
    }

    private static NbtIntArray ReadIntArray(BinaryReader reader, string name)
    {
        var length = reader.ReadInt32();
        var values = new int[Math.Max(0, length)];
        for (var i = 0; i < values.Length; i++) values[i] = reader.ReadInt32();
        return new NbtIntArray(name, values);
    }

    private static NbtLongArray ReadLongArray(BinaryReader reader, string name)
    {
        var length = reader.ReadInt32();
        var values = new long[Math.Max(0, length)];
        for (var i = 0; i < values.Length; i++) values[i] = reader.ReadInt64();
        return new NbtLongArray(name, values);
    }

    private static NbtList ReadList(BinaryReader reader, string name)
    {
        var elementType = (NbtTagType)reader.ReadByte();
        var length = reader.ReadInt32();
        var list = new NbtList(name, elementType);
        for (var i = 0; i < length; i++) list.Items.Add(ReadTag(reader, elementType, ""));
        return list;
    }

    private static NbtCompound ReadCompound(BinaryReader reader, string name)
    {
        var compound = new NbtCompound(name);
        ReadChildren(reader, compound);
        return compound;
    }

    #endregion

    #region 写入实现

    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? "");
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private static void WriteChildren(BinaryWriter writer, NbtCompound compound)
    {
        foreach (var child in compound.Children) WriteTag(writer, child);
        writer.Write((byte)NbtTagType.End);
    }

    private static void WriteTag(BinaryWriter writer, NbtTag tag)
    {
        writer.Write((byte)tag.Type);
        WriteString(writer, tag.Name);

        switch (tag)
        {
            case NbtByte value:
                writer.Write(value.Value);
                break;
            case NbtShort value:
                writer.Write(value.Value);
                break;
            case NbtInt value:
                writer.Write(value.Value);
                break;
            case NbtLong value:
                writer.Write(value.Value);
                break;
            case NbtFloat value:
                writer.Write(value.Value);
                break;
            case NbtDouble value:
                writer.Write(value.Value);
                break;
            case NbtString value:
                WriteString(writer, value.Value);
                break;
            case NbtByteArray value:
                writer.Write(value.Value.Length);
                writer.Write(value.Value);
                break;
            case NbtIntArray value:
                writer.Write(value.Value.Length);
                foreach (var item in value.Value) writer.Write(item);
                break;
            case NbtLongArray value:
                writer.Write(value.Value.Length);
                foreach (var item in value.Value) writer.Write(item);
                break;
            case NbtList value:
                // 空列表没法从元素推类型，用读入时记下的类型
                writer.Write((byte)(value.Items.Count > 0 ? value.Items[0].Type : value.ElementType));
                writer.Write(value.Items.Count);
                foreach (var item in value.Items) WriteTagBody(writer, item);
                break;
            case NbtCompound value:
                WriteChildren(writer, value);
                break;
        }
    }

    /// <summary>列表元素：不写类型与名字，只写内容。</summary>
    private static void WriteTagBody(BinaryWriter writer, NbtTag tag)
    {
        switch (tag)
        {
            case NbtByte value:
                writer.Write(value.Value);
                break;
            case NbtShort value:
                writer.Write(value.Value);
                break;
            case NbtInt value:
                writer.Write(value.Value);
                break;
            case NbtLong value:
                writer.Write(value.Value);
                break;
            case NbtFloat value:
                writer.Write(value.Value);
                break;
            case NbtDouble value:
                writer.Write(value.Value);
                break;
            case NbtString value:
                WriteString(writer, value.Value);
                break;
            case NbtByteArray value:
                writer.Write(value.Value.Length);
                writer.Write(value.Value);
                break;
            case NbtIntArray value:
                writer.Write(value.Value.Length);
                foreach (var item in value.Value) writer.Write(item);
                break;
            case NbtLongArray value:
                writer.Write(value.Value.Length);
                foreach (var item in value.Value) writer.Write(item);
                break;
            case NbtList value:
                writer.Write((byte)(value.Items.Count > 0 ? value.Items[0].Type : value.ElementType));
                writer.Write(value.Items.Count);
                foreach (var item in value.Items) WriteTagBody(writer, item);
                break;
            case NbtCompound value:
                WriteChildren(writer, value);
                break;
        }
    }

    #endregion
}
