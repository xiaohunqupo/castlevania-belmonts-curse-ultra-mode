# 功能模块参考

每个模块列出：做什么、挂在哪里（Harmony 补丁或 `ActionTracker` 事件）、读写了哪些游戏数据、相关配置项，以及维护时要注意的地方。模块名就是日志和 `[Feature("...")]` 里的名字。

游戏更新后某个模块失效时，先看日志里该模块的报错，再对照这里的"挂在哪里"用 TypeDump 检查方法签名是否变化。

---

## ActionTracker

`src/ActionTracker.cs`。基础设施，见[架构](architecture.md#动作追踪actiontracker)。

- 补丁：`Actor.UpdateState` 后置。
- 只处理主角；记录 `Seen` / `SeenData` 供 F11 导出。

## FastResponse（含 FrameScaler）

`src/Features/FastResponse.cs`。加快移动、攻击、钩锁、转身急停和镜子相关动作。

- 挂在：`ActionTracker.Started`（遇到新动作时注册缩放）、`ActionTracker.Updated`（扫描镜子动作、转身反应、调整移动参数）。无 Harmony 补丁。
- **FrameScaler**：通过反射找出 `ActionData`、其 `WeaponActionData` 子类以及所有 Feature（含一层嵌套列表）上名字含 `frame` 的 int 字段，从原值重新映射。第一次命中帧（`firstHitFrame`）之前用前摇倍率，之后用后摇倍率；同时按结束帧缩短的比例调高 `animTimeScale`，让动画和命中帧对齐。
  - `excluded` 里的字段（硬直、无敌时长、慢动作、版本号）不缩放。新发现有字段不该缩放时加到这里。
  - `claimed`：多个动作共用的 Feature 对象只缩放一次。
  - `pinned`：个别字段固定为某个值而不是按比例缩放（即按即进镜子用）。
- **哪些动作会被缩放**：攻击（`WeaponActionData`）、名字以 `MC_Whip` 开头且有结束帧的动作、主角移动 Feature（`McMovementFeature`）上的转身/加速/减速/靠墙反应、`MC_Crouch` / `MC_Crouch_Start`、镜子动作（名字含 `MirrorInteract` 或以 `GPE_MirrorBase_` 开头）。
- **即按即进镜子**：`MC_Crouch` 只在下蹲第 30 帧左右的一帧窗口里检测脚下的镜子，`MC_Crouch_MirrorInteract_Tp` 有按住判定和交互锁定。这些字段被固定到第 1~2 帧。镜子动作在第一次进入前就要处理好，所以有主角时会主动扫描一遍所有镜子动作。
- **移动参数**：`CharacterMovementState.movementSpeeds` 和地面加减速锐度，按"只改我们的值"的规则调整（游戏的减速效果、水中等不受影响）。
- 配置：`01 Fast Response` 下除镜头三项外的全部。

## CameraFollow

`src/Features/CameraFollow.cs`。镜头跟随更快。

- 挂在：`ActionTracker.Updated`。
- 当前激活的 `CinemachineVirtualCamera` 上，`CinemachineFramingTransposer` / `CinemachineTrackedDolly` 的 XYZ 阻尼和 `CinemachineConfiner2D` 的阻尼除以倍率。每个房间有自己的虚拟镜头，按"只改我们的值"处理（过场时游戏会把阻尼置零）。
- 配置：`CameraFollowSpeed`。

## CameraLook

`src/Features/CameraLook.cs`。右摇杆观察的范围和速度。

- 挂在：`ActionTracker.Updated`。
- `CameraOffset.maxDistance` 按快照放大；`currentValue` 每帧的变化量（由原生代码缓动）乘以速度倍率，回正时不会越过 0。
- 配置：`CameraLookRange`、`CameraLookSpeed`。

## Evasion

`src/Features/Evasion.cs`。滑铲、后撤步的位移阶段无敌。

- 补丁：`HeroHitStrategy.GetHitResult`、`DefaultHitStrategy.GetHitResult` 后置，把 `Success` 改成 `Ignore`。
- 闪避判定：动作名含 `EvasionActionKeywords` 里的关键词，或带 `DodgeMovementFeature`。无敌只到 `DodgeMovementFeature.impulsionEndFrame`，之后的收招和原版一样会被打。
- 陷阱、即死区域、坠落伤害（`DamageFlag.TrapDamage | DeathZone | FallDamage`）不受影响。
- 配置：`02 Evasion` 的 `Invulnerable`、`ActionKeywords`、`DetectByDodgeFeature`。

## EvasionBlink

`src/Features/EvasionBlink.cs`。无敌期间播放加速魔导器的闪光（`Charm_BoostSpeed_Feedback`），让玩家看得到无敌窗口。

- 挂在：`ActionTracker.Updated`；用 `Evasion.InWindow` 判断。
- `FeedbackUtils.StartFeedback` / `StopFeedback`。这是循环特效，必须在窗口结束时停止。
- 跟随 `Invulnerable` 开关，没有单独配置项。

## TechRecovery

`src/Features/TechRecovery.cs`。被击飞时按跳跃受身。

- 挂在：`ActionTracker.Updated`。
- 击飞动作：名字以 `MC_Projection` 开头（含落地翻滚、起身）。按下跳跃时 `Actor.StopAction` 结束它，并清掉击飞速度（`ActionVelocity`），之后两帧继续清水平速度，避免跳跃继承击飞的惯性。
- 跳跃键：从主角 `PlayerInput` 的绑定里找名字含 `jump` 的 `InputAction`。
- 过场或脚本禁用输入时不触发：`RedWineDisableInputFlag.flagCounter` 超过当前动作自己的 `LockControlsFeature` 数量，说明有外部锁定。
- 击飞动作在空中时自己的帧号一直是 0，所以经过的帧数由模块自己计数。
- 特效：`TechFeedback` 指定的 Feedback；找不到时从护盾类魔导器上找一个有固定时长的特效。
- 配置：`TechRecovery`、`TechFeedback`。

## AttackParry

`src/Features/AttackParry.cs`。解锁弹反后，普通攻击也能弹反。

- 原版弹反动作（`ParryActionName`，默认 `MC_VaniaLight_Parry`）在第 2~7 帧通过三个 Feature 生效：`ParryFeature`、`ChangeHitReceptionStrategy`（换成 `VaniaLightHitStrategy`）、`ToggleColliderListFeature`（弹反判定范围）。本模块在攻击的同一帧窗口里直接驱动这三样。
- 挂在：`ActionTracker.Started`（主角用过一次弹反即视为已解锁）、`ActionTracker.Updated`（开关窗口）。
- 补丁：
  - `ParryState.OnApplyParry` 前置/后置：攻击触发的弹反成功时，不切换到弹反成功动作、不打断攻击，只播放成功动作上的特效（`FeedbackLauncherFeature`）。
  - 两个 `GetHitResult` 后置：弹反成功后，在原版成功动作持续的时间内忽略后续命中，否则敌人的连段会被反复弹反。
- 是否解锁：每 30 帧调用一次弹反动作的 `CanStart`。
- 配置：`03 Parry`。

## WhipAssist

`src/Features/WhipAssist.cs`。钩锁辅助：无目标不出钩、自动瞄准、只高亮钩得到的锚点。

- 锚点高亮由 `DistanceReactionSystem` 根据距离调用 `WhipDistanceComponent.AddFeedback` / `RemoveFeedback`，不考虑墙壁遮挡。本模块对每个高亮锚点用游戏自己的目标搜索 `WhipHookDetect.FindEntityTarget` 朝它探测一次（`Probe`，探测前后保存并恢复钩锁状态），结果用于三件事。
- 补丁：
  - `WhipDistanceComponent.AddFeedback` / `RemoveFeedback` 前置：钩不到的锚点不亮。
  - `WhipHookDetect.CanStart` 前置：原方向有目标 → 原版；否则只有一个可达锚点 → 自动瞄准；都没有 → 不出钩。
  - `WhipHookDetect.GetAimDirection` 后置：探测时和自动瞄准时改写方向。
  - `WhipHookDetect.OnStart` / `OnStop` 后置：钩锁进行中冻结可达性计算。
- 锚点每 60 帧扫描一次场景。
- 配置：`06 Whip`。

## MirrorRepair

`src/Features/MirrorRepair.cs`。隐士任务后，存档室的传送镜子恢复，快速传送解锁。

- 补丁：`FastTravelMenuManager.IsDeathTrapActive` 后置：返回 `false`，解除快速传送锁定。
- **不能给 `GPE_Mirror.CanUseMirror` 打补丁**：这个函数太短，原生 detour 搬走开头指令时会把里面的短跳转搬坏。曾经的后置补丁让镜之厅最上层那面镜子一被检查就崩溃（`GameAssembly.dll` 里 `0x80000003`，落在 `CanUseMirror` 内部）。
- 场景替换：剧情推进后存档室会显示一面"破碎"的镜子，原来的镜子仍在场景里但被隐藏。只处理 `SavePoint` 下面的破镜子，并且要求两面镜子的最近公共祖先下只有它们两面；镜之厅中枢里按目的地分组的镜子不动。找到后隐藏破碎分支、激活原分支，关掉开关时当场还原。房间加载后的 120 帧内每帧检查，让替换发生在转场中；之后每 60 帧检查一次。
- 没用过的存档室镜子仍然不能传送（原版规则）；暗影镜子等非 `BaseMirror` 不处理。
- 配置：`RepairMirrors`。

## MirrorPrompt

`src/Features/MirrorPrompt.cs`。站在可传送的镜子前时，交互提示后面加上"下 交互"。

- 挂在：`ActionTracker.Updated`。
- 找到源文本里带 `<input="...Interact...">` 标签的 `InputTagTextPreprocessor`，复制一份标签指向移动操作的"下"方向，再交给游戏自己的 `PreprocessText` 转成当前设备的按键图标。
- "下"方向的标签写法在数据里看不到，所以 `DownTag` 会生成几种候选写法，让游戏的 `GetActionFromInputTag` 解析，第一个解析到 down 的生效，结果按输入资源缓存。
- 离开镜子、关闭选项时恢复原文本。切换语言或输入设备时游戏会重写源文本，所以每次都重新读取。
- 配置：`MirrorPrompt`。

## PassiveCharms

`src/Features/PassiveCharms.cs`。拥有的魔导器自动生效；强制开启洞幽石。

- 挂在：`ActionTracker.Updated`（每 60 帧 `Sync` 一次）。
- 被动装备：对每个应当被动的魔导器调用游戏自己的 `CharmItemData.OnEquipItem`，记在 `applied` 里。效果（包括负面效果）完全按游戏定义生效，存档里不会写入。
- 补丁：
  - `CharmItemData.OnEquipItem` / `OnUnequipItem` 前置：被动魔导器不会被重复装备，从槽位卸下时也不会撤掉被动效果。
  - `Inventory.HasCharmEquipped` 后置：被动魔导器报告为已装备，洞幽石（`Charm_RevealSecret`）始终报告为已装备。
  - `Inventory.HasItemEquipped` 两个重载后置：洞幽石。
  - `InventoryCharmsMenuManager.OnOpened` 后置：记下魔导器菜单。
  - `Inventory.EquipCharmFromInventory` / `CanEquipCharm` / `UnequipCharm` 前置 + Finalizer：`slotQueries` 计数。
- **为什么要 `slotQueries` 和 `MenuOpen`**：魔导器菜单用 `HasCharmEquipped` 判断点击是"装上"还是"卸下"。如果被动魔导器一律报告为已装备，玩家就再也装不进槽位。所以菜单打开时、以及背包自己的装备逻辑运行时，`HasCharmEquipped` 返回原版结果。
- 负面魔导器列表：`DrawbackCharms`（资源名）。新增负面魔导器时，同时更新 `Settings.cs` 的默认值和说明、`SettingsRows.cs` 的提示文字、两份 README 的魔导器表。
- 配置：`05 Charms`。
- 与 Charms Unlimited 冲突（两者都改装备逻辑）。
