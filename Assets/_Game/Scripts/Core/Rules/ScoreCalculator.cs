using System;
using SuitMerge.Core.Config;
using SuitMerge.Core.Model;

namespace SuitMerge.Core.Rules
{
    /// <summary>
    /// Công thức điểm (spec 4.1):
    /// MergeScore = RankValue(newRank) × N × BASE × SuitMult × ChainMult
    /// ChainMult  = 1 + chainBonusPerStep × (k − 1)
    /// </summary>
    public static class ScoreCalculator
    {
        public static double ChainMultiplier(int chainStep, GameConfig config)
        {
            if (chainStep < 1) throw new ArgumentOutOfRangeException(nameof(chainStep));
            return 1.0 + config.chainBonusPerStep * (chainStep - 1);
        }

        public static int MergeScore(Rank newRank, int groupSize, int suitMultiplier, int chainStep, GameConfig config)
        {
            double raw = newRank.Value() * groupSize * config.scoreBase * suitMultiplier
                         * ChainMultiplier(chainStep, config);
            return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        }

        /// <summary>Tạo Ace: điểm cố định, không nhân hệ số nào (spec 4.6).</summary>
        public static int AceScore(GameConfig config) => config.aceBonus;
    }
}
