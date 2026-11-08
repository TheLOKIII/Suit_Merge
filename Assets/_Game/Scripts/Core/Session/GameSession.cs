using System;
using System.Collections.Generic;
using SuitMerge.Core.Config;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Goals;
using SuitMerge.Core.Model;
using SuitMerge.Core.Rules;

namespace SuitMerge.Core.Session
{
    /// <summary>
    /// Quản lý trọn một ván. Đây là API duy nhất View được gọi — View KHÔNG tự sửa state.
    /// Một lượt (spec 3.1): đặt Piece → merge/chain → Piece Next thành Piece hiện tại → rút Piece Next mới → kiểm tra thua.
    /// Piece = lá đơn hoặc khối đôi (M4.5); các hàm "Card" (CurrentCard, PlaceCard…) là lối tắt tới lá ①.
    /// </summary>
    public sealed partial class GameSession
    {
        public const int EndlessBoardSize = 5;

        private static readonly Suit[] AllSuits = { Suit.Heart, Suit.Diamond, Suit.Club, Suit.Spade };

        private readonly GameConfig _config;
        private readonly TurnResolver _turnResolver;
        private GameState _state;
        private IPieceSource _pieceSource;

        public GameSession(GameConfig config = null)
        {
            _config = config ?? GameConfig.Default;
            _turnResolver = new TurnResolver(_config);
        }

        // ---------- Trạng thái (chỉ đọc) ----------
        public bool IsStarted => _state != null;
        public GameMode Mode => S.Mode;
        /// <summary>Bàn hiện tại. View chỉ được ĐỌC.</summary>
        public Board Board => S.Board;
        /// <summary>Piece đang cầm; null = hết lá.</summary>
        public Piece CurrentPiece => S.CurrentPiece;
        /// <summary>Piece UP NEXT; null = hết lá để rút.</summary>
        public Piece NextPiece => S.NextPiece;
        /// <summary>Lối tắt: lá ① của Piece đang cầm.</summary>
        public Card CurrentCard => S.CurrentPiece?.First;
        /// <summary>Lối tắt: lá ① của Piece UP NEXT.</summary>
        public Card NextCard => S.NextPiece?.First;
        public int Score => S.Score;
        public int Turn => S.Turn;
        public GameStatus Status => S.Status;
        public LoseReason LoseReason => S.LoseReason;
        public IReadOnlyCollection<Suit> EnabledSuitEffects => S.EnabledSuitEffects;
        /// <summary>Seed của ván Endless; null nếu ván dùng nguồn lá khác.</summary>
        public uint? Seed { get; private set; }

        private GameState S => _state ?? throw new InvalidOperationException(
            "Chưa bắt đầu ván — gọi StartEndless() hoặc Start() trước.");

        // ---------- Bắt đầu ván ----------

        /// <summary>
        /// Ván Endless mới: bàn 5×5 trống, mở cả 4 chất (spec 4.7, 5.2). Gọi lại để chơi lại.
        /// Có khối đôi khi endlessPairRatio &gt; 0 (spec 4.9); = 0 thì dãy lá y hệt trước M4.5.
        /// </summary>
        public void StartEndless(uint seed)
        {
            var cards = new EndlessCardGenerator(seed, _config);
            IPieceSource source = _config.endlessPairRatio > 0
                ? new EndlessPieceSource(cards, _config.endlessPairRatio, seed)
                : (IPieceSource)new SinglePieceSource(cards);
            Start(new Board(EndlessBoardSize, EndlessBoardSize), source, GameMode.Endless, AllSuits);
            Seed = seed;
        }

        /// <summary>Lối tắt: ván với nguồn LÁ — mỗi lá thành 1 Piece lá đơn.</summary>
        public void Start(Board board, ICardSource cardSource, GameMode mode, IEnumerable<Suit> enabledSuitEffects = null)
        {
            if (cardSource == null) throw new ArgumentNullException(nameof(cardSource));
            Start(board, new SinglePieceSource(cardSource), mode, enabledSuitEffects);
        }

        /// <summary>Ván với bàn + nguồn Piece tùy chọn (test, Level ở M4).</summary>
        public void Start(Board board, IPieceSource pieceSource, GameMode mode, IEnumerable<Suit> enabledSuitEffects = null)
        {
            if (board == null) throw new ArgumentNullException(nameof(board));
            _pieceSource = pieceSource ?? throw new ArgumentNullException(nameof(pieceSource));
            Seed = null;
            CurrentLevel = null;

            _state = new GameState(board, mode);
            if (enabledSuitEffects != null)
                foreach (Suit suit in enabledSuitEffects)
                    _state.EnabledSuitEffects.Add(suit);

            _state.CurrentPiece = _pieceSource.Draw(board);
            _state.NextPiece = _state.CurrentPiece != null ? _pieceSource.Draw(board) : null;
            CheckLose(null);
        }

        // ---------- Lượt chơi ----------

        /// <summary>Lối tắt: đặt Piece lá đơn vào ô <paramref name="pos"/>.</summary>
        public TurnResult PlaceCard(CellPos pos) => PlacePiece(pos);

