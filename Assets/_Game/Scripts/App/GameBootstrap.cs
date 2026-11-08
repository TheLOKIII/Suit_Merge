using System.Collections;
using System.Collections.Generic;
using SuitMerge.Core.Config;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;
using SuitMerge.View.Animation;
using SuitMerge.View.Board;
using SuitMerge.View.Hand;
using SuitMerge.View.Hud;
using SuitMerge.View.Localization;
using SuitMerge.View.Popups;
using UnityEngine;
using UnityEngine.UI;

namespace SuitMerge.App
{
    /// <summary>
    /// Điều phối màn chơi: nạp config → tạo GameSession → nhận lượt thả từ HandView → gọi Core
    /// → phát animation (EventPlayer) → vẽ lại View từ Core.
    /// Bước 4.5: chơi Endless HOẶC Level (chọn ở bảng chọn level tạm khi mở game); HUD đổi theo chế độ.
    /// KHÔNG chứa luật chơi — mọi luật nằm trong Core (GameSession).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private TextAsset configJson;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HudView hudView;
        [SerializeField] private HandView handView;
        [SerializeField] private GameOverPopup gameOverPopup;
        [Tooltip("Bỏ trống = không có animation (bàn đổi ngay như bước 2.4).")]
        [SerializeField] private EventPlayer eventPlayer;
        [Tooltip("Nút MENU trên HUD → mở popup tạm dừng.")]
        [SerializeField] private Button menuButton;
        [SerializeField] private PausePopup pausePopup;

        [Header("Level (bước 4.5) — bỏ trống = chỉ chơi Endless như trước")]
        [Tooltip("Data/Levels/level_0xx.json theo thứ tự.")]
        [SerializeField] private TextAsset[] levelFiles;
        [SerializeField] private LevelHudView levelHud;
        [SerializeField] private HintPointerView hintPointer;
        [SerializeField] private LevelSelectPopup levelSelectPopup;
        [SerializeField] private LevelCompletePopup levelCompletePopup;
        [SerializeField] private LevelFailPopup levelFailPopup;
        [SerializeField] private UnlockPopup unlockPopup;
        [SerializeField] private SettingsPopup settingsPopup;

        [Header("Khối đôi (M4.5)")]
        [Tooltip("Popup Figma \"HẾT CHỖ!\" khi khối đôi không còn 2 ô trống kề nhau. Bỏ trống = vào thẳng popup thua.")]
        [SerializeField] private PairNoSpacePopup pairNoSpacePopup;

        [Header("Animation phần 4")]
        [Tooltip("Màn đen chuyển màn (chơi lại / level kế / về bảng chọn). Bỏ trống = đổi màn tức thì như trước.")]
        [SerializeField] private ScreenFader screenFader;

        /// <summary>Android mặc định 30 FPS → animation giật; khoá 60 FPS.</summary>
        private const int TargetFrameRate = 60;
        /// <summary>Chest ở level chia hết cho 10 (spec 7): +100 coin.</summary>
        private const int ChestCoins = 100;

        private GameSession _session;
        private LevelFlow _levels;
        private int _levelIndex = -1; // -1 = Endless
        private Coroutine _turnRoutine;
        private Coroutine _introRoutine; // Animation phần 4: màn vào level / ván Endless
        private CellView _hoverCell;
        private readonly Dictionary<CellPos, PlacementPreview> _previewCache = new Dictionary<CellPos, PlacementPreview>();
        private readonly Queue<Suit> _unlockQueue = new Queue<Suit>();

        public GameSession Session => _session;
        private bool IsLevel => _levelIndex >= 0;
        private LevelData CurrentLevel => IsLevel ? _levels[_levelIndex] : null;

        private void Awake()
        {
            Application.targetFrameRate = TargetFrameRate;
            _session = new GameSession(LoadConfig());
            _levels = new LevelFlow(levelFiles);
        }

