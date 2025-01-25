using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Match3Game
{
    public class Board : MonoBehaviour
    {
        public static Board instance;
        public GameObject[] gemPrefabs;
        public int width = 8;
        public int height = 8;
        public float gemMoveSpeed = 5f;

        private Gem[,] gems;

        private void Awake()
        {
            instance = this;
        }

        void Start()
        {
            gems = new Gem[width, height];
            SetupBoard();
        }

        public Gem GetGem(int x, int y)
        {
            if (x >= 0 && x < width && y >= 0 && y < height)
                return gems[x, y];
            return null;
        }

        void SetupBoard()
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    CreateGem(x, y);
                }
            }
        }

        void CreateGem(int x, int y)
        {
            int gemId = Random.Range(0, gemPrefabs.Length);

            while (IsGemMatchingAtPosition(gemId, x, y))
            {
                gemId = Random.Range(0, gemPrefabs.Length);
            }

            GameObject gemObj = Instantiate(gemPrefabs[gemId], transform);
            Gem gem = gemObj.GetComponent<Gem>();

            gem.Init(gemId, x, y);
            gems[x, y] = gem;
        }

        bool IsGemMatchingAtPosition(int gemId, int x, int y)
        {
            if (x > 1)
            {
                if (gems[x - 1, y] != null && gems[x - 2, y] != null &&
                    gems[x - 1, y].id == gemId && gems[x - 2, y].id == gemId)
                    return true;
            }

            if (y > 1)
            {
                if (gems[x, y - 1] != null && gems[x, y - 2] != null &&
                    gems[x, y - 1].id == gemId && gems[x, y - 2].id == gemId)
                    return true;
            }

            return false;
        }

        public void TrySwapGems(int x1, int y1, int x2, int y2)
        {
            StartCoroutine(SwapGemsRoutine(x1, y1, x2, y2));
        }

        private IEnumerator SwapGemsRoutine(int x1, int y1, int x2, int y2)
        {
            Gem gem1 = gems[x1, y1];
            Gem gem2 = gems[x2, y2];

            gems[x1, y1] = gem2;
            gems[x2, y2] = gem1;

            float swapDuration = 0.3f;

            StartCoroutine(gem1.AnimateMove(new Vector3(x2, y2, 0), swapDuration));
            StartCoroutine(gem2.AnimateMove(new Vector3(x1, y1, 0), swapDuration));

            yield return new WaitForSeconds(swapDuration);

            if (!CheckForMatches())
            {
                gems[x1, y1] = gem1;
                gems[x2, y2] = gem2;

                StartCoroutine(gem1.AnimateMove(new Vector3(x1, y1, 0), swapDuration));
                StartCoroutine(gem2.AnimateMove(new Vector3(x2, y2, 0), swapDuration));

                yield return new WaitForSeconds(swapDuration);
            }
            else
            {
                gem1.x = x2;
                gem1.y = y2;
                gem2.x = x1;
                gem2.y = y1;

                yield return new WaitForSeconds(0.2f);
                DestroyMatches();
            }
        }

        void CheckAllMatches()
        {
            // 檢查水平方向
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width - 2; x++)
                {
                    CheckHorizontalMatch(x, y);
                }
            }

            // 檢查垂直方向  
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height - 2; y++)
                {
                    CheckVerticalMatch(x, y);
                }
            }
        }

        bool CheckHorizontalMatch(int x, int y)
        {
            if (gems[x, y] == null) return false;

            if (x < width - 2 &&
                gems[x + 1, y] != null &&
                gems[x + 2, y] != null &&
                gems[x, y].id == gems[x + 1, y].id &&
                gems[x + 1, y].id == gems[x + 2, y].id)
            {
                gems[x, y].isMatched = true;
                gems[x + 1, y].isMatched = true;
                gems[x + 2, y].isMatched = true;
                return true;
            }
            return false;
        }

        bool CheckVerticalMatch(int x, int y)
        {
            if (gems[x, y] == null) return false;

            if (y < height - 2 &&
                gems[x, y + 1] != null &&
                gems[x, y + 2] != null &&
                gems[x, y].id == gems[x, y + 1].id &&
                gems[x, y + 1].id == gems[x, y + 2].id)
            {
                gems[x, y].isMatched = true;
                gems[x, y + 1].isMatched = true;
                gems[x, y + 2].isMatched = true;
                return true;
            }
            return false;
        }

        bool CheckForMatches()
        {
            bool hasMatches = false;
            CheckAllMatches();

            // 檢查是否有任何寶石被標記為matched
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] != null && gems[x, y].isMatched)
                    {
                        hasMatches = true;
                        break;
                    }
                }
                if (hasMatches) break;
            }

            return hasMatches;
        }

        void DestroyMatches()
        {
            List<Gem> matchedGems = new List<Gem>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] != null && gems[x, y].isMatched)
                    {
                        matchedGems.Add(gems[x, y]);
                        gems[x, y] = null;
                    }
                }
            }

            StartCoroutine(FadeAndDestroyGems(matchedGems));
        }

        private IEnumerator FadeAndDestroyGems(List<Gem> gemsToDestroy)
        {
            foreach (var gem in gemsToDestroy)
            {
                SpriteRenderer renderer = gem.GetComponent<SpriteRenderer>();
                float alpha = 1f;
                Color startColor = renderer.color;

                while (alpha > 0)
                {
                    alpha -= Time.deltaTime * 5f;
                    renderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                    yield return null;
                }

                Destroy(gem.gameObject);
            }

            StartCoroutine(MakeGemsFall());
        }

        private IEnumerator MakeGemsFall()
        {
            bool hasFalling;
            do
            {
                hasFalling = false;
                int animatingGems = 0;

                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height - 1; y++)
                    {
                        if (gems[x, y] == null && gems[x, y + 1] != null)
                        {
                            gems[x, y] = gems[x, y + 1];
                            gems[x, y + 1] = null;
                            gems[x, y].y = y;

                            animatingGems++;
                            StartCoroutine(WaitForGemFall(gems[x, y], x, y, () => animatingGems--));
                            hasFalling = true;
                        }
                    }
                }

                if (hasFalling)
                {
                    while (animatingGems > 0)
                    {
                        yield return new WaitForSeconds(0.05f);
                    }
                }
            } while (hasFalling);

            StartCoroutine(FillEmptySpaces());
        }

        private IEnumerator WaitForGemFall(Gem gem, int x, int y, System.Action onComplete)
        {
            yield return StartCoroutine(gem.AnimateMove(
                new Vector3(x, y, 0),
                0.3f / gemMoveSpeed
            ));
            onComplete?.Invoke();
        }

        private IEnumerator FillEmptySpaces()
        {
            for (int x = 0; x < width; x++)
            {
                int dropDelay = 0;
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] == null)
                    {
                        CreateGem(x, y);
                        gems[x, y].transform.position = new Vector3(x, height + dropDelay, 0);

                        StartCoroutine(gems[x, y].AnimateMove(
                            new Vector3(x, y, 0),
                            0.3f / gemMoveSpeed
                        ));

                        dropDelay++;
                        yield return new WaitForSeconds(0.1f);
                    }
                }
            }

            yield return new WaitForSeconds(0.3f);

            if (CheckForMatches())
            {
                yield return new WaitForSeconds(0.2f);
                DestroyMatches();
            }
            else if (!HasValidMoves())
            {
                Debug.Log("No valid moves available!");
            }
        }

        private bool HasValidMoves()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width - 1; x++)
                {
                    if (CheckSwapForMatch(x, y, x + 1, y))
                        return true;
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height - 1; y++)
                {
                    if (CheckSwapForMatch(x, y, x, y + 1))
                        return true;
                }
            }

            return false;
        }

        private bool CheckSwapForMatch(int x1, int y1, int x2, int y2)
        {
            Gem gem1 = gems[x1, y1];
            Gem gem2 = gems[x2, y2];

            // 暫存兩個寶石的ID
            int gem1Id = gem1.id;
            int gem2Id = gem2.id;

            // 交換位置和ID
            gems[x1, y1] = gem2;
            gems[x2, y2] = gem1;
            gem1.id = gem2Id;
            gem2.id = gem1Id;

            bool hasMatch = CheckForMatches();

            // 還原位置和ID
            gems[x1, y1] = gem1;
            gems[x2, y2] = gem2;
            gem1.id = gem1Id;
            gem2.id = gem2Id;

            // 重置所有寶石的匹配狀態
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] != null)
                        gems[x, y].isMatched = false;
                }
            }

            return hasMatch;
        }
    }
}