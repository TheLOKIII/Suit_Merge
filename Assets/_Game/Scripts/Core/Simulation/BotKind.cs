namespace SuitMerge.Core.Simulation
{
    /// <summary>Kiểu "người chơi" máy dùng trong mô phỏng Endless.</summary>
    public enum BotKind
    {
        /// <summary>Thả vào một ô trống bất kỳ — mức người chơi kém nhất.</summary>
        Random,

        /// <summary>
        /// Mỗi lượt thử mọi ô (PreviewPlacement), chọn ô cho nhiều điểm nhất;
        /// hoà điểm thì ưu tiên ô có nhiều lá kề cùng rank với lá đang cầm (chuẩn bị merge) — mức người chơi khá.
        /// </summary>
        Greedy
    }
}
