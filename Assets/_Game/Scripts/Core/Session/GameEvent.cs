using System.Collections.Generic;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Session
{
    /// <summary>
    /// Sự kiện Logic Core gửi cho View để phát animation theo đúng thứ tự (spec 10.3).
    /// Card trong event là bản sao — View đọc thoải mái, không ảnh hưởng state.
    /// </summary>
    public abstract class GameEvent
    {
    }

    /// <summary>Lá được thả vào ô.</summary>
    public sealed class CardPlacedEvent : GameEvent
    {
        public CellPos Cell { get; }
        public Card Card { get; }

        public CardPlacedEvent(CellPos cell, Card card)
        {
            Cell = cell;
            Card = card;
        }

        public override string ToString() => $"CardPlaced {Card} @ {Cell}";
    }

    /// <summary>
    /// Khối đôi: lá ① đã resolve xong, bắt đầu resolve lá ② tại <see cref="Cell"/> (spec 4.9). Không có event này nếu
    /// lá ② đã bị merge của lá ① lấy mất.
    /// </summary>
    public sealed class PairSecondResolveEvent : GameEvent
    {
        public CellPos Cell { get; }

        public PairSecondResolveEvent(CellPos cell)
        {
            Cell = cell;
        }

        public override string ToString() => $"PairSecondResolve @ {Cell}";
    }

    /// <summary>Khối đôi trên tay xoay 90° (spec 10.3 <c>PieceRotated</c>) — không phải 1 lượt.</summary>
    public sealed class PieceRotatedEvent : GameEvent
    {
        /// <summary>Piece sau khi xoay (bản sao).</summary>
        public Piece Piece { get; }

        public PieceRotatedEvent(Piece piece)
        {
            Piece = piece;
        }

        public override string ToString() => $"PieceRotated {Piece} ({Piece.Orientation})";
    }

    /// <summary>Một nhóm bắt đầu merge — các lá bay về anchor.</summary>
    public sealed class MergeStartedEvent : GameEvent
    {
        public IReadOnlyList<CellPos> Group { get; }
        public CellPos Anchor { get; }
        public int ChainStep { get; }

        public MergeStartedEvent(IReadOnlyList<CellPos> group, CellPos anchor, int chainStep)
        {
            Group = group;
            Anchor = anchor;
            ChainStep = chainStep;
        }

        public override string ToString() => $"MergeStarted N={Group.Count} @ {Anchor} k={ChainStep}";
    }

    /// <summary>Bước chain thứ k (k ≥ 2) — popup "Combo ×k".</summary>
    public sealed class ChainStepEvent : GameEvent
    {
        public int Step { get; }

        public ChainStepEvent(int step)
        {
            Step = step;
        }

        public override string ToString() => $"ChainStep {Step}";
    }

    /// <summary>Lá kết quả merge xuất hiện tại ô, kèm điểm của merge đó.</summary>
    public sealed class CardCreatedEvent : GameEvent
    {
        public CellPos Cell { get; }
        public Card Card { get; }
        public int ScoreGained { get; }

        public CardCreatedEvent(CellPos cell, Card card, int scoreGained)
        {
            Cell = cell;
            Card = card;
            ScoreGained = scoreGained;
        }

        public override string ToString() => $"CardCreated {Card} @ {Cell} +{ScoreGained}";
    }

    /// <summary>Merge K ra Ace: lá biến mất, ô trống, cộng điểm Ace.</summary>
    public sealed class AceCreatedEvent : GameEvent
    {
        public CellPos Cell { get; }
        public int ScoreGained { get; }

        public AceCreatedEvent(CellPos cell, int scoreGained)
        {
            Cell = cell;
            ScoreGained = scoreGained;
        }

        public override string ToString() => $"AceCreated @ {Cell} +{ScoreGained}";
    }

    /// <summary>Tổng điểm thay đổi.</summary>
    public sealed class ScoreChangedEvent : GameEvent
    {
        public int OldScore { get; }
        public int NewScore { get; }

        public ScoreChangedEvent(int oldScore, int newScore)
        {
            OldScore = oldScore;
            NewScore = newScore;
        }

        public override string ToString() => $"ScoreChanged {OldScore} -> {NewScore}";
    }

    /// <summary>Sau lượt đặt: Piece Next thành Piece hiện tại, rút Piece Next mới (null = hết lá).</summary>
    public sealed class CardsDrawnEvent : GameEvent
    {
        public Piece CurrentPiece { get; }
        public Piece NextPiece { get; }
        /// <summary>Lối tắt: lá ① của Piece hiện tại.</summary>
        public Card Current => CurrentPiece?.First;
        /// <summary>Lối tắt: lá ① của Piece Next.</summary>
        public Card Next => NextPiece?.First;

        public CardsDrawnEvent(Piece currentPiece, Piece nextPiece)
        {
            CurrentPiece = currentPiece;
            NextPiece = nextPiece;
        }

        public override string ToString() =>
            $"CardsDrawn current={CurrentPiece?.ToString() ?? "-"} next={NextPiece?.ToString() ?? "-"}";
    }

    /// <summary>Ván kết thúc do thua.</summary>
    public sealed class GameOverEvent : GameEvent
    {
        public LoseReason Reason { get; }
        public int FinalScore { get; }

        public GameOverEvent(LoseReason reason, int finalScore)
        {
            Reason = reason;
            FinalScore = finalScore;
        }

        public override string ToString() => $"GameOver {Reason} score={FinalScore}";
    }

    /// <summary>Level: tiến độ mục tiêu thứ <see cref="Index"/> đổi sau lượt (phát sau CardsDrawn).</summary>
    public sealed class GoalProgressEvent : GameEvent
    {
        public int Index { get; }
        public int OldValue { get; }
        public int NewValue { get; }
        public int Target { get; }
        /// <summary>Mục tiêu vừa hoàn thành ở lượt này.</summary>
        public bool JustCompleted { get; }

        public GoalProgressEvent(int index, int oldValue, int newValue, int target, bool justCompleted)
        {
            Index = index;
            OldValue = oldValue;
            NewValue = newValue;
            Target = target;
            JustCompleted = justCompleted;
        }

        public override string ToString() =>
            $"GoalProgress #{Index} {OldValue} -> {NewValue}/{Target}{(JustCompleted ? " DONE" : "")}";
    }

    /// <summary>Level: mọi mục tiêu đã xong → THẮNG (phát cuối lượt, thay cho GameOver).</summary>
    public sealed class LevelWonEvent : GameEvent
    {
        public int FinalScore { get; }

        public LevelWonEvent(int finalScore)
        {
            FinalScore = finalScore;
        }

        public override string ToString() => $"LevelWon score={FinalScore}";
    }

    // ---------- Hiệu ứng chất (bước 1.4) — phát ngay sau CardCreated của merge đó ----------

    /// <summary>♦: điểm merge vừa rồi đã được nhân <see cref="Multiplier"/> (badge "×3").</summary>
    public sealed class DiamondBonusEvent : GameEvent
    {
        public CellPos Cell { get; }
        public int Multiplier { get; }

        public DiamondBonusEvent(CellPos cell, int multiplier)
        {
            Cell = cell;
            Multiplier = multiplier;
        }

        public override string ToString() => $"DiamondBonus x{Multiplier} @ {Cell}";
    }

    /// <summary>♥: lá kết quả nhảy từ <see cref="FromRank"/> lên <see cref="ToRank"/> (badge "+N"); vượt K → điểm bù.</summary>
    public sealed class HeartLeapEvent : GameEvent
    {
        public CellPos Cell { get; }
        /// <summary>Rank của nhóm trước khi merge.</summary>
        public Rank FromRank { get; }
        public Rank ToRank { get; }
        /// <summary>Số rank nhảy lên (ToRank − FromRank).</summary>
        public int Leap => ToRank.Value() - FromRank.Value();
        public int OverflowRanks { get; }
        public int OverflowBonus { get; }

        public HeartLeapEvent(CellPos cell, Rank fromRank, Rank toRank, int overflowRanks, int overflowBonus)
        {
            Cell = cell;
            FromRank = fromRank;
            ToRank = toRank;
            OverflowRanks = overflowRanks;
            OverflowBonus = overflowBonus;
        }

        public override string ToString() =>
            $"HeartLeap {FromRank.ToLabel()}->{ToRank.ToLabel()} @ {Cell}" + (OverflowBonus > 0 ? $" +{OverflowBonus}" : "");
    }

    /// <summary>
    /// ♣: sinh thêm lá <see cref="Card"/> tại <see cref="SpawnCell"/> (bay từ <see cref="Origin"/>).
    /// Bàn đầy → SpawnCell = null, Card = null, cộng <see cref="FallbackBonus"/>.
    /// </summary>
    public sealed class ClubSpawnEvent : GameEvent
    {
        public CellPos Origin { get; }
        public CellPos? SpawnCell { get; }
        public Card Card { get; }
        public int FallbackBonus { get; }

        public ClubSpawnEvent(CellPos origin, CellPos? spawnCell, Card card, int fallbackBonus)
        {
            Origin = origin;
            SpawnCell = spawnCell;
            Card = card;
            FallbackBonus = fallbackBonus;
        }

        public override string ToString() => SpawnCell.HasValue
            ? $"ClubSpawn {Card} @ {SpawnCell.Value} from {Origin}"
            : $"ClubSpawn board full @ {Origin} +{FallbackBonus}";
    }

    /// <summary>♠: dọn vùng 3×3 quanh <see cref="Center"/> (trừ lá kết quả). Mỗi lá bị xóa được RankValue × 10.</summary>
    public sealed class SpadeClearEvent : GameEvent
    {
        public CellPos Center { get; }
        /// <summary>Mọi ô tồn tại trong vùng 3×3 (trừ tâm) — vùng quét.</summary>
        public IReadOnlyList<CellPos> Area { get; }
        /// <summary>Các ô có lá bị xóa (cùng thứ tự với <see cref="ClearedCards"/>).</summary>
        public IReadOnlyList<CellPos> ClearedCells { get; }
        public IReadOnlyList<Card> ClearedCards { get; }
        public int Bonus { get; }

        public SpadeClearEvent(CellPos center, IReadOnlyList<CellPos> area, IReadOnlyList<CellPos> clearedCells,
            IReadOnlyList<Card> clearedCards, int bonus)
        {
            Center = center;
            Area = area;
            ClearedCells = clearedCells;
            ClearedCards = clearedCards;
            Bonus = bonus;
        }

        public override string ToString() => $"SpadeClear @ {Center} cleared={ClearedCells.Count} +{Bonus}";
    }
}
