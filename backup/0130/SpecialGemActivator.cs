using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Match3Game
{
    public enum ActivationSource
    {
        Player,         // �Ѫ��a����Ĳ�o
        ChainReaction  // �Ѩ�L�S���_��Ĳ�o
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
            // �[�J�B�~���ˬd
            if (gem == null || gem.gameObject == null)
            {
                Debug.LogWarning($"���ձҰʤw�g���s�b���S���_��");
                return;
            }

            if (!ValidateGemPosition(gem, gem.x, gem.y))
            {
                Debug.LogWarning($"�S���_�ۦ�m�L��: ({gem.x}, {gem.y})");
                return;
            }

            // �ˬd�O�_�w�g�b���C��
            //if (activationQueue.Any(item => item.gem == gem))
            //{
            //    Debug.Log($"�S���_�� ID: {gem.id} �w�b�Ұʶ��C���A���L���ƱҰ�");
            //    return;
            //}

            // �N�S���_�ۥ[�J���C
            activationQueue.Enqueue((gem, source));

            // �p�G�S�����b�B�z���S���_�ۡA�}�l�B�z���C
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
                var (currentGem, source) = activationQueue.Peek(); // �ϥ� Peek �Ӥ��O Dequeue

                // �A�������_�۪����ĩ�
                if (currentGem == null || currentGem.gameObject == null || !ValidateGemPosition(currentGem, currentGem.x, currentGem.y))
                {
                    Debug.Log($"���L�L�Ī��S���_�� {currentGem.x}, {currentGem.y}");
                    activationQueue.Dequeue(); // �����L�Ī��_��
                    continue;
                }

                // �p�G�_�ۦ��ġA�~�i��B�z
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
                Debug.LogWarning("���ձҰʵL�Ī��S���_��");
                yield break;
            }
            // �]�m�_�۪�Ĳ�o�H��
            gem.SetActivationInfo(source);

            if(source == ActivationSource.Player)
            {
                triggerGem = gem;
            }

            Debug.Log($"�S���_��Ĳ�o - ID: {gem.id}, ��m: ({gem.x}, {gem.y}), " +
              $"Ĳ�o�ӷ�: {source}, �s��h��: {gem.triggerChainDepth}");

            // ���ݥ洫�ʵe����
            while (gem.isAnimating)
            {
                yield return null;
            }

            // �����Ҧ��n�������_��
            List<Gem> allDestroyedGems = new List<Gem>();
            List<Gem> specialGemsToActivate = new List<Gem>();

            board.hasMoveCompleted = false;
            board.matchPredictor?.StopTimer();
            int resType = gem.id - 100;
            board.statusText.text = "������";

            // �N��V�Ʋղ���o�̡A���Ҧ� case ����ϥ�
            int[] dx = { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dy = { -1, 0, 1, -1, 1, -1, 0, 1 };

            // �����Ҧ��ӧO�����ݮɶ��A���������n�������_��
            switch (resType)
            {
                case 0: // LineH
                    lineH(allDestroyedGems, gem);
                    break;

                case 1: // LineV
                    //�p�G����_�۳��O���u�_�ۡA�h�����Q�r��
                    lineV(allDestroyedGems, gem);
                    break;

                case 2: // Bomb
                        // ���[�J�����I
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
                    Debug.Log($"Rainbow �_�۪��ؼ� ID�G{targetId}");

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

                case 7: // ��V�H������
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

                case 8: // �a�V�H������
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

                case 9: // �h�����u
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

                case 10: // ��V�T�����
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

                case 11: // �a�V�T�����
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

            // �b�u���������e�A���ˬd�æ����ݭnĲ�o���S���_��
            foreach (var targetGem in allDestroyedGems.ToList())
            {
                if (targetGem.id >= 100)  // �����T�{�o�O�S���_��
                {
                    // �o�̧ڭ̻ݭn���Y�檺�ˬd�A�T�O�G
                    // 1. ���O��e���b�B�z���_��
                    // 2. ���O�̪�Ĳ�o���_��
                    // 3. �o���_���٨S���Q�]�mĲ�o�H��
                    if (targetGem != gem && targetGem != triggerGem && targetGem.triggerChainDepth == 0)
                    {
                        // �]�mĲ�o�H��
                        targetGem.SetActivationInfo(ActivationSource.ChainReaction, gem);

                        // �N�S���_�ۥ[�J��Ĳ�o�C��
                        specialGemsToActivate.Add(targetGem);

                        Debug.Log($"�S���_�۱N�QĲ�o - ID: {targetGem.id}, " +
                                 $"��m: ({targetGem.x}, {targetGem.y}), " +
                                 $"�ӷ�: {source}, " +
                                 $"�QĲ�o��: {gem.id}, " +
                                 $"�s��`��: {targetGem.triggerChainDepth + 1}");
                    }
                    else
                    {
                        string skipReason = "";
                        if (targetGem == gem)
                        {
                            skipReason = "�O��e�_��";
                        }
                        else if (targetGem == triggerGem)
                        {
                            skipReason = "�OĲ�o�_��";
                        }
                        else if (targetGem.triggerChainDepth > 0)
                        {
                            skipReason = "�w�Q�]�mĲ�o";
                        }
                        else
                        {
                            skipReason = "������]";
                        }

                        Debug.Log($"���L�S���_�� - ID: {targetGem.id}, " +
                                 $"��m: ({targetGem.x}, {targetGem.y}), " +
                                 $"��]: {skipReason}");
                    }
                }
            }

            // �b��������ĪG���e�A���N�S���_�ۥ[�J�Ұʦ�C
            foreach (var specialGem in specialGemsToActivate)
            {
                if (specialGem != null && specialGem.gameObject != null)
                {
                    activationQueue.Enqueue((specialGem, ActivationSource.ChainReaction));
                }
            }

            // �̫�~�����ڪ������ĪG
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