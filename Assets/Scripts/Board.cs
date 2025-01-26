using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Match3Game
{
    public class Board : MonoBehaviour
    {
        public static Board instance;
        public GameObject[] gemPrefabs;
        public GameObject[] resGemPrefabs;  // 資源寶石prefabs [LineH, LineV, Cross, Bomb]

        public int width = 8;
        public int height = 8;
        public float gemMoveSpeed = 5f;

        private Gem[,] gems;
        [SerializeField] private MatchPredictor matchPredictor;
        public bool hasMoveCompleted = false;

        private void Awake()
        {
            instance = this;
            matchPredictor = GetComponent<MatchPredictor>();
        }

        public void Start()
        {
            gems = new Gem[width, height];
            hasMoveCompleted = false;
            SetupBoard();
            if (hasMoveCompleted)
            {
                matchPredictor?.ResetPredictionTimer();
            }
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
            hasMoveCompleted = true;
            if (hasMoveCompleted)
            {
                matchPredictor?.ResetPredictionTimer();
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
            hasMoveCompleted = false;
            Gem gem1 = gems[x1, y1];
            Gem gem2 = gems[x2, y2];

            gems[x1, y1] = gem2;
            gems[x2, y2] = gem1;

            float swapDuration = 0.3f / gemMoveSpeed;

            StartCoroutine(gem1.AnimateMove(new Vector3(x2, y2, 0), swapDuration));
            StartCoroutine(gem2.AnimateMove(new Vector3(x1, y1, 0), swapDuration));

            yield return new WaitForSeconds(swapDuration);

            // 如果是資源寶石且不是Bomb，直接在新位置觸發效果
            if (gem1.id >= 100 && gem1.id != 103)
            {
                gem1.x = x2;
                gem1.y = y2;
                gem2.x = x1;
                gem2.y = y1;
                ActivateResourceGem(gem1);
            }
            else if (gem2.id >= 100 && gem2.id != 103)
            {
                gem1.x = x2;
                gem1.y = y2;
                gem2.x = x1;
                gem2.y = y1;
                ActivateResourceGem(gem2);
            }
            else if (!CheckForMatches())
            {
                gems[x1, y1] = gem1;
                gems[x2, y2] = gem2;
                StartCoroutine(gem1.AnimateMove(new Vector3(x1, y1, 0), swapDuration));
                StartCoroutine(gem2.AnimateMove(new Vector3(x2, y2, 0), swapDuration));
                yield return new WaitForSeconds(swapDuration);
                hasMoveCompleted = true;
                if (hasMoveCompleted)
                {
                    matchPredictor?.ResetPredictionTimer();
                }
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

        public bool CheckHorizontalMatch(int x, int y)
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

        public bool CheckVerticalMatch(int x, int y)
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

        public bool CheckForMatches()
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
            Dictionary<int, int> idCounts = new Dictionary<int, int>();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] != null && gems[x, y].isMatched)
                    {
                        matchedGems.Add(gems[x, y]);
                        int gemId = gems[x, y].id;

                        if (!idCounts.ContainsKey(gemId))
                            idCounts[gemId] = 0;
                        idCounts[gemId]++;

                        gems[x, y] = null;
                    }
                }
            }

            foreach (var pair in idCounts)
            {
                //Debug.Log($"id {pair.Key}: {pair.Value} 個寶石被消除");                
            }
            //Debug.Log($"總共 {matchedGems.Count} 個寶石被消除");

            StartCoroutine(FadeAndDestroyGems(matchedGems));
            CheckSpecialMatch(matchedGems);
        }

        public IEnumerator FadeAndDestroyGems(List<Gem> gemsToDestroy)
        {
            // 先從board移除引用
            foreach (var gem in gemsToDestroy)
            {
                gems[gem.x, gem.y] = null;
            }

            foreach (var gem in gemsToDestroy)
            {
                SpriteRenderer renderer = gem.GetComponent<SpriteRenderer>();
                float alpha = 1f;
                Color startColor = renderer.color;

                while (alpha > 0)
                {
                    alpha -= Time.deltaTime * gemMoveSpeed * 3f;
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

        public IEnumerator FillEmptySpaces()
        {
            hasMoveCompleted = false;
            matchPredictor?.StopTimer();

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

            yield return new WaitForSeconds(0.1f);

            if (CheckForMatches())
            {
                yield return new WaitForSeconds(0.1f);
                DestroyMatches();
            }
            else
            {
                hasMoveCompleted = true;
                if (hasMoveCompleted)
                {
                    matchPredictor?.ResetPredictionTimer();
                }
            }
        }
        private void CheckSpecialMatch(List<Gem> matchedGems)
        {
            var groups = matchedGems.GroupBy(g => g.id);

            foreach (var group in groups)
            {
                var gems = group.ToList();
                if (gems.Count < 4) continue;

                if (gems.Count >= 5)
                {
                    CreateResourceGem(gems[0].x, gems[0].y, 3); // Bomb
                    continue;
                }

                if (gems.Count == 4)
                {
                    bool isHorizontal = gems.All(g => g.y == gems[0].y);
                    bool isVertical = gems.All(g => g.x == gems[0].x);

                    if (isHorizontal && isVertical)
                    {
                        CreateResourceGem(gems[0].x, gems[0].y, 2); // Cross
                    }
                    else if (isHorizontal)
                    {
                        CreateResourceGem(gems[0].x, gems[0].y, 0); // LineH
                    }
                    else if (isVertical)
                    {
                        CreateResourceGem(gems[0].x, gems[0].y, 1); // LineV
                    }
                }
            }
        }
        private void CreateResourceGem(int x, int y, int resType)
        {
            GameObject gemObj = Instantiate(resGemPrefabs[resType], transform);
            Gem gem = gemObj.GetComponent<Gem>();
            gem.Init(100 + resType, x, y); // 使用100以上的id來標識資源寶石
            gems[x, y] = gem;
        }

        public void ActivateResourceGem(Gem gem)
        {
            StartCoroutine(ActivateResourceGemSequentially(gem));
        }

        private IEnumerator ActivateResourceGemSequentially(Gem gem)
        {
            int resType = gem.id - 100;
            float delay = 0.01f;
            List<Gem> allDestroyedGems = new List<Gem>();

            switch (resType)
            {
                case 0: // LineH
                    for (int x = 0; x < width; x++)
                    {
                        if (gems[x, gem.y] != null)
                        {
                            allDestroyedGems.Add(gems[x, gem.y]);
                            yield return new WaitForSeconds(delay);
                        }
                    }
                    break;

                case 1: // LineV
                    for (int y = 0; y < height; y++)
                    {
                        if (gems[gem.x, y] != null)
                        {
                            allDestroyedGems.Add(gems[gem.x, y]);
                            yield return new WaitForSeconds(delay);
                        }
                    }
                    break;

                case 2: // Cross
                    for (int x = 0; x < width; x++)
                    {
                        if (gems[x, gem.y] != null)
                        {
                            allDestroyedGems.Add(gems[x, gem.y]);
                            yield return new WaitForSeconds(delay);
                        }
                    }
                    for (int y = 0; y < height; y++)
                    {
                        if (gems[gem.x, y] != null && gems[gem.x, y] != gem)
                        {
                            allDestroyedGems.Add(gems[gem.x, y]);
                            yield return new WaitForSeconds(delay);
                        }
                    }
                    break;

                case 3: // Bomb
                    for (int x = 0; x < width; x++)
                        for (int y = 0; y < height; y++)
                            if (gems[x, y] != null)
                                allDestroyedGems.Add(gems[x, y]);
                    break;
            }

            // 全部標記完成後，一起消除並落下
            yield return StartCoroutine(FadeAndDestroyGems(allDestroyedGems));
        }
    }
}