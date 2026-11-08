using System.Collections.Generic;
using SuitMerge.Core.Goals;

namespace SuitMerge.Core.Model
{
    public enum GameMode
    {
        Endless,
        Level
    }

    public enum GameStatus
    {
        Playing,
        Won,
        Lost
    }

    /// <summary>Lý do thua (spec 7).</summary>
    public enum LoseReason
    {
        None,
        /// <summary>Sau khi resolve không còn ô trống.</summary>
        BoardFull,
        /// <summary>Hết lá (chỉ xảy ra với nguồn lá có giới hạn: Level / test).</summary>
        OutOfCards,
        /// <summary>
        /// Piece hiện tại là khối đôi mà bàn không còn cặp ô trống kề nhau (spec 7 điều kiện 3, <c>NO_SPACE_FOR_PAIR</c>).
        /// Popup cứu bằng Swap / Hammer: M5.
        /// </summary>
        NoSpaceForPair
    }

    /// <summary>
    /// Toàn bộ trạng thái một ván (spec 2.6). Booster, history sẽ thêm ở M5. Vị trí deck nằm ở nguồn lá (Snapshot chụp riêng).
    /// </summary>
    public sealed class GameState
    {
        public GameMode Mode { get; set; }
        public Board Board { get; set; }
        /// <summary>Piece đang cầm (spec 2.6). null = hết lá.</summary>
        public Piece CurrentPiece { get; set; }
        /// <summary>Piece UP NEXT. null = hết lá để rút.</summary>
        public Piece NextPiece { get; set; }
        public int Score { get; set; }
        public int Turn { get; set; }
        /// <summary>Chất nào đã mở hiệu ứng (spec 4.7). Dùng từ bước 1.4.</summary>
        public HashSet<Suit> EnabledSuitEffects { get; set; } = new HashSet<Suit>();
        /// <summary>Tiến độ mục tiêu của Level (spec 6). Rỗng = không có mục tiêu (Endless).</summary>
        public List<GoalProgress> Goals { get; set; } = new List<GoalProgress>();
        public GameStatus Status { get; set; } = GameStatus.Playing;
        public LoseReason LoseReason { get; set; } = LoseReason.None;

        public GameState(Board board, GameMode mode = GameMode.Level)
        {
            Board = board;
            Mode = mode;
        }

        public GameState Clone()
        {
            return new GameState(Board.Clone(), Mode)
            {
                CurrentPiece = CurrentPiece?.Clone(),
                NextPiece = NextPiece?.Clone(),
                Score = Score,
                Turn = Turn,
                EnabledSuitEffects = new HashSet<Suit>(EnabledSuitEffects),
                Goals = Goals.ConvertAll(g => g.Clone()),
                Status = Status,
                LoseReason = LoseReason
            };
        }
    }
}
