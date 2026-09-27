using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Events.Custom;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 假商人事件（原版 FAKE_MERCHANT）里把高手形象的缩放**反算回去**。
///
/// ═══════════════════════【BUG】═══════════════════════
/// 用户实测：进假商人事件房间时，高手形象**过大、顶部超出屏幕**（商店/营火/战斗里都正常）。
///
/// ═══════════════════════【根因：两个房间的容器缩放不同】═══════════════════════
/// 原版那两个 <c>%CharacterContainer</c> 装的**不是同一种东西**，所以缩放基准不同：
///
/// | 房间 | 容器 scale | 原版塞进去的节点 | 该节点自带 scale |
/// |---|---|---|---|
/// | 商店 <c>merchant_room.tscn</c> | **无（1.0）** | <c>NMerchantCharacter</c>（<c>regent_merchant.tscn</c>） | 0.47 |
/// | 假商人 <c>fake_merchant.tscn</c> | **1.75** | <c>NCreatureVisuals</c>（战斗小人 <c>regent.tscn</c>） | 0.29 |
///
/// 也就是说 1.75 是**为战斗形象标定**的（0.29 × 1.75 = 0.5075 ≈ 商店的 0.47）。
///
/// 而 RitsuLib 的 <c>NFakeMerchantProceduralCharacterInstantiationPatch</c> 把
/// <c>NFakeMerchant.AfterRoomIsLoaded</c> 整体替换掉，在事件房里也塞 <c>NMerchantCharacter</c>
/// （节点名 <c>RitsuProceduralMerchant</c>，无自带缩放），于是按 scale=1 标定的
/// <see cref="GaoshouVisualSettings.ShopStyle" />（0.68）被**再放大 1.75 倍** ⇒ 有效 1.19
/// ⇒ 贴图屏幕高约 914px、顶端出屏约 82px ✗
///
/// ═══════════════════════【修法】═══════════════════════
/// RitsuLib 建完节点之后，把**自己的程序化摊位节点**整体乘 <c>1 / 容器缩放</c>。
///
/// * 乘在**节点**上而不是子 Sprite2D 上：后续任何打在 Sprite2D 上的样式
///   （<c>relaxed_loop</c> / <c>dead</c> / 游戏结束界面的 <c>die</c>）都被一次性覆盖，
///   包括游戏结束界面把节点 ReParent 之后 ✓。
/// * 用 <c>container.Scale.X</c> **反算**而不是硬编码 1.75：上游改场景常量时不会失效 ✓。
/// * 只认 RitsuLib 现场造的那个节点（见 <see cref="IsOurProceduralBooth" />），
///   不碰原版 Spine 商人节点，也不碰同队其它角色的摊位 ✓。
///
/// ⚠️ 这是**框架侧**的问题（RitsuLib 把按 scale=1 标定的节点塞进按战斗形象标定的 1.75 容器），
///    影响所有模组、也影响同队的原版角色。这里只是在本模组内兜住。
///
/// ⚠️ 与 RitsuLib 的 <c>NFakeMerchantProceduralCharacterInstantiationPatch</c> 的挂钩点关系：
///    它替换 <c>AfterRoomIsLoaded</c>，而 <c>NFakeMerchant._Ready()</c> 的**最后一句**才是
///    调用 <c>AfterRoomIsLoaded()</c> ⇒ 本 Postfix 挂在 <c>_Ready</c> 之后执行，那时容器已赋值、
///    RitsuLib 的替换版也跑完了。这样也**不依赖**"prefix 返回 false 后 postfix 是否仍执行"这一细节。
/// </summary>
public sealed class FakeMerchantBoothScalePatch : IPatchMethod
{
    /// <summary>RitsuLib 给程序化商人节点起的名字（见 ModWorldSceneVisualNodeFactory）。</summary>
    private const string ProceduralBoothNodeName = "RitsuProceduralMerchant";

    public static string PatchId => "gaoshou_fake_merchant_booth_scale";

    public static string Description =>
        "counter-scale Gaoshou procedural booth visuals in the vanilla FakeMerchant event room";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method<NFakeMerchant>(nameof(NFakeMerchant._Ready)),
    ];

    public static void Postfix(NFakeMerchant __instance)
    {
        if (__instance.GetNodeOrNull<Control>("%CharacterContainer") is not { } container)
            return;

        var k = container.Scale.X;                 // 商店 1.0；假商人 1.75
        if (k <= 0f || Mathf.IsEqualApprox(k, 1f))
            return;                                // 已经是 1 ⇒ 无需补偿（也对商店房间零影响）

        var fixedCount = 0;
        foreach (var child in container.GetChildren())
        {
            if (child is not NMerchantCharacter booth || !IsOurProceduralBooth(booth))
                continue;

            booth.Scale = Vector2.One / k;
            fixedCount++;
        }

        if (fixedCount > 0)
        {
            Entry.Logger.Info(
                $"[FakeMerchant] 容器 scale={k:F2} ⇒ 把 {fixedCount} 个程序化摊位节点反算为 1/{k:F2}");
        }
    }

    /// <summary>
    /// 只认 RitsuLib 现场造的程序化节点：它下面必定有一个直接子 <c>Sprite2D</c> 叫 <c>Visuals</c>，
    /// 且节点名或贴图路径能对上本模组。原版 Spine 商人节点两者都不满足 ⇒ 不会被误改 ✓。
    /// </summary>
    private static bool IsOurProceduralBooth(NMerchantCharacter booth)
    {
        if (booth.GetNodeOrNull<Sprite2D>("Visuals") is not { } sprite)
            return false;

        return string.Equals(booth.Name, ProceduralBoothNodeName, StringComparison.Ordinal)
               || string.Equals(sprite.Texture?.ResourcePath,
                   GaoshouVisualSettings.ShopTexturePath, StringComparison.Ordinal);
    }
}
