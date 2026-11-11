# 设置页与 Belmont UI

## 选择逻辑

`src/Menu/SettingsPage.cs` 在插件加载时按下面的顺序选一个，保证设置页只出现一次：

1. **装了 Belmont Minimap**（GUID `local.belmont.minimap`）：什么都不做。Minimap 自己会通过反射找到 `BelmontModSettings.ProviderV2` 并显示设置页。
2. **单独装了 Belmont UI**（GUID `leandrobueno.belmontui`）：通过反射调用它的 API 注册设置页。用反射是为了让本 mod 在没有 `BelmontUI.dll` 时也能加载。
3. **都没装**：启动内嵌的 Belmont UI（`vendor/BelmontUI`），用它的 API 注册。

`Plugin.cs` 对两个 GUID 声明了 `SoftDependency`，保证它们存在时先于本 mod 加载，`IL2CPPChainloader.Instance.Plugins` 里才查得到。

日志会写出选了哪一种：`Settings page: on ...`。

## 行定义

所有页面共用 `src/Menu/SettingsRows.cs`：

- `SettingsRow(Entry, LabelZh, LabelEn, HintZh, HintEn, Min, Max, Step)`：`Max > Min` 时是滑条，否则是开关。
- `TitleZh/En`、`SummaryZh/En`、`SliderFormat`。
- `Save()`：保存 cfg 文件。`ResetAll()`：把本 mod 的所有配置项恢复默认值，包括不在页面上的（热键、关键词列表）。

## Belmont Minimap 契约

`src/Menu/MinimapProvider.cs`。命名空间 `BelmontModSettings`、类名 `ProviderV2`、静态方法 `Describe()` 的返回结构都是 Minimap 的约定，**不能改名**：

```
object[] {
  string id, string titleZh, string titleEn, string summaryZh, string summaryEn,
  object[] items,      // 每项是 Dictionary<string, object>
  Action save, Action reset,
}
```

每个 item：`entry`（ConfigEntryBase）、`type`（`"bool"` / `"float"`）、`labelZh/En`、`hintZh/En`，滑条另有 `min`、`max`、`step`、`format`。

Minimap 的文字如果有游戏字体渲染不了的字，整行会被跳过。每个 provider 最多 32 项，超出时整页被丢弃。

Minimap 第三方 mod 不能做子标签，因此用三个 `ProviderV2`（主程序集 + `ExtraProviders` 生成的两个内存程序集）对应 MODS 列表里三行：奥创模式、快速操作、法术优化。每页最多 32 项。

Belmont UI 对应三次 `Register`。

快速操作页（`src/Menu/TarotPage.cs`，id `…actions`）：左右键换装、上下键换法术、快速施法开关，以及每张牌的组合键 `enum` 行；视图配置在 `BepInEx/cache/ultra_mode.tarot-page.cfg`。

## 独立 Belmont UI（反射）

`SettingsPage.Standalone` 用到的上游 API：

| 类型 | 成员 |
| --- | --- |
| `BelmontUi.Settings` | `static SettingsMenu Register(string guid, LocalizedText title, Action<SettingsMenu> build, LocalizedText summary)`（`build` 传 null，在返回的 menu 上逐行添加） |
| `BelmontUi.SettingsMenu` | `Toggle(ConfigEntryBase, LocalizedText, LocalizedText)`、`Slider(ConfigEntryBase, LocalizedText, float, float, float, string, LocalizedText)`、`Header(LocalizedText)`、`Choice(...)`、`Button(...)` |
| `BelmontUi.LocalizedText` | 构造函数 `(string en)`、索引器 `[string lang]` |

找不到任何一个都会抛异常，被 `Register` 捕获后只记日志。上游改了这些签名时，这里和内嵌版的 `SettingsPage.Embedded` 要一起改。

`DefaultReset` 里的三段英文是上游识别的默认文字，传这三段时上游会换成它自己的多语言翻译。

## 内嵌 Belmont UI

### 为什么这样做

Belmont UI 是 MIT 许可。内嵌后，玩家不需要另装任何 mod 就有设置页。

- 命名空间改成 `UltraModeVendor.BelmontUi`，和独立安装的 Belmont UI 不冲突。
- 不包含上游的 `Plugin.cs`：内嵌副本不能注册成 BepInEx 插件，否则会占用独立版的 GUID。由 `src/Menu/EmbeddedBelmontUi.cs` 里的 `BelmontUiPlugin.Start` 代替它启动 `BelmontUiWatcher`。
- 每个文件加 `#nullable disable`，因为本项目开启了可空检查而上游没有标注。

### 同步上游

```powershell
git -C reference/BelmontUI pull          # 或删掉 reference/BelmontUI，脚本会重新克隆
powershell -File tools/sync-belmontui.ps1
```

脚本会清空并重新生成 `vendor/BelmontUI/*.cs`，复制 `LICENSE`，并把版本和提交号写进 `vendor/BelmontUI/UPSTREAM`。**不要手改 `vendor/BelmontUI` 里的文件**，下次同步会被覆盖。

同步后：

1. 把 `src/Menu/EmbeddedBelmontUi.cs` 里的 `Version` 改成 `UPSTREAM` 里的版本号。
2. 对比上游 `Plugin.cs` 的 `Load()`，如果它启动库的方式变了（比如新增了组件或初始化步骤），同步到 `BelmontUiPlugin.Start`。
3. 检查上表的 API 是否变化。
4. 编译，分别在"什么都没装"和"单独装了 Belmont UI"两种情况下进游戏看设置页。

打包时 `vendor/BelmontUI/LICENSE` 会以 `BelmontUI-LICENSE.txt` 的名字放进 zip，满足 MIT 的署名要求。
