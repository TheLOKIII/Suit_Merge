namespace SuitMerge.Core.Config
{
    /// <summary>
    /// Hằng số luật chơi (spec mục 4.1 + các quyết định đã chốt).
    /// Giá trị mặc định ở đây; bản chạy thật được nạp từ Data/Config/game_config.json.
    /// Field public để JsonUtility (phía Unity) đọc được.
    /// </summary>
    [System.Serializable]
    public class GameConfig
    {
        // Điểm
        public int scoreBase = 10;
        public int diamondMultiplier = 3;
        public float chainBonusPerStep = 0.5f;
        public int aceBonus = 100000;
        public int heartOverflowPerRank = 500;
        public int spadeClearPerRankValue = 10;
        public int stoneBreakPerHp = 50;

        // Luật có thể đổi (spec mục 14)
        public bool clubSpawnJoinsChain = true;   // #3
        public bool createRankCountsHigher = true; // #8: goal "Tạo lá X" tính rank >= X
        public bool createRankCountsClubSpawn = true; // #8
        // 18/10/2026 "Tính 1": goal "Tạo lá X" chỉ đếm lá tạo trong lượt CÒN trên bàn cuối lượt (chain 4→5 = 1).
        // true = mỗi lá tạo ra trong chain đều tính (đúng chữ spec 6).
        public bool createRankCountsEveryChainStep = false;
        public int bombDangerThreshold = 3;

        // Level (spec 12.1 [GIẢ ĐỊNH]): lượt có ô gợi ý (hint) chỉ cho thả vào đúng ô đó
        public bool tutorialHintLocksDrop = true;

        // Khối đôi (spec 4.9, #16/#17): resolve ① trước ②, ① ② chung nhóm thì neo ①. Hiện chỉ có "FIRST_THEN_SECOND".
        public string pairResolveOrder = "FIRST_THEN_SECOND";
        // Tỉ lệ lượt Endless rút ra khối đôi (spec 5.2: 30%). M4.5 phần A để 0 — giao diện chưa vẽ được khối đôi;
        // phần B mới bật 0.3 trong game_config.json.
        public float endlessPairRatio = 0f;

        // Sinh lá Endless (spec 5.2): H = rank cao nhất trên bàn (bàn trống → endlessStartHighest)
        // rank rút đều trong [max(2, H − endlessLowOffset), max(endlessMinHigh, H − endlessHighOffset)]
        public int endlessStartHighest = 4;
        public int endlessLowOffset = 4;
        public int endlessHighOffset = 1;
        public int endlessMinHigh = 4;

        public static GameConfig Default => new GameConfig();
    }
}
