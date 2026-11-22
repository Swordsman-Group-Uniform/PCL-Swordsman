using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AdvancedInstance.Features;
using PCL.PluginSdk;

namespace AdvancedInstance.Ui;

/// <summary>
///     「高级实例管理」主页面：四块功能各一张卡片。
///     宿主已经把内容放进自己的滚动容器里，所以这里只返回一个 <see cref="StackPanel" />。
/// </summary>
internal static class MainPage
{
    public static FrameworkElement Build(IPluginContext context, SaveBackupService backup,
        Func<(string Name, string Path)?> currentInstance)
    {
        var root = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

        root.Children.Add(BuildOverview(context, currentInstance));
        root.Children.Add(BuildBackupCard(context, backup, currentInstance));
        root.Children.Add(BuildModCard(context, currentInstance));
        root.Children.Add(BuildLevelDatCard(context, currentInstance));
        root.Children.Add(BuildLitematicCard(context, currentInstance));

        return root;
    }

    #region 概览

    private static Border BuildOverview(IPluginContext context, Func<(string Name, string Path)?> currentInstance)
    {
        var instanceText = UiKit.Text("");
        var status = new UiKit.StatusLine();

        void RefreshInstance()
        {
            var instance = currentInstance();
            if (instance is null)
            {
                instanceText.Text = "当前没有选中的实例（请先在启动页选中一个实例）。";
                status.Warn("没有实例时，除了「投影版本适配」的手动选目录，其它功能都不可用。");
                return;
            }

            var saves = Path.Combine(instance.Value.Path, "saves");
            var mods = Path.Combine(instance.Value.Path, "mods");
            var saveCount = Directory.Exists(saves) ? Directory.GetDirectories(saves).Length : 0;
            var modCount = Directory.Exists(mods) ? Directory.GetFiles(mods, "*.jar").Length : 0;
            instanceText.Text = $"当前实例：{instance.Value.Name}\n{saveCount} 个存档 · {modCount} 个 Mod";
            status.Ok("实例已就绪。");
        }

        RefreshInstance();

        var refresh = UiKit.Button("刷新实例信息", RefreshInstance);

        var card = UiKit.Card("高级实例管理",
            UiKit.Muted("这个插件提供四样东西：存档自动备份、Mod 依赖关系与冲突检查、" +
                        "存档基本数据（level.dat）编辑、Litematica 投影适配版本批量修改。"),
            instanceText,
            UiKit.ButtonRow(refresh),
            status.Element);

        return card;
    }

    #endregion

    #region ① 存档自动备份

