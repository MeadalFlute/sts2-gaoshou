using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Multiplayer;
using Gaoshou.Characters;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 【2026-09-29 实施 R1】多人面板里"铁甲战士的阴影"清理器。
///
/// 背景（前面几轮已定位到的事实）：
///   * 原版多人界面场景**把铁甲战士的头像贴图直接烘进场景**当默认值 ✗：
///       - <c>res://scenes/ui/multiplayer_player_state.tscn</c>：L3 烘入
///         <c>res://images/ui/top_panel/character_icon_ironclad.png</c>，L33-41 作为
///         <c>%CharacterIcon</c>（TextureRect）的默认贴图；L11 还烘了 <c>energy_ironclad.tres</c>；
///       - <c>res://scenes/ui/remote_lobby_player.tscn</c>：L4/L24-32 同样烘了铁甲战士头像。
///   * 原版只覆盖**头像本体**（<c>NMultiplayerPlayerState.cs:135</c> = <c>Player.Character.IconTexture</c>；
///     <c>NRemoteLobbyPlayer.cs:120</c>），**不覆盖任何其它层** ✗ ⇒ 玩家仍能看到"我们头像下方压着一层
///     铁甲战士"（皮肤 mod 的做法也印证了：它同时替换 <c>character_icon_&lt;id&gt;.png</c> 与
///     <c>character_icon_&lt;id&gt;_outline.png</c> 两套资源 ✓）。
///   * 我们自己的 6 个槽位都设了 ✓、<c>IconPath</c> 也已改成自己的 .tscn ✓（日志证实我们的
///     <c>Gaoshou_character_icon_outline.png</c> 确实被加载 ✓）⇒ 残留的那层只能来自**场景烘死的默认值** ✗。
///
/// 做法（R1）：在这两个界面**构建/刷新之后**遍历整棵子树，凡"贴图资源路径里含**原版角色 id**"
/// （ironclad / silent / defect / necrobinder / regent）的节点：
///   * 若是头像本体（节点名含 CharacterIcon）⇒ 换成我们自己的头像；
///   * 若是能量图标（路径含 energy）⇒ 换成该角色卡池自己的能量图标；
///   * 其它未知的装饰/阴影层 ⇒ **直接隐藏**（用户口径：宁可没有，也不要铁甲战士的图 ✓）。
/// 判据严格限定"原版角色 id 的贴图路径"，且**只在该面板属于我们自己的角色时**才动手
/// ⇒ 不会影响其它角色（别人玩铁甲战士时照常显示铁甲战士 ✓）、也不会动我们的自有资源 ✓。
/// </summary>
public sealed class VanillaCharacterArtSweepPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_vanilla_character_art_sweep";

    public static string Description => "hide/replace vanilla-character art baked into the multiplayer player-state and lobby scenes";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        // 我们自己的角色面板：Player 是 public 的，直接能拿到角色做守卫 ✓
        PatchTarget.Method(typeof(NMultiplayerPlayerState), "_Ready"),
        // 大厅条目：RefreshVisuals 是 private，用名字定位（RitsuLib 支持字符串方法名 ✓）；
        // 用 OptionalMethod ⇒ 万一原版改名/改签名也只是这条失效，不会让整个补丁注册抛异常 ✓。
        PatchTarget.OptionalMethod(typeof(NRemoteLobbyPlayer), "RefreshVisuals"),
    ];

    public static void Postfix(Node __instance)
    {
        switch (__instance)
        {
            case NMultiplayerPlayerState state:
                Sweep(__instance, state.Player?.Character);
                break;
            case NRemoteLobbyPlayer lobby:
                Sweep(__instance, ReadLobbyCharacter(lobby));
                break;
        }
    }

    /// <summary>NRemoteLobbyPlayer 的角色字段是 private（_character）⇒ 反射读取（读不到就跳过）。</summary>
    private static CharacterModel? ReadLobbyCharacter(NRemoteLobbyPlayer lobby)
    {
        try
        {
            var field = typeof(NRemoteLobbyPlayer).GetField("_character",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            return field?.GetValue(lobby) as CharacterModel;
        }
        catch
        {
            return null;
        }
    }

    private static void Sweep(Node root, CharacterModel? character)
    {
        // 只管我们自己的角色：别人（原版角色）的面板照常显示他自己的图 ✓。
        if (character is not GaoshouCharacter)
            return;

        var ownIcon = Load($"{Entry.ResPath}/images/characters/Gaoshou_character_icon.png");
        var ownEnergy = character.CardPool is { } pool ? Load(pool.EnergyIconPath) : null;

        Walk(root);

        void Walk(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                Consider(child);
                Walk(child);
            }
        }

        void Consider(Node node)
        {
            var texture = node switch
            {
                TextureRect rect => rect.Texture,
                Sprite2D sprite => sprite.Texture,
                TextureButton button => button.TextureNormal,
                _ => null,
            };
            var path = texture?.ResourcePath ?? string.Empty;
            if (!IsVanillaCharacterArt(path))
                return;

            // 头像本体：换成我们自己的头像 ✓
            if (ownIcon != null && node.Name.ToString().Contains("CharacterIcon") && node is TextureRect iconRect)
            {
                iconRect.Texture = ownIcon;
                Entry.Logger.Info($"[Gaoshou][IconSweep] replaced baked vanilla icon on '{node.Name}' ({path})");
                return;
            }

            // 能量图标：换成该角色卡池自己的能量图标 ✓
            if (path.Contains("energy") && ownEnergy != null && node is TextureRect energyRect)
            {
                energyRect.Texture = ownEnergy;
                Entry.Logger.Info($"[Gaoshou][IconSweep] replaced baked vanilla energy icon on '{node.Name}'");
                return;
            }

            // 其它未知层（阴影/装饰）：隐藏 —— 宁可没有，也不要铁甲战士的图 ✓
            //（Visible 在 CanvasItem 上，Node 基类没有 ⇒ 必须转换 ✓）
            if (node is CanvasItem canvasItem)
            {
                canvasItem.Visible = false;
                Entry.Logger.Info($"[Gaoshou][IconSweep] hid baked vanilla-art node '{node.Name}' ({path})");
            }
        }
    }

    private static Texture2D? Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !ResourceLoader.Exists(path))
            return null;
        return ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
    }

    /// <summary>判据：贴图资源路径里含**原版角色 id**（严格限定，绝不误伤我们自己的资源 ✓）。</summary>
    private static bool IsVanillaCharacterArt(string path) =>
        path.Contains("ironclad")
        || path.Contains("silent")
        || path.Contains("defect")
        || path.Contains("necrobinder")
        || path.Contains("regent");
}
