// 插件包签名 / 校验工具（与宿主的 PCL.PluginHost.PluginIntegrity 对应）。
//
// 用法：
//   dotnet run --project tools/PluginSigner -- genkey <private.pem> [public.txt]
//   dotnet run --project tools/PluginSigner -- sign   <包.pclpkg> <private.pem>
//   dotnet run --project tools/PluginSigner -- hash   <包.pclpkg>
//   dotnet run --project tools/PluginSigner -- verify <包.pclpkg> [trusted-keys.json]
//
// 签名载荷（必须与宿主一致，UTF-8，\n 分隔）：
//   <id>\n<version>\n<文件名>:<sha256>…（文件名按 Ordinal 升序，哈希小写）

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length == 0)
{
    Console.WriteLine("用法：genkey | sign | hash | verify（详见源码顶部注释）");
    return 2;
}

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "genkey": return GenerateKey(args);
        case "sign": return Sign(args);
        case "hash": return Hash(args);
        case "verify": return Verify(args);
        default:
            Console.WriteLine($"未知命令：{args[0]}");
            return 2;
    }
}
catch (Exception ex)
{
    Console.WriteLine("!! " + ex.Message);
    return 1;
}

static int GenerateKey(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("用法：genkey <private.pem> [public.txt]");
        return 2;
    }

    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllText(args[1], ecdsa.ExportPkcs8PrivateKeyPem());

    var spki = ecdsa.ExportSubjectPublicKeyInfo();
    var publicBase64 = Convert.ToBase64String(spki);
    var keyId = Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();

    var publicPath = args.Length > 2 ? args[2] : Path.ChangeExtension(args[1], ".pub.txt");
    File.WriteAllText(publicPath,
        "// 把这个 key 贴进 PCL\\PluginData\\trusted-keys.json 的 keys 数组\n" +
        publicBase64 + "\n");

    Console.WriteLine($"私钥：{args[1]}");
    Console.WriteLine($"公钥：{publicPath}");
    Console.WriteLine($"公钥 Base64：{publicBase64}");
    Console.WriteLine($"keyId（指纹）：{keyId}");
    Console.WriteLine("信任列表示例：{ \"keys\": [ \"" + publicBase64 + "\" ] }");
    return 0;
}

static int Sign(string[] args)
{
    if (args.Length < 3)
    {
        Console.WriteLine("用法：sign <包.pclpkg> <private.pem>");
        return 2;
    }

    var packagePath = args[1];
    using var ecdsa = ECDsa.Create();
    ecdsa.ImportFromPem(File.ReadAllText(args[2]));

    var spki = ecdsa.ExportSubjectPublicKeyInfo();
    var keyId = Convert.ToHexString(SHA256.HashData(spki)).ToLowerInvariant();

    var entries = ReadEntries(packagePath);
    var manifestName = entries.Keys.FirstOrDefault(name =>
                            string.Equals(name, "plugin.json", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException("包里没有 plugin.json");

    var manifest = JsonNode.Parse(entries[manifestName])?.AsObject()
                   ?? throw new InvalidOperationException("plugin.json 不是合法的 JSON 对象");

    // ① 文件哈希（除 plugin.json 之外的全部文件）
    var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var (name, bytes) in entries)
    {
        if (string.Equals(name, manifestName, StringComparison.OrdinalIgnoreCase)) continue;
        files[name] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    var filesNode = new JsonObject();
    foreach (var (name, hash) in files) filesNode[name] = hash;
    manifest["files"] = filesNode;

    // ② 签名
    var payload = ComputePayload(manifest, files);
    var signature = Convert.ToBase64String(ecdsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256));
    manifest["signature"] = new JsonObject
    {
        ["alg"] = "ecdsa-p256-sha256",
        ["keyId"] = keyId,
        ["value"] = signature
    };

    entries[manifestName] = Encoding.UTF8.GetBytes(manifest.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }));

    WriteEntries(packagePath, entries, manifestName);

    Console.WriteLine($"已签名：{Path.GetFileName(packagePath)}（{files.Count} 个文件，keyId {keyId[..12]}…）");
    return 0;
}

static int Hash(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("用法：hash <包.pclpkg>");
        return 2;
    }

    var entries = ReadEntries(args[1]);
    var manifestName = entries.Keys.FirstOrDefault(name =>
                            string.Equals(name, "plugin.json", StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException("包里没有 plugin.json");

    var manifest = JsonNode.Parse(entries[manifestName])!.AsObject();
    var filesNode = new JsonObject();
    foreach (var (name, bytes) in entries.Where(pair =>
                 !string.Equals(pair.Key, manifestName, StringComparison.OrdinalIgnoreCase))
             .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        filesNode[name] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    manifest["files"] = filesNode;
    entries[manifestName] = Encoding.UTF8.GetBytes(manifest.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    }));
    WriteEntries(args[1], entries, manifestName);
    Console.WriteLine($"已写入文件哈希：{Path.GetFileName(args[1])}");
    return 0;
}

