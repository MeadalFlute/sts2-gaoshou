using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;

namespace Gaoshou.Characters;

[RegisterCharacter]
public sealed class GaoshouCharacter : ModCharacterTemplate<GaoshouCardPool, GaoshouRelicPool, GaoshouPotionPool>
{
    // 高手主题色：沉稳的金色。
    public static readonly Color ThemeColor = new(0.78f, 0.55f, 0.2f);

    public override Color NameColor => ThemeColor;
    public override Color EnergyLabelOutlineColor => new(0.15f, 0.08f, 0.02f);
    public override Color MapDrawingColor => ThemeColor;

    public override CharacterGender Gender => CharacterGender.Masculine;

    public override int StartingHp => 75;
    public override int StartingGold => 99;

    // 白模继承储君：战斗模型、能量表盘、商店/篝火、音效等未覆盖字段都会从储君(regent)补齐。
    public override string? PlaceholderCharacterId => "regent";

    // 只覆盖需要区分身份的 UI 图标；Scenes 留空以继承储君的模型与表盘。
    // IconPath 是"顶部栏左上角角色头像"用的（CharacterModel.Icon → ui/character_icons/<id>_icon 场景）。
    // 之前没给这个字段，RitsuLib 的 Icon 补丁会回退到占位角色（储君）的图标，所以单人左上角显示的是储君。
    // 多人头像走的是 IconTexturePath，本来就是对的，不用动。这里给贴图就行（RitsuLib 会自动转成 Control 图标）。
    public override CharacterAssetProfile AssetProfile => new(
        Ui: new CharacterUiAssetSet(
            IconTexturePath: $"{Entry.ResPath}/images/characters/Gaoshou_character_icon.png",
            IconOutlineTexturePath: $"{Entry.ResPath}/images/characters/Gaoshou_character_icon_outline.png",
            IconPath: $"{Entry.ResPath}/images/characters/Gaoshou_top_portrait.png",
            CharacterSelectIconPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_select.png",
            CharacterSelectLockedIconPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_select_locked.png",
            MapMarkerPath: $"{Entry.ResPath}/images/characters/Gaoshou_map_marker.png"));

    public override bool RequiresEpochAndTimeline => false;
    public override float AttackAnimDelay => 0f;
    public override float CastAnimDelay => 0f;

    // 不覆写 TryCreateCreatureVisuals()：返回 null 表示使用已配置的场景路径，
    // 结合 PlaceholderCharacterId="regent" 会加载储君的战斗模型。
    //
    // 【角色帧动画：暂缓】2026-09-20 曾接入一套 GPT 生成的待机帧（6 帧、RitsuLib VisualCueSet +
    // 自建 gaoshou_visuals.tscn），因该套图逐帧一致性不足（姿态/比例漂移）而回退到储君占位。
    // 相关资产与场景已挪到 _workspace\backups\character_anim_20260920_174*，
    // 生成脚本仍在 _workspace\_imgwork\make_character_idle_frames.py，源图在 _workspace\DiceCards\GPT\。
    // 将来要重做时的要点：
    //   1) 帧规格 512×512 透明画布、脚底落在 y=456、水平居中；人物高度取 330px
    //      （原版玩家小人的真实基准是储君场景里的 %Bounds = 230×335，不是 400px）；
    //   2) 场景照 res://scenes/creature_visuals/rocket.tscn 的结构：纯 Node2D 根 + %Visuals(Sprite2D)/
    //      %Bounds/%CenterPos/%IntentPos，根节点**不要**挂脚本（那是游戏工程内的路径，mod 工程没有），
    //      Sprite2D 上移 200px 使"脚底=节点原点"；.tscn 里也不能写 ';' 注释；
    //   3) 帧一致性坑：必须逐帧按脚底中心对齐 + 逐帧高度归一化，否则源图的几像素浮动会原样播成抖动。

    public override List<string> GetArchitectAttackVfx()
    {
        return
        [
            "vfx/vfx_attack_blunt",
            "vfx/vfx_heavy_blunt",
            "vfx/vfx_attack_slash",
            "vfx/vfx_bloody_impact",
            "vfx/vfx_rock_shatter"
        ];
    }
}
