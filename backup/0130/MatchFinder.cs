using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Game
{
    public class MatchFinder
    {
        private Board board;

        public MatchFinder(Board board)
        {
            this.board = board;
        }

        public List<Board.MatchInfo> FindAllMatches()
        {
            List<Board.MatchInfo> allMatches = new List<Board.MatchInfo>();

            // 用於追蹤已經被檢查過的寶石，避免重複處理
            bool[,] processed = new bool[board.width, board.height];

            // 水平方向配對檢測
            for (int y = 0; y < board.height; y++)
            {
                for (int x = 0; x < board.width - 2; x++)
                {
                    // 跳過已處理或空的寶石
                    if (processed[x, y] || board.gems[x, y] == null) continue;

                    Board.MatchInfo match = CheckMatch(x, y, true);
                    if (match != null)
                    {
                        // 標記已處理的寶石
                        foreach (var gem in match.matchedGems)
                        {
                            processed[gem.x, gem.y] = true;
                        }
                        allMatches.Add(match);
                    }
                }
            }

            // 重置已處理標記，為垂直檢測做準備
            processed = new bool[board.width, board.height];

            // 垂直方向配對檢測
            for (int x = 0; x < board.width; x++)
            {
                for (int y = 0; y < board.height - 2; y++)
                {
                    // 跳過已處理或空的寶石
                    if (processed[x, y] || board.gems[x, y] == null) continue;

                    Board.MatchInfo match = CheckMatch(x, y, false);
                    if (match != null)
                    {
                        // 標記已處理的寶石
                        foreach (var gem in match.matchedGems)
                        {
                            processed[gem.x, gem.y] = true;
                        }
                        allMatches.Add(match);
                    }
                }
            }

            // 特殊的十字配對檢測
            for (int x = 0; x < board.width; x++)
            {
                for (int y = 0; y < board.height; y++)
                {
                    var specialCrossMatch = CheckSpecialCrossMatch(x, y);
                    if (specialCrossMatch != null)
                    {
                        allMatches.Add(specialCrossMatch);
                    }
                }
            }

            return allMatches;
        }
        private Board.MatchInfo CheckSpecialCrossMatch(int centerX, int centerY)
        {
            Gem centerGem = board.GetGem(centerX, centerY);
            if (centerGem == null) return null;

            // 水平方向連續匹配檢查
            List<Gem> horizontalMatchGems = new List<Gem>();
            int leftX = centerX - 1;
            int rightX = centerX + 1;

            // 向左檢查連續匹配
            while (leftX >= 0)
            {
                Gem leftGem = board.GetGem(leftX, centerY);
                if (leftGem != null && leftGem.id == centerGem.id)
                {
                    horizontalMatchGems.Add(leftGem);
                    leftX--;
                }
                else
                {
                    break;
                }
            }

            // 向右檢查連續匹配
            while (rightX < board.width)
            {
                Gem rightGem = board.GetGem(rightX, centerY);
                if (rightGem != null && rightGem.id == centerGem.id)
                {
                    horizontalMatchGems.Add(rightGem);
                    rightX++;
                }
                else
                {
                    break;
                }
            }

            // 垂直方向連續匹配檢查
            List<Gem> verticalMatchGems = new List<Gem>();
            int upY = centerY - 1;
            int downY = centerY + 1;

            // 向上檢查連續匹配
            while (upY >= 0)
            {
                Gem upGem = board.GetGem(centerX, upY);
                if (upGem != null && upGem.id == centerGem.id)
                {
                    verticalMatchGems.Add(upGem);
                    upY--;
                }
                else
                {
                    break;
                }
            }

            // 向下檢查連續匹配
            while (downY < board.height)
            {
                Gem downGem = board.GetGem(centerX, downY);
                if (downGem != null && downGem.id == centerGem.id)
                {
                    verticalMatchGems.Add(downGem);
                    downY++;
                }
                else
                {
                    break;
                }
            }

            // 嚴格的十字配對判斷
            bool isCrossMatch =
                // 至少3個連續的水平匹配
                horizontalMatchGems.Count >= 2 &&
                // 至少3個連續的垂直匹配
                verticalMatchGems.Count >= 2;

            if (isCrossMatch)
            {
                var crossMatch = new Board.MatchInfo
                {
                    matchedGems = new List<Gem> { centerGem }
                };
                crossMatch.matchedGems.AddRange(horizontalMatchGems);
                crossMatch.matchedGems.AddRange(verticalMatchGems);
                crossMatch.isHorizontal = true;
                crossMatch.isVertical = true;
                return crossMatch;
            }

            return null;
        }
        private Board.MatchInfo CheckMatch(int startX, int startY, bool horizontal)
        {
            Board.MatchInfo match = new Board.MatchInfo();
            int currentId = board.gems[startX, startY].id;
            int x = startX, y = startY;

            while (true)
            {
                if (horizontal)
                {
                    if (x >= board.width || board.gems[x, y] == null || board.gems[x, y].id != currentId)
                        break;
                    match.matchedGems.Add(board.gems[x, y]);
                    x++;
                }
                else
                {
                    if (y >= board.height || board.gems[x, y] == null || board.gems[x, y].id != currentId)
                        break;
                    match.matchedGems.Add(board.gems[x, y]);
                    y++;
                }
            }

            if (match.matchCount >= 3)
            {
                match.isHorizontal = horizontal;
                match.isVertical = !horizontal;
                return match;
            }

            return null;
        }

        public bool CheckMatchAfterSwap(int x, int y)
        {
            Gem currentGem = board.GetGem(x, y);
            if (currentGem == null) return false;

            if (x >= 2 &&
                board.GetGem(x - 2, y)?.id == board.GetGem(x - 1, y)?.id &&
                board.GetGem(x - 1, y)?.id == currentGem.id)
                return true;

            if (x >= 1 && x < board.width - 1 &&
                board.GetGem(x - 1, y)?.id == currentGem.id &&
                currentGem.id == board.GetGem(x + 1, y)?.id)
                return true;

            if (x < board.width - 2 &&
                currentGem.id == board.GetGem(x + 1, y)?.id &&
                board.GetGem(x + 1, y)?.id == board.GetGem(x + 2, y)?.id)
                return true;

            if (y >= 2 &&
                board.GetGem(x, y - 2)?.id == board.GetGem(x, y - 1)?.id &&
                board.GetGem(x, y - 1)?.id == currentGem.id)
                return true;

            if (y >= 1 && y < board.height - 1 &&
                board.GetGem(x, y - 1)?.id == currentGem.id &&
                currentGem.id == board.GetGem(x, y + 1)?.id)
                return true;

            if (y < board.height - 2 &&
                currentGem.id == board.GetGem(x, y + 1)?.id &&
                board.GetGem(x, y + 1)?.id == board.GetGem(x, y + 2)?.id)
                return true;

            return false;
        }
    }
}