using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AdvancedInstance.Ui;

/// <summary>
///     「投影版本适配」第一次使用时的风险提示弹窗。
///     <para>
///         宿主的 <see cref="PCL.PluginSdk.IPluginNotify.Confirm" /> 只有「确定 / 取消」两个按钮、
///         带不了勾选框，所以这里自己画一个小窗口（纯 WPF，不依赖启动器程序集）：
///         正文是用户要求的原话，下面一个默认勾上的「在更改版本前创建备份」。
///     </para>
///     <para>
///         窗口用 <see cref="Window.ShowDialog" /> 模态打开、Owner 指向启动器主窗口，
///         因此不会跑到主界面后面去。
///     </para>
/// </summary>
internal static class RiskDialog
{
    private const string WarningText =
        "此功能具有一定风险，可能导致投影文件损坏。请慎重考虑是否更改版本。" +
        "我们推荐你勾选「在更改版本前创建备份」，如需更改此设置，请前往新版文件管理页面。";

    /// <summary>
    ///     显示风险提示。返回用户是否点了「继续」，同时回传勾选框状态。
    /// </summary>
    public static bool Show(out bool backupChecked)
    {
        backupChecked = true;

        var window = new Window
        {
            Title = "投影版本适配 · 风险提示",
            Width = 470d,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Owner = Application.Current?.MainWindow,
            Background = UiKit.CardBackground
        };

        var root = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };

        root.Children.Add(new TextBlock
        {
            Text = "⚠",
            FontSize = 26d,
            Foreground = UiKit.WarningBrush,
            Margin = new Thickness(0, 0, 0, 8)
        });

        root.Children.Add(new TextBlock
        {
            Text = WarningText,
            FontFamily = UiKit.UiFont,
            FontSize = 13d,
            Foreground = UiKit.Foreground,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21d,
            Margin = new Thickness(0, 0, 0, 14)
        });

        var check = UiKit.Check("在更改版本前创建备份", true);
        root.Children.Add(check);

        var status = new UiKit.StatusLine();
        root.Children.Add(status.Element);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var cancel = UiKit.Button("取消", () => window.DialogResult = false);
        cancel.Margin = new Thickness(0, 0, 8, 0);
        var confirm = UiKit.Button("我已了解，继续", () => window.DialogResult = true, true);
        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        root.Children.Add(buttons);

        window.Content = root;
        var accepted = window.ShowDialog() == true;
        backupChecked = check.IsChecked == true;
        return accepted;
    }

    /// <summary>改完之后的确认小窗（列出结果摘要）。</summary>
    public static void ShowResult(string title, string message, bool isError)
    {
        var window = new Window
        {
            Title = title,
            Width = 470d,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Owner = Application.Current?.MainWindow,
            Background = UiKit.CardBackground
        };

        var root = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
        root.Children.Add(new TextBlock
        {
            Text = message,
            FontFamily = UiKit.UiFont,
            FontSize = 13d,
            Foreground = isError ? UiKit.ErrorBrush : UiKit.Foreground,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 21d,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var ok = UiKit.Button("好", () => window.DialogResult = true, true);
        ok.HorizontalAlignment = HorizontalAlignment.Right;
        root.Children.Add(ok);

        window.Content = root;
        window.ShowDialog();
    }
}
