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
            var horizontal = MatchUtils.CheckDirection(board, centerX, centerY, true);
            var vertical = MatchUtils.CheckDirection(board, centerX, centerY, false);

            if (horizontal.Count >= 3 && vertical.Count >= 3)
            {
                var allGems = horizontal.Union(vertical).ToList();
                bool hasExtension;
                MatchUtils.ShapeType shapeType;

                if (MatchUtils.IsTLShape(allGems, out hasExtension, out shapeType))
                {
                    var (resourceType, _, _, _) = MatchUtils.確認特殊寶石類別(allGems, centerX, centerY);
                    if (resourceType >= 0)  // 確認是有效的特殊寶石類型
                    {
                        return new Board.MatchInfo
                        {
                            matchedGems = allGems,
                            isHorizontal = true,
                            isVertical = true
                        };
                    }
                }
            }

            return null;
        }

        public List<Board.MatchInfo> FindAllMatches()
        {
            List<Board.MatchInfo> allMatches = new List<Board.MatchInfo>();
            HashSet<(int x, int y)> processedPositions = new HashSet<(int x, int y)>();

            // 先檢查特殊形狀
            for (int x = 0; x < board.width; x++)
            {
                for (int y = 0; y < board.height; y++)
                {
                    if (board.gems[x, y] == null || processedPositions.Contains((x, y)))
                        continue;

                    // 優先檢查 T/L 型匹配
                    var tlMatch = CheckTLShapeMatch(x, y);
                    if (tlMatch != null)
                    {
                        allMatches.Add(tlMatch);
                        foreach (var gem in tlMatch.matchedGems)
                        {
                            processedPositions.Add((gem.x, gem.y));
                        }
                        continue;
                    }

                    // 其次檢查十字形匹配
                    var specialCrossMatch = CheckSpecialCrossMatch(x, y);
                    if (specialCrossMatch != null)
                    {
                        allMatches.Add(specialCrossMatch);
                        foreach (var gem in specialCrossMatch.matchedGems)
                        {
                            processedPositions.Add((gem.x, gem.y));
                        }
                        continue;
                    }
                }
            }

            // 最後檢查普通匹配
            CheckNormalMatches(allMatches, processedPositions);

            return allMatches;
        }
        private Board.MatchInfo CheckSpecialCrossMatch(int centerX, int centerY)
        {
            // 檢查十字形，checkTLShape 設為 false
            return MatchUtils.CheckSpecialMatch(board, centerX, centerY, false);
        }

        

        private void CheckNormalMatches(List<Board.MatchInfo> allMatches, HashSet<(int x, int y)> processedPositions)
        {
            // 水平檢查
            for (int y = 0; y < board.height; y++)
            {
                for (int x = 0; x < board.width - 2; x++)
                {
                    if (board.gems[x, y] == null || processedPositions.Contains((x, y))) continue;

                    var match = CheckMatch(x, y, true);
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

            // 垂直檢查
            for (int x = 0; x < board.width; x++)
            {
                for (int y = 0; y < board.height - 2; y++)
                {
                    if (board.gems[x, y] == null || processedPositions.Contains((x, y))) continue;

                    var match = CheckMatch(x, y, false);
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
        }

        private Board.MatchInfo CheckMatch(int startX, int startY, bool horizontal)
        {
            var matches = MatchUtils.CheckDirection(board, startX, startY, horizontal);
            if (matches.Count >= 3)
            {
                return new Board.MatchInfo
                {
                    matchedGems = matches,
                    isHorizontal = horizontal,
                    isVertical = !horizontal
                };
            }
            return null;
        }

        public bool CheckMatchAfterSwap(int x, int y)
        {
            return MatchUtils.CheckMatchAfterSwap(board, x, y);
        }
    }
}