        private void OnEnable()
        {
            handView.CardDragged += OnCardDragged;
            handView.CardDropped += OnCardDropped;
            handView.CardTapped += OnHandTapped;
            if (pairNoSpacePopup != null) pairNoSpacePopup.GiveUpClicked += OnPairGiveUp;
            gameOverPopup.PlayAgainClicked += OnPlayAgain;
            gameOverPopup.HomeClicked += OnHome;
            if (menuButton != null) menuButton.onClick.AddListener(OpenPause);
            if (pausePopup != null)
            {
                pausePopup.ResumeClicked += ClosePause;
                pausePopup.RestartClicked += RestartFromPause;
                pausePopup.SettingsClicked += OpenSettings;
                pausePopup.HomeClicked += HomeFromPause;
            }
            if (levelSelectPopup != null)
            {
                levelSelectPopup.LevelChosen += OnLevelChosen;
                levelSelectPopup.EndlessChosen += OnPlayAgain;
            }
            if (levelCompletePopup != null) levelCompletePopup.ContinueClicked += NextLevel;
            if (levelFailPopup != null)
            {
                levelFailPopup.RetryClicked += RetryLevel;
                levelFailPopup.GiveUpClicked += OnHome;
            }
            if (unlockPopup != null) unlockPopup.Closed += ShowNextUnlock;
            Loc.Changed += OnLanguageChanged;
        }

        private void OnDisable()
        {
            handView.CardDragged -= OnCardDragged;
            handView.CardDropped -= OnCardDropped;
            handView.CardTapped -= OnHandTapped;
            if (pairNoSpacePopup != null) pairNoSpacePopup.GiveUpClicked -= OnPairGiveUp;
            gameOverPopup.PlayAgainClicked -= OnPlayAgain;
            gameOverPopup.HomeClicked -= OnHome;
            if (menuButton != null) menuButton.onClick.RemoveListener(OpenPause);
            if (pausePopup != null)
            {
                pausePopup.ResumeClicked -= ClosePause;
                pausePopup.RestartClicked -= RestartFromPause;
                pausePopup.SettingsClicked -= OpenSettings;
                pausePopup.HomeClicked -= HomeFromPause;
            }
            if (levelSelectPopup != null)
            {
                levelSelectPopup.LevelChosen -= OnLevelChosen;
                levelSelectPopup.EndlessChosen -= OnPlayAgain;
            }
            if (levelCompletePopup != null) levelCompletePopup.ContinueClicked -= NextLevel;
            if (levelFailPopup != null)
            {
                levelFailPopup.RetryClicked -= RetryLevel;
                levelFailPopup.GiveUpClicked -= OnHome;
            }
            if (unlockPopup != null) unlockPopup.Closed -= ShowNextUnlock;
            Loc.Changed -= OnLanguageChanged;
            if (screenFader != null) screenFader.Cancel(); // không để màn đen kẹt lại
        }

        private void Start()
        {
            hudView.SetCoins(SaveData.Coins);
            AudioService.PlayMusic(); // Animation phần 5: nhạc nền (Cài đặt NHẠC: TẮT → im)
            // Bước 4.5: mở game → bảng chọn level (tạm, tới M7 có màn Home). Scene chưa cập nhật → vào Endless như cũ.
            if (levelSelectPopup != null && _levels.Count > 0) ShowLevelSelect();
            else StartNewGame();
        }

        // ------------------------------------------------------------------ Endless

        /// <summary>Ván Endless mới với seed ngẫu nhiên (ghi ra Console để tái hiện khi có lỗi).</summary>
        public void StartNewGame()
        {
            StopTurn();
            _levelIndex = -1;

            var seed = (uint)System.Environment.TickCount;
            _session.StartEndless(seed);
            Debug.Log($"[Suit Merge] Ván Endless mới — seed {seed}");

            HideAllPopups();
            SetLevelMode(false);
            handView.Interactable = true;
            ClearDragFeedback();
            RefreshAll();
            StartIntro(level: false);
        }

        // ------------------------------------------------------------------ Chuyển màn + màn vào level (Animation phần 4)

        /// <summary>Đổi màn qua màn đen (tối dần → đổi → sáng lại). Chưa có màn đen / đang chuyển → đổi ngay / bỏ qua.</summary>
        private void Transition(System.Action change)
        {
            if (screenFader == null || !isActiveAndEnabled)
            {
                change();
                return;
            }
            if (screenFader.IsRunning) return;
            handView.Interactable = false;
            ClearDragFeedback();
            screenFader.Run(change, this);
        }

        private void OnPlayAgain() => Transition(StartNewGame);

        private void OnHome() => Transition(ShowLevelSelect);

        /// <summary>
        /// Màn vào level / ván Endless: khoá tay → EventPlayer diễn (chạm để bỏ qua) → MỞ KHÓA (nếu có) → mở tay + gợi ý.
        /// </summary>
        private void StartIntro(bool level)
        {
            if (_introRoutine != null) StopCoroutine(_introRoutine);
            _introRoutine = null;
            if (eventPlayer == null)
            {
                ShowNextUnlock();
                return;
            }
            _introRoutine = StartCoroutine(IntroRoutine(level));
        }

