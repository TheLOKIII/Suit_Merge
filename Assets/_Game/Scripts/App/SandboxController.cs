using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using SuitMerge.Core.Config;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Goals;
using SuitMerge.Core.Level;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;
using SuitMerge.Core.Simulation;
using SuitMerge.View.Animation;
using SuitMerge.View.Board;
using SuitMerge.View.Hand;
using SuitMerge.View.Hud;
using SuitMerge.View.Sandbox;
using UnityEngine;

namespace SuitMerge.App
{
    /// <summary>
    /// Điều phối scene Sandbox (công cụ test): nạp preset, sửa bàn, chọn lá trên tay, bật/tắt chất, Undo, tốc độ.
    /// Kéo-thả / xem trước / animation giống hệt màn chơi thật (cùng GameSession + EventPlayer).
    /// KHÔNG lưu gì (kỷ lục, coin) — Sandbox không ảnh hưởng Endless.
    /// Bước 4.6 — chế độ LEVEL: chơi L1–L10 với bộ bài thật (script + seed), xem mục tiêu / lá còn lại / 5 lá sắp ra,
    /// "Seed khác" để thử may rủi; không đổi lá trên tay, không sửa bàn / mở chất (giống màn Game).
    /// </summary>
    public class SandboxController : MonoBehaviour
    {
        private const int MaxUndo = 100;
        private const int UpcomingShown = 5;
        private const string LevelFolder = "Assets/_Game/Data/Levels";

        [SerializeField] private TextAsset configJson;
        [SerializeField] private TextAsset presetsJson;
        [Tooltip("Để trống = tự tìm Data/Levels/level_*.json khi chạy trong Editor (Sandbox là công cụ dev).")]
        [SerializeField] private TextAsset[] levelFiles;
        [SerializeField] private BoardView boardView;
        [SerializeField] private HudView hudView;
        [SerializeField] private HandView handView;
        [SerializeField] private EventPlayer eventPlayer;
        [SerializeField] private SandboxToolbar toolbar;

        private GameSession _session;
        private FixedPieceSource _source;
        private SandboxPreset[] _presets = Array.Empty<SandboxPreset>();
        private int _presetIndex;
        private readonly List<GameSession.Snapshot> _undo = new List<GameSession.Snapshot>();
        private readonly HashSet<Suit> _enabledSuits = new HashSet<Suit>();
        private Rank _rank = Rank.Two;
        private Suit _suit = Suit.Heart;
        private SandboxBrush _brush = SandboxBrush.Play;
        private float _speed = 1f;
        private Coroutine _turnRoutine;
        private CellView _hoverCell;
        private readonly Dictionary<CellPos, PlacementPreview> _previewCache = new Dictionary<CellPos, PlacementPreview>();
        private LevelFlow _levels;
        private bool _levelMode;
        private int _levelIndex;
        /// <summary>Seed đang dùng cho phần random của deck; null = seed trong file level.</summary>
        private uint? _levelSeed;

        private LevelData CurrentLevel => _levelMode && _levels != null && _levels.Count > 0 ? _levels[_levelIndex] : null;

        private Card HandCard => new Card(_rank, _suit);
        /// <summary>M4.5: lá B của khối đôi đang chọn.</summary>
        private Card HandCardB => new Card(_rankB, _suitB);
        /// <summary>Lá đang sửa trong bảng chọn lá (lá A, hoặc lá B khi đang sửa lá B của khối đôi) — bút "Đặt lá" dùng lá này.</summary>
        private Card ActiveCard => _pairMode && _slot == 1 ? HandCardB : HandCard;
        /// <summary>Lá đang chọn gói thành Piece: lá đơn, hoặc khối đôi [A B] theo hướng đang xoay (M4.5).</summary>
        private Piece HandPiece => _pairMode ? Piece.Pair(HandCard, HandCardB, _orientation) : Piece.Single(HandCard);

        // M4.5: khối đôi trong chế độ PRESET
        private bool _pairMode;
        private int _slot;               // 0 = đang sửa lá A, 1 = lá B
        private Rank _rankB = Rank.Three;
        private Suit _suitB = Suit.Diamond;
        private PieceOrientation _orientation = PieceOrientation.H_AB;

        /// <summary>Nguồn lá cố định của chế độ PRESET: mỗi lần rút trả đúng Piece đang chọn (lá đơn / khối đôi).</summary>
        private sealed class FixedPieceSource : IPieceSource
        {
            public Piece Piece { get; set; }
            public Piece Draw(Board board) => Piece?.Clone();
        }

