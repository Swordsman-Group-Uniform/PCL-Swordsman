using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AdvancedInstance.Ui;

/// <summary>
///     插件页面用到的一小组控件工厂。
///     <para>
///         插件**不能**引用启动器程序集（不然会加载第二份宿主类型），所以这里的卡片 / 按钮
///         都用原生 WPF 拼；颜色尽量取宿主的主题资源（<c>ColorBrush*</c>），
///         取不到就退回一套灰阶兜底，保证在任何主题下都能看清。
///     </para>
/// </summary>
internal static class UiKit
{
    public static readonly FontFamily UiFont = new("Microsoft YaHei UI, Segoe UI, Sans Serif");

    /// <summary>取宿主主题资源（拿不到返回 null）。</summary>
    public static object? ThemeResource(string key)
    {
        try
        {
            return Application.Current?.TryFindResource(key);
        }
        catch
        {
            return null;
        }
    }

    public static Brush BrushOf(string key, Color fallback)
    {
        if (ThemeResource(key) is Brush brush) return brush;
        var solid = new SolidColorBrush(fallback);
        solid.Freeze();
        return solid;
    }

    /// <summary>正文字色。</summary>
    public static Brush Foreground => BrushOf("ColorBrush1", Color.FromRgb(0x33, 0x33, 0x33));

    /// <summary>弱化文字色（说明、注释）。</summary>
    public static Brush MutedForeground => BrushOf("ColorBrushGray3", Color.FromRgb(0x88, 0x88, 0x88));

    /// <summary>强调色（按钮 / 选中态）。</summary>
    public static Brush Accent => BrushOf("ColorBrush3", Color.FromRgb(0x33, 0x88, 0xDD));

    /// <summary>卡片背景。</summary>
    public static Brush CardBackground => BrushOf("ColorBrushBackground", Colors.White);

    /// <summary>卡片描边。</summary>
    public static Brush CardBorder => BrushOf("ColorBrushGray6", Color.FromRgb(0xDD, 0xDD, 0xDD));

    /// <summary>行内小输入框背景。</summary>
    public static Brush InputBackground => BrushOf("ColorBrushGray8", Color.FromRgb(0xF5, 0xF5, 0xF5));

    public static SolidColorBrush WarningBrush { get; } = new(Color.FromRgb(0xE0, 0x7B, 0x1E));

    public static SolidColorBrush ErrorBrush { get; } = new(Color.FromRgb(0xD9, 0x53, 0x4F));

    public static SolidColorBrush SuccessBrush { get; } = new(Color.FromRgb(0x4C, 0xAF, 0x50));

