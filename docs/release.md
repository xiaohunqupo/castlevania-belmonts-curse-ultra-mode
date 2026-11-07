# 发布流程

发布在本机完成，由 `tools/release.ps1` 一键执行。不能用 GitHub 托管的 Actions 自动编译：编译要引用游戏目录里 BepInEx 生成的 interop 程序集，它们来自游戏代码，不能上传到仓库或云端。

## 一次性准备

```powershell
winget install GitHub.cli
gh auth login --hostname github.com --git-protocol https --web
```

## 发布步骤

### 1. 写发布说明

新建 `docs/release-notes/v<版本>.md`，中英文各一份（参考 [v0.1.0](release-notes/v0.1.0.md)），写清楚：

- 新功能、改动、修复；
- 新增或改名的配置项，以及玩家是否需要处理旧配置；
- 适用的 BepInEx 版本（be.788）和已知冲突。

没有这个文件时，GitHub 会根据提交记录自动生成发布说明。

提交这个文件，并确保工作区干净、`main` 已推送。

### 2. 测试

把 `dotnet build -c Release` 打出的 zip 解压到一个装好 BepInEx 的游戏目录，按[测试清单](testing.md)过一遍。

### 3. 先演练

```powershell
powershell -File tools/release.ps1 0.2.0 -DryRun
```

会执行全部检查并编译打包，但不提交、不打标签、不推送、不发布；临时改动的版本号会还原。

### 4. 正式发布

```powershell
powershell -File tools/release.ps1 0.2.0
```

脚本依次执行：

1. 检查：`gh` 已登录；当前在 `main`；工作区干净；与 `origin/main` 一致；标签和 Release 都还不存在。
2. 版本号：`UltraMode.csproj` 的 `<Version>` 和 `src/Plugin.cs` 的 `Plugin.Version` 不是目标版本时，两处一起改。
3. 编译：`-warnaserror`，有警告就失败；检查 zip 里有 dll、两份 README、两份许可证和 BelmontUI-LICENSE.txt。
4. 如果改了版本号，提交 `Release v0.2.0`。
5. 打附注标签 `v0.2.0`，推送 `main` 和标签。
6. `gh release create`，上传 `dist/UltraMode-v0.2.0.zip` 和整合包 `dist/UltraMode-v0.2.0-with-BepInEx.zip`，发布说明用 `docs/release-notes/v0.2.0.md`。

第 3 步还会检查整合包：必须有 BepInEx 本体、`BepInEx/third-party/` 下的声明和许可证；不能有 `BepInEx/interop/`、`BepInEx/config/` 这类在玩家机器上生成的文件（interop 来自游戏代码，不能分发）。

## 整合包与 BepInEx 版本

整合包原样包含官方 BepInEx 6 IL2CPP win-x64。BepInEx 和 Doorstop 是 LGPL-2.1，Il2CppInterop 是 LGPL-3.0，其余组件是 MIT / Apache-2.0。原样再分发是允许的，但官方 zip 里没有任何许可证文件，所以必须附上 `third_party/BepInEx/`（`NOTICE.md` 列出每个组件的版本、许可证和对应版本的源码地址，`licenses/` 是全文）。**不要修改 BepInEx 的任何文件再打包**，否则还要提供修改后的源码。

升级捆绑的 BepInEx 版本：

1. 在 `UltraMode.csproj` 里改 `BepInExBuild`、`BepInExUrl`，并把 `BepInExSha256` 改成新 zip 的 SHA-256（`Get-FileHash <zip>`）。
2. 打开新 zip，查看 `BepInEx/core` 下各 dll 的版本（`(Get-Item x.dll).VersionInfo.ProductVersion`，`+` 后面是提交号），更新 `tools/fetch-bepinex-licenses.ps1` 里的 `$components`。新增或删除了组件时也要在这里增删。
3. 运行 `powershell -File tools/fetch-bepinex-licenses.ps1`，检查 `third_party/BepInEx/NOTICE.md` 的差异并提交。
4. 用新 BepInEx 在游戏里完整测试，并同步修改 README 里写的 BepInEx 版本。

其他参数：`-Draft` 只创建草稿，在网页上确认后再发布；`-NotesFile <文件>` 指定其他发布说明文件。

脚本在任何一步失败都会停下，之前已完成的步骤不会回滚。例如标签已推送但创建 Release 失败时，修好问题后在网页上用该标签手动创建 Release，或者删掉标签（`git push origin :refs/tags/v0.2.0` 和 `git tag -d v0.2.0`）后重新运行。

## 版本号规则

遵循语义化版本：修 bug 升补丁号（0.1.1），新增功能或配置项升次版本号（0.2.0）。删除或重命名配置项会让玩家的设置失效，要在发布说明里写清楚。

## 不能发布的内容

- `reference/`：反编译的游戏代码和其他 mod 的代码，只供本地参考。
- `tools/*.txt`：从游戏程序集导出的类型列表。
- 任何游戏文件、资源，包括 `BepInEx/interop/` 下的程序集。

这些都已在 `.gitignore` 里，提交前用 `git status` 确认没有被强制加入。
