using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Match3Game
{
    public class MatchPredictor : MonoBehaviour
    {
        private Board board;
        private GameObject highlightPrefab;
        private List<GameObject> highlights = new List<GameObject>();
        private int currentMatchIndex = 0;
        private float timer = 0f;
        private const float HINT_DELAY = 3f;
        private bool isMouseDown = false;
        private bool isResetting = false;

        void Start()
        {
            board = Board.instance;
            highlightPrefab = Resources.Load<GameObject>("Highlight");
        }

        void Update()
        {
            if (isResetting) return;

            if (Input.GetMouseButtonDown(0))
            {
                isMouseDown = true;
                StopTimer();
            }
            else if (Input.GetMouseButtonUp(0))
            {
                isMouseDown = false;
                StopTimer();
            }

            if (!isMouseDown && board.hasMoveCompleted)
            {
                timer += Time.deltaTime;
                if (timer >= HINT_DELAY)
                {
                    ShowPossibleMatches();
                    timer = 0f;
                }
            }
        }

        public void StopTimer()
        {
            timer = 0f;
            ClearHighlights();
        }

        void ShowPossibleMatches()
        {
            if (isResetting) return;

            ClearHighlights();
            List<PossibleMatch> matches = FindPossibleMatches();

            if (matches.Count == 0)
            {
                Debug.Log("NO Move");
                StartCoroutine(ResetBoard());
                return;
            }

            currentMatchIndex = (currentMatchIndex + 1) % matches.Count;
            var match = matches[currentMatchIndex];
            StartCoroutine(ShowSingleMatch(match));
        }

        private IEnumerator ShowSingleMatch(PossibleMatch match)
        {
            CreateHighlight(match.x1, match.y1);
            CreateHighlight(match.x2, match.y2);
            yield return new WaitForSeconds(1f);
            ClearHighlights();
        }

        private IEnumerator ResetBoard()
        {
            isResetting = true;

            // 收集所有寶石
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

            yield return StartCoroutine(board.消失與刪除寶石(allGems));

            // 重新填滿棋盤
            yield return StartCoroutine(board.FillEmptySpaces(-1));
            isResetting = false;
            ResetPredictionTimer();
        }

        public void ResetPredictionTimer()
        {
            timer = 0f;
            isMouseDown = false;
            ClearHighlights();
        }

        void CreateHighlight(int x, int y)
        {
            GameObject highlight = Instantiate(highlightPrefab, transform);
            highlight.transform.position = new Vector3(x, y, 0);
            highlights.Add(highlight);
        }

        void ClearHighlights()
        {
            foreach (var highlight in highlights)
            {
                Destroy(highlight);
            }
            highlights.Clear();
        }

        List<PossibleMatch> FindPossibleMatches()
        {
            List<PossibleMatch> possibleMatches = new List<PossibleMatch>();

            for (int y = 0; y < board.height; y++)
            {
                for (int x = 0; x < board.width - 1; x++)
                {
                    if (CheckPossibleMatch(x, y, x + 1, y))
                    {
                        possibleMatches.Add(new PossibleMatch(x, y, x + 1, y));
                    }
                }
            }

            for (int x = 0; x < board.width; x++)
            {
                for (int y = 0; y < board.height - 1; y++)
                {
                    if (CheckPossibleMatch(x, y, x, y + 1))
                    {
                        possibleMatches.Add(new PossibleMatch(x, y, x, y + 1));
                    }
                }
            }

            return possibleMatches;
        }

        bool CheckPossibleMatch(int x1, int y1, int x2, int y2)
        {
            Gem gem1 = board.GetGem(x1, y1);
            Gem gem2 = board.GetGem(x2, y2);
            if (gem1 == null || gem2 == null) return false;

            int gem1Id = gem1.id;
            int gem2Id = gem2.id;

            gem1.id = gem2Id;
            gem2.id = gem1Id;

            bool hasMatch = CheckMatchAfterSwap(x1, y1) ||
                           CheckMatchAfterSwap(x2, y2);

            gem1.id = gem1Id;
            gem2.id = gem2Id;

            return hasMatch;
        }

        bool CheckMatchAfterSwap(int x, int y)
        {
            Gem currentGem = board.GetGem(x, y);
            if (currentGem == null) return false;

            // 水平檢查
            if (x >= 2 &&
                board.GetGem(x - 2, y)?.id == board.GetGem(x - 1, y)?.id &&
                board.GetGem(x - 1, y)?.id == currentGem.id)
                return true;

            if (x >= 1 && x < board.width - 1 &&
                board.GetGem(x - 1, y)?.id == currentGem.id &&
                currentGem.id == board.GetGem(x + 1, y)?.id)
                return true;

            if (x < board.width - 2 &&
                currentGem.id == board.GetGem(x + 1, y)?.id &&
                board.GetGem(x + 1, y)?.id == board.GetGem(x + 2, y)?.id)
                return true;

            // 垂直檢查
            if (y >= 2 &&
                board.GetGem(x, y - 2)?.id == board.GetGem(x, y - 1)?.id &&
                board.GetGem(x, y - 1)?.id == currentGem.id)
                return true;

            if (y >= 1 && y < board.height - 1 &&
                board.GetGem(x, y - 1)?.id == currentGem.id &&
                currentGem.id == board.GetGem(x, y + 1)?.id)
                return true;

            if (y < board.height - 2 &&
                currentGem.id == board.GetGem(x, y + 1)?.id &&
                board.GetGem(x, y + 1)?.id == board.GetGem(x, y + 2)?.id)
                return true;

            return false;
        }

        struct PossibleMatch
        {
            public int x1, y1, x2, y2;

            public PossibleMatch(int x1, int y1, int x2, int y2)
            {
                this.x1 = x1;
                this.y1 = y1;
                this.x2 = x2;
                this.y2 = y2;
            }
        }
    }
}