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
- **FrameScaler**：通过反射找出 `ActionData`、其 `WeaponActionData` 子类以及所有 Feature 上名字含 `frame` 的 int 字段，从原值重新映射。第一次命中帧（`firstHitFrame`）之前用前摇倍率，之后用后摇倍率；同时按结束帧缩短的比例调高 `animTimeScale`，让动画和命中帧对齐。
  - 嵌套收集：`List`1`、`Il2CppReferenceArray`1`、`Il2CppStructArray`1` 都会走进一层。命中框（`TimedHurtbox` / `SimplifiedHurtbox`）和蓄力台阶（`ChargeStep.minChargeFrame`，值类型，写回数组槽位）必须一起缩放；以前只走 `List` 且跳过值类型，会造成位移/取消帧压缩了、判定和松手阈值仍是原版，表现为点按不出招、连点卡顿、推开敌人却无伤。
  - `*AfterFirstHit` / `lockBeforeFirstHitFrame` / `lockAfterFirstHitFrame` 按相对偏移缩放，不用绝对时间轴映射。
  - 圆整后若 `startFrame`/`endFrame`（及 activation、projectile 成对字段）塌成同一帧，会保底留出至少一帧有效窗口。
  - `excluded`：硬直、无敌、慢动作、版本号，以及 `*Length` / `*Duration`（淡入淡出混合除外）。新发现有字段不该缩放时加到这里。
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

`src/Features/WhipAssist.cs`。钩锁辅助：自动瞄准、只高亮钩得到的锚点；可选无目标不出钩（默认关）。

- 锚点高亮由 `DistanceReactionSystem` 根据距离调用 `WhipDistanceComponent.AddFeedback` / `RemoveFeedback`，不考虑墙壁遮挡。本模块对每个高亮锚点用游戏自己的目标搜索 `WhipHookDetect.FindEntityTarget` 朝它探测一次（`Probe`，探测前后保存并恢复钩锁状态），结果用于三件事。
- 补丁：
  - `WhipDistanceComponent.AddFeedback` / `RemoveFeedback` 前置：钩不到的锚点不亮。
  - `WhipHookDetect.CanStart` 前置：原方向有目标 → 原版；否则只有一个可达锚点 → 自动瞄准；都没有时若 `SkipEmpty` 打开则不出钩，默认关闭则交给原版（仍可挥空）。
  - `WhipHookDetect.GetAimDirection` 后置：探测时和自动瞄准时改写方向。
  - `WhipHookDetect.OnStart` / `OnStop` 后置：钩锁进行中冻结可达性计算。
- 锚点每 60 帧扫描一次场景；每个高亮锚点每帧用 `FindEntityTarget` 探测可达性（房间里勾点多时有开销，和 Nexus 上某房间掉帧的报告可能有关，尚未单独优化）。
- 平时的可达性用某个 `MC_` 钩锁动作上的 `WhipHookDetect` 探测；按下钩锁时（`CanStart`）改用这次真正要发出的那个 `WhipHookDetect` 重新算一遍。空中、爬墙后的钩锁和地面的不是同一个动作，射程和起点不同，用地面的参数判断会把能钩到的点算成"没有"，导致按了不出钩。
- 配置：`06 Whip`。`SkipEmpty` 默认 `false`（已有配置文件的玩家仍保留文件里的值）。

## MirrorRepair

`src/Features/MirrorRepair.cs`。隐士任务后，存档室的传送镜子恢复，快速传送解锁。

- 补丁：`FastTravelMenuManager.IsDeathTrapActive` 后置：返回 `false`，解除快速传送锁定。
- **不能给 `GPE_Mirror.CanUseMirror` 打补丁**：这个函数太短，原生 detour 搬走开头指令时会把里面的短跳转搬坏。曾经的后置补丁让镜之厅最上层那面镜子一被检查就崩溃（`GameAssembly.dll` 里 `0x80000003`，落在 `CanUseMirror` 内部）。
- 场景替换：剧情推进后存档室会显示一面"破碎"的镜子，原来的镜子仍在场景里但被隐藏。原镜子和破镜子是同一层的兄弟节点，名字只差 `Broken`（存档室 `GPE_MirrorBase_Broken` / `GPE_MirrorBase`，镜之厅中枢 `GPE_MirrorBase_Cemetery_04_Broken` / `GPE_MirrorBase_Cemetery_04`），只按这个规则配对。中枢里一个分组下有很多镜子，用更宽松的规则会配错。没有这种兄弟节点的破镜子（中枢的 `HallOfMirrors_01`~`04`）不处理。找到后隐藏破碎节点、激活原节点，关掉开关时当场还原。房间加载后的 120 帧内每帧检查，让替换发生在转场中；之后每 60 帧检查一次。
- 没用过的存档室镜子仍然不能传送（原版规则）；暗影镜子等非 `BaseMirror` 不处理。
- 配置：`RepairMirrors`。

## MirrorPrompt

`src/Features/MirrorPrompt.cs`。站在可传送的镜子前时，交互提示改成"保存"（存档点下的镜子）或"信息"（其他镜子），后面加上"下 传送"。

- 挂在：`ActionTracker.Updated`。
- 找到源文本里带 `<input="...Interact...">` 标签的 `InputTagTextPreprocessor`，加一段标签指向移动操作的"下"方向，再交给游戏自己的 `PreprocessText` 转成当前设备的按键图标。
- 文字：交互标签后面只有一个短词时才换成"保存/信息"，更长的文字保留游戏原文。原文里有汉字就用中文，否则用 Save/Info/Teleport。
- 能否传送由游戏自己的 `GPE_Mirror.CanUseMirror` 判断（它不能打补丁，见 MirrorRepair）；每面镜子的检查单独 `try/catch`，出错的镜子只跳过它自己。
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
- **装备框**：菜单打开时 `HasCharmEquipped` 仍为原版，但 `InventoryItemSlotController.SetUpSlot` / `RefreshSlot` 后置会给已被动生效的魔导器点亮 `equippedDisplay`，与槽位里装备的框一致；点击仍是往槽位装备。
- 负面魔导器列表：`DrawbackCharms`（资源名）。新增负面魔导器时，同时更新 `Settings.cs` 的默认值和说明、`SettingsRows.cs` 的提示文字、两份 README 的魔导器表。
- 配置：`05 Charms`。
- 与 Charms Unlimited 冲突（两者都改装备逻辑）。

## TarotShortcuts（含 TarotHint）

`src/Features/TarotShortcuts.cs`、`src/Features/TarotHint.cs`。组合键直接释放已获得的塔罗牌法术，不切换装备。

- Y 走的是 `UseItem`（`itemType == Spell`）这个 `ActionTrigger`，`GetActionData` 按当前装备的法术的 `attackBindings` 给出动作。本模块在它后置里把结果换成目标法术的动作（同一个 `bindingType` 的绑定，没有就用第一个），所以不需要重新装备。
- 选哪张牌（`Pick`）：R3 发起时强制指定 → 正在释放的替换法术（保证"准备"和"释放"两段是同一张牌）→ 按 Y 时按住 LT → 按 Y 时按住 RB（再看上/下）→ 上/下。LT、RB 都在按下 Y 的那一帧记录（`PollInput`，30 帧内有效），因为法术可能在几帧后才开始。
- 消耗：`SpellManaCostFeature.GetEquippedSpell` / `SpellHealthCostFeature.GetEquippedSpell` 后置，在替换法术开始前后返回目标法术，扣的是那张牌自己的 MP/HP。
- 释放中的判断：`ActionTracker.Started` 看到替换出来的动作时记下 `casting` 和它的名字前缀（第一个 `_` 之前），之后没有同前缀的动作在运行就算结束。
- R3 / RB+R3：`PollInput` 记下 R3 按下，`Update` 里用 `Actor.TryStartAction` 启动最近用过的法术 `UseItem`（`forced` 指定法术）。
- 按住型法术（赦免、灵魂收割等）：动作上的 `InputConditionFeature` 检查 `Player_Spell_InputState`，松开就停。R3 发起的法术进行中，`IsOtherInputOk` / `IsConditionValid` 后置在 R3 按住时返回 `true`。
- LT+Y：LT 先开始弹反，游戏会把 Y 缓冲到弹反可以衔接时才处理。按 Y 时如果 `CheckStartAction` 只是因为 `ChainableIssue` 不能开始、且动作的所有 Feature 的 `CanStart` 都同意（MP、法术锁等），就直接 `StartAction`，护盾和弹反同时生效；`Actor.CheckStartAction` 后置随后拦住缓冲的那次 Y，避免放两次。
- 法术关键词和法术资源名或绑定的动作名比较（`Matches`），第一个已获得的生效；已获得 = 当前装备的 + 背包里的 `SpellItemData`，每 60 帧扫描一次。
- **TarotHint**：`InventorySpellMenuManager.RefreshSelectedItemDisplay` 后置。描述文字由本地化组件异步填入，所以记下目标 `TMP_Text`（有副描述用副描述），每帧（`Plugin` 的 `Update`）检查末尾不是提示时重新接上。Y 和方向用 `<input="...">` 标签交给页面的 `InputTagTextPreprocessor` 转成图标，跟随按键配置；LT、RB、R3 是直接读手柄的，用 Y 图标的前缀（如 `xbox_`）拼出 `leftTrigger` / `rightShoulder` / `rightStickPress` 的精灵名，在精灵资源里找得到才用。需要按住的法术写"按住"。
- 配置：`07 Tarot` 的 `Shortcuts` 和各 `*Spell(s)`。

## DiveInvulnerable

`src/Features/DiveInvulnerable.cs`。时空飞冲（`RiseAndFall`）全程无敌。

- 补丁：`HeroHitStrategy.GetHitResult`、`DefaultHitStrategy.GetHitResult` 后置，和 Evasion 一样把 `Success` 改成 `Ignore`。
- 判断：主角任何一个名字含 `RiseAndFall` 的动作在运行（起跳、停顿、下落、落地，以及穿过房间时接上的 `MC_EndRoomTransition_ChainRiseAndFall`）。
- 陷阱、即死区域、坠落伤害不受影响。
- 配置：`DiveInvulnerable`。在"法术优化"子页里改。

## QuickEquip

`src/Features/QuickEquip.cs`。十字键快捷换装。

- 左/右（`QuickEquipWeapons`）：按资源名家族分组，每类只保留最高 `Tier`，顺序同暂停菜单。
- 上/下（`QuickEquipSpells`）：未设快速施法的法术，顺序同塔罗页。
- 攻击动作进行中不立刻换装：等该 `WeaponActionData` 结束，或当前帧已到 `chainableFrame`（可取消/连段的间隙）再执行；连按仍按队列轮换。
- 任一开启时，左摇杆没动则清掉十字键对 `moveInputValue` 的贡献。
- 配置与快速施法同在「快速操作」页。

## CrossWallBounce

`src/Features/CrossWallBounce.cs`。圣十字架（`BoomerangCross`）撞墙不消失。

- 补丁：`BoomerangCrossIncreaseHitCount.OnStart`、`BoomerangCrossStateComponent.IncrementCount` 前置，开启时不累计撞墙次数；`Actor.StartAction` 前置，若动作名含 `BoomerangCrossProjectile_Projectile_Destroy` 且实体有 `BoomerangCrossStateComponent`，改成 `…BounceWall`。
- 回到身边的回收逻辑不变。
- 配置：`CrossWallBounce`。在"法术优化"子页里改。

## LevelUpRefill

`src/Features/LevelUpRefill.cs`。升级时 HP、MP 回满。

- 补丁：`ExperienceComponent.IncrementCurrentExperience` 前置记下等级、后置比较；主角（`isMcEntity`）等级上升时调用 `Damageable.SetCurrentHealth`（`maxHealth`）和 `ManaCaster.SetCurrentMana`（`GetMaxMana`）。
- 读档设置等级不经过这里，所以不算升级。
- 配置：`LevelUpRefill`。

## StatueLevelUp

`src/Features/StatueLevelUp.cs`。经验雕像保证升级。

- 补丁：`ExperienceLootData.Drop` 后置，掉落者名字含 `DestructibleSourceExperience` 时记下当时的等级；`IncrementCurrentExperience` 后置记录最后一次获得经验的帧。
- 经验球是飞向主角后才入账的，所以等 90 帧不再有经验入账（最多 900 帧）后再判断：等级没有上升就用 `IncrementCurrentExperience` 补上 `GetExperienceMax() - GetExperienceCurrent()`。
- 日志里的 `experience dropped by ...` 会列出每个掉落经验的对象名，雕像改名时用它确认 `StatueName`。
- 配置：`StatueLevelUp`。
