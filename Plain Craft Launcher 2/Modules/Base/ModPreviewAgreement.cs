using System;
using System.Windows;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Logging;

namespace PCL;

/// <summary>
///     预览版协议：首次启动某个预览版构建时弹一次，必须同意才能继续使用。
///     <para>
///         判定依据是版本显示名里带 <c>-preview-</c>（例如 <c>27.1-preview-73(2.15.1)</c>）。
///         同意结果按**完整版本号**记录（<see cref="PCL.Core.App.States.UI.PreviewAgreementVersion" />），
///         换一个新的预览版会重新询问；快照版 / 正式版不会弹。
///     </para>
///     <para>
///         不同意时直接关闭启动器（<see cref="Application.Shutdown()" />），这是产品要求，不给「稍后再说」。
///     </para>
/// </summary>
internal static class ModPreviewAgreement
{
    private const string PreviewMarker = "-preview-";

    /// <summary>当前运行的构建是不是预览版。</summary>
    public static bool IsPreviewBuild =>
        !string.IsNullOrEmpty(ModBase.versionDisplayName) &&
        ModBase.versionDisplayName.Contains(PreviewMarker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     主窗体显示完成后调用：预览版且本版本还没同意过时弹协议；不同意就退出。
    ///     调用方确保在 UI 线程上（<see cref="ModBase.RunInUi(Action,bool)" />）。
    /// </summary>
    public static void EnsureAccepted()
    {
        try
        {
            if (!IsPreviewBuild) return;

            var version = ModBase.versionDisplayName;
            if (string.Equals(States.UI.PreviewAgreementVersion, version, StringComparison.Ordinal)) return;

            ModBase.Log($"[Preview] 首次启动预览版 {version}，弹出预览版协议");

            var result = ModMain.MyMsgBox(
                Lang.Text("Preview.Notice.Content"),
                Lang.Text("Preview.Notice.Title"),
                Lang.Text("Preview.Notice.Agree"),
                Lang.Text("Preview.Notice.Decline"));

            if (result == 1)
            {
                States.UI.PreviewAgreementVersion = version;
                ModBase.Log($"[Preview] 已同意 {version} 的预览版协议");
                return;
            }

            ModBase.Log("[Preview] 未同意预览版协议，启动器即将退出", ModBase.LogLevel.Msgbox);
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            // 这里的异常不值得把启动器卡死：记日志后放行
            LogWrapper.Warn(ex, "Preview", "预览版协议检查失败");
        }
    }
}
