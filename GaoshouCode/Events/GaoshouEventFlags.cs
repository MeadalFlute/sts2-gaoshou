namespace Gaoshou.Events;

// 本局运行期标记：是否在「打击大师」里选过"向大师学习"（供格挡达人解锁第三选项）。
// 只活在内存里、随生命周期事件清零：事件（打击大师 → 格挡达人）要跨章节保持，但绝不能跨局泄漏，
// 所以由 Entry.Initialize 订阅 RunStarted/RunLoaded/RunEnded 调 Reset()。
public static class GaoshouEventFlags
{
    public static bool LearnedFromSmiteMaster { get; set; }

    public static void Reset() => LearnedFromSmiteMaster = false;
}
