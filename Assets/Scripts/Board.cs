using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

namespace Match3Game
{
    public class Board : MonoBehaviour
    {
        public static Board instance;
        public GameObject[] gemPrefabs;
        public GameObject[] resGemPrefabs;
        public int width = 8;
        public int height = 8;
        public float gemMoveSpeed = 5f;
        private Gem[,] gems;
        [SerializeField] private MatchPredictor matchPredictor;
        public bool hasMoveCompleted = false;
        [SerializeField] private Text statusText;
        [SerializeField] private Text gemsText;
        private bool isSwitching = false;
        private Gem gem1, gem2;
        private const float SWAP_DURATION = 0.3f;
        private const float DESTROY_DELAY = 0.2f;
        private const float COLLECT_DELAY = 0.01f;
        private const float COMPLETE_DELAY = 0.1f;

        private class MatchInfo
        {
            public List<Gem> matchedGems = new List<Gem>();
            public bool isHorizontal;
            public bool isVertical;
            public int matchCount => matchedGems.Count;
        }

        private void Awake()
        {
            instance = this;
            matchPredictor = GetComponent<MatchPredictor>();
        }

        public void Start()
        {
            gems = new Gem[width, height];
            hasMoveCompleted = false;
            statusText.text = "進行中";
            matchPredictor?.StopTimer();
            SetupBoard();
            if (hasMoveCompleted)
            {
                statusText.text = "可遊玩";
                matchPredictor?.ResetPredictionTimer();
            }
        }

        private void Update()
        {
            gemsText.text = "Gems: " + transform.childCount;
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
            statusText.text = "可遊玩";
            matchPredictor?.ResetPredictionTimer();
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
            statusText.text = "交換中";
            isSwitching = true;
            gem1 = gems[x1, y1];
            gem2 = gems[x2, y2];
            gems[x1, y1] = gem2;
            gems[x2, y2] = gem1;

            float swapDuration = SWAP_DURATION / gemMoveSpeed;
            StartCoroutine(gem1.AnimateMove(new Vector3(x2, y2, 0), swapDuration));
            StartCoroutine(gem2.AnimateMove(new Vector3(x1, y1, 0), swapDuration));
            yield return new WaitForSeconds(swapDuration);

            gem1.x = x2;
            gem1.y = y2;
            gem2.x = x1;
            gem2.y = y1;

            bool hasMatches = CheckForMatches();
            if (gem1.id == 103 || gem2.id == 103) // Bomb直接觸發
            {
                ActivateResourceGem(gem1.id == 103 ? gem1 : gem2);
            }
            else if (hasMatches)
            {
                yield return new WaitForSeconds(DESTROY_DELAY);
                DestroyMatches(true, x2, y2);
                yield return new WaitForSeconds(DESTROY_DELAY);
                if (gem1.id >= 100 || gem2.id >= 100)
                {
                    ActivateResourceGem(gem1.id >= 100 ? gem1 : gem2);
                }
            }
            else if (gem1.id >= 100 || gem2.id >= 100)
            {
                ActivateResourceGem(gem1.id >= 100 ? gem1 : gem2);
            }
            else
            {
                gems[x1, y1] = gem1;
                gems[x2, y2] = gem2;
                StartCoroutine(gem1.AnimateMove(new Vector3(x1, y1, 0), swapDuration));
                StartCoroutine(gem2.AnimateMove(new Vector3(x2, y2, 0), swapDuration));
                yield return new WaitForSeconds(swapDuration);
                hasMoveCompleted = true;
                statusText.text = "可遊玩";
                matchPredictor?.ResetPredictionTimer();
            }
            isSwitching = false;
        }

        private List<MatchInfo> FindAllMatches()
        {
            List<MatchInfo> allMatches = new List<MatchInfo>();
            bool[,] processed = new bool[width, height];

            // 檢查水平方向
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width - 2; x++)
                {
                    if (processed[x, y] || gems[x, y] == null) continue;

                    MatchInfo match = CheckMatch(x, y, true);
                    if (match != null)
                    {
                        foreach (var gem in match.matchedGems)
                        {
                            processed[gem.x, gem.y] = true;
                        }
                        allMatches.Add(match);
                    }
                }
            }

