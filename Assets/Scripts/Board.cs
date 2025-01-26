using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Match3Game
{
    public class Board : MonoBehaviour
    {
        public static Board instance;
        public int width = 8;
        public int height = 8;
        public float gemMoveSpeed = 5f;
        [HideInInspector] public Gem[,] gems;
        [SerializeField] public MatchPredictor matchPredictor;
        [SerializeField] public Text statusText;
        [SerializeField] public Text gemsText;
        private bool isSwitching = false;
        private Gem gem1, gem2;
        public const float SWAP_DURATION = 0.2f;
        public const float DESTROY_DELAY = 0.2f;
        public const float COLLECT_DELAY = 0.01f;
        public const float COMPLETE_DELAY = 0.1f;
        public bool hasMoveCompleted = false;

        [SerializeField] private GameObject[] gemPrefabs;
        [SerializeField] private GameObject[] resGemPrefabs;
        private GemFactory gemFactory;
        private MatchFinder matchFinder;
        public SpecialGemActivator specialGemActivator;
        private BoardResetter boardResetter;

        public class MatchInfo
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
            gemFactory = new GemFactory(this, gemPrefabs);
            matchFinder = new MatchFinder(this);
            specialGemActivator = new SpecialGemActivator(this);
            boardResetter = new BoardResetter(this);
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
                    gemFactory.CreateGem(x, y);
                }
            }
            hasMoveCompleted = true;
            statusText.text = "可遊玩";
            matchPredictor?.ResetPredictionTimer();
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

            // 定義觸發點座標
            int triggerX = -1;
            int triggerY = -1;

            bool hasMatches = CheckForMatches();
            if (gem1.id == 103 || gem2.id == 103) // Bomb直接觸發
            {
                specialGemActivator.ActivateSpecialGem(gem1.id == 103 ? gem1 : gem2);
            }
            else if (hasMatches)
            {
                yield return new WaitForSeconds(DESTROY_DELAY);

                // 找出觸發點
                var matches = matchFinder.FindAllMatches();
                foreach (var match in matches)
                {
                    if (match.matchedGems.Any(g => g.x == x1 && g.y == y1) ||
                        match.matchedGems.Any(g => g.x == x2 && g.y == y2))
                    {
                        // 如果匹配包含交換的寶石，記錄觸發點
                        triggerX = match.matchedGems.Any(g => g.x == x1 && g.y == y1) ? x1 : x2;
                        triggerY = match.matchedGems.Any(g => g.x == x1 && g.y == y1) ? y1 : y2;
                        break;
                    }
                }

                DestroyMatches(true, triggerX, triggerY);
                yield return new WaitForSeconds(DESTROY_DELAY);

                if (gem1.id >= 100 || gem2.id >= 100)
                {
                    specialGemActivator.ActivateSpecialGem(gem1.id >= 100 ? gem1 : gem2);
                }
            }
            else if (gem1.id >= 100 || gem2.id >= 100)
            {
                specialGemActivator.ActivateSpecialGem(gem1.id >= 100 ? gem1 : gem2);
            }
            else
            {
                // 明確還原寶石的 x, y 座標
                gem1.x = x1;
                gem1.y = y1;
                gem2.x = x2;
                gem2.y = y2;

                gems[x1, y1] = gem1;
                gems[x2, y2] = gem2;
                StartCoroutine(gem1.AnimateMove(new Vector3(x1, y1, 0), swapDuration));
                StartCoroutine(gem2.AnimateMove(new Vector3(x2, y2, 0), swapDuration));
                yield return new WaitForSeconds(swapDuration);
                gem1.isAnimating = false;
                gem2.isAnimating = false;
                hasMoveCompleted = true;
                statusText.text = "可遊玩";
                matchPredictor?.ResetPredictionTimer();
            }
            isSwitching = false;
        }

        public bool CheckForMatches()
        {
            if (isSwitching && (gem1.id >= 100 || gem2.id >= 100))
                return false;

            var matches = matchFinder.FindAllMatches();
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
            var matches = matchFinder.FindAllMatches();
            List<Gem> allMatchedGems = new List<Gem>();

            foreach (var match in matches)
            {
                allMatchedGems.AddRange(match.matchedGems);
            }

            StartCoroutine(DestroyMatchesSequence(allMatchedGems, isFromInteraction, interactX, interactY));
        }

        private IEnumerator DestroyMatchesSequence(List<Gem> matchedGems, bool isFromInteraction, int interactX, int interactY)
        {
            // 使用 HashSet 確保每個寶石只被處理一次
            HashSet<Gem> processedGems = new HashSet<Gem>();

            // 儲存需要淡出的寶石資訊
            List<(Gem gem, SpriteRenderer renderer, Color startColor)> fadeData = new List<(Gem, SpriteRenderer, Color)>();

            // 依照寶石ID分組，方便後續處理
            Dictionary<int, List<Gem>> matchGroups = new Dictionary<int, List<Gem>>();

            // 處理匹配的寶石
            foreach (var gem in matchedGems)
            {
                // 確保寶石未被重複處理且存在
                if (gem != null && !processedGems.Contains(gem) && gems[gem.x, gem.y] == gem)
                {
                    processedGems.Add(gem);

                    // 從棋盤上移除寶石
                    gems[gem.x, gem.y] = null;

                    // 依照寶石ID分組
                    if (!matchGroups.ContainsKey(gem.id))
                    {
                        matchGroups[gem.id] = new List<Gem>();
                    }
                    matchGroups[gem.id].Add(gem);

                    // 準備淡出效果
                    var renderer = gem.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        fadeData.Add((gem, renderer, renderer.color));
                    }
                }
            }

            // 寶石淡出效果
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

            // 銷毀寶石
            foreach (var (gem, renderer, _) in fadeData)
            {
                if (gem != null && gem.gameObject != null)
                {
                    Destroy(gem.gameObject);
                }
            }

            yield return new WaitForSeconds(DESTROY_DELAY);

            // 生成資源寶石
            foreach (var group in matchGroups)
            {
                var gems = group.Value;
                if (gems.Count >= 4)
                {
                    int createX, createY;
                    bool isHorizontal = false;
                    bool isVertical = false;

                    // 判斷是水平還是垂直配對
                    isHorizontal = gems.All(g => g.y == gems[0].y);
                    isVertical = gems.All(g => g.x == gems[0].x);

                    // 優先使用互動的觸發點
                    if (isFromInteraction)
                    {
                        // 調試訊息：顯示觸發點座標
                        //Debug.Log($"觸發點座標：({interactX}, {interactY})");

                        // 預設使用觸發點
                        createX = interactX;
                        createY = interactY;
                    }
                    else
                    {
                        // 非互動觸發時的中心點邏輯
                        if (isHorizontal)
                        {
                            var orderedGems = gems.OrderBy(g => g.x).ToList();
                            createX = orderedGems[orderedGems.Count / 2].x;
                            createY = gems[0].y;
                        }
                        else if (isVertical)
                        {
                            var orderedGems = gems.OrderBy(g => g.y).ToList();
                            createX = gems[0].x;
                            createY = orderedGems[orderedGems.Count / 2].y;
                        }
                        else
                        {
                            createX = gems[0].x;
                            createY = gems[0].y;
                        }
                    }

                    // 根據配對數量和方向決定資源寶石類型
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
                    // 確保4個寶石的配對也會生成資源寶石
                    else if (gems.Count == 4)
                    {
                        CreateResourceGem(createX, createY, 0); // 預設為橫線
                    }
                }
            }

            yield return StartCoroutine(MakeGemsFall());
            CheckAndCleanUnmanagedGems();
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
                        gemFactory.CreateGem(x, y);
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

            // 保持原有的循環邏輯
            while (CheckForMatches())
            {
                yield return new WaitForSeconds(0.1f);
                DestroyMatches(false);
            }

            if (!statusText.text.Equals("消除中"))
            {
                hasMoveCompleted = true;
                statusText.text = "可遊玩";
                matchPredictor?.ResetPredictionTimer();
            }
            CheckAndCleanUnmanagedGems();
        }

        private void CreateResourceGem(int x, int y, int resType)
        {
            GameObject gemObj = Instantiate(resGemPrefabs[resType], transform);
            Gem gem = gemObj.GetComponent<Gem>();
            gem.Init(100 + resType, x, y);
            gems[x, y] = gem;
        }

        private void CheckAndCleanUnmanagedGems()
        {
            // 收集棋盤上所有的子物件
            List<Transform> childrenToCheck = new List<Transform>();
            foreach (Transform child in transform)
            {
                Gem gemComponent = child.GetComponent<Gem>();

                // 如果有Gem組件
                if (gemComponent != null)
                {
                    // 檢查這個寶石是否在gems數組中的正確位置
                    bool isValidGem =
                        gemComponent.x >= 0 &&
                        gemComponent.x < width &&
                        gemComponent.y >= 0 &&
                        gemComponent.y < height &&
                        gems[gemComponent.x, gemComponent.y] == gemComponent;

                    // 如果不是有效的寶石，標記為刪除
                    if (!isValidGem)
                    {
                        //Debug.Log($"檢測到未管理的寶石：位置 ({gemComponent.x}, {gemComponent.y})");
                        Destroy(child.gameObject);
                    }
                }
            }
        }
    }
}