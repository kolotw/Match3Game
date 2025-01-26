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
            board.StartCoroutine(ActivateSpecialGemSequentially(gem));
        }

        private IEnumerator ActivateSpecialGemSequentially(Gem gem)
        {
            board.hasMoveCompleted = false;
            board.matchPredictor?.StopTimer();
            int resType = gem.id - 100;
            List<Gem> allDestroyedGems = new List<Gem>();
            board.statusText.text = "消除中";

            switch (resType)
            {
                case 0: // LineH
                    for (int x = 0; x < board.width; x++)
                    {
                        if (board.gems[x, gem.y] != null)
                        {
                            allDestroyedGems.Add(board.gems[x, gem.y]);
                            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                        }
                    }
                    break;

                case 1: // LineV
                    for (int y = 0; y < board.height; y++)
                    {
                        if (board.gems[gem.x, y] != null)
                        {
                            allDestroyedGems.Add(board.gems[gem.x, y]);
                            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                        }
                    }
                    break;

                case 2: // Cross
                    for (int x = 0; x < board.width; x++)
                    {
                        if (board.gems[x, gem.y] != null)
                        {
                            allDestroyedGems.Add(board.gems[x, gem.y]);
                            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                        }
                    }
                    for (int y = 0; y < board.height; y++)
                    {
                        if (board.gems[gem.x, y] != null && board.gems[gem.x, y] != gem)
                        {
                            allDestroyedGems.Add(board.gems[gem.x, y]);
                            yield return new WaitForSeconds(Board.COLLECT_DELAY);
                        }
                    }
                    break;

                case 3: // Bomb
                    List<Gem> bombGems = new List<Gem>();
                    for (int x = 0; x < board.width; x++)
                        for (int y = 0; y < board.height; y++)
                            if (board.gems[x, y] != null)
                                bombGems.Add(board.gems[x, y]);
                    foreach (var bombGem in bombGems)
                    {
                        allDestroyedGems.Add(bombGem);
                        yield return new WaitForSeconds(Board.COLLECT_DELAY);
                    }
                    break;
            }

            board.statusText.text = "消除中";
            yield return board.StartCoroutine(board.FadeAndDestroyGems(allDestroyedGems));
            yield return new WaitForSeconds(Board.COMPLETE_DELAY);
        }
    }
}