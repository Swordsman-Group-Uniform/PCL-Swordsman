
# 让下游分支能登录正版（Microsoft）账号

> 状态：**操作手册**　适用范围：本下游分支（PCLCE For SGU）
> 相关：`HANDOVER.md` §0.11（为什么回滚到上游代码）、`PCL.Core/App/Secrets.cs`、`PCL.Core/Minecraft/Profile/Authentication/MicrosoftProvider.cs`

---

## 0. 一句话结论

上游代码**必须**有一个微软 OAuth 客户端 ID 才能登录正版。这个 ID 不是密码，可以安全地编进 exe，但**得自己注册一个**（免费的 Azure 应用），然后用环境变量在构建时注入：

```bash
PCL_WRITE_SECRET=1 PCL_MS_CLIENT_ID=<应用客户端 ID GUID> ./build.sh x64 Release
```

注入成功后，程序里 `Secrets.MSOAuthClientId` 就不再为空，`MicrosoftProvider` 会正常工作。

---

## 1. 为什么必须自己注册

上游 `MicrosoftProvider` 只做一件事：拿 `Secrets.MSOAuthClientId` 去 `login.microsoftonline.com/consumers` 换设备码。

```csharp
_clientId = string.IsNullOrWhiteSpace(clientId) ? Secrets.MSOAuthClientId : clientId;
if (string.IsNullOrWhiteSpace(_clientId))
    throw new InvalidOperationException("Microsoft OAuth client id is not configured.");
```

而 `Secrets.MSOAuthClientId` 来自 `EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly: true)`：

- Release 构建里 **只读编译期注入的 `SecretDictionary`**，不读运行期环境变量（`readEnvDebugOnly: true` 的语义）
- `SecretDictionary` 由 `PCL.Core.SourceGenerators/EnvironmentInteropGenerator.cs` 在**编译时**生成：
  只要构建环境里存在 `PCL_WRITE_SECRET`，就把当时所有 `PCL_*` 环境变量写进程序集

上游官方构建能登录，是因为他们的 CI 里配了这个密钥；本仓库没有，于是回滚后必然报
`Microsoft OAuth client id is not configured.`。

> ⚠️ 不要试图去「借用」上游或其它启动器的客户端 ID：那是别人注册的应用，微软可以随时封停，
> 而且责任（滥用、限流、封禁）会落在应用所有者身上。

---

## 1.2 为什么不能直接复用上游 PCL CE 的客户端 ID

**技术上可以**——客户端 ID 是公开标识符、不是凭据，填进 `PCL_MS_CLIENT_ID` 一样能跑。
但**不应该**，原因按重要性排序：

| 风险 | 说明 |
| --- | --- |
| **连累上游被封** | 微软按 app 做限流与滥用检测。fork 的用户量与上游混在同一个 app 下，一旦被判定异常（批量登录、脚本刷号等），**被停用的是整个 app**——PCL CE 官方所有用户会一起登不上。这个后果不是「你自己出问题」，而是「你把第三方拖下水」 |
| **未授权使用他人应用** | App registration 的所有者是 PCL CE 团队。用它等于以他们的名义接受微软开发者条款，而他们并不知情也未同意；换成 API Key 就是「借用别人的密钥」 |
| **随时失效且无法自救** | 上游改 ID、撤销应用、或被微软要求 publisher verification 时，本分支瞬间作废，本仓库没有任何自助手段 |
| **排查困难** | 撞上上游在该 app 上启用的限制（redirect、条件访问、区域策略）时，报错只会挂在他们 app 名下，你连日志都看不到 |

**拿不到也是事实**：该 ID 存在上游 CI 的 `MS_CLIENT_ID` secret 里，不在源码中；发布出来的 exe 里虽然
被烘焙进去了，但从别人产物里提取属于灰色手段，不建议做（而且同样跑不掉上表第 1、2 条）。

### 什么情况下可以复用

- PCL CE 团队**公开授权** fork 共用该客户端 ID（例如在仓库 README / issue 里明确写「各下游分支可共用」）
- 或者他们直接公开了 ID 与用途

