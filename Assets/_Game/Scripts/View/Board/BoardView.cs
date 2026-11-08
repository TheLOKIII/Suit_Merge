using System.Collections;
using System.Collections.Generic;
using SuitMerge.Core.Model;
using SuitMerge.View.Animation;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Board
{
    /// <summary>
    /// Bàn chơi: khung nền + lưới ô (Figma board-5x5: lề 12, ô 84, khe 6).
    /// Dùng lại các ô có sẵn trong scene nếu đúng số lượng, ngược lại tự sinh từ prefab.
    /// Bước 4.5: bàn khác 5×5 → khung giữ nguyên, ô PHÓNG TO / thu nhỏ cho vừa khung và căn giữa (4×4 ≈ 106.5).
    /// Animation phần 3: <see cref="SetDanger"/> — viền đỏ phát sáng quanh bàn khi sắp đầy (App quyết định lúc nào bật).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BoardView : MonoBehaviour
    {
        [SerializeField] private CellView cellPrefab;
        [SerializeField] private RectTransform cellRoot;
        [SerializeField] private float padding = 12f;
        [SerializeField] private float cellSize = 84f;
        [SerializeField] private float gap = 6f;

        private readonly List<CellView> _cells = new List<CellView>();
        private int _rows;
        private int _cols;
        private readonly List<CellView> _highlighted = new List<CellView>();
        private readonly List<CellView> _invalid = new List<CellView>();
        private readonly List<CellView> _hinted = new List<CellView>();
        private FxTheme _fxTheme;
        // Bố cục hiện tại (tính trong EnsureGrid); trước đó = bố cục 5×5 gốc (dùng khi dựng scene trong Editor)
        private float _layoutCell = -1f;
        private Vector2 _layoutOrigin;
        private readonly List<CellView> _glowing = new List<CellView>();
        private readonly List<CellView> _zoned = new List<CellView>();
        private Canvas _canvas;

        // Animation phần 3 — viền cảnh báo bàn sắp đầy
        private static readonly Color DangerEdgeColor = new Color32(0xD8, 0x48, 0x3F, 0xFF);
        private const float DangerMinAlpha = 0.35f;
        private const float DangerMaxAlpha = 0.85f;
        private const float DangerStaticAlpha = 0.6f;
        private const float DangerPeriod = 1.4f;
        private const float DangerFade = 0.25f;
        private Image _dangerEdge;
        private Coroutine _dangerRoutine;
        private bool _danger;

        /// <summary>Còn ≤ số ô trống này (đang chơi) → viền đỏ cảnh báo (quyết định Animation phần 3).</summary>
        public const int DangerEmptyCells = 2;

        /// <summary>Bật viền đỏ khi đang chơi và bàn còn ≤ <see cref="DangerEmptyCells"/> ô trống đặt được.</summary>
        public void SetDangerFor(Core.Model.Board board, bool playing)
        {
            int empty = 0;
            if (board != null)
                foreach (CellPos p in board.AllPositions())
                    if (board.IsPlaceable(p)) empty++;
            SetDanger(playing && board != null && empty <= DangerEmptyCells);
        }

        public int Rows => _rows;
        public int Cols => _cols;

        /// <summary>Cạnh 1 ô (đơn vị UI) của bàn hiện tại.</summary>
        public float CellSize => _layoutCell > 0f ? _layoutCell : cellSize;

        /// <summary>Ảnh overlay Figma cho các ô (EventPlayer gán lúc khởi động). null = cách vẽ cũ.</summary>
        public void SetFxTheme(FxTheme theme)
        {
            _fxTheme = theme;
            foreach (CellView c in _cells) c.SetFxTheme(theme);
        }

        public CellView GetCell(CellPos p) =>
            p.Row >= 0 && p.Row < _rows && p.Col >= 0 && p.Col < _cols ? _cells[p.Row * _cols + p.Col] : null;

        public IReadOnlyList<CellView> Cells => _cells;

        /// <summary>
        /// Bật / tắt viền đỏ phát sáng quanh bàn (cảnh báo sắp hết ô trống — không đổi luật). Viền "thở" chậm;
        /// Cài đặt GIẢM BỚT: đứng yên. Chưa có ảnh FxTheme.edgeGlow = không có viền.
        /// </summary>
        public void SetDanger(bool on)
        {
            if (on == _danger) return;
            _danger = on;
            Sprite sprite = _fxTheme != null ? _fxTheme.edgeGlow : null;
            if (sprite == null) return;
            if (_dangerEdge == null)
            {
                var go = new GameObject("DangerEdge", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                _dangerEdge = go.AddComponent<Image>();
                _dangerEdge.raycastTarget = false;
            }
            _dangerEdge.sprite = sprite;
            _dangerEdge.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            _dangerEdge.transform.SetAsLastSibling(); // trên các ô
            if (_dangerRoutine != null) StopCoroutine(_dangerRoutine);
            _dangerRoutine = null;
            if (isActiveAndEnabled) _dangerRoutine = StartCoroutine(DangerRoutine(on));
            else
            {
                _dangerEdge.gameObject.SetActive(on);
                SetDangerAlpha(on ? DangerStaticAlpha : 0f);
            }
        }

        private IEnumerator DangerRoutine(bool on)
        {
            _dangerEdge.gameObject.SetActive(true);
            float from = _dangerEdge.color.a;
            float peak = FxSettings.Reduced ? DangerStaticAlpha : DangerMaxAlpha;
            yield return Tween.Run(DangerFade, k => SetDangerAlpha(Mathf.Lerp(from, on ? peak : 0f, k)));
            if (!on)
            {
                _dangerEdge.gameObject.SetActive(false);
                _dangerRoutine = null;
                yield break;
            }
            if (FxSettings.Reduced)
            {
                _dangerRoutine = null;
                yield break;
            }
            float t = 0f;
            while (true)
            {
                float k = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * t / DangerPeriod); // bắt đầu ở đỉnh
                SetDangerAlpha(Mathf.Lerp(DangerMinAlpha, DangerMaxAlpha, k));
                t += Time.deltaTime;
                yield return null;
            }
        }

        private void SetDangerAlpha(float a)
        {
            Color c = DangerEdgeColor;
            c.a = a;
            _dangerEdge.color = c;
        }

        private void OnDisable()
        {
            _dangerRoutine = null;
            if (_dangerEdge != null) _dangerEdge.gameObject.SetActive(_danger);
        }

        /// <summary>Chuẩn bị lưới rows × cols và vẽ toàn bộ bàn.</summary>
        public void Render(Core.Model.Board board)
        {
            EnsureGrid(board.Rows, board.Cols);
            foreach (CellPos p in board.AllPositions())
                GetCell(p).Render(board[p]);
        }

        public void EnsureGrid(int rows, int cols)
        {
            if (rows == _rows && cols == _cols && _cells.Count == rows * cols) return;
            RectTransform root = cellRoot != null ? cellRoot : (RectTransform)transform;

            _cells.Clear();
            var existing = new List<CellView>(root.GetComponentsInChildren<CellView>(true));
            if (existing.Count != rows * cols)
            {
                foreach (CellView c in existing) Destroy(c.gameObject);
                existing.Clear();
                for (int i = 0; i < rows * cols; i++) existing.Add(Instantiate(cellPrefab, root));
            }

            _rows = rows;
            _cols = cols;
            ComputeLayout(rows, cols);
            // Ô cũ có thể vừa bị huỷ (đổi kích thước bàn) → bỏ mọi tham chiếu tới chúng
            _highlighted.Clear();
            _invalid.Clear();
            _hinted.Clear();
            _glowing.Clear();
            _zoned.Clear();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    CellView cell = existing[r * cols + c];
                    cell.Init(new CellPos(r, c));
                    cell.SetFxTheme(_fxTheme);
                    PlaceCell(cell.RectTransform, r, c);
                    _cells.Add(cell);
                }
        }

        /// <summary>Ô nằm dưới điểm <paramref name="screenPoint"/> (toạ độ màn hình); false nếu ngoài bàn.</summary>
        public bool TryGetCellAt(Vector2 screenPoint, out CellView cell)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            foreach (CellView c in _cells)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(c.RectTransform, screenPoint, cam))
                {
                    cell = c;
                    return true;
                }
            }
            cell = null;
            return false;
        }

        /// <summary>Làm sáng 1 ô (null = tắt sáng).</summary>
        public void SetHighlight(CellView cell) => SetHighlight(cell, null);

        /// <summary>
        /// M4.5: làm sáng ô đích của cả khối đôi (null = bỏ qua ô đó; cả 2 null = tắt sáng).
        /// Animation phần 2: ô vừa được trỏ vào thì nảy.
        /// </summary>
        public void SetHighlight(CellView first, CellView second)
        {
            if (!SetMarked(_highlighted, first, second, (c, on) => c.SetHighlight(on))) return;
            foreach (CellView c in _highlighted) c.Bump();
        }

        /// <summary>Bước 4.5: ô đang trỏ tới nhưng không thả được (null = tắt).</summary>
        public void SetInvalid(CellView cell) => SetInvalid(cell, null);

        /// <summary>M4.5: ô ① / ô ② của khối đôi không thả được (null = bỏ qua ô đó).</summary>
        public void SetInvalid(CellView first, CellView second) => SetMarked(_invalid, first, second, (c, on) => c.SetInvalid(on));

        /// <summary>Đánh dấu đúng các ô cho trước; trả false nếu không có gì đổi.</summary>
        private static bool SetMarked(List<CellView> marked, CellView first, CellView second, System.Action<CellView, bool> set)
        {
            if (marked.Count == (first != null ? 1 : 0) + (second != null && second != first ? 1 : 0)
                && (first == null || marked.Contains(first)) && (second == null || marked.Contains(second)))
                return false;
            foreach (CellView c in marked) set(c, false);
            marked.Clear();
            if (first != null) marked.Add(first);
            if (second != null && second != first) marked.Add(second);
            foreach (CellView c in marked) set(c, true);
            return true;
        }

        /// <summary>Bước 4.5: ô gợi ý của lượt tutorial (null / rỗng = tắt).</summary>
        public void SetHints(IEnumerable<CellPos> cells)
        {
            foreach (CellView c in _hinted) c.SetHint(false);
            _hinted.Clear();
            if (cells == null) return;
            foreach (CellPos p in cells)
            {
                CellView c = GetCell(p);
                if (c == null) continue;
                c.SetHint(true);
                _hinted.Add(c);
            }
        }

        /// <summary>Làm phát sáng các lá sẽ merge (xem trước khi kéo). null / rỗng = tắt hết.</summary>
        public void SetPreview(IEnumerable<CellPos> cells)
        {
            foreach (CellView c in _glowing) c.SetGlow(false);
            _glowing.Clear();
            if (cells == null) return;
            foreach (CellPos p in cells)
            {
                CellView c = GetCell(p);
                if (c == null) continue;
                c.SetGlow(true);
                _glowing.Add(c);
            }
        }

        /// <summary>
        /// Xem trước hiệu ứng chất khi kéo (bước 1.4): <paramref name="danger"/> = vùng ♠ tô đỏ,
        /// <paramref name="spawn"/> = ô ♣ sẽ sinh lá tô xanh. Gọi với (null, null) để tắt hết.
        /// </summary>
        public void SetZones(IEnumerable<CellPos> danger, CellPos? spawn)
        {
            foreach (CellView c in _zoned) c.SetZone(CellZone.None);
            _zoned.Clear();
            if (danger != null)
                foreach (CellPos p in danger) Zone(p, CellZone.Danger);
            if (spawn.HasValue) Zone(spawn.Value, CellZone.Spawn);
        }

        private void Zone(CellPos p, CellZone zone)
        {
            CellView c = GetCell(p);
            if (c == null) return;
            c.SetZone(zone);
            _zoned.Add(c);
        }

        /// <summary>Toạ độ thế giới (world) của tâm ô — dùng cho animation.</summary>
        public Vector3 CellCenterWorld(CellPos p)
        {
            CellView c = GetCell(p);
            return c != null ? c.CenterWorld : transform.position;
        }

        /// <summary>Đặt ô (r, c) theo bố cục hiện tại, tính từ góc trên-trái của bàn.</summary>
        public void PlaceCell(RectTransform rt, int row, int col)
        {
            float size = CellSize;
            Vector2 origin = _layoutCell > 0f ? _layoutOrigin : new Vector2(padding, padding);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(origin.x + col * (size + gap), -(origin.y + row * (size + gap)));
        }

        /// <summary>
        /// Cỡ ô cho vừa khung (trừ lề <see cref="padding"/>, khe <see cref="gap"/> giữ nguyên), lưới căn giữa.
        /// Khung 468 + bàn 5×5 → ô 84 đúng Figma.
        /// </summary>
        private void ComputeLayout(int rows, int cols)
        {
            Rect rect = ((RectTransform)transform).rect;
            float innerW = rect.width - 2f * padding;
            float innerH = rect.height - 2f * padding;
            if (innerW <= 0f || innerH <= 0f || rows <= 0 || cols <= 0)
            {
                _layoutCell = -1f;
                return;
            }
            float size = Mathf.Min((innerW - gap * (cols - 1)) / cols, (innerH - gap * (rows - 1)) / rows);
            _layoutCell = size;
            _layoutOrigin = new Vector2(
                padding + (innerW - (cols * size + (cols - 1) * gap)) / 2f,
                padding + (innerH - (rows * size + (rows - 1) * gap)) / 2f);
        }
    }
}
