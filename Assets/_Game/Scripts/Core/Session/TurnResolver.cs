using System;
using System.Collections.Generic;
using SuitMerge.Core.Config;
using SuitMerge.Core.Model;
using SuitMerge.Core.Rules;

namespace SuitMerge.Core.Session
{
    public enum PlaceRejectReason
    {
        None,
        GameNotPlaying,
        OutOfBoard,
        CellNotExist,
        CellOccupied,
        /// <summary>Số ô thả không khớp Piece (lá đơn cần 1 ô, khối đôi cần 2 ô).</summary>
        PieceShapeMismatch,
        /// <summary>Lượt tutorial có ô gợi ý: chỉ được thả vào đúng ô đó (cờ tutorialHintLocksDrop).</summary>
        NotHintCell
    }

    /// <summary>Kết quả một lượt đặt lá.</summary>
    public sealed class TurnResult
    {
        private readonly int[] _suitActivations; // chỉ số = (int)Suit

        public bool Accepted { get; }
        public PlaceRejectReason RejectReason { get; }
        public IReadOnlyList<GameEvent> Events { get; }
        public int ScoreGained { get; }
        public int MergeCount { get; }
        public int AcesCreated { get; }

        private TurnResult(bool accepted, PlaceRejectReason reason, IReadOnlyList<GameEvent> events,
            int scoreGained, int mergeCount, int acesCreated, int[] suitActivations)
        {
            Accepted = accepted;
            RejectReason = reason;
            Events = events;
            ScoreGained = scoreGained;
            MergeCount = mergeCount;
            AcesCreated = acesCreated;
            _suitActivations = suitActivations ?? new int[5];
        }

        /// <summary>Số lần hiệu ứng chất <paramref name="suit"/> được kích hoạt trong lượt (goal "Kích hoạt ♠ 2 lần" — M4).</summary>
        public int SuitActivations(Suit suit) => _suitActivations[(int)suit];

        internal static TurnResult Rejected(PlaceRejectReason reason) =>
            new TurnResult(false, reason, Array.Empty<GameEvent>(), 0, 0, 0, null);

        internal static TurnResult Success(List<GameEvent> events, MergeResolver.Result r) =>
            new TurnResult(true, PlaceRejectReason.None, events, r.ScoreGained, r.MergeCount, r.AcesCreated,
                r.CopySuitActivations());

        /// <summary>Cùng số liệu, danh sách event mới (GameSession nối thêm event rút lá / thua).</summary>
        internal TurnResult WithEvents(List<GameEvent> events) =>
            new TurnResult(Accepted, RejectReason, events, ScoreGained, MergeCount, AcesCreated, _suitActivations);
    }

    /// <summary>
    /// Xử lý một lượt đặt lá (spec 3.1 bước 1, 3, 4).
    /// Rút lá, bom, kiểm tra thắng/thua sẽ do GameSession đảm nhận ở bước 1.5.
    /// </summary>
    public sealed class TurnResolver
    {
        /// <summary>Thứ tự resolve khối đôi đã làm (spec 4.9, quyết định 24/10/2026: ① trước ②).</summary>
        public const string PairFirstThenSecond = "FIRST_THEN_SECOND";

        private readonly GameConfig _config;
        private readonly MergeResolver _mergeResolver;

        public TurnResolver(GameConfig config)
        {
            _config = config ?? GameConfig.Default;
            _mergeResolver = new MergeResolver(_config);
            if (_config.pairResolveOrder != PairFirstThenSecond)
                throw new NotSupportedException(
                    $"pairResolveOrder = \"{_config.pairResolveOrder}\" chưa làm — hiện chỉ có \"{PairFirstThenSecond}\".");
        }

