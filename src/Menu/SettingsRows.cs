using BepInEx.Configuration;

namespace UltraMode.Menu;

/// <summary>A row of the in-game settings page; a slider when Max > Min, else a toggle.</summary>
internal sealed record SettingsRow(ConfigEntryBase Entry, string LabelZh, string LabelEn, string HintZh, string HintEn,
    float Min = 0f, float Max = 0f, float Step = 0f)
{
    internal bool IsSlider => Max > Min;
}

/// <summary>
/// The settings page, shared by every menu that shows it. Every caption must be renderable by the game's fonts,
/// or that row is skipped (Minimap) or shows blank boxes (Belmont UI).
/// </summary>
internal static class SettingsRows
{
    internal const string TitleZh = "奥创模式", TitleEn = "Ultra Mode";
    internal const string SummaryZh = "高速反应、闪避无敌、攻击弹反、魔导器被动";
    internal const string SummaryEn = "Fast response, invulnerable evasion, attack parry, passive relics";
    internal const string SliderFormat = "{0:0.00}x";

    /// <summary>In page order. A config entry left out of this list can only be changed in the cfg file.</summary>
    internal static SettingsRow[] All() => new SettingsRow[]
    {
        Toggle(Settings.Enabled, "启用奥创模式", "Enable Ultra Mode", "关闭后全部恢复原版（也可按 F10 切换）", "Off restores vanilla behaviour (F10 toggles too)"),
        Toggle(Settings.ToggleKeyEnabled, "快捷键开关奥创模式", "Toggle hotkey", "默认 F10，与其他 mod 冲突时关闭", "F10 by default; turn off if another mod uses it"),

        Slider(Settings.MoveSpeed, 0.5f, 3f, 0.05f, "移动速度", "Move speed", "1.00x 为原版", "1.00x is vanilla"),
        Slider(Settings.GroundSharpness, 1f, 10f, 0.1f, "起步与停止灵敏度", "Start/stop sharpness", "越大越跟手", "Higher feels snappier"),
        Slider(Settings.TurnStopSpeed, 1f, 5f, 0.1f, "转身与急停速度", "Turn/stop speed", "转身、急停、起步过渡动作的速度", "Speed of turn, brake and start transitions"),
        Slider(Settings.AttackWindupSpeed, 1f, 4f, 0.05f, "攻击前摇速度", "Attack wind-up speed", "出手到命中的速度，和后摇相同时最自然", "Start to first hit; matching recovery looks most natural"),
        Slider(Settings.AttackRecoverySpeed, 1f, 4f, 0.05f, "攻击后摇速度", "Attack recovery speed", "命中之后到可以行动的速度", "From the hit until you can act again"),
        Slider(Settings.WhipSpeed, 1f, 4f, 0.05f, "钩锁出手与收回速度", "Whip hook speed", "出手、收回、弹回等动作，不影响摆荡", "Throw, retract and bounce; swinging is unchanged"),
        Slider(Settings.CameraFollowSpeed, 1f, 5f, 0.05f, "镜头跟随速度", "Camera follow speed", "人物快速移动或被弹射时镜头更快跟上", "Camera keeps up with fast moves and launches"),
        Slider(Settings.CameraLookRange, 1f, 3f, 0.05f, "右摇杆观察范围", "Right-stick look range", "镜头上下左右能移动多远", "How far the camera can look in each direction"),
        Slider(Settings.CameraLookSpeed, 1f, 4f, 0.05f, "右摇杆观察速度", "Right-stick look speed", "镜头移动和回正的速度", "How fast the camera moves out and recenters"),
        Slider(Settings.MirrorSpeed, 1f, 5f, 0.05f, "镜子传送速度", "Mirror travel speed", "进出镜子的动画，1 为原速", "Entering/exiting animation, 1 is vanilla"),
        Toggle(Settings.InstantMirror, "镜子即按即进", "Instant mirror entry", "镜子前按下不需要按住", "No need to hold down at a mirror"),
        Toggle(Settings.MirrorPrompt, "镜子提示下传送", "Mirror teleport prompt", "镜子前提示 保存/信息 和 下 传送", "Shows Save/Info and Down Teleport at mirrors"),

        Toggle(Settings.WhipSkipEmpty, "无目标时不出钩锁", "No empty whip", "没有可钩的东西时按钩锁不打断动作", "Pressing whip with nothing to hook does nothing"),
        Toggle(Settings.WhipAutoAim, "钩锁自动瞄准", "Whip auto-aim", "只有一个高亮目标时不管朝向都能钩中", "A lone highlighted target is hooked whatever you face"),
        Toggle(Settings.WhipHighlightReachable, "只高亮钩得到的目标", "Highlight reachable only", "被墙挡住或超出范围时不高亮", "No highlight when blocked or out of range"),

        Toggle(Settings.EvasionInvulnerable, "滑铲与后撤步无敌", "Invulnerable slide/backdash", "陷阱和即死区域仍然有效", "Traps and death zones still apply"),
        Toggle(Settings.TechRecovery, "击飞后按跳受身", "Jump to recover from knockback", "立即恢复正常状态并闪光", "Instantly regain control with a flash"),

        Toggle(Settings.AttackParry, "攻击也能弹反", "Attacks can parry", "获得弹反魔导器后生效，范围与窗口同原版弹反", "Needs the parry relic; same window and area as parry"),

        Toggle(Settings.RepairMirrors, "修复破碎镜子", "Repair broken mirrors", "隐士任务后损坏的传送镜子恢复可用", "Teleport mirrors broken after The Hermit work again"),

        Toggle(Settings.PassiveCharms, "自动装备正面魔导器", "Auto-equip relics without drawbacks", "获得即生效，不用装备", "Active once found, no slot needed"),
        Toggle(Settings.PassiveDrawbackCharms, "自动装备负面魔导器", "Auto-equip relics with drawbacks", "焦黑之心、地狱徽记、别西卜之印、苍白之刺、亡灵指环", "Blackened Heart, Infernal Medallion, Beelzebub's Seal, Pale Thorn, Phantom Ring"),
        Toggle(Settings.ForceRevealSecret, "强制开启洞幽石", "Always-on Gloomstone", "没获得也生效，靠近时显示隐藏墙壁", "Works even if not found; reveals hidden walls nearby"),
    };

    internal static void Save() => Plugin.Instance.Config.Save();

    /// <summary>Every entry of this mod's config, including ones not on the page (hotkeys, keyword lists).</summary>
    internal static void ResetAll()
    {
        foreach (var entry in Plugin.Instance.Config.Values) entry.BoxedValue = entry.DefaultValue;
    }

    private static SettingsRow Toggle(ConfigEntry<bool> entry, string labelZh, string labelEn, string hintZh, string hintEn) =>
        new(entry, labelZh, labelEn, hintZh, hintEn);

    private static SettingsRow Slider(ConfigEntry<float> entry, float min, float max, float step,
        string labelZh, string labelEn, string hintZh, string hintEn) =>
        new(entry, labelZh, labelEn, hintZh, hintEn, min, max, step);
}
