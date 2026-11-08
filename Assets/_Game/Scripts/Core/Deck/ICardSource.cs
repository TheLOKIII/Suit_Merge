using SuitMerge.Core.Model;

namespace SuitMerge.Core.Deck
{
    /// <summary>
    /// Nguồn lá cho một ván. Endless: sinh ngẫu nhiên vô hạn. Level/test: danh sách có giới hạn.
    /// </summary>
    public interface ICardSource
    {
        /// <summary>Rút lá kế tiếp dựa trên bàn hiện tại. Trả null khi hết lá.</summary>
        Card Draw(Board board);
    }
}