真有这一步，最省事的做法是去上游仓库开个 issue 问一句；得到书面同意后再写进 `secrets.env` / 构建环境变量即可，
**不需要改任何代码**。

> 对比：社区常说的 `00000000402b5328`（Xbox 第一方客户端）风险性质不同——它属于微软自己，
> 不存在「盗用第三方作品」的问题，只是依赖微软默许（详见 §6）。

---

## 1.5 ⚠️ 先解决「登不进 Azure 门户」

用**个人微软账号（live.com / outlook.com / QQ 邮箱注册的微软账号）**打开 <https://portal.azure.com>
经常直接报：

```
AADSTS16000: User account '…' from identity provider 'live.com' does not exist in tenant
'Microsoft Services' and cannot access the application '74658136-14ec-4630-ad9b-26e160ff0fc6'(ADIbizaUX)
in that tenant. The account needs to be added as an external user in the tenant first.
```

含义很直接：**这个个人账号根本没有属于自己的 Azure AD 租户**，而 Azure 门户本身就是
一个只能被「租户内用户」访问的应用（`ADIbizaUX`）。这不是权限没给，是没有地方可给。
**必须先有一个租户（目录），才能在门户里注册应用。**

### 拿到租户的三条路（按是否要信用卡排序）

| 路线 | 要不要信用卡 | 适合谁 | 说明 |
| --- | --- | --- | --- |
| **A. Microsoft 365 开发人员计划**（推荐） | **不要** | 个人开发者 | <https://developer.microsoft.com/microsoft-365/dev-program> → 加入 → 自动获得一个沙盒租户和管理员账号（形如 `admin@<xxx>.onmicrosoft.com`）。用这个**工作账号**登录门户即可注册应用 |
| B. Azure 免费账户 | 要（验证身份，不扣费） | 有 Visa/Mastercard | <https://azure.microsoft.com/free/> → 注册时会自动为你的账号创建目录与订阅。国内双币卡成功率视卡而定 |
| C. 用现成的工作/学校账号 | 不要 | 有单位/学校 Office 365 账号 | 直接用那个账号登录门户。**注意**：很多单位租户关闭了「用户可以注册应用」，会报 `AADSTS50020`/没有权限；此时只能求助该租户管理员 |
| D. **Azure for Students**（学生不要卡） | **不要** | 在校学生 | <https://azure.microsoft.com/free/students/> 用学校邮箱（国内可用 `edu.cn`）或 GitHub Student Pack 完成学籍验证，开通后自带目录/租户，可直接注册应用 |

