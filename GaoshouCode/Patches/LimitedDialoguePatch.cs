using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Patches;

/// <summary>
/// 「限制」挡住出牌时的角色台词。
///
/// 原版 <c>UnplayableReasonExtensions.GetPlayerDialogueLine</c> 只认得五种阻挡者模型
/// （CardModel / RelicModel / PowerModel / EnchantmentModel / AfflictionModel，见反编译
/// <c>UnplayableReasonExtensions.cs:39</c>），其它模型一律取不到名字 ——
/// 我们的规则单例 <see cref="Gaoshou.Keywords.LimitedPlayRule" /> 正好不在其中，
/// 于是玩家看到的是「我被 &lt;Unknown&gt; 所阻挡」。
///
/// 修法：在那个方法之后接管 —— 只要阻挡者是我们的规则，就换成「限制」自己的台词
/// （<c>card_keywords.GAOSHOU_KEYWORD_LIMITED.dialogue</c>），不动原版其它任何情况。
/// </summary>
public sealed class LimitedDialoguePatch : IPatchMethod
{
    public static string PatchId => "gaoshou_limited_dialogue";

    public static string Description => "replace the <Unknown> blocker line shown for the Limited keyword rule";

    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        PatchTarget.Method(
            typeof(UnplayableReasonExtensions),
            "GetPlayerDialogueLine",
            typeof(UnplayableReason),
            typeof(AbstractModel)),
    ];

    public static void Postfix(AbstractModel? preventer, ref LocString? __result)
    {
        if (preventer is Gaoshou.Keywords.LimitedPlayRule)
            __result = new LocString("card_keywords", "GAOSHOU_KEYWORD_LIMITED.dialogue");
    }
}
