using System.Collections.ObjectModel;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     回声洞（Echo Cave）：以打字动画展示被收录的语录。
///     <para>
///         语录列表可在 <see cref="Quotes" /> 中直接增删；每 30 秒自动轮换到下一句，
///         也可以手动点击「下一句」切换。
///     </para>
/// </summary>
public static class ModEchoCave
{
    /// <summary>
    ///     内置兜底语录。真正的语录由「SGU 插件包」通过 <c>echo-cave</c> 功能提供，
    ///     插件未提供时才使用这里的内容。
    /// </summary>
    private static readonly List<string> _builtInQuotes =
    [
        "帝国之辉，终不熄灭的光！",
        "Welcome to the SGU！",
        "PCL Swordsman！"
    ];

    private static IReadOnlyList<string> _quotes = new List<string>(_builtInQuotes);

    /// <summary>
    ///     已收录的语录（可能来自插件）。
    /// </summary>
    public static ReadOnlyCollection<string> Quotes => new(_quotes.ToList());

    /// <summary>
    ///     用插件提供的语录替换当前列表；传空则回退到内置语录。
    /// </summary>
    public static void SetQuotes(IReadOnlyList<string>? quotes)
    {
        var cleaned = quotes?.Where(q => !string.IsNullOrWhiteSpace(q)).Select(q => q.Trim()).ToList();
        _quotes = cleaned is { Count: > 0 } ? cleaned : new List<string>(_builtInQuotes);
        CurrentIndex = 0;
    }

    /// <summary>
    ///     当前展示的语录序号。
    /// </summary>
    public static int CurrentIndex { get; private set; }

    /// <summary>
    ///     当前语录的完整文本。
    /// </summary>
    public static string CurrentQuote => _quotes.Count == 0 ? "" : _quotes[CurrentIndex % _quotes.Count];

    /// <summary>
    ///     切换到下一句语录并返回其文本。
    /// </summary>
    public static string Next()
    {
        if (_quotes.Count == 0) return string.Empty;
        CurrentIndex = (CurrentIndex + 1) % _quotes.Count;
        return CurrentQuote;
    }

    /// <summary>
    ///     随机取一句语录（与当前不同的那一句）并返回其文本。
    /// </summary>
    public static string RandomDifferent()
    {
        if (_quotes.Count <= 1) return CurrentQuote;
        var next = CurrentIndex;
        while (next == CurrentIndex) next = Random.Shared.Next(_quotes.Count);
        CurrentIndex = next;
        return CurrentQuote;
    }

    /// <summary>
    ///     回声洞的标题文本。
    /// </summary>
    public static string Title => Lang.Text("Tools.Test.EchoCave.Title");
}
