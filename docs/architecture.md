# 架构

## 目录

```
src/
  Plugin.cs          入口；UltraModeBehaviour 轮询 F10/F11
  Feature.cs         FeatureAttribute 与 Feature（模块注册、失败回退）
  Settings.cs        全部配置项；OnChanged 变更通知
  Player.cs          主角查找与组件查询；Log.Debug
  ActionTracker.cs   主角动作的开始/结束/每帧更新事件
  ActionInfo.cs      动作资源上的高频查询（带缓存）
  Inspector.cs       F11 运行时数据导出
  Features/          各功能模块，一个文件一个模块（FastResponse.cs 里还有 FrameScaler）
  Menu/              设置页
vendor/BelmontUI/    Belmont UI 上游源码（脚本生成，不要手改）
tools/               TypeDump、同步脚本
```

## 加载流程

`Plugin.Load()` 按顺序做这些事：

1. `Settings.Bind(Config)` 绑定所有配置项，并把 `Config.SettingChanged` 接到 `Settings.Dispatch`。
2. 用反射找出程序集里所有带 `[Feature]` 的类，按名字分组。同名的多个类属于同一个模块（例如 `FrameScaler` 和 `FastResponse` 共用 `FastResponse`）。
3. 对每个模块：给其中带 `[HarmonyPatch]` 的类安装补丁，再调用类上的静态 `Init()`（如果有）。任何一步抛异常，这个模块的补丁全部撤销，`Feature.Fail` 记日志，其他模块不受影响。
4. 挂上 `UltraModeBehaviour`（热键），然后 `Menu.SettingsPage.Register` 注册设置页。
5. 日志输出 `Ultra Mode x.y.z loaded; n/m features active: ...`。

## 功能模块（Feature）

```csharp
[Feature("Evasion")]
[HarmonyPatch]
internal static class Evasion
{
    private static readonly Feature Fix = Feature.Get("Evasion");
    ...
}
```

- 每个模块有自己的 Harmony 实例（id 为 `ultra_mode.belmontscurse.<名字>`），可以单独撤销。
- `Fix.Enabled`：模块第一次出错后变为 `false`，直到重启游戏。**每个补丁和事件回调的第一行都要检查它**，并把主体包在 `try { } catch (Exception ex) { Fix.Fail(ex); }` 里。异常不能逃出补丁进入 IL2CPP，否则可能直接崩游戏。
- `Settings.On`（总开关 F10）和模块自己的开关在每次使用时检查，不缓存，所以开关立即生效。

## 动作追踪（ActionTracker）

游戏的角色行为由 MotherBase.ActionFramework 驱动：`Actor` 持有当前运行的动作列表 `activeActions`（`ActionInstance`），每个动作对应一份 `ActionData` 资源，资源上挂着若干 `ActionFeature`（位移、无敌、弹反、输入条件等）。

`ActionTracker` 在 `Actor.UpdateState` 后置补丁里只处理主角，对比前后两帧的动作列表，发出三个事件：

| 事件 | 时机 |
| --- | --- |
| `Started(Entity, ActionData)` | 主角开始一个新动作 |
| `Stopped(Entity, ActionData)` | 一个动作结束 |
| `Updated(Entity, Actor)` | 每次主角状态更新，在 Started/Stopped 之后 |

大部分模块订阅 `Updated` 做每帧逻辑，而不是自己再挂补丁。这样补丁点少，也只在有主角时运行。

`ActionTracker` 自己也是一个模块。它失败时所有依赖它的模块都会停止工作（但不会出错）。

## 主角过滤（Player）

动作资源、Feature 资源和受击策略是主角和敌人共用的。任何补丁在修改结果前都要先确认对象属于主角：`Player.Is(entity)`、`Player.Owns(component)`。

`Player.Get()` 每帧只查一次；标题画面和加载中返回 `null`。比较对象一律用 `.Pointer`，见[开发文档](development.md#il2cpp-注意事项)。

## 修改游戏数据的两种模式

1. **从快照重算**：`FrameScaler` 第一次遇到一个动作时记下所有帧数字段的原值，之后每次都从原值按当前倍率算出新值写回。设置变化或关掉总开关时调用 `ReapplyAll()`，倍率为 1 时写回的就是原值。新增类似功能时用 `Settings.OnChanged` 注册重算。
2. **只改"我们的值"**：移动速度、镜头阻尼等运行时状态，游戏自己也会改（减速、水中、过场时归零）。这些地方只在当前值等于快照原值或者等于我们上次写入的值时才改写，否则视为游戏在用，保持不动。见 `FastResponse.Retarget`、`CameraFollow.Slot`。

## 设置

- 所有配置项都在 `Settings.cs`，节名带编号（`00 General` … `99 Debug`）以固定在 cfg 文件里的顺序。
- 配置描述写中文，因为 cfg 文件主要给中文玩家看。
- 改名或换节会让玩家原来的值变成孤儿行、回到默认值。确实要改名时在 README 里说明。
- 设置页的行定义在 `Menu/SettingsRows.cs`，三种设置页共用。详见[设置页文档](settings-page.md)。

## 错误隔离一览

| 层级 | 机制 |
| --- | --- |
| 安装时 | 目标方法不存在（游戏更新）→ 该模块撤销补丁，日志写明原因 |
| 运行时 | 补丁内异常 → `Fix.Fail`，该模块本次会话停用 |
| 热键 | `UltraModeBehaviour` 出错 → 组件停用，热键失效，功能不受影响 |
| 设置页 | 注册失败 → 只记日志，配置文件仍然可用 |
| 设置变更回调 | 每个回调单独 try/catch |