    private static Border BuildBackupCard(IPluginContext context, SaveBackupService backup,
        Func<(string Name, string Path)?> currentInstance)
    {
        var status = new UiKit.StatusLine();

        var enabled = UiKit.Check("启用存档自动备份", backup.Enabled);
        var interval = UiKit.NumberBox(backup.IntervalMinutes, 64d);
        var keep = UiKit.NumberBox(backup.KeepPerWorld, 64d);
        var orphanDays = UiKit.NumberBox(backup.OrphanKeepDays, 64d);
        var listHost = new StackPanel();

        void SaveSettings()
        {
            backup.Enabled = enabled.IsChecked == true;
            backup.IntervalMinutes = ParseInt(interval.Text, backup.IntervalMinutes, 1, 1440);
            backup.KeepPerWorld = ParseInt(keep.Text, backup.KeepPerWorld, 1, 200);
            backup.OrphanKeepDays = ParseInt(orphanDays.Text, backup.OrphanKeepDays, 1, 3650);
            interval.Text = backup.IntervalMinutes.ToString();
            keep.Text = backup.KeepPerWorld.ToString();
            orphanDays.Text = backup.OrphanKeepDays.ToString();
            status.Ok("备份设置已保存。");
        }

        void RefreshList()
        {
            listHost.Children.Clear();
            var instance = currentInstance();
            if (instance is null)
            {
                listHost.Children.Add(UiKit.Muted("（没有选中的实例）"));
                return;
            }

            var savesPath = Path.Combine(instance.Value.Path, "saves");
            var backups = backup.ListBackups(instance.Value.Name);
            if (backups.Count == 0)
            {
                listHost.Children.Add(UiKit.Muted("还没有任何备份。点「立即备份一次」或等自动备份到点。"));
                return;
            }

            foreach (var (world, count, newest, newestPath) in backups)
            {
                var exists = Directory.Exists(Path.Combine(savesPath, world));
                var line = new WrapPanel { Margin = new Thickness(0, 2, 0, 2) };
                line.Children.Add(new TextBlock
                {
                    Text = $"{world} · {count} 份 · 最近 {newest:yyyy-MM-dd HH:mm}" + (exists ? "" : "（存档已删除，只留最后一份）"),
                    FontFamily = UiKit.UiFont,
                    FontSize = 12d,
                    Foreground = exists ? UiKit.Foreground : UiKit.MutedForeground,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                });

                var restore = UiKit.Button("恢复最近一份", () =>
                {
                    var answer = MessageBox.Show(
                        $"把「{world}」还原成 {newest:yyyy-MM-dd HH:mm} 的备份？\n" +
                        "现有同名存档会被改名成 <存档名>.old-时间，不会直接丢掉。",
                        "恢复存档", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (answer != MessageBoxResult.Yes) return;

                    if (backup.Restore(instance.Value.Path, instance.Value.Name, world, newestPath, out var error))
                        status.Ok($"已还原「{world}」，刷新实例列表即可看到。");
                    else
                        status.Error($"还原失败：{error}");
                    RefreshList();
                });
                restore.MinWidth = 0d;
                restore.Padding = new Thickness(9, 3, 9, 3);
                restore.FontSize = 12d;
                restore.Margin = new Thickness(0);
                line.Children.Add(restore);
                listHost.Children.Add(line);
            }
        }

        RefreshList();

        var backupNow = UiKit.Button("立即备份一次", () =>
        {
            var instance = currentInstance();
            if (instance is null)
            {
                status.Warn("请先选中一个实例。");
                return;
            }

            SaveSettings();
            var count = backup.BackupAll(instance.Value.Name, instance.Value.Path, out var error);
            if (error is null) status.Ok($"已备份 {count} 个存档。");
            else status.Error($"备份失败：{error}");
            RefreshList();
        }, true);

        var openFolder = UiKit.Button("打开备份目录", () =>
        {
            Directory.CreateDirectory(backup.BackupRoot);
            Process.Start(new ProcessStartInfo
            {
                FileName = backup.BackupRoot,
                UseShellExecute = true
            });
        });

        var saveButton = UiKit.Button("保存设置", SaveSettings);
        var refreshButton = UiKit.Button("刷新列表", RefreshList);

        return UiKit.Card("存档自动备份",
            UiKit.Muted($"默认每 30 分钟备份一次、每个存档保留最近 10 份；存档被删除后只保留最后一份，" +
                        $"超过 {backup.OrphanKeepDays} 天连它一起清理。备份放在插件数据目录里，不会污染实例文件夹。"),
            enabled,
            UiKit.Row("备份间隔（分钟）", interval),
            UiKit.Row("每个存档保留份数", keep),
            UiKit.Row("删除存档后保留天数", orphanDays),
            UiKit.ButtonRow(saveButton, backupNow, openFolder, refreshButton),
            status.Element,
            UiKit.Separator(),
            UiKit.Muted("备份列表（点「恢复最近一份」会先把现有存档改名，再解压还原）"),
            listHost);
    }

    #endregion

    #region ② Mod 依赖关系

    private static Border BuildModCard(IPluginContext context, Func<(string Name, string Path)?> currentInstance)
    {
        var status = new UiKit.StatusLine();
        var problemHost = new TextBlock
        {
            FontFamily = UiKit.UiFont,
            FontSize = 12d,
            Foreground = UiKit.Foreground,
            TextWrapping = TextWrapping.Wrap
        };
        var detailHost = new TextBlock
        {
            FontFamily = UiKit.UiFont,
            FontSize = 12d,
            Foreground = UiKit.MutedForeground,
            TextWrapping = TextWrapping.Wrap
        };

        void Refresh(bool force)
        {
            var instance = currentInstance();
            if (instance is null)
            {
                status.Warn("请先选中一个实例。");
                problemHost.Text = "";
                detailHost.Text = "";
                return;
            }

            var modsPath = Path.Combine(instance.Value.Path, "mods");
            var result = ModDependencyAnalyzer.Analyze(modsPath, force);
            status.Info(result.Summary);

            if (!result.HasProblems)
            {
                status.Ok(result.Summary);
                problemHost.Text = "";
            }
            else
            {
                status.Warn(result.Summary);
                problemHost.Text = string.Join("\n", result.Problems.Select(problem =>
                    (problem.Severity == "error" ? "⛔ " : "⚠ ") + problem.Text));
            }

            detailHost.Text = result.Mods.Count == 0
                ? "（这个实例没有可解析的 Mod：可能没装 Mod 加载器，或者 mods 目录是空的）"
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

        var again = UiKit.Button("重新检查", () => Refresh(true), true);
        var openMods = UiKit.Button("打开 Mod 文件夹", () =>
        {
            var instance = currentInstance();
            if (instance is null) return;
            var modsPath = Path.Combine(instance.Value.Path, "mods");
            if (!Directory.Exists(modsPath)) Directory.CreateDirectory(modsPath);
            Process.Start(new ProcessStartInfo { FileName = modsPath, UseShellExecute = true });
        });

        return UiKit.Card("Mod 依赖关系与冲突检查",
            UiKit.Muted("读每个 Mod 的加载器清单（fabric.mod.json / quilt.mod.json / mods.toml），" +
                        "检查依赖缺失、依赖版本不满足、重复安装与明确声明的不兼容。" +
                        "启动前会再检查一次，有问题会弹窗让你决定要不要继续。" +
                        "完整的依赖一览也挂在「实例 → Mod 管理」页。"),
            UiKit.ButtonRow(again, openMods),
            status.Element,
            problemHost,
            UiKit.Separator(),
            UiKit.Muted("依赖一览"),
            UiKit.ScrollableContent(detailHost, 170d));
    }

    #endregion

    #region ③ 存档基本数据（level.dat）

    private static Border BuildLevelDatCard(IPluginContext context, Func<(string Name, string Path)?> currentInstance)
    {
        var status = new UiKit.StatusLine();
        var worldCombo = UiKit.Combo([]);
        var levelName = UiKit.TextBox("", 260d);
        var gameType = UiKit.Combo(["生存", "创造", "冒险", "旁观"]);
        var difficulty = UiKit.Combo(["和平", "简单", "普通", "困难"]);
        var hardcore = UiKit.Check("硬核模式", false);
        var allowCommands = UiKit.Check("允许作弊（allowCommands）", false);
        var weather = UiKit.Combo(["晴天", "下雨", "雷雨"]);
        var dayTime = UiKit.NumberBox(0, 120d);
        var spawnX = UiKit.NumberBox(0);
        var spawnY = UiKit.NumberBox(64);
        var spawnZ = UiKit.NumberBox(0);

        LevelDatInfo? current = null;

        void LoadWorlds(int selectIndex = 0)
        {
            worldCombo.Items.Clear();
            var instance = currentInstance();
            if (instance is null)
            {
                status.Warn("请先选中一个实例。");
                return;
            }

            var saves = Path.Combine(instance.Value.Path, "saves");
            if (!Directory.Exists(saves))
            {
                status.Warn("这个实例还没有存档。");
                return;
            }

            foreach (var world in Directory.EnumerateDirectories(saves))
                worldCombo.Items.Add(Path.GetFileName(world));
            if (worldCombo.Items.Count == 0)
            {
                status.Warn("这个实例还没有存档。");
                return;
            }

            worldCombo.SelectedIndex = Math.Clamp(selectIndex, 0, worldCombo.Items.Count - 1);
            LoadCurrentFromCombo();
        }

        void LoadCurrentFromCombo()
        {
            var instance = currentInstance();
            if (instance is null || worldCombo.SelectedItem is not string worldName) return;

            var worldPath = Path.Combine(instance.Value.Path, "saves", worldName);
            current = LevelDatEditor.Load(worldPath);
            if (current is null)
            {
                status.Error("读不出这个存档的 level.dat。");
                return;
            }

            levelName.Text = current.LevelName;
            gameType.SelectedIndex = Math.Clamp(current.GameType, 0, 3);
            difficulty.SelectedIndex = Math.Clamp(current.Difficulty, 0, 3);
            hardcore.IsChecked = current.Hardcore;
            allowCommands.IsChecked = current.AllowCommands;
            weather.SelectedIndex = current.Thundering ? 2 : current.Raining ? 1 : 0;
            dayTime.Text = current.DayTime.ToString();
            spawnX.Text = current.SpawnX.ToString();
            spawnY.Text = current.SpawnY.ToString();
            spawnZ.Text = current.SpawnZ.ToString();

            status.Ok($"已读取「{current.LevelName}」" +
                      (string.IsNullOrWhiteSpace(current.VersionName) ? "" : $"（存档版本 {current.VersionName}）"));
        }

        worldCombo.SelectionChanged += (_, _) => LoadCurrentFromCombo();

        var save = UiKit.Button("保存修改", () =>
        {
            if (current is null)
            {
                status.Warn("请先选择并读取一个存档。");
                return;
            }

            current.LevelName = levelName.Text.Trim();
            current.GameType = gameType.SelectedIndex;
            current.Difficulty = difficulty.SelectedIndex;
            current.Hardcore = hardcore.IsChecked == true;
            current.AllowCommands = allowCommands.IsChecked == true;
            current.Raining = weather.SelectedIndex >= 1;
            current.Thundering = weather.SelectedIndex == 2;
            current.DayTime = ParseLong(dayTime.Text, current.DayTime);
            current.SpawnX = ParseInt(spawnX.Text, current.SpawnX, -30000000, 30000000);
            current.SpawnY = ParseInt(spawnY.Text, current.SpawnY, -64, 320);
            current.SpawnZ = ParseInt(spawnZ.Text, current.SpawnZ, -30000000, 30000000);

            var answer = MessageBox.Show(
                "确定要把这些改动写回 level.dat 吗？\n" +
                "写之前会自动把原文件备份成 level.dat.bak-时间。\n" +
                "游戏正在运行时改动可能不生效，建议先退出世界。",
                "保存存档数据", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            if (LevelDatEditor.Save(current, out var backupPath, out var error))
                status.Ok($"已保存。" + (backupPath is null ? "" : $"原文件备份：{Path.GetFileName(backupPath)}"));
            else
                status.Error($"保存失败：{error}");
        }, true);

        var reload = UiKit.Button("重新读取", LoadCurrentFromCombo);
        var refreshWorlds = UiKit.Button("刷新存档列表", () => LoadWorlds());

        LoadWorlds();

        return UiKit.Card("存档基本数据（level.dat）",
            UiKit.Muted("直接改存档的 level.dat：名字、游戏模式、难度、硬核、作弊、天气、时间和出生点。" +
                        "其它标签原样保留，写之前自动备份原文件。"),
            UiKit.Row("选择存档", worldCombo),
            UiKit.Row("存档名字", levelName),
            UiKit.Row("游戏模式", gameType),
            UiKit.Row("难度", difficulty),
            hardcore,
            allowCommands,
            UiKit.Row("天气", weather),
            UiKit.Row("世界时间（tick）", dayTime),
            UiKit.Row("出生点 X / Y / Z", BuildTriple(spawnX, spawnY, spawnZ)),
            UiKit.ButtonRow(save, reload, refreshWorlds),
            status.Element);
    }

    private static UIElement BuildTriple(params UIElement[] controls)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var control in controls)
        {
            control.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 6, 0));
            panel.Children.Add(control);
        }