        private IEnumerator IntroRoutine(bool level)
        {
            handView.Interactable = false;
            if (hintPointer != null) hintPointer.Hide();
            boardView.SetHints(null);
            yield return eventPlayer.PlayIntro(level);
            _introRoutine = null;
            handView.Interactable = _session.Status == GameStatus.Playing;
            UpdateHint();
            ShowNextUnlock();
        }

        // ------------------------------------------------------------------ Level (bước 4.5)

        private void ShowLevelSelect()
        {
            StopTurn();
            HideAllPopups();
            handView.Interactable = false;
            ClearDragFeedback();
            if (hintPointer != null) hintPointer.Hide();
            if (!_session.IsStarted)
            {
                // Mở game lần đầu: chưa có ván → tay / UP NEXT trống, HUD Level ẩn
                handView.SetCurrent((Piece)null);
                hudView.SetNext((Piece)null);
                SetLevelMode(false);
            }
            if (levelSelectPopup != null) levelSelectPopup.Show(_levels.Ids);
            else StartNewGame();
        }

        private void OnLevelChosen(int index) => Transition(() => StartLevel(index, retry: false));

        /// <summary>Bắt đầu level thứ <paramref name="index"/>. <paramref name="retry"/> = chơi lại → không hiện lại MỞ KHÓA!.</summary>
        private void StartLevel(int index, bool retry)
        {
            StopTurn();
            LevelData level = _levels[index];
            try
            {
                _session.StartLevel(level);
            }
            catch (System.NotSupportedException e)
            {
                Debug.LogError($"[Suit Merge] Level {level.Id} chưa chơi được: {e.Message}");
                ShowLevelSelect();
                return;
            }
            _levelIndex = index;
            Debug.Log($"[Suit Merge] Level {level.Id}{(retry ? " (chơi lại)" : "")} — {LevelLoader.Summary(level)}");

            HideAllPopups();
            SetLevelMode(true);
            levelHud.Show(level.Id, _session.Goals, _session.CardsLeftInDeck ?? 0);
            handView.Interactable = true;
            ClearDragFeedback();
            RefreshAll();

            _unlockQueue.Clear();
            if (!retry)
                foreach (Suit s in _levels.NewSuits(index)) _unlockQueue.Enqueue(s);
            StartIntro(level: true); // Animation phần 4: MỞ KHÓA hiện SAU màn vào level
        }

        private void ShowNextUnlock()
        {
            if (unlockPopup == null || _unlockQueue.Count == 0) return;
            unlockPopup.Show(_unlockQueue.Dequeue());
        }

        private void RetryLevel()
        {
            if (IsLevel) Transition(() => StartLevel(_levelIndex, retry: true));
        }

        private void NextLevel()
        {
            Transition(() =>
            {
                if (IsLevel && _levels.HasNext(_levelIndex)) StartLevel(_levelIndex + 1, retry: false);
                else ShowLevelSelect();
            });
        }

        private void SetLevelMode(bool level)
        {
            hudView.SetEndlessElementsVisible(!level);
            if (levelHud != null) levelHud.SetVisible(level);
            if (!level)
            {
                boardView.SetHints(null);
                if (hintPointer != null)
                {
                    hintPointer.Hide();
                    hintPointer.SetCaption(null);
                }
            }
        }

        // ------------------------------------------------------------------ Tạm dừng (MENU)

        /// <summary>Mở popup tạm dừng. Bỏ qua khi đang phát animation một lượt hoặc ván đã kết thúc.</summary>
        private void OpenPause()
        {
            if (pausePopup == null || pausePopup.IsShown || !_session.IsStarted) return;
            if (_turnRoutine != null || _introRoutine != null || _session.Status != GameStatus.Playing) return;
            if (levelSelectPopup != null && levelSelectPopup.IsShown) return;
            if (screenFader != null && screenFader.IsRunning) return;
            handView.Interactable = false;
            ClearDragFeedback();
            if (hintPointer != null) hintPointer.Hide();
            pausePopup.Show(_session.Score, Mathf.Max(SaveData.EndlessBest, _session.Score));
        }

        private void ClosePause()
        {
            pausePopup.Hide();
            if (settingsPopup != null) settingsPopup.Hide();
            handView.Interactable = _session.Status == GameStatus.Playing;
            UpdateHint();
        }

