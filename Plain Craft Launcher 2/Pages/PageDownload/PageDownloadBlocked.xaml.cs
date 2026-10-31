namespace PCL;

/// <summary>
///     下载功能被关闭时显示的占位页。
///     <para>
///         界面结构与「设置 → 反馈」被禁用时的显示一致（MyCard + MyUnavailablePlaceholder），
///         由 <see cref="DownloadPolicy.AllowDownloads" /> 控制；启动器不感知、不检测任何插件，
///         只读取这个进程内状态。
///     </para>
/// </summary>
public partial class PageDownloadBlocked
{
    public PageDownloadBlocked()
    {
        InitializeComponent();
        Loaded += (_, _) => PanBack.ScrollToHome();
    }
}
