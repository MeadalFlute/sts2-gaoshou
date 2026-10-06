using System.Collections.Generic;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib.Interop.AutoRegistration;

namespace Gaoshou.Keywords;

/// <summary>
/// 「附赠 n」词条的规则：**抽到带该词条的牌时，额外抽 n 张牌**。
///
/// 【2026-10-06 用户要求】把附赠从"每张卡自己 override AfterCardDrawn"**收进词条本身**：
/// 以前 PureAnger / FullyArmed / Cryptic / TheMythicalManMonth 各自抄了一遍同一段逻辑
/// （`if (card != this) return; CardPileCmd.Draw(...)`），任何一张新卡都要再抄一次，
/// 而且"哪些牌有附赠、附赠几张"分散在四处。现在只有**一份**实现，由本单例统一驱动：
///   * 触发条件 = 牌面带有 <see cref="GaoshouKeyword.Bonus" /> 词条（词条即数据）；
///   * 张数 = 该卡动态变量 <see cref="AmountVarName" />（<c>"Bonus"</c>，int）；
///   * 一张没写变量的附赠牌按 <see cref="DefaultAmount" />（1）处理，不会静默失效。
///
/// 写法与同目录的 <see cref="LimitedPlayRule" /> / <see cref="MiracleCounter" /> 完全一致
/// （[RegisterSingleton] + ShouldReceiveCombatHooks + 战斗状态钩子），因此：
///   * 战斗钩子**全网同步**执行，多人两端抽牌数一致，不需要我们自建状态；
///   * 不需要给每张卡加代码，卡面只留 `[gold]附赠[/gold]{Bonus:diff()}` 的文字。
///
/// ⚠️ 铁律：钩子路径里**不出现**闭包 lambda / async 状态机 / 对我方类型的泛型实例化
/// （MonoMod 的 JIT hook 编译它们会抛 ArgumentException，而钩子抛异常会让回合推进停住）。
/// 本文件全部是显式分支 + 纯字段查找，且整体 try/catch 兜底（绝不因为"附赠"把出牌/抽牌卡死）。
/// </summary>
[RegisterSingleton]
public sealed class BonusDrawRule : SingletonModel
{
    /// <summary>附赠张数使用的动态变量名（全模组统一：<c>Bonus</c>，与「神秘」一致）。</summary>
    public const string AmountVarName = "Bonus";

    /// <summary>带了附赠词条、但没写 <c>Bonus</c> 变量时的兜底张数。</summary>
    public const int DefaultAmount = 1;

    public BonusDrawRule()
    {
        ModHelper.SubscribeForCombatStateHooks(Id.Entry, CombatSubModels);
    }

    private IEnumerable<AbstractModel> CombatSubModels(CombatState _)
    {
        yield return this;
    }

    public override bool ShouldReceiveCombatHooks => true;

    /// <summary>
    /// 抽到任意一张牌时判定一次：带附赠词条 → 额外抽 n 张。
    /// （旧实现是每张卡自己的 AfterCardDrawn 里判 `card == this`，本单例天然只看"被抽到的那张牌"。）
    /// </summary>
    public override Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        try
        {
            if (card == null)
                return Task.CompletedTask;

            // 绝大多数牌没有附赠词条：第一句就返回，等于零开销。
            if (!card.Keywords.Contains(GaoshouKeyword.Bonus))
                return Task.CompletedTask;

            var owner = card.Owner;
            if (owner == null)
                return Task.CompletedTask;

            var amount = GetAmount(card);
            if (amount <= 0)
                return Task.CompletedTask;

            return CardPileCmd.Draw(choiceContext, amount, owner);
        }
        catch (System.Exception e)
        {
            // 钩子抛异常 = 抽牌/回合推进可能停住 ⇒ 无论如何都吞掉，只记一次日志。
            if (!_warned)
            {
                _warned = true;
                Entry.Logger.Warn($"[Bonus] draw hook failed, skipped: {e.Message}");
            }

            return Task.CompletedTask;
        }
    }

    private static bool _warned;

    /// <summary>
    /// 读取附赠张数：优先取该卡动态变量 <c>Bonus</c>（升级后的值也会正常反映），
    /// 取不到就按 <see cref="DefaultAmount" /> 兜底。
    /// </summary>
    public static int GetAmount(CardModel? card)
    {
        if (card == null)
            return 0;

        try
        {
            var vars = card.DynamicVars;
            if (vars != null && vars.TryGetValue(AmountVarName, out var variable) && variable is IntVar intVar)
                return (int)intVar.BaseValue;
        }
        catch (System.Exception)
        {
            // 变量集还没建好（预览/克隆的中间态）⇒ 走兜底，绝不抛给上层。
        }

        return DefaultAmount;
    }
}
