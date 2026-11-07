# Ultra Mode（奥创模式）

《恶魔城：贝尔蒙特诅咒》（Castlevania: Belmont's Curse）的 BepInEx 6 IL2CPP 插件。在保留原作操作体系的前提下，提升操作响应、强化闪避与受身、扩展弹反、辅助钩锁、改善镜头和镜子的操作，并可让魔导器获得后自动作为被动生效。

做这个模式的目的很简单：去掉操作上让人憋屈的地方，让大家玩得更尽兴、更开心。

> 非官方社区 mod，不包含任何游戏文件。使用前请备份存档。

## 功能

| 模块 | 说明 |
| --- | --- |
| 高速反应 | 提高移动速度和地面加减速；转身、急停、起步的过渡动作加速播放；攻击前摇和后摇分别按倍率压缩；钩锁出手、收回加速（直接改动作帧数据，判定帧和动画一起缩放） |
| 镜头 | 镜头跟随人物更快（被弹射、快速移动时不再慢一步）；右摇杆观察的范围和速度可调 |
| 闪避无敌 | 滑铲、后撤步的位移阶段不受伤害，并显示移速提升时的变色闪烁；之后恢复站姿的部分和原版一样会被打到（陷阱、即死区域、坠落伤害仍然有效） |
| 受身 | 被击飞期间（空中飞出、落地翻滚、倒地爬起）按跳跃立即停住并恢复操作，同时清掉击飞惯性，播放护盾魔导器的闪光特效 |
| 钩锁辅助 | 先按原版方向找目标（敌人和钩点都算）；这个方向上没有目标、附近只有一个钩得到的高亮钩点时，自动钩中它；什么都钩不到时按钩锁不出手、不打断动作；钩点只在真正钩得到时才高亮 |
| 攻击弹反 | 解锁弹反后，普通攻击也会按弹反动作相同的帧窗口进行弹反；成功时保持攻击动作，只播放弹反特效，之后在原版弹反成功动作的时长内，敌人后续的命中无效 |
| 镜子 | 镜子前按下立即进入，不需要按住；能传送的镜子前，交互提示后面加上「下 交互」（用游戏自己的按键图标）；隐士任务后变成破碎状态的普通传送镜子恢复可用，快速传送不再被锁定（没用过的存档室镜子仍按原版不能传送） |
| 被动魔导器 | 开启了自动装备的魔导器获得即生效，不需要装进装备槽（只自动装备已经获得的，没获得的不会生效）；正面魔导器和带负面效果的魔导器分开开关，默认只自动装备正面的。没有自动装备的魔导器仍按原版手动装备 |
| 洞幽石常开 | 洞幽石的效果始终生效，没获得也一样：靠近时显示可破坏的隐藏墙壁（默认开，可在设置里关闭） |

在暂停菜单 → MODS → 奥创模式 里可以开关各项功能、调节速度倍率，不需要装别的 mod。这个页面放在哪个 MODS 页签上，按下面的顺序选一个，所以不会重复出现：

1. 装了 [Belmont Minimap](https://www.nexusmods.com/castlevaniabelmontscurse/mods/36)：用它的 MODS 页签（它按 `BelmontModSettings.ProviderV2` 约定读取本 mod 的设置）。
2. 单独装了 [Belmont UI](https://www.nexusmods.com/castlevaniabelmontscurse/mods/89)：注册到它的 MODS 页签。
3. 都没有：启动本 mod 内嵌的 Belmont UI，标题画面的设置里也有这个页签。

配置文件是 `BepInEx/config/ultra_mode.belmontscurse.cfg`，修改会实时生效。游戏内按 **F10** 开关整个模式，按 **F11** 把运行时数据导出到 `BepInEx/ultra_mode_dump/`，用来调试。

每个模块单独安装，安装或运行时出错只会让该模块退回原版行为，原因写在 `BepInEx/LogOutput.log` 里。

### 注意

- **被动魔导器**和 Charms Unlimited 会冲突，请只启用其中一个。被动效果不会写入存档，所以卸载本 mod 后存档里的状态和原版一致。
- 两个开关：`PassiveCharms` 自动装备所有正面魔导器（默认开），`PassiveDrawbackCharms` 自动装备带负面效果的魔导器（默认关）。关闭某个开关后，对应的魔导器大约一秒内撤掉被动效果，回到原版的手动装备；已经装在槽位里的不受影响。
- 带负面效果的魔导器由 `DrawbackCharms`（资源名，逗号分隔）列出，默认是焦黑之心 `Charm_GainHPHitMobNoPotion`（无法使用治疗药水）、地狱徽记 `Charm_ClampMCMana`（MP 上限不超过 80%）、别西卜之印 `Charm_BoostDamageButReduceLife`（HP 上限降低 50%）、苍白之刺 `Charm_BoostDamageBackReduceDamageFront`（背后受伤加倍）和亡灵指环 `Charm_RoomChangeResetMobSpawner`（离开房间后敌人立即重生）。
- 自动装备的魔导器仍然可以装进装备槽（只是显示用，不会叠加效果）；卸下也不会撤掉被动效果。
- `ForceRevealSecret`（默认开）让洞幽石 `Charm_RevealSecret` 的效果始终生效，没获得也一样：靠近时显示可破坏的隐藏墙壁。它不受上面两个自动装备开关影响。

## 安装

1. 安装 BepInEx 6 IL2CPP x64（be.788）或者 [BepinEx for Castlevania](https://www.nexusmods.com/castlevaniabelmontscurse/mods/3)，然后运行一次游戏。
2. 把 `UltraMode-v<版本>.zip` 解压到游戏根目录（与游戏 exe 同级），得到 `BepInEx/plugins/UltraMode/UltraMode.dll`。

## 构建

每次编译都会打包到 `dist/UltraMode-v<版本>.zip`（内部结构为 `BepInEx/plugins/UltraMode/`），然后复制到游戏目录。

```powershell
dotnet build -c Release                     # 编译、打包并复制到游戏目录（复制需要先关闭游戏）
dotnet build -c Release -p:SkipDeploy=true  # 只编译和打包
dotnet build -c Release -p:SkipPackage=true # 不打包
dotnet build -c Release -p:GamePath="D:\Games\Castlevania Belmont's Curse"
```

## 代码结构

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
    MirrorPrompt.cs          镜子前的「下 交互」提示（InputTagTextPreprocessor）
    PassiveCharms.cs         被动魔导器、洞幽石常开
  Menu/
    SettingsRows.cs          设置页的全部行（唯一的定义处，各菜单共用）
    SettingsPage.cs          选择 Minimap、独立 Belmont UI 或内嵌 Belmont UI 来显示设置页
    MinimapProvider.cs       Belmont Minimap 的 ProviderV2 约定
    EmbeddedBelmontUi.cs     内嵌 Belmont UI 的启动入口（代替上游的 Plugin.cs）
vendor/BelmontUI/            Belmont UI 上游源码（MIT），由脚本生成，不要手改
tools/TypeDump/              从 interop 程序集导出类型和成员
tools/sync-belmontui.ps1     同步 Belmont UI 上游源码到 vendor/BelmontUI
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

## 致谢

- [BepInEx](https://github.com/BepInEx/BepInEx)、Il2CppInterop、HarmonyX
- [leandrobueno/BelmontUI](https://github.com/leandrobueno/BelmontUI)（MIT，内嵌为设置页）
- [ollieatkinson/castlevania-belmonts-curse-ultrawide](https://github.com/ollieatkinson/castlevania-belmonts-curse-ultrawide)
- KZekai 制作的游戏专用 BepInEx 整合包
