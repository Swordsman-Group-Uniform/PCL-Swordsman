# PCL CE 下游分支版本号规范

本分支（以下称“下游”）基于 PCL CE 源码二次开发，版本号采用
**`年份.大更新[.小更新][-阶段-Build号](上游版本号)`** 的形式。

> 括号内的上游版本号在下文描述中省略；完整展示时始终带上它，例如
> `27.1-snapshot-19(2.15.1)`。

---

## 1. 阶段与版本号形式

**Build 号规则：每次构建前 +1。**（同时把 `metadata.json` 的 `version.code` 也 +1，见 §3）

| 阶段 | 版本号形式 | 用途 |
| --- | --- | --- |
| 快照版 | `27.x-snapshot-[Build号](上游版本号)` | 添加并测试**新功能** |
| 预发布版 | `27.x-preview-[Build号](上游版本号)` | 测试**启动器稳定性** |
| 备选发布版 | `27.x-rc-[Build号](上游版本号)` | 最后的**稳定性测试** |
| 正式版 | `27.x` 或 `27.x.y`（小更新） | 正式发布的构建，**不显示 Build 号** |
| 热修版 | `27.x.y-hotfix-[Build号](上游版本号)` | 正式版发布后的小修复（带 Build 号） |
| 实验版 | `27.x-experiment-[Build号](上游版本号)` | **极不稳定，多改一行就会爆炸** |

各字段含义：

| 部分 | 含义 | 当前取值 |
| --- | --- | --- |
| `年份` | 下游分支的年份标记（因故固定为 `27`） | `27` |
| `大更新` | 下游的功能级大版本 | `1` |
| `小更新` | 正式版 / 热修版的修订号，快照等阶段省略 | 无 |
| `-阶段-Build号` | 快照 / 预览 / 备选 / 热修 / 实验 + 构建序号 | `-snapshot-18` |
| `(上游版本号)` | 当前已同步的 PCL CE 上游版本号 | `2.15.1` |

### 版本号推进示例

```text
27.1-snapshot-18(2.15.1)      ← 开发期，每构建一次 Build 号 +1
27.1-snapshot-19(2.15.1)
27.1-preview-20(2.15.1)       ← 功能冻结，转测稳定性
27.1-rc-21(2.15.1)            ← 最后稳定性测试
27.1(2.15.1)                  ← 正式发布，不带 Build 号
27.1.1-hotfix-22(2.15.1)      ← 正式版之后的小修复
```

> 预发布阶段的阶段名统一用 `preview`（历史上写过 `pre`，已废弃）。
>
> Build 号**跨阶段连续递增**，不要每个阶段从 1 重新数；这样从版本号就能看出构建顺序。

---

## 2. 推进规则

1. **同步上游更新时，括号外的下游版本号不变，只变更括号内的上游版本号。**
   例如上游从 `2.15.1` 升到 `2.16.0`：

   ```text
   同步前：27.1(2.15.1)
   同步后：27.1(2.16.0)
   ```

2. 仅当**下游自身**加入新功能或修复时才递增外层版本号
   （小更新 +1；不兼容的大改动时大更新 +1、小更新归零）——这一步只在**发正式版**时做。
3. 开发期一切构建都是快照版，**只递增 Build 号**，不动 `27.x`。

---

## 3. 字段与代码的对应关系

`Plain Craft Launcher 2/metadata.json`：

| 字段 | 含义 | 示例 |
| --- | --- | --- |
| `version.base` | 外层版本号（年份.大更新[.小更新]），**不含**阶段与 Build 号 | `27.1` |
| `version.suffix` | 阶段 + Build 号；**正式版留空** | `snapshot-18` |
| `version.upstream` | 已同步的 PCL CE 上游版本号 | `2.15.1` |
| `version.code` | 内部版本代号（整数），**每次构建 +1**，用于本地升级/降级判断 | `541` |