        return panel;
    }

    #endregion

    #region ④ 投影版本适配

    private static Border BuildLitematicCard(IPluginContext context, Func<(string Name, string Path)?> currentInstance)
    {
        var status = new UiKit.StatusLine();
        var presetItems = LitematicVersion.Presets
            .Select(preset => $"{preset.Label} → Version {preset.Version}")
            .Append("自定义…")
            .ToList();
        var presetCombo = UiKit.Combo(presetItems, 1);
        var backupBeforeChange = UiKit.Check("改动前为每个投影创建 .bak-时间 备份",
            context.Config.Get("litematic.backupBeforeChange", true));
        var custom = UiKit.NumberBox(6, 80d);
        var targetDataVersion = UiKit.NumberBox(0, 100d);
        var syncDataVersion = UiKit.Check("同时修改 MinecraftDataVersion（方块 ID 兼容性由它决定）", false);
        var recursive = UiKit.Check("包含子文件夹", true);
        var folderBox = UiKit.TextBox("", 380d);
        var resultText = new TextBlock
        {
            FontFamily = UiKit.UiFont,
            FontSize = 12d,
            Foreground = UiKit.MutedForeground,
            TextWrapping = TextWrapping.Wrap
        };

        void GuessFolder()
        {
            var instance = currentInstance();
            folderBox.Text = instance is null
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft")
                : Path.Combine(instance.Value.Path, "schematics");
        }

        GuessFolder();

        // 预设下拉与「自定义」互斥：选了预设就用预设的值，选最后一项「自定义」才读输入框
        presetCombo.SelectionChanged += (_, _) =>
        {
            var index = presetCombo.SelectedIndex;
            if (index >= 0 && index < LitematicVersion.Presets.Length)
                custom.Text = LitematicVersion.Presets[index].Version.ToString();
        };

        var run = UiKit.Button("开始批量修改", () =>
        {
            // 首次使用：先弹风险提示（勾选框状态写回配置）
            if (!context.Config.Get("litematic.warningAccepted", false))
            {
                var accepted = RiskDialog.Show(out var backupChecked);
                if (!accepted)
                {
                    status.Info("已取消，没有改动任何文件。");
                    return;
                }

                context.Config.Set("litematic.warningAccepted", true);
                context.Config.Set("litematic.backupBeforeChange", backupChecked);
                context.Config.Save();
                backupBeforeChange.IsChecked = backupChecked;
            }

            var target = ParseInt(custom.Text, 6, 0, 100000);
            int? dataVersion = syncDataVersion.IsChecked == true
                ? ParseInt(targetDataVersion.Text, 0, 0, 1000000)
                : null;

            var folder = folderBox.Text.Trim();
            if (!Directory.Exists(folder))
            {
                status.Error("这个文件夹不存在：" + folder);
                return;
            }

            var files = LitematicVersion.EnumerateFiles(folder, recursive.IsChecked == true);
            if (files.Count == 0)
            {
                status.Warn("这个文件夹里没有 .litematic 投影文件。");
                return;
            }

            var backupBefore = backupBeforeChange.IsChecked == true;
            context.Config.Set("litematic.backupBeforeChange", backupBefore);
            var answer = MessageBox.Show(
                $"找到 {files.Count} 个投影文件，准备把适配版本改成 {target}" +
                (dataVersion is null ? "。" : $"，MinecraftDataVersion 改成 {dataVersion}。") + "\n" +
                (backupBefore ? "改动前会为每个文件创建 .bak-时间 备份。" : "当前设置**不**创建备份，请确认你知道风险。") + "\n\n继续吗？",
                "投影版本适配", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;

            // 文件多的时候不要卡住界面：直接跑（每个文件只有几百 KB，很快）
            var result = LitematicVersion.Apply(files, target, dataVersion, backupBefore,
                message => context.Log.Warn(message));

            resultText.Text = result.Summary +
                              (result.Errors.Count == 0 ? "" : "\n\n失败明细：\n" + string.Join("\n", result.Errors.Take(20)));
            if (result.Failed == 0) status.Ok(result.Summary);
            else status.Warn(result.Summary);
        }, true);

        var pickFolder = UiKit.Button("选择文件夹…", () =>
        {
            var picked = context.Files.PickFolder();
            if (!string.IsNullOrWhiteSpace(picked)) folderBox.Text = picked;
        });

        var guess = UiKit.Button("用当前实例的 schematics 目录", GuessFolder);

        var syncCheck = syncDataVersion;
        syncCheck.Checked += (_, _) => targetDataVersion.IsEnabled = true;
        targetDataVersion.IsEnabled = false;
        syncCheck.Unchecked += (_, _) => targetDataVersion.IsEnabled = false;

        return UiKit.Card("Litematica 投影版本适配",
            UiKit.Muted("适配版本就是 .litematic 文件里的 Version 字段（平时要用 NBTExplorer 才能改）。" +
                        "这里可以一次改一个文件夹里的所有投影。版本对应关系：" +
                        "1.12.2 及更早 = 5，1.13 及以后 = 6（遇到特殊情况用「自定义」直接填数字）。"),
            UiKit.Row("目标版本", presetCombo),
            UiKit.Row("Version 数值", custom),
            syncCheck,
            UiKit.Row("MinecraftDataVersion", targetDataVersion),
            UiKit.Row("投影文件夹", folderBox),
            recursive,
            backupBeforeChange,
            UiKit.ButtonRow(run, pickFolder, guess),
            status.Element,
            resultText);
    }

    #endregion

    #region 小工具

    private static int ParseInt(string text, int fallback, int min, int max) =>
        int.TryParse(text.Trim(), out var value) ? Math.Clamp(value, min, max) : fallback;

    private static long ParseLong(string text, long fallback) =>
        long.TryParse(text.Trim(), out var value) ? Math.Max(0, value) : fallback;

    #endregion
}
