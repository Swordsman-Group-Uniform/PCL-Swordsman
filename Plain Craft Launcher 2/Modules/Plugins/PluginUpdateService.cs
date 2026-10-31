using System.IO;
using System.Security.Cryptography;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.PluginHost;
using PCL.PluginSdk;

namespace PCL;

/// <summary>
///     插件更新检测与安装（**不需要插件市场**）：
///     插件清单里可选写 <c>updateUrl</c>，指向一个返回下面这类 JSON 的地址：
///     <code>
///     { "version": "1.1.0", "url": "https://.../Foo.pclpkg", "sha256": "<十六进制，可选>", "notes": "更新说明，可选" }
///     </code>
///     <para>
///         流程：取 JSON → 比版本（<see cref="PluginVersion.Compare" />）→ 用户确认 → 下载到临时文件 →
///         （声明了 <c>sha256</c> 就校验）→ <see cref="PluginInstaller.InstallPackage" /> → 重扫。
///     下载下来的包同样要过 <see cref="PluginIntegrity" />，所以「更新源被劫持」也塞不进改过的包。
///     </para>
/// </summary>
internal static class PluginUpdateService
{
    /// <summary>更新源返回的 JSON 模型。</summary>
    private sealed class PluginUpdateInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("version")]
        public string? Version { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("url")]
        public string? Url { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    /// <summary>检查并安装更新。整个过程在后台线程跑，界面操作自己切回 UI 线程。</summary>
    public static void CheckAndInstall(PluginDescriptor plugin)
    {
        ModBase.RunInNewThread(() =>
        {
            try
            {
                var manifest = plugin.Manifest;
                var updateUrl = manifest?.UpdateUrl;
                if (manifest is null || string.IsNullOrWhiteSpace(updateUrl))
                {
                    ModBase.RunInUi(() => HintService.Hint(
                        Lang.Text("Tools.Plugin.Update.NoSource", plugin.Name), HintType.Warning));
                    return;
                }

                PluginUpdateInfo? info;
                try
                {
                    info = HttpRequest.GetJsonAsync<PluginUpdateInfo>(updateUrl).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"[Plugin] 检查 {plugin.Id} 的更新失败", ModBase.LogLevel.Hint);
                    ModBase.RunInUi(() => HintService.Hint(
                        Lang.Text("Tools.Plugin.Update.CheckFailed", plugin.Name), HintType.Error));
                    return;
                }

                if (info is null || string.IsNullOrWhiteSpace(info.Version) || string.IsNullOrWhiteSpace(info.Url))
                {
                    ModBase.RunInUi(() => HintService.Hint(
                        Lang.Text("Tools.Plugin.Update.CheckFailed", plugin.Name), HintType.Error));
                    return;
                }

                if (PluginVersion.Compare(info.Version, plugin.Version) <= 0)
                {
                    ModBase.RunInUi(() => HintService.Hint(
                        Lang.Text("Tools.Plugin.Update.UpToDate", plugin.Name, plugin.Version), HintType.Success));
                    return;
                }

                // 询问用户（弹窗必须回到 UI 线程）
                var message = Lang.Text("Tools.Plugin.Update.Confirm", plugin.Name, plugin.Version, info.Version);
                if (!string.IsNullOrWhiteSpace(info.Notes))
                    message += "\n\n" + info.Notes;

                var accepted = false;
                ModBase.RunInUi(() => accepted = ModMain.MyMsgBox(message,
                    Lang.Text("Tools.Plugin.Update.Title"),
                    button2: Lang.Text("Common.Action.Cancel")) == 1);
                if (!accepted) return;

                // 下载
                var temp = Path.Combine(Path.GetTempPath(), $"pcl-plugin-{Guid.NewGuid():N}.pclpkg");
                try
                {
                    using var response = HttpRequest.Create(info.Url).SendAsync().GetAwaiter().GetResult();
                    response.EnsureSuccessStatusCode();
                    var bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                    File.WriteAllBytes(temp, bytes);

                    // 声明的 sha256 先校验（包内的 files 哈希由 PluginIntegrity 在校验安装时把关）
                    if (!string.IsNullOrWhiteSpace(info.Sha256))
                    {
                        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                        if (!string.Equals(actual, info.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                        {
                            ModBase.Log($"[Plugin] {plugin.Id} 的更新包哈希不匹配：期望 {info.Sha256}，实际 {actual}",
                                ModBase.LogLevel.Hint);
                            ModBase.RunInUi(() => HintService.Hint(
                                Lang.Text("Tools.Plugin.Update.HashMismatch", plugin.Name), HintType.Error));
                            return;
                        }
                    }

                    var result = PluginInstaller.InstallPackage(PluginHostBootstrap.PluginsDirectory, temp);
                    if (!result.Success)
                    {
                        ModBase.Log($"[Plugin] 安装 {plugin.Id} 的更新失败：{result.Error}", ModBase.LogLevel.Hint);
                        ModBase.RunInUi(() => HintService.Hint(
                            Lang.Text("Tools.Plugin.Update.InstallFailed", plugin.Name, result.Error ?? ""),
                            HintType.Error));
                        return;
                    }

                    ModBase.Log($"[Plugin] {plugin.Id} 已从 {plugin.Version} 更新到 {info.Version}",
                        ModBase.LogLevel.Normal);
                    PluginHostBootstrap.Reload();
                    ModBase.RunInUi(() => HintService.Hint(
                        Lang.Text("Tools.Plugin.Update.Installed", plugin.Name, info.Version!), HintType.Success));
                }
                finally
                {
                    try
                    {
                        if (File.Exists(temp)) File.Delete(temp);
                    }
                    catch
                    {
                        // 临时文件删不掉无所谓
                    }
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Plugin] 更新 {plugin.Id} 时出错", ModBase.LogLevel.Hint);
                ModBase.RunInUi(() => HintService.Hint(
                    Lang.Text("Tools.Plugin.Update.CheckFailed", plugin.Name), HintType.Error));
            }
        }, "PluginUpdate", ThreadPriority.BelowNormal);
    }
}
