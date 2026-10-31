using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>一次完整性 / 签名校验的结果。</summary>
public sealed record PluginIntegrityResult(PluginIntegrityState State, string? Error)
{
    public bool CanLoad => State is PluginIntegrityState.Undeclared or PluginIntegrityState.Verified;

    public static readonly PluginIntegrityResult Undeclared = new(PluginIntegrityState.Undeclared, null);
}

/// <summary>
///     插件包的完整性（<c>files</c> 哈希）与签名（ECDSA P-256）校验。
///     <para>
///         规则：
///         <list type="bullet">
///             <item>清单里**没写** <c>files</c> / <c>signature</c>：状态 <c>Undeclared</c>，照常加载（向后兼容）；</item>
///             <item>写了 <c>files</c>：逐文件比对 SHA-256，对不上 → <c>Mismatch</c>，**拒绝加载**；</item>
///             <item>写了 <c>signature</c>：公钥必须出现在宿主的信任列表里，且 ECDSA 验签通过，否则拒绝加载。</item>
///         </list>
///     </para>
///     <para>
///         签名载荷（UTF-8，<c>\n</c> 分隔，大小写与顺序都固定）：
///         <c>&lt;id&gt;\n&lt;version&gt;\n&lt;文件名&gt;:&lt;sha256&gt;</c> …（文件名按序数升序）。
///         生成工具见仓库 <c>tools/PluginSigner</c>。
///     </para>
/// </summary>
public static class PluginIntegrity
{
    /// <summary>宿主的信任公钥列表（SubjectPublicKeyInfo 的 Base64，由启动器启动时注入）。</summary>
    public static IReadOnlyCollection<string> TrustedPublicKeys { get; set; } = [];

