using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Configuration;
using UnityEngine;

namespace UltraMode;

/// <summary>
/// All values are read live unless noted. Section names are numbered so the cfg file keeps this order. Renaming a
/// section or key leaves the player's old value behind as an orphan and falls back to the default.
/// </summary>
internal static class Settings
{
    internal static ConfigEntry<bool> Enabled = null!;
    internal static ConfigEntry<bool> ToggleKeyEnabled = null!;
    internal static ConfigEntry<KeyboardShortcut> ToggleKey = null!;

    internal static ConfigEntry<float> MoveSpeed = null!;
    internal static ConfigEntry<float> GroundSharpness = null!;
    internal static ConfigEntry<float> TurnStopSpeed = null!;
    internal static ConfigEntry<float> AttackWindupSpeed = null!;
    internal static ConfigEntry<float> AttackRecoverySpeed = null!;
    internal static ConfigEntry<float> WhipSpeed = null!;
    internal static ConfigEntry<float> MirrorSpeed = null!;
    internal static ConfigEntry<bool> InstantMirror = null!;
    internal static ConfigEntry<bool> MirrorPrompt = null!;
    internal static ConfigEntry<float> CameraFollowSpeed = null!;
    internal static ConfigEntry<float> CameraLookRange = null!;
    internal static ConfigEntry<float> CameraLookSpeed = null!;

    internal static ConfigEntry<bool> EvasionInvulnerable = null!;
    internal static ConfigEntry<string> EvasionActionKeywords = null!;
    internal static ConfigEntry<bool> EvasionUsesDodgeFeature = null!;

    internal static ConfigEntry<bool> TechRecovery = null!;
    internal static ConfigEntry<string> TechFeedback = null!;

    internal static ConfigEntry<bool> AttackParry = null!;
    internal static ConfigEntry<string> ParryActionName = null!;

    internal static ConfigEntry<bool> RepairMirrors = null!;

    internal static ConfigEntry<bool> WhipSkipEmpty = null!;
    internal static ConfigEntry<bool> WhipAutoAim = null!;
    internal static ConfigEntry<bool> WhipHighlightReachable = null!;

    internal static ConfigEntry<bool> PassiveCharms = null!;
    internal static ConfigEntry<bool> PassiveDrawbackCharms = null!;
    internal static ConfigEntry<string> DrawbackCharms = null!;
    internal static ConfigEntry<bool> ForceRevealSecret = null!;
    internal static ConfigEntry<bool> DebugLogging = null!;
    internal static ConfigEntry<bool> DumpKeyEnabled = null!;
    internal static ConfigEntry<KeyboardShortcut> DumpKey = null!;