        /// <summary>Chơi lại từ popup tạm dừng: điểm ván Endless đang chơi VẪN được tính vào kỷ lục (quyết định bước 2.6).</summary>
        private void RestartFromPause()
        {
            if (IsLevel)
            {
                Transition(() => StartLevel(_levelIndex, retry: true));
                return;
            }
            SubmitEndlessScore("Chơi lại từ MENU");
            Transition(StartNewGame);
        }

        /// <summary>Về màn chính (tạm = bảng chọn level). Endless: điểm ván đang chơi vẫn tính kỷ lục như Chơi lại.</summary>
        private void HomeFromPause()
        {
            if (!IsLevel) SubmitEndlessScore("Về màn chính từ MENU");
            Transition(ShowLevelSelect);
        }

        private void OpenSettings()
        {
            if (settingsPopup != null) settingsPopup.Show();
        }

        private void SubmitEndlessScore(string reason)
        {
            int score = _session.Score;
            bool isNewBest = SaveData.SubmitEndlessScore(score);
            Debug.Log($"[Suit Merge] {reason} — điểm {score}, kỷ lục {SaveData.EndlessBest}{(isNewBest ? " (mới!)" : "")}, seed {_session.Seed}");
        }

        private void OnLanguageChanged()
        {
            hudView.RefreshLanguage();
            if (!IsLevel || !_session.IsStarted) return;
            levelHud.RefreshLanguage();
            levelHud.Sync(_session.Goals, _session.CardsLeftInDeck ?? 0);
            UpdateHint();
        }

        private void RefreshAll()
        {
            _previewCache.Clear();
            boardView.Render(_session.Board);
            boardView.SetDangerFor(_session.Board, _session.Status == GameStatus.Playing); // Animation phần 3: bàn sắp đầy
            handView.DragTargetSize = boardView.CellSize; // lá kéo co đúng cỡ ô của bàn này
            hudView.SetScore(_session.Score);
            // Kỷ lục chỉ được LƯU khi hết ván; dòng BEST hiển thị số lớn hơn giữa kỷ lục đã lưu và điểm hiện tại
            hudView.SetBest(Mathf.Max(SaveData.EndlessBest, _session.Score));
            hudView.SetNext(_session.NextPiece);
            handView.SetCurrent(_session.CurrentPiece);
            if (IsLevel) levelHud.Sync(_session.Goals, _session.CardsLeftInDeck ?? 0);
            UpdateHint();
        }

        /// <summary>
        /// Lượt tutorial (Level): viền vàng ô gợi ý + bàn tay chỉ từ lá trên tay tới ô đó + chữ tutorial của lượt.
        /// </summary>
        private void UpdateHint()
        {
            if (!IsLevel || !_session.IsStarted)
            {
                boardView.SetHints(null);
                return;
            }
            Piece piece = _session.CurrentPiece;
            bool playing = _session.Status == GameStatus.Playing && _turnRoutine == null && _introRoutine == null;
            boardView.SetHints(playing && piece != null && piece.HasHint ? piece.Hint : null);

            TutorialStep step = null;
            foreach (TutorialStep t in CurrentLevel.Tutorial)
                if (t.Turn == _session.Turn + 1) step = t;
            if (hintPointer == null) return;
            hintPointer.SetCaption(playing && step != null ? step.Text.Get(Loc.Language) : null);

            CellPos? target = piece != null && piece.HasHint ? piece.Hint[0] : step?.HandPointer;
            bool pointing = playing && target.HasValue && handView.Interactable && !handView.IsDragging;
            if (pointing) hintPointer.Show(handView.HomeCenterWorld, boardView.CellCenterWorld(target.Value));
            else hintPointer.Hide();
        }

        // ------------------------------------------------------------------ Kéo-thả

