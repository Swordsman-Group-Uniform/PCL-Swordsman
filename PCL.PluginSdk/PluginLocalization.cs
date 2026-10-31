namespace PCL.PluginSdk;

/// <summary>
///     插件文案本地化的**约定 + 小工具**（宿主不代管插件的文案表，只在
///     <see cref="IPluginContext.LanguageCode" /> 里告诉插件当前语言）。
///     <para>
///         约定：插件自己维护一份「文案键 → (语言码 → 文本)」的表，
///         需要出文案时调 <see cref="Pick" />，按当前语言挑一个。
///         语言码与宿主一致：<c>zh-CN</c>、<c>zh_sgu</c>、<c>zh-TW</c>、<c>en-US</c>、<c>en-GB</c>、
///         <c>ja-JP</c>、<c>fr-FR</c>、<c>es-ES</c>。
///     </para>
///     <para>
///         回退顺序：精确语言 → 主语言（<c>zh-CN</c> → <c>zh</c>）→ <c>zh-CN</c> → 表里第一个值 →
///         <paramref name="fallback" />。所以插件只写中文也能在其它语言下正常显示。
///     </para>
/// </summary>
public static class PluginLocalization
{
    /// <summary>按当前语言从文案表里挑一条；挑不到就用 <paramref name="fallback" />。</summary>
    public static string Pick(IReadOnlyDictionary<string, string>? texts, string? currentLanguage,
        string fallback = "")
    {
        if (texts is null || texts.Count == 0)
            return fallback;

        var language = (currentLanguage ?? "").Trim();
        if (language.Length > 0)
        {
            // ① 精确匹配（忽略大小写与分隔符差异：zh_cn / zh-CN / zhcn 都当同一个）
            foreach (var (key, value) in texts)
                if (Normalize(key) == Normalize(language) && !string.IsNullOrEmpty(value))
                    return value;

            // ② 主语言（zh-CN → zh）
            var dash = language.IndexOfAny(['-', '_']);
            if (dash > 0)
            {
                var primary = Normalize(language[..dash]);
                foreach (var (key, value) in texts)
                    if (Normalize(key) == primary && !string.IsNullOrEmpty(value))
                        return value;
            }
        }

        // ③ 简体中文兜底
        foreach (var (key, value) in texts)
            if (Normalize(key) == "zhcn" && !string.IsNullOrEmpty(value))
                return value;

        // ④ 表里第一个非空值
        foreach (var value in texts.Values)
            if (!string.IsNullOrEmpty(value))
                return value;

        return fallback;
    }

    private static string Normalize(string text) =>
        text.Replace("-", "").Replace("_", "").Trim().ToLowerInvariant();
}
