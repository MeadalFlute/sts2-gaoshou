using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 【2026-09-30】历史记录 / 百科大全里"角色图渲染过大"的尺寸修正。
///
/// 现象（用户实测）：在**百科大全 / 历史记录查看历史对局**时，我们角色的图像**尺寸过大** ✗。
///
/// 根因（尺寸事实 + 代码定位）：
///   * 这两个界面用的是 **`CharacterModel.IconTexture`** 这个槽位：
///       - 历史记录：`NRunHistoryPlayerIcon.cs:64` `_icon.Texture = characterModel.IconTexture;`
///         （`_icon = GetNode&lt;TextureRect&gt;("%Icon")`，见同文件 L55；赋值在 `LoadRun` 内）
///       - 百科大全：`NBestiaryCharacterFilter.cs:137-138`（`%Image` 与 `%Shadow` 都用 `IconTexture`）、
///         `NBestiary.cs:369-370`（`_iconTexture` / `_iconOutlineTexture`）
///   * **该槽位的规范尺寸是 88×88**：
///       - 创意工坊皮肤 mod（`3787753911`）替换的 `character_icon_silent.png` 实测就是 **88×88** ✓
///       - 原版 `character_icon_ironclad.png` / `_outline.png` 的 ctex 都是 **7796 字节**（同尺寸同量级 ⇒ 88×88）✓
///       - 我们从原版槽位拿来的那张 outline 原本也是 **88×88** ✓
///   * 而**我们给的是 200×200** ✗ —— 顶部栏/其它地方没事，是因为**那些场景显式设了**
///     `expand_mode=1`（IgnoreSize）+ `stretch_mode=5`（KeepAspectCentered）把图缩进容器里 ✓
///     （例如我们自己的 `Gaoshou_top_icon.tscn`、原版 `ironclad_icon.tscn` ✓）；
///     而**历史记录的那个 `%Icon` 没有设这两项、也没有设尺寸** ✗ ⇒ 就按**贴图原始尺寸 200×200** 渲染 ✗
///     （场景里只有父节点 `PlayerIcon` 有 `custom_minimum_size = Vector2(64, 64)` ✓）
///     ⇒ **正是用户说的"忘记设置该项尺寸"** ✓✓。
///
/// 修法：**不动共享贴图**（`IconTexture` 顶部栏也在用 ✗ 缩小会连带改坏顶部栏 ✗），
/// 改为在**每一个用到它的界面**上，把显示该贴图的 `TextureRect` 补上尺寸/缩放设置：
///   `CustomMinimumSize = Size = (88, 88)` + `ExpandMode = IgnoreSize` + `StretchMode = KeepAspectCentered`
/// ⇒ 200×200 被等比缩放进 88×88 ⇒ **与原版 88×88 的观感一致** ✓，且只影响"正在显示我们这张图"的节点 ✓
/// （判据：节点贴图的 `ResourcePath` 含 `Gaoshou_character_icon` ⇒ 别的角色/别的图一律不动 ✓）。
/// </summary>
public sealed class CharacterIconDisplaySizePatch : IPatchMethod
{
    public static string PatchId => "gaoshou_character_icon_display_size";

    public static string Description => "give our 200x200 character icon the slot's 88x88 display size in run history / bestiary";

    public static bool IsCritical => false;

    /// <summary>该槽位的规范显示尺寸（见类注释的实测依据）。</summary>
    private static readonly Vector2 DisplaySize = new(88f, 88f);

    public static ModPatchTarget[] GetTargets() =>
    [
        // 历史记录：%Icon（public 方法 ✓，两个游戏版本都在）。
        // 用 Optional 而不是必需：万一某个版本里它改名/被移除，**不能**让整个补丁注册失败。
        PatchTarget.OptionalMethod(typeof(NRunHistoryPlayerIcon), "LoadRun"),
    ];

    public static void Postfix(Node __instance) => FixSubtree(__instance);

    internal static void FixSubtree(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is TextureRect rect && IsOurIcon(rect.Texture))
                Apply(rect);
            FixSubtree(child);
        }
    }

    /// <summary>只认"正在显示我们角色头像"的节点（按资源路径判据）⇒ 绝不误伤别的角色/别的图 ✓。</summary>
    private static bool IsOurIcon(Texture2D? texture)
    {
        var path = texture?.ResourcePath ?? string.Empty;
        return path.Contains("Gaoshou_character_icon");
    }

    private static void Apply(TextureRect rect)
    {
        if (rect.CustomMinimumSize == DisplaySize && rect.ExpandMode == TextureRect.ExpandModeEnum.IgnoreSize)
            return;   // 已经设好了（避免重复写入/抖动）

        // ⚠️ 绝对不要写 rect.Size ✗（2026-09-30 bug：历史头像"过大 + 偏右下"，beta/正式版都复现）：
        //    原版场景 run_history_player_icon.tscn 里的 `Icon` 是 **anchors_preset=15（0,0→1,1 全矩形）**
        //    的 TextureRect，**尺寸完全由父节点 PlayerIcon（custom_minimum_size = 64×64）的锚点决定**，
        //    场景里既没有 size 也没有 offset。Godot 里对 anchors≠0 的节点写 Size 会**重算并固化 offsets**
        //    ⇒ 节点从此脱离父容器排版、被甩到右下 ✗（这正是实测症状）。
        //    真正要做的只有两件事：① IgnoreSize 掐掉"贴图原始尺寸 200×200 顶起 min size"（原版 88×88
        //    贴图不会顶，我们的会 ⇒ 未修时就是"过大 + 往右下生长"）；② 让贴图等比缩进槽位。
        rect.CustomMinimumSize = DisplaySize;
        // 这两项就是原版角色 icon 场景用的组合（expand_mode=1 / stretch_mode=5）✓
        rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        rect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
    }
}

/// <summary>
/// 百科大全：角色详情里的角色图（<c>NBestiary.DisplayCharacterData</c>）。
///
/// ⚠️ **单独一个补丁类**的原因：该方法**只存在于游戏 0.111.0（beta）**；
/// 正式版 0.107.1 的 NBestiary 里没有它（已用反编译核对）⇒ 在正式版上这条本来就挂不上 ✗。
/// 拆开是为了保证"挂不上"**只影响这一条**，绝不连累上面「历史记录」那条（2026-09-30 事故复盘）。
/// RitsuLib 的可选目标缺失时是静默跳过的，所以 Entry 里另外补一条明确日志 ✓。
/// </summary>
public sealed class BestiaryCharacterIconDisplaySizePatch : IPatchMethod
{
    public static string PatchId => "gaoshou_character_icon_display_size_bestiary";

    public static string Description => "same 88x88 icon size fix for the bestiary character detail (beta-only method)";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        // 私有方法 ⇒ 按名定位；缺失时忽略（正式版就是这样）
        PatchTarget.OptionalMethod(typeof(NBestiary), "DisplayCharacterData"),
    ];

    public static void Postfix(Node __instance) => CharacterIconDisplaySizePatch.FixSubtree(__instance);
}
