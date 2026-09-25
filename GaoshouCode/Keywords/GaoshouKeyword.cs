using MegaCrit.Sts2.Core.Entities.Cards;
using STS2RitsuLib.Content;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Keywords;

namespace Gaoshou.Keywords;

// 自定义词条（参考 LexNinja2 的 NinjaKeyword）。
// 统一用 [RegisterOwnedCardKeyword(nameof(词条))] 注册：键 = GetQualifiedKeywordId(ModId, 词条) 的字符串限定 ID，
// 本地化键为 card_keywords.GAOSHOU_KEYWORD_<STEM>.title/.description（见 localization/{zhs,eng}/card_keywords.json），
// 悬停即可显示释义。词条机制由卡片 OnPlay / 自定义 Power 驱动（此处仅声明 + 显示）。
// 约定（对齐 LexNinja2）：
//  - None：词条以「[gold]词条[/gold]：效果文本」形式写进卡面描述（流转/增幅/奇迹/囤积），不再自动追加词条行，避免重复。
//  - AfterCardDescription：词条作为独立行追加在描述末尾（幻影/风暴/回响/临时等无内联文本的词条）。
[RegisterOwnedCardKeyword(nameof(Flow), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None, IncludeInCardHoverTip = true,
    IconPath = $"{Entry.ResPath}/images/keywords/flow_on.png")]
[RegisterOwnedCardKeyword(nameof(Amplify), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None, IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Miracle), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None, IncludeInCardHoverTip = true,
    IconPath = $"{Entry.ResPath}/images/keywords/miracle_on.png")]
[RegisterOwnedCardKeyword(nameof(Phantom), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.AfterCardDescription, IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Storm), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None, IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Echo), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.AfterCardDescription, IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Temporary), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.AfterCardDescription, IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Hoard), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None, IncludeInCardHoverTip = true,
    IconPath = $"{Entry.ResPath}/images/powers/hoard.png")]
[RegisterOwnedCardKeyword(nameof(Bonus), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None, IncludeInCardHoverTip = true)]
[RegisterOwnedCardKeyword(nameof(Virtue), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.None, IncludeInCardHoverTip = true)]
    [RegisterOwnedCardKeyword(nameof(Limited), CardDescriptionPlacement = ModKeywordCardDescriptionPlacement.AfterCardDescription, IncludeInCardHoverTip = true)]
public class GaoshouKeyword
{
    public static readonly CardKeyword Flow = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Flow)).GetModCardKeyword();

    public static readonly CardKeyword Amplify = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Amplify)).GetModCardKeyword();

    public static readonly CardKeyword Miracle = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Miracle)).GetModCardKeyword();

    public static readonly CardKeyword Phantom = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Phantom)).GetModCardKeyword();

    public static readonly CardKeyword Storm = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Storm)).GetModCardKeyword();

    public static readonly CardKeyword Echo = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Echo)).GetModCardKeyword();

    public static readonly CardKeyword Temporary = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Temporary)).GetModCardKeyword();

    public static readonly CardKeyword Hoard = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Hoard)).GetModCardKeyword();

    /// <summary>附赠 n：抽到这张牌时，额外抽 n 张牌。</summary>
    public static readonly CardKeyword Bonus = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Bonus)).GetModCardKeyword();

    /// <summary>美德：打出后获得 1 能量，抽 1 张牌。</summary>
    public static readonly CardKeyword Virtue = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Virtue)).GetModCardKeyword();

    /// <summary>限制：每回合每名玩家最多只能打出 1 张带该词条的牌（规则由 LimitedPlayRule 驱动）。</summary>
    public static readonly CardKeyword Limited = ModContentRegistry
        .GetQualifiedKeywordId(Entry.ModId, nameof(Limited)).GetModCardKeyword();
}