        private void OnCardDragged(Vector2 cardCenter)
        {
            if (hintPointer != null) hintPointer.Hide();

            CellView hover = null;
            PlacementPreview preview = null;
            if (_turnRoutine == null
                && _session.Status == GameStatus.Playing
                && boardView.TryGetCellAt(cardCenter, out CellView cell))
            {
                hover = cell;
                preview = GetPreview(cell.Pos);
            }

            if (hover == _hoverCell) return;
            _hoverCell = hover;
            bool valid = preview != null && preview.Valid;
            // M4.5: khối đôi → ô dưới lá ① + ô của lá ② (theo hướng khối) cùng sáng / cùng báo không thả được
            CellView second = SecondCellOf(hover);
            boardView.SetHighlight(valid ? hover : null, valid ? second : null);
            // Bước 4.5: ô không thả được (có lá / khóa / không phải ô gợi ý) → overlay Invalid
            boardView.SetInvalid(hover != null && !valid ? hover : null, hover != null && !valid ? second : null);
            // Xem trước: chỉ làm sáng NHÓM ĐẦU TIÊN sẽ merge với lá đang kéo (quyết định bước 2.5)
            if (!valid) preview = null;
            boardView.SetPreview(preview?.FirstGroupCells);
            // Bước 1.4 — hiệu ứng chất của bước merge đầu: icon chất sáng lên, ♠ tô đỏ 3×3, ♣ tô xanh ô sinh lá
            boardView.SetZones(DangerCells(preview), preview?.ClubSpawnCell);
            handView.SetSuitHighlight(preview != null && preview.FirstEffectSuit != Suit.None);
        }

        /// <summary>
        /// Vùng ♠ tô đỏ khi kéo: vùng 3×3 TRỪ các lá của nhóm merge (chúng bị merge chứ không bị ♠ xóa,
        /// giữ viền vàng cho rõ).
        /// </summary>
        private static List<CellPos> DangerCells(PlacementPreview preview)
        {
            if (preview == null || preview.SpadeArea.Count == 0) return null;
            var cells = new List<CellPos>(preview.SpadeArea);
            foreach (CellPos p in preview.FirstGroupCells) cells.Remove(p);
            return cells;
        }

        private PlacementPreview GetPreview(CellPos pos)
        {
            if (!_previewCache.TryGetValue(pos, out PlacementPreview preview))
            {
                preview = _session.PreviewPiece(pos);
                _previewCache[pos] = preview;
            }
            return preview;
        }

        private void OnCardDropped(Vector2 cardCenter)
        {
            ClearDragFeedback();
            if (_turnRoutine != null || _session.Status != GameStatus.Playing) return;
            if (!boardView.TryGetCellAt(cardCenter, out CellView cell)) // thả ngoài bàn → lá bay về tay + lắc (Animation phần 2)
            {
                UpdateHint();
                return;
            }

            TurnResult result = _session.PlacePiece(cell.Pos);
            if (!result.Accepted) // ô có lá / ô khóa / không phải ô gợi ý → lá bay về tay + lắc (Animation phần 2)
            {
                UpdateHint();
                return;
            }
            handView.ClaimDrop(); // Animation phần 2: lá đứng yên chỗ thả → EventPlayer cho trượt vào ô

            if (eventPlayer != null) _turnRoutine = StartCoroutine(PlayTurn(result));
            else
            {
                RefreshAll();
                AfterTurnEnded();
            }
        }

        /// <summary>Khoá tay → phát animation → vẽ lại từ Core → thắng / thua thì chờ rồi hiện popup, không thì mở khoá.</summary>
        private IEnumerator PlayTurn(TurnResult result)
        {
            handView.Interactable = false;
            boardView.SetHints(null);
            int savedBest = SaveData.EndlessBest;
            yield return eventPlayer.Play(result.Events, savedBest);
            _turnRoutine = null;
            RefreshAll();

            if (_session.Status != GameStatus.Playing)
            {
                yield return new WaitForSeconds(eventPlayer.GameOverDelay);
                // Animation phần 4: HOÀN THÀNH! + pháo giấy / các lá rung + bàn tối dần — rồi mới hiện popup
                if (_session.Status == GameStatus.Won) yield return eventPlayer.PlayWin();
                else yield return eventPlayer.PlayLose();
                AfterTurnEnded();
            }
            else
            {
                handView.Interactable = true;
                UpdateHint();
            }
        }

