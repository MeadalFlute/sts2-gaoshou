using System;
using System.Reflection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using Gaoshou.Keywords;
using STS2RitsuLib.Patching.Models;

namespace Gaoshou.Cards;

// 拦截 CombatState.CloneCard：所有幻影/复制生成的卡牌实例在创建时登记颜色——
// 双色卡随机抽取一个主色（实例级），mana 颜色释义按实例色显示单色。
public class PhantomCloneColorPatch : IPatchMethod
{
    public static string PatchId => "gaoshou_phantom_clone_color";
    public static string Description => "random single color for phantom copies of dual-color cards";
    public static bool IsCritical => false;

    public static ModPatchTarget[] GetTargets() =>
    [
        new(typeof(CombatState), "CloneCard"),
    ];

    public static void Postfix(CardModel card, ref CardModel __result)
    {
        if (__result == null || card?.Id?.Entry?.StartsWith("GAOSHOU_CARD") != true)
            return;
        if (card.GetType().GetProperty("CardColor")?.GetValue(card) is not GaoshouCardColor srcColor)
            return;

        var primaries = PhantomColorRegistry.GetPrimaries(srcColor);
        PhantomColorRegistry.Assign(__result,
            primaries.Count > 1 ? primaries[Random.Shared.Next(primaries.Count)] : srcColor);
    }
}