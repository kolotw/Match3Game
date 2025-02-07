using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.LookDev;

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

        // 通用方向檢查方法，同時處理水平和垂直
        public static List<Gem> CheckDirection(Board board, int x, int y, bool isHorizontal)
        {
            var matches = new List<Gem>();
            Gem centerGem = board.GetGem(x, y);
            if (centerGem == null) return matches;

            int max = isHorizontal ? board.width : board.height;
            Func<int, Gem> getGem = i => isHorizontal ? board.GetGem(i, y) : board.GetGem(x, i);

            // 向正方向檢查
            for (int i = (isHorizontal ? x : y); i < max; i++)
            {
                var gem = getGem(i);
                if (gem?.id == centerGem.id)
                    matches.Add(gem);
                else
                    break;
            }

            // 向負方向檢查
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

        // 檢查特殊形狀匹配
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

                // T/L形狀需要額外的檢查
                if (checkTLShape && !IsTLShape(horizontal, vertical))
                {
                    return null;
                }

                return match;
            }

            return null;
        }

        // 檢查交換後的匹配
        public static bool CheckMatchAfterSwap(Board board, int x, int y)
        {
            Gem centerGem = board.GetGem(x, y);
            if (centerGem == null) return false;

            // 檢查水平和垂直方向的可能匹配
            return IsMatchInDirection(board, x, y, true) || IsMatchInDirection(board, x, y, false);
        }

        // 檢查一個方向的匹配
        private static bool IsMatchInDirection(Board board, int x, int y, bool isHorizontal)
        {
            Gem centerGem = board.GetGem(x, y);
            int pos = isHorizontal ? x : y;
            int max = isHorizontal ? board.width : board.height;
            Func<int, Gem> getGem = i => isHorizontal ? board.GetGem(i, y) : board.GetGem(x, i);

            // 檢查三種可能的匹配模式
            return (pos >= 2 && getGem(pos - 2)?.id == getGem(pos - 1)?.id && getGem(pos - 1)?.id == centerGem.id) ||
                   (pos >= 1 && pos < max - 1 && getGem(pos - 1)?.id == centerGem.id && centerGem.id == getGem(pos + 1)?.id) ||
                   (pos < max - 2 && centerGem.id == getGem(pos + 1)?.id && getGem(pos + 1)?.id == getGem(pos + 2)?.id);
        }

        // 找出連續的寶石組別
        public static List<List<Gem>> FindContinuousGemGroups(IGrouping<int, Gem> group)
        {
            var gems = group.ToList();
            var result = new List<List<Gem>>();

            // 檢查水平和垂直方向
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

            // 檢查特殊形狀
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

        // 獲取連續的寶石組
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

        // 檢查特殊形狀
        private static List<Gem> CheckSpecialShape(List<Gem> allGems, Gem center)
        {
            var horizontal = allGems.Where(g => g.y == center.y).OrderBy(g => g.x).ToList();
            var vertical = allGems.Where(g => g.x == center.x).OrderBy(g => g.y).ToList();
            //Debug.Log($"CheckSpecialShape: H: {horizontal.Count},V: {vertical.Count}");
            if (horizontal.Count >= 3 && vertical.Count >= 3)
            {
                return horizontal.Union(vertical).ToList();
            }

            return null;
        }

        // 確定特殊寶石類型
        // 在 MatchUtils.cs 中添加更詳細的檢查
        public static (int resourceType, bool isHorizontal, bool isVertical, List<Gem> matchedGems) 確認特殊寶石類別(
    List<Gem> gems,
    int triggerX = 0,
    int triggerY = 0)
        {
            if (gems == null || gems.Count < 4)
                return (-1, false, false, null);

            // 使用FindContinuousGemGroups找連續的寶石群組
            var groupedGems = gems.GroupBy(g => g.id);
            var continuousGroups = groupedGems
                .SelectMany(group => FindContinuousGemGroups(group))
                .ToList();

            // 優先處理玩家觸發點的寶石群組
            if (triggerX != 0 || triggerY != 0)
            {
                var triggerGem = gems.FirstOrDefault(g => g.x == triggerX && g.y == triggerY);
                if (triggerGem != null)
                {
                    continuousGroups = continuousGroups
                        .Where(group => group.Contains(triggerGem))
                        .ToList();
                }
            }

            // 1. 五連
            var fiveGroup = continuousGroups.FirstOrDefault(g => g.Count >= 5);
            if (fiveGroup != null)
            {
                return (3, true, true, gems);
            }

            // 2. 轉角型 - 使用 CheckSpecialShape, 直的才進來，轉角反而不進來
            foreach (var group in continuousGroups)
            {
                foreach (var centerGem in group)
                {
                    var specialShape = CheckSpecialShape(group, centerGem);                    
                    Debug.Log("HERE"); 
                    if (specialShape != null)
                    {
                        //這邊沒反應
                        Debug.Log($"Special Shape: {specialShape.Count} at {specialShape[0].x},{specialShape[0].y}");
                        bool isLargeCorner = specialShape.Count >= 5;
                        return (isLargeCorner ? 3 : 2, true, true, gems);
                    }
                }
            }

            // 3. 純四連
            var fourGroup = continuousGroups
                .FirstOrDefault(g =>
                    g.Count >= 4 &&
                    !continuousGroups.Any(gg => gg.Count >= 5));

            if (fourGroup != null)
            {
                var horizontalFour = fourGroup.GroupBy(g => g.y).Any(g => g.Count() >= 4);
                var verticalFour = fourGroup.GroupBy(g => g.x).Any(g => g.Count() >= 4);

                return (horizontalFour ? 0 : 1, !horizontalFour, horizontalFour, gems);
            }

            return (-1, false, false, gems);
        }


        // 檢查特殊寶石組合
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