        /// <summary>
        /// Đặt Piece hiện tại: lá ① vào ô <paramref name="first"/>, lá ② (khối đôi) vào ô <paramref name="second"/>.
        /// Lá đơn thì không truyền <paramref name="second"/>; khối đôi bỏ trống = ô cạnh ① theo hướng khối.
        /// Ô không hợp lệ / sai hình dạng → từ chối, KHÔNG mất lá, state giữ nguyên.
        /// </summary>
        public TurnResult PlacePiece(CellPos first, CellPos? second = null)
        {
            GameState s = S;
            if (s.Status != GameStatus.Playing || s.CurrentPiece == null)
                return TurnResult.Rejected(PlaceRejectReason.GameNotPlaying);

            TurnResult result = _turnResolver.PlacePiece(s, s.CurrentPiece, first, SecondCellOf(s.CurrentPiece, first, second));
            if (!result.Accepted) return result;

            var events = new List<GameEvent>(result.Events);

            // Rút (spec 3.1 bước 6) — Piece Next được rút dựa trên bàn SAU khi resolve
            s.CurrentPiece = s.NextPiece;
            s.NextPiece = s.CurrentPiece != null ? _pieceSource.Draw(s.Board) : null;
            events.Add(new CardsDrawnEvent(s.CurrentPiece?.Clone(), s.NextPiece?.Clone()));

            // Cập nhật mục tiêu → kiểm tra THẮNG trước, rồi mới THUA (spec 3.1 bước 7, spec 7)
            UpdateGoals(result, events);
            CheckLose(events);

            return result.WithEvents(events);
        }

        /// <summary>Lối tắt: xem trước việc thả Piece lá đơn vào ô <paramref name="pos"/>.</summary>
        public PlacementPreview PreviewPlacement(CellPos pos) => PreviewPiece(pos);

        /// <summary>
        /// Xem trước việc thả Piece hiện tại (tham số như <see cref="PlacePiece"/>): chạy thử trên BẢN SAO trạng thái
        /// (không rút lá, không đổi bàn / điểm / lượt / dãy lá sẽ rút).
        /// </summary>
        public PlacementPreview PreviewPiece(CellPos first, CellPos? second = null)
        {
            GameState s = S;
            if (s.Status != GameStatus.Playing || s.CurrentPiece == null) return PlacementPreview.Invalid;

            GameState copy = s.Clone();
            CellPos? secondCell = SecondCellOf(copy.CurrentPiece, first, second);
            TurnResult result = _turnResolver.PlacePiece(copy, copy.CurrentPiece, first, secondCell);
            if (!result.Accepted) return PlacementPreview.Invalid;

            // Chỉ bước merge đầu tiên: nhóm + hiệu ứng chất của nó (các event trước MergeStarted thứ hai)
            var firstGroup = new List<CellPos>();
            Suit effect = Suit.None;
            IReadOnlyList<CellPos> spadeArea = null;
            CellPos? clubSpawn = null;
            bool inFirstStep = false;
            foreach (GameEvent e in result.Events)
            {
                if (e is MergeStartedEvent merge)
                {
                    if (inFirstStep) break;
                    inFirstStep = true;
                    foreach (CellPos p in merge.Group)
                        if (p != first && p != secondCell) firstGroup.Add(p);
                }
                else if (!inFirstStep) continue;
                else if (e is DiamondBonusEvent) effect = Suit.Diamond;
                else if (e is HeartLeapEvent) effect = Suit.Heart;
                else if (e is ClubSpawnEvent club) { effect = Suit.Club; clubSpawn = club.SpawnCell; }
                else if (e is SpadeClearEvent spade) { effect = Suit.Spade; spadeArea = spade.Area; }
            }

            return new PlacementPreview(true, firstGroup, result.MergeCount, result.ScoreGained, result.AcesCreated,
                effect, spadeArea, clubSpawn);
        }

        /// <summary>Khối đôi không truyền ô ② → ô cạnh ① theo hướng hiện tại; lá đơn giữ nguyên.</summary>
        private static CellPos? SecondCellOf(Piece piece, CellPos first, CellPos? second) =>
            piece != null && piece.IsPair && !second.HasValue ? piece.SecondCell(first) : second;

        /// <summary>
        /// Chạm vào khối đôi trên tay: xoay 90° (spec 4.9), không tốn lượt. Trả null (không làm gì) nếu không phải
        /// khối đôi hoặc ván đã kết thúc.
        /// </summary>
        public PieceRotatedEvent RotateCurrentPiece()
        {
            GameState s = S;
            if (s.Status != GameStatus.Playing || s.CurrentPiece == null || !s.CurrentPiece.IsPair) return null;
            s.CurrentPiece = s.CurrentPiece.Rotated();
            return new PieceRotatedEvent(s.CurrentPiece.Clone());
        }

        /// <summary>Level: cộng tiến độ mục tiêu; xong tất cả → THẮNG (thắng ưu tiên hơn thua trong cùng lượt).</summary>
        private void UpdateGoals(TurnResult turn, List<GameEvent> events)
        {
            GameState s = _state;
            if (s.Goals.Count == 0) return;

            events.AddRange(GoalTracker.Apply(s.Goals, s.Board, s.Score, turn.Events, turn, _config));
            if (s.Status != GameStatus.Playing || !s.Goals.TrueForAll(g => g.IsDone)) return;

            s.Status = GameStatus.Won;
            events.Add(new LevelWonEvent(s.Score));
        }

        private void CheckLose(List<GameEvent> events)
        {
            GameState s = _state;
            if (s.Status != GameStatus.Playing) return;

            LoseReason reason = LoseReason.None;
            if (!s.Board.HasPlaceableCell()) reason = LoseReason.BoardFull;
            else if (s.CurrentPiece == null) reason = LoseReason.OutOfCards;
            // Spec 7 điều kiện 3 — cứu bằng Swap / Hammer sẽ chen vào đây ở M5
            else if (s.CurrentPiece.IsPair && !s.Board.HasPairSlot()) reason = LoseReason.NoSpaceForPair;
            if (reason == LoseReason.None) return;

            s.Status = GameStatus.Lost;
            s.LoseReason = reason;
            events?.Add(new GameOverEvent(reason, s.Score));
        }
    }
}
