using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3Game
{
    public class SpecialGemActivator
    {
        private Board board;

        public SpecialGemActivator(Board board)
        {
            this.board = board;
        }

        public void ActivateSpecialGem(Gem gem)
        {
            if (!ValidateGemPosition(gem, gem.x, gem.y))
            {
                Debug.LogWarning($"特殊寶石位置無效: ({gem.x}, {gem.y})");
                return;
            }
            
            board.StartCoroutine(ActivateSpecialGemSequentially(gem));
        }
        private bool ValidateGemPosition(Gem gem, int x, int y)
        {
            return x >= 0 && x < board.width &&
           y >= 0 && y < board.height &&
           gem != null &&
           gem.gameObject != null;
        }
        private IEnumerator ActivateSpecialGemSequentially(Gem gem)
        {
            if (gem == null || gem.gameObject == null)
            {
                Debug.LogWarning("嘗試啟動無效的特殊寶石");
                yield break;
            }
            // 等待交換動畫完成
            while (gem.isAnimating)
            {
                yield return null;
            }
            board.hasMoveCompleted = false;
            board.matchPredictor?.StopTimer();
            int resType = gem.id - 100;
            List<Gem> allDestroyedGems = new List<Gem>();
            board.statusText.text = "消除中";

            // 將方向數組移到這裡，讓所有 case 都能使用
            int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

            // 移除所有個別的等待時間，直接收集要消除的寶石
            switch (resType)
            {
                case 0: // LineH
                    lineH(allDestroyedGems, gem);
                    break;

                case 1: // LineV
                    //如果兩個寶石都是直線寶石，則消除十字形
                    lineV(allDestroyedGems, gem);
                    break;

                case 2: // Bomb
                        // 先加入中心點
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                    }

                    //int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
                    //int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };
                    for (int i = 0; i < 8; i++)
                    {
                        int newX = gem.x + dx[i];
                        int newY = gem.y + dy[i];
                        if (newX >= 0 && newX < board.width && newY >= 0 && newY < board.height)
                        {
                            var targetGem = board.gems[newX, newY];
                            if (targetGem != null && ValidateGemPosition(targetGem, newX, newY))
                            {
                                allDestroyedGems.Add(targetGem);
                            }
                        }
                    }
                    break;

                case 3: // Rainbow
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                    }

                    int targetId = (board.gem1?.id == gem.id) ? board.gem2?.id ?? 1 : board.gem1?.id ?? 1;
                    
                    if (targetId >= 100) 
                    {
                        int randomTargetID = Random.Range(0, board.gemPrefabs.Length);
                        targetId = randomTargetID; 
                    }
                    Debug.Log($"Rainbow 寶石的目標 ID：{targetId}");

                    for (int x = 0; x < board.width; x++)
                    {
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null &&
                                targetGem.id == targetId &&
                                targetGem != gem &&
                                ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                            }
                        }
                    }
                    break;

                case 4: // Cross
                    for (int x = 0; x < board.width; x++)
                    {
                        var targetGem = board.gems[x, gem.y];
                        if (targetGem != null && ValidateGemPosition(targetGem, x, gem.y))
                        {
                            allDestroyedGems.Add(targetGem);
                        }
                    }
                    for (int y = 0; y < board.height; y++)
                    {
                        var targetGem = board.gems[gem.x, y];
                        if (targetGem != null && targetGem != gem &&
                            ValidateGemPosition(targetGem, gem.x, y))
                        {
                            allDestroyedGems.Add(targetGem);
                        }
                    }
                    break;

                case 5: // Bomb big 5x5
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                    }

                    int minX = Mathf.Max(gem.x - 2, 0);
                    int maxX = Mathf.Min(gem.x + 2, board.width - 1);
                    int minY = Mathf.Max(gem.y - 2, 0);
                    int maxY = Mathf.Min(gem.y + 2, board.height - 1);

                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            if (x == gem.x && y == gem.y) continue;
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                            }
                        }
                    }
                    break;

                case 6: // Destroy All Gems
                    for (int x = 0; x < board.width; x++)
                    {
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                            }
                        }
                    }
                    break;

                case 7: // 橫向隨機消除
                    int linesToDestroy = Random.Range(2, board.height - 2);
                    List<int> allLines = new List<int>();
                    for (int y = 0; y < board.height; y++)
                    {
                        allLines.Add(y);
                    }

                    // Fisher-Yates shuffle
                    for (int i = allLines.Count - 1; i > 0; i--)
                    {
                        int randomIndex = Random.Range(0, i + 1);
                        int temp = allLines[i];
                        allLines[i] = allLines[randomIndex];
                        allLines[randomIndex] = temp;
                    }

                    for (int i = 0; i < linesToDestroy; i++)
                    {
                        int y = allLines[i];
                        for (int x = 0; x < board.width; x++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                            }
                        }
                    }
                    break;

                case 8: // 縱向隨機消除
                    int columnsToDestroy = Random.Range(2, board.width - 2);
                    List<int> allColumns = new List<int>();
                    for (int x = 0; x < board.width; x++)
                    {
                        allColumns.Add(x);
                    }

                    for (int i = allColumns.Count - 1; i > 0; i--)
                    {
                        int randomIndex = Random.Range(0, i + 1);
                        int temp = allColumns[i];
                        allColumns[i] = allColumns[randomIndex];
                        allColumns[randomIndex] = temp;
                    }

                    for (int i = 0; i < columnsToDestroy; i++)
                    {
                        int x = allColumns[i];
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                            }
                        }
                    }
                    break;

                case 9: // 多重炸彈
                    List<(int x, int y)> bombPositions = new List<(int x, int y)>();
                    int attempts = 0;
                    while (bombPositions.Count < 2 && attempts < 3)
                    {
                        attempts++;
                        int x = Random.Range(0, board.width);
                        int y = Random.Range(0, board.height);
                        if (board.gems[x, y] != null && !bombPositions.Contains((x, y)))
                        {
                            bombPositions.Add((x, y));
                        }
                    }

                    foreach (var pos in bombPositions)
                    {
                        if (board.gems[pos.x, pos.y] != null)
                        {
                            board.gems[pos.x, pos.y].id = 102;
                        }
                    }

                    foreach (var pos in bombPositions)
                    {
                        var bombGem = board.gems[pos.x, pos.y];
                        if (bombGem != null)
                        {
                            if (ValidateGemPosition(bombGem, pos.x, pos.y))
                            {
                                allDestroyedGems.Add(bombGem);
                            }

                            for (int i = 0; i < 8; i++)
                            {
                                int newX = pos.x + dx[i];
                                int newY = pos.y + dy[i];
                                if (newX >= 0 && newX < board.width && newY >= 0 && newY < board.height)
                                {
                                    var targetGem = board.gems[newX, newY];
                                    if (targetGem != null && ValidateGemPosition(targetGem, newX, newY))
                                    {
                                        allDestroyedGems.Add(targetGem);
                                    }
                                }
                            }
                        }
                    }
                    break;

                case 10: // 橫向三行消除
                    int startY = Mathf.Max(gem.y - 1, 0);
                    int endY = Mathf.Min(gem.y + 1, board.height - 1);
                    for (int y = startY; y <= endY; y++)
                    {
                        for (int x = 0; x < board.width; x++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                            }
                        }
                    }
                    break;

                case 11: // 縱向三行消除
                    int startX = Mathf.Max(gem.x - 1, 0);
                    int endX = Mathf.Min(gem.x + 1, board.width - 1);
                    for (int x = startX; x <= endX; x++)
                    {
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                            }
                        }
                    }
                    break;
            }

            //Debug.Log("作用中: " + gem.id);
            board.statusText.text = "消除中";

            if (allDestroyedGems.Count > 0)
            {
                yield return board.StartCoroutine(board.FadeAndDestroyGems(allDestroyedGems));
            }
        }
        List<Gem> lineV(List<Gem> allDestroyedGems, Gem gem)
        {
            for (int y = 0; y < board.height; y++)
            {
                var targetGem = board.gems[gem.x, y];
                if (targetGem != null && ValidateGemPosition(targetGem, gem.x, y))
                {
                    allDestroyedGems.Add(targetGem);
                }
            }
            return allDestroyedGems;
        }
        List<Gem> lineH(List<Gem> allDestroyedGems, Gem gem)
        {
            for (int x = 0; x < board.width; x++)
            {
                var targetGem = board.gems[x, gem.y];
                if (targetGem != null && ValidateGemPosition(targetGem, x, gem.y))
                {
                    allDestroyedGems.Add(targetGem);
                }
            }
            return allDestroyedGems;
        }
    }
}