        private void Awake()
        {
            Application.targetFrameRate = 60;
            var config = new GameConfig();
            if (configJson != null) JsonUtility.FromJsonOverwrite(configJson.text, config);
            _session = new GameSession(config);
            _source = new FixedPieceSource();
            _presets = SandboxPresetFile.Load(presetsJson);
        }

        private void OnEnable()
        {
            handView.CardDragged += OnCardDragged;
            handView.CardDropped += OnCardDropped;
            handView.CardTapped += OnHandTapped;
            toolbar.PrevPresetClicked += PrevPreset;
            toolbar.NextPresetClicked += NextPreset;
            toolbar.ReloadClicked += ReloadPreset;
            toolbar.UndoClicked += Undo;
            toolbar.SpeedPicked += PickSpeed;
            toolbar.RankPicked += PickRank;
            toolbar.SuitPicked += PickSuit;
            toolbar.PairModePicked += PickPairMode;
            toolbar.PairSlotPicked += PickPairSlot;
            toolbar.BrushPicked += PickBrush;
            toolbar.SuitToggled += ToggleSuit;
            toolbar.BoardTapped += OnBoardTapped;
            toolbar.ModeToggled += ToggleMode;
            toolbar.NewSeedClicked += NewSeed;
        }

        private void OnDisable()
        {
            handView.CardDragged -= OnCardDragged;
            handView.CardDropped -= OnCardDropped;
            handView.CardTapped -= OnHandTapped;
            toolbar.PrevPresetClicked -= PrevPreset;
            toolbar.NextPresetClicked -= NextPreset;
            toolbar.ReloadClicked -= ReloadPreset;
            toolbar.UndoClicked -= Undo;
            toolbar.SpeedPicked -= PickSpeed;
            toolbar.RankPicked -= PickRank;
            toolbar.SuitPicked -= PickSuit;
            toolbar.PairModePicked -= PickPairMode;
            toolbar.PairSlotPicked -= PickPairSlot;
            toolbar.BrushPicked -= PickBrush;
            toolbar.SuitToggled -= ToggleSuit;
            toolbar.BoardTapped -= OnBoardTapped;
            toolbar.ModeToggled -= ToggleMode;
            toolbar.NewSeedClicked -= NewSeed;
        }

        private void Start()
        {
            hudView.SetBestLabel("SANDBOX");
            hudView.SetCoins(0);
            ApplySpeed();
            toolbar.SetBrush(_brush);
            if (_presets.Length == 0)
            {
                toolbar.SetPreset(0, 1, "(không có preset)", "Chưa gán sandbox_presets.json cho SandboxController");
                StartBoard(new Board(5, 5), new List<Suit>());
            }
            else LoadPreset(0);
        }

        // ------------------------------------------------------------------ Preset

        // ◀ ▶ / Nạp lại dùng chung cho 2 chế độ. Đổi level → seed về seed gốc; Nạp lại giữ seed đang dùng.
        private void PrevPreset()
        {
            if (_levelMode) LoadLevel((_levelIndex - 1 + _levels.Count) % _levels.Count, null);
            else LoadPreset((_presetIndex - 1 + Math.Max(1, _presets.Length)) % Math.Max(1, _presets.Length));
        }

        private void NextPreset()
        {
            if (_levelMode) LoadLevel((_levelIndex + 1) % _levels.Count, null);
            else LoadPreset((_presetIndex + 1) % Math.Max(1, _presets.Length));
        }

        private void ReloadPreset()
        {
            if (_levelMode) LoadLevel(_levelIndex, _levelSeed);
            else LoadPreset(_presetIndex);
        }

        private void LoadPreset(int index)
        {
            if (_presets.Length == 0) return;
            StopTurn();
            _levelMode = false;
            toolbar.SetMode(false);
            _presetIndex = index;
            SandboxPreset p = _presets[index];
            toolbar.SetPreset(index, _presets.Length, p.name, p.expect);

            Board board;
            string error = null;
            try
            {
                board = p.BuildBoard();
            }
            catch (Exception e)
            {
                error = e.Message;
                Debug.LogError($"[Sandbox] Preset '{p.name}' lỗi bàn: {e.Message}");
                board = new Board(5, 5);
            }
            if (Card.TryParse(p.hand, out Card hand))
            {
                _rank = hand.Rank;
                _suit = hand.Suit;
            }
            // Preset ghi 1 lá trên tay → về lá đơn (khối đôi chọn lại bằng nút "Đổi lá")
            _pairMode = false;
            _slot = 0;
            _orientation = PieceOrientation.H_AB;
            StartBoard(board, p.EnabledSuits());
            toolbar.SetLog(error != null
                ? "Preset lỗi (bàn trống thay thế): " + error
                : $"Đã nạp \"{p.name}\". Kéo lá trên tay vào bàn · chạm vào lá để đổi lá.");
        }

