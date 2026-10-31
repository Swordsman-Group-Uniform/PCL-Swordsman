using System.Text;

namespace PCL.PluginHost;

/// <summary>
///     版本号解析 / 比较，以及插件依赖的版本范围判定。
///     <para>
///         宿主版本形如 <c>27.1-snapshot-57(2.15.1)</c>，插件清单里写 <c>minHostVersion: "27.1.0"</c>；
///         这里只取**前导数字段**做比较（<c>27.1-snapshot-57</c> → <c>27.1</c>），
///         后缀（snapshot / beta / 括号里的上游版本）一律忽略，避免快照号让判定飘。
///     </para>
///     <para>
///         依赖版本范围支持：<c>*</c> / 留空（任意）、<c>1.2.0</c>（即 <c>=1.2.0</c>）、
///         <c>&gt;=1.2</c>、<c>&gt;1.2</c>、<c>&lt;=1.2</c>、<c>&lt;1.2</c>、
///         <c>^1.2</c>（&gt;=1.2 且 &lt;2.0）、<c>~1.2</c>（&gt;=1.2 且 &lt;1.3）。
///     </para>
/// </summary>
public static class PluginVersion
{
    /// <summary>把版本字符串解析成 4 段数字（缺的补 0）；解析不到返回 <see langword="null" />。</summary>
    public static int[]? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // 先砍掉括号里的上游版本 / 后缀：27.1-snapshot-57(2.15.1) → 27.1-snapshot-57
        var head = text.Trim();
        var paren = head.IndexOf('(');
        if (paren > 0) head = head[..paren];

        var parts = new List<int>(4);
        var buffer = new StringBuilder();
        foreach (var ch in head)
        {
            if (char.IsDigit(ch))
            {
                buffer.Append(ch);
                continue;
            }

            if (buffer.Length > 0)
            {
                parts.Add(int.TryParse(buffer.ToString(), out var value) ? value : 0);
                buffer.Clear();
                if (parts.Count == 4) break;
            }

            // 数字段之间的分隔符：- . 空格 都可以，其余字符视为版本头结束
            if (ch is not ('.' or '-' or '_' or '+' or ' '))
                break;
        }

        if (buffer.Length > 0 && parts.Count < 4)
            parts.Add(int.TryParse(buffer.ToString(), out var last) ? last : 0);

        if (parts.Count == 0)
            return null;

        while (parts.Count < 4) parts.Add(0);
        return parts.ToArray();
    }

    /// <summary>比较两个版本；<paramref name="left" /> &lt; <paramref name="right" /> 返回负数。</summary>
    public static int Compare(string? left, string? right)
    {
        var a = Parse(left) ?? [0, 0, 0, 0];
        var b = Parse(right) ?? [0, 0, 0, 0];
        for (var i = 0; i < 4; i++)
        {
            var diff = a[i].CompareTo(b[i]);
            if (diff != 0) return diff;
        }

        return 0;
    }

    /// <summary><paramref name="actual" /> 是否 ≥ <paramref name="required" />。</summary>
    public static bool IsAtLeast(string? actual, string? required) => Compare(actual, required) >= 0;

    /// <summary>
    ///     判定 <paramref name="actualVersion" /> 是否满足 <paramref name="requirement" /> 描述的范围。
    ///     无法解析的需求按「不通过」处理，并通过 <paramref name="error" /> 说明原因。
    /// </summary>
    public static bool SatisfiesRange(string? requirement, string? actualVersion, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(requirement) || requirement.Trim() == "*")
            return true;

        var spec = requirement.Trim();
        var actual = Parse(actualVersion);
        if (actual is null)
        {
            error = "插件版本号无法解析";
            return false;
        }

        // ^1.2 / ~1.2 展开成区间
        if (spec.StartsWith('^'))
        {
            var lower = spec[1..];
            var low = Parse(lower);
            if (low is null)
            {
                error = $"无法解析版本范围 {spec}";
                return false;
            }

            var upper = $"{low[0] + 1}.0.0";
            return InRange(actual, lower, upper, out error);
        }

        if (spec.StartsWith('~'))
        {
            var lower = spec[1..];
            var low = Parse(lower);
            if (low is null)
            {
                error = $"无法解析版本范围 {spec}";
                return false;
            }

            var upper = $"{low[0]}.{low[1] + 1}.0";
            return InRange(actual, lower, upper, out error);
        }

        foreach (var (prefix, mode) in new[] { (">=", 0), ("<=", 1), (">", 2), ("<", 3), ("=", 4), ("==", 4) })
        {
            if (!spec.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            var target = spec[prefix.Length..];
            if (Parse(target) is null)
            {
                error = $"无法解析版本范围 {spec}";
                return false;
            }

            var cmp = Compare(actualVersion, target);
            var ok = mode switch
            {
                0 => cmp >= 0,
                1 => cmp <= 0,
                2 => cmp > 0,
                3 => cmp < 0,
                _ => cmp == 0
            };
            if (!ok) error = $"需要 {spec}，实际 {actualVersion}";
            return ok;
        }

        // 裸版本号 = 至少这个版本（插件作者最常这么写）
        if (Parse(spec) is null)
        {
            error = $"无法解析版本范围 {spec}";
            return false;
        }

        var atLeast = Compare(actualVersion, spec) >= 0;
        if (!atLeast) error = $"需要 ≥{spec}，实际 {actualVersion}";
        return atLeast;
    }

    private static bool InRange(int[] actual, string lower, string upper, out string? error)
    {
        error = null;
        var lowerCompare = Compare(Join(actual), lower);
        var upperCompare = Compare(Join(actual), upper);
        if (lowerCompare < 0)
        {
            error = $"需要 ≥{lower}，实际 {Join(actual)}";
            return false;
        }

        if (upperCompare >= 0)
        {
            error = $"需要 <{upper}，实际 {Join(actual)}";
            return false;
        }

        return true;
    }

    private static string Join(int[] parts) => $"{parts[0]}.{parts[1]}.{parts[2]}";
}
