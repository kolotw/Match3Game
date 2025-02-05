using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Game
{
    public static class MatchUtils
    {
        // 通用的方向檢查方法
        public static List<Gem> CheckDirection(Board board, int startX, int startY, bool isHorizontal)
        {
            List<Gem> matches = new List<Gem>();
            Gem startGem = board.GetGem(startX, startY);
            if (startGem == null) return matches;

            // 計算邊界
            int maxRange = isHorizontal ? board.width : board.height;

            // 向正方向查找
            for (int i = isHorizontal ? startX : startY; i < maxRange; i++)
            {
                Gem currentGem = isHorizontal ?
                    board.GetGem(i, startY) :
                    board.GetGem(startX, i);

                if (currentGem?.id == startGem.id)
                    matches.Add(currentGem);
                else
                    break;
            }

            // 向負方向查找
            for (int i = (isHorizontal ? startX : startY) - 1; i >= 0; i--)
            {
                Gem currentGem = isHorizontal ?
                    board.GetGem(i, startY) :
                    board.GetGem(startX, i);

                if (currentGem?.id == startGem.id)
                    matches.Add(currentGem);
                else
                    break;
            }

            return matches;
        }

        // 檢查特定位置是否會在交換後形成匹配
        public static bool CheckMatchAfterSwap(Board board, int x, int y)
        {
            Gem currentGem = board.GetGem(x, y);
            if (currentGem == null) return false;

            // 水平檢查
            if (CheckHorizontalMatch(board, x, y)) return true;
            // 垂直檢查
            if (CheckVerticalMatch(board, x, y)) return true;

            return false;
        }

        // 檢查水平匹配
        private static bool CheckHorizontalMatch(Board board, int x, int y)
        {
            Gem currentGem = board.GetGem(x, y);
            if (currentGem == null) return false;

            // 左側兩個
            if (x >= 2 &&
                board.GetGem(x - 2, y)?.id == board.GetGem(x - 1, y)?.id &&
                board.GetGem(x - 1, y)?.id == currentGem.id)
                return true;

            // 中間
            if (x >= 1 && x < board.width - 1 &&
                board.GetGem(x - 1, y)?.id == currentGem.id &&
                currentGem.id == board.GetGem(x + 1, y)?.id)
                return true;

            // 右側兩個
            if (x < board.width - 2 &&
                currentGem.id == board.GetGem(x + 1, y)?.id &&
                board.GetGem(x + 1, y)?.id == board.GetGem(x + 2, y)?.id)
                return true;

            return false;
        }

        // 檢查垂直匹配
        private static bool CheckVerticalMatch(Board board, int x, int y)
        {
            Gem currentGem = board.GetGem(x, y);
            if (currentGem == null) return false;

            // 下方兩個
            if (y >= 2 &&
                board.GetGem(x, y - 2)?.id == board.GetGem(x, y - 1)?.id &&
                board.GetGem(x, y - 1)?.id == currentGem.id)
                return true;

            // 中間
            if (y >= 1 && y < board.height - 1 &&
                board.GetGem(x, y - 1)?.id == currentGem.id &&
                currentGem.id == board.GetGem(x, y + 1)?.id)
                return true;

            // 上方兩個
            if (y < board.height - 2 &&
                currentGem.id == board.GetGem(x, y + 1)?.id &&
                board.GetGem(x, y + 1)?.id == board.GetGem(x, y + 2)?.id)
                return true;

            return false;
        }

        // 檢查十字形匹配
        public static Board.MatchInfo CheckCrossMatch(Board board, int centerX, int centerY)
        {
            var horizontalMatches = CheckDirection(board, centerX, centerY, true);
            var verticalMatches = CheckDirection(board, centerX, centerY, false);

            if (horizontalMatches.Count >= 3 && verticalMatches.Count >= 3)
            {
                var crossMatch = new Board.MatchInfo
                {
                    matchedGems = horizontalMatches.Union(verticalMatches).ToList(),
                    isHorizontal = true,
                    isVertical = true
                };

                return crossMatch;
            }

            return null;
        }

        // 檢查T形和L形匹配
        public static Board.MatchInfo CheckTLShapeMatch(Board board, int centerX, int centerY)
        {
            var horizontalMatches = CheckDirection(board, centerX, centerY, true);
            var verticalMatches = CheckDirection(board, centerX, centerY, false);

            bool hasHorizontalMatch = horizontalMatches.Count >= 3;
            bool hasVerticalMatch = verticalMatches.Count >= 3;

            if (hasHorizontalMatch && hasVerticalMatch)
            {
                var tlMatch = new Board.MatchInfo
                {
                    matchedGems = horizontalMatches.Union(verticalMatches).ToList(),
                    isHorizontal = true,
                    isVertical = true
                };

                return tlMatch;
            }

            return null;
        }
    }
}