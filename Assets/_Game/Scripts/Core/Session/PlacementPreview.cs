using System;
using System.Collections.Generic;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Session
{
    /// <summary>
    /// Kết quả xem trước "nếu thả lá hiện tại vào ô này" — tính trên bản sao, không đổi ván đang chơi.
    /// View dùng để làm sáng nhóm sẽ merge khi đang kéo (spec 10.3).
    /// </summary>
    public sealed class PlacementPreview
    {
        public static readonly PlacementPreview Invalid =
            new PlacementPreview(false, Array.Empty<CellPos>(), 0, 0, 0);

        /// <summary>Thả vào ô này có hợp lệ không (ô trống, ván đang chơi).</summary>
        public bool Valid { get; }

        /// <summary>
        /// Các ô ĐANG có lá sẽ merge cùng lá vừa thả ở bước merge đầu tiên (không gồm ô thả).
        /// Rỗng nếu không tạo merge.
        /// </summary>
        public IReadOnlyList<CellPos> FirstGroupCells { get; }

        /// <summary>Tổng số bước merge (1 = merge thường, ≥2 = có chain).</summary>
        public int MergeSteps { get; }
        public int ScoreGain { get; }
        public int AcesCreated { get; }

        public bool WillMerge => MergeSteps > 0;

        // ---------- Hiệu ứng chất của bước merge ĐẦU TIÊN (bước 1.4, spec 4.4 + 10.3) ----------

        /// <summary>Chất sẽ kích hoạt ở bước merge đầu tiên; Suit.None nếu không có (icon chất sáng lên).</summary>
        public Suit FirstEffectSuit { get; }

        /// <summary>♠: các ô trong vùng 3×3 sẽ bị quét (tô đỏ). Rỗng nếu bước đầu không phải ♠.</summary>
        public IReadOnlyList<CellPos> SpadeArea { get; }

        /// <summary>♣: ô sẽ sinh lá (viền xanh). null nếu bước đầu không phải ♣ hoặc bàn đầy.</summary>
        public CellPos? ClubSpawnCell { get; }

        public PlacementPreview(bool valid, IReadOnlyList<CellPos> firstGroupCells, int mergeSteps, int scoreGain, int acesCreated)
            : this(valid, firstGroupCells, mergeSteps, scoreGain, acesCreated, Suit.None, null, null)
        {
        }

        public PlacementPreview(bool valid, IReadOnlyList<CellPos> firstGroupCells, int mergeSteps, int scoreGain, int acesCreated,
            Suit firstEffectSuit, IReadOnlyList<CellPos> spadeArea, CellPos? clubSpawnCell)
        {
            Valid = valid;
            FirstGroupCells = firstGroupCells;
            MergeSteps = mergeSteps;
            ScoreGain = scoreGain;
            AcesCreated = acesCreated;
            FirstEffectSuit = firstEffectSuit;
            SpadeArea = spadeArea ?? Array.Empty<CellPos>();
            ClubSpawnCell = clubSpawnCell;
        }
    }
}
