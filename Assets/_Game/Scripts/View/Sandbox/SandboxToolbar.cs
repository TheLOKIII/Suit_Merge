using System;
using System.Collections.Generic;
using SuitMerge.Core.Model;
using SuitMerge.View.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Sandbox
{
    /// <summary>Bút của chế độ thao tác trong Sandbox.</summary>
    public enum SandboxBrush
    {
        /// <summary>Kéo-thả lá như chơi thật.</summary>
        Play,
        /// <summary>Chạm ô → đặt lá (rank + chất đang chọn).</summary>
        Card,
        /// <summary>Chạm ô → xoá lá.</summary>
        Erase,
        /// <summary>Chạm ô → khoá / mở khoá ô.</summary>
        Lock
    }

    /// <summary>
    /// Thanh công cụ scene Sandbox — bản sửa 1: thanh dưới chỉ còn preset + 3 nút to (Undo · Nạp lại · Thêm…) + nhật ký;
    /// đổi lá trên tay bằng bảng nổi (mở khi chạm vào lá trên tay); Sửa bàn / Mở chất / Tốc độ nằm trong bảng "Thêm…".
    /// Bước 4.6: nút PRESET / LEVEL ở hàng đầu; chế độ LEVEL có thêm nút "Seed khác", khoá Sửa bàn / Mở chất.
    /// M4.5: chế độ PRESET = Undo · Đổi lá · Thêm… ("Nạp lại" vào bảng "Thêm…"); bảng chọn lá có Lá đơn / Khối đôi, Lá A / Lá B.
    /// Giao diện dựng bằng code lúc chạy (công cụ dev). Chỉ hiển thị + báo sự kiện; SandboxController (App) xử lý.
    /// Toạ độ theo khung thiết kế 500×1080 (góc trên-trái).
    /// </summary>
    public class SandboxToolbar : MonoBehaviour
    {
        private static readonly Color Cream = new Color32(0xF4, 0xEC, 0xD6, 0xFF);
        private static readonly Color Gold = new Color32(0xF6, 0xD4, 0x77, 0xFF);
        private static readonly Color Off = new Color32(0x7D, 0x6A, 0x8C, 0xFF);
        private static readonly Color Dark = new Color32(0x2A, 0x2A, 0x2A, 0xFF);
        private static readonly Color Soft = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.6f);
        private const float PillSourceHeight = 46.8f; // chiều cao gốc của btn-pill (đơn vị UI)

        private static readonly Rank[] Ranks =
        {
            Rank.Two, Rank.Three, Rank.Four, Rank.Five, Rank.Six, Rank.Seven, Rank.Eight,
            Rank.Nine, Rank.Ten, Rank.Jack, Rank.Queen, Rank.King
        };
        /// <summary>Thứ tự nút chọn chất lá trên tay.</summary>
        public static readonly Suit[] PickSuits = { Suit.Heart, Suit.Diamond, Suit.Club, Suit.Spade };
        /// <summary>Thứ tự nút bật/tắt hiệu ứng chất (theo thứ tự mở khoá trong spec 4.7).</summary>
        public static readonly Suit[] ToggleSuits = { Suit.Diamond, Suit.Club, Suit.Spade, Suit.Heart };
        /// <summary>Các mức tốc độ animation.</summary>
        public static readonly float[] Speeds = { 0.25f, 0.5f, 1f, 2f };

        [Header("Ảnh / font (menu Tạo scene Sandbox gán sẵn)")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private CardTheme cardTheme;
        [Tooltip("Nút công cụ: chữ nhật bo góc trắng 9-slice (fx-flash).")]
        [SerializeField] private Sprite buttonSprite;
        [Tooltip("Nút hành động: btn-pill.")]
        [SerializeField] private Sprite pillSprite;
        [Tooltip("Nền thanh công cụ / bảng nổi: board-panel.")]
        [SerializeField] private Sprite panelSprite;
        [Tooltip("Bàn chơi — để phủ lớp nhận chạm khi Sửa ô.")]
        [SerializeField] private RectTransform boardRect;

        [Header("Vị trí (khung 500×1080, góc trên-trái)")]
        [SerializeField] private Rect panelRect = new Rect(8f, 836f, 484f, 238f);

        public event Action PrevPresetClicked;
        public event Action NextPresetClicked;
        public event Action ReloadClicked;
        public event Action UndoClicked;
        public event Action<Rank> RankPicked;
        public event Action<Suit> SuitPicked;
        /// <summary>Chọn bút (Play = bấm "Xong" sửa bàn).</summary>
        public event Action<SandboxBrush> BrushPicked;
        public event Action<Suit> SuitToggled;
        public event Action<float> SpeedPicked;
        /// <summary>Chạm vào bàn khi đang ở chế độ Sửa ô (toạ độ màn hình).</summary>
        public event Action<Vector2> BoardTapped;
        /// <summary>Bấm nút PRESET / LEVEL (đổi chế độ).</summary>
        public event Action ModeToggled;
        /// <summary>Bấm "Seed khác" (chế độ LEVEL).</summary>
        public event Action NewSeedClicked;
        /// <summary>Bảng chọn lá: true = Khối đôi, false = Lá đơn (M4.5).</summary>
        public event Action<bool> PairModePicked;
        /// <summary>Bảng chọn lá: đang sửa lá A (0) hay lá B (1) của khối đôi.</summary>
        public event Action<int> PairSlotPicked;

        private RectTransform _root, _panel, _mainButtons, _editBar, _cardPopup, _morePopup;
        private Button _showButton, _undoButton, _reloadButton, _seedButton, _moreButton, _changeButton;
        private Image _singleBg, _pairBg, _slotABg, _slotBBg;
        private Button _slotA, _slotB;
        private TextMeshProUGUI _presetName, _expect, _log, _undoLabel, _editLabel, _handLabel, _modeLabel;
        private TextMeshProUGUI _brushTitle, _toggleTitle;
        private readonly List<Button> _lockedInLevel = new List<Button>();
        private readonly List<Image> _rankBg = new List<Image>();
        private readonly List<Image> _suitBg = new List<Image>();
        private readonly List<Image> _brushBg = new List<Image>();
        private readonly List<Image> _toggleBg = new List<Image>();
        private readonly List<Image> _toggleIcon = new List<Image>();
        private readonly List<Image> _speedBg = new List<Image>();
        private BoardTapCatcher _catcher;
        private bool _built;

        private void Awake() => Build();

        // ------------------------------------------------------------------ Cập nhật hiển thị

        public void SetPreset(int index, int count, string name, string expect)
        {
            _presetName.text = $"{index + 1}/{count} · {name}";
            _expect.text = string.IsNullOrEmpty(expect) ? "" : "Mong đợi: " + expect;
        }

        /// <summary>Chế độ LEVEL: hàng đầu = tên level + seed, hàng 2 = mục tiêu / lá còn lại / lá sắp ra.</summary>
        public void SetLevelInfo(string title, string info)
        {
            _presetName.text = title;
            _expect.text = info;
        }

        /// <summary>
        /// Đổi chế độ: PRESET = 3 nút to như cũ; LEVEL = 4 nút (thêm "Seed khác") + khoá Sửa bàn / Mở chất trong bảng "Thêm…".
        /// </summary>
        public void SetMode(bool level)
        {
            _modeLabel.text = level ? "LEVEL" : "PRESET";
            _seedButton.gameObject.SetActive(level);
            // M4.5: PRESET = Undo · Đổi lá · Thêm… ("Nạp lại" nằm trong bảng "Thêm…"); LEVEL như bước 4.6
            _reloadButton.gameObject.SetActive(level);
            _changeButton.gameObject.SetActive(!level);
            float w = level ? 113f : 150f, gap = level ? 6f : 9f;
            Button[] row = level
                ? new[] { _undoButton, _reloadButton, _seedButton, _moreButton }
                : new[] { _undoButton, _changeButton, _moreButton };
            for (int i = 0; i < row.Length; i++)
            {
                var rt = (RectTransform)row[i].transform;
                rt.anchoredPosition = new Vector2(8f + i * (w + gap), 0f);
                rt.sizeDelta = new Vector2(w, 60f);
                ((RectTransform)row[i].GetComponentInChildren<TextMeshProUGUI>().transform).sizeDelta = new Vector2(w, 60f);
                row[i].image.pixelsPerUnitMultiplier = PillSourceHeight / 60f;
            }
            foreach (Button b in _lockedInLevel)
            {
                b.interactable = !level; // nền tự mờ theo màu Disabled của Button
                Transform icon = b.transform.Find("Icon");
                if (icon != null) icon.GetComponent<Image>().canvasRenderer.SetAlpha(level ? 0.35f : 1f);
            }
            _brushTitle.text = level ? "Sửa bàn — chỉ dùng ở chế độ PRESET" : "Sửa bàn (chạm vào ô trên bàn)";
            _toggleTitle.text = level ? "Mở hiệu ứng chất — theo file level (khoá)" : "Mở hiệu ứng chất (vàng = mở)";
        }

        public void SetUndoCount(int count)
        {
            _undoLabel.text = $"Undo ({count})";
            _undoButton.interactable = count > 0;
        }

        public void SetRank(Rank rank)
        {
            for (int i = 0; i < Ranks.Length; i++) _rankBg[i].color = Ranks[i] == rank ? Gold : Cream;
        }

        public void SetSuit(Suit suit)
        {
            for (int i = 0; i < PickSuits.Length; i++) _suitBg[i].color = PickSuits[i] == suit ? Gold : Cream;
        }

        /// <summary>Dòng "Đang chọn: …" trong bảng chọn lá.</summary>
        public void SetHandLabel(string text) => _handLabel.text = text;

        /// <summary>Đổi bút: Play → hiện 3 nút; bút sửa → hiện dải "Đang sửa" + bật lớp nhận chạm trên bàn.</summary>
        public void SetBrush(SandboxBrush brush)
        {
            for (int i = 0; i < _brushBg.Count; i++) _brushBg[i].color = i + 1 == (int)brush ? Gold : Cream;
            bool editing = brush != SandboxBrush.Play;
            _mainButtons.gameObject.SetActive(!editing);
            _editBar.gameObject.SetActive(editing);
            if (_catcher != null)
            {
                _catcher.gameObject.SetActive(editing);
                _catcher.transform.SetAsLastSibling();
            }
        }

        /// <summary>Chữ trên dải "Đang sửa" (vd "Đang sửa: Đặt lá 7 Rô — chạm vào ô").</summary>
        public void SetEditText(string text) => _editLabel.text = text;

        public void SetSuitEnabled(Suit suit, bool enabled)
        {
            int i = Array.IndexOf(ToggleSuits, suit);
            if (i < 0) return;
            _toggleBg[i].color = enabled ? Gold : Off;
            _toggleIcon[i].color = new Color(1f, 1f, 1f, enabled ? 1f : 0.35f);
        }

        public void SetSpeed(float speed)
        {
            for (int i = 0; i < Speeds.Length; i++) _speedBg[i].color = Mathf.Approximately(Speeds[i], speed) ? Gold : Cream;
        }

        public void SetLog(string text) => _log.text = text;

        /// <summary>Bảng chọn lá: lá đơn / khối đôi + đang sửa lá A hay lá B (nút A/B mờ khi là lá đơn).</summary>
        public void SetPairMode(bool pair, int slot)
        {
            _singleBg.color = pair ? Cream : Gold;
            _pairBg.color = pair ? Gold : Cream;
            _slotA.interactable = _slotB.interactable = pair;
            _slotABg.color = pair && slot == 0 ? Gold : Cream;
            _slotBBg.color = pair && slot == 1 ? Gold : Cream;
        }

        /// <summary>Mở bảng chọn lá trên tay (nút "Đổi lá", hoặc chạm vào lá đơn ở chế độ PRESET).</summary>
        public void OpenCardPicker()
        {
            _morePopup.gameObject.SetActive(false);
            _cardPopup.gameObject.SetActive(true);
            _cardPopup.SetAsLastSibling();
        }

        public bool IsPopupOpen => _cardPopup.gameObject.activeSelf || _morePopup.gameObject.activeSelf;

        // ------------------------------------------------------------------ Dựng giao diện

        private void Build()
        {
            if (_built) return;
            _built = true;
            _root = (RectTransform)transform;

            BuildBottomPanel();
            _showButton = Pill(_root, 352f, 1018f, 140f, 52f, "Công cụ", 20f, () => SetPanelVisible(true));
            _showButton.gameObject.SetActive(false);
            BuildCardPopup();
            BuildMorePopup();
            BuildTapCatcher();
            SetBrush(SandboxBrush.Play);
        }

        private void BuildBottomPanel()
        {
            _panel = PanelImage(Place(NewUI("Panel", _root), panelRect.x, panelRect.y, panelRect.width, panelRect.height));

            // Hàng 1: [PRESET/LEVEL] ◀ tên preset / level ▶
            Button mode = Tool(_panel, 8f, 8f, 96f, 44f, "PRESET", 17f, null, () => ModeToggled?.Invoke());
            _modeLabel = mode.GetComponentInChildren<TextMeshProUGUI>();
            Pill(_panel, 110f, 8f, 50f, 44f, "◀", 22f, () => PrevPresetClicked?.Invoke());
            _presetName = Label(_panel, 164f, 8f, 252f, 44f, "", 17f, Color.white, TextAlignmentOptions.Center);
            Pill(_panel, 420f, 8f, 56f, 44f, "▶", 22f, () => NextPresetClicked?.Invoke());

            // Hàng 2: kết quả mong đợi (tối đa 2 dòng)
            _expect = Label(_panel, 12f, 54f, 460f, 42f, "", 14f, Soft, TextAlignmentOptions.TopLeft);
            _expect.textWrappingMode = TextWrappingModes.Normal;

            // Hàng 3a: 3 nút to
            _mainButtons = Place(NewUI("MainButtons", _panel), 0f, 98f, 484f, 60f);
            _undoButton = Pill(_mainButtons, 8f, 0f, 150f, 60f, "Undo (0)", 20f, () => UndoClicked?.Invoke());
            _undoLabel = _undoButton.GetComponentInChildren<TextMeshProUGUI>();
            _reloadButton = Pill(_mainButtons, 167f, 0f, 150f, 60f, "Nạp lại", 20f, () => ReloadClicked?.Invoke());
            _changeButton = Pill(_mainButtons, 167f, 0f, 150f, 60f, "Đổi lá", 20f, OpenCardPicker);
            _seedButton = Pill(_mainButtons, 245f, 0f, 113f, 60f, "Seed khác", 20f, () => NewSeedClicked?.Invoke());
            _moreButton = Pill(_mainButtons, 326f, 0f, 150f, 60f, "Thêm…", 20f, OpenMore);
            _seedButton.gameObject.SetActive(false);
            _reloadButton.gameObject.SetActive(false); // PRESET: "Nạp lại" nằm trong bảng "Thêm…"

            // Hàng 3b: dải "Đang sửa" (thay 3 nút khi dùng bút sửa)
            _editBar = Place(NewUI("EditBar", _panel), 0f, 98f, 484f, 60f);
            Image bar = Place(NewUI("Bg", _editBar), 8f, 0f, 310f, 60f).gameObject.AddComponent<Image>();
            bar.sprite = buttonSprite;
            bar.type = Image.Type.Sliced;
            bar.pixelsPerUnitMultiplier = 2f;
            bar.color = Gold;
            _editLabel = Label(_editBar, 18f, 0f, 292f, 60f, "", 16f, Dark, TextAlignmentOptions.Left);
            _editLabel.textWrappingMode = TextWrappingModes.Normal;
            Pill(_editBar, 326f, 0f, 150f, 60f, "Xong", 20f, () => BrushPicked?.Invoke(SandboxBrush.Play));

            // Hàng 4: nhật ký lượt
            _log = Label(_panel, 12f, 164f, 460f, 68f, "", 15f, Gold, TextAlignmentOptions.TopLeft);
            _log.textWrappingMode = TextWrappingModes.Normal;
        }

        /// <summary>Bảng chọn lá: lưới 12 rank + 4 chất, áp dụng ngay khi bấm.</summary>
        private void BuildCardPopup()
        {
            _cardPopup = Popup("CardPopup", 520f, out RectTransform box, 280f);
            Label(box, 0f, 14f, 440f, 32f, "Chọn lá trên tay", 22f, Color.white, TextAlignmentOptions.Center);
            _handLabel = Label(box, 0f, 46f, 440f, 28f, "", 16f, Gold, TextAlignmentOptions.Center);

            // M4.5: Lá đơn / Khối đôi · đang sửa lá A / lá B
            _singleBg = Tool(box, 20f, 80f, 94f, 46f, "Lá đơn", 17f, null, () => PairModePicked?.Invoke(false)).image;
            _pairBg = Tool(box, 122f, 80f, 94f, 46f, "Khối đôi", 17f, null, () => PairModePicked?.Invoke(true)).image;
            _slotA = Tool(box, 224f, 80f, 94f, 46f, "Lá A", 17f, null, () => PairSlotPicked?.Invoke(0));
            _slotB = Tool(box, 326f, 80f, 94f, 46f, "Lá B", 17f, null, () => PairSlotPicked?.Invoke(1));
            _slotABg = _slotA.image;
            _slotBBg = _slotB.image;

            for (int i = 0; i < Ranks.Length; i++)
            {
                Rank rank = Ranks[i];
                float x = 20f + (i % 4) * 102f, y = 134f + (i / 4) * 70f;
                _rankBg.Add(Tool(box, x, y, 94f, 62f, rank.ToLabel(), 26f, null, () => RankPicked?.Invoke(rank)).image);
            }
            for (int i = 0; i < PickSuits.Length; i++)
            {
                Suit suit = PickSuits[i];
                _suitBg.Add(Tool(box, 20f + i * 102f, 350f, 94f, 72f, null, 0f, SuitSprite(suit),
                    () => SuitPicked?.Invoke(suit)).image);
            }
            Pill(box, 140f, 442f, 160f, 56f, "Xong", 20f, ClosePopups);
        }

        /// <summary>Bảng "Thêm…": Sửa bàn · Mở chất · Tốc độ · Ẩn thanh công cụ.</summary>
        private void BuildMorePopup()
        {
            _morePopup = Popup("MorePopup", 440f, out RectTransform box, 290f);
            Label(box, 0f, 12f, 440f, 32f, "Thêm", 22f, Color.white, TextAlignmentOptions.Center);

            _brushTitle = Label(box, 20f, 50f, 400f, 26f, "Sửa bàn (chạm vào ô trên bàn)", 16f, Soft, TextAlignmentOptions.Left);
            string[] brushes = { "Đặt lá", "Xoá lá", "Khoá ô" };
            for (int i = 0; i < brushes.Length; i++)
            {
                var brush = (SandboxBrush)(i + 1);
                Button b = Tool(box, 20f + i * 136f, 78f, 128f, 58f, brushes[i], 19f, null, () =>
                {
                    ClosePopups();
                    BrushPicked?.Invoke(brush);
                });
                _brushBg.Add(b.image);
                _lockedInLevel.Add(b);
            }

            _toggleTitle = Label(box, 20f, 146f, 400f, 26f, "Mở hiệu ứng chất (vàng = mở)", 16f, Soft, TextAlignmentOptions.Left);
            for (int i = 0; i < ToggleSuits.Length; i++)
            {
                Suit suit = ToggleSuits[i];
                Button b = Tool(box, 20f + i * 102f, 174f, 94f, 62f, null, 0f, SuitSprite(suit), () => SuitToggled?.Invoke(suit));
                _toggleBg.Add(b.image);
                _toggleIcon.Add(b.transform.Find("Icon").GetComponent<Image>());
                _lockedInLevel.Add(b);
            }

            Label(box, 20f, 246f, 400f, 26f, "Tốc độ animation", 16f, Soft, TextAlignmentOptions.Left);
            for (int i = 0; i < Speeds.Length; i++)
            {
                float speed = Speeds[i];
                _speedBg.Add(Tool(box, 20f + i * 102f, 274f, 94f, 56f, $"{speed:0.##}×", 19f, null,
                    () => SpeedPicked?.Invoke(speed)).image);
            }

            // M4.5: "Nạp lại" chuyển từ thanh dưới (chế độ PRESET) vào đây
            Pill(box, 20f, 360f, 120f, 56f, "Nạp lại", 18f, () =>
            {
                ClosePopups();
                ReloadClicked?.Invoke();
            });
            Pill(box, 148f, 360f, 164f, 56f, "Ẩn thanh công cụ", 16f, () =>
            {
                ClosePopups();
                SetPanelVisible(false);
            });
            Pill(box, 320f, 360f, 100f, 56f, "Đóng", 20f, ClosePopups);
        }

        private void BuildTapCatcher()
        {
            if (boardRect == null) return;
            RectTransform c = NewUI("SandboxTapCatcher", boardRect);
            c.anchorMin = Vector2.zero;
            c.anchorMax = Vector2.one;
            c.offsetMin = c.offsetMax = Vector2.zero;
            Image img = c.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // trong suốt nhưng vẫn nhận chạm
            _catcher = c.gameObject.AddComponent<BoardTapCatcher>();
            _catcher.Tapped += p => BoardTapped?.Invoke(p);
        }

        private void OpenMore()
        {
            _cardPopup.gameObject.SetActive(false);
            _morePopup.gameObject.SetActive(true);
            _morePopup.SetAsLastSibling();
        }

        private void ClosePopups()
        {
            _cardPopup.gameObject.SetActive(false);
            _morePopup.gameObject.SetActive(false);
        }

        private void SetPanelVisible(bool visible)
        {
            _panel.gameObject.SetActive(visible);
            _showButton.gameObject.SetActive(!visible);
        }

        // ------------------------------------------------------------------ Khối dựng

        /// <summary>Bảng nổi: nền tối phủ khung (chạm ra ngoài = đóng) + khung 440 × <paramref name="height"/>.</summary>
        private RectTransform Popup(string name, float height, out RectTransform box, float top)
        {
            RectTransform overlay = NewUI(name, _root);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = overlay.offsetMax = Vector2.zero;
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = Dim;
            var close = overlay.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(ClosePopups);

            box = PanelImage(Place(NewUI("Box", overlay), 30f, top, 440f, height));
            // Chạm vào khung (chỗ không có nút) dừng tại đây, không "nổi bọt" lên nền tối → không đóng bảng
            box.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            overlay.gameObject.SetActive(false);
            return overlay;
        }

        private RectTransform PanelImage(RectTransform rt)
        {
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = panelSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, 0.97f);
            return rt;
        }

        private Sprite SuitSprite(Suit suit) => cardTheme != null ? cardTheme.GetSuitSprite(suit) : null;

        private RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = gameObject.layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Đặt theo toạ độ góc trên-trái trong cha.</summary>
        private static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        private TextMeshProUGUI Label(RectTransform parent, float x, float y, float w, float h, string text, float size,
            Color color, TextAlignmentOptions align)
        {
            RectTransform rt = Place(NewUI("Label", parent), x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Nút hành động: viên thuốc hồng, chữ trắng. Đầu tròn giữ đúng tỉ lệ theo chiều cao nút.</summary>
        private Button Pill(RectTransform parent, float x, float y, float w, float h, string text, float size, Action onClick)
        {
            RectTransform rt = Place(NewUI("Btn " + text, parent), x, y, w, h);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = pillSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = PillSourceHeight / h;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(() => onClick());
            Label(rt, 0f, 0f, w, h, text, size, Color.white, TextAlignmentOptions.Center).overflowMode = TextOverflowModes.Overflow;
            return b;
        }

        /// <summary>Nút công cụ: ô kem bo góc (vàng = đang chọn), có chữ hoặc icon chất.</summary>
        private Button Tool(RectTransform parent, float x, float y, float w, float h, string text, float size, Sprite icon,
            Action onClick)
        {
            RectTransform rt = Place(NewUI("Tool " + (text ?? icon?.name), parent), x, y, w, h);
            Image img = rt.gameObject.AddComponent<Image>();
            img.sprite = buttonSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 2f; // viền 48px gốc → bo 12 đơn vị
            img.color = Cream;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(0.95f, 0.95f, 0.95f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            b.colors = cb;
            b.onClick.AddListener(() => onClick());
            if (text != null) Label(rt, 0f, 0f, w, h, text, size, Dark, TextAlignmentOptions.Center).overflowMode = TextOverflowModes.Overflow;
            if (icon != null)
            {
                float s = h - 14f;
                RectTransform ir = Place(NewUI("Icon", rt), (w - s) / 2f, 7f, s, s);
                Image ii = ir.gameObject.AddComponent<Image>();
                ii.sprite = icon;
                ii.preserveAspect = true;
                ii.raycastTarget = false;
            }
            return b;
        }
    }
}
