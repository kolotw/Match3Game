using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3Game
{
    public static class MatchUtils
    {
        // �q�Ϊ���V�ˬd��k
        public static List<Gem> CheckDirection(Board board, int startX, int startY, bool isHorizontal)
        {
            List<Gem> matches = new List<Gem>();
            Gem startGem = board.GetGem(startX, startY);
            if (startGem == null) return matches;

            // �p�����
            int maxRange = isHorizontal ? board.width : board.height;

            // �V����V�d��
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

            // �V�t��V�d��
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

        // �ˬd�S�w��m�O�_�|�b�洫��Φ��ǰt
        public static bool CheckMatchAfterSwap(Board board, int x, int y)
        {
            Gem currentGem = board.GetGem(x, y);
            if (currentGem == null) return false;

            // �����ˬd
            if (CheckHorizontalMatch(board, x, y)) return true;
            // �����ˬd
            if (CheckVerticalMatch(board, x, y)) return true;

            return false;
        }

        // �ˬd�����ǰt
        private static bool CheckHorizontalMatch(Board board, int x, int y)
        {
            Gem currentGem = board.GetGem(x, y);
            if (currentGem == null) return false;

            // �������
            if (x >= 2 &&
                board.GetGem(x - 2, y)?.id == board.GetGem(x - 1, y)?.id &&
                board.GetGem(x - 1, y)?.id == currentGem.id)
                return true;

            // ����
            if (x >= 1 && x < board.width - 1 &&
                board.GetGem(x - 1, y)?.id == currentGem.id &&
                currentGem.id == board.GetGem(x + 1, y)?.id)
                return true;

            // �k�����
            if (x < board.width - 2 &&
                currentGem.id == board.GetGem(x + 1, y)?.id &&
                board.GetGem(x + 1, y)?.id == board.GetGem(x + 2, y)?.id)
                return true;

            return false;
        }

        // �ˬd�����ǰt
        private static bool CheckVerticalMatch(Board board, int x, int y)
        {
            Gem currentGem = board.GetGem(x, y);
            if (currentGem == null) return false;

            // �U����
            if (y >= 2 &&
                board.GetGem(x, y - 2)?.id == board.GetGem(x, y - 1)?.id &&
                board.GetGem(x, y - 1)?.id == currentGem.id)
                return true;

            // ����
            if (y >= 1 && y < board.height - 1 &&
                board.GetGem(x, y - 1)?.id == currentGem.id &&
                currentGem.id == board.GetGem(x, y + 1)?.id)
                return true;

            // �W����
            if (y < board.height - 2 &&
                currentGem.id == board.GetGem(x, y + 1)?.id &&
                board.GetGem(x, y + 1)?.id == board.GetGem(x, y + 2)?.id)
                return true;

            return false;
        }

        // �ˬd�Q�r�Τǰt
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

        // �ˬdT�ΩML�Τǰt
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