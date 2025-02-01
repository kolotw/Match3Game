using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Match3Game
{
    public class SpecialGemActivator
    {
        private Board board;
        private HashSet<Gem> processedGems = new HashSet<Gem>();

        public SpecialGemActivator(Board board)
        {
            this.board = board;
        }

        public void 啟動特殊寶石(Gem gem)
        {
            if (gem == null || gem.gameObject == null) return;
            board.StartCoroutine(觸發特殊寶石(gem, gem.id - 100));

        }
        private bool ValidateGemPosition(Gem gem, int x, int y)
        {
            return x >= 0 && x < board.width &&
           y >= 0 && y < board.height &&
           gem != null &&
           gem.gameObject != null;
        }
        public IEnumerator 觸發特殊寶石(Gem gem, int effectID)
        {
            if (gem == null || gem.gameObject == null)
            {
                Debug.LogWarning("嘗試啟動無效的特殊寶石");
                yield break;
            }
            // 將當前寶石加入已處理集合，避免重複處理
            processedGems.Add(gem);
            // 等待交換動畫完成
            while (gem.isAnimating)
            {
                yield return null;
            }

            board.hasMoveCompleted = false;
            board.matchPredictor?.StopTimer();
            //int resType = gem.id - 100; // 改effectID好嗎
            List<Gem> allDestroyedGems = new List<Gem>();
            allDestroyedGems.Add(gem);
            board.statusText.text = "消除中";

            // 收集要被消除的寶石
            CollectGemsToDestroy(gem, effectID, allDestroyedGems);

            // 在寶石被消除前，先找出其中的特殊寶石
            var specialGems = allDestroyedGems
                .Where(g => g != null && g != gem && g.id >= 100)
                .ToList();

            // 先移除要觸發的特殊寶石，避免它們被消除
            foreach (var specialGem in specialGems)
            {
                allDestroyedGems.Remove(specialGem);
            }

            // 執行普通寶石的消除
            if (allDestroyedGems.Count > 0)
            {
                yield return board.StartCoroutine(board.消失與刪除寶石(allDestroyedGems));
            }

            // 依序觸發收集到的特殊寶石
            foreach (var specialGem in specialGems)
            {
                if (specialGem != null && specialGem.gameObject != null)
                {
                    yield return new WaitForSeconds(Board.DESTROY_DELAY); // 觸發間隔
                    啟動特殊寶石(specialGem);
                }
            }
            // 特殊寶石效果結束後，檢查並清理已處理集合
            processedGems.Remove(gem);
            if (processedGems.Count == 0)
            {
                // 所有特殊寶石效果都處理完畢，執行寶石下落
                yield return board.StartCoroutine(board.落下寶石());
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
        private void CollectGemsToDestroy(Gem gem, int resType, List<Gem> allDestroyedGems)
        {
            int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

            switch (resType)
            {
                case 0: // LineH
                    lineH(allDestroyedGems, gem);
                    break;
                case 1: // LineV
                    lineV(allDestroyedGems, gem);
                    break;
                case 2: // Bomb
                    if (ValidateGemPosition(gem, gem.x, gem.y)) allDestroyedGems.Add(gem);

                    for (int i = 0; i < 8; i++)
                    {
                        int newX = gem.x + dx[i], newY = gem.y + dy[i];
                        if (newX >= 0 && newX < board.width && newY >= 0 && newY < board.height)
                        {
                            var targetGem = board.gems[newX, newY];
                            if (targetGem != null && ValidateGemPosition(targetGem, newX, newY))
                                allDestroyedGems.Add(targetGem);
                        }
                    }
                    break;
                case 3: // Rainbow
                    if (ValidateGemPosition(gem, gem.x, gem.y)) allDestroyedGems.Add(gem);

                    int targetId = (board.gem1?.id == gem.id) ? board.gem2?.id ?? 1 : board.gem1?.id ?? 1;
                    if (targetId >= 100) targetId = Random.Range(0, board.gemPrefabs.Length);

                    for (int x = 0; x < board.width; x++)
                        for (int y = 0; y < board.height; y++)
                            if (board.gems[x, y]?.id == targetId && board.gems[x, y] != gem &&
                                ValidateGemPosition(board.gems[x, y], x, y))
                                allDestroyedGems.Add(board.gems[x, y]);
                    break;
                case 4: // Cross
                    for (int x = 0; x < board.width; x++)
                        if (board.gems[x, gem.y] != null && ValidateGemPosition(board.gems[x, gem.y], x, gem.y))
                            allDestroyedGems.Add(board.gems[x, gem.y]);

                    for (int y = 0; y < board.height; y++)
                        if (board.gems[gem.x, y] != null && board.gems[gem.x, y] != gem &&
                            ValidateGemPosition(board.gems[gem.x, y], gem.x, y))
                            allDestroyedGems.Add(board.gems[gem.x, y]);
                    break;
                case 5: // Bomb big 5x5
                    if (ValidateGemPosition(gem, gem.x, gem.y)) allDestroyedGems.Add(gem);

                    for (int x = Mathf.Max(gem.x - 2, 0); x <= Mathf.Min(gem.x + 2, board.width - 1); x++)
                        for (int y = Mathf.Max(gem.y - 2, 0); y <= Mathf.Min(gem.y + 2, board.height - 1); y++)
                        {
                            if (x == gem.x && y == gem.y) continue;
                            if (board.gems[x, y] != null && ValidateGemPosition(board.gems[x, y], x, y))
                                allDestroyedGems.Add(board.gems[x, y]);
                        }
                    break;
                case 6: // Destroy All Gems
                    for (int x = 0; x < board.width; x++)
                        for (int y = 0; y < board.height; y++)
                            if (board.gems[x, y] != null && ValidateGemPosition(board.gems[x, y], x, y))
                                allDestroyedGems.Add(board.gems[x, y]);
                    break;
                case 7: // 橫向隨機消除
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(board.gem1);
                        allDestroyedGems.Add(board.gem2);
                    }

                    var allLines = Enumerable.Range(0, board.height).ToList();
                    for (int i = allLines.Count - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        (allLines[i], allLines[j]) = (allLines[j], allLines[i]);
                    }

                    for (int i = 0; i < Random.Range(2, board.height - 2); i++)
                        for (int x = 0; x < board.width; x++)
                            if (board.gems[x, allLines[i]] != null &&
                                ValidateGemPosition(board.gems[x, allLines[i]], x, allLines[i]))
                                allDestroyedGems.Add(board.gems[x, allLines[i]]);
                    break;
                case 8: // 縱向隨機消除
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(board.gem1);
                        allDestroyedGems.Add(board.gem2);
                    }

                    var allColumns = Enumerable.Range(0, board.width).ToList();
                    for (int i = allColumns.Count - 1; i > 0; i--)
                    {
                        int j = Random.Range(0, i + 1);
                        (allColumns[i], allColumns[j]) = (allColumns[j], allColumns[i]);
                    }

                    for (int i = 0; i < Random.Range(2, board.width - 2); i++)
                        for (int y = 0; y < board.height; y++)
                            if (board.gems[allColumns[i], y] != null &&
                                ValidateGemPosition(board.gems[allColumns[i], y], allColumns[i], y))
                                allDestroyedGems.Add(board.gems[allColumns[i], y]);
                    break;
                case 9: // 多重炸彈
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(board.gem1);
                        allDestroyedGems.Add(board.gem2);
                    }

                    var bombPositions = new List<(int x, int y)>();
                    for (int attempts = 0; attempts < 3 && bombPositions.Count < 2; attempts++)
                    {
                        int x = Random.Range(0, board.width), y = Random.Range(0, board.height);
                        if (board.gems[x, y] != null && !bombPositions.Contains((x, y)))
                        {
                            bombPositions.Add((x, y));
                            if (board.gems[x, y] != null) board.gems[x, y].id = 102;
                        }
                    }

                    foreach (var pos in bombPositions)
                    {
                        if (board.gems[pos.x, pos.y] != null && ValidateGemPosition(board.gems[pos.x, pos.y], pos.x, pos.y))
                        {
                            allDestroyedGems.Add(board.gems[pos.x, pos.y]);
                            for (int i = 0; i < 8; i++)
                            {
                                int newX = pos.x + dx[i], newY = pos.y + dy[i];
                                if (newX >= 0 && newX < board.width && newY >= 0 && newY < board.height)
                                {
                                    var targetGem = board.gems[newX, newY];
                                    if (targetGem != null && ValidateGemPosition(targetGem, newX, newY))
                                        allDestroyedGems.Add(targetGem);
                                }
                            }
                        }
                    }
                    break;
                case 10: // 橫向三行消除
                    for (int y = Mathf.Max(gem.y - 1, 0); y <= Mathf.Min(gem.y + 1, board.height - 1); y++)
                        for (int x = 0; x < board.width; x++)
                            if (board.gems[x, y] != null && ValidateGemPosition(board.gems[x, y], x, y))
                                allDestroyedGems.Add(board.gems[x, y]);
                    break;
                case 11: // 縱向三行消除
                    for (int x = Mathf.Max(gem.x - 1, 0); x <= Mathf.Min(gem.x + 1, board.width - 1); x++)
                        for (int y = 0; y < board.height; y++)
                            if (board.gems[x, y] != null && ValidateGemPosition(board.gems[x, y], x, y))
                                allDestroyedGems.Add(board.gems[x, y]);
                    break;
            }
        }

    }
}