    internal static void Bind(ConfigFile cfg)
    {
        Enabled = cfg.Bind("00 General", "Enabled", true, "总开关：奥创模式。关闭后所有补丁都回到原版行为。");
        ToggleKeyEnabled = cfg.Bind("00 General", "ToggleKeyEnabled", true, "启用切换奥创模式的快捷键（ToggleKey）。和其他 mod 的快捷键冲突时关闭，或者改 ToggleKey。");
        ToggleKey = cfg.Bind("00 General", "ToggleKey", new KeyboardShortcut(KeyCode.F10), "游戏内切换奥创模式的按键。");

        MoveSpeed = cfg.Bind("01 Fast Response", "MoveSpeed", 1.25f,
            new ConfigDescription("移动速度倍率，1 = 原版。", new AcceptableValueRange<float>(0.5f, 3f)));
        GroundSharpness = cfg.Bind("01 Fast Response", "GroundSharpness", 2.5f,
            new ConfigDescription("地面起步/停止的加减速锐度倍率，越大越跟手。", new AcceptableValueRange<float>(1f, 10f)));
        TurnStopSpeed = cfg.Bind("01 Fast Response", "TurnStopSpeed", 2f,
            new ConfigDescription("转身、急停、起步过渡动作的播放速度倍率。", new AcceptableValueRange<float>(1f, 5f)));
        AttackWindupSpeed = cfg.Bind("01 Fast Response", "AttackWindupSpeed", 1.4f,
            new ConfigDescription("攻击前摇（出手到第一次命中帧）速度倍率。前摇和后摇倍率相同时动画最自然。", new AcceptableValueRange<float>(1f, 4f)));
        AttackRecoverySpeed = cfg.Bind("01 Fast Response", "AttackRecoverySpeed", 1.4f,
            new ConfigDescription("攻击后摇（命中之后到可行动）速度倍率。", new AcceptableValueRange<float>(1f, 4f)));
        WhipSpeed = cfg.Bind("01 Fast Response", "WhipSpeed", 1.6f,
            new ConfigDescription("钩锁出手、收回、弹回、空中悬停等动作的速度倍率（摆荡本身不受影响）。", new AcceptableValueRange<float>(1f, 4f)));
        MirrorSpeed = cfg.Bind("01 Fast Response", "MirrorSpeed", 1f,
            new ConfigDescription("镜子传送：按住判定和进出镜子动作的速度倍率。", new AcceptableValueRange<float>(1f, 5f)));
        InstantMirror = cfg.Bind("01 Fast Response", "InstantMirror", true,
            "镜子前蹲下即开始进入镜子，不需要按住。");
        MirrorPrompt = cfg.Bind("01 Fast Response", "MirrorPrompt", true,
            "站在能传送的镜子前时，交互提示改为“保存”（存档点）或“信息”，后面加上“下 传送”。");
        CameraFollowSpeed = cfg.Bind("01 Fast Response", "CameraFollowSpeed", 1.25f,
            new ConfigDescription("镜头跟随人物的速度倍率（镜头阻尼除以这个值），解决快速移动、被弹射时镜头慢一步。", new AcceptableValueRange<float>(1f, 5f)));
        CameraLookRange = cfg.Bind("01 Fast Response", "CameraLookRange", 1.6f,
            new ConfigDescription("右摇杆观察镜头的上下左右最大范围倍率。", new AcceptableValueRange<float>(1f, 3f)));
        CameraLookSpeed = cfg.Bind("01 Fast Response", "CameraLookSpeed", 2f,
            new ConfigDescription("右摇杆观察镜头移动和回正的速度倍率。", new AcceptableValueRange<float>(1f, 4f)));

        EvasionInvulnerable = cfg.Bind("02 Evasion", "Invulnerable", true, "滑铲、后撤步过程中无敌（陷阱、即死区域仍然有效）。");
        EvasionActionKeywords = cfg.Bind("02 Evasion", "ActionKeywords", "slide,backdash,back_dash,glissade,dodge",
            "动作名包含这些关键词（逗号分隔，不区分大小写）时视为闪避动作。开 DebugLogging 可在日志里看到动作名。");
        EvasionUsesDodgeFeature = cfg.Bind("02 Evasion", "DetectByDodgeFeature", true, "带有闪避位移组件（DodgeMovementFeature）的动作也视为闪避动作。");

        TechRecovery = cfg.Bind("02 Evasion", "TechRecovery", true, "被击飞后按跳跃立即受身，恢复正常状态，并播放闪光特效。");
        TechFeedback = cfg.Bind("02 Evasion", "TechFeedback", "CharmTankOneHit_TriggerArmor_Release_Feedback",
            "受身特效（Feedback 资源名），默认是护盾魔导器触发时的特效。F11 导出的 feedbacks.txt 里有全部可用名字。");

        AttackParry = cfg.Bind("03 Parry", "AttackParry", true, "解锁弹反后，普通攻击在与弹反动作相同的判定窗口内也能弹反。");
        ParryActionName = cfg.Bind("03 Parry", "ParryActionName", "MC_VaniaLight_Parry", "主角弹反动作的名字。攻击时会复用它的弹反窗口、受击策略和判定范围。");

        RepairMirrors = cfg.Bind("04 World", "RepairMirrors", true, "隐士任务后变成破碎状态的普通传送镜子恢复可用，快速传送不再被锁定。");

        WhipSkipEmpty = cfg.Bind("06 Whip", "SkipEmpty", true, "附近没有可以钩的东西时，按钩锁键不出钩锁，也不打断当前动作。");
        WhipAutoAim = cfg.Bind("06 Whip", "AutoAim", true, "只有一个可钩物体高亮时，无论朝向哪边，按钩锁都自动钩中它。");
        WhipHighlightReachable = cfg.Bind("06 Whip", "HighlightReachable", true, "可钩物体只在真正钩得到时才高亮（被墙挡住、超出范围时不高亮）。");

        PassiveCharms = cfg.Bind("05 Charms", "PassiveCharms", true,
            "自动装备所有正面魔导器（不在 DrawbackCharms 里的）：获得即生效，不占装备槽。与 Charms Unlimited 同时使用时请关闭。");
        PassiveDrawbackCharms = cfg.Bind("05 Charms", "PassiveDrawbackCharms", false,
            "自动装备所有带负面效果的魔导器（DrawbackCharms 里的）。关闭时它们和原版一样，要手动装进槽位才生效。");
        DrawbackCharms = cfg.Bind("05 Charms", "DrawbackCharms",
            "Charm_GainHPHitMobNoPotion,Charm_ClampMCMana,Charm_BoostDamageButReduceLife,Charm_BoostDamageBackReduceDamageFront,Charm_RoomChangeResetMobSpawner",
            "带负面效果的魔导器（资源名，逗号分隔）。默认：焦黑之心（无法使用治疗药水）、地狱徽记（MP 上限不超过 80%）、别西卜之印（HP 上限降低 50%）、苍白之刺（背后受伤加倍）、亡灵指环（离开房间后敌人立即重生）。F11 导出的 charms.txt 里有全部资源名。");
        ForceRevealSecret = cfg.Bind("05 Charms", "ForceRevealSecret", true,
            "洞幽石（Charm_RevealSecret）的效果始终生效，没获得也一样：靠近时显示可破坏的隐藏墙壁。");
        DebugLogging = cfg.Bind("99 Debug", "DebugLogging", false, "在 BepInEx/LogOutput.log 输出主角每个动作的开始/结束、弹反等事件。");
        DumpKeyEnabled = cfg.Bind("99 Debug", "DumpKeyEnabled", false, "启用导出调试数据的快捷键（DumpKey）。只在排查问题时需要，默认关闭以免和其他 mod 冲突。");
        DumpKey = cfg.Bind("99 Debug", "DumpKey", new KeyboardShortcut(KeyCode.F11), "把主角动作、魔导器、能力等运行时数据导出到 BepInEx/ultra_mode_dump/。");
    }

    /// <summary>The master switch (ToggleKey). Features check it on every use, so turning it off restores vanilla at once.</summary>
    internal static bool On => Enabled.Value;

    private static readonly List<(ConfigEntryBase entry, Action action)> listeners = new();

    /// <summary>
    /// Runs <paramref name="action"/> when any of <paramref name="entries"/> changes, whether from a settings page or
    /// an edited cfg file. Needed only where a setting was baked into game data, e.g. rescaled frame numbers.
    /// </summary>
    internal static void OnChanged(Action action, params ConfigEntryBase[] entries)
    {
        foreach (var entry in entries) listeners.Add((entry, action));
    }

    internal static void Dispatch(object? sender, SettingChangedEventArgs args)
    {
        foreach (var (entry, action) in listeners)
        {
            if (entry != args.ChangedSetting) continue;
            try { action(); }
            catch (Exception e) { Plugin.Logger.LogError(e); }
        }
    }

    /// <summary>A comma-separated entry as trimmed, lowercased items.</summary>
    internal static string[] Keywords(ConfigEntry<string> entry) =>
        entry.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => s.ToLowerInvariant()).ToArray();
}
