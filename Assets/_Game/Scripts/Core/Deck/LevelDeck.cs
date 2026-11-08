using System;
using System.Collections.Generic;
using System.Text;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Deck
{
    /// <summary>
    /// Deck của 1 level (spec 5.1): rút hết đoạn script theo thứ tự (kèm ô gợi ý), sau đó rút ngẫu nhiên có seed
    /// theo rankRange + trọng số. Phần random KHÔNG phụ thuộc bàn → cùng seed = cùng dãy lá cho mọi người chơi.
    /// Mỗi lá random dùng đúng 2 số ngẫu nhiên: 1 chọn rank, 1 chọn chất.
    /// Khối đôi (M4.5, spec 4.9 + 5.1): entry "pair" trong script; phần random với pairRatio &gt; 0 thì mỗi lượt rút
    /// thêm 1 số "ra khối đôi?" TRƯỚC 2 lá — pairRatio = 0 (L1–L12) không rút số này nên dãy lá giữ nguyên như trước.
    /// Deck đếm theo lá: khối đôi tiêu 2 lá; còn 1 lá thì luôn ra lá đơn.
    /// </summary>
    public sealed class LevelDeck : ICountedPieceSource, IRestorablePieceSource
    {
        private readonly DeckData _data;
        private readonly SeededRng _rng;
        private int _scriptIndex;
        private int _drawnCards;

        public LevelDeck(DeckData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (data.RandomCardCount > 0 && data.Random == null)
                throw new ArgumentException($"Deck còn {data.RandomCardCount} lá phải rút ngẫu nhiên nhưng thiếu \"random\".", nameof(data));
            _rng = new SeededRng(data.Random?.Seed ?? 0);
        }

        /// <summary>Số lá chưa rút (HUD "BỘ BÀI").</summary>
        public int CardsLeft => Math.Max(0, _data.Size - _drawnCards);

        public Piece Draw(Board board)
        {
            if (CardsLeft == 0) return null;

            if (_scriptIndex < _data.Script.Count)
            {
                DeckScriptEntry entry = _data.Script[_scriptIndex++];
                _drawnCards += entry.CardCount;
                return entry.IsPair
                    ? Piece.Pair(entry.Cards[0].Clone(), entry.Cards[1].Clone(), entry.Orientation, entry.Hint)
                    : Piece.Single(entry.Cards[0].Clone(), entry.Hint.Count > 0 ? entry.Hint[0] : (CellPos?)null);
            }

            RandomDeckData random = _data.Random;
            bool pair = random.PairRatio > 0 && CardsLeft >= 2 && _rng.NextDouble() < random.PairRatio;
            Card a = DrawRandomCard(random, _rng);
            if (!pair)
            {
                _drawnCards++;
                return Piece.Single(a);
            }
            _drawnCards += 2;
            return Piece.Pair(a, DrawRandomCard(random, _rng)); // 2 lá rút độc lập (spec 4.9 [GIẢ ĐỊNH]), hướng [A B]
        }

        /// <summary>Rút 1 lá ngẫu nhiên: số thứ nhất chọn rank, số thứ hai chọn chất (theo xác suất đã chia phần còn lại).</summary>
        public static Card DrawRandomCard(RandomDeckData random, SeededRng rng)
        {
            Rank rank = Pick(random.Ranks(), random.RankProbability, rng.NextDouble());
            Suit suit = Pick(RandomDeckData.AllSuits, random.SuitProbability, rng.NextDouble());
            return new Card(rank, suit);
        }

        /// <summary>Chọn theo xác suất cộng dồn; sai số làm tròn → lấy mục cuối có xác suất &gt; 0.</summary>
        private static T Pick<T>(IEnumerable<T> items, Func<T, double> probability, double roll)
        {
            double sum = 0;
            T last = default;
            bool any = false;
            foreach (T item in items)
            {
                double p = probability(item);
                if (p <= 0) continue;
                sum += p;
                last = item;
                any = true;
                if (roll < sum) return item;
            }
            if (!any) throw new InvalidOperationException("Mọi lựa chọn đều có xác suất 0.");
            return last;
        }

        // ------------------------------------------------------------------ Undo

        private sealed class State
        {
            public readonly int ScriptIndex;
            public readonly int DrawnCards;
            public readonly uint Rng;

            public State(int scriptIndex, int drawnCards, uint rng)
            {
                ScriptIndex = scriptIndex;
                DrawnCards = drawnCards;
                Rng = rng;
            }
        }

        public object CaptureState() => new State(_scriptIndex, _drawnCards, _rng.State);

        public void RestoreState(object state)
        {
            if (!(state is State s)) throw new ArgumentException("Trạng thái không phải của LevelDeck.", nameof(state));
            _scriptIndex = s.ScriptIndex;
            _drawnCards = s.DrawnCards;
            _rng.State = s.Rng;
        }

        // ------------------------------------------------------------------ Xem trước (menu Kiểm tra file level)

        /// <summary>Toàn bộ dãy Piece sẽ rút, theo đúng thứ tự (dùng deck mới, không ảnh hưởng ván nào).</summary>
        public static IReadOnlyList<Piece> Preview(DeckData data)
        {
            var deck = new LevelDeck(data);
            var pieces = new List<Piece>();
            Piece p;
            while ((p = deck.Draw(null)) != null) pieces.Add(p);
            return pieces;
        }

        /// <summary>"Dãy lá (seed 1001): 2S→(0,2) 3H→(1,2) 2D … | random: 3C 2H".</summary>
        public static string DescribeSequence(DeckData data)
        {
            IReadOnlyList<Piece> pieces = Preview(data);
            int scripted = Math.Min(data.Script.Count, pieces.Count);
            var sb = new StringBuilder(data.Random != null ? $"Dãy lá (seed {data.Random.Seed}):" : "Dãy lá:");
            for (int i = 0; i < pieces.Count; i++)
            {
                if (i == scripted) sb.Append(" | random:");
                sb.Append(' ').Append(pieces[i]);
                if (pieces[i].HasHint) sb.Append('→').Append(pieces[i].Hint[0]);
                if (pieces[i].Hint.Count > 1) sb.Append('+').Append(pieces[i].Hint[1]);
            }
            return sb.ToString();
        }
    }
}
