using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Match3Game
{
    public static class MatchUtils
    {
        // �S���_�۲զX�t�m
        private static readonly Dictionary<(int, int), int> SpecialGemCombinations = new Dictionary<(int, int), int>
        {
            {(100, 100), 104}, // LineH + LineH = Cross
            {(101, 101), 104}, // LineV + LineV = Cross
            {(102, 102), 105}, // Bomb + Bomb = BigBomb
            {(103, 103), 106}, // Rainbow + Rainbow = DestroyAll
            {(100, 101), 104}, // LineH + LineV = Cross
            {(100, 102), 110}, // LineH + Bomb = ThreeHLines
            {(100, 103), 107}, // LineH + Rainbow = RandomHLines
            {(101, 102), 111}, // LineV + Bomb = ThreeVLines
            {(101, 103), 108}, // LineV + Rainbow = RandomVLines
            {(102, 103), 109}  // Bomb + Rainbow = MultiBomb
        };

        public enum ShapeType
        {
            None,
            T,
            L
        }

        // �ˬd���w��V���s���_��
        public static List<Gem> CheckDirection(Board board, int x, int y, bool isHorizontal)
        {
            var matches = new List<Gem>();
            Gem centerGem = board.GetGem(x, y);
            if (centerGem == null) return matches;

            int max = isHorizontal ? board.width : board.height;
            Func<int, Gem> getGem = i => isHorizontal ? board.GetGem(i, y) : board.GetGem(x, i);

            // �V�e�ˬd
            for (int i = (isHorizontal ? x : y); i < max; i++)
            {
                var gem = getGem(i);
                if (gem?.id == centerGem.id)
                    matches.Add(gem);
                else
                    break;
            }

            // �V���ˬd
            for (int i = (isHorizontal ? x : y) - 1; i >= 0; i--)
            {
                var gem = getGem(i);
                if (gem?.id == centerGem.id)
                    matches.Add(gem);
                else
                    break;
            }

            return matches;
        }

        // �ˬd�S��Ϊ��ǰt
        public static Board.MatchInfo CheckSpecialMatch(Board board, int x, int y, bool checkTLShape = true)
        {
            var horizontal = CheckDirection(board, x, y, true);
            var vertical = CheckDirection(board, x, y, false);

            bool hasHorizontal = horizontal.Count >= 3;
            bool hasVertical = vertical.Count >= 3;

            if (hasHorizontal && hasVertical)
            {
                var matchedGems = horizontal.Union(vertical).ToList();
                bool hasExtension;
                ShapeType shapeType;

                // �ϥηs�� TL �Ϊ��ˬd�޿�
                if (checkTLShape && !IsTLShape(matchedGems, out hasExtension, out shapeType))
                {
                    return null;
                }

                return new Board.MatchInfo
                {
                    matchedGems = matchedGems,
                    isHorizontal = true,
                    isVertical = true
                };
            }

            return null;
        }

        // �ˬd�洫�᪺�ǰt
        public static bool CheckMatchAfterSwap(Board board, int x, int y)
        {
            Gem centerGem = board.GetGem(x, y);
            if (centerGem == null) return false;

            return IsMatchInDirection(board, x, y, true) ||
                   IsMatchInDirection(board, x, y, false);
        }

        // �ˬd�O�_��T����L�����D�n��k
        public static bool IsTLShape(List<Gem> gems, out bool hasExtension, out ShapeType shapeType)
        {
            hasExtension = false;
            shapeType = ShapeType.None;

            if (gems == null || gems.Count < 5)
                return false;

            // ��C�ӥi�઺�����I�ˬd
            foreach (var centerGem in gems)
            {
                var horizontal = gems.Where(g => g.y == centerGem.y)
                                   .OrderBy(g => g.x)
                                   .ToList();
                var vertical = gems.Where(g => g.x == centerGem.x)
                                 .OrderBy(g => g.y)
                                 .ToList();

                // �ˬdT��
                if (IsTShape(horizontal, vertical, out hasExtension))
                {
                    shapeType = ShapeType.T;
                    return true;
                }

                // �ˬdL��
                if (IsLShape(horizontal, vertical))
                {
                    shapeType = ShapeType.L;
                    return true;
                }
            }

            return false;
        }

        // �ˬd�O�_��T��
        private static bool IsTShape(List<Gem> horizontal, List<Gem> vertical, out bool hasExtension)
        {
            hasExtension = false;
            //Debug.Log($"�ˬdT�� - �����ƶq: {horizontal.Count}, �����ƶq: {vertical.Count}");

            // ���ˬd�O�_��5�s�u�A�p�G���N���OT��
            if (horizontal.Count >= 5)
            {
                //Debug.Log("�o�{5�s�u�A���OT��");
                return false;
            }

            // ��T���P�_ (3x3)
            if (horizontal.Count == 3 && vertical.Count == 3)
            {
                var allGems = horizontal.Union(vertical).Distinct().ToList();
                if (allGems.Count == 5) // �T�{�O�u����T���]�`�@5�Ӥ����ƪ��_�ۡ^
                {
                    //Debug.Log("�o�{��T��(3x3)");
                    return true;
                }
            }

            // �X�iT���P�_ (4x3 �� 3x4)
            if ((horizontal.Count == 4 && vertical.Count >= 3) ||
                (horizontal.Count >= 3 && vertical.Count == 4))
            {
                // �T�{�Ϊ��O�s��
                if (AreGemsConsecutive(horizontal) && AreGemsConsecutive(vertical))
                {
                    hasExtension = true;
                    //Debug.Log("�o�{�X�iT��");
                    return true;
                }
            }

            //Debug.Log("���ŦXT������");
            return false;
        }

        // �ˬd�O�_��L��
        private static bool IsLShape(List<Gem> horizontal, List<Gem> vertical)
        {
            // L�������O3x3
            if (horizontal.Count != 3 || vertical.Count != 3)
                return false;

            // �ˬd�O�_�@�Τ@�Ө���
            var intersection = horizontal.Intersect(vertical).ToList();
            if (intersection.Count != 1)
                return false;

            var cornerGem = intersection[0];

            // �T�{corner�O�b�����M�����u�����I
            bool isAtHorizontalEnd = cornerGem.x == horizontal.First().x ||
                                   cornerGem.x == horizontal.Last().x;
            bool isAtVerticalEnd = cornerGem.y == vertical.First().y ||
                                 cornerGem.y == vertical.Last().y;

            return isAtHorizontalEnd && isAtVerticalEnd;
        }

        // �ˬd�_�۬O�_�s��
        private static bool AreGemsConsecutive(List<Gem> gems)
        {
            if (gems.Count < 2)
                return true;

            bool isHorizontal = gems[0].y == gems[1].y;

            for (int i = 1; i < gems.Count; i++)
            {
                if (isHorizontal)
                {
                    if (gems[i].x != gems[i - 1].x + 1)
                        return false;
                }
                else
                {
                    if (gems[i].y != gems[i - 1].y + 1)
                        return false;
                }
            }

            return true;
        }

        // �ˬd�S���_�۲զX
        public static (bool success, int resultType) CheckSpecialGemCombination(Gem first, Gem second)
        {
            if (first == null || second == null || first.id < 100 || second.id < 100)
                return (false, -1);

            var key = (Math.Min(first.id, second.id), Math.Max(first.id, second.id));
            return SpecialGemCombinations.TryGetValue(key, out int resultId)
                ? (true, resultId)
                : (false, -1);
        }

        // �ˬd�@�Ӥ�V�W���ǰt
        private static bool IsMatchInDirection(Board board, int x, int y, bool isHorizontal)
        {
            Gem centerGem = board.GetGem(x, y);
            int pos = isHorizontal ? x : y;
            int max = isHorizontal ? board.width : board.height;
            Func<int, Gem> getGem = i => isHorizontal ? board.GetGem(i, y) : board.GetGem(x, i);

            return (pos >= 2 && getGem(pos - 2)?.id == getGem(pos - 1)?.id && getGem(pos - 1)?.id == centerGem.id) ||
                   (pos >= 1 && pos < max - 1 && getGem(pos - 1)?.id == centerGem.id && centerGem.id == getGem(pos + 1)?.id) ||
                   (pos < max - 2 && centerGem.id == getGem(pos + 1)?.id && getGem(pos + 1)?.id == getGem(pos + 2)?.id);
        }

        // �M��s���_�۸s��
        public static List<List<Gem>> FindContinuousGemGroups(IGrouping<int, Gem> group)
        {
            var gems = group.ToList();
            var result = new List<List<Gem>>();
            Debug.Log($"�}�l�B�z�s�աA�`�_�ۼ�: {gems.Count}");

            // 1. ���ˬd T/L ��
            bool hasExtension;
            ShapeType shapeType;
            if (IsTLShape(gems, out hasExtension, out shapeType))
            {
                Debug.Log($"���S��Ϊ�: {shapeType}, ����: {hasExtension}");
                result.Add(gems);
                return result;
            }

            // 2. �A�ˬd 5 �s�u
            int maxHorizontal = CheckLine(gems, true);
            int maxVertical = CheckLine(gems, false);

            if (maxHorizontal >= 5 || maxVertical >= 5)
            {
                Debug.Log($"���5�s�u - ����: {maxHorizontal}, ����: {maxVertical}");
                result.Add(gems);
                return result;
            }

            // 3. �̫�~�B�z 4 �s�u
            var horizontalLines = new List<List<Gem>>();
            var verticalLines = new List<List<Gem>>();

            foreach (var line in gems.GroupBy(g => g.y))
            {
                var sorted = line.OrderBy(g => g.x).ToList();
                var groups = GetConsecutiveGroups(sorted, true);
                horizontalLines.AddRange(groups.Where(g => g.Count == 4));
            }

            foreach (var line in gems.GroupBy(g => g.x))
            {
                var sorted = line.OrderBy(g => g.y).ToList();
                var groups = GetConsecutiveGroups(sorted, false);
                verticalLines.AddRange(groups.Where(g => g.Count == 4));
            }

            result.AddRange(horizontalLines);
            result.AddRange(verticalLines);

            //Debug.Log($"��� 4 �s�u - ����: {horizontalLines.Count}, ����: {verticalLines.Count}");
            return result.Distinct(new ListComparer<Gem>()).ToList();
        }

        // �ˬd�S���_������
        public static (int resourceType, bool isHorizontal, bool isVertical, List<Gem> matchedGems)
    �T�{�S���_�����O(List<Gem> gems, int triggerX = 0, int triggerY = 0)
        {
            if (gems == null || gems.Count < 4)
            {
                return (-1, false, false, new List<Gem>());
            }

            // ���ˬd�O�_��5�s�u
            int maxHorizontal = CheckLine(gems, true);
            int maxVertical = CheckLine(gems, false);

            // �u���B�z5�s�u
            if (maxHorizontal >= 5)
            {
                Debug.Log($"�T�{�F����5�s�u");
                return (3, true, false, gems);
            }
            if (maxVertical >= 5)
            {
                Debug.Log($"�T�{�F����5�s�u");
                return (3, false, true, gems);
            }

            // �ˬdT/L��
            bool hasExtension;
            ShapeType shapeType;
            if (IsTLShape(gems, out hasExtension, out shapeType))
            {
                Debug.Log($"�T�{�F{shapeType}���A�X�i:{hasExtension}");
                return (hasExtension ? 3 : 2, true, true, gems);
            }

            // �̫�B�z4�s�u
            if (maxHorizontal == 4)
            {
                Debug.Log($"�T�{�F����4�s�u");
                return (0, true, false, gems);
            }
            if (maxVertical == 4)
            {
                Debug.Log($"�T�{�F����4�s�u");
                return (1, false, true, gems);
            }

            return (-1, false, false, gems);
        }

        // ���U��k�G���o�s���_�۸s��
        private static List<List<Gem>> GetConsecutiveGroups(List<Gem> gems, bool isHorizontal)
        {
            var groups = new List<List<Gem>>();
            var current = new List<Gem>();

            for (int i = 0; i < gems.Count; i++)
            {
                if (current.Count == 0)
                {
                    current.Add(gems[i]);
                }
                else if ((isHorizontal && gems[i].x == current.Last().x + 1) ||
                         (!isHorizontal && gems[i].y == current.Last().y + 1))
                {
                    current.Add(gems[i]);
                }
                else
                {
                    if (current.Count >= 3) groups.Add(new List<Gem>(current));
                    current = new List<Gem> { gems[i] };
                }
            }

            if (current.Count >= 3) groups.Add(current);
            return groups;
        }

        // �ˬd�@��Τ@�C���̤j�s���
        private static int CheckLine(List<Gem> gems, bool isHorizontal)
        {
            int maxLength = 0;

            // ����ΦC����
            var groups = gems.GroupBy(g => isHorizontal ? g.y : g.x);

            foreach (var group in groups)
            {
                // ��X��Y�y�бƧ�
                var sortedGems = group.OrderBy(g => isHorizontal ? g.x : g.y).ToList();

                // �ˬd�̪����s��ǦC
                int currentLength = 1;
                for (int i = 1; i < sortedGems.Count; i++)
                {
                    bool isConsecutive = false;
                    if (isHorizontal)
                    {
                        isConsecutive = sortedGems[i].x == sortedGems[i - 1].x + 1;
                    }
                    else
                    {
                        isConsecutive = sortedGems[i].y == sortedGems[i - 1].y + 1;
                    }

                    if (isConsecutive)
                    {
                        currentLength++;
                        if (currentLength > maxLength)
                        {
                            maxLength = currentLength;
                            //Debug.Log($"���{'����' if isHorizontal else '����'}�s�u: {maxLength} @ row/col {group.Key}");
                        }
                    }
                    else
                    {
                        currentLength = 1;
                    }
                }
            }

            //Debug.Log($"�̲�{'����' if isHorizontal else '����'}�̤j�s�u: {maxLength}");
            return maxLength;
        }

        // �Ω����_�ۦC�������
        private class ListComparer<T> : IEqualityComparer<List<T>>
        {
            public bool Equals(List<T> x, List<T> y) =>
                x.Count == y.Count && !x.Except(y).Any();

            public int GetHashCode(List<T> obj)
            {
                int hash = 17;
                foreach (var item in obj.OrderBy(x => x.GetHashCode()))
                {
                    hash = hash * 31 + item.GetHashCode();
                }
                return hash;
            }
        }
    }
}