        // ------------------------------------------------------------------ Level (bước 4.6)

        private void ToggleMode()
        {
            if (_turnRoutine != null) return;
            if (_levelMode)
            {
                if (_presets.Length > 0) LoadPreset(_presetIndex);
                else
                {
                    _levelMode = false;
                    toolbar.SetMode(false);
                    toolbar.SetPreset(0, 1, "(không có preset)", "Chưa gán sandbox_presets.json cho SandboxController");
                    StartBoard(new Board(5, 5), new List<Suit>());
                }
                return;
            }

            if (_levels == null) _levels = new LevelFlow(FindLevelFiles());
            if (_levels.Count == 0)
            {
                toolbar.SetLog($"Không tìm thấy file level nào trong {LevelFolder} (hoặc file lỗi — xem Console).");
                return;
            }
            if (_brush != SandboxBrush.Play) PickBrush(SandboxBrush.Play);
            LoadLevel(_levelIndex, null);
        }

        /// <summary>Nạp level thứ <paramref name="index"/> với <paramref name="seed"/> (null = seed trong file).</summary>
        private void LoadLevel(int index, uint? seed)
        {
            StopTurn();
            LevelData level = _levels[index];
            try
            {
                // Seed khác: bản sao level chỉ khác seed phần random (đoạn script giữ nguyên), level gốc không đổi
                _session.StartLevel(seed.HasValue ? LevelSimulator.WithDeckSeed(level, seed.Value) : level);
            }
            catch (NotSupportedException e)
            {
                toolbar.SetLog($"Level {level.Id} chưa chơi được: {e.Message}");
                return;
            }
            _levelMode = true;
            _levelIndex = index;
            _levelSeed = seed;
            _enabledSuits.Clear();
            foreach (Suit s in level.EnabledSuitEffects) _enabledSuits.Add(s);
            _undo.Clear();
            toolbar.SetMode(true);
            SyncToolbar();
            RefreshAll();
            Debug.Log($"[Sandbox] Level {level.Id}, {SeedText(level)} — {LevelLoader.Summary(level)}");
            toolbar.SetLog($"Đã nạp Level {level.Id} ({level.Rows}×{level.Cols}, {level.Deck.Size} lá). Kéo lá trên tay vào bàn.");
        }

        /// <summary>Seed khác: chỉ đổi phần random của deck (đoạn script giữ nguyên), chơi lại từ đầu.</summary>
        private void NewSeed()
        {
            LevelData level = CurrentLevel;
            if (level == null || _turnRoutine != null) return;
            if (level.Deck.Random == null || level.Deck.RandomCardCount == 0)
            {
                toolbar.SetLog($"Level {level.Id} không có phần rút ngẫu nhiên — đổi seed không có tác dụng.");
                return;
            }
            uint current = _levelSeed ?? level.Deck.Random.Seed;
            uint seed;
            do seed = (uint)UnityEngine.Random.Range(1, 1000000);
            while (seed == current || seed == level.Deck.Random.Seed);
            LoadLevel(_levelIndex, seed);
        }

        private string SeedText(LevelData level)
        {
            if (level.Deck.Random == null || level.Deck.RandomCardCount == 0) return "chỉ script";
            return _levelSeed.HasValue ? $"seed {_levelSeed.Value} (khác)" : $"seed {level.Deck.Random.Seed} (gốc)";
        }