> ℹ️ **Microsoft 365 开发人员计划的「Instant sandbox」现在多数账号看不到按钮**（微软收紧了沙盒开通，
> 大量用户反馈 [按钮缺失](https://github.com/OfficeDev/office-js/issues/5799) / [加入后没有沙盒](https://learn.microsoft.com/en-my/answers/questions/5563594/missing-sandbox-after-rejoining-developer-program)、
> [无法开通](https://learn.microsoft.com/en-my/answers/questions/5994056/unable-to-provision-microsoft-365-e5-instant-sandb)），
> 它是**可选福利而不是稳定通道**，别在这里耗时间——走 A / B / D 更靠谱。

拿到租户之后的差别（注册时要注意）：

- 用**开发租户/单位租户**（路线 A/C）注册时，「受支持的账户类型」选
  **`任何组织目录中的账户和个人 Microsoft 账户`** —— 这样 Minecraft 玩家的个人账号才能登录
  （门户里那个「仅个人账户」选项在这种租户下通常不可选）
- 其余步骤（公共客户端流、`XboxLive.signin`）与 §2 完全一致

> 无论走哪条路，**注册应用不需要 Azure 订阅、不需要花一分钱**；
> 需要的只是「一个目录 + 一个能登录门户的账号」。

---

## 2. 注册 Azure 应用（约 10 分钟，免费，无需信用卡）

1. 打开 <https://portal.azure.com> → **Microsoft Entra ID**（旧名 Azure AD）→ **应用注册** → **新注册**
2. 填写：
   - **名称**：例如 `PCLCE For SGU`
   - **受支持的账户类型**：选 **`仅个人 Microsoft 账户`**
     （若想同时支持工作/学校账户，可选 `任何组织目录中的账户和个人 Microsoft 账户`；
     两种都能用 `/consumers` 端点，但**必须包含个人账户**，否则 Minecraft 玩家登不进来）
   - **重定向 URI**：留空（设备码流程不需要）
3. 注册完成后，在「概述」页复制 **`应用程序(客户端) ID`** —— 形如
   `8f8c1f75-31e4-4bd2-a2f4-0d1e5c1dc0a2`。这就是 `PCL_MS_CLIENT_ID` 的值
4. 左侧 **身份验证** → 拉到底 **高级设置** → **允许公共客户端流** 选 **`是`**
   （设备码流程是公共客户端流程，不打开会报 `AADSTS70002`/`unauthorized_client`）
5. 左侧 **API 权限** → **添加权限** → **我的组织使用的 API** → 搜索 `XboxLive` →
   选 **委托的权限** → 勾选 **`XboxLive.signin`** → 添加
   - `offline_access`（用于拿刷新令牌）由代码里的 scope 直接申请，不需要单独添加
   - 个人账户应用不需要「代表组织授予管理员同意」，用户首次登录时自行同意即可
6. （可选）左侧 **令牌配置** 无需改动；**证书和密码** 里 **不要** 创建客户端密码 ——
   公共客户端不应该有 secret，泄漏了也用不上

---

## 2.4 用单位 / 学校的「工作账户」注册（不要卡）

工作账户本身就属于某个租户，所以**既不需要信用卡、也不需要额外建租户**，直接登录注册即可。
入口建议用 **Entra 管理中心**（比 Azure 门户干净，没有订阅也能用）：

1. 打开 <https://entra.microsoft.com>，用**工作账户**登录，右上角确认当前目录是你的单位目录
2. 左侧 **应用** → **应用注册** → 顶部 **+ 新注册**
3. 名称随便填（如 `PCLCE For SGU`）
4. **受支持的账户类型**必须选 **`任何组织目录中的账户和个人 Microsoft 账户`**
   （选「仅此组织目录」或「仅多租户」都会导致玩家的**个人**账号登不进来）
5. 重定向 URI **留空** → 注册
6. 复制「概述」页的 **应用程序(客户端) ID**
7. **身份验证** → 最底部「高级设置」→ **允许公共客户端流 = 是** → 保存
8. **API 权限** → 添加权限 → **我的组织使用的 API** → 搜索 `XboxLive` → 委托的权限 →
   `XboxLive.signin` → 添加（不是管理员的话可以不点「授予管理员同意」，个人账号首次登录时自行同意即可）
9. **证书和密码** → **不要**新建客户端密码

### 没有权限注册应用怎么办

- 报错形式：「你没有权限注册应用」/ `Insufficient privileges` / 按钮变灰
- 原因：租户把「用户可以注册应用」关掉了
- 解决（二选一）：
  - 请管理员打开：**Entra 管理中心 → 用户 → 用户设置 → 应用注册 →
    「用户可以注册应用程序」= 是**
  - 或者直接请管理员在该租户里按上面 3~9 步帮你注册，然后把 **客户端 ID** 给你

### 用公司租户的代价（交接时必须写清楚）

- 应用归这个租户所有，管理员随时可以删除或修改 → 应用失效后**所有用户的正版登录同时失效**
- 离职 / 学校账号注销 = 应用跟着消失。想长期稳定，最好用自己的租户（Azure 免费 / 学生）
- 别在该租户里顺手加别的权限：`XboxLive.signin` 之外都不需要

---

## 2.6 门户报 `AADSTS50020` / `AADSTS16000`（账户进不去 Azure 门户）

现象：账户明明属于某个租户（例如 `xxx@contoso.onmicrosoft.com`，租户 ID `87af9bc9-…`），
但打开 `portal.azure.com` 报

```
AADSTS50020: User account 'xxx@contoso.onmicrosoft.com' from identity provider
'https://sts.windows.net/<租户ID>/' does not exist in tenant 'Microsoft Services'
and cannot access the application '74658136-14ec-4630-ad9b-26e160ff0fc6'(ADIbizaUX) in that tenant.
```

**这不是权限问题，也不是账户坏了**：`ADIbizaUX` 是 Azure 门户这个应用，报错说明浏览器把登录请求
送进了消费端租户 **Microsoft Services**，而不是你自己单位的租户。基本都是**残留的会话/Cookie**
或者门户自动选错了目录导致的。按顺序试：

1. **彻底退出所有微软会话**，再用**无痕窗口**打开：
   - 退出：<https://login.microsoftonline.com/common/oauth2/v2.0/logout>（或 account.microsoft.com → 注销）
2. **用带租户后缀的地址进**，强制指定目录：
   - <https://entra.microsoft.com/contoso.onmicrosoft.com>
   - <https://portal.azure.com/contoso.onmicrosoft.com>
3. **先验证账号本身正常**：打开 <https://admin.microsoft.com>（M365 管理中心）。能进说明账号/租户没问题，
   只是 Azure 门户的会话问题 → 回到第 2 步
4. 仍然不行：清空 `login.microsoftonline.com` / `portal.azure.com` 的 Cookie，换浏览器，
   或换网络（公司代理/加速器可能改写登录请求）

### 完全绕开门户：用 Azure CLI 注册

```bash
# 安装（Windows）：winget install Microsoft.AzureCLI
az login --tenant 87af9bc9-c175-47c5-937e-90fff4a45ef5          # 也可写域名
az ad app create --display-name "PCLCE For SGU" \
    --sign-in-audience AzureADandPersonalMicrosoftAccount
# 输出里的 appId 就是「应用程序(客户端) ID」
az ad app show --id <appId> --query appId -o tsv
```

还需要两个属性：

```bash
# 1) 允许公共客户端流（设备码流程必需）
az ad app update --id <appId> --set isFallbackPublicClient=true
#    若该参数不被你的 az 版本接受，改用 Graph：
#    az rest --method PATCH #      --url "https://graph.microsoft.com/v1.0/applications/<objectId>" #      --headers Content-Type=application/json #      --body '{"isFallbackPublicClient":true}'

# 2) 加 XboxLive.signin 委托权限（先查这个资源里该 scope 的 GUID）
az ad sp show --id 000000004c12ae6f \
    --query "oauth2PermissionScopes[?value=='XboxLive.signin'].id" -o tsv
az ad app permission add --id <appId> --api 000000004c12ae6f \
    --api-permissions <上一步查到的 GUID>=Scope
az ad app permission grant --id <appId> --api 000000004c12ae6f   # 你是管理员时授予同意
```

> `000000004c12ae6f` 是微软第一方的 **Xbox Live** 资源应用 ID，任何租户都能给它加委托权限。

---

## 2.5 把 GUID 交给维护者之前请自检

| 检查项 | 怎么确认 | 常见错法 |
| --- | --- | --- |
| 给的是 **应用程序(客户端) ID** | 「概述」页第一行，36 位 GUID | ❌ 给成 **对象 ID**（`Object ID`）或 **目录(租户) ID** —— 这两个长得一样像，填错会在设备码阶段报 `AADSTS700016`/`invalid_client` |
| 受支持的账户类型包含**个人 Microsoft 账户** | 「身份验证」页顶部 | ❌ 选成「仅此组织目录中的账户」，玩家的个人账号会登不进来 |
| **允许公共客户端流 = 是** | 「身份验证」→ 最底部「高级设置」 | ❌ 保持默认「否」→ `unauthorized_client` / `AADSTS70002` |
| 有 **`XboxLive.signin`** 委托权限 | 「API 权限」列表里状态为「已授予」/「已同意」 | ❌ 只加了 Microsoft Graph 的 `User.Read`（这对登录 Minecraft 没用） |
| **没有**客户端密码 | 「证书和密码」里为空 | ✅ 本来就不该有；有 secret 也不影响，但别泄漏 |

自检最快的方法：把 GUID 粘进下面这个地址（把 `{GUID}` 换掉），浏览器能返回一段含
`"tenant_discovery_endpoint"` 的 JSON 就说明这个应用在该租户里真实存在：

```
https://login.microsoftonline.com/consumers/v2.0/.well-known/openid-configuration
```

（这条只能证明租户可达；应用是否可用要靠设备码请求实际验证，维护者构建时会替你验证。）

---

## 3. 构建时注入

```bash
# Linux / Termux（先 export，再调用构建脚本）
export PCL_WRITE_SECRET=1
export PCL_MS_CLIENT_ID=8f8c1f75-31e4-4bd2-a2f4-0d1e5c1dc0a2
./build.sh x64 Release
```

```powershell
# Windows（PowerShell）
$env:PCL_WRITE_SECRET = "1"
$env:PCL_MS_CLIENT_ID = "8f8c1f75-31e4-4bd2-a2f4-0d1e5c1dc0a2"
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```

要点：

- `PCL_WRITE_SECRET` 只要**存在**即可（值随意），它是「开始收集 `PCL_*`」的开关
- 这两个变量必须在 **dotnet 编译进程**的环境里，因此要 export 在调用构建脚本的那个 shell 里
- **换 ID 时必须先清 `obj/bin`**（2026-10-25 实测）：源文件没变时 MSBuild 会判定项目是最新的，
  增量生成器不会重新执行，于是**旧的 Client ID 会原封不动留在程序集里**（改环境变量再编译，
  2.7 秒就走了个「已是最新」，DLL 里仍是旧 GUID）。所以第一次注入或更换 ID 前：
  ```bash
  rm -rf PCL.Core/obj PCL.Core/bin "Plain Craft Launcher 2/obj" "Plain Craft Launcher 2/bin"
  ```
- **本项目不含其它必需密钥**：`CURSEFORGE_API_KEY`/`TELEMETRY_KEY`/`NAID_*` 缺失不影响正版登录，
  但打开 `PCL_WRITE_SECRET` 会把这些也一并写进程序集（缺失的只是不写入）

### 验证注入成功（已实测可行）

```bash
strings -a "artifact-x64/PCLCE For SGU 27.1-snapshot-13(2.15.1).exe" | grep -i "8f8c1f75" # 命中即成功
# 或者直接跑起来点「正版」：不再报 "Microsoft OAuth client id is not configured." 而是弹出设备码对话框
```

程序集里出现该 GUID 是正常且必要的——它是**公开标识符**，不是密码，官方启动器也是把它编进 exe 的。

---

## 4. 登录流程回顾（排查用）

```
设备码      POST login.microsoftonline.com/consumers/oauth2/v2.0/devicecode
            client_id=<你的 GUID>, scope="XboxLive.signin offline_access"
            → user_code / verification_uri(https://microsoft.com/devicelogin) / interval / expires_in
轮询        POST .../oauth2/v2.0/token   grant_type=urn:ietf:params:oauth:grant-type:device_code
            （authorization_pending 表示用户还没输入码，MyMsgLogin 会自动重试）
刷新        POST .../oauth2/v2.0/token   grant_type=refresh_token   （登录档案里存的是 refresh_token）
Xbox Live   POST https://user.auth.xboxlive.com/user/authenticate
            RpsTicket="d=<access_token>"、SiteName="user.auth.xboxlive.com"、AuthMethod="RPS"
XSTS        POST https://xsts.auth.xboxlive.com/xsts/authorize   RelyingParty="rp://api.minecraftservices.com/"
Minecraft   POST https://api.minecraftservices.com/authentication/login_with_xbox
            → access_token；再 /minecraft/profile 取名字与 UUID
```

对应代码：`PCL.Core/Minecraft/Profile/Authentication/MicrosoftProvider.cs`（上游原件，**本分支不应再改**）、
`Utils/XboxUtils.cs`、`Plain Craft Launcher 2/Modules/Minecraft/ModLaunch.cs`（`McLoginMsStart`）、
`Plain Craft Launcher 2/Pages/PageLaunch/MyMsgLogin.xaml.cs`（设备码对话框与轮询）。

---

## 5. 常见错误对照表

| 报错 | 原因 | 处理 |
| --- | --- | --- |
| `Microsoft OAuth client id is not configured.` | 构建时没注入 `PCL_MS_CLIENT_ID` | 见 §3，并清 `obj/bin` 后重编 |
| `AADSTS700016: Application with identifier '…' was not found in the directory '9188040d-…'` | 应用不支持个人账户，或用的是微软第一方客户端 ID | 重新检查「受支持的账户类型」是否包含个人 Microsoft 账户 |
| `AADSTS70002` / `unauthorized_client`（设备码阶段） | 没开「允许公共客户端流」 | §2 第 4 步改为 `是` |
| `AADSTS65001` / 同意失败 | 没加 `XboxLive.signin` 权限 | §2 第 5 步 |
| `xsts_…` / `2148916233` | 该微软账号没有 Xbox 档案（从没登录过 Xbox 服务） | 先用该账号登录一次 <https://www.xbox.com> 或 Minecraft 官网创建档案 |
| `2148916238` | 未成年账号需要加入家庭组 | 家长账号在 <https://account.microsoft.com/family> 处理 |
| `minecraft_not_owned` | 该账号没有游玩 Minecraft: Java 版的权利 | 无法登录，正常报错 |
| 登录成功但启动 401 | 令牌过期 / 旧档案残留 | 在账号列表里刷新一次，或删档重登 |

---

## 6. 当前生效方案：微软第一方客户端 ID（不注册 Azure 应用）

> ✅ **从 `snapshot-16` 起，本分支默认走这一节**（用户确认无法注册 Azure 应用：没有可验证的卡，
> 工作账户也没有管理员角色）。§2~§3 的自建应用路线依然有效且**不需要改代码**：
> 把 GUID 放进环境变量 `PCL_MS_CLIENT_ID` 或构建期 `MS_CLIENT_ID`，Provider 会自动切到 Entra ID v2。

实现位置：`PCL.Core/Minecraft/Profile/Authentication/MicrosoftProvider.cs`

| 项目 | 值 |
| --- | --- |
| 客户端 ID | `00000000402b5328`（微软第一方 Xbox 客户端，公开标识符） |
| 设备码 | `POST https://login.live.com/oauth20_connect.srf`（`response_type=device_code`） |
| 权限范围 | `service::user.auth.xboxlive.com::MBI_SSL` |
| 令牌 / 刷新 | `POST https://login.live.com/oauth20_token.srf`（`device_code` 轮询 / `refresh_token`） |
| 用户验证页 | `https://www.microsoft.com/link`（输入设备码） |
| Xbox 票据前缀 | **`t=`**（第一方客户端专用；自建应用用 `d=`） |

维护要点：

- 分流依据是 Client ID **形式**：GUID → Entra ID v2；非 GUID → live.com 旧版。改 ID 只需改环境变量/构建密钥
- 失败时先看日志里的「使用 … 设备码流程」「Xbox Live 用户认证失败（票据前缀：…）」
- **这是非官方支持路径**：微软若关停第一方客户端复用，全体用户同时失效 → 届时回到 §2~§3
  自建应用路线（只需注入 GUID，代码无需改动）
- 只支持**个人微软账号**；单位/学校账号（`@xxx.onmicrosoft.com`）无法登录

---

## 6.5 早先记录的备选说明（保留备查）

唯一可行的替代是走**微软第一方（Title）客户端 ID + `login.live.com` 旧版设备码流程**
（`00000000402b5328` + `service::user.auth.xboxlive.com::MBI_SSL`）。这条链路 2026-10 实测仍可用，
但：

- 需要修改 `MicrosoftProvider`（下游改上游代码，正是 2026-10-25 用户明确要求**不要**做的事，已回滚）
- 依赖微软对第三方复用第一方客户端的默许，随时可能被关停
- 一旦被关停，所有用户同时登不上，且无法用配置补救

因此只在「确实无法注册 Azure 应用」时考虑，并且要单独开分支、单独发版、写清楚风险。

---

## 7. 交接备注

- 本文件描述的是**上游原生做法**，不需要改动任何代码
- 构建产物里含有客户端 ID 属于预期行为；**不要把任何密钥（secret）写进仓库或构建脚本**
- 若未来上游把客户端 ID 改成运行期配置（例如加设置项），优先跟随上游，不要在 `MicrosoftProvider` 里另起分支
