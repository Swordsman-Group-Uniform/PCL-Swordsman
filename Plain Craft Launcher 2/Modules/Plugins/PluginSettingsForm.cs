using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.PluginHost;
using PCL.PluginSdk;

namespace PCL;

/// <summary>
///     把插件声明的 <see cref="PluginSettingDefinition" /> 渲染成宿主样式的控件行。
///     <para>
///         两个地方共用：插件管理页的「齿轮」面板、设置 → 插件页。
///         读写的都是插件自己的 <see cref="IPluginConfig" />（就是插件 <c>Context.Config</c> 拿到的那个对象），
///         所以宿主改完插件立刻能读到，插件不需要自己写界面也不需要自己保存。
///     </para>
/// </summary>
internal static class PluginSettingsForm
{
    /// <summary>一行设置：左边标题 + 说明，右边控件。</summary>
    public static FrameworkElement BuildRow(PluginDescriptor plugin, PluginSettingDefinition definition)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 14) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190d) });

        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        text.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(definition.Title) ? definition.Key : definition.Title,
            FontSize = 13d,
            FontWeight = FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap
        });
        if (!string.IsNullOrWhiteSpace(definition.Description))
            text.Children.Add(new TextBlock
            {
                Text = definition.Description,
                FontSize = 12d,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                Foreground = Application.Current?.TryFindResource("ColorBrushGray3") as Brush ?? Brushes.Gray
            });
        grid.Children.Add(text);

        var control = BuildControl(plugin, definition);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    /// <summary>把一个插件的全部设置项渲染成一串行（没有设置项时返回空列表）。</summary>
    public static List<FrameworkElement> BuildRows(PluginDescriptor plugin, IPluginSettingsProvider provider)
    {
        var rows = new List<FrameworkElement>();

        IReadOnlyList<PluginSettingDefinition> definitions;
        try
        {
            definitions = provider.GetSettings() ?? [];
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"[Plugin] {plugin.Id} 的 GetSettings() 抛出异常", ModBase.LogLevel.Hint);
            return rows;
        }

        foreach (var definition in definitions)
        {
            if (definition is null || string.IsNullOrWhiteSpace(definition.Key)) continue;
            rows.Add(BuildRow(plugin, definition));
        }

        return rows;
    }

    private static FrameworkElement BuildControl(PluginDescriptor plugin, PluginSettingDefinition definition)
    {
        var config = plugin.Config;
        if (config is null)
            return new TextBlock
            {
                Text = "—",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Foreground = Application.Current?.TryFindResource("ColorBrushGray3") as Brush ?? Brushes.Gray
            };

        switch (definition.Type)
        {
            case PluginSettingType.Toggle:
            {
                var defaultValue = !string.Equals(definition.DefaultValue, "false", StringComparison.OrdinalIgnoreCase);
                var check = new MyCheckBox
                {
                    Checked = config.Get(definition.Key, defaultValue),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                check.Change += (_, user) =>
                {
                    if (!user) return;
                    Save(plugin, definition, () => config.Set(definition.Key, check.Checked == true));
                };
                return check;
            }

            case PluginSettingType.Choice:
            {
                var options = definition.Options ?? [];
                var combo = new MyComboBox
                {
                    Width = 190d,
                    VerticalAlignment = VerticalAlignment.Center
                };
                combo.ItemsSource = options.Select(option => option.Title).ToList();

                var current = config.Get(definition.Key, definition.DefaultValue ?? options.FirstOrDefault()?.Value ?? "");
                var index = options.FindIndex(option =>
                    string.Equals(option.Value, current, StringComparison.OrdinalIgnoreCase));
                combo.SelectedIndex = index < 0 ? (options.Count > 0 ? 0 : -1) : index;

                var guard = true;
                combo.SelectionChanged += (_, _) =>
                {
                    if (guard) return;
                    var selected = combo.SelectedIndex;
                    if (selected < 0 || selected >= options.Count) return;
                    var value = options[selected].Value;
                    Save(plugin, definition, () => config.Set(definition.Key, value));
                };
                guard = false;
                return combo;
            }

            case PluginSettingType.Integer when definition.Max is not null:
            {
                var max = (int)Math.Clamp(definition.Max.Value, 1d, 1000d);
                var current = (int)Math.Clamp(config.Get(definition.Key, ParseDouble(definition.DefaultValue)), 0d, max);
                var slider = new MySlider
                {
                    MaxValue = max,
                    Value = current,
                    Width = 190d,
                    VerticalAlignment = VerticalAlignment.Center
                };
                slider.Change += (_, user) =>
                {
                    if (!user) return;
                    Save(plugin, definition, () => config.Set(definition.Key, (long)slider.Value));
                };
                return slider;
            }

            case PluginSettingType.Integer:
            case PluginSettingType.Decimal:
            {
                var current = config.Get(definition.Key, ParseDouble(definition.DefaultValue));
                var box = new MyTextBox
                {
                    Text = current.ToString(CultureInfo.InvariantCulture),
                    Width = 190d,
                    VerticalAlignment = VerticalAlignment.Center,
                    HintText = definition.Placeholder ?? ""
                };
                var guard = true;
                box.TextChanged += (_, _) =>
                {
                    if (guard) return;
                    if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                        return;
                    if (definition.Min is not null && parsed < definition.Min.Value) return;
                    if (definition.Max is not null && parsed > definition.Max.Value) return;
                    Save(plugin, definition,
                        () => config.Set(definition.Key,
                            definition.Type == PluginSettingType.Integer ? (object)(long)Math.Round(parsed) : parsed));
                };
                guard = false;
                return box;
            }

            default:
            {
                var box = new MyTextBox
                {
                    Text = config.Get(definition.Key, definition.DefaultValue ?? ""),
                    Width = 190d,
                    VerticalAlignment = VerticalAlignment.Center,
                    HintText = definition.Placeholder ?? ""
                };
                var guard = true;
                box.TextChanged += (_, _) =>
                {
                    if (guard) return;
                    var value = box.Text ?? "";
                    Save(plugin, definition, () => config.Set(definition.Key, value));
                };
                guard = false;
                return box;
            }
        }
    }

    private static double ParseDouble(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0d;

    /// <summary>写一项设置：失败只记日志，不影响界面（落盘由 <see cref="IPluginConfig.Set{T}" /> 负责）。</summary>
    private static void Save(PluginDescriptor plugin, PluginSettingDefinition definition, Action write)
    {
        try
        {
            write();
            ModBase.Log($"[Plugin] {plugin.Id} 的设置 {definition.Key} 已更新", ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"[Plugin] 写入 {plugin.Id} 的设置 {definition.Key} 失败", ModBase.LogLevel.Hint);
        }
    }
}
