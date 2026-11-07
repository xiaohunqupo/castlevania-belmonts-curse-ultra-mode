# 新增一个功能

以"新增一个开关控制的功能 `Foo`"为例。

## 1. 配置项

在 `src/Settings.cs` 里声明并绑定：

```csharp
internal static ConfigEntry<bool> Foo = null!;
...
Foo = cfg.Bind("02 Evasion", "Foo", true, "中文说明：做什么，有什么例外。");
```

- 放进最合适的已有节；确实需要新节时按编号插入（例如 `07 Xxx`）。
- 浮点值用 `ConfigDescription` + `AcceptableValueRange`，范围和设置页滑条一致。
- 默认值要保守：会改变游戏难度或有副作用的功能默认关闭。

## 2. 模块类

在 `src/Features/Foo.cs` 新建：

```csharp
using HarmonyLib;
using MotherBase.EntityFramework;

namespace UltraMode.Features;

/// <summary>
/// 用英文说明：改了游戏的什么、挂在哪里、为什么这样改不会破坏原版其他部分。
/// </summary>
[Feature("Foo")]
[HarmonyPatch]
internal static class Foo
{
    private static readonly Feature Fix = Feature.Get("Foo");

    private static bool Active => Fix.Enabled && Settings.On && Settings.Foo.Value;

    internal static void Init() => ActionTracker.Updated += OnUpdated;

    private static void OnUpdated(Entity e, Actor actor)
    {
        if (!Active) return;
        try
        {
            ...
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }

    [HarmonyPatch(typeof(SomeGameType), nameof(SomeGameType.SomeMethod)), HarmonyPostfix]
    private static void SomeMethod(SomeGameType __instance, Entity e, ref bool __result)
    {
        if (!Active || !Player.Is(e)) return;
        try
        {
            ...
        }
        catch (Exception ex) { Fix.Fail(ex); }
    }
}
```

要点：

- `[Feature]` 的名字和 `Feature.Get` 的名字必须一致。`Plugin.Load` 会自动发现这个类，不需要手动注册。
- 只用事件、不打补丁的模块可以去掉 `[HarmonyPatch]`。
- 能用 `ActionTracker` 事件解决的就不要新增补丁。
- 补丁里先检查开关、再检查 `Player`，最后才做有开销的事。
- 改了游戏资源的，要保证关掉开关或总开关后能恢复原值；需要在设置变化时重算的，在 `Init()` 里用 `Settings.OnChanged(重算, Settings.Enabled, Settings.Foo)`。
- 每帧都会触发的日志用 `Log.Debug`。

不清楚游戏类型和方法时，见[开发文档](development.md#查找游戏类型typedump)。

## 3. 设置页

在 `src/Menu/SettingsRows.cs` 的 `All()` 里按页面顺序加一行：

```csharp
Toggle(Settings.Foo, "中文标题", "English label", "中文提示", "English hint"),
```

滑条用 `Slider(entry, min, max, step, ...)`。三种设置页（Minimap、独立 Belmont UI、内嵌 Belmont UI）都读这一份，不需要分别改。

文字要能被游戏字体渲染（见[开发文档](development.md#il2cpp-注意事项)），中文标题尽量控制在 10 个字以内。

## 4. 文档

- `README.md` 和 `README.zh-CN.md`：功能表加一行；配置节表格里补上新配置项；有兼容性问题的写进"兼容性"。
- `docs/features.md`：加一节，写清楚挂在哪里、改了什么、注意事项。
- `docs/testing.md`：加上需要手动验证的内容。
- 新文件写进 README 的"代码结构"一节。

## 5. 验证

- `dotnet build -c Release` 无警告。
- 进游戏，日志里 `n/m features active` 包含新模块。
- 开 `DebugLogging` 验证行为；分别关掉功能开关和 F10 总开关，确认回到原版。
- 按[测试清单](testing.md)回归相关功能。
