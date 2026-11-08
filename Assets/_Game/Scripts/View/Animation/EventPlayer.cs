using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;
using SuitMerge.View.Board;
using SuitMerge.View.Cards;
using SuitMerge.View.Hand;
using SuitMerge.View.Hud;
using SuitMerge.View.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.View.Animation
{
    /// <summary>
    /// Phát lần lượt danh sách sự kiện Core của 1 lượt (spec 10.3). Core đã tính xong kết quả;
    /// EventPlayer chỉ "diễn lại" — xong thì GameBootstrap vẽ lại toàn bộ từ Core để chắc khớp.
    /// Lá đang bay và chữ nổi được vẽ trên lớp Fx (nằm trên bàn + tay, dưới popup).
    /// Thời lượng ở Tốc độ = 1; Tốc độ 0.25 = chậm 4 lần (để soi), 2 = nhanh gấp đôi.
    /// </summary>
    public class EventPlayer : MonoBehaviour
    {
        private static readonly Color ScoreTextColor = Color.white;
        private static readonly Color GoldColor = new Color32(0xF6, 0xD4, 0x77, 0xFF);
        private static readonly Color TextShadowColor = new Color32(0x3D, 0x12, 0x56, 0xFF);
        private static readonly Color SpadeTextColor = new Color32(0xFF, 0x9E, 0x94, 0xFF); // đỏ nhạt — điểm ♠
        // Bước 4.5 — token Figma fx-badge 177:716
        private static readonly Color BadgeShadowColor = new Color32(0x29, 0x0F, 0x38, 0xFF);
        private static readonly Color ComboColor = new Color32(0x37, 0xD9, 0xBF, 0xFF);

        [Header("Tham chiếu")]
        [SerializeField] private RectTransform fxLayer;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HandView handView;
        [SerializeField] private HudView hudView;
        [SerializeField] private CardView flyingCardPrefab;
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("HUD Level (bước 4.5): tiến độ mục tiêu + BỘ BÀI. Bỏ trống ở Sandbox.")]
        [SerializeField] private LevelHudView levelHud;

        [Header("Tốc độ (1 = mặc định)")]
        [SerializeField, Range(0.1f, 3f)] private float speed = 1f;

        [Header("Tua nhanh (Animation phần 1)")]
        [Tooltip("Lớp trong suốt phủ màn chơi: chạm khi đang phát → tua nhanh. Bỏ trống = không tua được (vd Sandbox).")]
        [SerializeField] private FastForwardCatcher fastForwardCatcher;
        [Tooltip("Tua nhanh = Time.timeScale bằng số này cho tới hết lượt.")]
        [SerializeField, Range(1f, 6f)] private float fastForwardScale = 3f;

        [Header("Thời lượng (giây, ở Tốc độ = 1)")]
        [Tooltip("Nhịp nảy của lá ② khối đôi trước khi merge (mỗi nửa nhịp).")]
        [SerializeField] private float placeDuration = 0.12f;
        [Tooltip("Animation phần 2: lá trượt từ chỗ ngón tay thả vào giữa ô.")]
        [SerializeField] private float placeSlideDuration = 0.08f;
        [Tooltip("Animation phần 2: chạm bàn ép dẹt → bật giãn → về cỡ thường.")]
        [SerializeField] private float placeSquashDuration = 0.18f;
        [SerializeField] private float mergeFlyDuration = 0.18f;
        [SerializeField] private float createPopDuration = 0.18f;
        [SerializeField] private float chainPause = 0.08f;
        [SerializeField] private float scoreTextDuration = 0.7f;
        [SerializeField] private float comboDuration = 0.8f;
        [SerializeField] private float aceDuration = 0.5f;
        [SerializeField] private float scoreCountDuration = 0.3f;
        [SerializeField] private float drawDuration = 0.2f;
        [SerializeField] private float gameOverDelay = 0.35f;

        [Header("Lượt chơi (Animation phần 3)")]
        [Tooltip("Độ cong đường bay của lá khi merge (0 = thẳng).")]
        [SerializeField] private float mergeCurve = 0.3f;
        [Tooltip("Lá merge thu nhỏ còn bao nhiêu khi tới ô neo.")]
        [SerializeField] private float mergeEndScale = 0.85f;
        [SerializeField] private float mergeFlashDuration = 0.15f;
        [Tooltip("Vòng sáng toả ra khi lá mới bật ra.")]
        [SerializeField] private float createRingDuration = 0.3f;
        [Tooltip("Ace: lá K bay lên + tia sáng (trước khi nổ).")]
        [SerializeField] private float aceRiseDuration = 0.45f;
        [Tooltip("Ace: khựng hình lúc nổ.")]
        [SerializeField] private float aceHitStop = 0.12f;
        [Tooltip("Ace: sau khi nổ — nền sáng lại, pháo giấy rơi.")]
        [SerializeField] private float aceAfterDuration = 0.6f;
        [SerializeField] private float aceRiseHeight = 40f;
        [SerializeField, Range(0f, 1f)] private float aceDimAlpha = 0.55f;
        [SerializeField] private int confettiCount = 40;
        [Tooltip("Bản sao bay từ bàn vào ô mục tiêu.")]
        [SerializeField] private float goalFlyDuration = 0.4f;

        [Header("Đầu / cuối level (Animation phần 4)")]
        [Tooltip("Màn vào level (làn sóng ô → lá rơi → LEVEL N + MỤC TIÊU bay lên HUD → lá vào tay). Endless ngắn hơn (không có LEVEL N).")]
        [SerializeField] private float introDuration = 1.2f;
        [SerializeField] private float introEndlessDuration = 0.85f;
        [Tooltip("Màn thắng: HOÀN THÀNH! đóng dấu + tia sáng + pháo giấy, trước popup.")]
        [SerializeField] private float winDuration = 0.9f;
        [SerializeField] private float winTextSize = 64f;
        [Tooltip("Màn thua: các lá rung nhẹ rồi bàn tối dần, trước popup.")]
        [SerializeField] private float loseShakeDuration = 0.3f;
        [SerializeField] private float loseDimDuration = 0.4f;
        [SerializeField, Range(0f, 1f)] private float loseDimAlpha = 0.5f;

        [Header("Âm thanh (Animation phần 5)")]
        [Tooltip("Bảng âm thanh (Data/Theme/SfxTheme.asset). Bỏ trống = chỉ có âm 4 chất của FxTheme (vd scene Sandbox).")]
        [SerializeField] private SfxTheme sfxTheme;

        [Header("FX chất — chung (bước 1.4b)")]
        [Tooltip("Ảnh FX, màu từng chất, âm thanh (Data/Theme/FxTheme.asset). Bỏ trống = không có hạt / vòng sóng.")]
        [SerializeField] private FxTheme fxTheme;
        [Tooltip("Khựng hình một nhịp khi hiệu ứng chất kích hoạt.")]
        [SerializeField] private bool hitStop = true;
        [SerializeField] private float hitStopDuration = 0.06f;
        [Tooltip("Rung màn hình (tắt nếu thấy chóng mặt).")]
        [SerializeField] private bool screenShake = true;
        [Tooltip("Biên độ rung nhẹ (♦ ♥ ♣), đơn vị UI.")]
        [SerializeField] private float shakeLight = 6f;
        [Tooltip("Biên độ rung mạnh (♠ nổ).")]
        [SerializeField] private float shakeStrong = 16f;
        [SerializeField] private float shakeDuration = 0.28f;
        [Tooltip("Hệ số số lượng hạt (1 = mặc định, 0 = tắt hạt).")]
        [SerializeField, Range(0f, 2f)] private float particleAmount = 1f;
        [Tooltip("Cỡ icon chất to đóng dấu phía trên lá kết quả.")]
        [SerializeField] private float stampSize = 120f;
        [SerializeField] private float stampDuration = 0.6f;
        [SerializeField, Range(0f, 1f)] private float boardFlashAlpha = 0.4f;
        [SerializeField] private float boardFlashDuration = 0.3f;
        [SerializeField] private float ringDuration = 0.4f;

        [Header("FX ♦ Mưa kim cương")]
        [SerializeField] private float diamondCountDuration = 0.28f;
        [SerializeField] private float diamondFlyDuration = 0.32f;
        [SerializeField] private float diamondTextSize = 48f;

        [Header("FX ♥ Nhảy rank")]
        [Tooltip("Thời gian 1 cú nhảy (1 nấc rank).")]
        [SerializeField] private float heartJumpDuration = 0.14f;
        [SerializeField] private float heartJumpHeight = 22f;
        [SerializeField] private float leapTextSize = 44f;

        [Header("FX ♣ Nhân bản")]
        [SerializeField] private float clubWobbleDuration = 0.14f;
        [SerializeField] private float clubFlyDuration = 0.24f;

        [Header("FX ♠ Nổ quét")]
        [SerializeField] private float spadeChargeDuration = 0.16f;
        [SerializeField] private float spadeBlastDuration = 0.36f;
        [Tooltip("Quãng đường các lá bị dọn văng ra.")]
        [SerializeField] private float spadeFlyDistance = 150f;

        [Header("Badge Figma (bước 4.5)")]
        [Tooltip("Chữ trên badge +3 RANK / +1 LÁ / DỌN 3×3 (Figma 28).")]
        [SerializeField] private float badgeTextSize = 28f;
        [Tooltip("Chữ trên badge ×3 (Figma 34).")]
        [SerializeField] private float diamondBadgeTextSize = 34f;
        [Tooltip("COMBO ×k! (Figma 56).")]
        [SerializeField] private float comboBadgeSize = 56f;
        [Tooltip("ACE! (Figma 80) — dòng điểm bên dưới 48.")]
        [SerializeField] private float aceBadgeSize = 80f;
        [SerializeField] private float aceScoreSize = 48f;
        [SerializeField] private float badgeDuration = 0.9f;

        [Header("Chữ nổi")]
        [SerializeField] private float scoreTextSize = 32f;  // bản sửa 1 (2.5): 24 → 32
        [SerializeField] private float comboTextSize = 34f;  // 26 → 34
        [SerializeField] private float aceTextSize = 42f;    // 34 → 42
        [SerializeField] private float floatDistance = 44f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<CellView> _zoned = new List<CellView>();
        // Lá trên bàn bị dịch / xoay tạm (♥ nhảy, ♣ lắc, ♠ rung) → trả lại vị trí gốc khi xong / khi huỷ
        private readonly Dictionary<RectTransform, (Vector2 pos, Quaternion rot, Vector3 scale)> _moved =
            new Dictionary<RectTransform, (Vector2, Quaternion, Vector3)>();
        private UiParticles _fx;
        private ScreenShake _shake;
        private SfxPlayer _sfx;
        private bool _hitStopActive;
        private float _timeScaleBeforeHitStop = 1f;
        private bool _fastForward;

        private static readonly Color DustColor = new Color32(0xFF, 0xEE, 0xB8, 0xFF);
        private static readonly Color DimColor = new Color32(0x0A, 0x00, 0x14, 0xFF);
        private static readonly Color GoalDoneColor = new Color32(0x37, 0xD9, 0xBF, 0xFF);
        private static readonly Color[] ConfettiColors =
        {
            new Color32(0xF6, 0xD4, 0x77, 0xFF), new Color32(0xFF, 0x6B, 0x9A, 0xFF), new Color32(0x4F, 0xA3, 0xF7, 0xFF),
            new Color32(0x37, 0xD9, 0xBF, 0xFF), Color.white, new Color32(0xD8, 0x48, 0x3F, 0xFF)
        };

        // Animation phần 3 — "nguồn" của tiến độ mục tiêu trong lượt đang phát (ô tạo lá, ô kích hoạt chất, ô Ace)
        private readonly List<(CellPos cell, Card card)> _turnCreated = new List<(CellPos, Card)>();
        private readonly List<(Suit suit, CellPos cell)> _turnSuits = new List<(Suit, CellPos)>();
        private readonly List<CellPos> _turnAces = new List<CellPos>();
        private bool _turnDiamond;
        private Card _lastMergeCard;
        private int _goalFlights;

        // Animation phần 4 — màn vào level: mọi thứ tính theo 1 mốc thời gian t → bỏ qua = nhảy thẳng tới cuối
        private bool _introRunning, _skipIntro;
        private System.Action<float> _introApply;
        private float _introTotal;

        private UiParticles Fx => _fx ??= new UiParticles(this, fxLayer);
        private ScreenShake Shaker => _shake ??= new ScreenShake(this, fxLayer != null ? fxLayer.parent as RectTransform : null);
        private SfxPlayer Sfx => _sfx ??= new SfxPlayer(gameObject);

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            // Bước 4.5: overlay Figma cho các ô bàn lấy từ FxTheme (dùng chung scene Game / Sandbox)
            if (boardView != null) boardView.SetFxTheme(fxTheme);
            // M4.5: khung + mũi tên xoay của khối đôi trên tay
            if (handView != null) handView.SetFxTheme(fxTheme);
            // Animation phần 5: bảng âm thanh dùng chung cho tay, nút, popup
            if (sfxTheme != null) AudioService.Theme = sfxTheme;
            if (fastForwardCatcher != null)
            {
                fastForwardCatcher.Tapped += OnCatcherTapped;
                fastForwardCatcher.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (fastForwardCatcher != null) fastForwardCatcher.Tapped -= OnCatcherTapped;
        }

        /// <summary>
        /// Tua nhanh phần còn lại của lượt đang phát (Animation phần 1): Time.timeScale = <see cref="fastForwardScale"/>
        /// tới hết lượt. Mọi đường thoát (hết lượt, huỷ, tắt) đều trả về 1.
        /// </summary>
        public void FastForward()
        {
            if (!IsPlaying || _fastForward) return;
            _fastForward = true;
            // Đang khựng hình (timeScale 0) → khựng xong sẽ trả về tốc độ tua nhanh
            if (_hitStopActive) _timeScaleBeforeHitStop = fastForwardScale;
            else Time.timeScale = fastForwardScale;
        }

        /// <summary>Chạm khi đang phát: màn vào level → bỏ qua; lượt chơi → tua nhanh.</summary>
        private void OnCatcherTapped()
        {
            if (_introRunning) _skipIntro = true;
            else FastForward();
        }

        private void EndFastForward()
        {
            if (!_fastForward) return;
            _fastForward = false;
            if (_hitStopActive) _timeScaleBeforeHitStop = 1f;
            else Time.timeScale = 1f;
        }

        /// <summary>Hệ số tốc độ animation (1 = mặc định). Scene Sandbox đổi lúc chạy để soi chậm.</summary>
        public float Speed
        {
            get => speed;
            set => speed = Mathf.Clamp(value, 0.1f, 3f);
        }

        /// <summary>Thời gian chờ trước khi hiện popup Game Over (đã tính Tốc độ).</summary>
        public float GameOverDelay => Scaled(gameOverDelay);

        private float Scaled(float seconds) => seconds / Mathf.Max(0.01f, speed);

        /// <summary>Phát toàn bộ sự kiện của 1 lượt. <paramref name="savedBest"/> = kỷ lục đã lưu trước lượt (cho dòng BEST).</summary>
        public IEnumerator Play(IReadOnlyList<GameEvent> events, int savedBest)
        {
            IsPlaying = true;
            if (fastForwardCatcher != null) fastForwardCatcher.SetActive(true);
            CellPos lastAnchor = default;
            int lastCreatedScore = 0;
            _turnCreated.Clear();
            _turnSuits.Clear();
            _turnAces.Clear();
            _turnDiamond = false;
            bool won = false;

            for (int i = 0; i < events.Count; i++)
            {
                GameEvent e = events[i];
                switch (e)
                {
                    case CardPlacedEvent placed:
                        // Khối đôi: 2 lá hạ xuống CÙNG LÚC (spec 3.1 bước 3) → lá ① không chờ
                        if (i + 1 < events.Count && events[i + 1] is CardPlacedEvent) StartCoroutine(PlayPlaced(placed));
                        else yield return PlayPlaced(placed);
                        break;

                    case PairSecondResolveEvent second:
                        yield return PlaySecondResolve(second);
                        break;

                    case MergeStartedEvent merge:
                        lastAnchor = merge.Anchor;
                        yield return PlayMerge(merge);
                        break;

                    case ChainStepEvent chain:
                        PlayCombo(chain.Step, lastAnchor);
                        break;

                    case CardCreatedEvent created:
                    {
                        // ♥: lá bật ra ở rank thường (R+1) trước, HeartLeap sẽ cho nó nhảy tiếp
                        // ♦: chữ điểm do màn "Mưa kim cương" lo (đếm 150 → 450 rồi bay vào HUD)
                        GameEvent next = i + 1 < events.Count ? events[i + 1] : null;
                        lastCreatedScore = created.ScoreGained;
                        _turnCreated.Add((created.Cell, created.Card));
                        yield return PlayCreated(created, next as HeartLeapEvent, showScore: !(next is DiamondBonusEvent));
                        break;
                    }

                    case DiamondBonusEvent diamond:
                        _turnSuits.Add((Suit.Diamond, diamond.Cell));
                        _turnDiamond = true;
                        yield return PlayDiamond(diamond, lastCreatedScore);
                        break;

                    case HeartLeapEvent heart:
                        _turnSuits.Add((Suit.Heart, heart.Cell));
                        yield return PlayHeartLeap(heart);
                        break;

                    case ClubSpawnEvent club:
                        _turnSuits.Add((Suit.Club, club.Origin));
                        if (club.SpawnCell.HasValue && club.Card != null) _turnCreated.Add((club.SpawnCell.Value, club.Card));
                        yield return PlayClubSpawn(club);
                        break;

                    case SpadeClearEvent spade:
                        _turnSuits.Add((Suit.Spade, spade.Center));
                        yield return PlaySpadeClear(spade);
                        break;

                    case AceCreatedEvent ace:
                        _turnAces.Add(ace.Cell);
                        yield return PlayAce(ace);
                        break;

                    case ScoreChangedEvent score:
                        hudView.AnimateScore(score.OldScore, score.NewScore, Scaled(scoreCountDuration), savedBest);
                        break;

                    case CardsDrawnEvent drawn:
                        yield return PlayDrawn(drawn);
                        if (levelHud != null) levelHud.TakeFromDeck(drawn.NextPiece?.Count ?? 0);
                        break;

                    case GoalProgressEvent goal:
                        if (levelHud != null) StartCoroutine(PlayGoal(goal, lastAnchor)); // song song, không bắt lượt chờ
                        break;

                    case LevelWonEvent _:
                        won = true;
                        break;

                    // GameOverEvent: GameBootstrap xử lý (chờ GameOverDelay rồi hiện popup)
                }
            }

            // Lượt thắng: chờ bản sao bay tới ô mục tiêu + dấu ✓ đóng xong rồi mới hiện popup Hoàn thành
            if (won && _goalFlights > 0)
            {
                while (_goalFlights > 0) yield return null;
                yield return Tween.Delay(Scaled(0.3f));
            }
            EndPlaying();
        }

        /// <summary>Hết lượt: tắt lớp bắt chạm, về tốc độ thường.</summary>
        private void EndPlaying()
        {
            if (fastForwardCatcher != null && !_introRunning) fastForwardCatcher.SetActive(false);
            EndFastForward();
            IsPlaying = false;
        }

        /// <summary>Huỷ mọi animation đang chạy + dọn lá bay / chữ nổi (vd khi PLAY AGAIN).</summary>
        public void StopAll()
        {
            FinishIntro();
            StopAllCoroutines();
            foreach (GameObject go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
            foreach (CellView c in _zoned)
                if (c != null) c.SetZone(CellZone.None);
            _zoned.Clear();
            _fx?.Clear();
            _shake?.Stop();
            _goalFlights = 0;
            if (levelHud != null) levelHud.ReleaseGoals(); // lần Sync sau vẽ đúng số từ Core
            EndHitStop();
            RestoreMoved();
            EndPlaying();
        }

        private void OnDisable()
        {
            // Không bao giờ để game kẹt ở Time.timeScale = 0 (khựng hình) / 3 (tua nhanh) hay khung bị lệch (rung)
            EndHitStop();
            EndFastForward();
            _shake?.Stop();
        }

        // ------------------------------------------------------------------ Đầu / cuối level (Animation phần 4)

        /// <summary>
        /// Màn vào level (≈1,2s; Endless ≈0,85s, không có LEVEL N). Gọi SAU khi bàn / tay / HUD đã vẽ đúng trạng thái đầu ván.
        /// Ô bàn hiện theo làn sóng chéo → lá có sẵn rơi xuống ô → khung LEVEL N + MỤC TIÊU hiện giữa màn rồi bay lên chỗ cũ
        /// → lá trên tay + UP NEXT bật ra. Chạm màn hình = bỏ qua (nhảy tới trạng thái cuối).
        /// </summary>
        public IEnumerator PlayIntro(bool level)
        {
            FinishIntro();
            var cells = new List<(RectTransform cell, RectTransform card, Vector2 cardHome, int diag)>();
            foreach (CellView c in boardView.Cells)
            {
                if (c == null) continue;
                RectTransform card = c.CardView.Card != null ? (RectTransform)c.CardView.transform : null;
                cells.Add((c.RectTransform, card, card != null ? card.anchoredPosition : Vector2.zero, c.Pos.Row + c.Pos.Col));
            }

            var panels = new List<(RectTransform rt, CanvasGroup group, Vector3 home, Vector3 start, Vector3 homeScale, float scale)>();
            if (level && levelHud != null && fxLayer != null)
            {
                Vector3 center = fxLayer.TransformPoint(fxLayer.rect.center);
                AddIntroPanel(panels, levelHud.TitlePanel, center + fxLayer.TransformVector(new Vector3(0f, 60f, 0f)), 1.6f);
                AddIntroPanel(panels, levelHud.ObjectivePanel, center + fxLayer.TransformVector(new Vector3(0f, -30f, 0f)), 1.3f);
            }

            float handStart = level ? 0.9f : 0.55f;
            void Apply(float t)
            {
                foreach ((RectTransform cell, RectTransform card, Vector2 cardHome, int diag) in cells)
                {
                    float k = Mathf.Clamp01((t - 0.04f * diag) / 0.2f);
                    if (cell != null) cell.localScale = Vector3.one * (k <= 0f ? 0f : Ease.OutBack(k));
                    if (card == null) continue;
                    float kc = Mathf.Clamp01((t - 0.3f - 0.03f * diag) / 0.2f);
                    card.localScale = Vector3.one * (kc <= 0f ? 0f : Mathf.Lerp(1.15f, 1f, kc));
                    card.anchoredPosition = cardHome + new Vector2(0f, 60f * (1f - Ease.OutCubic(kc)));
                }
                foreach ((RectTransform rt, CanvasGroup group, Vector3 home, Vector3 start, Vector3 homeScale, float scale) in panels)
                {
                    if (rt == null) continue;
                    float a = Mathf.Clamp01((t - 0.1f) / 0.2f);
                    float fly = Ease.OutCubic(Mathf.Clamp01((t - 0.65f) / 0.3f));
                    group.alpha = a;
                    float pop = a < 1f ? Mathf.LerpUnclamped(0.7f, 1f, Ease.OutBack(a)) : 1f;
                    rt.localScale = homeScale * (Mathf.Lerp(scale, 1f, fly) * pop);
                    rt.position = Vector3.Lerp(start, home, fly);
                }
                float kh = Mathf.Clamp01((t - handStart) / 0.25f);
                handView.SetAppear(kh <= 0f ? 0f : Ease.OutBack(kh));
                float kn = Mathf.Clamp01((t - handStart - 0.05f) / 0.25f);
                hudView.SetNextAppear(kn <= 0f ? 0f : Ease.OutBack(kn));
            }

            _introApply = Apply;
            _introTotal = level ? introDuration : introEndlessDuration;
            _introRunning = true;
            _skipIntro = false;
            if (fastForwardCatcher != null) fastForwardCatcher.SetActive(true);

            float time = 0f;
            Apply(0f);
            while (time < _introTotal && !_skipIntro)
            {
                yield return null;
                time += Time.deltaTime * Mathf.Max(0.01f, speed);
                Apply(Mathf.Min(time, _introTotal));
            }
            FinishIntro();
        }

        /// <summary>
        /// Khung HUD bay từ giữa màn (<paramref name="target"/> = tâm khung lúc ở giữa, phóng <paramref name="scale"/>) về chỗ cũ.
        /// Tính theo tâm khung (khung neo góc trên-trái) để phóng to vẫn đúng giữa.
        /// </summary>
        private static void AddIntroPanel(List<(RectTransform, CanvasGroup, Vector3, Vector3, Vector3, float)> panels,
            RectTransform rt, Vector3 target, float scale)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt); // khung co theo chữ (ContentSizeFitter) → đúng cỡ trước khi tính tâm
            CanvasGroup group = rt.GetComponent<CanvasGroup>();
            if (group == null) group = rt.gameObject.AddComponent<CanvasGroup>();
            Vector3 home = rt.position;
            Vector3 centerOffset = rt.TransformPoint(rt.rect.center) - home;
            panels.Add((rt, group, home, target - centerOffset * scale, rt.localScale, scale));
        }

        /// <summary>Kết thúc màn vào level ngay (bỏ qua / huỷ): mọi thứ về đúng trạng thái cuối.</summary>
        private void FinishIntro()
        {
            if (!_introRunning) return;
            _introRunning = false;
            _introApply?.Invoke(_introTotal);
            _introApply = null;
            if (fastForwardCatcher != null && !IsPlaying) fastForwardCatcher.SetActive(false);
        }

        /// <summary>
        /// Màn thắng (trước popup Hoàn thành): chữ HOÀN THÀNH! đóng dấu giữa bàn, tia sáng xanh ngọc xoay phía sau,
        /// pháo giấy rơi từ mép trên xuống (GIẢM BỚT: một nửa).
        /// </summary>
        public IEnumerator PlayWin()
        {
            var board = (RectTransform)boardView.transform;
            Vector3 center = board.TransformPoint(board.rect.center);
            Image rays = fxTheme != null && fxTheme.rays != null ? Fx.Spawn(fxTheme.rays, center, 420f, GoalDoneColor, "WinRays") : null;
            RectTransform text = MakeShadowText(Loc.Get("complete.done"), center, winTextSize, GoalDoneColor);
            var group = text.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            AudioService.Play(SfxId.Win, vary: false);

            if (fxTheme != null && fxTheme.flash != null && fxLayer != null)
            {
                Rect r = fxLayer.rect;
                foreach (float x in new[] { 0.2f, 0.5f, 0.8f })
                {
                    Vector3 from = fxLayer.TransformPoint(new Vector3(Mathf.Lerp(r.xMin, r.xMax, x), r.yMax - 30f, 0f));
                    UiParticles.BurstSettings c = Spray(Mathf.Max(1, confettiCount / 2), 8f, 12f, 60f, 240f, 260f, 1.8f, 270f, 80f, 540f);
                    c.Aspect = 1.6f;
                    c.Palette = ConfettiColors;
                    Fx.Burst(fxTheme.flash, from, Color.white, c);
                }
            }

            yield return Tween.Run(Scaled(winDuration), k =>
            {
                if (text != null)
                {
                    float sc = k < 0.25f ? Mathf.Lerp(0f, 1.4f, Ease.OutCubic(k / 0.25f))
                             : k < 0.4f ? Mathf.Lerp(1.4f, 1f, (k - 0.25f) / 0.15f) : 1f;
                    text.localScale = Vector3.one * sc;
                    group.alpha = k < 0.8f ? 1f : 1f - (k - 0.8f) / 0.2f;
                }
                if (rays != null)
                {
                    SetAlpha(rays, 0.6f * (k < 0.2f ? k / 0.2f : k > 0.8f ? (1f - k) / 0.2f : 1f));
                    rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -40f * k);
                }
            });
            if (text != null) Despawn(text.gameObject);
            if (rays != null) Fx.Despawn(rays.gameObject);
        }

        /// <summary>
        /// Màn thua (trước popup thua): các lá trên bàn rung nhẹ (GIẢM BỚT: bỏ), rồi bàn tối dần. Lớp tối ở lại tới khi
        /// chơi lại / đổi màn (StopAll dọn).
        /// </summary>
        public IEnumerator PlayLose()
        {
            AudioService.Play(SfxId.Lose, vary: false);
            if (!FxSettings.Reduced)
            {
                var cards = new List<RectTransform>();
                foreach (CellView c in boardView.Cells)
                {
                    if (c == null || c.CardView.Card == null) continue;
                    var rt = (RectTransform)c.CardView.transform;
                    Remember(rt);
                    cards.Add(rt);
                }
                yield return Tween.Run(Scaled(loseShakeDuration), k =>
                {
                    foreach (RectTransform rt in cards)
                        if (rt != null && _moved.TryGetValue(rt, out var home))
                            rt.anchoredPosition = home.pos + Random.insideUnitCircle * (4f * (1f - k));
                });
                RestoreMoved();
            }

            if (fxTheme == null || fxTheme.flash == null || fxLayer == null) yield break;
            var board = (RectTransform)boardView.transform;
            Image dim = Fx.Spawn(fxTheme.flash, board.TransformPoint(board.rect.center), 0f, DimColor, "LoseDim");
            dim.type = Image.Type.Sliced;
            float toFx = fxLayer.lossyScale.x > 0f ? board.lossyScale.x / fxLayer.lossyScale.x : 1f;
            dim.rectTransform.sizeDelta = board.rect.size * toFx;
            yield return Tween.Run(Scaled(loseDimDuration), k => SetAlpha(dim, loseDimAlpha * k));
        }

        // ------------------------------------------------------------------ Từng sự kiện

        /// <summary>
        /// Animation phần 2: lá trượt từ chỗ ngón tay thả vào giữa ô (thẳng lại, bóng hạ xuống) → chạm bàn ÉP dẹt 1,18×0,85
        /// kèm bụi sáng toả 2 bên → bật GIÃN 0,92×1,08 → về 1. Lá không đến từ tay (không có vị trí thả) thì bỏ bước trượt.
        /// </summary>
        private IEnumerator PlayPlaced(CardPlacedEvent placed)
        {
            bool fromHand = handView.TakeDropPose(placed.Card, out Vector3 dropWorld, out float dropWorldSize, out float dropRot);
            handView.HideCard();
            CellView cell = boardView.GetCell(placed.Cell);
            if (cell == null) yield break;

            if (fromHand && fxLayer != null && flyingCardPrefab != null && placeSlideDuration > 0f)
            {
                float cellSize = CellSize(placed.Cell);
                float fromSize = fxLayer.lossyScale.x > 0f ? dropWorldSize / fxLayer.lossyScale.x : cellSize;
                Vector3 target = cell.CenterWorld;
                CardView flyer = SpawnCard(placed.Card, dropWorld, fromSize, shadow: true);
                var rt = (RectTransform)flyer.transform;
                yield return Tween.Run(Scaled(placeSlideDuration), k =>
                {
                    if (flyer == null) return;
                    rt.position = Vector3.LerpUnclamped(dropWorld, target, k);
                    rt.sizeDelta = Vector2.one * Mathf.Lerp(fromSize, cellSize, k);
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(dropRot, 0f, k));
                    flyer.ApplyLayout();
                    flyer.SetLift(1f - k);
                }, Ease.OutQuad);
                Despawn(flyer.gameObject);
            }

            cell.ShowCard(placed.Card);
            Transform card = cell.CardView.transform;
            PlaceDust(cell);
            AudioService.Play(SfxId.Place);
            var squash = new Vector2(1.18f, 0.85f);
            var stretch = new Vector2(0.92f, 1.08f);
            yield return Tween.Run(Scaled(placeSquashDuration), k =>
            {
                if (card == null) return;
                Vector2 v = k < 0.4f
                    ? Vector2.LerpUnclamped(squash, stretch, Ease.OutQuad(k / 0.4f))
                    : Vector2.LerpUnclamped(stretch, Vector2.one, Ease.OutBack((k - 0.4f) / 0.6f));
                card.localScale = new Vector3(v.x, v.y, 1f);
            });
            if (card != null) card.localScale = Vector3.one;
        }

        /// <summary>6 hạt bụi sáng toả lên 2 bên từ đáy ô rồi rơi (GIẢM BỚT: còn một nửa).</summary>
        private void PlaceDust(CellView cell)
        {
            if (fxTheme == null || fxTheme.spark == null || cell == null) return;
            RectTransform rt = cell.RectTransform;
            Vector3 bottom = rt.TransformPoint(rt.rect.center + new Vector2(0f, -rt.rect.height * 0.42f));
            Fx.Burst(fxTheme.spark, bottom, DustColor, Spray(6, 10f, 18f, 140f, 260f, 420f, 0.35f, 90f, 150f));
        }

        private IEnumerator PlayMerge(MergeStartedEvent merge)
        {
            CellView anchor = boardView.GetCell(merge.Anchor);
            if (anchor == null) yield break;
            Vector3 target = anchor.CenterWorld;

            // Ace (Animation phần 3): nhớ lá của nhóm (K) để màn Ace cho nó bay lên
            _lastMergeCard = anchor.CardView.Card;

            var flyers = new List<CardView>();
            foreach (CellPos p in merge.Group)
            {
                if (p == merge.Anchor) continue;
                CellView cell = boardView.GetCell(p);
                if (cell == null || cell.CardView.Card == null) continue;
                if (_lastMergeCard == null) _lastMergeCard = cell.CardView.Card;
                CardView flyer = SpawnCard(cell.CardView.Card, cell.CenterWorld, cell.RectTransform.rect.width, false);
                cell.ShowCard(null);
                flyers.Add(flyer);
                StartCoroutine(MergeFly(flyer, cell.CenterWorld, target, cell.RectTransform.rect.width));
            }

            yield return Tween.Delay(Scaled(mergeFlyDuration));
            foreach (CardView f in flyers)
                if (f != null) Despawn(f.gameObject);
            anchor.ShowCard(null); // lá neo cũng bị "nuốt" — lá mới sẽ bật ra ở đây
            MergeFlash(target, anchor.RectTransform.rect.width);
            AudioService.Play(SfxId.Merge);
        }

        /// <summary>
        /// Animation phần 3: lá bay CONG về ô neo (nhanh dần), thu nhỏ còn <see cref="mergeEndScale"/>, để lại 3 bóng mờ phía sau.
        /// Mọi lá cong cùng một chiều → trông như cuộn xoáy về ô neo.
        /// </summary>
        private IEnumerator MergeFly(CardView flyer, Vector3 from, Vector3 to, float size)
        {
            Vector3 d = to - from;
            Vector3 control = (from + to) * 0.5f + new Vector3(-d.y, d.x, 0f) * mergeCurve;
            var rt = (RectTransform)flyer.transform;
            float duration = Scaled(mergeFlyDuration);
            StartCoroutine(GhostTrail(flyer, size, duration));
            yield return Tween.Run(duration, k =>
            {
                if (rt == null) return;
                rt.position = Tween.Bezier(from, control, to, k);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, mergeEndScale, k);
            }, Ease.InCubic);
        }

        /// <summary>Bóng mờ của lá đang bay: cứ 1/4 quãng bay để lại 1 bản sao mờ, tắt dần trong 0,15s.</summary>
        private IEnumerator GhostTrail(CardView flyer, float size, float duration)
        {
            float interval = duration / 4f;
            for (int i = 0; i < 3; i++)
            {
                yield return Tween.Delay(interval);
                if (flyer == null || flyer.Card == null) yield break;
                CardView ghost = SpawnCard(flyer.Card, flyer.transform.position, size * flyer.transform.localScale.x, false);
                var group = ghost.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                ghost.transform.SetSiblingIndex(flyer.transform.GetSiblingIndex()); // dưới lá thật
                StartCoroutine(FadeAndDespawn(group, 0.45f, Scaled(0.15f)));
            }
        }

        private IEnumerator FadeAndDespawn(CanvasGroup group, float from, float duration)
        {
            yield return Tween.Fade(group, from, 0f, duration);
            if (group != null) Despawn(group.gameObject);
        }

        /// <summary>Lóe trắng ở ô neo lúc các lá gộp lại (GIẢM BỚT: nhạt bớt).</summary>
        private void MergeFlash(Vector3 center, float size)
        {
            if (fxTheme == null || fxTheme.glow == null) return;
            var c = new Color(1f, 1f, 1f, FxSettings.Reduced ? 0.5f : 0.95f);
            Image img = Fx.Spawn(fxTheme.glow, center, size * 2.2f, c, "MergeFlash");
            StartCoroutine(Fx.FlashImage(img, c, Scaled(mergeFlashDuration)));
        }

        private IEnumerator PlayCreated(CardCreatedEvent created, HeartLeapEvent leap, bool showScore)
        {
            CellView cell = boardView.GetCell(created.Cell);
            if (cell == null) yield break;
            Card shown = created.Card;
            if (leap != null && leap.Cell == created.Cell && leap.Leap > 1)
                shown = new Card(leap.FromRank.Next(), created.Card.Suit);
            cell.ShowCard(shown);
            CreatedRing(cell, created.Card);
            AudioService.Play(SfxId.Create);
            if (showScore)
                StartCoroutine(FloatText("+" + created.ScoreGained, cell.CenterWorld, scoreTextSize, ScoreTextColor,
                    Scaled(scoreTextDuration), popScale: 1f, startOffset: 0f));
            yield return Tween.Scale(cell.CardView.transform, Vector3.zero, Vector3.one, Scaled(createPopDuration), Ease.OutBack);
            yield return Tween.Delay(Scaled(chainPause));
        }

        /// <summary>Lá mới bật ra: vòng sáng trắng toả; rank ≥ J: vòng VÀNG to hơn + tia lấp lánh.</summary>
        private void CreatedRing(CellView cell, Card card)
        {
            if (fxTheme == null || fxTheme.ring == null || card == null) return;
            float size = cell.RectTransform.rect.width;
            bool high = card.Rank >= Rank.Jack;
            Color color = high ? GoldColor : new Color(1f, 1f, 1f, 0.8f);
            StartCoroutine(Fx.Ring(fxTheme.ring, cell.CenterWorld, color, size, size * (high ? 2.2f : 1.7f), Scaled(createRingDuration)));
            if (high && fxTheme.spark != null)
                Fx.Burst(fxTheme.spark, cell.CenterWorld, GoldColor, Spray(8, 14f, 26f, 160f, 320f, 200f, 0.5f));
        }

        /// <summary>
        /// COMBO ×k leo thang: k = 2 như cũ · k = 3 chữ to 1,2 + khung bàn chớp xanh ngọc + rung nhẹ ·
        /// k ≥ 4 chữ VÀNG to 1,5 (k ≥ 5: 1,65) + tia lấp lánh + chớp vàng + rung mạnh hơn.
        /// </summary>
        private void PlayCombo(int step, CellPos anchor)
        {
            Vector3 at = boardView.CellCenterWorld(anchor);
            int level = Mathf.Min(step, 5);
            AudioService.Play(SfxId.Combo, pitch: 1f + 0.12f * (Mathf.Min(step, 7) - 2), vary: false);
            float scale = level <= 2 ? 1f : level == 3 ? 1.2f : level == 4 ? 1.5f : 1.65f;
            Color color = level >= 4 ? GoldColor : ComboColor;
            // Bước 4.5: chữ Figma "COMBO ×k!" có bóng; FxTheme cũ → chữ cũ
            if (HasFigmaBadges)
                StartCoroutine(Float(MakeShadowText(Loc.Format("fx.combo", step), at, comboBadgeSize * scale, color),
                    Scaled(comboDuration), popScale: 1.4f, startOffset: 30f));
            else
                StartCoroutine(FloatText($"Combo ×{step}", at, comboTextSize * scale, GoldColor, Scaled(comboDuration),
                    popScale: 1.4f, startOffset: 30f));
            if (level < 3) return;
            BoardFlash(color);
            if (screenShake) Shaker.Shake(shakeLight * (level - 2), Scaled(shakeDuration));
            if (level >= 4 && fxTheme != null)
            {
                Vector3 text = at + fxLayer.TransformVector(new Vector3(0f, 40f, 0f));
                Fx.Burst(fxTheme.spark, text, GoldColor, Spray(10, 16f, 30f, 220f, 420f, 150f, 0.6f));
            }
        }

        /// <summary>
        /// Animation phần 3 — màn Ace (~1,2s): nền tối dần, lá K bay lên phóng 1,35 trước tia sáng vàng xoay →
        /// NỔ: khựng hình, vòng sóng vàng, hạt vàng, pháo giấy, chớp vàng, rung, chữ ACE! → nền sáng lại.
        /// Chưa có ảnh tia sáng (FxTheme.rays) → chữ ACE! như bước 4.5.
        /// </summary>
        private IEnumerator PlayAce(AceCreatedEvent ace)
        {
            if (fxTheme == null || fxTheme.rays == null)
            {
                yield return PlayAceSimple(ace);
                yield break;
            }

            CellView cell = boardView.GetCell(ace.Cell);
            if (cell != null) cell.ShowCard(null);
            Vector3 center = boardView.CellCenterWorld(ace.Cell);
            float size = CellSize(ace.Cell);
            Vector3 top = center + fxLayer.TransformVector(new Vector3(0f, aceRiseHeight, 0f));

            Image dim = Fx.Spawn(null, fxLayer.TransformPoint(fxLayer.rect.center), 0f, DimColor, "AceDim");
            dim.rectTransform.sizeDelta = fxLayer.rect.size;
            Image rays = Fx.Spawn(fxTheme.rays, top, size * 4.5f, GoldColor, "AceRays");
            Image glow = fxTheme.glow != null ? Fx.Spawn(fxTheme.glow, top, size * 2.6f, GoldColor, "AceGlow") : null;
            CardView king = SpawnCard(_lastMergeCard ?? new Card(Rank.King, Suit.Spade), center, size, shadow: true);
            var kingRt = (RectTransform)king.transform;
            float spin = 0f;

            void Backdrop(float k)
            {
                SetAlpha(dim, aceDimAlpha * k);
                SetAlpha(rays, 0.8f * k);
                if (glow != null) SetAlpha(glow, 0.9f * k);
                if (rays != null) rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -spin);
            }

            // 1. Bay lên + tụ lực (rung nhẹ ở cuối)
            float rise = Scaled(aceRiseDuration);
            yield return Tween.Run(rise, k =>
            {
                spin = 40f * k;
                Backdrop(k);
                if (kingRt == null) return;
                float jitter = k > 0.7f ? Mathf.Sin(k * 90f) * 3f * (k - 0.7f) / 0.3f : 0f;
                kingRt.position = Vector3.LerpUnclamped(center, top, Ease.OutCubic(k)) + fxLayer.TransformVector(new Vector3(jitter, 0f, 0f));
                kingRt.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 1.35f, Ease.OutBack(k));
            });

            // 2. NỔ
            if (fxTheme.stamp != null) Sfx.Play(fxTheme.stamp, fxTheme.volume);
            if (hitStop) yield return HitStop(aceHitStop);
            if (king != null) Despawn(king.gameObject);
            AudioService.Play(SfxId.Ace, vary: false);
            if (fxTheme.ring != null) StartCoroutine(Fx.Ring(fxTheme.ring, top, GoldColor, size, size * 4f, Scaled(0.45f)));
            if (fxTheme.spark != null) Fx.Burst(fxTheme.spark, top, GoldColor, Spray(14, 18f, 34f, 300f, 560f, 250f, 0.7f));
            Confetti(top);
            BoardFlash(GoldColor);
            if (screenShake) Shaker.Shake(shakeStrong * 0.75f, Scaled(shakeDuration * 1.2f));
            if (HasFigmaBadges)
                StartCoroutine(Float(MakeAceBadge("+" + Loc.Number(ace.ScoreGained), top), Scaled(aceDuration) * 2f, popScale: 1.6f, startOffset: 0f));
            else
                StartCoroutine(FloatText("+" + ace.ScoreGained.ToString("N0", CultureInfo.InvariantCulture), top, aceTextSize, GoldColor,
                    Scaled(aceDuration) * 2f, popScale: 1.6f, startOffset: 0f));

            // 3. Nền sáng lại, tia sáng tắt dần (vẫn xoay)
            yield return Tween.Run(Scaled(aceAfterDuration), k =>
            {
                spin = 40f + 50f * k;
                Backdrop(1f - k);
            });
            Fx.Despawn(dim.gameObject);
            if (rays != null) Fx.Despawn(rays.gameObject);
            if (glow != null) Fx.Despawn(glow.gameObject);
        }

        private static void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            Color c = img.color;
            c.a = a;
            img.color = c;
        }

        /// <summary>Pháo giấy: mảnh chữ nhật 6 màu bung mọi hướng, xoay, rơi chậm (GIẢM BỚT: một nửa).</summary>
        private void Confetti(Vector3 world)
        {
            if (fxTheme == null || fxTheme.flash == null) return;
            UiParticles.BurstSettings s = Spray(confettiCount, 8f, 12f, 300f, 620f, 380f, 1.4f, 90f, 360f, 540f);
            s.Aspect = 1.6f;
            s.Palette = ConfettiColors;
            Fx.Burst(fxTheme.flash, world, Color.white, s);
        }

        /// <summary>
        /// Animation phần 3 — tiến độ mục tiêu: bản sao nhỏ bay cong từ ô nguồn trên bàn vào ô mục tiêu (≤ 3 bản, cách nhau 0,06s).
        /// TẠO LÁ: lá nhỏ · KÍCH HOẠT CHẤT: icon chất · ACE: quầng vàng · ĐIỂM: tia sáng vàng (lượt ♦ thì chữ điểm ♦ đã bay rồi → bỏ).
        /// Tới nơi: số đổi + ô nảy + tia sáng; vừa hoàn thành: dấu ✓ đóng dấu + vòng sóng xanh ngọc. Không có nguồn → đổi số ngay như cũ.
        /// </summary>
        private IEnumerator PlayGoal(GoalProgressEvent e, CellPos lastAnchor)
        {
            GoalData goal = levelHud.GoalAt(e.Index);
            Vector3? targetOrNull = levelHud.GoalIconWorld(e.Index);
            var sources = new List<(Vector3 world, Card card, Sprite sprite, Color color, float size)>();
            if (goal != null && targetOrNull.HasValue && e.NewValue > e.OldValue) CollectGoalSources(goal, e, lastAnchor, sources);
            if (sources.Count == 0)
            {
                levelHud.SetGoalProgress(e.Index, e.NewValue, animate: true);
                if (!e.JustCompleted) AudioService.Play(SfxId.GoalStep);
                if (e.JustCompleted) GoalDone(e.Index, targetOrNull);
                yield break;
            }

            Vector3 target = targetOrNull.Value;
            _goalFlights++;
            levelHud.HoldGoal(e.Index);
            float duration = Scaled(goalFlyDuration);
            var flying = new List<(GameObject go, bool isCard)>();
            for (int i = 0; i < sources.Count; i++)
            {
                (Vector3 from, Card card, Sprite sprite, Color color, float size) = sources[i];
                RectTransform rt = card != null
                    ? (RectTransform)SpawnCard(card, from, size, false).transform
                    : Fx.Spawn(sprite, from, size, color, "GoalFlyer").rectTransform;
                flying.Add((rt.gameObject, card != null));
                StartCoroutine(GoalFly(rt, from, target, size, i * Scaled(0.06f), duration));
            }
            yield return Tween.Delay(duration + (sources.Count - 1) * Scaled(0.06f));

            foreach ((GameObject go, bool isCard) in flying)
            {
                if (go == null) continue;
                if (isCard) Despawn(go);
                else Fx.Despawn(go);
            }
            if (fxTheme != null && fxTheme.spark != null) Fx.Burst(fxTheme.spark, target, GoldColor, Spray(6, 12f, 20f, 100f, 200f, 0f, 0.35f));
            levelHud.SetGoalProgress(e.Index, e.NewValue, animate: true);
            if (!e.JustCompleted) AudioService.Play(SfxId.GoalStep);
            if (e.JustCompleted) GoalDone(e.Index, target);
            _goalFlights = Mathf.Max(0, _goalFlights - 1);
        }

        private IEnumerator GoalFly(RectTransform rt, Vector3 from, Vector3 to, float fromSize, float delay, float duration)
        {
            if (delay > 0f) yield return Tween.Delay(delay);
            Vector3 d = to - from;
            Vector3 control = (from + to) * 0.5f + new Vector3(d.y, -d.x, 0f) * 0.3f;
            const float endSize = 34f;
            yield return Tween.Run(duration, k =>
            {
                if (rt == null) return;
                rt.position = Tween.Bezier(from, control, to, k);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, endSize / Mathf.Max(1f, fromSize), Ease.InCubic(k));
            }, Ease.InOutSine);
        }

        private void GoalDone(int index, Vector3? target)
        {
            levelHud.StampGoal(index);
            AudioService.Play(SfxId.GoalDone, vary: false);
            if (!target.HasValue || fxTheme == null) return;
            if (fxTheme.ring != null) StartCoroutine(Fx.Ring(fxTheme.ring, target.Value, GoalDoneColor, 30f, 120f, Scaled(0.4f)));
            if (fxTheme.spark != null) Fx.Burst(fxTheme.spark, target.Value, GoalDoneColor, Spray(12, 14f, 24f, 140f, 260f, 0f, 0.45f));
        }

        /// <summary>Ô nguồn trên bàn cho mục tiêu <paramref name="goal"/> trong lượt vừa phát (tối đa 3, mới nhất trước).</summary>
        private void CollectGoalSources(GoalData goal, GoalProgressEvent e, CellPos lastAnchor,
            List<(Vector3 world, Card card, Sprite sprite, Color color, float size)> into)
        {
            int want = goal.Type == GoalType.Score ? 1 : Mathf.Clamp(e.NewValue - e.OldValue, 1, 3);
            float cell = boardView.CellSize;
            Sprite glow = fxTheme != null ? fxTheme.glow : null;
            Sprite spark = fxTheme != null ? fxTheme.spark : null;
            var cells = new List<(CellPos pos, Card card, Sprite sprite, Color color)>();
            switch (goal.Type)
            {
                case GoalType.CreateRank:
                    for (int i = _turnCreated.Count - 1; i >= 0; i--)
                        if (_turnCreated[i].card != null && _turnCreated[i].card.Rank >= goal.Rank)
                            cells.Add((_turnCreated[i].cell, _turnCreated[i].card, null, Color.white));
                    // Ace cũng tính cho TẠO LÁ (≥ X) → quầng vàng từ ô Ace
                    for (int i = _turnAces.Count - 1; i >= 0; i--) cells.Add((_turnAces[i], null, glow, GoldColor));
                    break;
                case GoalType.SuitTrigger:
                    for (int i = _turnSuits.Count - 1; i >= 0; i--)
                        if (_turnSuits[i].suit == goal.Suit) cells.Add((_turnSuits[i].cell, null, SuitSprite(goal.Suit), Color.white));
                    break;
                case GoalType.Ace:
                    for (int i = _turnAces.Count - 1; i >= 0; i--) cells.Add((_turnAces[i], null, glow, GoldColor));
                    break;
                case GoalType.Score:
                    if (!_turnDiamond && _turnCreated.Count > 0) cells.Add((lastAnchor, null, spark, GoldColor));
                    break;
            }
            if (cells.Count == 0) return;
            for (int i = 0; i < want; i++)
            {
                (CellPos pos, Card card, Sprite sprite, Color color) = cells[Mathf.Min(i, cells.Count - 1)];
                if (card == null && sprite == null) continue;
                float size = card != null ? cell * 0.55f : cell * 0.6f;
                into.Add((boardView.CellCenterWorld(pos), card, sprite, color, size));
            }
        }

        private IEnumerator PlayAceSimple(AceCreatedEvent ace)
        {
            CellView cell = boardView.GetCell(ace.Cell);
            if (cell != null) cell.ShowCard(null);
            // Bước 4.5: Figma "ACE!" vàng + "+100,000" trắng, có bóng chữ; FxTheme cũ → chữ cũ
            if (HasFigmaBadges)
            {
                RectTransform badge = MakeAceBadge("+" + Loc.Number(ace.ScoreGained), boardView.CellCenterWorld(ace.Cell));
                StartCoroutine(Float(badge, Scaled(aceDuration) * 2f, popScale: 1.6f, startOffset: 0f));
            }
            else
            {
                string text = "+" + ace.ScoreGained.ToString("N0", CultureInfo.InvariantCulture);
                StartCoroutine(FloatText(text, boardView.CellCenterWorld(ace.Cell), aceTextSize, GoldColor,
                    Scaled(aceDuration) * 2f, popScale: 1.6f, startOffset: 0f));
            }
            yield return Tween.Delay(Scaled(aceDuration));
        }

        private IEnumerator PlayDrawn(CardsDrawnEvent drawn)
        {
            if (drawn.CurrentPiece != null) AudioService.Play(SfxId.Draw); // Animation phần 5
            if (drawn.CurrentPiece == null)
            {
                handView.SetCurrent(drawn.CurrentPiece);
                hudView.SetNext(drawn.NextPiece);
                yield break;
            }

            // Lá UP NEXT bay xuống khung tay và phóng to thành lá hiện tại
            RectTransform next = hudView.NextCardRect;
            Vector3 from = next.TransformPoint(next.rect.center);
            Vector3 to = handView.HomeCardCenterWorld(drawn.CurrentPiece, 0); // lá đơn: tâm khung (kể cả khi lượt trước là khối đôi)
            float handSize = handView.CardSize;
            float startScale = handSize > 0f ? next.rect.width / handSize : 0.4f;

            if (drawn.CurrentPiece.IsPair)
            {
                yield return FlyPairToHand(drawn.CurrentPiece);
                handView.SetCurrent(drawn.CurrentPiece);
                yield return hudView.PopNext(drawn.NextPiece, Scaled(drawDuration));
                yield break;
            }

            CardView flyer = SpawnCard(drawn.CurrentPiece.First, from, handSize, true);
            flyer.transform.localScale = Vector3.one * startScale;
            hudView.SetNext((Piece)null);

            float d = Scaled(drawDuration);
            StartCoroutine(Tween.Scale(flyer.transform, Vector3.one * startScale, Vector3.one, d, Ease.OutCubic));
            yield return Tween.Move(flyer.transform, from, to, d, Ease.OutCubic);

            Despawn(flyer.gameObject);
            handView.SetCurrent(drawn.CurrentPiece);
            yield return hudView.PopNext(drawn.NextPiece, Scaled(drawDuration));
        }

        /// <summary>
        /// M4.5: khối đôi UP NEXT bay xuống tay — 2 lá nhỏ bay cùng lúc tới đúng chỗ của chúng trong khung khối đôi.
        /// Lá ① (lá trái/trên ở UP NEXT) là lá A hay lá B tuỳ hướng khối.
        /// </summary>
        private IEnumerator FlyPairToHand(Piece piece)
        {
            float handSize = handView.CardSizeFor(piece);
            RectTransform next = hudView.NextCardRect;
            float startScale = handSize > 0f ? next.rect.width / handSize : 0.4f;
            float d = Scaled(drawDuration);
            var flyers = new List<CardView>();
            for (int slot = 0; slot < 2; slot++)
            {
                Card card = slot == 0 ? piece.First : piece.Second;
                int handIndex = ReferenceEquals(card, piece.Cards[0]) ? 0 : 1;
                Vector3 from = hudView.NextCardCenterWorld(slot);
                Vector3 to = handView.HomeCardCenterWorld(piece, handIndex);
                CardView flyer = SpawnCard(card, from, handSize, true);
                flyer.transform.localScale = Vector3.one * startScale;
                flyers.Add(flyer);
                StartCoroutine(Tween.Scale(flyer.transform, Vector3.one * startScale, Vector3.one, d, Ease.OutCubic));
                StartCoroutine(Tween.Move(flyer.transform, from, to, d, Ease.OutCubic));
            }
            hudView.SetNext((Piece)null);
            yield return Tween.Delay(d);
            foreach (CardView f in flyers) Despawn(f.gameObject);
        }

        /// <summary>M4.5: lá ① xử lý xong → lá ② nảy nhẹ báo "tới lượt lá ②" rồi mới merge.</summary>
        private IEnumerator PlaySecondResolve(PairSecondResolveEvent second)
        {
            CellView cell = boardView.GetCell(second.Cell);
            if (cell == null || cell.CardView.Card == null) yield break;
            Transform card = cell.CardView.transform;
            float half = Scaled(placeDuration);
            yield return Tween.Scale(card, Vector3.one, Vector3.one * 1.12f, half, Ease.OutCubic);
            yield return Tween.Scale(card, Vector3.one * 1.12f, Vector3.one, half, Ease.OutCubic);
        }

        // ------------------------------------------------------------------ FX chất — khoảnh khắc chung (bước 1.4b)

        private Color SuitColor(Suit suit) => fxTheme != null ? fxTheme.GetColor(suit) : GoldColor;
        private Sprite SuitSprite(Suit suit) => flyingCardPrefab != null && flyingCardPrefab.Theme != null
            ? flyingCardPrefab.Theme.GetSuitSprite(suit) : null;
        /// <summary>Số hạt theo hệ số Inspector; GIẢM BỚT còn một nửa (ít nhất 1 hạt nếu vốn có hạt).</summary>
        private int Particles(int baseCount)
        {
            float amount = particleAmount * (FxSettings.Reduced ? FxSettings.ReducedParticles : 1f);
            int count = Mathf.RoundToInt(baseCount * amount);
            return count == 0 && FxSettings.Reduced && baseCount > 0 && particleAmount > 0f ? 1 : count;
        }
        private float CellSize(CellPos p)
        {
            CellView c = boardView.GetCell(p);
            return c != null ? c.RectTransform.rect.width : 84f;
        }

        /// <summary>
        /// Khoảnh khắc kích hoạt (mọi chất): âm "đóng dấu" → khựng hình → chớp màu khung bàn → vòng sóng
        /// → icon chất to đóng dấu phía trên lá → rung nhẹ. <paramref name="shakeAmount"/> = 0 để tự rung sau (♠).
        /// </summary>
        private IEnumerator PlayActivation(Suit suit, CellPos cell, float shakeAmount)
        {
            Color color = SuitColor(suit);
            Vector3 center = boardView.CellCenterWorld(cell);
            float size = CellSize(cell);
            if (fxTheme != null) Sfx.Play(fxTheme.stamp, fxTheme.volume);

            if (hitStop) yield return HitStop();

            BoardFlash(color);
            if (fxTheme != null)
                StartCoroutine(Fx.Ring(fxTheme.ring, center, color, size, size * 3.2f, Scaled(ringDuration)));
            StartCoroutine(Stamp(suit, center, size, color));
            if (screenShake && shakeAmount > 0f) Shaker.Shake(shakeAmount, Scaled(shakeDuration));
            yield return Tween.Delay(Scaled(0.12f));
        }

        private IEnumerator HitStop(float duration = -1f)
        {
            if (duration < 0f) duration = hitStopDuration;
            if (_hitStopActive || duration <= 0f || FxSettings.Reduced) yield break; // GIẢM BỚT: không khựng hình
            _hitStopActive = true;
            _timeScaleBeforeHitStop = Time.timeScale;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(duration);
            EndHitStop();
        }

        private void EndHitStop()
        {
            if (!_hitStopActive) return;
            _hitStopActive = false;
            Time.timeScale = _timeScaleBeforeHitStop > 0f ? _timeScaleBeforeHitStop : 1f;
        }

        /// <summary>Lớp màu bo góc phủ đúng khung bàn, hiện rồi mờ dần.</summary>
        private void BoardFlash(Color color)
        {
            if (fxTheme == null || fxTheme.flash == null || boardFlashAlpha <= 0f) return;
            var board = (RectTransform)boardView.transform;
            Image img = Fx.Spawn(fxTheme.flash, board.TransformPoint(board.rect.center), 0f, color, "BoardFlash");
            img.type = Image.Type.Sliced;
            img.rectTransform.sizeDelta = board.rect.size;
            Color c = color;
            c.a = boardFlashAlpha * (FxSettings.Reduced ? FxSettings.ReducedFlash : 1f); // GIẢM BỚT: chớp 1/3
            StartCoroutine(Fx.FlashImage(img, c, Scaled(boardFlashDuration)));
        }

        /// <summary>Icon chất to (có quầng màu chất phía sau) bật 0 → 1.35 → 1 phía trên lá, rồi bay lên mờ dần.</summary>
        private IEnumerator Stamp(Suit suit, Vector3 center, float cellSize, Color color)
        {
            Sprite icon = SuitSprite(suit);
            if (icon == null) yield break;
            var offset = new Vector2(0f, StampOffsetY(cellSize)); // nằm hẳn phía trên lá, không che lá kết quả

            Image glow = null;
            if (fxTheme != null && fxTheme.glow != null)
            {
                glow = Fx.Spawn(fxTheme.glow, center, stampSize * 1.9f, color, "StampGlow");
                glow.rectTransform.anchoredPosition += offset;
            }
            Image img = Fx.Spawn(icon, center, stampSize, Color.white, "Stamp");
            img.rectTransform.anchoredPosition += offset;
            Vector2 start = img.rectTransform.anchoredPosition;

            yield return Tween.Run(Scaled(stampDuration), k =>
            {
                if (img == null) return;
                float scale = k < 0.25f ? Mathf.Lerp(0f, 1.35f, Ease.OutCubic(k / 0.25f))
                            : k < 0.4f ? Mathf.Lerp(1.35f, 1f, (k - 0.25f) / 0.15f)
                            : 1f;
                float rise = k < 0.5f ? 0f : Ease.OutCubic((k - 0.5f) / 0.5f) * 40f;
                float alpha = k < 0.55f ? 1f : 1f - (k - 0.55f) / 0.45f;
                img.rectTransform.localScale = Vector3.one * scale;
                img.rectTransform.anchoredPosition = start + new Vector2(0f, rise);
                img.color = new Color(1f, 1f, 1f, alpha);
                if (glow != null)
                {
                    glow.rectTransform.localScale = Vector3.one * scale;
                    glow.rectTransform.anchoredPosition = start + new Vector2(0f, rise);
                    Color g = color;
                    g.a = alpha * 0.85f;
                    glow.color = g;
                }
            });
            if (glow != null) Fx.Despawn(glow.gameObject);
            if (img != null) Fx.Despawn(img.gameObject);
        }

        /// <summary>Tâm icon đóng dấu cách tâm lá: nửa lá + nửa icon + 6.</summary>
        private float StampOffsetY(float cellSize) => cellSize * 0.5f + stampSize * 0.5f + 6f;

        private UiParticles.BurstSettings Spray(int count, float sizeMin, float sizeMax, float speedMin, float speedMax,
            float gravity, float lifetime, float direction = 90f, float spread = 360f, float spin = 360f) =>
            new UiParticles.BurstSettings
            {
                Count = Particles(count), SizeMin = sizeMin, SizeMax = sizeMax, SpeedMin = speedMin, SpeedMax = speedMax,
                Gravity = gravity, Lifetime = Scaled(lifetime), Direction = direction, Spread = spread, Spin = spin
            };

        private void Remember(RectTransform rt)
        {
            if (rt != null && !_moved.ContainsKey(rt)) _moved[rt] = (rt.anchoredPosition, rt.localRotation, rt.localScale);
        }

        private void RestoreMoved()
        {
            foreach (var pair in _moved)
            {
                if (pair.Key == null) continue;
                pair.Key.anchoredPosition = pair.Value.pos;
                pair.Key.localRotation = pair.Value.rot;
                pair.Key.localScale = pair.Value.scale;
            }
            _moved.Clear();
        }

        // ------------------------------------------------------------------ ♦ Mưa kim cương

        /// <summary>
        /// Hạt ♦ + tia lấp lánh bung ra; chữ điểm vàng to đếm từ điểm thường lên điểm ×3,
        /// rồi bay cong vào ô điểm HUD (ô điểm nảy lên).
        /// </summary>
        private IEnumerator PlayDiamond(DiamondBonusEvent e, int score)
        {
            Color color = SuitColor(Suit.Diamond);
            Vector3 center = boardView.CellCenterWorld(e.Cell);
            yield return PlayActivation(Suit.Diamond, e.Cell, shakeLight);
            SuitBadge(fxTheme != null ? fxTheme.badgeDiamond : null, Loc.Format("fx.diamond", e.Multiplier), e.Cell, diamondBadgeTextSize);
            if (fxTheme != null) Sfx.Play(fxTheme.diamond, fxTheme.volume);

            Fx.Burst(SuitSprite(Suit.Diamond), center, Color.white, Spray(14, 18f, 30f, 260f, 480f, 700f, 0.75f));
            if (fxTheme != null) Fx.Burst(fxTheme.spark, center, color, Spray(10, 16f, 32f, 180f, 380f, 150f, 0.6f));

            int baseScore = Mathf.RoundToInt(score / (float)Mathf.Max(1, e.Multiplier));
            (RectTransform rt, TextMeshProUGUI tmp) = MakeText("+" + baseScore, center, diamondTextSize, GoldColor);

            // Đếm điểm thường → điểm ×3, kèm nảy
            yield return Tween.Run(Scaled(diamondCountDuration), k =>
            {
                if (tmp == null) return;
                tmp.text = "+" + Mathf.RoundToInt(Mathf.Lerp(baseScore, score, k));
                rt.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Ease.OutCubic(k));
            });
            if (tmp != null) tmp.text = "+" + score;
            yield return Tween.Delay(Scaled(0.08f));

            // Bay cong vào ô điểm (Endless) / mục tiêu điểm (Level); không có chỗ nào → mờ dần tại chỗ
            Vector3 from = rt != null ? rt.position : center;
            Vector3? target = hudView.ScoreVisible ? hudView.ScoreWorldCenter : levelHud != null ? levelHud.ScoreGoalWorld : null;
            if (target == null)
            {
                if (rt != null) yield return Float(rt, Scaled(0.35f), popScale: 1f, startOffset: 0f);
                yield break;
            }
            Vector3 to = target.Value;
            Vector3 side = Vector3.Cross(to - from, Vector3.forward).normalized * Vector3.Distance(from, to) * 0.25f;
            yield return Tween.Run(Scaled(diamondFlyDuration), k =>
            {
                if (rt == null) return;
                float arc = Mathf.Sin(k * Mathf.PI);
                rt.position = Vector3.Lerp(from, to, k) + side * arc;
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
            }, Ease.InCubic);
            if (rt != null) Despawn(rt.gameObject);

            if (hudView.ScoreVisible) hudView.BumpScore(1.3f, Scaled(0.35f));
            if (fxTheme != null) Fx.Burst(fxTheme.spark, to, color, Spray(6, 14f, 24f, 120f, 240f, 0f, 0.4f));
        }

        // ------------------------------------------------------------------ ♥ Nhảy rank

        /// <summary>
        /// Lá bật nhảy theo đường cong ở mỗi nấc rank (số đổi ở đỉnh cú nhảy) + hạt ♥ trôi lên;
        /// cú cuối phóng to kèm quầng hồng; chữ "+N" vàng to cạnh icon; vượt K → "+500" + hạt vàng.
        /// </summary>
        private IEnumerator PlayHeartLeap(HeartLeapEvent heart)
        {
            Color color = SuitColor(Suit.Heart);
            Vector3 center = boardView.CellCenterWorld(heart.Cell);
            float size = CellSize(heart.Cell);
            yield return PlayActivation(Suit.Heart, heart.Cell, shakeLight);
            if (fxTheme != null && fxTheme.badgeHeart != null)
                SuitBadge(fxTheme.badgeHeart, Loc.Format("fx.heart", heart.Leap), heart.Cell, badgeTextSize);
            else
                StartCoroutine(FloatText("+" + heart.Leap, center, leapTextSize, GoldColor, Scaled(stampDuration + 0.2f),
                    popScale: 1.6f, startOffset: StampOffsetY(size), startOffsetX: stampSize * 0.62f));

            CellView cell = boardView.GetCell(heart.Cell);
            if (cell != null && cell.CardView.Card != null)
            {
                var card = (RectTransform)cell.CardView.transform;
                Remember(card);
                Vector2 basePos = _moved[card].pos;
                Suit suit = cell.CardView.Card.Suit;
                Rank rank = cell.CardView.Card.Rank;
                int jumps = Mathf.Max(1, heart.ToRank.Value() - rank.Value()); // ít nhất 1 cú nhảy ăn mừng
                for (int j = 0; j < jumps; j++)
                {
                    bool changes = rank < heart.ToRank;
                    Rank nextRank = changes ? rank.Next() : rank;
                    float tilt = j % 2 == 0 ? 8f : -8f;
                    bool swapped = false;
                    if (fxTheme != null) Sfx.Play(fxTheme.heartStep, fxTheme.volume, 1f + 0.12f * j);
                    Fx.Burst(SuitSprite(Suit.Heart), center, Color.white, Spray(3, 14f, 22f, 60f, 130f, -160f, 0.6f, 90f, 80f, 90f));
                    yield return Tween.Run(Scaled(heartJumpDuration), k =>
                    {
                        if (card == null) return;
                        float arc = Mathf.Sin(k * Mathf.PI);
                        card.anchoredPosition = basePos + new Vector2(0f, arc * heartJumpHeight);
                        card.localRotation = Quaternion.Euler(0f, 0f, arc * tilt);
                        if (!swapped && k >= 0.5f && changes)
                        {
                            swapped = true;
                            cell.CardView.SetCard(new Card(nextRank, suit)); // SetCard không đụng vị trí / cỡ
                        }
                    });
                    rank = nextRank;
                }
                card.anchoredPosition = basePos;
                card.localRotation = Quaternion.identity;

                // Cú cuối: phóng to + quầng hồng
                if (fxTheme != null && fxTheme.glow != null)
                {
                    Color g = color;
                    g.a = 0.6f;
                    StartCoroutine(Fx.FlashImage(Fx.Spawn(fxTheme.glow, center, size * 2.4f, g, "HeartGlow"), g, Scaled(0.35f)));
                }
                yield return Tween.Scale(card, Vector3.one * 1.3f, Vector3.one, Scaled(0.22f), Ease.OutBack);
                RestoreMoved();
            }

            if (heart.OverflowBonus > 0)
            {
                StartCoroutine(FloatText("+" + heart.OverflowBonus, center, scoreTextSize, GoldColor,
                    Scaled(scoreTextDuration), popScale: 1.4f, startOffset: -50f));
                if (fxTheme != null) Fx.Burst(fxTheme.spark, center, GoldColor, Spray(10, 16f, 28f, 200f, 380f, 300f, 0.6f));
            }
        }

        // ------------------------------------------------------------------ ♣ Nhân bản

        /// <summary>
        /// Lá kết quả rung lắc → tách đôi: bản sao trượt sang ô sinh lá kéo theo 3 bóng mờ + hạt ♣ rơi dọc đường;
        /// ô đích có vòng sóng xanh; cả 2 lá nảy cùng lúc. Bàn đầy → chữ điểm bù.
        /// </summary>
        private IEnumerator PlayClubSpawn(ClubSpawnEvent club)
        {
            Color color = SuitColor(Suit.Club);
            yield return PlayActivation(Suit.Club, club.Origin, shakeLight);
            if (fxTheme != null) Sfx.Play(fxTheme.club, fxTheme.volume);

            if (!club.SpawnCell.HasValue)
            {
                StartCoroutine(FloatText("+" + club.FallbackBonus, boardView.CellCenterWorld(club.Origin),
                    scoreTextSize, GoldColor, Scaled(scoreTextDuration), popScale: 1.4f, startOffset: -50f));
                yield break;
            }

            SuitBadge(fxTheme != null ? fxTheme.badgeClub : null, Loc.Get("fx.club"), club.Origin, badgeTextSize);
            CellView target = boardView.GetCell(club.SpawnCell.Value);
            CellView origin = boardView.GetCell(club.Origin);
            if (target == null || origin == null) yield break;
            SetZone(target, CellZone.Spawn);

            // Rung lắc lá gốc
            var originCard = (RectTransform)origin.CardView.transform;
            Remember(originCard);
            yield return Tween.Run(Scaled(clubWobbleDuration), k =>
            {
                if (originCard == null) return;
                originCard.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI * 4f) * 10f * (1f - k));
                originCard.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(k * Mathf.PI));
            });
            RestoreMoved();

            // Bản sao + 3 bóng mờ (bóng sinh trước → nằm dưới bản sao)
            float size = origin.RectTransform.rect.width;
            Vector3 from = origin.CenterWorld, to = target.CenterWorld;
            var ghosts = new List<CanvasGroup>();
            float[] ghostAlpha = { 0.45f, 0.28f, 0.14f };
            for (int g = ghostAlpha.Length - 1; g >= 0; g--)
            {
                CardView ghost = SpawnCard(club.Card, from, size, false);
                var group = ghost.gameObject.AddComponent<CanvasGroup>();
                group.alpha = ghostAlpha[g];
                ghosts.Insert(0, group);
            }
            CardView flyer = SpawnCard(club.Card, from, size, false);

            Sprite clubIcon = SuitSprite(Suit.Club);
            float dropTimer = 0f;
            yield return Tween.Run(Scaled(clubFlyDuration), k =>
            {
                float e = Ease.OutCubic(k);
                if (flyer != null) flyer.transform.position = Vector3.LerpUnclamped(from, to, e);
                for (int g = 0; g < ghosts.Count; g++)
                    if (ghosts[g] != null)
                        ghosts[g].transform.position = Vector3.LerpUnclamped(from, to, Ease.OutCubic(Mathf.Max(0f, k - 0.12f * (g + 1))));
                dropTimer += Time.deltaTime;
                if (dropTimer >= 0.04f && flyer != null)
                {
                    dropTimer = 0f;
                    Fx.Burst(clubIcon, flyer.transform.position, Color.white, Spray(1, 12f, 18f, 30f, 80f, 500f, 0.5f, 270f, 120f));
                }
            });
            Despawn(flyer.gameObject);
            foreach (CanvasGroup g in ghosts)
                if (g != null) Despawn(g.gameObject);

            target.ShowCard(club.Card);
            ClearZone(target);
            if (fxTheme != null)
                StartCoroutine(Fx.Ring(fxTheme.ring, to, color, size, size * 2.4f, Scaled(ringDuration * 0.8f)));
            Fx.Burst(clubIcon, to, Color.white, Spray(8, 14f, 24f, 160f, 320f, 500f, 0.6f));

            // Cả 2 lá nảy cùng lúc
            StartCoroutine(Tween.Scale(origin.CardView.transform, Vector3.one * 1.25f, Vector3.one, Scaled(createPopDuration), Ease.OutBack));
            yield return Tween.Scale(target.CardView.transform, Vector3.one * 0.6f, Vector3.one, Scaled(createPopDuration), Ease.OutBack);
        }

        // ------------------------------------------------------------------ ♠ Nổ quét

        /// <summary>
        /// Lá kết quả tụ lực (rung + quầng đỏ lớn dần) → vòng sóng đỏ lan khắp 3×3 + rung mạnh →
        /// các lá bị dọn văng ra xa tâm, vừa xoay vừa mờ; mỗi lá bật điểm nhỏ; chữ tổng đỏ to.
        /// </summary>
        private IEnumerator PlaySpadeClear(SpadeClearEvent spade)
        {
            Color color = SuitColor(Suit.Spade);
            Vector3 center = boardView.CellCenterWorld(spade.Center);
            float size = CellSize(spade.Center);
            yield return PlayActivation(Suit.Spade, spade.Center, 0f); // rung mạnh lúc nổ
            SuitBadge(fxTheme != null ? fxTheme.badgeSpade : null, Loc.Get("fx.spade"), spade.Center, badgeTextSize);

            var area = new List<CellView>();
            foreach (CellPos p in spade.Area)
            {
                CellView c = boardView.GetCell(p);
                if (c == null) continue;
                SetZone(c, CellZone.Danger);
                area.Add(c);
            }

            // Tụ lực
            CellView centerCell = boardView.GetCell(spade.Center);
            RectTransform centerCard = centerCell != null && centerCell.CardView.Card != null
                ? (RectTransform)centerCell.CardView.transform : null;
            Remember(centerCard);
            Vector2 basePos = centerCard != null ? _moved[centerCard].pos : Vector2.zero;
            Image charge = fxTheme != null && fxTheme.glow != null ? Fx.Spawn(fxTheme.glow, center, size, color, "SpadeCharge") : null;
            yield return Tween.Run(Scaled(spadeChargeDuration), k =>
            {
                if (centerCard != null) centerCard.anchoredPosition = basePos + Random.insideUnitCircle * 3f * k;
                if (charge != null)
                {
                    float s = Mathf.Lerp(size * 1.2f, size * 2.4f, k);
                    charge.rectTransform.sizeDelta = new Vector2(s, s);
                    Color c = color;
                    c.a = Mathf.Lerp(0.3f, 0.75f, k);
                    charge.color = c;
                }
            });
            RestoreMoved();

            // Nổ
            if (fxTheme != null)
            {
                Sfx.Play(fxTheme.spade, fxTheme.volume);
                StartCoroutine(Fx.Ring(fxTheme.ring, center, color, size, size * 4.4f, Scaled(spadeBlastDuration * 0.8f)));
                Fx.Burst(fxTheme.spark, center, color, Spray(12, 16f, 30f, 300f, 520f, 200f, 0.6f));
            }
            if (charge != null)
            {
                Color c = color;
                c.a = 0.75f;
                StartCoroutine(Fx.FlashImage(charge, c, Scaled(0.25f)));
            }
            Fx.Burst(SuitSprite(Suit.Spade), center, Color.white, Spray(10, 16f, 26f, 300f, 520f, 600f, 0.65f));
            if (screenShake) Shaker.Shake(shakeStrong, Scaled(shakeDuration * 1.2f));

            float flyTime = Scaled(spadeBlastDuration);
            Vector3 centerLocal = fxLayer.InverseTransformPoint(center);
            foreach (CellPos p in spade.ClearedCells)
            {
                CellView cell = boardView.GetCell(p);
                if (cell == null || cell.CardView.Card == null) continue;
                Card card = cell.CardView.Card;
                CardView flyer = SpawnCard(card, cell.CenterWorld, cell.RectTransform.rect.width, false);
                cell.ShowCard(null);
                Vector3 dir = fxLayer.InverseTransformPoint(cell.CenterWorld) - centerLocal;
                StartCoroutine(FlyAway(flyer, dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.up, flyTime));
                StartCoroutine(FloatText("+" + SpadePoints(card), cell.CenterWorld, scoreTextSize * 0.75f, SpadeTextColor,
                    Scaled(scoreTextDuration * 0.8f), popScale: 1.3f, startOffset: 0f));
            }

            yield return Tween.Delay(Scaled(0.12f));
            if (spade.Bonus > 0)
                StartCoroutine(FloatText("+" + spade.Bonus, center, scoreTextSize * 1.25f, SpadeTextColor,
                    Scaled(scoreTextDuration), popScale: 1.5f, startOffset: -50f));

            yield return Tween.Run(Mathf.Max(0f, flyTime - Scaled(0.12f)), k =>
            {
                foreach (CellView c in area) c.SetZoneAlpha(1f - k);
            });
            foreach (CellView c in area) ClearZone(c);
        }

        /// <summary>Điểm ♠ của 1 lá chỉ để hiển thị (Core đã cộng đúng tổng): rank × 10.</summary>
        private static int SpadePoints(Card card) => card.IsJoker ? 0 : card.Rank.Value() * 10;

        private IEnumerator FlyAway(CardView flyer, Vector3 dir, float duration)
        {
            var rt = (RectTransform)flyer.transform;
            var group = flyer.gameObject.AddComponent<CanvasGroup>();
            Vector3 start = rt.localPosition;
            Vector3 end = start + dir * spadeFlyDistance;
            float spin = Random.Range(120f, 220f) * (Random.value < 0.5f ? -1f : 1f);
            yield return Tween.Run(duration, k =>
            {
                if (rt == null) return;
                rt.localPosition = Vector3.LerpUnclamped(start, end, Ease.OutCubic(k));
                rt.localRotation = Quaternion.Euler(0f, 0f, spin * k);
                rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, k);
                group.alpha = k < 0.3f ? 1f : 1f - (k - 0.3f) / 0.7f;
            });
            Despawn(flyer.gameObject);
        }

        private void SetZone(CellView cell, CellZone zone)
        {
            cell.SetZone(zone, pulse: false);
            if (!_zoned.Contains(cell)) _zoned.Add(cell);
        }

        private void ClearZone(CellView cell)
        {
            cell.SetZone(CellZone.None);
            _zoned.Remove(cell);
        }

        // ------------------------------------------------------------------ Lá bay & chữ nổi

        private CardView SpawnCard(Card card, Vector3 worldCenter, float size, bool shadow)
        {
            CardView view = Instantiate(flyingCardPrefab, fxLayer);
            var rt = (RectTransform)view.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.position = worldCenter;
            view.SetShadowVisible(shadow);
            view.SetRaycastTarget(false);
            view.SetCard(card);
            _spawned.Add(view.gameObject);
            return view;
        }

        private IEnumerator FloatText(string text, Vector3 worldCenter, float size, Color color, float duration,
            float popScale, float startOffset, float startOffsetX = 0f)
        {
            (RectTransform rt, TextMeshProUGUI _) = MakeText(text, worldCenter, size, color);
            yield return Float(rt, duration, popScale, startOffset, startOffsetX);
        }

        /// <summary>Nảy (popScale → 1) rồi bay lên mờ dần, xong thì huỷ. Dùng cho chữ nổi và badge.</summary>
        private IEnumerator Float(RectTransform rt, float duration, float popScale, float startOffset, float startOffsetX = 0f)
        {
            if (rt == null) yield break;
            rt.anchoredPosition += new Vector2(startOffsetX, startOffset);
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            Vector2 start = rt.anchoredPosition;
            Vector2 end = start + new Vector2(0f, floatDistance);
            yield return Tween.Run(duration, k =>
            {
                if (rt == null) return;
                rt.anchoredPosition = Vector2.LerpUnclamped(start, end, Ease.OutCubic(k));
                float pop = popScale > 1f && k < 0.25f ? Mathf.Lerp(popScale, 1f, Ease.OutCubic(k / 0.25f)) : 1f;
                rt.localScale = Vector3.one * pop;
                group.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            });
            if (rt != null) Despawn(rt.gameObject);
        }

        /// <summary>Chữ nổi (font Baloo 2, viền tím đậm) trên lớp Fx, tâm tại <paramref name="worldCenter"/>.</summary>
        private (RectTransform, TextMeshProUGUI) MakeText(string text, Vector3 worldCenter, float size, Color color)
        {
            var go = new GameObject("FloatText", typeof(RectTransform));
            go.layer = fxLayer.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(fxLayer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(300f, size * 1.6f);
            rt.position = worldCenter;
            _spawned.Add(go);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = TextShadowColor;
            return (rt, tmp);
        }

        // ------------------------------------------------------------------ Badge Figma (bước 4.5)

        /// <summary>FxTheme đã có ảnh badge Figma (menu Cập nhật scene Game đã chạy).</summary>
        private bool HasFigmaBadges => fxTheme != null && fxTheme.badgeHeart != null;

        /// <summary>
        /// Badge chất (nền pill Figma + chữ trắng) nằm ngang hàng icon đóng dấu, bên phải icon; nảy rồi bay lên mờ dần.
        /// Chưa có ảnh nền (FxTheme cũ) → bỏ qua.
        /// </summary>
        private void SuitBadge(Sprite background, string text, CellPos cell, float textSize)
        {
            if (background == null) return;
            Vector3 center = boardView.CellCenterWorld(cell);
            RectTransform rt = MakeBadge(background, text, center, textSize);
            float offsetX = stampSize * 0.5f + rt.sizeDelta.x * 0.5f - 10f;
            rt.anchoredPosition += new Vector2(offsetX, StampOffsetY(CellSize(cell)));
            ClampToFrame(rt);
            StartCoroutine(Float(rt, Scaled(badgeDuration), popScale: 1.35f, startOffset: 0f));
        }

        /// <summary>Nền pill 9-slice (cao theo ảnh) + chữ trắng Baloo 2; rộng theo chữ.</summary>
        private RectTransform MakeBadge(Sprite background, string text, Vector3 worldCenter, float textSize)
        {
            var go = new GameObject("Badge", typeof(RectTransform));
            go.layer = fxLayer.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(fxLayer, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = worldCenter;
            _spawned.Add(go);

            var img = go.AddComponent<Image>();
            img.sprite = background;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            img.SetNativeSize();
            float height = rt.sizeDelta.y;
            const float shadow = 6f; // bóng 5px + lề trong ảnh

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.layer = go.layer;
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(0f, shadow);
            trt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = textSize;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;

            // Figma: lề ngang 15.4 (18.7 với ×3) + viền 4
            float padding = textSize >= diamondBadgeTextSize ? 18.7f : 15.4f;
            float width = Mathf.Max(height, tmp.GetPreferredValues(text).x + 2f * (padding + 4f));
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        /// <summary>Chữ to có bóng đặc lệch xuống (Figma text-shadow 0 · 1/12 cỡ chữ · #290f38).</summary>
        private RectTransform MakeShadowText(string text, Vector3 worldCenter, float size, Color color)
        {
            (RectTransform shadowRt, TextMeshProUGUI shadow) = MakeText(text, worldCenter, size, BadgeShadowColor);
            shadow.outlineWidth = 0f;
            shadowRt.name = "ShadowText";
            (RectTransform mainRt, TextMeshProUGUI main) = MakeText(text, worldCenter, size, color);
            main.outlineWidth = 0f;
            _spawned.Remove(mainRt.gameObject);
            mainRt.SetParent(shadowRt, true);
            mainRt.anchoredPosition = new Vector2(0f, size / 12f); // chữ chính cao hơn bóng
            return shadowRt;
        }

        /// <summary>"ACE!" vàng to + dòng điểm trắng bên dưới, cả hai có bóng chữ.</summary>
        private RectTransform MakeAceBadge(string scoreText, Vector3 worldCenter)
        {
            RectTransform title = MakeShadowText(Loc.Get("fx.ace"), worldCenter, aceBadgeSize, GoldColor);
            RectTransform score = MakeShadowText(scoreText, worldCenter, aceScoreSize, Color.white);
            _spawned.Remove(score.gameObject);
            score.SetParent(title, true);
            score.anchoredPosition = new Vector2(0f, -(aceBadgeSize * 0.6f + aceScoreSize * 0.55f));
            return title;
        }

        /// <summary>Giữ badge nằm trong khung 500 (cell sát mép phải → badge dời vào).</summary>
        private void ClampToFrame(RectTransform rt)
        {
            float half = fxLayer.rect.width * 0.5f;
            float w = rt.sizeDelta.x * 0.5f + 8f;
            Vector2 p = rt.anchoredPosition;
            p.x = Mathf.Clamp(p.x, -half + w, half - w);
            rt.anchoredPosition = p;
        }

        private void Despawn(GameObject go)
        {
            _spawned.Remove(go);
            if (go != null) Destroy(go);
        }
    }
}
