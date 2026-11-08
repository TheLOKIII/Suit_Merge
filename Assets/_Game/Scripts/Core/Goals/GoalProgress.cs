using SuitMerge.Core.Level;

namespace SuitMerge.Core.Goals
{
    /// <summary>
    /// Tiến độ 1 mục tiêu trong ván (spec 2.6 goals). Nằm trong GameState → Undo khôi phục đúng.
    /// <see cref="Current"/> không bao giờ giảm (điểm chỉ tăng, số lần chỉ cộng) nên đã xong là xong luôn.
    /// </summary>
    public sealed class GoalProgress
    {
        public GoalData Goal { get; }
        /// <summary>Giá trị hiện tại (SCORE: điểm; loại khác: số lần). Có thể vượt <see cref="Target"/> — View tự cắt khi hiển thị.</summary>
        public int Current { get; internal set; }
        public int Target => Goal.Target;
        public bool IsDone => Current >= Target;

        public GoalProgress(GoalData goal, int current = 0)
        {
            Goal = goal;
            Current = current;
        }

        public GoalProgress Clone() => new GoalProgress(Goal, Current);

        public override string ToString() => $"{LevelLoader.Describe(Goal)}: {Current}/{Target}";
    }
}
