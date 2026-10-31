using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS;

/// <summary>
///     内存优化（百宝箱 → 内存优化，以及启动游戏前的自动内存优化）。<br/>
///     请注意这与「清理垃圾」是完全不同的两件事：本功能<b>不删除任何文件</b>，
///     只通过系统 API 回收物理内存 —— 清空系统备用内存列表（standby list）并裁剪各进程的工作集，
///     通常可以让物理内存占用下降三分之一左右，而且效果不仅限于 Minecraft。<br/>
///     清空备用内存列表需要管理员权限（SeProfileSingleProcessPrivilege），
///     权限不足时会退化为只裁剪工作集，仍有一定效果。
/// </summary>
public static partial class MemoryOptimizer
{
    /// <summary>
    ///     是否支持内存优化（当前仅 Windows 提供所需 API）。
    /// </summary>
    public static bool IsSupported => OperatingSystem.IsWindows();

    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x0002;

    /// <summary>
    ///     <c>SystemMemoryListInformation</c> 的信息类别编号。
    /// </summary>
    private const int SystemMemoryListInformation = 0x50;

    /// <summary>
    ///     不同资料对 <c>MEMORY_LIST_COMMAND</c> 的编号（0 基 / 1 基）存在分歧，
    ///     因此「清空备用列表」「清空工作集」都准备两种可能编号，成功即止。
    /// </summary>
    private static readonly int[] _PurgeStandbyCommands = [4, 5];

    private static readonly int[] _EmptyWorkingSetCommands = [3, 2];

    private static readonly string[] _Privileges =
    [
        "SeIncreaseQuotaPrivilege",
        "SeProfileSingleProcessPrivilege",
        "SeDebugPrivilege"
    ];

    /// <summary>
    ///     执行一次内存优化。
    /// </summary>
    /// <returns>本次释放的物理内存字节数（可能为 0）。</returns>
    public static long Optimize()
    {
        if (!IsSupported) return 0;

        var before = KernelInterop.GetPhysicalMemoryBytes();
        LogWrapper.Info("MemoryOptimize",
            $"内存优化开始：当前可用物理内存 {_ToMegabytes((long)before.Available)} MB / 共 {_ToMegabytes((long)before.Total)} MB。");

        _EnablePrivileges();

        var purged = false;
        foreach (var command in _PurgeStandbyCommands)
            if (_TrySetMemoryListCommand(command))
            {
                purged = true;
                break;
            }

        foreach (var command in _EmptyWorkingSetCommands)
            if (_TrySetMemoryListCommand(command))
                break;

        var trimmed = _TrimProcessWorkingSets();

        var after = KernelInterop.GetPhysicalMemoryBytes();
        var freed = (long)after.Available - (long)before.Available;
        LogWrapper.Info("MemoryOptimize",
            $"内存优化完成：释放 {_ToMegabytes(Math.Max(0, freed))} MB，可用内存 {_ToMegabytes((long)after.Available)} MB；" +
            $"清空备用列表 {(purged ? "成功" : "失败（通常是没有管理员权限）")}，裁剪工作集进程 {trimmed} 个。");

        return Math.Max(0, freed);
    }

    /// <summary>
    ///     将字节数格式化为便于阅读的文本（MB / GB）。
    /// </summary>
    public static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0 MB";
        if (bytes >= 1024L * 1024 * 1024)
            return (bytes / 1024d / 1024 / 1024).ToString("0.0") + " GB";
        return Math.Max(1, bytes / 1024 / 1024) + " MB";
    }

    private static long _ToMegabytes(long bytes) => bytes / 1024 / 1024;

    private static void _EnablePrivileges()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
        {
            LogWrapper.Warn("MemoryOptimize", $"打开当前进程令牌失败（错误码 {Marshal.GetLastWin32Error()}），将跳过权限申请。");
            return;
        }

        try
        {
            foreach (var privilege in _Privileges) _EnablePrivilege(token, privilege);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("MemoryOptimize", $"申请内存优化所需权限时出错：{ex.Message}");
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static void _EnablePrivilege(IntPtr token, string privilege)
    {
        if (!LookupPrivilegeValue(null, privilege, out var luid)) return;

        // TOKEN_PRIVILEGES: DWORD PrivilegeCount; LUID Luid; DWORD Attributes;
        var buffer = Marshal.AllocHGlobal(16);
        try
        {
            Marshal.WriteInt32(buffer, 0, 1);
            Marshal.WriteInt64(buffer, 4, luid);
            Marshal.WriteInt32(buffer, 12, (int)SePrivilegeEnabled);
            if (!AdjustTokenPrivileges(token, false, buffer, 16, IntPtr.Zero, IntPtr.Zero))
                LogWrapper.Debug("MemoryOptimize",
                    $"调整令牌权限 {privilege} 失败（错误码 {Marshal.GetLastWin32Error()}）。");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool _TrySetMemoryListCommand(int command)
    {
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            Marshal.WriteInt32(buffer, 0, command);
            var status = NtSetSystemInformation(SystemMemoryListInformation, buffer, 4);
            if (status != 0)
            {
                LogWrapper.Debug("MemoryOptimize",
                    $"NtSetSystemInformation(SystemMemoryListInformation, {command}) 返回 0x{status:X8}。");
                return false;
            }

            LogWrapper.Debug("MemoryOptimize", $"NtSetSystemInformation(SystemMemoryListInformation, {command}) 执行成功。");
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int _TrimProcessWorkingSets()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("MemoryOptimize", $"枚举进程失败，跳过工作集裁剪：{ex.Message}");
            return 0;
        }

        var count = 0;
        foreach (var process in processes)
        {
            try
            {
                if (process.Id <= 4) continue;
                if (process.HasExited) continue;
                if (EmptyWorkingSet(process.Handle)) count++;
            }
            catch (Exception)
            {
                // 系统进程、受保护进程与权限不足的进程会被直接跳过
            }
            finally
            {
                process.Dispose();
            }
        }

        return count;
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtSetSystemInformation(int infoClass, IntPtr info, int length);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AdjustTokenPrivileges(IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges, IntPtr newState, uint bufferLength,
        IntPtr previousState, IntPtr returnLength);

    [LibraryImport("advapi32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool LookupPrivilegeValue(string? systemName, string name, out long luid);

    [LibraryImport("kernel32.dll")]
    private static partial IntPtr GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyWorkingSet(IntPtr processHandle);
}
