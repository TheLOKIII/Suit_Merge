using System;
using System.Collections;
using System.Collections.Generic;
using SuitMerge.Core.Model;
using SuitMerge.View.Animation;
using SuitMerge.View.Cards;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SuitMerge.View.Hand
{
    /// <summary>
    /// Lá hiện tại trong khung viền nét đứt (Figma hand-current 106:56) + kéo-thả.
    /// Khi kéo: lá thu nhỏ bằng cỡ ô bàn, TÂM lá đi theo con trỏ (giữ nguyên chỗ cầm).
    /// HandView không biết luật chơi: chỉ báo vị trí tâm lá ra ngoài qua sự kiện.
    /// M4.5: khối đôi (Figma hand-pair 177:354) — khung giãn ngang / dọc, 2 mũi tên xoay, chạm = xoay (App gọi
    /// <see cref="ShowRotation"/>), kéo cả khối; vị trí báo ra ngoài = TÂM LÁ ① (ô dưới lá ① là ô đặt).
    /// Phần khối đôi dựng bằng code lúc chạy (ảnh lấy từ FxTheme) → scene cũ không cần dựng lại.
    /// Animation phần 2: nhấc lá (co về cỡ ô × <see cref="liftScale"/> trong 0,1s, bóng dịch xa) · nghiêng theo hướng kéo ·
    /// thả xong KHÔNG nhảy về ngay: App gọi <see cref="ClaimDrop"/> nếu lượt hợp lệ, còn lại lá bay cong về khung rồi lắc "không" ·
    /// lưu vị trí lá lúc thả (<see cref="TakeDropPose"/>) để EventPlayer cho lá trượt vào ô · chờ lâu thì lá "thở".
    /// </summary>
    public class HandView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerDownHandler
    {
        /// <summary>Figma màn L13 (178:539): khối đôi trên tay thu 0.88 (hand-pair 252 → 221.76).</summary>
        private const float PairScale = 0.88f;
        /// <summary>Tâm mỗi lá cách tâm khối 60 (lá 112, khe 8 — hand-pair 177:304).</summary>
        private const float PairOffset = 60f;
        /// <summary>Khối dọc dời xuống 10 để khung không đè mép dưới bàn chơi.</summary>
        private const float VerticalShift = 10f;
        private const float RotateDuration = 0.15f;
        /// <summary>Figma: chữ "Chạm để xoay · Kéo để đặt" ở y 830 (lá đơn: y 818).</summary>
        private const float PairCaptionShiftH = 12f;
        private const float CaptionGap = 8f;

        [SerializeField] private CardView cardView;
        [Tooltip("Tỉ lệ cỡ lá khi kéo so với lúc nằm trên tay (ô bàn 84 / lá tay 112).")]
        [SerializeField] private float dragScale = 84f / 112f;
        [Tooltip("Chữ dưới khung tay. Để trống = tự tìm \"Hint\" cạnh khung tay.")]
        [SerializeField] private TextMeshProUGUI caption;

        [Header("Cầm – kéo – thả (Animation phần 2)")]
        [Tooltip("Khi kéo, lá to hơn ô bàn bao nhiêu lần (nhấc lên).")]
        [SerializeField] private float liftScale = 1.08f;
        [SerializeField] private float liftDuration = 0.1f;
        [Tooltip("Góc nghiêng tối đa khi kéo (độ).")]
        [SerializeField, Range(0f, 20f)] private float maxTilt = 10f;
        [Tooltip("Độ nghiêng (độ) cho mỗi 100 đơn vị UI / giây kéo ngang.")]
        [SerializeField] private float tiltPerSpeed = 1.2f;
        [Tooltip("Lá đuổi theo góc nghiêng nhanh cỡ nào (càng lớn càng gắt).")]
        [SerializeField] private float tiltSmoothing = 14f;
        [Tooltip("Thả không thành: thời gian bay cong về khung tay.")]
        [SerializeField] private float flyBackDuration = 0.22f;
        [Tooltip("Lắc 'không' khi về tới khung: biên độ ngang (đơn vị UI).")]
        [SerializeField] private float shakeAmplitude = 10f;
        [Tooltip("Lắc 'không': thời gian cho 3 nhịp.")]
        [SerializeField] private float shakeDuration = 0.3f;

        [Header("Chờ lâu (Animation phần 2)")]
        [Tooltip("Không chạm gì bao nhiêu giây thì lá bắt đầu 'thở' (0 = tắt).")]
        [SerializeField] private float idleDelay = 6f;
        [SerializeField] private float breatheScale = 1.05f;
        [SerializeField] private float breathePeriod = 1.6f;
        [Tooltip("Khối đôi: 2 mũi tên xoay nhích bao nhiêu độ.")]
        [SerializeField] private float arrowNudge = 12f;

        /// <summary>Đang kéo: toạ độ màn hình của tâm lá (khối đôi: tâm lá ①).</summary>
        public event Action<Vector2> CardDragged;
        /// <summary>Vừa thả: toạ độ màn hình của tâm lá (khối đôi: tâm lá ①). Sau sự kiện này lá luôn về lại khung.</summary>
        public event Action<Vector2> CardDropped;
        /// <summary>
        /// Chạm nhẹ vào lá (không kéo). Khối đôi: App xoay khối. Lá đơn trong Sandbox: mở bảng chọn lá.
        /// </summary>
        public event Action CardTapped;

        private Canvas _canvas;
        private RectTransform _piece;          // gốc chứa lá A (+ lá B) — thứ được kéo đi
        private CardView _second;              // lá B của khối đôi
        private Image _pairFrame, _arrowA, _arrowB;
        private GameObject _singleFrame;
        private FxTheme _theme;
        private Piece _current;
        private Vector2 _baseHome;             // tâm khung lá đơn (đơn vị anchoredPosition của _piece)
        private Vector3 _baseScale;
        private Vector2 _homePosition;
        private Vector3 _homeScale;
        private Vector2 _captionHome;
        private bool _captionSaved;
        private bool _built;
        private bool _dragging;
        private Coroutine _rotating;

        // Kéo (Animation phần 2) — toạ độ màn hình
        private Vector2 _pointer, _lastPointer;
        private Vector2 _grabBase;      // tâm khối − con trỏ, quy về cỡ khung (hệ số 1)
        private Vector2 _reportOffset;  // tâm lá ① − con trỏ ở cỡ kéo cuối cùng (vị trí báo ra ngoài)
        private float _startFactor, _dragFactor, _lift, _tilt, _liftK;
        // Thả
        private bool _claimed;
        private Coroutine _flyRoutine;
        private readonly List<(Card card, Vector3 world, float size, float rot)> _dropPoses =
            new List<(Card card, Vector3 world, float size, float rot)>();
        // Chờ lâu
        private float _idleTime;
        private bool _breathing;
        private Image _idleGlow;
        private float _arrowBaseA, _arrowBaseB;
        private static readonly Color IdleGlowColor = new Color32(0xF6, 0xD4, 0x77, 0xFF);
        private const float IdleGlowAlpha = 0.45f;
        private const float IdleGlowSize = 1.7f;
        private const float ShakeTilt = 3f;

        public CardView CardView => cardView;
        /// <summary>false = không cho kéo (vd ván đã kết thúc).</summary>
        public bool Interactable { get; set; } = true;
        public bool IsDragging => _dragging;
        /// <summary>Lá đang bay về khung sau lần thả không thành.</summary>
        public bool IsReturning => _flyRoutine != null;

        /// <summary>
        /// Bước 4.5: cạnh ô bàn hiện tại — khi kéo, lá co đúng bằng cỡ ô (bàn 4×4 có ô to hơn 5×5).
        /// ≤ 0 = dùng <see cref="dragScale"/> cố định như trước.
        /// </summary>
        public float DragTargetSize { get; set; }

        private RectTransform CardRect => (RectTransform)cardView.transform;
        private bool IsPair => _current != null && _current.IsPair;
        private float PieceScaleFactor => IsPair ? PairScale : 1f;

        /// <summary>Tỉ lệ phóng khi kéo so với lúc nằm trên tay (lá co đúng cỡ ô).</summary>
        private float CurrentDragScale
        {
            get
            {
                float card = CardRect.rect.width;
                float absolute = DragTargetSize > 0f && card > 0f ? DragTargetSize / card : dragScale;
                return absolute / PieceScaleFactor;
            }
        }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            Build();
            cardView.SetRaycastTarget(true);
        }

        /// <summary>Ảnh khung / mũi tên khối đôi (EventPlayer gán lúc khởi động, như overlay của bàn).</summary>
        public void SetFxTheme(FxTheme theme)
        {
            _theme = theme;
            if (_built && IsPair) Layout(animateFrom: null);
        }

        /// <summary>Piece hiện tại (lá đơn / khối đôi); null = không còn lá.</summary>
        public void SetCurrent(Piece piece)
        {
            Build();
            StopRotation();
            _claimed = true;
            _dropPoses.Clear();
            _current = piece;
            cardView.SetCard(piece == null ? null : piece.IsPair ? piece.Cards[0] : piece.First);
            if (piece != null) cardView.SetShadowVisible(true);
            _second.SetCard(piece != null && piece.IsPair ? piece.Cards[1] : null);
            if (piece != null && piece.IsPair) _second.SetShadowVisible(true);
            Layout(animateFrom: null);
            ReturnHome();
        }

        /// <summary>Lối tắt: lá hiện tại (thành Piece lá đơn); null = không còn lá.</summary>
        public void SetCurrent(Card card) => SetCurrent(card != null ? Piece.Single(card) : null);

        /// <summary>Khối đôi vừa xoay 90° (Core đã đổi hướng): 2 lá quay theo chiều kim đồng hồ ~0.15s.</summary>
        public void ShowRotation(Piece rotated)
        {
            if (rotated == null || !rotated.IsPair || _current == null || !_current.IsPair)
            {
                SetCurrent(rotated);
                return;
            }
            Piece before = _current;
            _current = rotated;
            StopRotation();
            Layout(animateFrom: before);
            AudioService.Play(SfxId.Rotate); // Animation phần 5
        }

        /// <summary>Icon chất trên lá ① sáng lên (thả xuống sẽ kích hoạt hiệu ứng chất — bước 1.4).</summary>
        public void SetSuitHighlight(bool on)
        {
            FirstView.SetSuitHighlight(on);
            if (IsPair) SecondView.SetSuitHighlight(false);
        }

        /// <summary>Ẩn lá trên tay (lá vừa được thả hợp lệ — animation sẽ hiện nó ở ô bàn).</summary>
        public void HideCard()
        {
            _claimed = true;
            ReturnHome();
            cardView.SetCard(null);
            if (_second != null) _second.SetCard(null);
            SetArrowsVisible(false);
        }

        /// <summary>Toạ độ thế giới của tâm khung tay (đích lá UP NEXT bay xuống, gốc bàn tay gợi ý).</summary>
        public Vector3 HomeCenterWorld
        {
            get
            {
                // Không kéo lá về (Animation phần 2: lá có thể đang bay về khung) — tính thẳng từ vị trí khung
                Build();
                var parent = (RectTransform)_piece.parent;
                return parent.TransformPoint(parent.rect.center + _homePosition);
            }
        }

        /// <summary>
        /// Tâm lá thứ <paramref name="index"/> (0 = A, 1 = B theo thứ tự gốc) khi <paramref name="piece"/> nằm trong khung —
        /// đích lá UP NEXT bay xuống. Lá đơn: tâm khung.
        /// </summary>
        public Vector3 HomeCardCenterWorld(Piece piece, int index)
        {
            Build();
            bool pair = piece != null && piece.IsPair;
            Vector2 offset = pair ? CardOffset(piece.Orientation, index) * (PairScale * _baseScale.x) : Vector2.zero;
            // _piece neo + pivot ở giữa cha → localPosition = tâm rect của cha + anchoredPosition
            var parent = (RectTransform)_piece.parent;
            return parent.TransformPoint(parent.rect.center + HomePositionFor(piece) + offset);
        }

        /// <summary>Cạnh lá trên tay (đơn vị UI, chưa tính thu nhỏ của khối đôi).</summary>
        public float CardSize => CardRect.rect.width;

        /// <summary>Cạnh lá trên tay khi đang hiện <paramref name="piece"/> (khối đôi thu 0.88).</summary>
        public float CardSizeFor(Piece piece) => CardSize * (piece != null && piece.IsPair ? PairScale : 1f);

        /// <summary>
        /// Animation phần 4 (màn vào level): lá trên tay hiện dần — <paramref name="k"/> 0 = ẩn (cỡ 0), 1 = đúng cỡ trong khung
        /// (có thể vượt 1 một chút để nảy). Mũi tên xoay khối đôi chỉ hiện khi k ≥ 1.
        /// </summary>
        public void SetAppear(float k)
        {
            Build();
            if (_dragging) return;
            _piece.localScale = _homeScale * Mathf.Max(0f, k);
            SetArrowsVisible(k >= 1f && IsPair && cardView.Card != null);
        }

        /// <summary>Đưa lá về đúng chỗ, đúng cỡ trong khung (dừng mọi bay về / nghiêng / nhấc).</summary>
        public void ReturnHome()
        {
            Build();
            StopFly();
            _dragging = false;
            _piece.anchoredPosition = _homePosition;
            _piece.localScale = _homeScale;
            _piece.localRotation = Quaternion.identity;
            SetLift(0f);
            SetArrowsVisible(IsPair && cardView.Card != null);
        }

        /// <summary>
        /// App gọi trong lúc xử lý <see cref="CardDropped"/> khi lượt được nhận: lá đứng yên chỗ thả để EventPlayer cho trượt vào ô.
        /// Không gọi → lá tự bay về khung và lắc "không".
        /// </summary>
        public void ClaimDrop() => _claimed = true;

        /// <summary>
        /// Vị trí lá <paramref name="card"/> lúc vừa thả (tâm thế giới, cạnh lá theo thế giới, góc nghiêng) — lấy 1 lần rồi bỏ.
        /// false = không có (lá không đến từ tay).
        /// </summary>
        public bool TakeDropPose(Card card, out Vector3 worldCenter, out float worldSize, out float rotation)
        {
            int index = _dropPoses.FindIndex(p => ReferenceEquals(p.card, card));
            if (index < 0 && card != null)
                index = _dropPoses.FindIndex(p => p.card != null && p.card.Rank == card.Rank && p.card.Suit == card.Suit);
            if (index < 0)
            {
                worldCenter = Vector3.zero;
                worldSize = rotation = 0f;
                return false;
            }
            (_, worldCenter, worldSize, rotation) = _dropPoses[index];
            _dropPoses.RemoveAt(index);
            return true;
        }

        public void OnPointerDown(PointerEventData eventData) => StopIdle();

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!Interactable || cardView.Card == null || !cardView.gameObject.activeInHierarchy) return;
            StopRotation();
            StopIdle();
            StopFly(); // bắt lại lá đang bay về: kéo tiếp từ chỗ nó đang ở
            _dragging = true;
            _claimed = false;
            _dropPoses.Clear();
            SetArrowsVisible(false); // Figma: mũi tên xoay có thể ẩn khi đang kéo
            _piece.localRotation = Quaternion.identity;
            AudioService.Play(SfxId.Pick); // Animation phần 5

            // Mọi khoảng cách (chỗ cầm ↔ tâm khối ↔ tâm lá ①) co theo cỡ khối → ngón tay luôn nằm đúng chỗ trên lá
            _startFactor = _homeScale.x > 0f ? _piece.localScale.x / _homeScale.x : 1f;
            _dragFactor = CurrentDragScale * liftScale;
            Vector2 pieceCenter = PieceCenterScreen();
            _grabBase = (pieceCenter - eventData.position) / _startFactor;
            Vector2 firstBase = (FirstCenterScreen() - pieceCenter) / _startFactor;
            _reportOffset = (_grabBase + firstBase) * _dragFactor;
            _pointer = _lastPointer = eventData.position;
            _lift = 0f;
            _tilt = 0f;
            PlaceDragged();
            CardDragged?.Invoke(_pointer + _reportOffset);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _pointer = eventData.position;
            PlaceDragged();
            CardDragged?.Invoke(_pointer + _reportOffset);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // EventSystem không gọi click nếu con trỏ đã kéo → kéo-thả không mở nhầm
            StopIdle();
            if (_dragging || _flyRoutine != null || cardView.Card == null) return; // đang bay về khung → bỏ qua (không xoay giữa đường)
            CardTapped?.Invoke();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            _pointer = eventData.position;
            Vector2 first = _pointer + _reportOffset;
            CaptureDropPoses();
            _claimed = false;
            CardDropped?.Invoke(first);
            if (_claimed) return; // lượt được nhận → lá đứng yên, EventPlayer cho trượt vào ô
            // Không ai nhận (ô sai / ngoài bàn / đang khoá) → bay cong về khung + lắc "không"
            if (cardView.Card != null && isActiveAndEnabled) _flyRoutine = StartCoroutine(FlyHomeRoutine());
            else ReturnHome();
        }

        // ------------------------------------------------------------------ Kéo / thả / chờ lâu (Animation phần 2)

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_dragging) UpdateDrag(dt);
            UpdateIdle(dt);
        }

        /// <summary>Nhấc dần lên (0,1s) + nghiêng theo tốc độ kéo ngang (làm mượt, tự thẳng lại khi dừng tay).</summary>
        private void UpdateDrag(float dt)
        {
            if (dt <= 0f) return;
            _lift = liftDuration > 0f ? Mathf.Min(1f, _lift + dt / liftDuration) : 1f;
            float scaleFactor = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            float vx = (_pointer.x - _lastPointer.x) / scaleFactor / dt;
            _lastPointer = _pointer;
            // Kéo sang trái → đầu lá ngả sang trái (z dương = ngược chiều kim đồng hồ)
            float target = Mathf.Clamp(-vx * tiltPerSpeed / 100f, -maxTilt, maxTilt);
            _tilt = Mathf.Lerp(_tilt, target, 1f - Mathf.Exp(-tiltSmoothing * dt));
            PlaceDragged();
        }

        private void PlaceDragged()
        {
            float k = Ease.OutCubic(_lift);
            float f = Mathf.Lerp(_startFactor, _dragFactor, k);
            _piece.localScale = _homeScale * f;
            _piece.localRotation = Quaternion.Euler(0f, 0f, _tilt);
            MovePieceCenterTo(_pointer + _grabBase * f);
            SetLift(k);
        }

        private void CaptureDropPoses()
        {
            _dropPoses.Clear();
            float rot = _piece.eulerAngles.z;
            foreach (CardView v in IsPair ? new[] { FirstView, SecondView } : new[] { cardView })
            {
                if (v == null || v.Card == null) continue;
                var rt = (RectTransform)v.transform;
                _dropPoses.Add((v.Card, rt.TransformPoint(rt.rect.center), rt.rect.width * rt.lossyScale.x, rot));
            }
        }

        /// <summary>Bay cong về khung (nhanh dần rồi chậm lại), thu về cỡ khung, thẳng lại, hạ bóng — rồi lắc "không" 3 nhịp.</summary>
        private IEnumerator FlyHomeRoutine()
        {
            Vector2 from = _piece.anchoredPosition;
            Vector3 fromScale = _piece.localScale;
            float fromRot = Mathf.DeltaAngle(0f, _piece.localEulerAngles.z);
            float fromLift = _liftK;
            Vector2 to = _homePosition;
            Vector2 dir = to - from;
            Vector2 control = (from + to) * 0.5f + new Vector2(-dir.y, dir.x) * 0.15f; // lệch nhẹ sang 1 bên → đường cong

            yield return Tween.Run(flyBackDuration, k =>
            {
                _piece.anchoredPosition = Tween.Bezier(from, control, to, k);
                _piece.localScale = Vector3.LerpUnclamped(fromScale, _homeScale, k);
                _piece.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(fromRot, 0f, k));
                SetLift(fromLift * (1f - k));
            }, Ease.InOutSine);

            _piece.localScale = _homeScale;
            AudioService.Play(SfxId.Invalid); // Animation phần 5: lắc "không"
            yield return Tween.Run(shakeDuration, k =>
            {
                float swing = Mathf.Sin(k * Mathf.PI * 2f * 3f) * (1f - k);
                _piece.anchoredPosition = _homePosition + new Vector2(swing * shakeAmplitude, 0f);
                _piece.localRotation = Quaternion.Euler(0f, 0f, -swing * ShakeTilt);
            });
            _flyRoutine = null;
            ReturnHome();
        }

        private void StopFly()
        {
            if (_flyRoutine == null) return;
            StopCoroutine(_flyRoutine);
            _flyRoutine = null;
        }

        private void SetLift(float k)
        {
            _liftK = k;
            cardView.SetLift(k);
            if (_second != null) _second.SetLift(IsPair ? k : 0f);
        }

        /// <summary>
        /// Không chạm gì <see cref="idleDelay"/> giây (đang được chơi): lá đơn "thở" 1 ↔ <see cref="breatheScale"/> kèm quầng vàng,
        /// khối đôi thì 2 mũi tên xoay nhích. Chạm / kéo / hết lượt chơi được → dừng ngay.
        /// </summary>
        private void UpdateIdle(float dt)
        {
            bool canIdle = idleDelay > 0f && Interactable && !_dragging && _flyRoutine == null && _rotating == null
                           && cardView.Card != null;
            if (!canIdle)
            {
                if (_idleTime > 0f || _breathing) StopIdle();
                return;
            }
            _idleTime += dt;
            if (_idleTime < idleDelay) return;

            _breathing = true;
            float t = _idleTime - idleDelay;
            float p = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / Mathf.Max(0.1f, breathePeriod));
            if (IsPair)
            {
                // Quay theo chiều kim đồng hồ (cùng chiều xoay của khối) rồi về
                if (_arrowA != null) _arrowA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _arrowBaseA - arrowNudge * p);
                if (_arrowB != null) _arrowB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _arrowBaseB - arrowNudge * p);
                return;
            }
            cardView.transform.localScale = Vector3.one * Mathf.Lerp(1f, breatheScale, p);
            Image glow = IdleGlow();
            if (glow == null) return;
            glow.gameObject.SetActive(true);
            Color c = IdleGlowColor;
            c.a = IdleGlowAlpha * p;
            glow.color = c;
        }

        private void StopIdle()
        {
            _idleTime = 0f;
            if (!_breathing) return;
            _breathing = false;
            cardView.transform.localScale = Vector3.one;
            if (_idleGlow != null) _idleGlow.gameObject.SetActive(false);
            if (_arrowA != null) _arrowA.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _arrowBaseA);
            if (_arrowB != null) _arrowB.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _arrowBaseB);
        }

        /// <summary>Quầng vàng sau lá đơn khi "thở" (ảnh glow của FxTheme; chưa có ảnh = không có quầng).</summary>
        private Image IdleGlow()
        {
            if (_theme == null || _theme.glow == null) return null;
            if (_idleGlow == null)
            {
                _idleGlow = NewImage("IdleGlow", transform, raycast: false);
                _idleGlow.sprite = _theme.glow;
                _idleGlow.rectTransform.SetSiblingIndex(_piece.GetSiblingIndex()); // ngay dưới lá
            }
            Place(_idleGlow.rectTransform, _homePosition, CardRect.rect.size * IdleGlowSize);
            return _idleGlow;
        }

        // ------------------------------------------------------------------ Dựng + bố cục

        /// <summary>
        /// Lần đầu: tạo gốc "Piece" ngay tại chỗ lá trên tay rồi chuyển lá vào đó, thêm lá B + khung khối đôi + 2 mũi tên.
        /// Lá đơn trông và hoạt động y như trước.
        /// </summary>
        private void Build()
        {
            if (_built) return;
            _built = true;
            var hand = (RectTransform)transform;

            Vector3 cardCenter = CardRect.TransformPoint(CardRect.rect.center);
            Vector2 cardSize = CardRect.rect.size;
            Vector3 cardScale = CardRect.localScale;

            _piece = NewChild("Piece", hand);
            _piece.anchorMin = _piece.anchorMax = _piece.pivot = new Vector2(0.5f, 0.5f);
            _piece.sizeDelta = cardSize;
            _piece.SetSiblingIndex(CardRect.GetSiblingIndex());
            _piece.position = cardCenter;
            _piece.localScale = cardScale;

            CardRect.SetParent(_piece, false);
            CenterIn(CardRect, cardSize);

            _second = Instantiate(cardView, _piece);
            _second.name = "SecondCard";
            CenterIn((RectTransform)_second.transform, cardSize);
            _second.SetRaycastTarget(true);
            _second.SetCard(null);

            Transform single = hand.Find("Frame");
            _singleFrame = single != null ? single.gameObject : null;
            _pairFrame = NewImage("PairFrame", hand, raycast: true);
            _pairFrame.rectTransform.SetSiblingIndex(single != null ? single.GetSiblingIndex() + 1 : 0);
            _arrowA = NewImage("RotateArrowA", hand, raycast: false);
            _arrowB = NewImage("RotateArrowB", hand, raycast: false);
            _pairFrame.gameObject.SetActive(false);
            SetArrowsVisible(false);

            _baseHome = _piece.anchoredPosition;
            _baseScale = _piece.localScale;
            _homePosition = _baseHome;
            _homeScale = _baseScale;

            if (caption == null && hand.parent != null)
            {
                Transform hint = hand.parent.Find("Hint");
                if (hint != null) caption = hint.GetComponent<TextMeshProUGUI>();
            }
        }

        /// <summary>Bố cục theo Piece hiện tại; <paramref name="animateFrom"/> ≠ null = quay từ hướng cũ sang hướng mới.</summary>
        private void Layout(Piece animateFrom)
        {
            bool pair = IsPair;
            bool vertical = pair && _current.IsVertical;
            _homePosition = HomePositionFor(_current);
            _homeScale = _baseScale * (pair ? PairScale : 1f);

            if (_singleFrame != null) _singleFrame.SetActive(!pair);
            _pairFrame.gameObject.SetActive(pair);
            _second.gameObject.SetActive(pair);
            if (pair)
            {
                Sprite frame = _theme != null ? (vertical ? _theme.handPairV : _theme.handPairH) : null;
                _pairFrame.sprite = frame;
                _pairFrame.color = frame != null ? Color.white : new Color(0f, 0f, 0f, 0f); // chưa gán ảnh: khung vô hình vẫn nhận chạm
                Vector2 size = (vertical ? new Vector2(132f, 252f) : new Vector2(252f, 132f)) * PairScale;
                Place(_pairFrame.rectTransform, _homePosition, size);
                // Figma: mũi tên 160×40 cách tâm khung 88 — trên/dưới (ngang), phải/trái (dọc)
                Vector2 arrowSize = new Vector2(160f, 40f) * PairScale;
                float d = 88f * PairScale;
                PlaceArrow(_arrowA, _homePosition + (vertical ? new Vector2(d, 0f) : new Vector2(0f, d)), arrowSize, vertical ? -90f : 0f);
                PlaceArrow(_arrowB, _homePosition + (vertical ? new Vector2(-d, 0f) : new Vector2(0f, -d)), arrowSize, vertical ? 90f : 180f);
            }
            SetArrowsVisible(pair && cardView.Card != null && !_dragging);
            UpdateCaption(pair, vertical);

            if (!pair)
            {
                ((RectTransform)cardView.transform).anchoredPosition = Vector2.zero;
                return;
            }
            if (animateFrom != null && isActiveAndEnabled)
            {
                _rotating = StartCoroutine(RotateRoutine(animateFrom.Orientation, _current.Orientation));
                return;
            }
            ((RectTransform)cardView.transform).anchoredPosition = CardOffset(_current.Orientation, 0);
            ((RectTransform)_second.transform).anchoredPosition = CardOffset(_current.Orientation, 1);
            if (!_dragging)
            {
                _piece.anchoredPosition = _homePosition;
                _piece.localScale = _homeScale;
            }
        }

        /// <summary>Quay 2 lá quanh tâm khối 90° theo chiều kim đồng hồ (khối dọc dời xuống thì tâm cũng trượt theo).</summary>
        private IEnumerator RotateRoutine(PieceOrientation from, PieceOrientation to)
        {
            var a = (RectTransform)cardView.transform;
            var b = (RectTransform)_second.transform;
            float startAngle = AngleOf(from, 0);
            float endAngle = startAngle - 90f;
            Vector2 startHome = _piece.anchoredPosition;
            yield return Tween.Run(RotateDuration, k =>
            {
                float angle = Mathf.Lerp(startAngle, endAngle, k) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * PairOffset;
                a.anchoredPosition = dir;
                b.anchoredPosition = -dir;
                _piece.anchoredPosition = Vector2.Lerp(startHome, _homePosition, k);
            }, Ease.OutCubic);
            a.anchoredPosition = CardOffset(to, 0);
            b.anchoredPosition = CardOffset(to, 1);
            _piece.anchoredPosition = _homePosition;
            _rotating = null;
        }

        private void StopRotation()
        {
            if (_rotating == null) return;
            StopCoroutine(_rotating);
            _rotating = null;
            if (_current != null && _current.IsPair)
            {
                ((RectTransform)cardView.transform).anchoredPosition = CardOffset(_current.Orientation, 0);
                ((RectTransform)_second.transform).anchoredPosition = CardOffset(_current.Orientation, 1);
            }
            _piece.anchoredPosition = _homePosition;
        }

        /// <summary>Góc (độ) của lá A (index 0) / lá B (index 1) quanh tâm khối: [A B] → A bên trái (180°).</summary>
        private static float AngleOf(PieceOrientation o, int index)
        {
            float a;
            switch (o)
            {
                case PieceOrientation.V_AB: a = 90f; break;   // A trên
                case PieceOrientation.H_BA: a = 0f; break;    // A phải
                case PieceOrientation.V_BA: a = -90f; break;  // A dưới
                default: a = 180f; break;                     // H_AB: A trái
            }
            return index == 0 ? a : a + 180f;
        }

        private static Vector2 CardOffset(PieceOrientation o, int index)
        {
            float rad = AngleOf(o, index) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Round(Mathf.Cos(rad)), Mathf.Round(Mathf.Sin(rad))) * PairOffset;
        }

        private Vector2 HomePositionFor(Piece piece) =>
            piece != null && piece.IsPair && piece.IsVertical ? _baseHome + new Vector2(0f, -VerticalShift) : _baseHome;

        /// <summary>Lá ① (lá đơn: chính lá; khối đôi: lá trái/trên theo hướng hiện tại).</summary>
        private CardView FirstView =>
            IsPair && (_current.Orientation == PieceOrientation.H_BA || _current.Orientation == PieceOrientation.V_BA) ? _second : cardView;

        private CardView SecondView => FirstView == cardView ? _second : cardView;

        private void UpdateCaption(bool pair, bool vertical)
        {
            if (caption == null) return;
            var rt = caption.rectTransform;
            if (!_captionSaved)
            {
                _captionHome = rt.anchoredPosition;
                _captionSaved = true;
            }
            string key = pair ? "hud.pair_hint" : "hud.drag_hint";
            var label = caption.GetComponent<LocalizedLabel>();
            if (label != null) label.Key = key;
            else caption.text = Loc.Get(key);

            float shift = 0f;
            if (pair && !vertical) shift = PairCaptionShiftH;
            else if (pair)
            {
                // Chữ nằm dưới đáy khung dọc: đáy khung (so với tâm) − đáy khung lá đơn
                float pairBottom = VerticalShift + 126f * PairScale;
                float singleBottom = ((RectTransform)transform).rect.height / 2f;
                shift = pairBottom - singleBottom + CaptionGap;
            }
            rt.anchoredPosition = _captionHome + new Vector2(0f, -shift);
        }

        private void SetArrowsVisible(bool visible)
        {
            if (_arrowA == null) return;
            bool show = visible && _theme != null && _theme.rotateArrow != null;
            _arrowA.gameObject.SetActive(show);
            _arrowB.gameObject.SetActive(show);
        }

        private void PlaceArrow(Image img, Vector2 center, Vector2 size, float rotation)
        {
            if (img == _arrowA) _arrowBaseA = rotation;
            else _arrowBaseB = rotation;
            img.sprite = _theme != null ? _theme.rotateArrow : null;
            Place(img.rectTransform, center, size);
            img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void Place(RectTransform rt, Vector2 center, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = center;
        }

        private static void CenterIn(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        private RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = gameObject.layer };
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        private Image NewImage(string name, Transform parent, bool raycast)
        {
            Image img = NewChild(name, parent).gameObject.AddComponent<Image>();
            img.raycastTarget = raycast;
            img.preserveAspect = false;
            return img;
        }

        // ------------------------------------------------------------------ Toạ độ màn hình

        private Camera EventCamera =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private Vector2 PieceCenterScreen() =>
            RectTransformUtility.WorldToScreenPoint(EventCamera, _piece.TransformPoint(_piece.rect.center));

        private Vector2 FirstCenterScreen()
        {
            var rt = (RectTransform)FirstView.transform;
            return RectTransformUtility.WorldToScreenPoint(EventCamera, rt.TransformPoint(rt.rect.center));
        }

        private void MovePieceCenterTo(Vector2 screenPoint)
        {
            var parent = (RectTransform)_piece.parent;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPoint, EventCamera, out Vector3 world))
                return;
            Vector3 centerOffset = _piece.TransformPoint(_piece.rect.center) - _piece.position;
            _piece.position = world - centerOffset;
        }
    }
}
