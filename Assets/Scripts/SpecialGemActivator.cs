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
            // 提前檢查寶石和遊戲物件的有效性
            if (gem == null || gem.gameObject == null)
            {
                Debug.LogWarning("嘗試啟動無效的特殊寶石");
                yield break;
            }

            board.hasMoveCompleted = false;
            board.matchPredictor?.StopTimer();
            int resType = gem.id - 100;
            List<Gem> allDestroyedGems = new List<Gem>();
            board.statusText.text = "消除中";
            // 0 : LineH, 1: LineV, 2: Bomb, 3: Rainbow, 4: Cross 5: Bomb big 5x5 6: Destroy All Gems 7: 橫 與 同類型消除 8: 直 與 同類型消除 9: 3x3 Bomb 10: 3x1 Bomb 11: 1x3 Bomb
            //Debug.Log(resType);

            switch (resType)
            {
                case 0: // LineH
                    for (int x = 0; x < board.width; x++)
                    {
                        var targetGem = board.gems[x, gem.y];
                        if (targetGem != null && ValidateGemPosition(targetGem, x, gem.y))
                        {
                            allDestroyedGems.Add(targetGem);
                            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                        }
                    }
                    break;

                case 1: // LineV
                    for (int y = 0; y < board.height; y++)
                    {
                        var targetGem = board.gems[gem.x, y];
                        if (targetGem != null && ValidateGemPosition(targetGem, gem.x, y))
                        {
                            allDestroyedGems.Add(targetGem);
                            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                        }
                    }
                    break;              

                case 2: // Bomb
                        // 先加入中心點
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                        yield return new WaitForSeconds(Board.COLLECT_DELAY);
                    }
                    // 定義要檢查的相對座標
                    int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
                    int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

                    // 檢查中心點周圍8個位置
                    for (int i = 0; i < 8; i++)
                    {
                        int newX = gem.x + dx[i];
                        int newY = gem.y + dy[i];

                        // 檢查座標是否在邊界內
                        if (newX >= 0 && newX < board.width && newY >= 0 && newY < board.height)
                        {
                            var targetGem = board.gems[newX, newY];
                            if (targetGem != null && ValidateGemPosition(targetGem, newX, newY))
                            {
                                allDestroyedGems.Add(targetGem);
                                yield return new WaitForSeconds(Board.COLLECT_DELAY);
                            }
                        }
                    }
                    break;

                case 3: // Rainbow
                        // 先將 Rainbow 寶石自己加入刪除列表
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                        yield return new WaitForSeconds(Board.COLLECT_DELAY);
                    }

                    // 取得被互動寶石的 ID
                    int targetId = (board.gem1?.id == gem.id) ? board.gem2?.id ?? 1 : board.gem1?.id ?? 1;

                    // 搜尋整個棋盤尋找相同 ID 的寶石
                    for (int x = 0; x < board.width; x++)
                    {
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null &&
                                targetGem.id == targetId &&
                                targetGem != gem && // 確保不會重複刪除 Rainbow 寶石
                                ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                                yield return new WaitForSeconds(Board.COLLECT_DELAY);
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
                            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                        }
                    }
                    for (int y = 0; y < board.height; y++)
                    {
                        var targetGem = board.gems[gem.x, y];
                        if (targetGem != null && targetGem != gem &&
                            ValidateGemPosition(targetGem, gem.x, y))
                        {
                            allDestroyedGems.Add(targetGem);
                            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                        }
                    }
                    break;

                case 5: // Bomb big 5x5
                        // 先加入中心點
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                        yield return new WaitForSeconds(Board.COLLECT_DELAY);
                    }

                    // 獲取有效的檢查範圍
                    int minX = gem.x - 2 < 0 ? 0 : gem.x - 2;
                    int maxX = gem.x + 2 >= board.width ? board.width - 1 : gem.x + 2;
                    int minY = gem.y - 2 < 0 ? 0 : gem.y - 2;
                    int maxY = gem.y + 2 >= board.height ? board.height - 1 : gem.y + 2;

                    // 遍歷有效範圍內的所有格子
                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            // 跳過中心點，因為已經檢查過了
                            if (x == gem.x && y == gem.y)
                                continue;

                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                                yield return new WaitForSeconds(Board.COLLECT_DELAY);
                            }
                        }
                    }
                    break;

                case 6:
                    //Destroy All Gems
                    List<Gem> bombGems = new List<Gem>();
                    for (int x = 0; x < board.width; x++)
                    {
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                bombGems.Add(targetGem);
                            }
                        }
                    }
                    foreach (var bombGem in bombGems)
                    {
                        allDestroyedGems.Add(bombGem);
                        yield return new WaitForSeconds(Board.COLLECT_DELAY);
                    }
                    break;
                case 7:
                    // 橫 與 同類型消除
                    // 隨機決定要消除幾列
                    int linesToDestroy = Random.Range(2, board.height - 2); // 至少消除1行，最多消除整個棋盤的行數
                    Debug.Log($"要消除的列數：{linesToDestroy}");

                    // 創建一個包含所有可能行號的列表，並打亂順序
                    List<int> allLines = new List<int>();
                    for (int y = 0; y < board.height; y++)
                    {
                        allLines.Add(y);
                    }
                    // 打亂列表順序
                    for (int i = allLines.Count - 1; i > 0; i--)
                    {
                        int randomIndex = Random.Range(0, i + 1);
                        int temp = allLines[i];
                        allLines[i] = allLines[randomIndex];
                        allLines[randomIndex] = temp;
                    }

                    // 取前 linesToDestroy 個行號進行消除
                    for (int i = 0; i < linesToDestroy; i++)
                    {
                        int y = allLines[i];
                        for (int x = 0; x < board.width; x++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                                yield return new WaitForSeconds(Board.FALL_DELAY);
                            }
                        }
                    }
                    
                    

                    break;

                case 8:
                    // 直 與 同類型消除
                    // 隨機決定要消除幾行
                    int columnsToDestroy = Random.Range(2, board.width - 2); // 至少消除1列，最多消除整個棋盤的列數
                    Debug.Log($"要消除的行數：{columnsToDestroy}");

                    // 創建一個包含所有可能行號的列表，並打亂順序
                    List<int> allColumns = new List<int>();
                    for (int x = 0; x < board.width; x++)
                    {
                        allColumns.Add(x);
                    }
                    // 打亂列表順序
                    for (int i = allColumns.Count - 1; i > 0; i--)
                    {
                        int randomIndex = Random.Range(0, i + 1);
                        int temp = allColumns[i];
                        allColumns[i] = allColumns[randomIndex];
                        allColumns[randomIndex] = temp;
                    }

                    // 取前 columnsToDestroy 個列號進行消除
                    for (int i = 0; i < columnsToDestroy; i++)
                    {
                        int x = allColumns[i];
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                                yield return new WaitForSeconds(Board.FALL_DELAY);
                            }
                        }
                    }

                    break;
                case 9:
                    // 先找出要放置炸彈的位置
                    List<(int x, int y)> bombPositions = new List<(int x, int y)>();
                    int attempts = 0;
                    while (bombPositions.Count < 2 && attempts < 100) // 加入最大嘗試次數避免無限迴圈
                    {
                        attempts++;
                        int x = Random.Range(0, board.width);
                        int y = Random.Range(0, board.height);

                        // 檢查這個位置是否有效且未被選中
                        if (board.gems[x, y] != null && !bombPositions.Contains((x, y)))
                        {
                            bombPositions.Add((x, y));
                        }
                    }

                    //Debug.Log($"找到 {bombPositions.Count} 個位置來放置炸彈");

                    // 先將所有位置的寶石變成炸彈
                    foreach (var pos in bombPositions)
                    {
                        if (board.gems[pos.x, pos.y] != null)
                        {
                            board.gems[pos.x, pos.y].id = 102;
                        }
                    }

                    // 等待一小段時間讓玩家看到炸彈生成
                    yield return new WaitForSeconds(0.5f);

                    // 再依序引爆每個炸彈
                    foreach (var pos in bombPositions)
                    {
                        var bombGem = board.gems[pos.x, pos.y];
                        if (bombGem != null)
                        {
                            // 處理中心點
                            if (ValidateGemPosition(bombGem, pos.x, pos.y))
                            {
                                allDestroyedGems.Add(bombGem);
                                yield return new WaitForSeconds(Board.COLLECT_DELAY);
                            }

                            // 處理周圍8格
                            int[] cx = { -1, -1, -1, 0, 0, 1, 1, 1 };
                            int[] cy = { -1, 0, 1, -1, 1, -1, 0, 1 };

                            for (int i = 0; i < 8; i++)
                            {
                                int newX = pos.x + cx[i];
                                int newY = pos.y + cy[i];

                                if (newX >= 0 && newX < board.width && newY >= 0 && newY < board.height)
                                {
                                    var targetGem = board.gems[newX, newY];
                                    if (targetGem != null && ValidateGemPosition(targetGem, newX, newY))
                                    {
                                        allDestroyedGems.Add(targetGem);
                                        yield return new WaitForSeconds(Board.FALL_DELAY);
                                    }
                                }
                            }
                        }
                    }
                    break;
                case 10:
                    // 以id102為主的縱向3行消除
                    // 確定要消除的列範圍
                    int startX = Mathf.Max(gem.x - 1, 0);  // 左邊界，不小於0
                    int endX = Mathf.Min(gem.x + 1, board.width - 1);  // 右邊界，不超過棋盤寬度

                    // 消除指定範圍內的所有寶石
                    for (int x = startX; x <= endX; x++)
                    {
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                                yield return new WaitForSeconds(Board.COLLECT_DELAY);
                            }
                        }
                    }
                    break;

                case 11:
                    // 以id102為主的橫向3行消除
                    // 確定要消除的行範圍
                    int startY = Mathf.Max(gem.y - 1, 0);  // 下邊界，不小於0
                    int endY = Mathf.Min(gem.y + 1, board.height - 1);  // 上邊界，不超過棋盤高度

                    // 消除指定範圍內的所有寶石
                    for (int y = startY; y <= endY; y++)
                    {
                        for (int x = 0; x < board.width; x++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                                yield return new WaitForSeconds(Board.COLLECT_DELAY);
                            }
                        }
                    }
                    
                    break;
            }

            board.statusText.text = "消除中";
            if (allDestroyedGems.Count > 0)
            {
                yield return board.StartCoroutine(board.FadeAndDestroyGems(allDestroyedGems));
            }
            yield return new WaitForSeconds(Board.COMPLETE_DELAY);
        }
    }
}