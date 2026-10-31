using System.Text.Json.Serialization;

namespace PCL.Core.Link.EasyTier;

// ReSharper disable InconsistentNaming
/// <summary>
///     EasyTier <c>peer -o json</c> 的输出节点模型。<br/>
///     注意：所有字段都不用 <c>required</c>。不同 EasyTier 版本的输出字段有差异，
///     一旦把字段标记为 required，任何缺字段都会让 <c>System.Text.Json</c> 抛出 JsonException，
///     进而导致整张玩家列表解析失败（表现为「Failed to get EasyTier player list info」）。
/// </summary>
public record ETPeerInfo
{
    [JsonPropertyName("hostname")] public string? Hostname { get; init; }
    [JsonPropertyName("ipv4")] public string? Ipv4 { get; init; }
    [JsonPropertyName("cost")] public string? Cost { get; init; }
    [JsonPropertyName("lat_ms")] public string? Ping { get; init; }
    [JsonPropertyName("loss_rate")] public string? Loss { get; init; }
    [JsonPropertyName("nat_type")] public string? NatType { get; init; }
    [JsonPropertyName("version")] public string? ETVersion { get; init; }
}
