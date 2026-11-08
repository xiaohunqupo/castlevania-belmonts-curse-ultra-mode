# 开发环境与调试

## 环境

- Windows x64，Steam 版游戏。
- .NET 6 SDK 或更新版本。
- 游戏目录装好 BepInEx 6 IL2CPP be.788，并启动过一次游戏，生成 `BepInEx/interop/` 下的 interop 程序集。项目直接引用这些 dll，没有它们无法编译（`CheckGamePath` 目标会报错提示）。
- 编辑器任意：Visual Studio、Rider、VS Code / Cursor 都可以。

游戏不在默认 Steam 路径时，用 `-p:GamePath=...` 或环境变量 `BELMONTS_CURSE_DIR` 指定。

## 构建

```powershell
dotnet build -c Release                     # 编译、打包到 dist/、复制到游戏目录
dotnet build -c Release -p:SkipDeploy=true  # 游戏正在运行（dll 被锁）时用这个
dotnet build -c Release -p:SkipPackage=true # 不打包
```

- 打包目标声明在部署之前，所以即使游戏占用 dll 导致部署失败，`dist/` 里的 zip 也已经生成。
- 部署只是把 dll 和说明文件复制到 `BepInEx/plugins/UltraMode/`，重启游戏才会加载新版本。BepInEx IL2CPP 不支持热重载。

## 调试手段

### 日志

`BepInEx/LogOutput.log`。启动时会写出每个模块是否安装成功：

```
Ultra Mode 0.1.0 loaded; 12/12 features active: ActionTracker, AttackParry, CameraFollow, ...
Settings page: on Belmont Minimap's MODS tab
```

模块出错时会有一段 `[模块名] disabled for this session; vanilla behaviour restored.` 加完整异常。

打开 `99 Debug / DebugLogging` 后，`Log.Debug` 的内容也会写出：主角每个动作的开始（`+ 动作名 [类型] end= chain=`）和结束（`- 动作名`），以及各模块的决策（例如 `WhipAssist: auto-aim at ...`）。**每帧都可能触发的日志一定要走 `Log.Debug`**，不要直接用 `Plugin.Logger`。

### F11 导出

F11 快捷键默认关闭，先在配置文件里打开 `99 Debug / DumpKeyEnabled`（设置页里没有这一行）。之后按 F11 把运行时数据写到 `BepInEx/ultra_mode_dump/`（`Inspector.DumpAll`）：

| 文件 | 内容 |
| --- | --- |
| `mc_actions.txt` | 本次会话主角用过的动作及次数，每个动作的完整字段树 |
| `action_index.txt` | 所有已加载动作的名字、类型、结束帧、时间缩放 |
| `vania_moves.txt` | 已解锁的能力和全部能力 |
| `parry_candidates.txt` | 所有带 ParryFeature 的动作 |
| `mirror_actions.txt` / `mirrors.txt` | 镜子相关动作和场景里每面镜子的状态 |
| `feedbacks.txt` | 所有 Feedback 资源名（受身特效的可选值） |
| `charms.txt` | 每个魔导器的资源名、是否拥有、效果和属性修改 |

要研究新动作时，先在游戏里做一遍那个动作，再按 F11，在 `mc_actions.txt` 里找它的字段。

### 查找游戏类型：TypeDump

`tools/TypeDump` 以只读元数据方式读取 interop 程序集，列出类型和成员，不需要启动游戏：

```powershell
$interop = "C:\Program Files (x86)\Steam\steamapps\common\Castlevania Belmont's Curse\BepInEx\interop"
dotnet run --project tools/TypeDump -- $interop MotherBase.ActionFramework.dll "Parry" --members
dotnet run --project tools/TypeDump -- $interop Assembly-CSharp.dll "^Inventory$" --members --out tools/types_inv.txt
```

参数：interop 目录、程序集文件名、类型全名正则，`--members` 列出字段/属性/方法，`--out` 写到文件。

游戏代码主要在 `Assembly-CSharp.dll`、`MotherBase.ActionFramework.dll`、`MotherBase.EntityFramework.dll`、`MotherBase.FeedbackManager.dll`，命名空间对应 `MotherBase.*`。输出放在 `tools/*.txt`，已被 gitignore。

资源名（魔导器、动作等）也可以在 `<游戏>/Castlevania Belmont's Curse_Data/StreamingAssets/aa/catalog.json` 里搜索。

### 方法体

interop 程序集只有签名，没有方法实现。需要知道游戏方法内部做了什么时，可以用 Il2CppDumper / Cpp2IL 等工具配合反编译 `GameAssembly.dll`，或者先在补丁里加 `Log.Debug` 观察参数和返回值。反编译得到的任何东西都只能放在被忽略的 `reference/` 目录，**不能提交**。

## IL2CPP 注意事项

这些是本项目踩过的坑，代码里大量的写法都是因为它们：

- **异常不能逃出补丁。** 每个 Harmony 补丁和事件回调都包 try/catch，出错调用 `Fix.Fail`。
- **比较对象用 `.Pointer`。** 同一个游戏对象每次从 IL2CPP 取出来都可能是新的托管包装对象，`==` 和引用比较不可靠。缓存的 key 也用 `IntPtr`。
- **子类型用 `TryCast<T>()`。** C# 的 `is` / `as` 对 interop 对象不起作用。列表里取出的元素是声明类型的包装，要 `TryCast` 才能访问子类成员；反射遍历时用 `Inspector.Resolve`。
- **缓存的对象要检查 `WasCollected`。** 场景切换后原生对象会被销毁，旧的包装还在，访问会出错。
- **避免给带"按值传递的结构体"参数的方法打补丁。** 例如参数里有 `ActionInstance` 值类型的方法，Harmony 的参数封送容易出问题。`ActionTracker` 选择挂在只有 `Entity` 参数的 `Actor.UpdateState` 上就是这个原因。
- **`Resources.FindObjectsOfTypeAll` / `FindObjectsOfType` 很慢。** 只在必要时扫描，并且限频（各模块的 `ScanInterval`），结果缓存。
- **游戏资源是共用的。** 改 `ActionData`、Feature、受击策略之前先用 `Player` 过滤，否则会影响敌人。改的是资源本身，会持续到游戏退出，所以必须能恢复（见[架构](architecture.md#修改游戏数据的两种模式)）。
- **设置页的文字要能被游戏字体渲染。** 生僻字或特殊符号在 Minimap 里会导致整行被跳过，在 Belmont UI 里显示成方框。
- **`HarmonyPatch` 指定重载时写全参数类型**，例如 `typeof(CharmItemData), typeof(int)`。游戏更新新增重载时，不写参数类型会匹配失败。

## 代码风格

- 代码、注释、日志用英文；配置描述和设置页文案中英双语（配置描述只写中文）。
- 注释只写代码本身看不出来的约束和原因：为什么要这样做、游戏在这里有什么特殊行为。不写"下一行做什么"。
- 每个模块文件顶部用 `/// <summary>` 说明它改了游戏的什么、以及为什么这样改不会破坏原版的其他部分。
- 文件命名空间、`internal static class`、私有字段小驼峰，与现有代码保持一致。
