# Ultra Mode（奥创模式）

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE-MIT)
[![License: 木兰宽松许可证 v2](https://img.shields.io/badge/License-%E6%9C%A8%E5%85%B0%E5%AE%BD%E6%9D%BE%E8%AE%B8%E5%8F%AF%E8%AF%81%20v2-blue.svg)](LICENSE-MulanPSL2)

[English](README.md) | 简体中文

《恶魔城：贝尔蒙特诅咒》（Castlevania: Belmont's Curse）的 BepInEx 6 IL2CPP 插件。在保留原作操作体系的前提下，提升操作响应、强化闪避与受身、扩展弹反、辅助钩锁、改善镜头和镜子的操作，用组合键直接释放塔罗牌法术，升级时回满状态，并可让魔导器获得后自动作为被动生效。

做这个模式的目的很简单：去掉操作上让人憋屈的地方，让大家玩得更尽兴、更开心。

> 非官方社区 mod，不包含任何游戏文件。使用前请备份存档。

## 功能

| 模块 | 说明 |
| --- | --- |
| 高速反应 | 提高移动速度和地面加减速；转身、急停、起步的过渡动作加速播放；攻击前摇和后摇分别按倍率压缩；钩锁出手、收回加速（直接改动作帧数据，判定帧和动画一起缩放） |
| 镜头 | 镜头跟随人物更快（被弹射、快速移动时不再慢一步）；右摇杆观察的范围和速度可调 |
| 闪避无敌 | 滑铲、后撤步的位移阶段不受伤害，并显示移速提升时的变色闪烁；之后恢复站姿的部分和原版一样会被打到（陷阱、即死区域、坠落伤害仍然有效） |
| 受身 | 被击飞期间（空中飞出、落地翻滚、倒地爬起）按跳跃立即停住并恢复操作，同时清掉击飞惯性，播放护盾魔导器的闪光特效 |
| 钩锁辅助 | 先按原版方向找目标（敌人和钩点都算）；这个方向上没有目标、附近只有一个钩得到的高亮钩点时，自动钩中它；钩点只在真正钩得到时才高亮。可选「无目标时不出钩锁」（默认关，避免被当成失灵） |
| 攻击弹反 | 解锁弹反后，普通攻击也会按弹反动作相同的帧窗口进行弹反；成功时保持攻击动作，只播放弹反特效，之后在原版弹反成功动作的时长内，敌人后续的命中无效 |
| 快速施法 | 不用切换装备，按组合键直接释放已获得的塔罗牌法术（见下文[快速施法](#快速施法)），消耗按那张牌自己的计算；需要按住的法术按住组合键即可。装备塔罗牌页面上，有快捷键的牌在描述末尾显示"按下/按住 组合键 快速施法"，按键用游戏自己的图标 |
| 时空飞冲无敌 | 时空飞冲从起跳到落地全程无敌（陷阱、即死区域、坠落伤害仍然有效）；设置在「奥创模式」的法术优化段 |
| 圣十字架撞墙不消失 | 撞墙继续弹射，回到身边时仍按原版回收；设置在「奥创模式」的法术优化段 |
| 快速操作 | 左右键换装、上下键换法术、快速施法；单独一页「奥创模式 - 快速操作」 |
| 升级回满 | 获得经验升级时，HP 和 MP 回满 |
| 经验雕像保证升级 | 击碎经验雕像后，掉落的经验不够升一级时自动补足，保证升级 |
| 镜子 | 镜子前按下立即进入，不需要按住；能传送的镜子前，交互提示改为「保存」（存档点）或「信息」，后面加上「下 传送」（用游戏自己的按键图标）；隐士任务后变成破碎状态的普通传送镜子恢复可用，快速传送不再被锁定（没用过的存档室镜子仍按原版不能传送） |
| 被动魔导器 | 开启了自动装备的魔导器获得即生效，不需要装进装备槽（只自动装备已经获得的，没获得的不会生效）；正面魔导器和带负面效果的魔导器分开开关，默认只自动装备正面的。没有自动装备的魔导器仍按原版手动装备 |
| 洞幽石常开 | 洞幽石的效果始终生效，没获得也一样：靠近时显示可破坏的隐藏墙壁（默认开，可在设置里关闭） |

每个模块单独安装，安装或运行时出错只会让该模块退回原版行为，原因写在 `BepInEx/LogOutput.log` 里，其他模块照常工作。

## 安装

需要 Windows x64 的 Steam 版游戏。[Releases](https://github.com/xiaohunqupo/castlevania-belmonts-curse-ultra-mode/releases) 里有两个包，选一个：

| 文件 | 适合 |
| --- | --- |
| `UltraMode-v<版本>-with-BepInEx.zip` | 还没装 BepInEx：整合包，内含官方原版 BepInEx 6 IL2CPP x64 be.788，一次装好 |
| `UltraMode-v<版本>.zip` | 已经装了 BepInEx 6 IL2CPP（be.788）或其他 mod：只有本 mod |

### 整合包（推荐新手）

1. 把 `UltraMode-v<版本>-with-BepInEx.zip` 解压到游戏根目录（与 `Castlevania Belmont's Curse.exe` 同级）。根目录会多出 `BepInEx/`、`dotnet/`、`winhttp.dll`、`doorstop_config.ini` 等。
2. 启动游戏。**第一次启动需要几分钟**，BepInEx 要下载 Unity 基础库并生成 interop 程序集，期间请保持联网，不要关闭游戏。之后启动就正常了。
3. 日志 `BepInEx/LogOutput.log` 里出现 `Ultra Mode ... loaded` 就说明装好了。

### 只装本 mod

1. 先装好 [BepInEx 6 IL2CPP x64（be.788）](https://builds.bepinex.dev/projects/bepinex_be)（选 `BepInEx-Unity.IL2CPP-win-x64`），或者游戏专用整合包 [BepinEx for Castlevania](https://www.nexusmods.com/castlevaniabelmontscurse/mods/3)，并运行过一次游戏。
2. 把 `UltraMode-v<版本>.zip` 解压到游戏根目录，得到 `BepInEx/plugins/UltraMode/UltraMode.dll`。
3. 启动游戏，同样以日志里的 `Ultra Mode ... loaded` 为准。

两种方式都不需要安装其他 mod。

### 卸载

- 只卸载本 mod：关闭游戏，删除 `BepInEx/plugins/UltraMode/`。配置文件 `BepInEx/config/ultra_mode.belmontscurse.cfg` 也可以一并删除。
- 连 BepInEx 一起卸载（用整合包装的）：删除游戏根目录下的 `BepInEx/`、`dotnet/`、`winhttp.dll`、`doorstop_config.ini`、`.doorstop_version`、`changelog.txt`。如果还装了其他 mod，它们也会一起被删除。

本 mod 不修改游戏文件，被动魔导器的效果也不写入存档，卸载后存档和原版一致。

## 设置

### 游戏内设置页

暂停菜单 → MODS → 奥创模式，可以开关各项功能、调节速度倍率，修改立即生效。这个页面放在哪个 MODS 页签上，按下面的顺序选一个，所以不会重复出现：

1. 装了 [Belmont Minimap](https://www.nexusmods.com/castlevaniabelmontscurse/mods/36)：用它的 MODS 页签（它按 `BelmontModSettings.ProviderV2` 约定读取本 mod 的设置）。
2. 单独装了 [Belmont UI](https://www.nexusmods.com/castlevaniabelmontscurse/mods/89)：注册到它的 MODS 页签。
3. 都没有：启动本 mod 内嵌的 Belmont UI，标题画面的设置里也有这个页签。

### 快捷键

| 按键 | 作用 | 默认 |
| --- | --- | --- |
| F10 | 开关整个奥创模式（`ToggleKey`） | 开启 |
| F11 | 把动作、魔导器、特效等运行时数据导出到 `BepInEx/ultra_mode_dump/`，用来调试（`DumpKey`） | 关闭 |

和其他 mod 的快捷键冲突时，可以在设置页关掉对应的快捷键（"快捷键开关奥创模式"），或者在配置文件里改 `ToggleKeyEnabled` / `DumpKeyEnabled`。想换成别的按键，就改 `ToggleKey` / `DumpKey`，例如 `F9`；组合键 Ctrl+F10 写成 `F10 + LeftControl`（主键写在最前面）。

### 配置文件

`BepInEx/config/ultra_mode.belmontscurse.cfg`，第一次启动后生成。每一项都有中文说明；下面只列分组：

| 分组 | 内容 |
| --- | --- |
| `00 General` | 总开关、切换快捷键及其开关 |
| `01 Fast Response` | 移动速度、起步与停止灵敏度、转身急停速度、攻击前摇与后摇、钩锁速度、镜子传送速度、镜子即按即进与提示、镜头跟随与右摇杆观察 |
| `02 Evasion` | 闪避无敌及其判定方式、受身及其特效 |
| `03 Parry` | 攻击弹反、复用的弹反动作 |
| `04 World` | 修复破碎镜子 |
| `05 Charms` | 自动装备正面 / 负面魔导器、负面魔导器名单、洞幽石常开 |
| `06 Whip` | 无目标时不出钩锁（默认关）、自动瞄准、只高亮钩得到的目标 |
| `07 Tarot` | 快速施法、各组合键对应的法术、法术优化（时空飞冲无敌、圣十字架撞墙不消失、十字键快捷换装） |
| `08 Recovery` | 升级回满、经验雕像保证升级 |
| `99 Debug` | 调试日志、导出快捷键（默认关闭，只能在配置文件里改） |

### 魔导器说明

- 两个开关：`PassiveCharms` 自动装备所有正面魔导器（默认开），`PassiveDrawbackCharms` 自动装备带负面效果的魔导器（默认关）。关闭某个开关后，对应的魔导器大约一秒内撤掉被动效果，回到原版的手动装备；已经装在槽位里的不受影响。
- 带负面效果的魔导器由 `DrawbackCharms`（资源名，逗号分隔）列出，默认是：

  | 魔导器 | 资源名 | 代价 |
  | --- | --- | --- |
  | 焦黑之心 | `Charm_GainHPHitMobNoPotion` | 无法使用治疗药水 |
  | 地狱徽记 | `Charm_ClampMCMana` | MP 上限不超过 80% |
  | 别西卜之印 | `Charm_BoostDamageButReduceLife` | HP 上限降低 50% |
  | 苍白之刺 | `Charm_BoostDamageBackReduceDamageFront` | 背后受到的伤害加倍 |
  | 亡灵指环 | `Charm_RoomChangeResetMobSpawner` | 离开房间后敌人立即重生 |

- 自动装备的魔导器仍然可以装进装备槽（只是显示用，不会叠加效果）；卸下也不会撤掉被动效果。
- `ForceRevealSecret`（默认开）让洞幽石 `Charm_RevealSecret` 的效果始终生效，没获得也一样：靠近时显示可破坏的隐藏墙壁。它不受上面两个自动装备开关影响。

### 快速施法

默认的组合键（手柄）：

| 组合键 | 法术 | 配置项 | 默认值 |
| --- | --- | --- | --- |
| 上 + Y | 时空飞冲 | `UpSpell` | `RiseAndFall` |
| 下 + Y | 地狱火葬 | `DownSpell` | `FireSeismicWave` |
| 按住 LT + Y | 彩绘玻璃护盾 | `LTSpell` | `GlassArmor` |
| R3 | 赦免；还没有赦免时为诅咒之火 | `R3Spells` | `MassPurification,CursedFire` |
| 按住 RB + Y | 石化凝视 | `RBSpell` | `PetrifyingRay` |
| 按住 RB + 下 + Y | 塔罗斯之拳 | `RBDownSpell` | `GiantFist` |
| 按住 RB + 上 + Y | 未指定（按 RB + Y 处理） | `RBUpSpell` | 空 |
| 按住 RB + R3 | 灵魂收割 | `RBR3Spells` | `LaserScytheSlash` |

- 只释放已经获得的牌；没获得时按原版处理（释放当前装备的牌）。
- 游戏内在 MODS 的「奥创模式」里「快速施法」一段修改：启用开关，左边塔罗牌、右边选组合键（牌名用游戏当前语言；进存档前显示内部名）。一个组合键只对应一张牌，给另一张牌选同一个时，原来那张变成「无」。
- 配置项填的是法术的内部名关键词，逗号分隔，第一个已获得的生效，所以把升级后的牌写在前面（例如赦免获得后代替诅咒之火）。全部内部名：`BloodSpikes`、`BoomerangCross`、`CursedFireBall`、`Detainment`、`FireSeismicWave`、`FreezingMist`、`GiantFist`、`GlassArmor`、`InvocationStone`、`LaserScytheSlash`、`LifeDrain`、`MassPurification`、`PetrifyingRay`、`PocketMirror`、`RiseAndFall`、`TargetingChargingWheels`。
- LT 原版是弹反，按住 LT 再按 Y 时，护盾和弹反同时生效，不用等弹反结束。
- RB 原版是交互键，站在可交互的东西旁边按 RB + Y 可能会同时触发交互。
- 赦免、灵魂收割这类需要按住的法术，用 R3 释放时按住 R3 即可，松开的效果和原版松开 Y 一样。

## 兼容性

- **Charms Unlimited**：和被动魔导器冲突，请只启用其中一个（关闭 `PassiveCharms` 和 `PassiveDrawbackCharms`，或者不装 Charms Unlimited）。
- **Belmont Minimap / Belmont UI**：可以一起使用，设置页会自动放到它们的 MODS 页签上，见上文。
- 游戏更新后，如果某个补丁的目标方法变了，只有对应的模块会退回原版，日志里会写明原因。

## 问题排查

- 第一次启动卡在 `Downloading unity base libraries from https://unity.bepinex.dev/libraries/2022.3.62.zip` 或下载失败：这是 BepInEx 在下载 Unity 引擎的基础库（Unity 的闭源程序集，不能随本 mod 分发）。可以用浏览器或其他工具手动下载这个链接，把 `2022.3.62.zip` 原样放进 `BepInEx/unity-libs/`（没有这个文件夹就新建，不用解压），再启动游戏，BepInEx 会直接使用它。游戏更新后版本号可能变化，以日志里显示的链接为准。
- 设置页或某个功能没有出现：查看 `BepInEx/LogOutput.log` 中 `Ultra Mode` 和 `[ui]` 开头的行。
- 提交问题时请附上 `LogOutput.log`、游戏版本和本 mod 的版本；需要的话在配置文件里把 `DebugLogging` 和 `DumpKeyEnabled` 改成 `true`，并按 F11 附上导出的数据。

## 构建

需要 .NET 6 或更高版本的 SDK，以及装好 BepInEx 并运行过一次的游戏（编译时引用它生成的 interop 程序集）。

每次编译都会打包出 `dist/UltraMode-v<版本>.zip`（内部结构为 `BepInEx/plugins/UltraMode/`）和整合包 `dist/UltraMode-v<版本>-with-BepInEx.zip`，然后复制到游戏目录。整合包第一次构建时会下载官方 BepInEx 到 `obj/bepinex/` 并校验 SHA-256；用 `-p:SkipFullPackage=true` 可以跳过。

```powershell
dotnet build -c Release                     # 编译、打包并复制到游戏目录（复制需要先关闭游戏）
dotnet build -c Release -p:SkipDeploy=true  # 只编译和打包
dotnet build -c Release -p:SkipPackage=true # 不打包
dotnet build -c Release -p:GamePath="D:\Games\Castlevania Belmont's Curse"
```

游戏不在默认 Steam 路径时，用 `-p:GamePath=...` 或环境变量 `BELMONTS_CURSE_DIR` 指定。

## 代码结构

参与开发前请先读 [维护文档](docs/README.md)（架构、各模块的实现细节、调试方法、新增功能步骤、测试清单、发布流程）和 [贡献指南](CONTRIBUTING.md)。

```
src/
  Plugin.cs                  入口：按模块安装补丁并调用各模块的 Init
  Feature.cs                 模块管理与出错回退
  Settings.cs                配置项
  Player.cs                  主角判定、组件查询
  ActionTracker.cs           追踪主角当前的动作（Actor.UpdateState 之后比对动作列表）
  ActionInfo.cs              动作组件查询缓存
  Inspector.cs               F11 运行时数据导出
  Features/
    FastResponse.cs          帧数据缩放器、移动速度与加减速、镜子即按即进
    CameraFollow.cs          镜头跟随（Cinemachine 阻尼）
    CameraLook.cs            右摇杆观察范围与速度（CameraOffset）
    Evasion.cs               闪避无敌（HitStrategy.GetHitResult，只在 DodgeMovementFeature 的位移阶段）
    EvasionBlink.cs          闪避无敌期间的变色闪烁
    TechRecovery.cs          受身
    WhipAssist.cs            钩锁辅助（WhipHookDetect.FindEntityTarget 探测、钩点高亮）
    AttackParry.cs           攻击时打开弹反窗口（ParryFeature.StartParry/StopParry）与成功后的保护时间
    MirrorRepair.cs          修复破碎镜子、解除快速传送锁定
    MirrorPrompt.cs          镜子前的「保存/信息」「下 传送」提示（InputTagTextPreprocessor）
    PassiveCharms.cs         被动魔导器、洞幽石常开
    TarotShortcuts.cs        快速施法（UseItem.GetActionData 替换法术动作，消耗按替换后的牌）
    TarotHint.cs             装备塔罗牌页面的快速施法说明
    DiveInvulnerable.cs      时空飞冲无敌（HitStrategy.GetHitResult）
    CrossWallBounce.cs       圣十字架撞墙不消失
    QuickEquip.cs            十字键快捷换装
    LevelUpRefill.cs         升级回满（ExperienceComponent.IncrementCurrentExperience）
    StatueLevelUp.cs         经验雕像保证升级（ExperienceLootData.Drop）
  Menu/
    SettingsRows.cs          设置页的全部行（唯一的定义处，各菜单共用）
    SettingsPage.cs          选择 Minimap、独立 Belmont UI 或内嵌 Belmont UI 来显示设置页
    MinimapProvider.cs       Belmont Minimap 的 ProviderV2（单页三段前缀）
    TarotPage.cs             快速施法段：每张牌一个组合键列表
    SpellPage.cs             法术优化段
    EmbeddedBelmontUi.cs     内嵌 Belmont UI 的启动入口（代替上游的 Plugin.cs）
vendor/BelmontUI/            Belmont UI 上游源码（MIT），由脚本生成，不要手改
tools/TypeDump/              从 interop 程序集导出类型和成员
tools/sync-belmontui.ps1     同步 Belmont UI 上游源码到 vendor/BelmontUI
tools/release.ps1            一键发布：改版本号、编译打包、打标签、发布 GitHub Release
tools/fetch-bepinex-licenses.ps1  下载整合包里 BepInEx 各组件的许可证到 third_party/BepInEx
third_party/BepInEx/         整合包附带的第三方声明和许可证
docs/                        维护文档
```

### 内嵌的 Belmont UI

`vendor/BelmontUI/` 是 [leandrobueno/BelmontUI](https://github.com/leandrobueno/BelmontUI) 的源码，版本和提交号记录在 `vendor/BelmontUI/UPSTREAM`，授权见同目录的 `LICENSE`（打包时以 `BelmontUI-LICENSE.txt` 随 dll 发布）。同步脚本只做机械修改，所以这个目录里不放手写代码：

- 命名空间 `BelmontUi` 改成 `UltraModeVendor.BelmontUi`，避免和单独安装的 Belmont UI 冲突；
- 每个文件开头加 `#nullable disable`（上游没有可空注解）；
- 不复制 `Plugin.cs`：内嵌副本不能注册成 BepInEx 插件，否则会占用独立版的 GUID。启动入口在 `src/Menu/EmbeddedBelmontUi.cs`。

升级上游：

```powershell
git -C reference/BelmontUI pull          # 或删掉 reference/BelmontUI，脚本会重新克隆
powershell -File tools/sync-belmontui.ps1
```

然后编译。如果上游改了 `Plugin.cs` 的启动逻辑，或者改了 `SettingsPage.cs` 用到的 API（`Settings.Register`、`SettingsMenu.Toggle/Slider/ResetButton`、`LocalizedText`），需要手动同步到 `src/Menu/`。

## 许可证

Copyright (c) 2026 Danny许（[@xiaohunqupo](https://github.com/xiaohunqupo)）

双许可，以下两种任选其一：

- [MIT 许可证](LICENSE-MIT)
- [木兰宽松许可证，第 2 版（Mulan PSL v2）](LICENSE-MulanPSL2)

木兰宽松许可证是由中国开源社区制定的宽松型开源许可证，中英文双语表述、具有同等法律效力，并已通过 [OSI](https://opensource.org/license/mulanpsl-2-0) 认证。它和 MIT 一样允许自由使用、修改和再分发，另外明确授予了专利许可。详见 [license.coscl.org.cn/MulanPSL2](http://license.coscl.org.cn/MulanPSL2)。

SPDX 标识：`MIT OR MulanPSL-2.0`。除非另有声明，提交到本项目的贡献也按同样的条款授权。

内嵌的 Belmont UI 保留其自身的 [MIT 许可证](vendor/BelmontUI/LICENSE)。整合包还原样包含官方 BepInEx 及其组件，它们各自遵循自己的许可证（LGPL-2.1、LGPL-3.0、MIT、Apache-2.0），详见 [third_party/BepInEx/NOTICE.md](third_party/BepInEx/NOTICE.md)，随包放在 `BepInEx/third-party/`。

本项目是非官方的社区作品，与 Konami 及游戏开发商无关。《恶魔城》及相关名称、素材归其各自的权利人所有。

## 致谢

- [BepInEx](https://github.com/BepInEx/BepInEx)、Il2CppInterop、HarmonyX
- [leandrobueno/BelmontUI](https://github.com/leandrobueno/BelmontUI)（MIT，内嵌为设置页）
- [ollieatkinson/castlevania-belmonts-curse-ultrawide](https://github.com/ollieatkinson/castlevania-belmonts-curse-ultrawide)
- KZekai 制作的游戏专用 BepInEx 整合包