        /// <summary>
        /// Đặt <paramref name="piece"/>: lá ① vào ô <paramref name="first"/>, lá ② vào ô <paramref name="second"/>
        /// (chỉ khối đôi; phải đúng ô cạnh ① theo hướng khối). Sai số ô / sai ô gợi ý / ô không trống → từ chối,
        /// state giữ nguyên.
        /// </summary>
        public TurnResult PlacePiece(GameState state, Piece piece, CellPos first, CellPos? second = null)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            if (piece.IsPair != second.HasValue) return TurnResult.Rejected(PlaceRejectReason.PieceShapeMismatch);
            if (piece.IsPair && second.Value != piece.SecondCell(first)) return TurnResult.Rejected(PlaceRejectReason.PieceShapeMismatch);
            if (!HintAllows(piece, first, second)) return TurnResult.Rejected(PlaceRejectReason.NotHintCell);
            return piece.IsPair
                ? PlacePair(state, piece, first, second.Value)
                : PlaceCard(state, first, piece.First);
        }

        /// <summary>
        /// Spec 12.1 [GIẢ ĐỊNH]: Piece có ô gợi ý chỉ được thả vào đúng (các) ô đó — tắt bằng tutorialHintLocksDrop.
        /// Khối đôi còn phải đúng hướng lúc rút ra (xoay khác hướng → 2 lá bị đổi chỗ so với hướng dẫn).
        /// </summary>
        private bool HintAllows(Piece piece, CellPos first, CellPos? second)
        {
            if (!_config.tutorialHintLocksDrop || !piece.HasHint) return true;
            if (first != piece.Hint[0]) return false;
            if (piece.Hint.Count < 2) return true;
            return second == piece.Hint[1] && piece.Orientation == piece.HintOrientation;
        }

        private static PlaceRejectReason CheckCell(Board board, CellPos pos)
        {
            if (!board.InBounds(pos)) return PlaceRejectReason.OutOfBoard;
            Cell cell = board[pos];
            if (!cell.Exists) return PlaceRejectReason.CellNotExist;
            return cell.Card != null ? PlaceRejectReason.CellOccupied : PlaceRejectReason.None;
        }

        /// <summary>
        /// Khối đôi (spec 3.1 bước 3–4, 4.9): cả 2 ô phải trống → đặt CẢ 2 lá cùng lúc, rồi resolve ① trước ②.
        /// </summary>
        private TurnResult PlacePair(GameState state, Piece piece, CellPos first, CellPos second)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Status != GameStatus.Playing) return TurnResult.Rejected(PlaceRejectReason.GameNotPlaying);
            Board board = state.Board;
            PlaceRejectReason reason = CheckCell(board, first);
            if (reason == PlaceRejectReason.None) reason = CheckCell(board, second);
            if (reason != PlaceRejectReason.None) return TurnResult.Rejected(reason);

            var events = new List<GameEvent>();
            Card a = piece.First.Clone();
            Card b = piece.Second.Clone();
            board[first].Card = a;
            board[second].Card = b;
            state.Turn++;
            events.Add(new CardPlacedEvent(first, a.Clone()));
            events.Add(new CardPlacedEvent(second, b.Clone()));

            MergeResolver.Result result = _mergeResolver.ResolvePair(state, first, second, b, events);
            return TurnResult.Success(events, result);
        }

        /// <summary>
        /// Đặt <paramref name="card"/> vào ô <paramref name="pos"/> rồi resolve merge/chain.
        /// Ô không hợp lệ → từ chối, state giữ nguyên.
        /// </summary>
        public TurnResult PlaceCard(GameState state, CellPos pos, Card card)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (card == null) throw new ArgumentNullException(nameof(card));

            if (state.Status != GameStatus.Playing) return TurnResult.Rejected(PlaceRejectReason.GameNotPlaying);
            Board board = state.Board;
            PlaceRejectReason reason = CheckCell(board, pos);
            if (reason != PlaceRejectReason.None) return TurnResult.Rejected(reason);

            var events = new List<GameEvent>();
            Card placed = card.Clone();
            board[pos].Card = placed;
            state.Turn++;
            events.Add(new CardPlacedEvent(pos, placed.Clone()));

            MergeResolver.Result result = _mergeResolver.Resolve(state, pos, events);
            return TurnResult.Success(events, result);
        }
    }
}