            // 檢查垂直方向
            processed = new bool[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height - 2; y++)
                {
                    if (processed[x, y] || gems[x, y] == null) continue;

                    MatchInfo match = CheckMatch(x, y, false);
                    if (match != null)
                    {
                        foreach (var gem in match.matchedGems)
                        {
                            processed[gem.x, gem.y] = true;
                        }
                        allMatches.Add(match);
                    }
                }
            }

            return allMatches;
        }

        private MatchInfo CheckMatch(int startX, int startY, bool horizontal)
        {
            MatchInfo match = new MatchInfo();
            int currentId = gems[startX, startY].id;
            int x = startX, y = startY;

            while (true)
            {
                if (horizontal)
                {
                    if (x >= width || gems[x, y] == null || gems[x, y].id != currentId)
                        break;
                    match.matchedGems.Add(gems[x, y]);
                    x++;
                }
                else
                {
                    if (y >= height || gems[x, y] == null || gems[x, y].id != currentId)
                        break;
                    match.matchedGems.Add(gems[x, y]);
                    y++;
                }
            }

            if (match.matchCount >= 3)
            {
                match.isHorizontal = horizontal;
                match.isVertical = !horizontal;
                return match;
            }

            return null;
        }

        public bool CheckForMatches()
        {
            if (isSwitching && (gem1.id >= 100 || gem2.id >= 100))
                return false;

            var matches = FindAllMatches();
            foreach (var match in matches)
            {
                foreach (var gem in match.matchedGems)
                {
                    gem.isMatched = true;
                }
            }

            return matches.Count > 0;
        }

        private void DestroyMatches(bool isFromInteraction = false, int interactX = -1, int interactY = -1)
        {
            var matches = FindAllMatches();
            List<Gem> allMatchedGems = new List<Gem>();

            foreach (var match in matches)
            {
                allMatchedGems.AddRange(match.matchedGems);
            }

            StartCoroutine(DestroyMatchesSequence(allMatchedGems, isFromInteraction, interactX, interactY));
        }
        private IEnumerator DestroyMatchesSequence(List<Gem> matchedGems, bool isFromInteraction, int interactX, int interactY)
        {
            HashSet<Gem> processedGems = new HashSet<Gem>();
            List<(Gem gem, SpriteRenderer renderer, Color startColor)> fadeData = new List<(Gem, SpriteRenderer, Color)>();
            Dictionary<int, List<Gem>> matchGroups = new Dictionary<int, List<Gem>>();

            // 整理要消除的寶石
            foreach (var gem in matchedGems)
            {
                if (!processedGems.Contains(gem) && gem != null)
                {
                    processedGems.Add(gem);
                    if (gems[gem.x, gem.y] == gem)
                    {
                        gems[gem.x, gem.y] = null;

                        // 按ID分組以檢查特殊組合
                        if (!matchGroups.ContainsKey(gem.id))
                        {
                            matchGroups[gem.id] = new List<Gem>();
                        }
                        matchGroups[gem.id].Add(gem);
                    }

                    // 收集漸變數據
                    var renderer = gem.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        fadeData.Add((gem, renderer, renderer.color));
                    }
                }
            }

            // 執行漸變
            float alpha = 1f;
            while (alpha > 0)
            {
                alpha -= Time.deltaTime * gemMoveSpeed * 3f;
                foreach (var (gem, renderer, startColor) in fadeData)
                {
                    if (gem != null && gem.gameObject != null && renderer != null)
                    {
                        renderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                    }
                }
                yield return null;
            }

            // 銷毀物件
            foreach (var (gem, renderer, _) in fadeData)
            {
                if (gem != null && gem.gameObject != null)
                {
                    Destroy(gem.gameObject);
                }
            }

            yield return new WaitForSeconds(DESTROY_DELAY);

            // 檢查並生成特殊寶石
            foreach (var group in matchGroups)
            {
                var gems = group.Value;
                if (gems.Count >= 4)
                {
                    int createX = isFromInteraction ? interactX : gems[0].x;
                    int createY = isFromInteraction ? interactY : gems[0].y;

                    bool isHorizontal = gems.All(g => g.y == gems[0].y);
                    bool isVertical = gems.All(g => g.x == gems[0].x);

                    if (gems.Count >= 5)
                    {
                        CreateResourceGem(createX, createY, 3); // Bomb
                    }
                    else if (isHorizontal && isVertical)
                    {
                        CreateResourceGem(createX, createY, 2); // Cross
                    }
                    else if (isHorizontal)
                    {
                        CreateResourceGem(createX, createY, 0); // LineH
                    }
                    else if (isVertical)
                    {
                        CreateResourceGem(createX, createY, 1); // LineV
                    }
                }
            }

            CheckAndCleanupGems();
            yield return StartCoroutine(MakeGemsFall());
        }
        public IEnumerator FadeAndDestroyGems(List<Gem> gemsToDestroy)
        {
            var gemData = gemsToDestroy.Select(gem => new {
                Gem = gem,
                Renderer = gem.GetComponent<SpriteRenderer>(),
                StartColor = gem.GetComponent<SpriteRenderer>().color
            }).ToList();

            foreach (var data in gemData)
            {
                gems[data.Gem.x, data.Gem.y] = null;
            }

            foreach (var data in gemData)
            {
                float alpha = 1f;
                while (alpha > 0)
                {
                    alpha -= Time.deltaTime * gemMoveSpeed * 3f;
                    if (data.Renderer != null)
                    {
                        data.Renderer.color = new Color(
                            data.StartColor.r,
                            data.StartColor.g,
                            data.StartColor.b,
                            alpha
                        );
                    }
                    yield return null;
                }
                if (data.Gem != null && data.Gem.gameObject != null)
                {
                    Destroy(data.Gem.gameObject);
                }
            }

            yield return StartCoroutine(MakeGemsFall());
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
            statusText.text = "填滿中";
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
                DestroyMatches(false);
            }
            else if (!statusText.text.Equals("消除中"))
            {
                hasMoveCompleted = true;
                statusText.text = "可遊玩";
                matchPredictor?.ResetPredictionTimer();
            }
            CheckAndCleanupGems();
        }

        private void CreateResourceGem(int x, int y, int resType)
        {
            GameObject gemObj = Instantiate(resGemPrefabs[resType], transform);
            Gem gem = gemObj.GetComponent<Gem>();
            gem.Init(100 + resType, x, y);
            gems[x, y] = gem;
        }

        public void ActivateResourceGem(Gem gem)
        {
            StartCoroutine(ActivateResourceGemSequentially(gem));
        }

        private IEnumerator ActivateResourceGemSequentially(Gem gem)
        {
            hasMoveCompleted = false;
            matchPredictor?.StopTimer();
            int resType = gem.id - 100;
            List<Gem> allDestroyedGems = new List<Gem>();
            statusText.text = "消除中";

            switch (resType)
            {
                case 0: // LineH
                    for (int x = 0; x < width; x++)
                    {
                        if (gems[x, gem.y] != null)
                        {
                            allDestroyedGems.Add(gems[x, gem.y]);
                            yield return new WaitForSeconds(COLLECT_DELAY);
                        }
                    }
                    break;

                case 1: // LineV
                    for (int y = 0; y < height; y++)
                    {
                        if (gems[gem.x, y] != null)
                        {
                            allDestroyedGems.Add(gems[gem.x, y]);
                            yield return new WaitForSeconds(COLLECT_DELAY);
                        }
                    }
                    break;

                case 2: // Cross
                    for (int x = 0; x < width; x++)
                    {
                        if (gems[x, gem.y] != null)
                        {
                            allDestroyedGems.Add(gems[x, gem.y]);
                            yield return new WaitForSeconds(COLLECT_DELAY);
                        }
                    }
                    for (int y = 0; y < height; y++)
                    {
                        if (gems[gem.x, y] != null && gems[gem.x, y] != gem)
                        {
                            allDestroyedGems.Add(gems[gem.x, y]);
                            yield return new WaitForSeconds(COLLECT_DELAY);
                        }
                    }
                    break;

                case 3: // Bomb
                    List<Gem> bombGems = new List<Gem>();
                    for (int x = 0; x < width; x++)
                        for (int y = 0; y < height; y++)
                            if (gems[x, y] != null)
                                bombGems.Add(gems[x, y]);
                    foreach (var bombGem in bombGems)
                    {
                        allDestroyedGems.Add(bombGem);
                        yield return new WaitForSeconds(COLLECT_DELAY);
                    }
                    break;
            }

            statusText.text = "消除中";
            yield return StartCoroutine(FadeAndDestroyGems(allDestroyedGems));
            yield return new WaitForSeconds(COMPLETE_DELAY);
        }

        private void CheckAndCleanupGems()
        {
            // 收集所有應該存在的寶石位置
            HashSet<GameObject> validGems = new HashSet<GameObject>();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] != null)
                    {
                        validGems.Add(gems[x, y].gameObject);
                    }
                }
            }

            // 檢查並刪除不在陣列中的寶石
            foreach (Transform child in transform)
            {
                if (!validGems.Contains(child.gameObject))
                {
                    Debug.Log("Destroying: " + child.gameObject.name);
                    Destroy(child.gameObject);
                }
            }
        }

    }
}