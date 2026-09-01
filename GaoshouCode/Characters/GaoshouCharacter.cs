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
    public override CharacterAssetProfile AssetProfile => new(
        Ui: new CharacterUiAssetSet(
            IconTexturePath: $"{Entry.ResPath}/images/characters/Gaoshou_character_icon.png",
            IconOutlineTexturePath: $"{Entry.ResPath}/images/characters/Gaoshou_character_icon_outline.png",
            CharacterSelectIconPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_select.png",
            CharacterSelectLockedIconPath: $"{Entry.ResPath}/images/characters/Gaoshou_character_select_locked.png",
            MapMarkerPath: $"{Entry.ResPath}/images/characters/Gaoshou_map_marker.png"));

    public override bool RequiresEpochAndTimeline => false;
    public override float AttackAnimDelay => 0f;
    public override float CastAnimDelay => 0f;

    // 不覆写 TryCreateCreatureVisuals()：返回 null 表示使用已配置的场景路径，
    // 结合 PlaceholderCharacterId="regent" 会加载储君的战斗模型。

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
