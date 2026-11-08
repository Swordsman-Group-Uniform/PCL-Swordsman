using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.UI;
using PCL.Core.UI.Theme;
using PCL.Core.Utils;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupUI
{
    /// <summary>
    ///     主题色下拉框每一项对应的配色主题（顺序与 <see cref="ThemeColors" /> 一致）。
    /// </summary>
    /// <summary>
    ///     全部可选主题的展示顺序。
    /// </summary>
    private static readonly ColorTheme[] AllThemes =
    [
        ColorTheme.SkyBlue, ColorTheme.CatBlue, ColorTheme.DeathBlue,
        ColorTheme.FatFishBlue, ColorTheme.TianyiBlue, ColorTheme.MermanBlue, ColorTheme.IchikaBlue,
        ColorTheme.SwordsmanYellow, ColorTheme.SakuraPink, ColorTheme.NetherRed, ColorTheme.MethylOrange,
        ColorTheme.AmethystPurple, ColorTheme.MikuGreen, ColorTheme.KaoYinGreen,
        // 四个原版贴图主题（snapshot-86）：琉璃彩 / 曜石黑 / 蜂蜜黄 / 黏液绿
        ColorTheme.LiuliGlass, ColorTheme.ObsidianBlack, ColorTheme.HoneyYellow, ColorTheme.SlimeGreen,
        ColorTheme.Custom
    ];

    /// <summary>
    ///     一个主题下拉项：内置主题（<c>PluginThemeId</c> 为 null）或插件自带的主题。
    ///     插件主题全部落在 <see cref="ColorTheme.PluginDefined" /> 槽位上，靠 id 区分。
    /// </summary>
    private sealed record ThemeChoice(ColorTheme Theme, string? PluginThemeId);

    /// <summary>
    ///     当前构建中实际展示的主题项：发行版只保留蓝色系主题，插件主题只要插件提供了就展示。
    /// </summary>
    private static IReadOnlyList<ThemeChoice> ThemeChoices
    {
        get
        {
            var values = AllThemes.Where(ThemeService.IsThemeSelectable)
                .Select(theme => new ThemeChoice(theme, null))
                .ToList();

            foreach (var (id, _, _) in ThemeService.PluginThemes)
                values.Add(new ThemeChoice(ColorTheme.PluginDefined, id));

            if (Basics.IsAprilFool) values.Add(new ThemeChoice(ColorTheme.HmclBlue, null));
            return values;
        }
    }

    /// <summary>兼容旧代码：只取槽位枚举（插件主题会出现多个重复的 PluginDefined）。</summary>
    private static ColorTheme[] ThemeColorValues => ThemeChoices.Select(choice => choice.Theme).ToArray();

    public string[] ThemeColors => ThemeChoices.Select(choice =>
        choice.PluginThemeId is not null
            ? ThemeService.GetPluginThemeDisplayName(choice.PluginThemeId) ?? choice.PluginThemeId
            : ThemeService.GetExtraThemeDisplayName(choice.Theme) ?? (choice.Theme switch
    {
        ColorTheme.SkyBlue => Lang.Text("Setup.Ui.Theme.Color.SkyBlue"),
        ColorTheme.CatBlue => Lang.Text("Setup.Ui.Theme.Color.CatBlue"),
        ColorTheme.DeathBlue => Lang.Text("Setup.Ui.Theme.Color.CrashBlue"),
        ColorTheme.HmclBlue => Lang.Text("Setup.Ui.Theme.Color.Hmcl"),
        ColorTheme.FatFishBlue => Lang.Text("Setup.Ui.Theme.Color.FatFishBlue"),
        ColorTheme.TianyiBlue => Lang.Text("Setup.Ui.Theme.Color.TianyiBlue"),
        ColorTheme.SwordsmanYellow => Lang.Text("Setup.Ui.Theme.Color.SwordsmanYellow"),
        ColorTheme.SakuraPink => Lang.Text("Setup.Ui.Theme.Color.SakuraPink"),
        ColorTheme.NetherRed => Lang.Text("Setup.Ui.Theme.Color.NetherRed"),
        ColorTheme.MethylOrange => Lang.Text("Setup.Ui.Theme.Color.MethylOrange"),
        ColorTheme.AmethystPurple => Lang.Text("Setup.Ui.Theme.Color.AmethystPurple"),
        ColorTheme.LiuliGlass => Lang.Text("Setup.Ui.Theme.Color.Liuli"),
        ColorTheme.ObsidianBlack => Lang.Text("Setup.Ui.Theme.Color.Obsidian"),
        ColorTheme.HoneyYellow => Lang.Text("Setup.Ui.Theme.Color.Honey"),
        ColorTheme.SlimeGreen => Lang.Text("Setup.Ui.Theme.Color.Slime"),
        ColorTheme.MikuGreen => Lang.Text("Setup.Ui.Theme.Color.MikuGreen"),
        ColorTheme.MermanBlue => Lang.Text("Setup.Ui.Theme.Color.MermanBlue"),
        ColorTheme.KaoYinGreen => Lang.Text("Setup.Ui.Theme.Color.KaoYinGreen"),
        ColorTheme.IchikaBlue => Lang.Text("Setup.Ui.Theme.Color.IchikaBlue"),
        ColorTheme.PluginDefined => Lang.Text("Setup.Ui.Theme.Color.Custom"),
        _ => Lang.Text("Setup.Ui.Theme.Color.Custom")
    })).ToArray();
    
    public new bool isLoaded;

    public PageSetupUI()
    {
        InitializeComponent();
        Loaded += PageSetupUI_Loaded;
        Loaded += (_, _) => HiddenRefresh();
        Loaded += (_, _) => MaterialSubscribe();
        Unloaded += (_, _) => MaterialUnsubscribe();
    }

    private void PageSetupUI_Loaded(object sender, RoutedEventArgs e)
    {
        // 重复加载部分
        PanBack.ScrollToHome();

        ModAnimation.AniBeginControl();
        Reload(); // #4826，在每次进入页面时都刷新一下
        ModAnimation.AniEndControl();

        // 非重复加载部分
        if (isLoaded)
            return;
        isLoaded = true;

        SliderLoad();

        PanLauncherHide.Visibility = Visibility.Visible;
    }

    public void Reload()
    {
        try
        {
            RefreshThemeCombos();
            // 启动器
            SliderLauncherOpacity.Value = Config.Preference.Theme.WindowOpacity;
            CheckLauncherLogo.Checked = Config.Preference.ShowStartupLogo;
            ComboDarkMode.SelectedIndex = (int)Config.Preference.Theme.ColorMode;
            // 深浅色主题已合并：只有一个配色主题下拉（统一以浅色槽位为准，深色槽位由 ThemeService 镜像）
            var themeChoices = ThemeChoices;
            var themeIndex = IndexOfChoice(themeChoices, Config.Preference.Theme.LightColor,
                Config.Preference.Theme.LightPluginTheme);
            if (themeIndex < 0)
            {
                ThemeService.SetCurrentThemeSelection(ColorTheme.CatBlue, null);
                themeIndex = IndexOfChoice(themeChoices, ColorTheme.CatBlue, null);
            }

            ComboThemeColor.SelectedIndex = Math.Max(0, themeIndex);
            PanCustomColor.Visibility = ThemeService.IsCustomColorEnabled
                ? Visibility.Visible
                : Visibility.Collapsed;
            TextCustomColor.Text = ThemeService.NormalizeHexColor(Config.Preference.Theme.CustomColor) ?? "#39C5BB";
            TextCustomColor.Validate();
            RefreshCustomColorPreview();
            CheckShowLaunchingHint.Checked = Config.Preference.ShowLaunchingHint;

            // 字体设置
            ComboUiFont.SelectedFontTag = Config.Preference.Font;
            ComboUiMotdFont.SelectedFontTag = Config.Preference.MotdFont;

            SliderWindowCornerRadius.Value = Math.Clamp(Config.System.WindowCornerRadius, 0, 24);
            CheckBlur.Checked = Config.Preference.Blur.IsEnabled;
            SliderBlurValue.Value = Config.Preference.Blur.Radius;
            SliderBlurSamplingRate.Value = Config.Preference.Blur.SamplingRate;
            ComboBlurType.SelectedIndex = Config.Preference.Blur.KernelType;
            PanBlurValue.Visibility = CheckBlur.Checked == true ? Visibility.Visible : Visibility.Collapsed;
            CheckLockWindowSize.Checked = Config.Preference.LockWindowSize;

            // 高级材质（云母 / Aero）
            ReloadMaterial();

            // 背景图片
            SliderBackgroundOpacity.Value = Config.Preference.Background.WallpaperOpacity;
            SliderBackgroundBlur.Value = Config.Preference.Background.WallpaperBlurRadius;
            ComboBackgroundSuit.SelectedIndex = Config.Preference.Background.WallpaperSuitMode;
            CheckBackgroundColorful.Checked = Config.Preference.Background.BackgroundColorful;
            var autoPauseVideo = Config.Preference.Background.AutoPauseVideo;
            CheckAutoPauseVideo.Checked = autoPauseVideo;
            if (ModVideoBack.IsGaming)
                if (autoPauseVideo)
                    BtnBackgroundRefresh.IsEnabled = false;

            BackgroundRefresh(false, false);

            // 标题栏
            ((MyRadioBox)FindName("RadioLogoType" + (int)Config.Preference.WindowTitleType))
                .Checked = true;
            CheckLogoLeft.Visibility = RadioLogoType0.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoText.Visibility = RadioLogoType2.Checked ? Visibility.Visible : Visibility.Collapsed;
            PanLogoChange.Visibility = RadioLogoType3.Checked ? Visibility.Visible : Visibility.Collapsed;
            TextLogoText.Text = Config.Preference.WindowTitleCustomText;
            CheckLogoLeft.Checked = Config.Preference.TopBarLeftAlign;

            // 背景音乐
            CheckMusicRandom.Checked = Config.Preference.Music.ShufflePlayback;
            CheckMusicAuto.Checked = Config.Preference.Music.StartOnStartup;
            CheckMusicStop.Checked = Config.Preference.Music.StopInGame;
            CheckMusicStart.Checked = Config.Preference.Music.StartInGame;
            CheckMusicSMTC.Checked = Config.Preference.Music.EnableSMTC;
            SliderMusicVolume.Value = Config.Preference.Music.Volume;
            MusicRefreshUI();

            // 主页
            try
            {
                ComboCustomPreset.SelectedIndex = Config.Preference.Homepage.SelectedPreset;
            }
            catch
            {
                Config.Preference.Homepage.SelectedPresetConfig.Reset();
            }

            ((MyRadioBox)FindName("RadioCustomType" + Config.Preference.Homepage.Type)).Checked = true;
            TextCustomNet.Text = Config.Preference.Homepage.CustomUrl;
            ModSetup.UiCustomType(Config.Preference.Homepage.Type);

            // 功能隐藏
            // 获取配置组引用
            var uiHidden = Config.Preference.Hide;

            // 主页面
            CheckHiddenPageDownload.Checked = uiHidden.PageDownload;
            CheckHiddenPageSetup.Checked = uiHidden.PageSetup;
            CheckHiddenPageTools.Checked = uiHidden.PageTools;

            // 子页面 设置
            CheckHiddenSetupLaunch.Checked = uiHidden.SetupLaunch;
            CheckHiddenSetupUI.Checked = uiHidden.SetupUi;
            CheckHiddenSetupLauncherLanguage.Checked = uiHidden.SetupLauncherLanguage;
            CheckHiddenSetupGameManage.Checked = uiHidden.SetupGameManage;
            CheckHiddenSetupJava.Checked = uiHidden.SetupJava;
            CheckHiddenLauncherMisc.Checked = uiHidden.SetupLauncherMisc;
            CheckHiddenSetupUpdate.Checked = uiHidden.SetupUpdate;
            CheckHiddenSetupGameLink.Checked = uiHidden.SetupGameLink;
            CheckHiddenSetupAbout.Checked = uiHidden.SetupAbout;
            CheckHiddenSetupFeedback.Checked = uiHidden.SetupFeedback;
            CheckHiddenSetupLog.Checked = uiHidden.SetupLog;

            // 子页面 工具
            CheckHiddenToolsGameLink.Checked = uiHidden.ToolsGameLink;
            CheckHiddenToolsTest.Checked = uiHidden.ToolsTest;

            // 子页面 实例设置
            CheckHiddenVersionEdit.Checked = uiHidden.InstanceEdit;
            CheckHiddenVersionExport.Checked = uiHidden.InstanceExport;
            CheckHiddenVersionSave.Checked = uiHidden.InstanceSave;
            CheckHiddenVersionScreenshot.Checked = uiHidden.InstanceScreenshot;
            CheckHiddenVersionMod.Checked = uiHidden.InstanceMod;
            CheckHiddenVersionResourcePack.Checked = uiHidden.InstanceResourcePack;
            CheckHiddenVersionShader.Checked = uiHidden.InstanceShader;
            CheckHiddenVersionSchematic.Checked = uiHidden.InstanceSchematic;
            CheckHiddenVersionServer.Checked = uiHidden.InstanceServer;

            // 特定功能
            CheckHiddenFunctionSelect.Checked = uiHidden.FunctionSelect;
            CheckHiddenFunctionModUpdate.Checked = uiHidden.FunctionModUpdate;
            CheckHiddenFunctionHidden.Checked = uiHidden.FunctionHidden;
        }
        catch (NullReferenceException ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Ui.Error.ConfigReset"),
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.ConfigReset"));
            Reset();
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Ui.Error.LoadFailed"),
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.LoadFailed"));
        }
    }

    // 初始化
    public void Reset()
    {
        try
        {
            Config.Preference.Reset();
            ModBase.Log("[Setup] 已初始化个性化设置！");
            HintService.Hint(Lang.Text("Setup.Ui.Initialized"), HintType.Success, false);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Setup.Ui.Error.InitFailed"),
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.InitFailed"));
        }

        Reload();
    }

    // 将控件改变路由到设置改变
    private void SliderChange(object senderRaw, bool user)
    {
        var sender = (MySlider)senderRaw;
        if (!ModAnimation.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Value);
    }

    private void ComboChange(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        if (!ModAnimation.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.SelectedIndex);
    }

    private void CheckBoxChange(object senderRaw, bool user)
    {
        var sender = (MyCheckBox)senderRaw;
        if (!ModAnimation.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Checked);
    }

    private void TextBoxChange(object senderRaw, RoutedEventArgs e)
    {
        var sender = (MyTextBox)senderRaw;
        if (!ModAnimation.AniExitsAnyControl())
            SetByTag(sender.Tag?.ToString(), sender.Text);
    }

    private void RadioBoxChange(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyRadioBox)senderRaw;
        var gotCfg = sender.Tag?.ToString()?.Split("/") ?? Array.Empty<string>();
        if (!ModAnimation.AniExitsAnyControl() && gotCfg.Length >= 2)
            SetByTag(gotCfg[0], int.Parse(gotCfg[1]));
    }

    private static void SetByTag(string tag, object value)
    {
        ConfigService.TrySetValue(tag, value);
    }

    private void ComboFontChange(object sender, SelectionChangedEventArgs e)
    {
        if (!ModAnimation.AniExitsAnyControl()) Config.Preference.Font = ComboUiFont.SelectedFontTag;
    }

    private void ComboMotdFontChange(object sender, SelectionChangedEventArgs e)
    {
        if (!ModAnimation.AniExitsAnyControl()) Config.Preference.MotdFont = ComboUiMotdFont.SelectedFontTag;
    }

    // 背景图片
    private void BtnUIBgOpen_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenExplorer(ModBase.exePath + @"PCL\Pictures\");
    }

    private void BtnBackgroundRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        BackgroundRefresh(true, true);
    }

    public void BackgroundRefreshUI(bool show, int count)
    {
        if (PanBackgroundOpacity is null)
            return;
        if (show)
        {
            PanBackgroundOpacity.Visibility = Visibility.Visible;
            PanBackgroundBlur.Visibility = Visibility.Visible;
            PanBackgroundSuit.Visibility = Visibility.Visible;
            BtnBackgroundClear.Visibility = Visibility.Visible;
            CheckAutoPauseVideo.Visibility = Visibility.Visible;
            CardBackground.Title = Lang.Text("Setup.Ui.Background.TitleWithCount", count);
        }
        else
        {
            PanBackgroundOpacity.Visibility = Visibility.Collapsed;
            PanBackgroundBlur.Visibility = Visibility.Collapsed;
            PanBackgroundSuit.Visibility = Visibility.Collapsed;
            BtnBackgroundClear.Visibility = Visibility.Collapsed;
            CheckAutoPauseVideo.Visibility = Visibility.Collapsed;
            CardBackground.Title = Lang.Text("Setup.Ui.Background.TitleDefault");
        }

        CardBackground.TriggerForceResize();
    }

    private void BtnBackgroundClear_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox(Lang.Text("Setup.Ui.Background.Clear.Confirm.Message"),
                Lang.Text("Common.Dialog.Warning"), button2: Lang.Text("Common.Action.Cancel"),
                isWarn: true) == 1)
        {
            ModBase.DeleteDirectory(ModBase.exePath + @"PCL\Pictures");
            BackgroundRefresh(false, true);
            HintService.Hint(Lang.Text("Setup.Ui.Background.Clear.Success"), HintType.Success);
        }
    }

    /// <summary>
    ///     刷新背景图片及设置页 UI。
    /// </summary>
    /// <param name="isHint">是否显示刷新提示。</param>
    /// <param name="refresh">是否刷新图片显示。</param>
    public static void BackgroundRefresh(bool isHint, bool refresh)
    {
        try
        {
            // 获取可用的图片文件
            Directory.CreateDirectory(ModBase.exePath + @"PCL\Pictures\");
            var pic = ModBase.EnumerateFiles(ModBase.exePath + @"PCL\Pictures\").Where(file =>
                    !(file.Extension.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                      file.Extension.Equals(".db", StringComparison.OrdinalIgnoreCase))).Select(file => file.FullName)
                .ToList();

            // 视频加载异常处理

            EventHandler<ExceptionRoutedEventArgs> videoHandler = (sender, e) =>
            {
                var videoEx = e.ErrorException;
                var videoAddress = ModMain.frmMain.VideoBack.Source.ToString();
                if (ModMain.frmMain.VideoBack.Source is not null)
                {
                    ModVideoBack.VideoStop();

                    if (videoEx.Message.Contains("0xC00D109B"))
                        ModBase.Log(
                            $"""
                             刷新背景内容失败，该视频文件可能并非 H.264（AVC）格式。
                             你可以尝试使用视频转码工具打开视频文件并设定目标格式为 H.264（AVC），然后转码该视频。
                             文件：{videoAddress}
                             """,
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Setup.Ui.Error.BackgroundVideoUnsupported"));
                    else
                        ModBase.Log(
                            videoEx,
                            $"刷新背景内容失败（{videoAddress}）",
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            };
            ModMain.frmMain.VideoBack.MediaFailed -= videoHandler;
            ModVideoBack.GamingStateChanged -= ModVideoBack.OnGamingStateChanged;
            ModVideoBack.ForcePlayChanged -= ModVideoBack.OnForcePlayChanged;
            ModVideoBack.GamingStateChanged += ModVideoBack.OnGamingStateChanged;
            ModVideoBack.ForcePlayChanged += ModVideoBack.OnForcePlayChanged;
            if (!Config.Preference.Background.AutoPauseVideo)
                ModVideoBack.ForcePlay = true;
            // 加载
            if (pic.Count == 0)
            {
                if (refresh)
                {
                    if (ModMain.frmMain.ImgBack.Visibility == Visibility.Collapsed)
                    {
                        if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.NoAvailableContent"), HintType.Error);
                    }
                    else
                    {
                        ModMain.frmMain.ImgBack.Visibility = Visibility.Collapsed;
                        if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.Cleared"), HintType.Success);
                    }
                }

                if (ModMain.frmSetupUI is not null)
                    ModMain.frmSetupUI.BackgroundRefreshUI(false, 0);
            }
            else
            {
                if (refresh)
                {
                    var address = RandomUtils.PickRandom(pic);
                    try
                    {
                        ModMain.frmMain.ImgBack.Background = null;
                        ModVideoBack.VideoStop();
                        ModBase.Log("[UI] 加载背景内容：" + address);
                        ModMain.frmMain.ImgBack.Background = new MyBitmap(address);
                        _ = Config.Preference.Background.WallpaperSuitMode;
                        ModMain.frmMain.ImgBack.Visibility = Visibility.Visible;
                        if (isHint)
                                HintService.Hint(Lang.Text("Setup.Ui.Background.Refresh.Success", ModBase.GetFileNameFromPath(address)), HintType.Success,
                                false);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            ModMain.frmMain.VideoBack.MediaFailed += videoHandler;
                            ModBase.Log(ex, "[UI] 加载背景图片失败" + address);
                            if (ModBase.modeDebug)
                                HintService.Hint(Lang.Text("Setup.Ui.Background.ImageLoadFailed", address));
                            ModMain.frmMain.ImgBack.Visibility = Visibility.Visible;
                            ModMain.frmMain.VideoBack.Source = new Uri(address, UriKind.Absolute);
                            ModVideoBack.VideoPlay();
                            if (isHint)
                            HintService.Hint(Lang.Text("Setup.Ui.Background.Refresh.Success", ModBase.GetFileNameFromPath(address)), HintType.Success,
                                    false);
                        }
                        catch (Exception playEx)
                        {
                            ModBase.Log(playEx, "播放背景内容时出现未知错误：");
                        }
                    }
                }

                if (ModMain.frmSetupUI is not null)
                    ModMain.frmSetupUI.BackgroundRefreshUI(true, pic.Count);
            }
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "刷新背景内容时出现未知错误",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // 顶部栏
    private void BtnLogoChange_Click(object sender, MouseButtonEventArgs e)
    {
        var fileName = SystemDialogs.SelectFile(
            Lang.Text("Setup.Ui.ImageFile.Filter"),
            Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName))
            return;
        try
        {
            // 拷贝文件
            File.Delete(ModBase.exePath + @"PCL\Logo.png");
            ModBase.CopyFile(fileName, ModBase.exePath + @"PCL\Logo.png");
            // 设置当前显示
            ModMain.frmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
            ModMain.frmMain.ImageTitleLogo.Source = ModBase.exePath + @"PCL\Logo.png";
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("参数无效"))
                ModBase.Log(
                    """
                    改变标题栏图片失败，该图片文件可能并非标准格式。
                    你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。
                    """,
                    ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.TitleImageInvalidFormat"));
            else
                ModBase.Log(
                    ex,
                    "设置标题栏图片失败",
                    ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
            ModMain.frmMain.ImageTitleLogo.Source = null;
        }
    }

    private void RadioLogoType3_Check(object sender, ModBase.RouteEventArgs e)
    {
        if (!(!ModAnimation.AniExitsAnyControl() && e.raiseByMouse))
            return;
        Refresh: ;

        // 已有图片则不再选择
        if (File.Exists(ModBase.exePath + @"PCL\Logo.png"))
        {
            try
            {
                ModMain.frmMain.ImageTitleLogo.Source = null; // 防止因为 Source 属性前后的值相同而不更新 (#5628)
                ModMain.frmMain.ImageTitleLogo.Source = ModBase.exePath + @"PCL\Logo.png";
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("参数无效"))
                    ModBase.Log(
                        """
                        调整标题栏图片失败，该图片文件可能并非标准格式。
                        你可以尝试使用画图打开该文件并重新保存，这会让图片变为标准格式。
                        """,
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.TitleImageResizeInvalidFormat"));
                else
                    ModBase.Log(
                        ex,
                        "调整标题栏图片失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                ModMain.frmMain.ImageTitleLogo.Source = null;
                e.handled = true;
                try
                {
                    File.Delete(ModBase.exePath + @"PCL\Logo.png");
                }
                catch (Exception exx)
                {
                    ModBase.Log(
                        exx,
                        "清理错误的标题栏图片失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            }

            return;
        }

        // 没有图片则要求选择
        var fileName = SystemDialogs.SelectFile(Lang.Text("Setup.Ui.ImageFile.Filter"), Lang.Text("Setup.Ui.ImageFile.SelectTitle"));
        if (string.IsNullOrEmpty(fileName))
        {
            ModMain.frmMain.ImageTitleLogo.Source = null;
            e.handled = true;
        }
        else
        {
            try
            {
                // 拷贝文件
                File.Delete(ModBase.exePath + @"PCL\Logo.png");
                ModBase.CopyFile(fileName, ModBase.exePath + @"PCL\Logo.png");
                goto Refresh;
            }
            catch (Exception ex)
            {
                ModBase.Log(
                    ex,
                    "复制标题栏图片失败",
                    ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
            }
        }
    }

    private void BtnLogoDelete_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            File.Delete(ModBase.exePath + @"PCL\Logo.png");
            RadioLogoType1.SetChecked(true, true);
            HintService.Hint(Lang.Text("Setup.Ui.Logo.Clear.Success"), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "清空标题栏图片失败",
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // 背景音乐
    private void BtnMusicOpen_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenExplorer(ModBase.exePath + @"PCL\Musics\");
    }

    private void BtnMusicRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        ModMusic.MusicRefreshPlay(true);
    }

    public void MusicRefreshUI()
    {
        if (PanBackgroundOpacity is null)
            return;
        if (ModMusic.musicAllList.Any())
        {
            PanMusicVolume.Visibility = Visibility.Visible;
            PanMusicDetail.Visibility = Visibility.Visible;
            BtnMusicClear.Visibility = Visibility.Visible;
            CardMusic.Title = Lang.Text("Setup.Ui.Music.TitleWithCount", ModBase.EnumerateFiles(ModBase.exePath + @"PCL\Musics\").Count());
        }
        else
        {
            PanMusicVolume.Visibility = Visibility.Collapsed;
            PanMusicDetail.Visibility = Visibility.Collapsed;
            BtnMusicClear.Visibility = Visibility.Collapsed;
            CardMusic.Title = Lang.Text("Setup.Ui.Music.Title");
        }

        CardMusic.TriggerForceResize();
    }

    private void BtnMusicClear_Click(object sender, MouseButtonEventArgs e)
    {
        if (ModMain.MyMsgBox(Lang.Text("Setup.Ui.Music.Clear.Confirm.Message"),
                Lang.Text("Common.Dialog.Warning"), button2: Lang.Text("Common.Action.Cancel"),
                isWarn: true) == 1)
            ModBase.RunInThread(() =>
            {
                HintService.Hint(Lang.Text("Setup.Ui.Music.Deleting"));
                // 停止播放音乐
                ModMusic.musicNAudio = null;
                ModMusic.musicWaitingList = new List<string>();
                ModMusic.musicAllList = new List<string>();
                Thread.Sleep(200);
                // 删除文件
                try
                {
                    ModBase.DeleteDirectory(ModBase.exePath + @"PCL\Musics");
                    // DisableSMTCSupport()
                    HintService.Hint(Lang.Text("Setup.Ui.Music.Delete.Success"), HintType.Success);
                }
                catch (Exception ex)
                {
                    ModBase.Log(
                        ex,
                        "删除背景音乐失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }

                try
                {
                    Directory.CreateDirectory(ModBase.exePath + @"PCL\Musics");
                    ModBase.RunInUi(() => ModMusic.MusicRefreshPlay(false));
                }
                catch (Exception ex)
                {
                    ModBase.Log(
                        ex,
                        "重建背景音乐文件夹失败",
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
                }
            });
    }

    private void CheckMusicStart_Change(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        if (ModAnimation.AniExitsAnyControl())
            return;
        if (CheckMusicStart.Checked == true)
            CheckMusicStop.Checked = false;
    }

    private void CheckMusicStop_Change()
    {
        if (ModAnimation.AniExitsAnyControl())
            return;
        if (CheckMusicStop.Checked == true)
            CheckMusicStart.Checked = false;
    }

    // 主页

    private void BtnCustomRefresh_Click(object sender, MouseButtonEventArgs e)
    {
        ModMain.frmLaunchRight.ForceRefresh();
        HintService.Hint(Lang.Text("Setup.Ui.Homepage.Refresh.Success"), HintType.Success);
    }

    private void BtnCustomTutorial_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://docs.pclc.cc/ce/customization/xaml-format");
    }

    // 主题
    private void ThemeColor_Change(object senderRaw, SelectionChangedEventArgs e)
    {
        var sender = (MyComboBox)senderRaw;
        var choices = ThemeChoices;
        if (sender.SelectedIndex < 0 || sender.SelectedIndex >= choices.Count) return;

        var choice = choices[sender.SelectedIndex];

        // 深浅色主题已合并：一次选择同时写浅色 / 深色两个槽位（含插件主题 id），
        // 内部先写插件 id 再写槽位，因为写槽位会立刻触发主题刷新事件，
        // 事件里（标题栏装饰）要能读到新的 id
        ThemeService.SetCurrentThemeSelection(choice.Theme, choice.PluginThemeId);

        // 槽位没变、只是换了一个插件主题 id 时，配置事件不会触发（值没变），这里手动补一次配色与装饰
        ThemeService.ApplyColorResources();
        ModMain.frmMain?.RefreshTitleSword();

        ThemeManager.ThemeRefresh();
        RefreshCustomColorPreview();
        // 琉璃彩锁定四层不透明度，相关滑条要跟着禁用
        RefreshMaterialLayout();
    }

    // 自定义主题色：校验通过后写回配置并刷新预览
    private void TextCustomColor_ValidatedTextChanged(object sender, RoutedEventArgs e)
    {
        RefreshCustomColorPreview();
        var normalized = ThemeService.NormalizeHexColor(TextCustomColor.Text);
        if (normalized is null) return;
        if (normalized == Config.Preference.Theme.CustomColor) return;
        Config.Preference.Theme.CustomColor = normalized;
        ModBase.Log($"[UI] 自定义主题色已更新为 {normalized}");
    }

    // 刷新自定义主题色预览色块
    private void RefreshCustomColorPreview()
    {
        if (RectCustomColor is null) return;
        if (ThemeService.TryParseHexColor(TextCustomColor?.Text, out var r, out var g, out var b))
            RectCustomColor.Background = new SolidColorBrush(Color.FromRgb(r, g, b));
        else
            RectCustomColor.SetResourceReference(Border.BackgroundProperty, "ColorBrush3");
    }

    // 赞助
    private void BtnLauncherDonate_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.OpenWebsite("https://afdian.com/a/LTCat");
    }

    #region 高级材质

    /// <summary>
    ///     不透明度滑条是 0~70，对应配置里的 30~100（下限 30%，否则界面会糊成一片）。
    /// </summary>
    private const int MaterialOpacitySliderOffset = 30;

    private void MaterialSubscribe()
    {
        FeatureFlags.Changed -= MaterialFeatureChanged;
        FeatureFlags.Changed += MaterialFeatureChanged;
        PluginHostBootstrap.Ready -= MaterialFeatureChanged;
        PluginHostBootstrap.Ready += MaterialFeatureChanged;
        RefreshMaterialCardVisibility();
        RefreshMaterialStatus();
    }

    private void MaterialUnsubscribe()
    {
        FeatureFlags.Changed -= MaterialFeatureChanged;
        PluginHostBootstrap.Ready -= MaterialFeatureChanged;
    }

    // 插件启用 / 禁用后功能开关会重置，这里同步卡片的显隐与状态文字
    private void MaterialFeatureChanged()
    {
        ModBase.RunInUi(() =>
        {
            try
            {
                // 扩展主题功能键可能刚被停用：下拉框要把非蓝色主题去掉，选中项回退到龙猫蓝
                RefreshThemeCombos();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Setup] 刷新主题下拉框失败", ModBase.LogLevel.Debug);
            }

            RefreshMaterialCardVisibility();
            RefreshMaterialStatus();
        });
    }

    private void ReloadMaterial()
    {
        if (CardMaterial is null) return;
        RefreshMaterialCardVisibility();
        ComboMaterialKind.SelectedIndex = Math.Clamp(Config.System.MaterialKind, 0, 3);
        SliderMaterialBlurRadius.Value = Math.Clamp(Config.System.MaterialBlurRadius, 0, 40);
        SliderMaterialTintStrength.Value = Math.Clamp(Config.System.MaterialTintStrength, 0, 100);
        SliderMaterialTitleOpacity.Value =
            Math.Clamp(Config.System.MaterialTitleOpacity, 30, 100) - MaterialOpacitySliderOffset;
        SliderMaterialMainOpacity.Value =
            Math.Clamp(Config.System.MaterialMainOpacity, 30, 100) - MaterialOpacitySliderOffset;
        SliderMaterialSidebarOpacity.Value =
            Math.Clamp(Config.System.MaterialSidebarOpacity, 30, 100) - MaterialOpacitySliderOffset;
        SliderMaterialCardOpacity.Value =
            Math.Clamp(Config.System.MaterialCardOpacity, 30, 100) - MaterialOpacitySliderOffset;
        SliderMaterialGlassCorner.Value = Math.Clamp(Config.System.MaterialGlassCornerRadius, 0, 24);
        SliderMaterialGlassTitleOpacity.Value = Math.Clamp(Config.System.MaterialGlassTitleOpacity, 0, 100);
        SliderMaterialGlassOpacity.Value =
            Math.Clamp(Config.System.MaterialGlassOpacity, 30, 100) - MaterialOpacitySliderOffset;
        SliderMaterialDialogOpacity.Value =
            Math.Clamp(Config.System.MaterialDialogOpacity, 30, 100) - MaterialOpacitySliderOffset;
        SliderMaterialToastOpacity.Value =
            Math.Clamp(Config.System.MaterialToastOpacity, 30, 100) - MaterialOpacitySliderOffset;
        SliderMaterialPillOpacity.Value =
            Math.Clamp(Config.System.MaterialPillOpacity, 30, 100) - MaterialOpacitySliderOffset;
        SliderMaterialDividerOpacity.Value = Math.Clamp(Config.System.MaterialDividerOpacity, 0, 100);
        SliderMaterialGlowRadius.Value = Math.Clamp(Config.System.MaterialGlowRadius, 0, 20);
        SliderMaterialGlowOpacity.Value = Math.Clamp(Config.System.MaterialGlowOpacity, 0, 100);
        RefreshMaterialLayout();
        RefreshMaterialStatus();
    }

    /// <summary>
    ///     按所选效果切换滑条分组：云母 / Aero 有标题栏、主界面、侧边栏不透明度；
    ///     液态玻璃没有这些（整块玻璃 + 竖线 + 小玻璃），改用大玻璃 / 侧边栏项 / 分割线三个参数。
    /// </summary>
    private void RefreshMaterialLayout()
    {
        if (PanMaterialOpaque is null || PanMaterialGlass is null) return;
        var isGlass = ComboMaterialKind.SelectedIndex == (int)PCL.Core.Utils.OS.Window.MaterialKind.LiquidGlass;
        PanMaterialOpaque.Visibility = isGlass ? Visibility.Collapsed : Visibility.Visible;
        // 「Windows 10 旧版磨砂」在 snapshot-72 起是**默认且唯一**的 Win10 实现（界面铺满窗口、磨砂与界面等大），
        // 不再由用户勾选，所以这一项直接隐藏，免得让人以为关掉还有别的磨砂可用
        PanMaterialGlass.Visibility = isGlass ? Visibility.Visible : Visibility.Collapsed;
        LabGlassHint.Visibility = isGlass ? Visibility.Visible : Visibility.Collapsed;

        // 「琉璃彩」主题把四层不透明度锁死 30%（用户定的设计，不是 bug），
        // 这里把对应滑条禁用并显示说明，免得用户以为滑块坏了
        var liuliLocked = ThemeService.CurrentTheme == ColorTheme.LiuliGlass;
        if (LabLiuliLocked is not null)
            LabLiuliLocked.Visibility = liuliLocked ? Visibility.Visible : Visibility.Collapsed;
        if (SliderMaterialTitleOpacity is not null)
        {
            SliderMaterialTitleOpacity.IsEnabled = !liuliLocked;
            SliderMaterialMainOpacity.IsEnabled = !liuliLocked;
            SliderMaterialSidebarOpacity.IsEnabled = !liuliLocked;
            SliderMaterialCardOpacity.IsEnabled = !liuliLocked;
        }

        CardMaterial.TriggerForceResize();
    }

    /// <summary>
    ///     重建浅色 / 深色主题下拉框的候选项，并在当前主题已不可选时回退到龙猫蓝。
    ///     <para>
    ///         候选项来自 ThemeColors，它依赖「扩展主题」功能键是否启用。
    ///         停用 SGU 插件包后如果只改 SelectedIndex 而不重新绑定 ItemsSource，
    ///         非蓝色主题的名字会继续留在下拉框里（用户实测 bug）。
    ///     </para>
    /// </summary>
    /// <summary>按下拉项内容找索引（插件主题比槽位枚举多一个 id 维度）。</summary>
    private static int IndexOfChoice(IReadOnlyList<ThemeChoice> choices, ColorTheme theme, string? pluginThemeId)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (choices[i].Theme != theme) continue;
            if (theme == ColorTheme.PluginDefined)
            {
                if (string.Equals(choices[i].PluginThemeId, pluginThemeId, StringComparison.OrdinalIgnoreCase))
                    return i;
                continue;
            }

            if (choices[i].PluginThemeId is null) return i;
        }

        return -1;
    }

    private void RefreshThemeCombos()
    {
        if (ComboThemeColor is null) return;

        var choices = ThemeChoices;
        var names = ThemeColors;

        ComboThemeColor.ItemsSource = names;
        ComboThemeColor.SelectedIndex = Math.Max(0, IndexOfChoice(
            choices, Config.Preference.Theme.LightColor, Config.Preference.Theme.LightPluginTheme));

        // 选不到了（插件被卸载等）：退回龙猫蓝，避免下拉框停在错误项。
        // 深浅色主题已合并，回退要同时写两个槽位（走 ThemeService 才不会只改一半）。
        if (IndexOfChoice(choices, Config.Preference.Theme.LightColor,
                Config.Preference.Theme.LightPluginTheme) < 0)
            ThemeService.SetCurrentThemeSelection(ColorTheme.CatBlue, null);
    }

    /// <summary>
    ///     高级材质由插件提供（<c>materials.advanced</c>）：没装插件时整张卡片不显示。
    /// </summary>
    private void RefreshMaterialCardVisibility()
    {
        if (CardMaterial is null) return;
        CardMaterial.Visibility = ModMaterial.IsAvailable ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshMaterialStatus()
    {
        if (LabMaterialStatus is null) return;
        if (!ModMaterial.IsAvailable)
        {
            LabMaterialStatus.Text = string.Empty;
            return;
        }

        var text = Lang.Text("Setup.Ui.Material.Status",
            MaterialKindName(ModMaterial.MaxSupported), MaterialKindName(ModMaterial.Applied));
        if (!string.IsNullOrWhiteSpace(ModMaterial.FallbackReason))
            text += Environment.NewLine + ModMaterial.FallbackReason;
        LabMaterialStatus.Text = text;
    }

    private static string MaterialKindName(PCL.Core.Utils.OS.Window.MaterialKind kind) => kind switch
    {
        PCL.Core.Utils.OS.Window.MaterialKind.Mica => Lang.Text("Setup.Ui.Material.Effect.Mica"),
        PCL.Core.Utils.OS.Window.MaterialKind.Aero => Lang.Text("Setup.Ui.Material.Effect.Aero"),
        PCL.Core.Utils.OS.Window.MaterialKind.LiquidGlass => Lang.Text("Setup.Ui.Material.Effect.Glass"),
        _ => Lang.Text("Setup.Ui.Material.Effect.None")
    };

    private void MaterialKind_Change(object senderRaw, SelectionChangedEventArgs e)
    {
        if (ModAnimation.AniExitsAnyControl()) return;
        var index = ComboMaterialKind.SelectedIndex;
        if (index < 0) return;
        SetByTag("UiMaterialKind", index);
        // 材质类型不即时切换：改类型要重启启动器才生效（运行时撤销系统磨砂在部分环境下不生效）
        if (index != (int)ModMaterial.Kind)
            HintService.Hint(Lang.Text("Setup.Ui.Material.RestartRequired"), HintType.Info);
        RefreshMaterialLayout();
        RefreshMaterialStatus();
    }

    private void MaterialSliderChange(object senderRaw, bool user)
    {
        if (ModAnimation.AniExitsAnyControl()) return;
        var slider = (MySlider)senderRaw;
        var tag = slider.Tag?.ToString();
        if (string.IsNullOrEmpty(tag)) return;
        var value = slider.Value;
        if (tag is "UiMaterialTitleOpacity" or "UiMaterialMainOpacity"
            or "UiMaterialSidebarOpacity" or "UiMaterialCardOpacity"
            or "UiMaterialGlassOpacity" or "UiMaterialPillOpacity"
            or "UiMaterialDialogOpacity" or "UiMaterialToastOpacity")
            value += MaterialOpacitySliderOffset;
        SetByTag(tag, value);
        RefreshMaterialStatus();
    }

    #endregion

    // 滑动条
    private void SliderLoad()
    {
        SliderMusicVolume.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Ceiling(Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderLauncherOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Round(40 + Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderBackgroundOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Math.Round(Convert.ToDouble(v) * 0.1d) / 100d, "P0"));
        SliderBackgroundBlur.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderBlurValue.getHintText = new Func<object, object>(v => Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderBlurSamplingRate.getHintText = new Func<object, object>(v => Lang.Number(Convert.ToDouble(v) / 100d, "P0"));
        // 高级材质
        SliderMaterialBlurRadius.getHintText = new Func<object, object>(v =>
            Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderMaterialTintStrength.getHintText = new Func<object, object>(v =>
            Lang.Number(Convert.ToDouble(v) / 100d, "P0"));
        SliderMaterialTitleOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number((Convert.ToDouble(v) + MaterialOpacitySliderOffset) / 100d, "P0"));
        SliderMaterialMainOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number((Convert.ToDouble(v) + MaterialOpacitySliderOffset) / 100d, "P0"));
        SliderMaterialSidebarOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number((Convert.ToDouble(v) + MaterialOpacitySliderOffset) / 100d, "P0"));
        SliderMaterialCardOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number((Convert.ToDouble(v) + MaterialOpacitySliderOffset) / 100d, "P0"));
        SliderMaterialGlassOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number((Convert.ToDouble(v) + MaterialOpacitySliderOffset) / 100d, "P0"));
        SliderMaterialDialogOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number((Convert.ToDouble(v) + MaterialOpacitySliderOffset) / 100d, "P0"));
        SliderMaterialToastOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number((Convert.ToDouble(v) + MaterialOpacitySliderOffset) / 100d, "P0"));
        SliderMaterialPillOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number((Convert.ToDouble(v) + MaterialOpacitySliderOffset) / 100d, "P0"));
        SliderMaterialDividerOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Convert.ToDouble(v) / 100d, "P0"));
        SliderWindowCornerRadius.getHintText = new Func<object, object>(v =>
            Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderMaterialGlassCorner.getHintText = new Func<object, object>(v =>
            Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderMaterialGlowRadius.getHintText = new Func<object, object>(v =>
            Lang.Text("Setup.Ui.Slider.Pixel", Lang.Number(Convert.ToDouble(v), "N0")));
        SliderMaterialGlowOpacity.getHintText = new Func<object, object>(v =>
            Lang.Number(Convert.ToDouble(v) / 100d, "P0"));
    }

    private void CheckMusicStart_OnChange(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckMusicStart_Change(sender, user);
    }

    private void CheckMusicStop_OnChange(object sender, bool user)
    {
        CheckBoxChange(sender, user);
        CheckMusicStop_Change();
    }

    #region 功能隐藏

    /// <summary>
    ///     是否强制显示被禁用的功能。
    /// </summary>
    public static bool HiddenForceShow
    {
        get => field;
        set
        {
            field = value;
            HiddenRefresh();
        }
    }

    /// <summary>
    ///     更新功能隐藏带来的显示变化。
    /// </summary>
    public static void HiddenRefresh()
    {
        if (ModMain.frmMain.PanTitleSelect is null || !ModMain.frmMain.PanTitleSelect.IsLoaded)
            return;
        try
        {
            // 获取配置组引用以缩短代码
            var conf = Config.Preference.Hide;

            // 顶部栏：下载、设置、工具
            var isAllTitleHidden = !HiddenForceShow && conf.PageDownload && conf.PageSetup && conf.PageTools;

            if (isAllTitleHidden)
            {
                ModMain.frmMain.PanTitleSelect.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModMain.frmMain.PanTitleSelect.Visibility = Visibility.Visible;
                ModMain.frmMain.BtnTitleSelect1.Visibility = !HiddenForceShow && conf.PageDownload
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmMain.BtnTitleSelect2.Visibility =
                    !HiddenForceShow && conf.PageSetup ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmMain.BtnTitleSelect3.Visibility =
                    !HiddenForceShow && conf.PageTools ? Visibility.Collapsed : Visibility.Visible;
            }

            // 功能隐藏设置卡片
            if (ModMain.frmSetupUI is not null)
            {
                ModMain.frmSetupUI.CardSwitch.Visibility = !HiddenForceShow && conf.FunctionHidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupUI.CardSwitch.Title = HiddenForceShow ? Lang.Text("Setup.Ui.FeatureHide.TitleTemporarilyDisabled") : Lang.Text("Setup.Ui.FeatureHide.Title");
            }

            // 设置子页面 (FrmSetupLeft)
            if (ModMain.frmSetupLeft is not null)
            {
                ModMain.frmSetupLeft.ItemLaunch.Visibility =
                    !HiddenForceShow && conf.SetupLaunch ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemUI.Visibility =
                    !HiddenForceShow && conf.SetupUi ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemLauncherLanguage.Visibility = !HiddenForceShow && conf.SetupLauncherLanguage
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemGameManage.Visibility = !HiddenForceShow && conf.SetupGameManage
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemLauncherMisc.Visibility = !HiddenForceShow && conf.SetupLauncherMisc
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemJava.Visibility =
                    !HiddenForceShow && conf.SetupJava ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemUpdate.Visibility =
                    !HiddenForceShow && conf.SetupUpdate ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemAbout.Visibility =
                    !HiddenForceShow && conf.SetupAbout ? Visibility.Collapsed : Visibility.Visible;
                ModMain.frmSetupLeft.ItemFeedback.Visibility = !HiddenForceShow && conf.SetupFeedback
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmSetupLeft.ItemLog.Visibility =
                    !HiddenForceShow && conf.SetupLog ? Visibility.Collapsed : Visibility.Visible;

                var categories = new[]
                {
                    (ModMain.frmSetupLeft.TextGameCategory,
                        !(conf.SetupLaunch && conf.SetupJava && conf.SetupGameManage)),
                    (ModMain.frmSetupLeft.TextToolsCategory,
                        false), // 该分类下已无任何条目（联机页已移到「工具」页），标题必须隐藏
                    (ModMain.frmSetupLeft.TextLauncherCategory, !(conf.SetupUi && conf.SetupLauncherLanguage && conf.SetupLauncherMisc)),
                    (ModMain.frmSetupLeft.TextAboutCategory,
                        !(conf.SetupAbout && conf.SetupUpdate && conf.SetupFeedback && conf.SetupLog))
                };

                foreach (var category in categories)
                {
                    var isVisible = category.Item2 || HiddenForceShow;
                    category.Item1.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                    if (isVisible)
                        category.Item1.Opacity = 0.6d;
                }

                // 统计设置页可用项数量
                var setupCount = 0;
                if (!conf.SetupLaunch)
                    setupCount += 1;
                if (!conf.SetupUi)
                    setupCount += 1;
                if (!conf.SetupLauncherLanguage)
                    setupCount += 1;
                if (!conf.SetupGameManage)
                    setupCount += 1;
                if (!conf.SetupLauncherMisc)
                    setupCount += 1;
                if (!conf.SetupJava)
                    setupCount += 1;
                if (!conf.SetupUpdate)
                    setupCount += 1;
                if (!conf.SetupAbout)
                    setupCount += 1;
                if (!conf.SetupFeedback)
                    setupCount += 1;
                if (!conf.SetupLog)
                    setupCount += 1;
                ModMain.frmSetupLeft.PanItem.Visibility =
                    setupCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
            }

            // 工具子页面 (FrmToolsLeft)
            if (ModMain.frmToolsLeft is not null)
            {
                ModMain.frmToolsLeft.ItemGameLink.Visibility = !HiddenForceShow && conf.ToolsGameLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                ModMain.frmToolsLeft.ItemTest.Visibility =
                    !HiddenForceShow && conf.ToolsTest ? Visibility.Collapsed : Visibility.Visible;
                
                // 处理分类标题
                var isGameLinkVisible = (!HiddenForceShow && !conf.ToolsGameLink) || HiddenForceShow;
                ModMain.frmToolsLeft.TextGameLinkCategory.Visibility = isGameLinkVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isGameLinkVisible) ModMain.frmToolsLeft.TextGameLinkCategory.Opacity = 0.6;

                var isToolsVisible = (!HiddenForceShow && !conf.ToolsTest) || HiddenForceShow;
                ModMain.frmToolsLeft.TextToolsCategory.Visibility = isToolsVisible ? Visibility.Visible : Visibility.Collapsed;
                if (isToolsVisible) ModMain.frmToolsLeft.TextToolsCategory.Opacity = 0.6;
                
                // 统计工具页可用项数量
                var toolsCount = 0;
                if (!conf.ToolsGameLink)
                    toolsCount += 1;
                if (!conf.ToolsTest)
                    toolsCount += 1;
                ModMain.frmToolsLeft.PanItem.Visibility =
                    toolsCount < 2 && !HiddenForceShow ? Visibility.Collapsed : Visibility.Visible;
            }

            // 其他入口刷新
            if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSelect)
                ModMain.frmSelectRight.BtnEmptyDownload_Loaded();
            if (ModMain.frmMain.pageCurrent == FormMain.PageType.Launch)
                ModMain.frmLaunchLeft.RefreshButtonsUI();
            if (ModMain.frmMain.pageCurrent == FormMain.PageType.InstanceSetup &&
                ModMain.frmInstanceModDisabled is not null)
                ModMain.frmInstanceModDisabled.BtnDownload_Loaded();
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "刷新功能隐藏项目失败",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Ui.Error.OperationFailed"));
        }
    }

    // ================= 设置页面协同 =================
    private void HiddenSetupMain()
    {
        var isChecked = (bool)CheckHiddenPageSetup.Checked;
        CheckHiddenSetupLaunch.Checked = isChecked;
        CheckHiddenSetupUI.Checked = isChecked;
        CheckHiddenSetupLauncherLanguage.Checked = isChecked;
        CheckHiddenSetupGameManage.Checked = isChecked;
        CheckHiddenLauncherMisc.Checked = isChecked;
        CheckHiddenSetupJava.Checked = isChecked;
        CheckHiddenSetupUpdate.Checked = isChecked;
        CheckHiddenSetupGameLink.Checked = isChecked;
        CheckHiddenSetupAbout.Checked = isChecked;
        CheckHiddenSetupFeedback.Checked = isChecked;
        CheckHiddenSetupLog.Checked = isChecked;
    }

    // ================= 设置页面协同 =================
    private void HiddenSetupMain(object sender, bool user)
    {
        if (!user)
            return; // 仅处理用户点击，防止死循环
        var isChecked = (bool)CheckHiddenPageSetup.Checked;
        CheckHiddenSetupLaunch.Checked = isChecked;
        CheckHiddenSetupUI.Checked = isChecked;
        CheckHiddenSetupLauncherLanguage.Checked = isChecked;
        CheckHiddenSetupGameManage.Checked = isChecked;
        CheckHiddenLauncherMisc.Checked = isChecked;
        CheckHiddenSetupJava.Checked = isChecked;
        CheckHiddenSetupUpdate.Checked = isChecked;
        CheckHiddenSetupGameLink.Checked = isChecked;
        CheckHiddenSetupAbout.Checked = isChecked;
        CheckHiddenSetupFeedback.Checked = isChecked;
        CheckHiddenSetupLog.Checked = isChecked;
    }

    private void HiddenSetupSub(object sender, bool user)
    {
        if (!user)
            return;
        var conf = Config.Preference.Hide;
        // 判断是否全部勾选
        var allChecked = conf.SetupLaunch && conf.SetupUi && conf.SetupLauncherLanguage && conf.SetupJava &&
                         conf.SetupUpdate && conf.SetupGameLink && conf.SetupAbout && conf.SetupFeedback &&
                         conf.SetupLog && conf.SetupLauncherMisc && conf.SetupGameManage;
        CheckHiddenPageSetup.Checked = allChecked;
    }

    // ================= 工具页面协同 =================
    private void HiddenToolsMain(object sender, bool user)
    {
        if (!user)
            return;
        var isChecked = (bool)CheckHiddenPageTools.Checked;
        CheckHiddenToolsGameLink.Checked = isChecked;
        CheckHiddenToolsTest.Checked = isChecked;
    }

    private void HiddenToolsSub(object sender, bool user)
    {
        if (!user)
            return;
        var conf = Config.Preference.Hide;
        var allChecked = conf.ToolsGameLink && conf.ToolsTest;
        CheckHiddenPageTools.Checked = allChecked;
    }

    // 警告提示
    private void HiddenHint(object sender, bool user)
    {
        if (!ModAnimation.AniExitsAnyControl() && sender is MyCheckBox checkBox && checkBox.Checked == true)
            HintService.Hint(Lang.Text("Setup.Ui.FeatureHide.TemporaryHint"));
    }

    #endregion
}
