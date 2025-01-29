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
            // 0 : LineH, 1: LineV, 2: Bomb, 3: Rainbow, 4: Cross 5: Bomb big 5x5 6: Destroy All Gems 7: �� �P �P�������� 8: �� �P �P�������� 9: 3x3 Bomb 10: 3x1 Bomb 11: 1x3 Bomb
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
                        // ���[�J�����I
                    if (ValidateGemPosition(gem, gem.x, gem.y))
                    {
                        allDestroyedGems.Add(gem);
                        yield return new WaitForSeconds(Board.COLLECT_DELAY);
                    }

                    // ������Ī��ˬd�d��
                    int minX = gem.x - 2 < 0 ? 0 : gem.x - 2;
                    int maxX = gem.x + 2 >= board.width ? board.width - 1 : gem.x + 2;
                    int minY = gem.y - 2 < 0 ? 0 : gem.y - 2;
                    int maxY = gem.y + 2 >= board.height ? board.height - 1 : gem.y + 2;

                    // �M�����Ľd�򤺪��Ҧ���l
                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int y = minY; y <= maxY; y++)
                        {
                            // ���L�����I�A�]���w�g�ˬd�L�F
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
                    // �� �P �P��������
                    // �H���M�w�n�����X�C
                    int linesToDestroy = Random.Range(2, board.height - 2); // �ܤ֮���1��A�̦h������ӴѽL�����
                    Debug.Log($"�n�������C�ơG{linesToDestroy}");

                    // �Ыؤ@�ӥ]�t�Ҧ��i��渹���C��A�å��ö���
                    List<int> allLines = new List<int>();
                    for (int y = 0; y < board.height; y++)
                    {
                        allLines.Add(y);
                    }
                    // ���æC����
                    for (int i = allLines.Count - 1; i > 0; i--)
                    {
                        int randomIndex = Random.Range(0, i + 1);
                        int temp = allLines[i];
                        allLines[i] = allLines[randomIndex];
                        allLines[randomIndex] = temp;
                    }

                    // ���e linesToDestroy �Ӧ渹�i�����
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
                    // �� �P �P��������
                    // �H���M�w�n�����X��
                    int columnsToDestroy = Random.Range(2, board.width - 2); // �ܤ֮���1�C�A�̦h������ӴѽL���C��
                    Debug.Log($"�n��������ơG{columnsToDestroy}");

                    // �Ыؤ@�ӥ]�t�Ҧ��i��渹���C��A�å��ö���
                    List<int> allColumns = new List<int>();
                    for (int x = 0; x < board.width; x++)
                    {
                        allColumns.Add(x);
                    }
                    // ���æC����
                    for (int i = allColumns.Count - 1; i > 0; i--)
                    {
                        int randomIndex = Random.Range(0, i + 1);
                        int temp = allColumns[i];
                        allColumns[i] = allColumns[randomIndex];
                        allColumns[randomIndex] = temp;
                    }

                    // ���e columnsToDestroy �ӦC���i�����
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
                    // ����X�n��m���u����m
                    List<(int x, int y)> bombPositions = new List<(int x, int y)>();
                    int attempts = 0;
                    while (bombPositions.Count < 2 && attempts < 100) // �[�J�̤j���զ����קK�L���j��
                    {
                        attempts++;
                        int x = Random.Range(0, board.width);
                        int y = Random.Range(0, board.height);

                        // �ˬd�o�Ӧ�m�O�_���ĥB���Q�襤
                        if (board.gems[x, y] != null && !bombPositions.Contains((x, y)))
                        {
                            bombPositions.Add((x, y));
                        }
                    }

                    //Debug.Log($"��� {bombPositions.Count} �Ӧ�m�ө�m���u");

                    // ���N�Ҧ���m���_���ܦ����u
                    foreach (var pos in bombPositions)
                    {
                        if (board.gems[pos.x, pos.y] != null)
                        {
                            board.gems[pos.x, pos.y].id = 102;
                        }
                    }

                    // ���ݤ@�p�q�ɶ������a�ݨ쬵�u�ͦ�
                    yield return new WaitForSeconds(0.5f);

                    // �A�̧Ǥ��z�C�Ӭ��u
                    foreach (var pos in bombPositions)
                    {
                        var bombGem = board.gems[pos.x, pos.y];
                        if (bombGem != null)
                        {
                            // �B�z�����I
                            if (ValidateGemPosition(bombGem, pos.x, pos.y))
                            {
                                allDestroyedGems.Add(bombGem);
                                yield return new WaitForSeconds(Board.COLLECT_DELAY);
                            }

                            // �B�z�P��8��
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
                    // �Hid102���D���a�V3�����
                    // �T�w�n�������C�d��
                    int startX = Mathf.Max(gem.x - 1, 0);  // ����ɡA���p��0
                    int endX = Mathf.Min(gem.x + 1, board.width - 1);  // �k��ɡA���W�L�ѽL�e��

                    // �������w�d�򤺪��Ҧ��_��
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
                    // �Hid102���D����V3�����
                    // �T�w�n��������d��
                    int startY = Mathf.Max(gem.y - 1, 0);  // �U��ɡA���p��0
                    int endY = Mathf.Min(gem.y + 1, board.height - 1);  // �W��ɡA���W�L�ѽL����

                    // �������w�d�򤺪��Ҧ��_��
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

            board.statusText.text = "������";
            if (allDestroyedGems.Count > 0)
            {
                yield return board.StartCoroutine(board.FadeAndDestroyGems(allDestroyedGems));
            }
            yield return new WaitForSeconds(Board.COMPLETE_DELAY);
        }
    }
}