using System;

namespace SuitMerge.Core.Model
{
    /// <summary>
    /// Một lá bài (spec 2.3). Rank luôn trong 2..K — Ace không tồn tại dưới dạng Card.
    /// Các trường blocker (FaceDown, IceLayers, BombCountdown) đã khai báo sẵn cho M6.
    /// </summary>
    public sealed class Card
    {
        public Rank Rank { get; }
        public Suit Suit { get; }
        public bool IsJoker { get; }

        // Trạng thái blocker — dùng từ M6
        public bool FaceDown { get; set; }
        public int IceLayers { get; set; }
        public int? BombCountdown { get; set; }

        public Card(Rank rank, Suit suit)
        {
            if (rank < RankUtil.Lowest || rank > RankUtil.HighestOnBoard)
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Lá bài chỉ có rank 2..K.");
            if (suit == Suit.None)
                throw new ArgumentException("Lá thường phải có chất.", nameof(suit));
            Rank = rank;
            Suit = suit;
        }

        private Card(Rank rank, Suit suit, bool isJoker)
        {
            Rank = rank;
            Suit = suit;
            IsJoker = isJoker;
        }

        /// <summary>Joker (booster, M5). Rank thật được chọn khi tìm nhóm.</summary>
        public static Card CreateJoker() => new Card(Rank.Two, Suit.None, true);

        /// <summary>Lá có tham gia nhóm merge được không (lá úp / còn băng thì không).</summary>
        public bool CanJoinGroup => !FaceDown && IceLayers == 0;

        public Card Clone()
        {
            return new Card(Rank, Suit, IsJoker)
            {
                FaceDown = FaceDown,
                IceLayers = IceLayers,
                BombCountdown = BombCountdown
            };
        }

        /// <summary>Dạng chữ dùng trong file level / log: "7S", "10H", "QD".</summary>
        public override string ToString() => IsJoker ? "JK" : Rank.ToLabel() + Suit.ToCode();

        public static bool TryParse(string token, out Card card)
        {
            card = null;
            if (string.IsNullOrEmpty(token) || token.Length < 2) return false;
            if (!SuitUtil.TryParseCode(token[token.Length - 1], out Suit suit)) return false;
            if (!RankUtil.TryParse(token.Substring(0, token.Length - 1), out Rank rank)) return false;
            if (rank > RankUtil.HighestOnBoard) return false;
            card = new Card(rank, suit);
            return true;
        }

        public static Card Parse(string token)
        {
            if (!TryParse(token, out Card card))
                throw new FormatException($"Lá bài không hợp lệ: '{token}'");
            return card;
        }
    }
}
