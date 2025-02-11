using UnityEngine;

namespace Match3Game
{
    public class GemFactory
    {
        private Board board;
        private GameObject[] gemPrefabs;

        public GemFactory(Board board, GameObject[] gemPrefabs)
        {
            this.board = board;
            this.gemPrefabs = board.gemPrefabs;
        }
        public void CreateGem(int x, int y)
        {
            int gemId = Random.Range(0, gemPrefabs.Length);
            while (IsGemMatchingAtPosition(gemId, x, y))
            {
                gemId = Random.Range(0, gemPrefabs.Length);
            }
            GameObject gemObj = Object.Instantiate(gemPrefabs[gemId], board.transform);
            Gem gem = gemObj.GetComponent<Gem>();
            gem.Init(gemId, x, y);
            board.gems[x, y] = gem;
        }

        private bool IsGemMatchingAtPosition(int gemId, int x, int y)
        {
            if (x > 1)
            {
                if (board.gems[x - 1, y] != null && board.gems[x - 2, y] != null &&
                    board.gems[x - 1, y].id == gemId && board.gems[x - 2, y].id == gemId)
                    return true;
            }
            if (y > 1)
            {
                if (board.gems[x, y - 1] != null && board.gems[x, y - 2] != null &&
                    board.gems[x, y - 1].id == gemId && board.gems[x, y - 2].id == gemId)
                    return true;
            }
            return false;
        }
    }
}