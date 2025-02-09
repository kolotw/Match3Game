using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Match3Game
{
    public static class MatchUtils
    {
        // 特殊寶石組合配置
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

        // 檢查指定方向的連續寶石
        public static List<Gem> CheckDirection(Board board, int x, int y, bool isHorizontal)
        {
            var matches = new List<Gem>();
            Gem centerGem = board.GetGem(x, y);
            if (centerGem == null) return matches;

            int max = isHorizontal ? board.width : board.height;
            Func<int, Gem> getGem = i => isHorizontal ? board.GetGem(i, y) : board.GetGem(x, i);

            // 向前檢查
            for (int i = (isHorizontal ? x : y); i < max; i++)
            {
                var gem = getGem(i);
                if (gem?.id == centerGem.id)
                    matches.Add(gem);
                else
                    break;
            }

            // 向後檢查
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
                var matchedGems = horizontal.Union(vertical).ToList();
                bool hasExtension;
                ShapeType shapeType;

                // 使用新的 TL 形狀檢查邏輯
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

        // 檢查交換後的匹配
        public static bool CheckMatchAfterSwap(Board board, int x, int y)
        {
            Gem centerGem = board.GetGem(x, y);
            if (centerGem == null) return false;

            return IsMatchInDirection(board, x, y, true) ||
                   IsMatchInDirection(board, x, y, false);
        }

        // 檢查是否為T型或L型的主要方法
        public static bool IsTLShape(List<Gem> gems, out bool hasExtension, out ShapeType shapeType)
        {
            hasExtension = false;
            shapeType = ShapeType.None;

            if (gems == null || gems.Count < 5)
                return false;

            // 對每個可能的中心點檢查
            foreach (var centerGem in gems)
            {
                var horizontal = gems.Where(g => g.y == centerGem.y)
                                   .OrderBy(g => g.x)
                                   .ToList();
                var vertical = gems.Where(g => g.x == centerGem.x)
                                 .OrderBy(g => g.y)
                                 .ToList();

                // 檢查T型
                if (IsTShape(horizontal, vertical, out hasExtension))
                {
                    shapeType = ShapeType.T;
                    return true;
                }

                // 檢查L型
                if (IsLShape(horizontal, vertical))
                {
                    shapeType = ShapeType.L;
                    return true;
                }
            }

            return false;
        }

        // 檢查是否為T型
        private static bool IsTShape(List<Gem> horizontal, List<Gem> vertical, out bool hasExtension)
        {
            hasExtension = false;
            //Debug.Log($"檢查T型 - 水平數量: {horizontal.Count}, 垂直數量: {vertical.Count}");

            // 先檢查是否有5連線，如果有就不是T型
            if (horizontal.Count >= 5)
            {
                //Debug.Log("發現5連線，不是T型");
                return false;
            }

            // 基本T型判斷 (3x3)
            if (horizontal.Count == 3 && vertical.Count == 3)
            {
                var allGems = horizontal.Union(vertical).Distinct().ToList();
                if (allGems.Count == 5) // 確認是真正的T型（總共5個不重複的寶石）
                {
                    //Debug.Log("發現基本T型(3x3)");
                    return true;
                }
            }

            // 擴展T型判斷 (4x3 或 3x4)
            if ((horizontal.Count == 4 && vertical.Count >= 3) ||
                (horizontal.Count >= 3 && vertical.Count == 4))
            {
                // 確認形狀是連續的
                if (AreGemsConsecutive(horizontal) && AreGemsConsecutive(vertical))
                {
                    hasExtension = true;
                    //Debug.Log("發現擴展T型");
                    return true;
                }
            }

            //Debug.Log("不符合T型條件");
            return false;
        }

        // 檢查是否為L型
        private static bool IsLShape(List<Gem> horizontal, List<Gem> vertical)
        {
            // L型必須是3x3
            if (horizontal.Count != 3 || vertical.Count != 3)
                return false;

            // 檢查是否共用一個角落
            var intersection = horizontal.Intersect(vertical).ToList();
            if (intersection.Count != 1)
                return false;

            var cornerGem = intersection[0];

            // 確認corner是在水平和垂直線的端點
            bool isAtHorizontalEnd = cornerGem.x == horizontal.First().x ||
                                   cornerGem.x == horizontal.Last().x;
            bool isAtVerticalEnd = cornerGem.y == vertical.First().y ||
                                 cornerGem.y == vertical.Last().y;

            return isAtHorizontalEnd && isAtVerticalEnd;
        }

        // 檢查寶石是否連續
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

        // 檢查一個方向上的匹配
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

        // 尋找連續的寶石群組
        public static List<List<Gem>> FindContinuousGemGroups(IGrouping<int, Gem> group)
        {
            var gems = group.ToList();
            var result = new List<List<Gem>>();
            Debug.Log($"開始處理群組，總寶石數: {gems.Count}");

            // 1. 先檢查 T/L 型
            bool hasExtension;
            ShapeType shapeType;
            if (IsTLShape(gems, out hasExtension, out shapeType))
            {
                Debug.Log($"找到特殊形狀: {shapeType}, 延伸: {hasExtension}");
                result.Add(gems);
                return result;
            }

            // 2. 再檢查 5 連線
            int maxHorizontal = CheckLine(gems, true);
            int maxVertical = CheckLine(gems, false);

            if (maxHorizontal >= 5 || maxVertical >= 5)
            {
                Debug.Log($"找到5連線 - 水平: {maxHorizontal}, 垂直: {maxVertical}");
                result.Add(gems);
                return result;
            }

            // 3. 最後才處理 4 連線
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

            //Debug.Log($"找到 4 連線 - 水平: {horizontalLines.Count}, 垂直: {verticalLines.Count}");
            return result.Distinct(new ListComparer<Gem>()).ToList();
        }

        // 檢查特殊寶石類型
        public static (int resourceType, bool isHorizontal, bool isVertical, List<Gem> matchedGems)
    確認特殊寶石類別(List<Gem> gems, int triggerX = 0, int triggerY = 0)
        {
            if (gems == null || gems.Count < 4)
            {
                return (-1, false, false, new List<Gem>());
            }

            // 先檢查是否有5連線
            int maxHorizontal = CheckLine(gems, true);
            int maxVertical = CheckLine(gems, false);

            // 優先處理5連線
            if (maxHorizontal >= 5)
            {
                Debug.Log($"確認了水平5連線");
                return (3, true, false, gems);
            }
            if (maxVertical >= 5)
            {
                Debug.Log($"確認了垂直5連線");
                return (3, false, true, gems);
            }

            // 檢查T/L型
            bool hasExtension;
            ShapeType shapeType;
            if (IsTLShape(gems, out hasExtension, out shapeType))
            {
                Debug.Log($"確認了{shapeType}型，擴展:{hasExtension}");
                return (hasExtension ? 3 : 2, true, true, gems);
            }

            // 最後處理4連線
            if (maxHorizontal == 4)
            {
                Debug.Log($"確認了水平4連線");
                return (0, true, false, gems);
            }
            if (maxVertical == 4)
            {
                Debug.Log($"確認了垂直4連線");
                return (1, false, true, gems);
            }

            return (-1, false, false, gems);
        }

        // 輔助方法：取得連續的寶石群組
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

        // 檢查一行或一列的最大連續數
        private static int CheckLine(List<Gem> gems, bool isHorizontal)
        {
            int maxLength = 0;

            // 按行或列分組
            var groups = gems.GroupBy(g => isHorizontal ? g.y : g.x);

            foreach (var group in groups)
            {
                // 按X或Y座標排序
                var sortedGems = group.OrderBy(g => isHorizontal ? g.x : g.y).ToList();

                // 檢查最長的連續序列
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
                            //Debug.Log($"找到{'水平' if isHorizontal else '垂直'}連線: {maxLength} @ row/col {group.Key}");
                        }
                    }
                    else
                    {
                        currentLength = 1;
                    }
                }
            }

            //Debug.Log($"最終{'水平' if isHorizontal else '垂直'}最大連線: {maxLength}");
            return maxLength;
        }

        // 用於比較寶石列表的比較器
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