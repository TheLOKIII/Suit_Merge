using System;
using System.Collections.Generic;
using System.Text;

namespace SuitMerge.Core.Model
{
    /// <summary>Lưới ô rows × cols (spec 2.5).</summary>
    public sealed class Board
    {
        private readonly Cell[,] _cells;

        public int Rows { get; }
        public int Cols { get; }

        public Board(int rows, int cols)
        {
            if (rows <= 0 || cols <= 0) throw new ArgumentException("Kích thước bàn phải > 0.");
            Rows = rows;
            Cols = cols;
            _cells = new Cell[rows, cols];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    _cells[r, c] = new Cell();
        }

        public bool InBounds(CellPos p) => p.Row >= 0 && p.Row < Rows && p.Col >= 0 && p.Col < Cols;

        /// <summary>Ô tại p. Ném lỗi nếu p nằm ngoài lưới.</summary>
        public Cell this[CellPos p]
        {
            get
            {
                if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), p.ToString());
                return _cells[p.Row, p.Col];
            }
        }

        public Cell this[int row, int col] => this[new CellPos(row, col)];

        /// <summary>Lá tại p; null nếu ngoài lưới, ô không tồn tại hoặc ô trống.</summary>
        public Card GetCard(CellPos p)
        {
            if (!InBounds(p)) return null;
            Cell cell = _cells[p.Row, p.Col];
            return cell.Exists ? cell.Card : null;
        }

        public bool IsPlaceable(CellPos p) => InBounds(p) && _cells[p.Row, p.Col].IsPlaceable;

        /// <summary>Mọi tọa độ theo thứ tự row-major.</summary>
        public IEnumerable<CellPos> AllPositions()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    yield return new CellPos(r, c);
        }

        public bool HasPlaceableCell()
        {
            foreach (CellPos p in AllPositions())
                if (_cells[p.Row, p.Col].IsPlaceable) return true;
            return false;
        }

        /// <summary>
        /// Mọi cặp ô trống kề nhau (spec 13.1 <c>findPairSlots</c>, luật hết chỗ của khối đôi): mỗi cặp ghi 1 lần
        /// (ô trên/trái trước), theo row-major, cặp ngang trước cặp dọc ở cùng ô.
        /// </summary>
        public List<(CellPos first, CellPos second)> FindPairSlots()
        {
            var slots = new List<(CellPos, CellPos)>();
            foreach (CellPos p in AllPositions())
            {
                if (!IsPlaceable(p)) continue;
                if (IsPlaceable(p.Right)) slots.Add((p, p.Right));
                if (IsPlaceable(p.Down)) slots.Add((p, p.Down));
            }
            return slots;
        }

        /// <summary>Còn ít nhất 1 cặp ô trống kề nhau (ngang / dọc).</summary>
        public bool HasPairSlot()
        {
            foreach (CellPos p in AllPositions())
                if (IsPlaceable(p) && (IsPlaceable(p.Right) || IsPlaceable(p.Down))) return true;
            return false;
        }

        public Board Clone()
        {
            var copy = new Board(Rows, Cols);
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    copy._cells[r, c] = _cells[r, c].Clone();
            return copy;
        }

        /// <summary>
        /// Dựng bàn từ token (spec 12.1): "." ô trống, "#" ô không tồn tại, "2H"/"10S"/"QD" lá đặt sẵn.
        /// Token blocker (ST, FD, ICE, BOMB, @) sẽ hỗ trợ ở M6.
        /// </summary>
        public static Board FromTokens(string[][] rows)
        {
            if (rows == null || rows.Length == 0) throw new ArgumentException("Bàn rỗng.");
            int cols = rows[0].Length;
            var board = new Board(rows.Length, cols);
            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Length != cols)
                    throw new FormatException($"Hàng {r} có {rows[r].Length} ô, cần {cols}.");
                for (int c = 0; c < cols; c++)
                {
                    string token = rows[r][c];
                    Cell cell = board._cells[r, c];
                    if (token == ".") continue;
                    if (token == "#") { cell.Exists = false; continue; }
                    if (Card.TryParse(token, out Card card)) { cell.Card = card; continue; }
                    throw new FormatException($"Token chưa hỗ trợ tại ({r},{c}): '{token}'");
                }
            }
            return board;
        }

        /// <summary>Dựng bàn từ chuỗi nhiều dòng, token cách nhau bởi khoảng trắng. Dòng trống bị bỏ qua.</summary>
        public static Board FromText(string text)
        {
            var rows = new List<string[]>();
            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                rows.Add(trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            }
            return FromTokens(rows.ToArray());
        }

        /// <summary>In bàn ra chữ (cùng định dạng FromText) để debug/log.</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    Cell cell = _cells[r, c];
                    string token = !cell.Exists ? "#" : cell.Card == null ? "." : cell.Card.ToString();
                    sb.Append(token.PadRight(4));
                }
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }
    }
}
