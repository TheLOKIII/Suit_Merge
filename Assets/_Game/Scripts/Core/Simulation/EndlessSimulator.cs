using System;
using System.Collections.Generic;
using SuitMerge.Core.Config;
using SuitMerge.Core.Deck;
using SuitMerge.Core.Model;
using SuitMerge.Core.Session;

namespace SuitMerge.Core.Simulation
{
    /// <summary>
    /// Cho máy tự chơi nhiều ván Endless để đo độ khó của bộ sinh lá (playtest bằng số liệu).
    /// Chỉ DÙNG GameSession — không sửa luật. Cùng seed + cùng bot → cùng kết quả.
    /// </summary>
    public static class EndlessSimulator
    {
        /// <summary>Giới hạn an toàn: ván dài hơn số lượt này thì dừng (đánh dấu Finished = false).</summary>
        public const int MaxTurns = 5000;

        public static SimulationReport Run(BotKind bot, int games, uint firstSeed, GameConfig config,
            Action<int> onGameDone = null)
        {
            var runs = new List<GameRunStats>(games);
            for (int i = 0; i < games; i++)
            {
                runs.Add(PlayOne(firstSeed + (uint)i, bot, config));
                onGameDone?.Invoke(i + 1);
            }
            return new SimulationReport(bot, runs);
        }

        public static GameRunStats PlayOne(uint seed, BotKind bot, GameConfig config)
        {
            var session = new GameSession(config);
            session.StartEndless(seed);
            var botRng = new SeededRng(seed ^ 0x9E3779B9u); // tách khỏi dãy sinh lá
            int highest = 0;
            int aces = 0;

            while (session.Status == GameStatus.Playing && session.Turn < MaxTurns)
            {
                TurnResult result;
                if (session.CurrentPiece.IsPair)
                {
                    List<PairBot.Move> moves = PairBot.ValidMoves(session);
                    if (moves.Count == 0) break; // không xảy ra: không còn chỗ cho khối đôi thì ván đã THUA
                    result = PairBot.Play(session,
                        bot == BotKind.Greedy ? PairBot.ChooseGreedy(moves, botRng) : moves[botRng.NextInt(0, moves.Count)]);
                }
                else
                {
                    CellPos target = bot == BotKind.Greedy ? ChooseGreedy(session, botRng) : ChooseRandom(session, botRng);
                    result = session.PlaceCard(target);
                }
                foreach (GameEvent e in result.Events)
                {
                    switch (e)
                    {
                        case CardPlacedEvent placed:
                            highest = Math.Max(highest, (int)placed.Card.Rank);
                            break;
                        case CardCreatedEvent created:
                            highest = Math.Max(highest, (int)created.Card.Rank);
                            break;
                        case AceCreatedEvent _:
                            aces++;
                            highest = (int)Rank.Ace;
                            break;
                    }
                }
            }

            return new GameRunStats(seed, session.Turn, session.Score, highest, aces,
                session.Status != GameStatus.Playing, session.LoseReason);
        }

        private static List<CellPos> PlaceableCells(GameSession session)
        {
            var cells = new List<CellPos>();
            foreach (CellPos p in session.Board.AllPositions())
                if (session.Board.IsPlaceable(p)) cells.Add(p);
            return cells;
        }

        private static CellPos ChooseRandom(GameSession session, SeededRng rng)
        {
            List<CellPos> cells = PlaceableCells(session);
            return cells[rng.NextInt(0, cells.Count)];
        }

        private static CellPos ChooseGreedy(GameSession session, SeededRng rng)
        {
            Rank current = session.CurrentCard.Rank;
            var best = new List<CellPos>();
            int bestScore = int.MinValue;
            int bestNeighbors = int.MinValue;

            foreach (CellPos p in PlaceableCells(session))
            {
                int score = session.PreviewPlacement(p).ScoreGain;
                int neighbors = 0;
                foreach (CellPos n in p.Neighbors4())
                {
                    Card c = session.Board.GetCard(n);
                    if (c != null && c.Rank == current) neighbors++;
                }

                if (score > bestScore || (score == bestScore && neighbors > bestNeighbors))
                {
                    best.Clear();
                    bestScore = score;
                    bestNeighbors = neighbors;
                }
                if (score == bestScore && neighbors == bestNeighbors) best.Add(p);
            }
            return best[rng.NextInt(0, best.Count)];
        }
    }
}