        /// <summary>Ván kết thúc sau lượt vừa rồi: Level → popup Hoàn thành / Hết lá; Endless → Game Over như cũ.</summary>
        private void AfterTurnEnded()
        {
            if (_session.Status == GameStatus.Playing) return;
            handView.Interactable = false;
            // M4.5: hết chỗ cho khối đôi → popup HẾT CHỖ! trước (M5 thêm nút Swap / Hammer cứu); bỏ cuộc mới tới popup thua
            if (_session.Status == GameStatus.Lost && _session.LoseReason == LoseReason.NoSpaceForPair && pairNoSpacePopup != null)
            {
                Debug.Log($"[Suit Merge] Hết chỗ cho khối đôi {_session.CurrentPiece} — điểm {_session.Score}");
                pairNoSpacePopup.Show(_session.CurrentPiece);
                return;
            }
            if (!IsLevel)
            {
                HandleGameOver();
                return;
            }

            LevelData level = CurrentLevel;
            if (_session.Status == GameStatus.Won)
            {
                int coins = level.Reward.Coins + (level.Reward.Chest ? ChestCoins : 0);
                Debug.Log($"[Suit Merge] Level {level.Id} THẮNG — điểm {_session.Score}, lượt {_session.Turn}");
                levelCompletePopup.Show(level.Id, _session.Score, coins);
            }
            else
            {
                Debug.Log($"[Suit Merge] Level {level.Id} THUA ({_session.LoseReason}) — điểm {_session.Score}");
                levelFailPopup.Show(_session.LoseReason, _session.Goals);
            }
        }

        /// <summary>Ô của lá ② khi lá ① nằm ở <paramref name="first"/> (lá đơn / ngoài bàn: null).</summary>
        private CellView SecondCellOf(CellView first)
        {
            Piece piece = _session.IsStarted ? _session.CurrentPiece : null;
            return first != null && piece != null && piece.IsPair ? boardView.GetCell(piece.SecondCell(first.Pos)) : null;
        }

        /// <summary>M4.5: chạm vào khối đôi trên tay → xoay 90° (không tốn lượt).</summary>
        private void OnHandTapped()
        {
            if (_turnRoutine != null || !handView.Interactable || !_session.IsStarted || _session.Status != GameStatus.Playing) return;
            PieceRotatedEvent rotated = _session.RotateCurrentPiece();
            if (rotated == null) return;
            ClearDragFeedback();
            _previewCache.Clear();
            handView.ShowRotation(rotated.Piece);
            UpdateHint();
        }

        /// <summary>Bỏ cuộc ở popup HẾT CHỖ! của khối đôi → popup thua như thường (Level: Hết lá · Endless: Game Over).</summary>
        private void OnPairGiveUp()
        {
            pairNoSpacePopup.Hide();
            if (IsLevel) levelFailPopup.Show(_session.LoseReason, _session.Goals);
            else HandleGameOver();
        }

        private void ClearDragFeedback()
        {
            _hoverCell = null;
            boardView.SetHighlight(null);
            boardView.SetInvalid(null);
            boardView.SetPreview(null);
            boardView.SetZones(null, null);
            handView.SetSuitHighlight(false);
        }

        private void StopTurn()
        {
            if (_turnRoutine != null) StopCoroutine(_turnRoutine);
            _turnRoutine = null;
            if (_introRoutine != null) StopCoroutine(_introRoutine);
            _introRoutine = null;
            if (eventPlayer != null) eventPlayer.StopAll();
        }

        /// <summary>Tắt NGAY mọi popup, không animation (chơi lại / đổi level / về bảng chọn level — Animation phần 1).</summary>
        private void HideAllPopups()
        {
            gameOverPopup.HideImmediate();
            if (pausePopup != null) pausePopup.HideImmediate();
            if (levelSelectPopup != null) levelSelectPopup.HideImmediate();
            if (levelCompletePopup != null) levelCompletePopup.HideImmediate();
            if (levelFailPopup != null) levelFailPopup.HideImmediate();
            if (unlockPopup != null) unlockPopup.HideImmediate();
            if (settingsPopup != null) settingsPopup.HideImmediate();
            if (pairNoSpacePopup != null) pairNoSpacePopup.HideImmediate();
        }

        private void HandleGameOver()
        {
            handView.Interactable = false;
            int score = _session.Score;
            bool isNewBest = SaveData.SubmitEndlessScore(score);
            Debug.Log($"[Suit Merge] Hết chỗ — điểm {score}, kỷ lục {SaveData.EndlessBest}{(isNewBest ? " (mới!)" : "")}, seed {_session.Seed}");
            gameOverPopup.Show(score, SaveData.EndlessBest, isNewBest);
        }

        private GameConfig LoadConfig()
        {
            var config = new GameConfig();
            if (configJson != null) JsonUtility.FromJsonOverwrite(configJson.text, config);
            else Debug.LogWarning("[Suit Merge] Chưa gán game_config.json cho GameBootstrap — dùng giá trị mặc định.");
            return config;
        }
    }
}
