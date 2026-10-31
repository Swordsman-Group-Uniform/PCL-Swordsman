namespace PCL.PluginSdk;

/// <summary>插件包的完整性与签名状态（管理页展示、加载决策都用它）。</summary>
public enum PluginIntegrityState
{
    /// <summary>清单里没声明 <c>files</c> / <c>signature</c>（老插件就是这种，允许加载）。</summary>
    Undeclared = 0,

    /// <summary>声明了哈希或签名，且校验通过。</summary>
    Verified = 1,

    /// <summary>声明了哈希但文件对不上（包被改过 / 打包出错）——**必须拒绝加载**。</summary>
    Mismatch = 2,

    /// <summary>声明了签名，但所用公钥不在宿主的信任列表里——**拒绝加载**。</summary>
    UntrustedKey = 3,

    /// <summary>声明了签名但验签失败（内容被改 / 签名不匹配）——**拒绝加载**。</summary>
    BadSignature = 4
}

/// <summary>插件签名块（<c>plugin.json</c> 的可选字段 <c>signature</c>）。</summary>
public sealed class PluginSignature
{
    /// <summary>算法标识，目前只支持 <c>ecdsa-p256-sha256</c>。</summary>
    public const string AlgorithmEcdsaP256 = "ecdsa-p256-sha256";

    /// <summary>算法，留空时按 <see cref="AlgorithmEcdsaP256" /> 处理。</summary>
    [System.Text.Json.Serialization.JsonPropertyName("alg")]
    public string Algorithm { get; set; } = AlgorithmEcdsaP256;

    /// <summary>公钥的 SHA-256 指纹（小写十六进制），宿主用它去信任列表里找公钥。</summary>
    [System.Text.Json.Serialization.JsonPropertyName("keyId")]
    public string KeyId { get; set; } = "";

    /// <summary>签名值（Base64）。</summary>
    [System.Text.Json.Serialization.JsonPropertyName("value")]
    public string Value { get; set; } = "";
}
