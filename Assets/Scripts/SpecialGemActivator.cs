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
            // 0 : LineH, 1: LineV, 2: Bomb, 3: Rainbow, 4: Cross 5: Bomb big
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