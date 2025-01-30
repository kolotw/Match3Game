using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3Game
{
    public class BoardResetter
    {
        private Board board;

        public BoardResetter(Board board)
        {
            this.board = board;
        }

        public IEnumerator ResetBoard()
        {
            List<Gem> allGems = new List<Gem>();
            for (int x = 0; x < board.width; x++)
            {
                for (int y = board.height; y >= 0; y--)
                {
                    var gem = board.GetGem(x, y);
                    if (gem != null)
                    {
                        allGems.Add(gem);
                    }
                }
            }

            yield return board.StartCoroutine(board.消失與刪除寶石(allGems));

            yield return board.StartCoroutine(board.FillEmptySpaces(-1));
            board.matchPredictor.ResetPredictionTimer();
        }
    }
}