    /// <summary>一张卡片（标题 + 内容）。</summary>
    public static Border Card(string title, params UIElement[] children)
    {
        var titleText = new TextBlock
        {
            Text = title,
            FontSize = 15d,
            FontWeight = FontWeights.SemiBold,
            FontFamily = UiFont,
            Foreground = Foreground,
            Margin = new Thickness(0, 0, 0, 10)
        };

        var body = new StackPanel();
        body.Children.Add(titleText);
        foreach (var child in children) body.Children.Add(child);

        return new Border
        {
            Background = CardBackground,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 14, 16, 16),
            Margin = new Thickness(0, 0, 0, 14),
            Child = body
        };
    }

    /// <summary>一句话说明。</summary>
    public static TextBlock Muted(string text, double fontSize = 12d)
        => new()
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = UiFont,
            Foreground = MutedForeground,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };

    /// <summary>正文一行。</summary>
    public static TextBlock Text(string text, double fontSize = 13d, Brush? foreground = null)
        => new()
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = UiFont,
            Foreground = foreground ?? Foreground,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };

    /// <summary>一个按钮。</summary>
    public static Button Button(string text, Action onClick, bool primary = false)
    {
        var button = new Button
        {
            Content = text,
            FontFamily = UiFont,
            FontSize = 13d,
            Padding = new Thickness(13, 6, 13, 6),
            Margin = new Thickness(0, 0, 8, 8),
            MinWidth = 96d,
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(1),
            BorderBrush = primary ? Accent : CardBorder,
            Foreground = primary ? Brushes.White : Foreground,
            Background = primary ? Accent : InputBackground
        };
        button.Click += (_, _) =>
        {
            try
            {
                onClick();
            }
            catch (Exception ex)
            {
                MessageBox.Show("操作失败：" + ex.Message, "高级实例管理", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        return button;
    }

    /// <summary>一行「标签 + 控件」。</summary>
    public static Grid Row(string label, UIElement control, double labelWidth = 132d)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var text = new TextBlock
        {
            Text = label,
            FontSize = 13d,
            FontFamily = UiFont,
            Foreground = Foreground,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    /// <summary>横向排列的一组控件。</summary>
    public static WrapPanel ButtonRow(params UIElement[] children)
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 4, 0, 4) };
        foreach (var child in children) panel.Children.Add(child);
        return panel;
    }

    /// <summary>数字输入框（带校验）。</summary>
    public static TextBox NumberBox(int value, double width = 76d)
        => new()
        {
            Text = value.ToString(),
            Width = width,
            FontFamily = UiFont,
            FontSize = 13d,
            Padding = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = InputBackground,
            Foreground = Foreground,
            BorderBrush = CardBorder,
            HorizontalAlignment = HorizontalAlignment.Left
        };

    /// <summary>文本输入框。</summary>
    public static TextBox TextBox(string value, double width = 260d)
        => new()
        {
            Text = value,
            Width = width,
            FontFamily = UiFont,
            FontSize = 13d,
            Padding = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = InputBackground,
            Foreground = Foreground,
            BorderBrush = CardBorder,
            HorizontalAlignment = HorizontalAlignment.Left
        };

    /// <summary>下拉框。</summary>
    public static ComboBox Combo(IEnumerable<string> items, int selectedIndex = 0)
    {
        var combo = new ComboBox
        {
            FontFamily = UiFont,
            FontSize = 13d,
            MinWidth = 220d,
            Padding = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        foreach (var item in items) combo.Items.Add(item);
        if (combo.Items.Count > 0) combo.SelectedIndex = Math.Clamp(selectedIndex, 0, combo.Items.Count - 1);
        return combo;
    }

    /// <summary>勾选框。</summary>
    public static CheckBox Check(string text, bool isChecked)
        => new()
        {
            Content = text,
            IsChecked = isChecked,
            FontFamily = UiFont,
            FontSize = 13d,
            Foreground = Foreground,
            Margin = new Thickness(0, 4, 0, 8)
        };

    /// <summary>分割线。</summary>
    public static Border Separator()
        => new()
        {
            Height = 1,
            Background = CardBorder,
            Margin = new Thickness(0, 10, 0, 10)
        };

    /// <summary>把已有控件包进一个固定高度的滚动区（日志 / 依赖一览）。</summary>
    public static ScrollViewer ScrollableContent(UIElement content, double height = 140d)
        => new()
        {
            Content = content,
            Height = height,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Margin = new Thickness(0, 4, 0, 8)
        };

    /// <summary>可滚动的文本区（日志 / 问题列表）。</summary>
    public static ScrollViewer ScrollableText(string text, double height = 140d)
        => ScrollableContent(Text(text, 12d), height);

    /// <summary>一个可以就地更新文字的提示条。</summary>
    public sealed class StatusLine
    {
        private readonly TextBlock _block = new()
        {
            FontFamily = UiFont,
            FontSize = 12d,
            Foreground = MutedForeground,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 6)
        };

        public UIElement Element => _block;

        public void Info(string text) => Set(text, MutedForeground);

        public void Ok(string text) => Set(text, SuccessBrush);

        public void Warn(string text) => Set(text, WarningBrush);

        public void Error(string text) => Set(text, ErrorBrush);

        private void Set(string text, Brush color)
        {
            _block.Text = text;
            _block.Foreground = color;
        }
    }
}
