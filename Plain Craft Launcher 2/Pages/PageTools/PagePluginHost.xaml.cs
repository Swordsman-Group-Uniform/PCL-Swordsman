using System.Windows;
using PCL.Core.App.Localization;
using PCL.PluginHost;
using PCL.PluginSdk;

namespace PCL;

/// <summary>
///     插件页面宿主：「工具 → 插件」侧栏里由插件注册的页面都渲染在这里。
///     <para>
///         宿主只负责外壳（标题 / 说明 / 来源 / 滚动 / 出错兜底），页面内容由插件的
///         <see cref="PluginPageDefinition.CreatePage" /> 现场创建。
///         切换页面时会先清空上一个内容，避免残留上一个插件的控件。
///     </para>
/// </summary>
public partial class PagePluginHost
{
    /// <summary>当前展示的页面标识（插件 id + 页面 id）。</summary>
    private string? _currentKey;

    /// <summary>当前页面对应的插件描述符（插件被卸载 / 禁用时用来判断是否需要切走）。</summary>
    private PluginDescriptor? _descriptor;

    public PagePluginHost()
    {
        InitializeComponent();
    }

    /// <summary>当前是否正展示某个插件页面。</summary>
    public bool HasPage => _currentKey is not null;

    /// <summary>当前展示的插件 id（没有时为 null）。</summary>
    public string? CurrentPluginId => _descriptor?.Id;

    /// <summary>
    ///     切到某个插件页面。相同页面重复调用不会重建控件。
    ///     创建失败时展示兜底卡片，绝不会把异常抛给导航流程。
    /// </summary>
    public void ShowPage(PluginDescriptor descriptor, PluginPageDefinition definition)
    {
        var key = $"{descriptor.Id}::{definition.Id}";
        if (_currentKey == key && PanContent.Content is not null)
            return;

        _currentKey = key;
        _descriptor = descriptor;

        TxtTitle.Text = string.IsNullOrWhiteSpace(definition.Title)
            ? definition.Id
            : definition.Title;
        TxtDescription.Text = definition.Description ?? "";
        TxtDescription.Visibility = string.IsNullOrWhiteSpace(definition.Description)
            ? Visibility.Collapsed
            : Visibility.Visible;
        TxtSource.Text = Lang.Text("Tools.Plugin.Page.Source", descriptor.Name, descriptor.Version);

        PanContent.Content = null;
        PanError.Visibility = Visibility.Collapsed;
        TxtError.Text = "";

        try
        {
            var factory = definition.CreatePage;
            if (factory is null)
                throw new InvalidOperationException("插件没有提供 CreatePage");

            var content = factory(descriptor.Context!);
            if (content is null)
                throw new InvalidOperationException("CreatePage 返回了 null");

            PanContent.Content = content;
            ModBase.Log($"[Plugin] 已打开插件页面 {descriptor.Id}/{definition.Id}", ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"[Plugin] 打开 {descriptor.Id}/{definition.Id} 的页面失败", ModBase.LogLevel.Hint);
            TxtError.Text = ex.Message;
            PanError.Visibility = Visibility.Visible;
        }

        PanBack.ScrollToHome();
    }

    /// <summary>清空当前页面（插件被卸载 / 禁用时调用，断开对插件控件的引用）。</summary>
    public void ClearPage()
    {
        _currentKey = null;
        _descriptor = null;
        PanContent.Content = null;
        PanError.Visibility = Visibility.Collapsed;
        TxtError.Text = "";
        TxtTitle.Text = "";
        TxtDescription.Text = "";
        TxtSource.Text = "";
    }
}