static int Verify(string[] args)
{
    if (args.Length < 2)
    {
        Console.WriteLine("用法：verify <包.pclpkg> [trusted-keys.json]");
        return 2;
    }

    var entries = ReadEntries(args[1]);
    var manifestName = entries.Keys.FirstOrDefault(name =>
                            string.Equals(name, "plugin.json", StringComparison.OrdinalIgnoreCase));
    if (manifestName is null)
    {
        Console.WriteLine("!! 包里没有 plugin.json");
        return 1;
    }

    var manifest = JsonNode.Parse(entries[manifestName])!.AsObject();

    // ① 文件哈希
    if (manifest["files"] is JsonObject files && files.Count > 0)
    {
        foreach (var (name, expectedNode) in files)
        {
            var expected = expectedNode?.GetValue<string>() ?? "";
            if (!entries.TryGetValue(name, out var bytes))
            {
                Console.WriteLine($"!! 缺少文件：{name}");
                return 1;
            }

            var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"!! {name} 哈希不匹配（期望 {expected}，实际 {actual}）");
                return 1;
            }
        }

        Console.WriteLine($"文件哈希：通过（{files.Count} 个文件）");
        manifest["files"] = files.DeepClone();
    }
    else
    {
        Console.WriteLine("文件哈希：未声明");
    }

    // ② 签名
    if (manifest["signature"] is JsonObject signature)
    {
        var keyId = signature["keyId"]?.GetValue<string>() ?? "";
        var value = signature["value"]?.GetValue<string>() ?? "";
        var trusted = args.Length > 2 ? ReadTrustedKeys(args[2]) : [];

        var publicKey = trusted.FirstOrDefault(key =>
            string.Equals(Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(key))),
                keyId, StringComparison.OrdinalIgnoreCase));
        if (publicKey is null)
        {
            Console.WriteLine($"签名：公钥 {keyId[..Math.Min(12, keyId.Length)]}… 不在信任列表里");
            return 1;
        }

        var filesMap = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (manifest["files"] is JsonObject fileNode)
            foreach (var (name, hash) in fileNode)
                filesMap[name] = hash!.GetValue<string>();

        var payload = ComputePayload(manifest, filesMap);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
        var signatureBytes = Convert.FromBase64String(value);
        var ok = ecdsa.VerifyData(Encoding.UTF8.GetBytes(payload), signatureBytes, HashAlgorithmName.SHA256);
        Console.WriteLine(ok ? "签名：通过" : "签名：失败");
        return ok ? 0 : 1;
    }

    Console.WriteLine("签名：未声明");
    return 0;
}

static string ComputePayload(JsonObject manifest, IReadOnlyDictionary<string, string> files)
{
    var builder = new StringBuilder();
    builder.Append(manifest["id"]?.GetValue<string>() ?? "")
        .Append('\n')
        .Append(manifest["version"]?.GetValue<string>() ?? "");
    foreach (var (name, hash) in files)
        builder.Append('\n').Append(name).Append(':').Append(hash.Trim().ToLowerInvariant());
    return builder.ToString();
}

static List<string> ReadTrustedKeys(string path)
{
    if (!File.Exists(path)) return [];
    var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
    var keys = new List<string>();
    if (root?["keys"] is JsonArray array)
        foreach (var key in array)
        {
            var text = key?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(text)) keys.Add(text.Trim());
        }

    return keys;
}

static Dictionary<string, byte[]> ReadEntries(string packagePath)
{
    using var archive = ZipFile.OpenRead(packagePath);
    var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in archive.Entries)
    {
        if (string.IsNullOrEmpty(entry.Name)) continue;
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        result[entry.FullName] = memory.ToArray();
    }

    return result;
}

static void WriteEntries(string packagePath, Dictionary<string, byte[]> entries, string manifestName)
{
    var tempPath = packagePath + ".tmp";
    using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
    {
        foreach (var name in new[] { manifestName }.Concat(entries.Keys.Where(n =>
                     !string.Equals(n, manifestName, StringComparison.OrdinalIgnoreCase))))
        {
            var entry = archive.CreateEntry(name, CompressionLevel.SmallestSize);
            using var stream = entry.Open();
            stream.Write(entries[name]);
        }
    }

    File.Move(tempPath, packagePath, true);
}
