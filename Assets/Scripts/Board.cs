using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Match3Game
{
    public enum GameState
    {
        Ready,
        Swapping,
        Processing,
        Filling,
        Resetting
    }

    public class Board : MonoBehaviour
    {
        #region Variables
        public static Board instance;
        public int width = 8;
        public int height = 8;
        public float gemMoveSpeed = 5f;
        [HideInInspector] public Gem[,] gems;
        [SerializeField] public MatchPredictor matchPredictor;
        [SerializeField] public Text statusText;
        [SerializeField] public Text gemsText;
        [SerializeField] private GameObject[] gemPrefabs;
        [SerializeField] private GameObject[] resGemPrefabs;

        private GameState currentState = GameState.Ready;
        private bool isSwitching = false;
        private Gem gem1, gem2;
        private float lastUpdateTime;
        private int frameCounter;
        private float fps;

        private GemFactory gemFactory;
        private MatchFinder matchFinder;
        public SpecialGemActivator specialGemActivator;
        private BoardResetter boardResetter;

        // Constants
        public const float SWAP_DURATION = 0.2f;
        public const float DESTROY_DELAY = 0.2f;
        public const float COLLECT_DELAY = 0.01f;
        public const float COMPLETE_DELAY = 0.01f;
        private const float FPS_UPDATE_INTERVAL = 0.5f;
        #endregion

        #region Properties
        public GameState CurrentState
        {
            get => currentState;
            private set
            {
                currentState = value;
                UpdateStatusText();
            }
        }

        public bool hasMoveCompleted
        {
            get => CurrentState == GameState.Ready;
            set => CurrentState = value ? GameState.Ready : GameState.Processing;
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            instance = this;
            matchPredictor = GetComponent<MatchPredictor>();
            InitializeComponents();
        }

        public void Start()
        {
            InitializeBoard();
        }

        private void Update()
        {
            UpdateGameMetrics();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }
        #endregion

        #region Initialization
        private void InitializeComponents()
        {
            gemFactory = new GemFactory(this, gemPrefabs);
            matchFinder = new MatchFinder(this);
            specialGemActivator = new SpecialGemActivator(this);
            boardResetter = new BoardResetter(this);
        }

        private void InitializeBoard()
        {
            gems = new Gem[width, height];
            CurrentState = GameState.Processing;
            SetupBoard();
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
            CurrentState = GameState.Ready;
            matchPredictor?.ResetPredictionTimer();
        }
        #endregion

        #region Game Logic
        public Gem GetGem(int x, int y)
        {
            if (IsValidPosition(x, y))
                return gems[x, y];
            return null;
        }

        public void TrySwapGems(int x1, int y1, int x2, int y2)
        {
            if (!ValidateMove(x1, y1, x2, y2)) return;
            StartCoroutine(SwapGemsRoutine(x1, y1, x2, y2));
        }


        #endregion

        #region Coroutines
        private IEnumerator SwapGemsRoutine(int x1, int y1, int x2, int y2)
        {
            // 安全性檢查
            if (!ValidateMove(x1, y1, x2, y2))
            {
                Debug.LogWarning($"無效的交換操作：從 ({x1},{y1}) 到 ({x2},{y2})");
                yield break;
            }

            CurrentState = GameState.Swapping;
            isSwitching = true;

            // 直接從棋盤獲取寶石
            gem1 = gems[x1, y1];
            gem2 = gems[x2, y2];

            // 安全檢查：確保寶石存在
            if (gem1 == null || gem2 == null)
            {
                Debug.LogWarning($"嘗試交換的寶石為空：gem1 @ ({x1},{y1}), gem2 @ ({x2},{y2})");
                isSwitching = false;
                CurrentState = GameState.Ready;
                yield break;
            }

            // 更新棋盤上的寶石陣列
            gems[x1, y1] = gem2;
            gems[x2, y2] = gem1;

            // 計算移動動畫參數
            var pos1 = new Vector3(x2, y2, 0);
            var pos2 = new Vector3(x1, y1, 0);
            float swapDuration = SWAP_DURATION / gemMoveSpeed;

            // 執行寶石移動動畫
            StartCoroutine(gem1.AnimateMove(pos1, swapDuration));
            StartCoroutine(gem2.AnimateMove(pos2, swapDuration));

            // 等待動畫完成
            yield return new WaitForSeconds(swapDuration);

            // 更新寶石的座標
            gem1.x = x2;
            gem1.y = y2;
            gem2.x = x1;
            gem2.y = y1;

            // 處理交換後的比對邏輯
            ProcessMatchesAfterSwap();

            // 重置動畫和交換狀態
            gem1.isAnimating = false;
            gem2.isAnimating = false;
            isSwitching = false;
            CurrentState = GameState.Ready;
            matchPredictor?.ResetPredictionTimer();
        }

        // 修改 ProcessMatchesAfterSwap 方法，增加安全檢查
        private void ProcessMatchesAfterSwap()
        {
            // 確保 gem1 和 gem2 在調用前不為 null
            if (gem1 == null || gem2 == null)
            {
                Debug.LogWarning("嘗試處理匹配時，寶石為空");
                StartCoroutine(MakeGemsFall());
                return;
            }

            bool hasMatches = CheckForMatches();

            if (gem1.id == 103 || gem2.id == 103)
            {
                specialGemActivator.ActivateSpecialGem(gem1.id == 103 ? gem1 : gem2);
            }
            else if (hasMatches)
            {
                StartCoroutine(ProcessMatchSequence());
            }
            else if (gem1.id >= 100 || gem2.id >= 100)
            {
                specialGemActivator.ActivateSpecialGem(gem1.id >= 100 ? gem1 : gem2);
            }
            else
            {
                SwapBack();
            }
        }

        // 修改 CheckForMatches 方法，增加安全檢查
        public bool CheckForMatches()
        {
            // 確保在特殊寶石情況下不進行匹配檢查
            if (isSwitching && (gem1.id >= 100 || gem2.id >= 100))
                return false;

            var matches = matchFinder.FindAllMatches();
            foreach (var match in matches)
            {
                foreach (var gem in match.matchedGems)
                {
                    // 安全檢查：確保 gem 不為 null
                    if (gem != null)
                    {
                        gem.isMatched = true;
                    }
                }
            }

            return matches.Count > 0;
        }

        
        // 輔助方法：驗證移動的有效性
        private bool ValidateMove(int x1, int y1, int x2, int y2)
        {
            // 檢查座標是否在棋盤範圍內
            if (!IsValidPosition(x1, y1) || !IsValidPosition(x2, y2))
            {
                LogError("無效的移動位置：({0},{1}) 到 ({2},{3})", x1, y1, x2, y2);
                return false;
            }

            // 檢查是否為相鄰位置的交換
            bool isAdjacent =
                (Math.Abs(x1 - x2) == 1 && y1 == y2) ||
                (Math.Abs(y1 - y2) == 1 && x1 == x2);

            if (!isAdjacent)
            {
                LogError("只允許交換相鄰的寶石：({0},{1}) 到 ({2},{3})", x1, y1, x2, y2);
                return false;
            }

            return true;
        }

        

        private IEnumerator ProcessMatchSequence()
        {
            yield return new WaitForSeconds(DESTROY_DELAY);
            DestroyMatches(true, gem1.x, gem1.y);
            yield return new WaitForSeconds(DESTROY_DELAY);

            if (gem1.id >= 100 || gem2.id >= 100)
            {
                specialGemActivator.ActivateSpecialGem(gem1.id >= 100 ? gem1 : gem2);
            }
        }

        private void SwapBack()
        {
            // 確保交換的寶石都存在且有效
            if (gem1 == null || gem2 == null ||
                gem1.gameObject == null || gem2.gameObject == null)
            {
                return;
            }

            var tempGem = gems[gem1.x, gem1.y];
            gems[gem1.x, gem1.y] = gems[gem2.x, gem2.y];
            gems[gem2.x, gem2.y] = tempGem;

            // 檢查協程是否可以啟動
            if (gem1 != null && gem1.gameObject != null &&
                gem2 != null && gem2.gameObject != null)
            {
                StartCoroutine(gem1.AnimateMove(new Vector3(gem2.x, gem2.y, 0), SWAP_DURATION / gemMoveSpeed));
                StartCoroutine(gem2.AnimateMove(new Vector3(gem1.x, gem1.y, 0), SWAP_DURATION / gemMoveSpeed));
            }
        }

        public IEnumerator FillEmptySpaces()
        {
            CurrentState = GameState.Filling;
            matchPredictor?.StopTimer();

            for (int x = 0; x < width; x++)
            {
                int dropDelay = 0;
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] == null && !HasFloatingGemsAbove(x, y))
                    {
                        CreateAndDropGem(x, y, ref dropDelay);
                        yield return new WaitForSeconds(0.1f);
                    }
                }
            }

            yield return ProcessChainReactions();
        }

        private IEnumerator ProcessChainReactions()
        {
            yield return new WaitForSeconds(0.1f);

            while (CheckForMatches())
            {
                yield return new WaitForSeconds(DESTROY_DELAY);
                DestroyMatches(false);
                yield return new WaitForSeconds(DESTROY_DELAY);
            }

            if (CurrentState != GameState.Processing)
            {
                CurrentState = GameState.Ready;
                matchPredictor?.ResetPredictionTimer();
            }
        }
        #endregion

        #region Helper Methods
        private void UpdateStatusText()
        {
            if (statusText != null)
            {
                statusText.text = currentState switch
                {
                    GameState.Ready => "可遊玩",
                    GameState.Swapping => "交換中",
                    GameState.Processing => "消除中",
                    GameState.Filling => "填滿中",
                    GameState.Resetting => "重置中",
                    _ => "進行中"
                };
            }
        }

        private void UpdateGameMetrics()
        {
            if (gemsText != null)
            {
                gemsText.text = "Gems: " + transform.childCount;
            }

            UpdatePerformanceMetrics();

            if (Time.frameCount % 300 == 0)
            {
                CleanupBoard();
            }
        }

        private void UpdatePerformanceMetrics()
        {
            frameCounter++;
            float timeElapsed = Time.time - lastUpdateTime;

            if (timeElapsed >= FPS_UPDATE_INTERVAL)
            {
                fps = frameCounter / timeElapsed;
                frameCounter = 0;
                lastUpdateTime = Time.time;

                if (fps < 30)
                {
                    AdjustPerformance();
                }
            }
        }

        private void AdjustPerformance()
        {
            gemMoveSpeed = Mathf.Min(gemMoveSpeed * 1.2f, 10f);
        }

        private bool HasFloatingGemsAbove(int x, int y)
        {
            for (int above = y + 1; above < height; above++)
            {
                if (gems[x, above] != null)
                    return true;
            }
            return false;
        }

        private void CreateAndDropGem(int x, int y, ref int dropDelay)
        {
            gemFactory.CreateGem(x, y);
            gems[x, y].transform.position = new Vector3(x, height + dropDelay, 0);
            StartCoroutine(gems[x, y].AnimateMove(
                new Vector3(x, y, 0),
                0.3f / gemMoveSpeed
            ));
            dropDelay++;
        }

        private void CleanupBoard()
        {
            foreach (Transform child in transform)
            {
                Gem gemComponent = child.GetComponent<Gem>();
                if (gemComponent != null && !IsGemValid(gemComponent))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        private bool IsGemValid(Gem gem)
        {
            return IsValidPosition(gem.x, gem.y) && gems[gem.x, gem.y] == gem;
        }

        private void CleanupResources()
        {
            StopAllCoroutines();
            // 清理所有現有寶石
            foreach (Transform child in transform)
            {
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }


        private void LogError(string message, params object[] args)
        {
            Debug.LogError($"[Board] {string.Format(message, args)}");
        }

        private bool IsValidPosition(int x, int y)
        {
            return x >= 0 && x < width && y >= 0 && y < height;
        }
        #endregion

        #region Match Processing
        private void DestroyMatches(bool isFromInteraction = false, int interactX = -1, int interactY = -1)
        {
            var matches = matchFinder.FindAllMatches();
            List<Gem> allMatchedGems = matches.SelectMany(m => m.matchedGems).ToList();
            StartCoroutine(DestroyMatchesSequence(allMatchedGems, isFromInteraction, interactX, interactY));
        }

        private IEnumerator DestroyMatchesSequence(List<Gem> matchedGems, bool isFromInteraction, int interactX, int interactY)
        {
            HashSet<Gem> processedGems = new HashSet<Gem>();
            Dictionary<int, List<Gem>> matchGroups = new Dictionary<int, List<Gem>>();

            foreach (var gem in matchedGems)
            {
                if (gem != null && !processedGems.Contains(gem) && gems[gem.x, gem.y] == gem)
                {
                    processedGems.Add(gem);
                    gems[gem.x, gem.y] = null;

                    if (!matchGroups.ContainsKey(gem.id))
                    {
                        matchGroups[gem.id] = new List<Gem>();
                    }
                    matchGroups[gem.id].Add(gem);
                }
            }

            // 淡出效果
            yield return StartCoroutine(FadeOutGems(processedGems));

            yield return new WaitForSeconds(DESTROY_DELAY);

            // 生成資源寶石
            foreach (var group in matchGroups)
            {
                var gems = group.Value;
                if (gems.Count >= 4)
                {
                    ProcessResourceGemCreation(gems, isFromInteraction, interactX, interactY);
                }
            }

            yield return StartCoroutine(MakeGemsFall());
        }

        private IEnumerator FadeOutGems(HashSet<Gem> gems)
        {
            var fadeData = gems.Select(gem => (
                gem,
                renderer: gem.GetComponent<SpriteRenderer>(),
                startColor: gem.GetComponent<SpriteRenderer>().color
            )).ToList();

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

            foreach (var (gem, _, _) in fadeData)
            {
                if (gem != null && gem.gameObject != null)
                {
                    Destroy(gem.gameObject);  // 改用 Destroy 而不是 ReturnGemToPool
                }
            }
        }

        private void ProcessResourceGemCreation(List<Gem> gems, bool isFromInteraction, int interactX, int interactY)
        {
            int createX, createY;
            bool isHorizontal = gems.All(g => g.y == gems[0].y);
            bool isVertical = gems.All(g => g.x == gems[0].x);

            if (isFromInteraction)
            {
                createX = interactX;
                createY = interactY;
            }
            else
            {
                (createX, createY) = CalculateResourceGemPosition(gems, isHorizontal, isVertical);
            }

            if (this.gems[createX, createY] == null)
            {
                CreateResourceGem(createX, createY, DetermineResourceGemType(gems.Count, isHorizontal, isVertical));
            }
        }

        private (int x, int y) CalculateResourceGemPosition(List<Gem> gems, bool isHorizontal, bool isVertical)
        {
            if (isHorizontal)
            {
                var orderedGems = gems.OrderBy(g => g.x).ToList();
                return (orderedGems[orderedGems.Count / 2].x, gems[0].y);
            }
            else if (isVertical)
            {
                var orderedGems = gems.OrderBy(g => g.y).ToList();
                return (gems[0].x, orderedGems[orderedGems.Count / 2].y);
            }
            else
            {
                return (gems[0].x, gems[0].y);
            }
        }

        private int DetermineResourceGemType(int matchCount, bool isHorizontal, bool isVertical)
        {
            if (matchCount >= 5)
            {
                return 3; // Bomb
            }
            else if (isHorizontal && isVertical)
            {
                return 2; // Cross
            }
            else if (isHorizontal)
            {
                return 0; // LineH
            }
            else if (isVertical)
            {
                return 1; // LineV
            }
            else
            {
                return 0; // Default to LineH for 4-match
            }
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
                            var gem = gems[x, y + 1];
                            if (gem != null)
                            {
                                gems[x, y] = gem;
                                gems[x, y + 1] = null;
                                gem.y = y;
                                animatingGems++;
                                StartCoroutine(WaitForGemFall(gem, x, y, () => animatingGems--));
                                hasFalling = true;
                            }
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

            yield return StartCoroutine(FillEmptySpaces());
        }

        private IEnumerator WaitForGemFall(Gem gem, int x, int y, System.Action onComplete)
        {
            yield return StartCoroutine(gem.AnimateMove(
                new Vector3(x, y, 0),
                0.3f / gemMoveSpeed
            ));
            onComplete?.Invoke();
        }

        private void CreateResourceGem(int x, int y, int resType)
        {
            GameObject gemObj = Instantiate(resGemPrefabs[resType], transform);
            Gem gem = gemObj.GetComponent<Gem>();
            gem.Init(100 + resType, x, y);
            gems[x, y] = gem;
        }
        #endregion

        public class MatchInfo
        {
            public List<Gem> matchedGems = new List<Gem>();
            public bool isHorizontal;
            public bool isVertical;
            public int matchCount => matchedGems.Count;
        }
        public IEnumerator FadeAndDestroyGems(List<Gem> gemsToDestroy)
        {
            if (gemsToDestroy == null || gemsToDestroy.Count == 0) yield break;

            // 創建一個安全的臨時列表，確保只處理有效的寶石
            var safeGems = new List<Gem>();
            foreach (var gem in gemsToDestroy)
            {
                // 嚴格檢查寶石和遊戲物件的有效性
                if (gem != null && gem.gameObject != null)
                {
                    try
                    {
                        // 額外檢查是否能獲取 SpriteRenderer
                        var renderer = gem.GetComponent<SpriteRenderer>();
                        if (renderer != null)
                        {
                            safeGems.Add(gem);
                        }
                        else
                        {
                            Debug.LogWarning($"寶石 ({gem.x}, {gem.y}) 的 SpriteRenderer 已丟失");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"處理寶石時發生異常: {e.Message}");
                    }
                }
            }

            // 如果沒有有效的寶石，直接返回
            if (safeGems.Count == 0) yield break;

            // 從棋盤中移除寶石
            foreach (var gem in safeGems)
            {
                if (gem != null && IsValidGemPosition(gem))
                {
                    gems[gem.x, gem.y] = null;
                }
            }

            // 淡出效果
            var fadeData = safeGems
                .Select(gem => (
                    Gem: gem,
                    Renderer: gem.GetComponent<SpriteRenderer>(),
                    StartColor: gem.GetComponent<SpriteRenderer>().color
                ))
                .ToList();

            float alpha = 1f;
            while (alpha > 0)
            {
                alpha -= Time.deltaTime * gemMoveSpeed * 3f;

                // 安全地更新顏色
                foreach (var (gem, renderer, startColor) in fadeData)
                {
                    if (gem != null && gem.gameObject != null && renderer != null)
                    {
                        renderer.color = new Color(
                            startColor.r,
                            startColor.g,
                            startColor.b,
                            Mathf.Clamp01(alpha)
                        );
                    }
                }
                yield return null;
            }

            // 銷毀寶石
            foreach (var (gem, _, _) in fadeData)
            {
                if (gem != null && gem.gameObject != null)
                {
                    Destroy(gem.gameObject);
                }
            }

            yield return StartCoroutine(MakeGemsFall());
        }

        // 輔助方法：驗證寶石位置是否有效
        private bool IsValidGemPosition(Gem gem)
        {
            return gem != null &&
                   gem.x >= 0 && gem.x < width &&
                   gem.y >= 0 && gem.y < height;
        }

        private void SafeInvoke(Action action, string errorMessage = "執行操作時發生錯誤")
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"{errorMessage}: {e.Message}");
                // 可以添加更多的錯誤處理邏輯
            }
        }

    }
}