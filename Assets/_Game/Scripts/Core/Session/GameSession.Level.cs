using System;
using System.Collections.Generic;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Goals;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Session
{
    /// <summary>Chế độ Level (M4): bắt đầu ván từ dữ liệu level + đếm lá còn lại trong deck.</summary>
    public sealed partial class GameSession
    {
        /// <summary>Level đang chơi; null với Endless / ván dựng tay.</summary>
        public LevelData CurrentLevel { get; private set; }

        /// <summary>Số lá chưa rút (HUD "BỘ BÀI", không tính Piece hiện tại / Next); null = deck vô hạn (Endless).</summary>
        public int? CardsLeftInDeck => (_pieceSource as ICountedPieceSource)?.CardsLeft;

        /// <summary>Tiến độ mục tiêu (cùng thứ tự file level); rỗng với Endless. View chỉ được ĐỌC.</summary>
        public IReadOnlyList<GoalProgress> Goals => S.Goals;

        /// <summary>Đã thắng level (mọi mục tiêu xong).</summary>
        public bool IsWon => S.Status == GameStatus.Won;

        /// <summary>
        /// Ván Level mới: bàn dựng mới từ file, deck = script rồi random có seed, mở đúng các chất của level.
        /// Gọi lại để chơi lại từ đầu (cùng dãy lá).
        /// </summary>
        public void StartLevel(LevelData level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            Start(level.CreateBoard(), new LevelDeck(level.Deck), GameMode.Level, level.EnabledSuitEffects);
            CurrentLevel = level;
            foreach (GoalData goal in level.Goals) _state.Goals.Add(new GoalProgress(goal));
        }

        /// <summary>
        /// Xem trước tối đa <paramref name="count"/> Piece sẽ rút SAU lá UP NEXT, không làm thay đổi deck (Sandbox).
        /// Rỗng nếu nguồn lá không đếm / không khôi phục được (Endless).
        /// </summary>
        public IReadOnlyList<Piece> PeekUpcoming(int count)
        {
            var pieces = new List<Piece>();
            if (!(_pieceSource is IRestorablePieceSource deck) || !(_pieceSource is ICountedPieceSource)) return pieces;
            object saved = deck.CaptureState();
            try
            {
                Piece p;
                while (pieces.Count < count && (p = _pieceSource.Draw(S.Board)) != null) pieces.Add(p);
            }
            finally
            {
                deck.RestoreState(saved);
            }
            return pieces;
        }
    }
}
