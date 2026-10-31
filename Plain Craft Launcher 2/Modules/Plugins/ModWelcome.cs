using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Utils;

namespace PCL;

/// <summary>
///     启动欢迎语（⑩）：插件提供「欢迎语池」（<see cref="PCL.PluginSdk.IWelcomeMessagesProvider" />），
///     宿主等概率抽一条、替换占位符，然后用右下角小提示（Toast）显示。
///     <para>
///         占位符：<c>LAUNCHER_BOOT_COUNT</c> = 启动次数、<c>TIME</c> = 时段、
///         <c>PLAYERNAME</c> = 当前档案玩家名。
///         功能由插件声明（<c>welcome.messages</c>），停用插件即不再弹。
///     </para>
/// </summary>
internal static class ModWelcome
{
    private static readonly List<string> _messages = [];

    private static bool _isShownThisRun;

    /// <summary>由插件提供的欢迎语池（替换上一次的内容）。</summary>
    public static void SetMessages(IEnumerable<string>? messages)
    {
        lock (_messages)
        {
            _messages.Clear();
            if (messages is null) return;
            foreach (var message in messages)
                if (!string.IsNullOrWhiteSpace(message))
                    _messages.Add(message.Trim());
        }
    }

    /// <summary>本次启动显示一次欢迎语（重复调用无副作用）。</summary>
    public static void ShowWelcome()
    {
        if (_isShownThisRun) return;
        if (!FeatureFlags.IsEnabled(FeatureFlags.Keys.WelcomeMessages)) return;

        string message;
        lock (_messages)
        {
            if (_messages.Count == 0) return;
            message = _messages[Random.Shared.Next(_messages.Count)];
        }

        _isShownThisRun = true;

        try
        {
            var text = message
                .Replace("LAUNCHER_BOOT_COUNT", States.System.StartupCount.ToString())
                .Replace("TIME", TimeOfDayGreeting())
                .Replace("PLAYERNAME", PlayerName());
            ModBase.Log($"[Welcome] 欢迎语：{text}", ModBase.LogLevel.Debug);
            HintService.Hint(text, HintType.Info, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Welcome] 显示欢迎语失败", ModBase.LogLevel.Debug);
        }
    }

    /// <summary>
    ///     午别（与默认欢迎语池里的「TIME 好」拼接使用）：上午 / 中午 / 下午 / 晚上。
    /// </summary>
    private static string TimeOfDayGreeting()
    {
        var hour = DateTime.Now.Hour;
        if (hour is >= 5 and < 12) return "上午";
        if (hour is >= 12 and < 14) return "中午";
        if (hour is >= 14 and < 18) return "下午";
        return "晚上"; // 18:00 ~ 04:59
    }

    /// <summary>
    ///     玩家名：以**最后登录的档案**为准（<see cref="ProfileService.Current" /> 即 lastUsed 指向的档案）；
    ///     一个档案都没有时给一个中性称呼。
    /// </summary>
    private static string PlayerName()
    {
        try
        {
            var name = ProfileService.Current?.UserName;
            if (!string.IsNullOrWhiteSpace(name)) return name;
        }
        catch
        {
            // 忽略：未登录 / 无档案
        }

        return "冒险家";
    }
}