        /// <summary>
        /// Hàng 2 của thanh: "Mục tiêu: Tạo lá 4 1/2 · Còn 8 lá" + "Sắp ra: 3 Cơ · 2 Rô→(1,2) …".
        /// </summary>
        private string LevelInfoText()
        {
            var goals = new List<string>();
            foreach (GoalProgress g in _session.Goals)
                goals.Add($"{GoalName(g.Goal)} {Math.Min(g.Current, g.Target)}/{g.Target}{(g.IsDone ? " (xong)" : "")}");
            string line1 = $"Mục tiêu: {string.Join(" · ", goals)} · Còn {_session.CardsLeftInDeck ?? 0} lá";

            var next = new List<string>();
            foreach (Piece p in _session.PeekUpcoming(UpcomingShown)) next.Add(PieceName(p));
            string line2 = "Sắp ra (sau UP NEXT): " + (next.Count > 0 ? string.Join(" · ", next) : "hết");
            return line1 + "\n" + line2;
        }

        private static string GoalName(GoalData goal)
        {
            switch (goal.Type)
            {
                case GoalType.CreateRank: return "Tạo lá " + goal.Rank.ToLabel();
                case GoalType.Score: return "Điểm";
                case GoalType.Ace: return "Tạo Ace";
                case GoalType.SuitTrigger: return "Kích hoạt " + SuitName(goal.Suit);
                default: return LevelLoader.Describe(goal);
            }
        }

        private static string PieceName(Piece p) =>
            CardName(p.First) + (p.HasHint ? $"→({p.Hint[0].Row},{p.Hint[0].Col})" : "");

        /// <summary>Thông báo thắng / thua cuối ván (chế độ LEVEL).</summary>
        private string LevelEndText()
        {
            if (_session.Status == GameStatus.Won)
                return $"THẮNG ở lượt {_session.Turn} · điểm {_session.Score} — bấm Undo / Nạp lại / Seed khác để thử tiếp.";
            string reason = _session.LoseReason == LoseReason.OutOfCards ? "hết lá"
                : _session.LoseReason == LoseReason.NoSpaceForPair ? "hết chỗ cho khối đôi" : "bàn đầy";
            return $"THUA ({reason}) ở lượt {_session.Turn} · điểm {_session.Score} — bấm Undo để thử nước khác.";
        }

        /// <summary>File level: gán sẵn trong Inspector, hoặc tự tìm trong Data/Levels (chỉ chạy trong Editor).</summary>
        private TextAsset[] FindLevelFiles()
        {
            if (levelFiles != null && levelFiles.Length > 0) return levelFiles;
#if UNITY_EDITOR
            var found = new List<TextAsset>();
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("level_ t:TextAsset", new[] { LevelFolder }))
            {
                var file = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (file != null) found.Add(file);
            }
            return found.ToArray();
#else
            return Array.Empty<TextAsset>();
#endif
        }

        private void StartBoard(Board board, List<Suit> suits)
        {
            _enabledSuits.Clear();
            foreach (Suit s in suits) _enabledSuits.Add(s);
            _source.Piece = HandPiece;
            _session.Start(board, _source, GameMode.Level, _enabledSuits);
            _undo.Clear();
            SyncToolbar();
            RefreshAll();
        }

        // ------------------------------------------------------------------ Công cụ

        private void PickRank(Rank rank)
        {
            if (_pairMode && _slot == 1) _rankB = rank;
            else _rank = rank;
            ApplyHand();
        }

        private void PickSuit(Suit suit)
        {
            if (_pairMode && _slot == 1) _suitB = suit;
            else _suit = suit;
            ApplyHand();
        }

        /// <summary>M4.5: bảng chọn lá — Lá đơn / Khối đôi.</summary>
        private void PickPairMode(bool pair)
        {
            _pairMode = pair;
            if (!pair) _slot = 0;
            ApplyHand();
        }

        /// <summary>M4.5: bảng chọn lá — đang sửa lá A hay lá B.</summary>
        private void PickPairSlot(int slot)
        {
            _slot = _pairMode ? slot : 0;
            SyncToolbar();
        }

        /// <summary>Lá trên tay = lá đang chọn (cả UP NEXT, vì nguồn lá cố định).</summary>
        private void ApplyHand()
        {
            if (_turnRoutine != null || _levelMode) return; // LEVEL: lá trên tay do bộ bài quyết định
            _source.Piece = HandPiece;
            _session.SetHand(HandPiece, HandPiece);
            SyncToolbar();
            RefreshAll();
        }