    /// <summary>
    ///     校验一个插件包（<c>.pclpkg</c>，zip）或一个已解压目录。
    ///     <paramref name="manifest" /> 为 <see langword="null" /> 时返回 <c>Undeclared</c>。
    /// </summary>
    public static PluginIntegrityResult Verify(string path, PluginManifest? manifest)
    {
        if (manifest is null)
            return PluginIntegrityResult.Undeclared;

        var hasFiles = manifest.Files.Count > 0;
        var hasSignature = manifest.Signature is not null;
        if (!hasFiles && !hasSignature)
            return PluginIntegrityResult.Undeclared;

        try
        {
            // ① 文件哈希
            if (hasFiles)
            {
                foreach (var (fileName, expected) in manifest.Files)
                {
                    if (string.IsNullOrWhiteSpace(fileName)) continue;
                    if (string.IsNullOrWhiteSpace(expected))
                        return new PluginIntegrityResult(PluginIntegrityState.Mismatch,
                            $"清单里 {fileName} 的哈希为空");

                    var actual = ReadFileHash(path, fileName);
                    if (actual is null)
                        return new PluginIntegrityResult(PluginIntegrityState.Mismatch,
                            $"包里缺少清单声明的文件：{fileName}");

                    if (!string.Equals(actual, expected.Trim(), StringComparison.OrdinalIgnoreCase))
                        return new PluginIntegrityResult(PluginIntegrityState.Mismatch,
                            $"{fileName} 校验失败（清单 {Short(expected)}，实际 {Short(actual)}）");
                }
            }

            // ② 签名
            if (hasSignature)
            {
                var signature = manifest.Signature!;
                if (!string.Equals(signature.Algorithm, PluginSignature.AlgorithmEcdsaP256,
                        StringComparison.OrdinalIgnoreCase))
                    return new PluginIntegrityResult(PluginIntegrityState.BadSignature,
                        $"不支持的签名算法：{signature.Algorithm}");

                var publicKeyBase64 = FindTrustedKey(signature.KeyId);
                if (publicKeyBase64 is null)
                    return new PluginIntegrityResult(PluginIntegrityState.UntrustedKey,
                        $"签名公钥 {Short(signature.KeyId)} 不在信任列表里");

                var payload = Encoding.UTF8.GetBytes(ComputePayload(manifest));
                byte[] signatureBytes;
                try
                {
                    signatureBytes = Convert.FromBase64String(signature.Value);
                }
                catch
                {
                    return new PluginIntegrityResult(PluginIntegrityState.BadSignature, "签名值不是合法的 Base64");
                }

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);

                // .NET 默认签 P1363（r||s），openssl 等工具默认是 DER（RFC 3279），两种都接受
                var verified = ecdsa.VerifyData(payload, signatureBytes, HashAlgorithmName.SHA256) ||
                               ecdsa.VerifyData(payload, signatureBytes, HashAlgorithmName.SHA256,
                                   DSASignatureFormat.Rfc3279DerSequence);
                if (!verified)
                    return new PluginIntegrityResult(PluginIntegrityState.BadSignature, "验签失败（内容与签名不匹配）");
            }

            return new PluginIntegrityResult(PluginIntegrityState.Verified, null);
        }
        catch (Exception ex)
        {
            return new PluginIntegrityResult(PluginIntegrityState.BadSignature, "校验过程出错：" + ex.Message);
        }
    }

    /// <summary>算签名载荷（工具侧与宿主侧必须一致）。</summary>
    public static string ComputePayload(PluginManifest manifest)
    {
        var builder = new StringBuilder();
        builder.Append(manifest.Id).Append('\n').Append(manifest.Version);
        foreach (var (fileName, hash) in manifest.Files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            builder.Append('\n').Append(fileName).Append(':').Append(hash.Trim().ToLowerInvariant());
        return builder.ToString();
    }

    /// <summary>
    ///     入口程序集看起来是不是一个 PE 文件（前两个字节 <c>MZ</c>）。
    ///     <para>
    ///         用户手工改包 / 复制损坏时，<c>.dll</c> 可能根本不是编译产物，
    ///         <c>LoadFromAssemblyPath</c> 会抛 <c>BadImageFormatException: Bad IL format</c>。
    ///         发现阶段先粗查一下，能给出「包可能损坏或不是编译产物」这种能看懂的原因。
    ///     </para>
    /// </summary>
    public static bool LooksLikeAssembly(string packageOrDirectory, string fileName)
    {
        var normalized = fileName.Replace('\\', '/');

        try
        {
            if (Directory.Exists(packageOrDirectory))
            {
                var full = Path.Combine(packageOrDirectory, normalized.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) return false;
                using var fileStream = File.OpenRead(full);
                return HasMzHeader(fileStream);
            }

            // 包格式：注意 zip 与条目流都要释放，否则会占着包文件
            using var archive = PluginPackage.OpenArchiveShared(packageOrDirectory);
            var entry = archive.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return false;

            using var entryStream = entry.Open();
            return HasMzHeader(entryStream);
        }
        catch
        {
            return true; // 读不出来就交给加载器判断，别误杀
        }
    }

    private static bool HasMzHeader(Stream stream)
    {
        var header = new byte[2];
        var read = stream.Read(header, 0, 2);
        return read == 2 && header[0] == 0x4D && header[1] == 0x5A; // "MZ"
    }

    /// <summary>算一个文件（包内条目或磁盘文件）的 SHA-256；找不到返回 null。</summary>
    public static string? ReadFileHash(string packageOrDirectory, string fileName)
    {
        var normalized = fileName.Replace('\\', '/');

        if (Directory.Exists(packageOrDirectory))
        {
            var full = Path.Combine(packageOrDirectory, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full)) return null;
            using var stream = File.OpenRead(full);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        using var archive = PluginPackage.OpenArchiveShared(packageOrDirectory);
        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName.Replace('\\', '/'), normalized, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;

        using var entryStream = entry.Open();
        return Convert.ToHexString(SHA256.HashData(entryStream)).ToLowerInvariant();
    }

    /// <summary>按 keyId（公钥 SHA-256 指纹）在信任列表里找公钥。</summary>
    private static string? FindTrustedKey(string? keyId)
    {
        foreach (var key in TrustedPublicKeys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (string.IsNullOrWhiteSpace(keyId)) return key;

            var fingerprint = ComputeKeyId(key);
            if (string.Equals(fingerprint, keyId.Trim(), StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return null;
    }

    /// <summary>公钥指纹：SubjectPublicKeyInfo 字节的 SHA-256（小写十六进制）。</summary>
    public static string ComputeKeyId(string publicKeyBase64)
    {
        var bytes = Convert.FromBase64String(publicKeyBase64);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string Short(string? text) =>
        string.IsNullOrEmpty(text) ? "?" : text.Length <= 12 ? text : text[..12] + "…";
}
