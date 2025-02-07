using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Match3Game
{
    public static class MatchUtils
    {
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

        // �q�Τ�V�ˬd��k�A�P�ɳB�z�����M����
        public static List<Gem> CheckDirection(Board board, int x, int y, bool isHorizontal)
        {
            var matches = new List<Gem>();
            Gem centerGem = board.GetGem(x, y);
            if (centerGem == null) return matches;

            int max = isHorizontal ? board.width : board.height;
            Func<int, Gem> getGem = i => isHorizontal ? board.GetGem(i, y) : board.GetGem(x, i);

            // �V����V�ˬd
            for (int i = (isHorizontal ? x : y); i < max; i++)
            {
                var gem = getGem(i);
                if (gem?.id == centerGem.id)
                    matches.Add(gem);
                else
                    break;
            }

            // �V�t��V�ˬd
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
                var match = new Board.MatchInfo
                {
                    matchedGems = horizontal.Union(vertical).ToList(),
                    isHorizontal = true,
                    isVertical = true
                };

                // T/L�Ϊ��ݭn�B�~���ˬd
                if (checkTLShape && !IsTLShape(horizontal, vertical))
                {
                    return null;
                }

                return match;
            }

            return null;
        }

        // �ˬd�洫�᪺�ǰt
        public static bool CheckMatchAfterSwap(Board board, int x, int y)
        {
            Gem centerGem = board.GetGem(x, y);
            if (centerGem == null) return false;

            // �ˬd�����M������V���i��ǰt
            return IsMatchInDirection(board, x, y, true) || IsMatchInDirection(board, x, y, false);
        }

        // �ˬd�@�Ӥ�V���ǰt
        private static bool IsMatchInDirection(Board board, int x, int y, bool isHorizontal)
        {
            Gem centerGem = board.GetGem(x, y);
            int pos = isHorizontal ? x : y;
            int max = isHorizontal ? board.width : board.height;
            Func<int, Gem> getGem = i => isHorizontal ? board.GetGem(i, y) : board.GetGem(x, i);

            // �ˬd�T�إi�઺�ǰt�Ҧ�
            return (pos >= 2 && getGem(pos - 2)?.id == getGem(pos - 1)?.id && getGem(pos - 1)?.id == centerGem.id) ||
                   (pos >= 1 && pos < max - 1 && getGem(pos - 1)?.id == centerGem.id && centerGem.id == getGem(pos + 1)?.id) ||
                   (pos < max - 2 && centerGem.id == getGem(pos + 1)?.id && getGem(pos + 1)?.id == getGem(pos + 2)?.id);
        }

        // ��X�s���_�۲էO
        public static List<List<Gem>> FindContinuousGemGroups(IGrouping<int, Gem> group)
        {
            var gems = group.ToList();
            var result = new List<List<Gem>>();

            // �ˬd�����M������V
            foreach (var isHorizontal in new[] { true, false })
            {
                var groups = gems.GroupBy(g => isHorizontal ? g.y : g.x)
                    .SelectMany(line =>
                    {
                        var sorted = line.OrderBy(g => isHorizontal ? g.x : g.y).ToList();
                        return GetConsecutiveGroups(sorted, isHorizontal);
                    });

                result.AddRange(groups);
            }

            // �ˬd�S��Ϊ�
            foreach (var gem in gems)
            {
                var specialGroup = CheckSpecialShape(gems, gem);
                if (specialGroup?.Count >= 5)
                {
                    result.Add(specialGroup);
                }
            }

            return result.Where(g => g.Count >= 4)
                        .Distinct(new ListComparer<Gem>())
                        .ToList();
        }

        // ����s���_�۲�
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

        // �ˬd�S��Ϊ�
        private static List<Gem> CheckSpecialShape(List<Gem> allGems, Gem center)
        {
            var horizontal = allGems.Where(g => g.y == center.y).OrderBy(g => g.x).ToList();
            var vertical = allGems.Where(g => g.x == center.x).OrderBy(g => g.y).ToList();

            if (horizontal.Count >= 3 && vertical.Count >= 3)
            {
                return horizontal.Union(vertical).ToList();
            }

            return null;
        }

        // �T�w�S���_������
        // �b MatchUtils.cs ���K�[��ԲӪ��ˬd
        public static (int resourceType, bool isHorizontal, bool isVertical, List<Gem> matchedGems)
            �T�{�S���_�����O(List<Gem> gems,
            int triggerX = 0, int triggerY = 0
            )
        {
            if (gems == null || gems.Count < 4)
            {
                return (-1, false, false, new List<Gem>());
            }

            // �ˬd������V���s�u
            bool hasHorizontalLine = false;
            var horizontalGroups = gems.GroupBy(g => g.y)
                .Select(group =>
                {
                    var sortedGems = group.OrderBy(g => g.x).ToList();
                    int consecutiveCount = 1;
                    for (int i = 1; i < sortedGems.Count; i++)
                    {
                        if (sortedGems[i].x == sortedGems[i - 1].x + 1)
                        {
                            consecutiveCount++;
                            if (consecutiveCount >= 3)
                            {
                                hasHorizontalLine = true;
                                break;
                            }
                        }
                        else
                        {
                            consecutiveCount = 1;
                        }
                    }
                    return hasHorizontalLine;
                })
                .Any(x => x);

            // �ˬd������V���s�u
            bool hasVerticalLine = false;
            var verticalGroups = gems.GroupBy(g => g.x)
                .Select(group =>
                {
                    var sortedGems = group.OrderBy(g => g.y).ToList();
                    int consecutiveCount = 1;
                    for (int i = 1; i < sortedGems.Count; i++)
                    {
                        if (sortedGems[i].y == sortedGems[i - 1].y + 1)
                        {
                            consecutiveCount++;
                            if (consecutiveCount >= 3)
                            {
                                hasVerticalLine = true;
                                break;
                            }
                        }
                        else
                        {
                            consecutiveCount = 1;
                        }
                    }
                    return hasVerticalLine;
                })
                .Any(x => x);

            bool isCornerMatch = hasHorizontalLine && hasVerticalLine;
            int resourceType;

            // �M�w�S���_������
            if (isCornerMatch)
            {
                resourceType = gems.Count >= 6 ? 3 : 2; // Rainbow or Bomb
            }
            else
            {
                if (gems.Count >= 5)
                {
                    resourceType = 3; // Rainbow
                }
                else if (hasVerticalLine)
                {
                    resourceType = 1; // �����M���u
                }
                else if (hasHorizontalLine)
                {
                    resourceType = 0; // �����M���u
                }
                else
                {
                    resourceType = -1;
                }
            }

            Debug.Log($"�T�{�S���_�����O - �ƶq:{gems.Count}, ����:{hasHorizontalLine}, " +
                      $"����:{hasVerticalLine}, ����:{isCornerMatch}, ����:{resourceType}");

            return (resourceType, hasHorizontalLine, hasVerticalLine, gems);
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

        private static bool IsTLShape(List<Gem> horizontal, List<Gem> vertical)
        {
            return (horizontal.Count >= 3 && vertical.Count >= 3) &&
                   (horizontal.Count >= 2 || vertical.Count >= 2);
        }

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