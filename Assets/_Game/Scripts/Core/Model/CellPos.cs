using System;

namespace SuitMerge.Core.Model
{
    /// <summary>Tọa độ ô (row, col), 0-based, gốc góc trên-trái (spec 2.5).</summary>
    public readonly struct CellPos : IEquatable<CellPos>, IComparable<CellPos>
    {
        public readonly int Row;
        public readonly int Col;

        public CellPos(int row, int col)
        {
            Row = row;
            Col = col;
        }

        public CellPos Up => new CellPos(Row - 1, Col);
        public CellPos Right => new CellPos(Row, Col + 1);
        public CellPos Down => new CellPos(Row + 1, Col);
        public CellPos Left => new CellPos(Row, Col - 1);

        /// <summary>4 ô kề cạnh, thứ tự cố định: Trên → Phải → Dưới → Trái.</summary>
        public CellPos[] Neighbors4() => new[] { Up, Right, Down, Left };

        public int ManhattanTo(CellPos other) => Math.Abs(Row - other.Row) + Math.Abs(Col - other.Col);

        /// <summary>So sánh row-major (hàng trước, cột sau).</summary>
        public int CompareTo(CellPos other)
        {
            int byRow = Row.CompareTo(other.Row);
            return byRow != 0 ? byRow : Col.CompareTo(other.Col);
        }

        public bool Equals(CellPos other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is CellPos other && Equals(other);
        public override int GetHashCode() => (Row * 397) ^ Col;
        public static bool operator ==(CellPos a, CellPos b) => a.Equals(b);
        public static bool operator !=(CellPos a, CellPos b) => !a.Equals(b);

        public override string ToString() => $"({Row},{Col})";
    }
}