        /// <summary>
        /// Chạm nhẹ vào lá trên tay: khối đôi → xoay (như màn chơi thật, M4.5); lá đơn → bảng chọn lá (PRESET).
        /// </summary>
        private void OnHandTapped()
        {
            if (_turnRoutine != null) return;
            ClearDragFeedback();
            if (_session.Status == GameStatus.Playing && _session.CurrentPiece != null && _session.CurrentPiece.IsPair)
            {
                RotateHand();
                return;
            }
            if (_levelMode)
            {
                toolbar.SetLog("Chế độ LEVEL: không đổi lá trên tay — dùng chế độ PRESET để thử lá tuỳ ý.");
                return;
            }
            toolbar.OpenCardPicker();
        }

        /// <summary>Xoay khối đôi trên tay. PRESET: các khối rút sau (UP NEXT) cũng theo hướng mới.</summary>
        private void RotateHand()
        {
            PieceRotatedEvent rotated = _session.RotateCurrentPiece();
            if (rotated == null) return;
            if (!_levelMode)
            {
                _orientation = rotated.Piece.Orientation;
                _source.Piece = HandPiece;
                _session.SetHand(_session.CurrentPiece, HandPiece);
                hudView.SetNext(_session.NextPiece);
            }
            _previewCache.Clear();
            handView.ShowRotation(rotated.Piece);
            toolbar.SetLog($"Xoay khối đôi → {rotated.Piece} ({rotated.Piece.Orientation}) — lá ① = {CardName(rotated.Piece.First)}");
        }

        /// <summary>Ô của lá ② khi lá ① nằm ở <paramref name="first"/> (lá đơn / ngoài bàn: null).</summary>
        private CellView SecondCellOf(CellView first)
        {
            Piece piece = _session.CurrentPiece;
            return first != null && piece != null && piece.IsPair ? boardView.GetCell(piece.SecondCell(first.Pos)) : null;
        }

        private void ToggleSuit(Suit suit)
        {
            if (_turnRoutine != null || _levelMode) return; // LEVEL: chất mở theo file level
            bool on = !_enabledSuits.Contains(suit);
            if (on) _enabledSuits.Add(suit);
            else _enabledSuits.Remove(suit);
            _session.SetSuitEnabled(suit, on);
            _previewCache.Clear();
            SyncToolbar();
        }

        private void PickBrush(SandboxBrush brush)
        {
            if (_levelMode && brush != SandboxBrush.Play) return; // LEVEL: không sửa bàn
            _brush = brush;
            toolbar.SetBrush(brush);
            ClearDragFeedback();
            UpdateInteractable();
            SyncToolbar();
            if (brush == SandboxBrush.Play) toolbar.SetLog("Xong sửa bàn — kéo lá trên tay vào bàn.");
        }

        private void PickSpeed(float speed)
        {
            _speed = speed;
            ApplySpeed();
        }

        private void ApplySpeed()
        {
            if (eventPlayer != null) eventPlayer.Speed = _speed;
            toolbar.SetSpeed(_speed);
        }

        private void Undo()
        {
            if (_turnRoutine != null || _undo.Count == 0) return;
            GameSession.Snapshot snap = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _session.RestoreSnapshot(snap);
            if (!_levelMode)
            {
                // PRESET: lá trên tay + chất đang mở là THIẾT LẬP của công cụ → giữ theo thanh công cụ, không quay lại.
                // LEVEL: snapshot đã gồm lá trên tay, UP NEXT, vị trí rút deck và mục tiêu → dùng nguyên.
                _session.SetHand(HandPiece, HandPiece);
                foreach (Suit s in SandboxToolbar.ToggleSuits) _session.SetSuitEnabled(s, _enabledSuits.Contains(s));
            }
            SyncToolbar();
            RefreshAll();
            toolbar.SetLog($"Undo → lượt {_session.Turn}, điểm {_session.Score}");
        }

        private void PushUndo()
        {
            _undo.Add(_session.CaptureSnapshot());
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
        }

        // ------------------------------------------------------------------ Sửa ô

        private void OnBoardTapped(Vector2 screenPoint)
        {
            if (_turnRoutine != null || _brush == SandboxBrush.Play || _levelMode) return;
            if (!boardView.TryGetCellAt(screenPoint, out CellView cell)) return;
            CellPos pos = cell.Pos;
            Cell data = _session.Board[pos];

            PushUndo();
            string what;
            switch (_brush)
            {
                case SandboxBrush.Card:
                    _session.SetCell(pos, ActiveCard);
                    what = "đặt " + CardName(ActiveCard);
                    break;
                case SandboxBrush.Erase:
                    _session.SetCell(pos, null);
                    what = "xoá lá";
                    break;
                default: // Lock
                    _session.SetCell(pos, null, exists: !data.Exists);
                    what = data.Exists ? "khoá ô" : "mở khoá ô";
                    break;
            }
            SyncToolbar();
            RefreshAll();
            toolbar.SetLog($"Sửa ô ({pos.Row},{pos.Col}): {what}");
        }

