using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Match3Game
{
    public enum ActivationSource
    {
        Player,         // 由玩家直接觸發
        ChainReaction  // 由其他特殊寶石觸發
    }
    public class SpecialGemActivator
    {
        private Board board;
        private bool isProcessing = false;
        private Queue<(Gem gem, ActivationSource source)> activationQueue = new Queue<(Gem gem, ActivationSource source)>();

        public Gem triggerGem { get; private set; }

        public SpecialGemActivator(Board board)
        {
            this.board = board;
        }

        public void ActivateSpecialGem(Gem gem, ActivationSource source = ActivationSource.Player)
        {
            // 加入額外的檢查
            if (gem == null || gem.gameObject == null)
            {
                Debug.LogWarning($"嘗試啟動已經不存在的特殊寶石");
                return;
            }

            if (!ValidateGemPosition(gem, gem.x, gem.y))
            {
                Debug.LogWarning($"特殊寶石位置無效: ({gem.x}, {gem.y})");
                return;
            }

            // 檢查是否已經在隊列中
            //if (activationQueue.Any(item => item.gem == gem))
            //{
            //    Debug.Log($"特殊寶石 ID: {gem.id} 已在啟動隊列中，跳過重複啟動");
            //    return;
            //}

            // 將特殊寶石加入隊列
            activationQueue.Enqueue((gem, source));

            // 如果沒有正在處理的特殊寶石，開始處理隊列
            if (!isProcessing)
            {
                board.StartCoroutine(ProcessActivationQueue());
            }
        }
        private IEnumerator ProcessActivationQueue()
        {
            isProcessing = true;

            while (activationQueue.Count > 0)
            {
                var (currentGem, source) = activationQueue.Peek(); // 使用 Peek 而不是 Dequeue

                // 再次驗證寶石的有效性
                if (currentGem == null || currentGem.gameObject == null || !ValidateGemPosition(currentGem, currentGem.x, currentGem.y))
                {
                    Debug.Log($"跳過無效的特殊寶石 {currentGem.x}, {currentGem.y}");
                    activationQueue.Dequeue(); // 移除無效的寶石
                    continue;
                }

                // 如果寶石有效，才進行處理
                activationQueue.Dequeue();
                yield return ActivateSpecialGemSequentially(currentGem, source);
            }

            isProcessing = false;
        }
        private bool ValidateGemPosition(Gem gem, int x, int y)
        {
            return x >= 0 && x < board.width &&
           y >= 0 && y < board.height &&
           gem != null &&
           gem.gameObject != null;
        }
        private IEnumerator ActivateSpecialGemSequentially(Gem gem, ActivationSource source)
        {
            if (gem == null || gem.gameObject == null)
            {
                Debug.LogWarning("嘗試啟動無效的特殊寶石");
                yield break;
            }
            // 設置寶石的觸發信息
            gem.SetActivationInfo(source);

            if(source == ActivationSource.Player)
            {
                triggerGem = gem;
            }

            Debug.Log($"特殊寶石觸發 - ID: {gem.id}, 位置: ({gem.x}, {gem.y}), " +
              $"觸發來源: {source}, 連鎖層級: {gem.triggerChainDepth}");

            // 等待交換動畫完成
            while (gem.isAnimating)
            {
                yield return null;
            }

            // 收集所有要消除的寶石
            List<Gem> allDestroyedGems = new List<Gem>();
            List<Gem> specialGemsToActivate = new List<Gem>();

            board.hasMoveCompleted = false;
            board.matchPredictor?.StopTimer();
            int resType = gem.id - 100;
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

            // 在真正消除之前，先檢查並收集需要觸發的特殊寶石
            foreach (var targetGem in allDestroyedGems.ToList())
            {
                if (targetGem.id >= 100)  // 首先確認這是特殊寶石
                {
                    // 這裡我們需要更嚴格的檢查，確保：
                    // 1. 不是當前正在處理的寶石
                    // 2. 不是最初觸發的寶石
                    // 3. 這個寶石還沒有被設置觸發信息
                    if (targetGem != gem && targetGem != triggerGem && targetGem.triggerChainDepth == 0)
                    {
                        // 設置觸發信息
                        targetGem.SetActivationInfo(ActivationSource.ChainReaction, gem);

                        // 將特殊寶石加入待觸發列表
                        specialGemsToActivate.Add(targetGem);

                        Debug.Log($"特殊寶石將被觸發 - ID: {targetGem.id}, " +
                                 $"位置: ({targetGem.x}, {targetGem.y}), " +
                                 $"來源: {source}, " +
                                 $"被觸發者: {gem.id}, " +
                                 $"連鎖深度: {targetGem.triggerChainDepth + 1}");
                    }
                    else
                    {
                        string skipReason = "";
                        if (targetGem == gem)
                        {
                            skipReason = "是當前寶石";
                        }
                        else if (targetGem == triggerGem)
                        {
                            skipReason = "是觸發寶石";
                        }
                        else if (targetGem.triggerChainDepth > 0)
                        {
                            skipReason = "已被設置觸發";
                        }
                        else
                        {
                            skipReason = "未知原因";
                        }

                        Debug.Log($"跳過特殊寶石 - ID: {targetGem.id}, " +
                                 $"位置: ({targetGem.x}, {targetGem.y}), " +
                                 $"原因: {skipReason}");
                    }
                }
            }

            // 在執行消除效果之前，先將特殊寶石加入啟動佇列
            foreach (var specialGem in specialGemsToActivate)
            {
                if (specialGem != null && specialGem.gameObject != null)
                {
                    activationQueue.Enqueue((specialGem, ActivationSource.ChainReaction));
                }
            }

            // 最後才執行實際的消除效果
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