`PCL.Core/App/Metadata.cs` 的 `LauncherVersionModel`：

- `DisplayName` → `27.1-snapshot-18(2.15.1)`，用于界面与日志
  （`Basics.DisplayVersionName` / `ModBase.versionDisplayName`）
- `BaseName` / `DownstreamVersionName` → `27.1-snapshot-18`，
  用于 HTTP 头（User-Agent 要求合法 token，不能带括号）、文件名与语义化版本解析
- `Plain Craft Launcher 2.csproj` 的 `AssemblyVersion` / `FileVersion` 仍是四段格式（`27.1.0.0`），不随 suffix 变化

### ⚠️ 「发行版」判定

`PCL.Core/UI/Theme/ThemeService.cs`：

```csharp
public static bool IsReleaseDistribution => string.IsNullOrWhiteSpace(Basics.Metadata.Version.Suffix);
```

即**只有正式版（无后缀）**会隐藏非蓝色主题与自定义主题色（原作者赞助项目的政策）。
如果将来希望 `rc` / `hotfix` 也按发行版处理，改这一行判定即可（例如同时排除 `rc-`、`hotfix-` 前缀）。

---

## 4. 构建产物命名

构建脚本 [`build.sh`](build.sh) 与 [`tools/build.ps1`](tools/build.ps1) 会按以下规范重命名产物：

```text
PCLCE For SGU [版本号].exe
```

其中 `[版本号]` = `base` + `-` + `suffix`（若有）+ `(` + `upstream` + `)`，例如：

```text
PCLCE For SGU 27.1-snapshot-18(2.15.1).exe
PCLCE For SGU 27.1(2.15.1).exe                 ← 正式版
PCLCE For SGU 27.1.1-hotfix-22(2.15.1).exe     ← 热修版
```

> 程序集名（`AssemblyName`）仍保持 `Plain Craft Launcher 2`：WPF 的 `pack://` 资源 URI 直接引用
> 程序集名，改名会导致图标等资源加载失败，所以只在构建完成后重命名可执行文件。

> 本分支已在代码层禁用更新功能（`UpdateManager.IsUpdateSupported == false`），
> 版本号不再参与远端更新比对，仅用于展示与本地升级/降级判断。

---

## 5. 每次构建前的检查清单

1. `version.suffix` 的 Build 号 +1
2. `version.code` +1
3. 确认 `version.base` / `version.upstream` 没被误改
4. 构建后在**产物文件名**、界面「关于」、日志头部三处确认版本号一致
5. 版本说明写进 `DOWNSTREAM-CHANGES.md` 对应小节，并在 `HANDOVER.md` 追加修订记录

`metadata.json` 里改两个数字即可：

```jsonc
{
  "version": {
    "base": "27.1",
    "upstream": "2.15.1",
    "suffix": "snapshot-19",   // ← Build 号 +1
    "code": 542                // ← +1
  }
}
```

（需要重编密钥等场合记得先清 `PCL.Core/obj`、`PCL.Core/bin`、
`Plain Craft Launcher 2/obj`、`Plain Craft Launcher 2/bin`，否则增量编译会复用旧产物。）

---

## ⚠️ `upstream` 字段的两个同名含义（务必分清）

上游 PCL CE 自己的 `metadata.json` 里也有一个 `upstream` 字段，但它的含义是
**「本作继承自原版 PCL2 的版本号」**，从 fork 起就固定为 `2.12.1`，**不随上游版本推进而改变**
（上游 `2.15.1` 与 `2.15.1-beta.1` 的 `metadata.json` 里它都是 `2.12.1`，变的是 `base`）。

本分支的 `upstream` 字段含义**不同**：记录的是「已同步的 PCL CE 版本号」，
由维护者手工填写。判断本分支究竟基于哪个上游版本时，**不要看上游 metadata 的 `upstream` 字段**，
应当用三方文件比对确认（见 `HANDOVER.md` §4.1）。
