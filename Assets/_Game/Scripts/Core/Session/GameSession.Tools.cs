using System;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Session
{
    /// <summary>
    /// Công cụ cho scene Sandbox / test (và nền móng cho booster Undo ở M5).
    /// Chỉ THÊM cách thao tác trạng thái — không đổi luật merge / điểm / hiệu ứng.
    /// </summary>
    public sealed partial class GameSession
    {
        /// <summary>Ảnh chụp trạng thái ván (bàn, lá trên tay, điểm, lượt, chất đã mở, thắng/thua).</summary>
        public sealed class Snapshot
        {
            internal GameState State { get; }
            internal uint? Seed { get; }
            /// <summary>Vị trí rút của deck (Level); null nếu nguồn lá không khôi phục được (Endless, Sandbox).</summary>
            internal object DeckState { get; }

            internal Snapshot(GameState state, uint? seed, object deckState)
            {
                State = state;
                Seed = seed;
                DeckState = deckState;
            }
        }

        /// <summary>
        /// Chụp trạng thái hiện tại, gồm cả vị trí rút của deck Level (Undo xong rút lại đúng lá cũ).
        /// Nguồn lá Endless chưa chụp được (booster Undo Endless — M5); Sandbox dùng nguồn lá cố định nên không cần.
        /// </summary>
        public Snapshot CaptureSnapshot() =>
            new Snapshot(S.Clone(), Seed, (_pieceSource as IRestorablePieceSource)?.CaptureState());

        /// <summary>
        /// Khôi phục đúng trạng thái đã chụp (chụp được bao nhiêu lần cũng dùng lại được).
        /// Chỉ dùng snapshot chụp từ CÙNG ván đang chơi (cùng deck).
        /// </summary>
        public void RestoreSnapshot(Snapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _state = snapshot.State.Clone();
            Seed = snapshot.Seed;
            if (snapshot.DeckState != null && _pieceSource is IRestorablePieceSource deck)
                deck.RestoreState(snapshot.DeckState);
        }

        /// <summary>Lối tắt: đặt lá hiện tại + lá kế tiếp (mỗi lá thành Piece lá đơn; null = không có).</summary>
        public void SetHand(Card current, Card next)
        {
            SetHand(current != null ? Piece.Single(current) : null, next != null ? Piece.Single(next) : null);
        }

        /// <summary>Đặt Piece hiện tại + Piece kế tiếp (null = không có).</summary>
        public void SetHand(Piece current, Piece next)
        {
            GameState s = S;
            s.CurrentPiece = current?.Clone();
            s.NextPiece = next?.Clone();
            RecheckAfterEdit();
        }

        /// <summary>
        /// Sửa 1 ô: <paramref name="exists"/> = false → ô khóa (không có lá);
        /// ngược lại đặt <paramref name="card"/> (null = ô trống).
        /// </summary>
        public void SetCell(CellPos pos, Card card, bool exists = true)
        {
            Board board = S.Board;
            if (!board.InBounds(pos)) throw new ArgumentOutOfRangeException(nameof(pos), pos.ToString());
            Cell cell = board[pos];
            cell.Exists = exists;
            cell.Card = exists ? card?.Clone() : null;
            RecheckAfterEdit();
        }

        /// <summary>Bật / tắt hiệu ứng 1 chất (spec 4.7).</summary>
        public void SetSuitEnabled(Suit suit, bool enabled)
        {
            if (suit == Suit.None) throw new ArgumentException("Chất không hợp lệ.", nameof(suit));
            if (enabled) S.EnabledSuitEffects.Add(suit);
            else S.EnabledSuitEffects.Remove(suit);
        }

        /// <summary>Sau khi sửa tay: ván đang "thua" mà giờ lại có chỗ / có lá → chơi tiếp; ngược lại kiểm tra thua.</summary>
        private void RecheckAfterEdit()
        {
            GameState s = S;
            if (s.Status == GameStatus.Lost)
            {
                s.Status = GameStatus.Playing;
                s.LoseReason = LoseReason.None;
            }
            CheckLose(null);
        }
    }
}
