using System.Collections.Generic;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Core.Simulation
{
    /// <summary>
    /// Nước đi của bot khi Piece hiện tại là khối đôi (M4.5): thử cả 4 hướng xoay × mọi ô của lá ①.
    /// Lá đơn KHÔNG đi qua đây — code bot lá đơn giữ nguyên để kết quả mô phỏng cũ không đổi.
    /// </summary>
    internal static class PairBot
    {
        internal readonly struct Move
        {
            /// <summary>Số lần xoay từ hướng hiện tại (0..3).</summary>
            public readonly int Rotations;
            /// <summary>Ô của lá ①.</summary>
            public readonly CellPos Cell;
            public readonly int Score;
            /// <summary>Số lá kề cùng rank với lá ① + lá ② (tiêu chí hoà của bot Tham lam).</summary>
            public readonly int Neighbors;

            public Move(int rotations, CellPos cell, int score, int neighbors)
            {
                Rotations = rotations;
                Cell = cell;
                Score = score;
                Neighbors = neighbors;
            }
        }

        /// <summary>Mọi nước hợp lệ (xem trước), theo thứ tự: hướng xoay → ô row-major. Khối trên tay quay về hướng cũ.</summary>
        public static List<Move> ValidMoves(GameSession session)
        {
            var moves = new List<Move>();
            Board board = session.Board;
            for (int r = 0; r < 4; r++)
            {
                Piece piece = session.CurrentPiece;
                foreach (CellPos p in board.AllPositions())
                {
                    if (!board.IsPlaceable(p)) continue;
                    PlacementPreview preview = session.PreviewPiece(p);
                    if (!preview.Valid) continue;
                    CellPos second = piece.SecondCell(p);
                    int neighbors = SameRankNeighbors(board, p, piece.First.Rank, second)
                                    + SameRankNeighbors(board, second, piece.Second.Rank, p);
                    moves.Add(new Move(r, p, preview.ScoreGain, neighbors));
                }
                session.RotateCurrentPiece(); // 4 lần xoay = về hướng ban đầu
            }
            return moves;
        }

        /// <summary>Tham lam: nhiều điểm nhất → nhiều lá kề cùng rank nhất → ngẫu nhiên.</summary>
        public static Move ChooseGreedy(List<Move> moves, SeededRng rng)
        {
            var best = new List<Move>();
            foreach (Move m in moves)
            {
                if (best.Count > 0 && (m.Score < best[0].Score || (m.Score == best[0].Score && m.Neighbors < best[0].Neighbors)))
                    continue;
                if (best.Count > 0 && (m.Score > best[0].Score || m.Neighbors > best[0].Neighbors)) best.Clear();
                best.Add(m);
            }
            return best[rng.NextInt(0, best.Count)];
        }

        /// <summary>Xoay đúng số lần rồi đặt.</summary>
        public static TurnResult Play(GameSession session, Move move)
        {
            for (int i = 0; i < move.Rotations; i++) session.RotateCurrentPiece();
            return session.PlacePiece(move.Cell);
        }

        private static int SameRankNeighbors(Board board, CellPos cell, Rank rank, CellPos skip)
        {
            int n = 0;
            foreach (CellPos p in cell.Neighbors4())
            {
                if (p == skip) continue;
                Card c = board.GetCard(p);
                if (c != null && c.Rank == rank) n++;
            }
            return n;
        }
    }
}
