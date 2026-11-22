using System.IO;
using System.Windows;
using System.Windows.Controls;
using AdvancedInstance.Features;
using PCL.PluginSdk;

namespace AdvancedInstance.Ui;

/// <summary>
///     「实例 → Mod 管理」页里的依赖关系面板（宿主通过
///     <see cref="IPluginInstanceModPanelProvider" /> 把它渲染在按钮区下面）。
///     内容比主页面那块更紧凑：一行摘要 + 问题清单 + 可展开的依赖一览。
/// </summary>
internal static class ModPanel
{
    public static FrameworkElement Build(IPluginContext context, Func<(string Name, string Path)?> currentInstance)
    {
        var root = new StackPanel();

        var summary = new TextBlock
        {
            FontFamily = UiKit.UiFont,
            FontSize = 13d,
            Foreground = UiKit.Foreground,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var problems = new TextBlock
        {
            FontFamily = UiKit.UiFont,
            FontSize = 12d,
            Foreground = UiKit.Foreground,
            TextWrapping = TextWrapping.Wrap
        };

        var details = new TextBlock
        {
            FontFamily = UiKit.UiFont,
            FontSize = 12d,
            Foreground = UiKit.MutedForeground,
            TextWrapping = TextWrapping.Wrap
        };

        var detailsHost = new Expander
        {
            Header = "展开依赖一览",
            IsExpanded = false,
            Foreground = UiKit.Foreground,
            FontFamily = UiKit.UiFont,
            FontSize = 12d,
            Margin = new Thickness(0, 6, 0, 0),
            Content = UiKit.ScrollableContent(details, 190d)
        };

        void Refresh(bool force)
        {
            var instance = currentInstance();
            if (instance is null)
            {
                summary.Text = "没有选中的实例。";
                problems.Text = "";
                details.Text = "";
                return;
            }

            var result = ModDependencyAnalyzer.Analyze(Path.Combine(instance.Value.Path, "mods"), force);
            summary.Text = result.Summary;
            summary.Foreground = result.HasProblems ? UiKit.WarningBrush : UiKit.SuccessBrush;

            problems.Text = result.Problems.Count == 0
                ? ""
                : string.Join("\n", result.Problems.Select(problem =>
                    (problem.Severity == "error" ? "⛔ " : "⚠ ") + problem.Text));

            details.Text = result.Mods.Count == 0
                ? "（没有可解析的 Mod 元数据）"
                : string.Join("\n", result.Mods.OrderBy(mod => mod.ModId).Select(mod =>
                {
                    var deps = mod.Depends.Count == 0
                        ? "无外部依赖"
                        : string.Join("、", mod.Depends.Select(pair =>
                            string.IsNullOrWhiteSpace(pair.Value) ? pair.Key : $"{pair.Key} {pair.Value}"));
                    return $"{mod.DisplayName}（{mod.ModId} {mod.Version}｜{mod.Loader}）→ {deps}";
                }));
        }

        Refresh(false);

        var buttons = UiKit.ButtonRow(
            UiKit.Button("重新检查依赖", () => Refresh(true), true),
            UiKit.Button("在「高级实例管理」页查看详情", () =>
                MessageBox.Show(
                    "完整页面在左侧栏的「高级实例管理」里：那里还能做存档自动备份、level.dat 编辑与投影版本适配。",
                    "高级实例管理", MessageBoxButton.OK, MessageBoxImage.Information)));

        root.Children.Add(summary);
        root.Children.Add(problems);
        root.Children.Add(buttons);
        root.Children.Add(detailsHost);
        return root;
    }
}
