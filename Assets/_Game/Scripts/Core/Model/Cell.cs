namespace SuitMerge.Core.Model
{
    /// <summary>
    /// Một ô bàn chơi (spec 2.4). Đá (Stone) sẽ thêm ở M6.
    /// </summary>
    public sealed class Cell
    {
        /// <summary>false = ô ngoài bàn / ô khóa (token "#").</summary>
        public bool Exists { get; set; } = true;
        public Card Card { get; set; }
        /// <summary>Ô ấn (M6). Suit.None = không có ấn.</summary>
        public Suit Sigil { get; set; } = Suit.None;

        /// <summary>Ô trống đặt lá được. Ô có ấn nhưng chưa có lá vẫn là ô trống.</summary>
        public bool IsPlaceable => Exists && Card == null;

        public Cell Clone()
        {
            return new Cell
            {
                Exists = Exists,
                Card = Card?.Clone(),
                Sigil = Sigil
            };
        }
    }
}
