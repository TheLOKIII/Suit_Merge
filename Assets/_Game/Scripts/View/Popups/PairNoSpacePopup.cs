using System;
using SuitMerge.Core.Model;
using SuitMerge.View.Animation;
using SuitMerge.View.Cards;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Popups
{
    /// <summary>
    /// Hết chỗ cho khối đôi (Figma popup-pair-no-space 178:390, spec 7 điều kiện 3): HẾT CHỖ! · khối đôi trên tay ·
    /// "Không còn 2 ô trống kề nhau cho khối đôi này." · SWAP / HAMMER (M5 mới bật) · BỎ CUỘC.
    /// Khối đôi trong popup thu 0.8 (Figma: khung 201.6 × 105.6, lá 89.6), đúng hướng đang cầm.
    /// </summary>
    public class PairNoSpacePopup : PopupView
    {
        private const float Scale = 0.8f;
        private const float CardSize = 112f * Scale;
        private const float CardOffset = 60f * Scale;

        [SerializeField] private UiTheme theme;
        [SerializeField] private FxTheme fxTheme;
        [Tooltip("Chỗ vẽ khối đôi (khung + 2 lá dựng lúc chạy).")]
        [SerializeField] private RectTransform pieceRoot;
        [Tooltip("SWAP – ĐỔI LÁ NEXT (M5: chỉ hiện khi Next là lá đơn).")]
        [SerializeField] private Button swapButton;
        [Tooltip("HAMMER – PHÁ 1 Ô (M5: chỉ hiện khi phá 1 ô tạo được cặp trống).")]
        [SerializeField] private Button hammerButton;
        [SerializeField] private Button giveUpButton;

        public event Action GiveUpClicked;
        /// <summary>M5.</summary>
        public event Action SwapClicked;
        /// <summary>M5.</summary>
        public event Action HammerClicked;

        private Image _frame;
        private CardView _a, _b;

        private void Awake()
        {
            giveUpButton.onClick.AddListener(() => GiveUpClicked?.Invoke());
            if (swapButton != null) swapButton.onClick.AddListener(() => SwapClicked?.Invoke());
            if (hammerButton != null) hammerButton.onClick.AddListener(() => HammerClicked?.Invoke());
        }

        /// <summary>Hiện popup với khối đôi đang cầm. M4.5 chưa có booster → <paramref name="canSwap"/>, <paramref name="canHammer"/> = false.</summary>
        public void Show(Piece piece, bool canSwap = false, bool canHammer = false)
        {
            if (swapButton != null) swapButton.gameObject.SetActive(canSwap);
            if (hammerButton != null) hammerButton.gameObject.SetActive(canHammer);
            DrawPiece(piece);
            Open();
        }

        private void DrawPiece(Piece piece)
        {
            if (pieceRoot == null) return;
            EnsureParts();
            bool show = piece != null && piece.IsPair;
            pieceRoot.gameObject.SetActive(show);
            if (!show) return;

            bool vertical = piece.IsVertical;
            Vector2 size = (vertical ? new Vector2(132f, 252f) : new Vector2(252f, 132f)) * Scale;
            var le = pieceRoot.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredWidth = size.x;
                le.preferredHeight = size.y;
            }
            pieceRoot.sizeDelta = size;
            _frame.sprite = fxTheme != null ? (vertical ? fxTheme.handPairV : fxTheme.handPairH) : null;
            _frame.enabled = _frame.sprite != null;

            // Lá ① trái / trên, lá ② phải / dưới
            Vector2 step = vertical ? new Vector2(0f, -CardOffset) : new Vector2(CardOffset, 0f);
            Place(_a, piece.First, -step);
            Place(_b, piece.Second, step);
        }

        private void EnsureParts()
        {
            if (_frame != null) return;
            var go = new GameObject("Frame", typeof(RectTransform)) { layer = pieceRoot.gameObject.layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(pieceRoot, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _frame = go.AddComponent<Image>();
            _frame.raycastTarget = false;
            if (theme == null || theme.cardPrefab == null) return;
            _a = NewCard("Card1");
            _b = NewCard("Card2");
        }

        private CardView NewCard(string name)
        {
            CardView card = Instantiate(theme.cardPrefab, pieceRoot);
            card.name = name;
            var rt = (RectTransform)card.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(CardSize, CardSize);
            card.SetShadowVisible(false);
            card.SetRaycastTarget(false);
            return card;
        }

        private static void Place(CardView view, Card card, Vector2 position)
        {
            if (view == null) return;
            view.SetCard(card);
            ((RectTransform)view.transform).anchoredPosition = position;
        }
    }
}