        // ------------------------------------------------------------------ Kéo-thả (giống màn chơi thật)

        private void OnCardDragged(Vector2 cardCenter)
        {
            CellView hover = null;
            PlacementPreview preview = null;
            if (_turnRoutine == null
                && _brush == SandboxBrush.Play
                && _session.Status == GameStatus.Playing
                && boardView.TryGetCellAt(cardCenter, out CellView cell))
            {
                hover = cell;
                preview = GetPreview(cell.Pos);
            }

            if (hover == _hoverCell) return;
            _hoverCell = hover;
            bool valid = preview != null && preview.Valid;
            CellView second = SecondCellOf(hover); // M4.5: khối đôi sáng / báo lỗi cả ô lá ②
            boardView.SetHighlight(valid ? hover : null, valid ? second : null);
            boardView.SetInvalid(hover != null && !valid ? hover : null, hover != null && !valid ? second : null); // bước 4.5: ô không thả được
            if (!valid) preview = null;
            boardView.SetPreview(preview?.FirstGroupCells);
            boardView.SetZones(DangerCells(preview), preview?.ClubSpawnCell);
            handView.SetSuitHighlight(preview != null && preview.FirstEffectSuit != Suit.None);
        }

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
            if (_turnRoutine != null || _brush != SandboxBrush.Play || _session.Status != GameStatus.Playing) return;
            if (!boardView.TryGetCellAt(cardCenter, out CellView cell)) return;

            GameSession.Snapshot before = _session.CaptureSnapshot();
            TurnResult result = _session.PlacePiece(cell.Pos);
            if (!result.Accepted)
            {
                if (result.RejectReason == PlaceRejectReason.NotHintCell)
                    toolbar.SetLog("Lượt hướng dẫn: chỉ thả được vào ô viền vàng.");
                return;
            }

            handView.ClaimDrop(); // Animation phần 2: lá đứng yên chỗ thả → EventPlayer cho trượt vào ô
            _undo.Add(before);
            if (_undo.Count > MaxUndo) _undo.RemoveAt(0);
            LogTurn(result);

