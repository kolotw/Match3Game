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
        private Board.MatchInfo CheckTLShapeMatch(int centerX, int centerY)
        {
            Gem centerGem = board.GetGem(centerX, centerY);
            if (centerGem == null) return null;

            // 檢查四個方向的連續匹配
            List<Gem> leftGems = new List<Gem>();
            List<Gem> rightGems = new List<Gem>();
            List<Gem> upGems = new List<Gem>();
            List<Gem> downGems = new List<Gem>();

            // 向左檢查
            for (int x = centerX - 1; x >= 0; x--)
            {
                Gem gem = board.GetGem(x, centerY);
                if (gem?.id == centerGem.id)
                    leftGems.Add(gem);
                else
                    break;
            }

            // 向右檢查
            for (int x = centerX + 1; x < board.width; x++)
            {
                Gem gem = board.GetGem(x, centerY);
                if (gem?.id == centerGem.id)
                    rightGems.Add(gem);
                else
                    break;
            }

            // 向上檢查
            for (int y = centerY - 1; y >= 0; y--)
            {
                Gem gem = board.GetGem(centerX, y);
                if (gem?.id == centerGem.id)
                    upGems.Add(gem);
                else
                    break;
            }

            // 向下檢查
            for (int y = centerY + 1; y < board.height; y++)
            {
                Gem gem = board.GetGem(centerX, y);
                if (gem?.id == centerGem.id)
                    downGems.Add(gem);
                else
                    break;
            }

            // 檢查是否形成T型或L型
            bool hasHorizontalMatch = leftGems.Count + rightGems.Count >= 2;
            bool hasVerticalMatch = upGems.Count + downGems.Count >= 2;

            // T型檢查 (三個方向都有至少一個匹配)
            bool isTShape = (leftGems.Count >= 1 && rightGems.Count >= 1 && (upGems.Count >= 2 || downGems.Count >= 2)) ||
                           (upGems.Count >= 1 && downGems.Count >= 1 && (leftGems.Count >= 2 || rightGems.Count >= 2));

            // L型檢查 (兩個垂直方向有至少兩個匹配)
            bool isLShape = (hasHorizontalMatch && hasVerticalMatch) &&
                           ((leftGems.Count >= 2 && (upGems.Count >= 2 || downGems.Count >= 2)) ||
                            (rightGems.Count >= 2 && (upGems.Count >= 2 || downGems.Count >= 2)));

            if (isTShape || isLShape)
            {
                var specialMatch = new Board.MatchInfo
                {
                    matchedGems = new List<Gem> { centerGem }
                };
                specialMatch.matchedGems.AddRange(leftGems);
                specialMatch.matchedGems.AddRange(rightGems);
                specialMatch.matchedGems.AddRange(upGems);
                specialMatch.matchedGems.AddRange(downGems);
                specialMatch.isHorizontal = true;
                specialMatch.isVertical = true;
                return specialMatch;
            }

            return null;
        }
        public List<Board.MatchInfo> FindAllMatches()
        {
            List<Board.MatchInfo> allMatches = new List<Board.MatchInfo>();
            HashSet<(int x, int y)> processedPositions = new HashSet<(int x, int y)>();

            // 先檢查特殊形狀（十字、T型和L型），因為這些可能產生特殊寶石
            for (int x = 0; x < board.width; x++)
            {
                for (int y = 0; y < board.height; y++)
                {
                    if (board.gems[x, y] == null) continue;

                    // 檢查十字形匹配
                    var specialCrossMatch = CheckSpecialCrossMatch(x, y);
                    if (specialCrossMatch != null)
                    {
                        allMatches.Add(specialCrossMatch);
                        foreach (var gem in specialCrossMatch.matchedGems)
                        {
                            processedPositions.Add((gem.x, gem.y));
                        }
                        continue; // 找到十字形就跳過其他檢查
                    }

                    // 檢查 T型和L型匹配
                    var tlMatch = CheckTLShapeMatch(x, y);
                    if (tlMatch != null)
                    {
                        allMatches.Add(tlMatch);
                        foreach (var gem in tlMatch.matchedGems)
                        {
                            processedPositions.Add((gem.x, gem.y));
                        }
                        continue; // 找到T型或L型就跳過其他檢查
                    }
                }
            }

            // 檢查水平方向的普通匹配
            for (int y = 0; y < board.height; y++)
            {
                for (int x = 0; x < board.width - 2; x++)
                {
                    if (board.gems[x, y] == null || processedPositions.Contains((x, y))) continue;

                    Board.MatchInfo match = CheckMatch(x, y, true);
                    if (match != null)
                    {
                        allMatches.Add(match);
                        foreach (var gem in match.matchedGems)
                        {
                            processedPositions.Add((gem.x, gem.y));
                        }
                    }
                }
            }

            // 檢查垂直方向的普通匹配
            for (int x = 0; x < board.width; x++)
            {
                for (int y = 0; y < board.height - 2; y++)
                {
                    if (board.gems[x, y] == null || processedPositions.Contains((x, y))) continue;

                    Board.MatchInfo match = CheckMatch(x, y, false);
                    if (match != null)
                    {
                        allMatches.Add(match);
                        foreach (var gem in match.matchedGems)
                        {
                            processedPositions.Add((gem.x, gem.y));
                        }
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