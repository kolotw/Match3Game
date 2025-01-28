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
                Debug.LogWarning($"�S���_�ۦ�m�L��: ({gem.x}, {gem.y})");
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
            // ���e�ˬd�_�۩M�C�����󪺦��ĩ�
            if (gem == null || gem.gameObject == null)
            {
                Debug.LogWarning("���ձҰʵL�Ī��S���_��");
                yield break;
            }

            board.hasMoveCompleted = false;
            board.matchPredictor?.StopTimer();
            int resType = gem.id - 100;
            List<Gem> allDestroyedGems = new List<Gem>();
            board.statusText.text = "������";

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

                //case 2: // Cross
                //    for (int x = 0; x < board.width; x++)
                //    {
                //        var targetGem = board.gems[x, gem.y];
                //        if (targetGem != null && ValidateGemPosition(targetGem, x, gem.y))
                //        {
                //            allDestroyedGems.Add(targetGem);
                //            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                //        }
                //    }
                //    for (int y = 0; y < board.height; y++)
                //    {
                //        var targetGem = board.gems[gem.x, y];
                //        if (targetGem != null && targetGem != gem &&
                //            ValidateGemPosition(targetGem, gem.x, y))
                //        {
                //            allDestroyedGems.Add(targetGem);
                //            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                //        }
                //    }
                //    break;

                case 2: // Bomb
                        // ���[�J�����I
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                        yield return new WaitForSeconds(Board.COLLECT_DELAY);
                    }
                    // �w�q�n�ˬd���۹�y��
                    int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
                    int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

                    // �ˬd�����I�P��8�Ӧ�m
                    for (int i = 0; i < 8; i++)
                    {
                        int newX = gem.x + dx[i];
                        int newY = gem.y + dy[i];

                        // �ˬd�y�ЬO�_�b��ɤ�
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
                ////Destroy All Gems
                //List<Gem> bombGems = new List<Gem>();
                //for (int x = 0; x < board.width; x++)
                //{
                //    for (int y = 0; y < board.height; y++)
                //    {
                //        var targetGem = board.gems[x, y];
                //        if (targetGem != null && ValidateGemPosition(targetGem, x, y))
                //        {
                //            bombGems.Add(targetGem);
                //        }
                //    }
                //}
                //foreach (var bombGem in bombGems)
                //{
                //    allDestroyedGems.Add(bombGem);
                //    yield return new WaitForSeconds(Board.COLLECT_DELAY);
                //}
                //break;
                case 3: // Rainbow
                        // ���N Rainbow �_�ۦۤv�[�J�R���C��
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                        yield return new WaitForSeconds(Board.COLLECT_DELAY);
                    }

                    // ���o�Q�����_�۪� ID
                    int targetId = (board.gem1?.id == gem.id) ? board.gem2?.id ?? 1 : board.gem1?.id ?? 1;

                    // �j�M��ӴѽL�M��ۦP ID ���_��
                    for (int x = 0; x < board.width; x++)
                    {
                        for (int y = 0; y < board.height; y++)
                        {
                            var targetGem = board.gems[x, y];
                            if (targetGem != null &&
                                targetGem.id == targetId &&
                                targetGem != gem && // �T�O���|���ƧR�� Rainbow �_��
                                ValidateGemPosition(targetGem, x, y))
                            {
                                allDestroyedGems.Add(targetGem);
                                yield return new WaitForSeconds(Board.COLLECT_DELAY);
                            }
                        }
                    }
                    break;
            }

            board.statusText.text = "������";
            if (allDestroyedGems.Count > 0)
            {
                yield return board.StartCoroutine(board.FadeAndDestroyGems(allDestroyedGems));
            }
            yield return new WaitForSeconds(Board.COMPLETE_DELAY);
        }
    }
}