            if (eventPlayer != null) _turnRoutine = StartCoroutine(PlayTurn(result));
            else AfterTurn();
        }

        private IEnumerator PlayTurn(TurnResult result)
        {
            handView.Interactable = false;
            boardView.SetHints(null);
            yield return eventPlayer.Play(result.Events, 0);
            _turnRoutine = null;
            AfterTurn();
        }

        private void AfterTurn()
        {
            SyncToolbar();
            RefreshAll();
            if (_levelMode && _session.Status != GameStatus.Playing) toolbar.SetLog(LevelEndText());
        }

        private void StopTurn()
        {
            if (_turnRoutine != null) StopCoroutine(_turnRoutine);
            _turnRoutine = null;
            if (eventPlayer != null) eventPlayer.StopAll();
            ClearDragFeedback();
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

        // ------------------------------------------------------------------ Hiển thị

        private void RefreshAll()
        {
            _previewCache.Clear();
            boardView.Render(_session.Board);
            boardView.SetDangerFor(_session.Board, _session.Status == GameStatus.Playing); // Animation phần 3: bàn sắp đầy
            handView.DragTargetSize = boardView.CellSize; // bước 4.5: lá kéo co đúng cỡ ô
            hudView.SetScore(_session.Score);
            hudView.SetNext(_session.NextPiece);
            handView.SetCurrent(_session.CurrentPiece);
            // LEVEL: lượt hướng dẫn → viền vàng ô gợi ý (chỉ thả được vào đó, như màn Game)
            Piece piece = _session.CurrentPiece;
            bool hint = _levelMode && _turnRoutine == null && _session.Status == GameStatus.Playing && piece != null && piece.HasHint;
            boardView.SetHints(hint ? piece.Hint : null);
            UpdateInteractable();
        }

        private void UpdateInteractable()
        {
            handView.Interactable = _turnRoutine == null && _brush == SandboxBrush.Play && _session.Status == GameStatus.Playing;
        }

        private void SyncToolbar()
        {
            toolbar.SetRank(_pairMode && _slot == 1 ? _rankB : _rank);
            toolbar.SetSuit(_pairMode && _slot == 1 ? _suitB : _suit);
            toolbar.SetPairMode(_pairMode, _slot);
            toolbar.SetHandLabel(_pairMode
                ? $"Khối đôi: {CardName(HandCard)} | {CardName(HandCardB)} — đang sửa lá {(_slot == 1 ? "B" : "A")}"
                : "Đang chọn: " + CardName(HandCard));
            toolbar.SetEditText(EditText());
            toolbar.SetUndoCount(_undo.Count);
            foreach (Suit s in SandboxToolbar.ToggleSuits) toolbar.SetSuitEnabled(s, _enabledSuits.Contains(s));
            LevelData level = CurrentLevel;
            if (level != null) toolbar.SetLevelInfo($"Level {level.Id} · {SeedText(level)}", LevelInfoText());
        }

        // ------------------------------------------------------------------ Nhật ký

        private void LogTurn(TurnResult result)
        {
            var parts = new List<string>();
            foreach (GameEvent e in result.Events)
            {
                switch (e)
                {
                    case MergeStartedEvent m: parts.Add($"Merge {m.Group.Count} lá"); break;
                    case ChainStepEvent c: parts.Add($"Combo ×{c.Step}"); break;
                    case CardCreatedEvent c: parts.Add($"→ {CardName(c.Card)} +{c.ScoreGained}"); break;
                    case DiamondBonusEvent d: parts.Add($"Rô ×{d.Multiplier}"); break;
                    case HeartLeapEvent h:
                        parts.Add($"Cơ nhảy +{h.Leap}" + (h.OverflowBonus > 0 ? $" (vượt K +{h.OverflowBonus})" : ""));
                        break;
                    case ClubSpawnEvent c:
                        parts.Add(c.SpawnCell.HasValue
                            ? $"Chuồn sinh {CardName(c.Card)} ở ({c.SpawnCell.Value.Row},{c.SpawnCell.Value.Col})"
                            : $"Chuồn: bàn đầy +{c.FallbackBonus}");
                        break;
                    case SpadeClearEvent s: parts.Add($"Bích dọn {s.ClearedCells.Count} lá +{s.Bonus}"); break;
                    case AceCreatedEvent a: parts.Add($"ACE +{a.ScoreGained:N0}"); break;
                    case GoalProgressEvent g: parts.Add($"Mục tiêu {g.Index + 1}: {Math.Min(g.NewValue, g.Target)}/{g.Target}"); break;
                    case LevelWonEvent _: parts.Add("THẮNG!"); break;
                    case GameOverEvent o:
                        parts.Add(o.Reason == LoseReason.OutOfCards ? "THUA (hết lá) — bấm Undo"
                            : o.Reason == LoseReason.NoSpaceForPair ? "THUA (hết chỗ cho khối đôi) — bấm Undo" : "THUA (bàn đầy) — bấm Undo");
                        break;
                }
            }
            if (result.MergeCount == 0) parts.Insert(0, "Không merge");

            string line = $"Lượt {_session.Turn}: {string.Join(" · ", parts)} = +{result.ScoreGained}";
            toolbar.SetLog(line);

            var full = new StringBuilder($"[Sandbox] {line}\n");
            foreach (GameEvent e in result.Events) full.AppendLine("  " + e);
            full.Append(_session.Board.ToText());
            Debug.Log(full.ToString());
        }

        /// <summary>Chữ trên dải "Đang sửa".</summary>
        private string EditText()
        {
            switch (_brush)
            {
                case SandboxBrush.Card: return $"Đặt lá {CardName(ActiveCard)} — chạm vào ô";
                case SandboxBrush.Erase: return "Xoá lá — chạm vào ô";
                case SandboxBrush.Lock: return "Khoá / mở khoá ô — chạm vào ô";
                default: return "";
            }
        }

        private static string SuitName(Suit suit)
        {
            switch (suit)
            {
                case Suit.Heart: return "Cơ";
                case Suit.Diamond: return "Rô";
                case Suit.Club: return "Chuồn";
                case Suit.Spade: return "Bích";
                default: return "";
            }
        }

        private static string CardName(Card card) => card == null ? "-" : $"{card.Rank.ToLabel()} {SuitName(card.Suit)}";
    }
}
