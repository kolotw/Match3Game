using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Match3Game
{
    // 定義遊戲狀態的枚舉
    // 描述遊戲板在不同階段可能的狀態
    public enum GameState
    {
        Ready,       // 遊戲就緒狀態，玩家可以進行交換操作
        Swapping,    // 寶石交換中，正在執行交換動畫
        Processing,  // 正在處理寶石消除邏輯
        Filling,     // 正在填充遊戲板的空白位置
        Resetting,    // 遊戲板重置中
        Completed    // 遊戲完成狀態，所有關卡已通過或失敗
    }
   
    // Board 類別是遊戲的核心管理類別
    // 負責管理遊戲板的所有邏輯，包括寶石交換、匹配、消除等
    public class Board : MonoBehaviour
    {
        #region 常量定義
        // 遊戲常數定義
        public static class GameTiming
        {
            // 基本操作 - 最快速度
            public const float SWAP_DURATION = 0.08f;     // 交換需要快速、流暢
            public const float FALL_DELAY = 0.03f;        // 下落也要快速，保持遊戲節奏

            // 消除相關 - 中等速度
            public const float DESTROY_DELAY = 0.12f;     // 消除前的短暫等待
            public const float FADE_DELAY = 0.15f;        // 淡出效果稍微慢一點以便看清

            // 完成動作 - 稍慢
            public const float COMPLETE_DELAY = 0.3f;     // 動作完成的等待時間
            public const float COLLECT_DELAY = 0.35f;     // 收集寶石稍微放慢以強調效果
            public const float WAIT_FOR_DISPEAR = 0.4f;   // 等待消失的時間

            // 特殊效果 - 最慢
            public const float SPECIAL_EFFECT_DELAY = 0.5f; // 特殊效果放慢以突出表現
        }

        private static class BoardDefaults
        {
            public const int DEFAULT_WIDTH = 8;           // 預設遊戲板寬度
            public const int DEFAULT_HEIGHT = 8;          // 預設遊戲板高度
            public const float DEFAULT_MOVE_SPEED = 5f;   // 預設移動速度
        }

        private static class GemProperties
        {
            public const int SPECIAL_GEM_START_ID = 100;  // 特殊寶石起始ID
            public const int MIN_MATCH_COUNT = 3;         // 最小匹配數量
            public const int SPECIAL_MATCH_COUNT = 4;     // 特殊寶石匹配數量
            public const int SUPER_MATCH_COUNT = 5;       // 超級匹配數量
        }
        #endregion

        #region 變數

        // 其他的class
        private GemFactory gemFactory;        // 寶石工廠
        private MatchFinder matchFinder;      // 匹配查找器
        public SpecialGemActivator specialGemActivator;  // 特殊寶石激活器
        public GameManager GM;

        // 遊戲板的基本配置參數
        public int width = BoardDefaults.DEFAULT_WIDTH;
        public int height = BoardDefaults.DEFAULT_HEIGHT;
        public float gemMoveSpeed = BoardDefaults.DEFAULT_MOVE_SPEED;

        // 單例模式：確保遊戲中只有一個遊戲板實例
        public static Board instance; // 靜態實例變數
        private readonly object swapLock = new object();

        // 二維數組，用於追蹤遊戲板上每個位置的寶石
        [HideInInspector] public Gem[,] gems;

        int triggerX, triggerY; // 觸發點的座標
        int[] playerTriggerX, playerTriggerY; // 玩家操作的觸發點座標

        // 遊戲邏輯相關的組件引用
        [SerializeField] public MatchPredictor matchPredictor;  // 匹配預測器

        // 寶石預製體數組
        [SerializeField] public GameObject[] gemPrefabs;       // 普通寶石的預製體
        [SerializeField] private GameObject[] resGemPrefabs;    // 特殊資源寶石的預製體

        // 遊戲狀態管理的關鍵變數
        public GameState currentState = GameState.Ready;  // 當前遊戲狀態
        private bool isSwitching = false;  // 是否正在交換寶石
        public Gem gem1, gem2;  // 當前正在交換的兩個寶石

        //public bool byPlayer = false; // 是否由玩家操作 
        private bool 由玩家觸發生成 = false;

        public bool hasMoveCompleted
        {
            get => CurrentState == GameState.Ready;  // 是否處於就緒狀態
            set => CurrentState = value ? GameState.Ready : GameState.Processing;  // 根據傳入值設置遊戲狀態
        }

        #endregion

        #region 遊戲狀態屬性
        // 提供對當前遊戲狀態的安全訪問和管理
        public GameState CurrentState
        {
            get => currentState;
            private set
            {
                if (currentState == GameState.Completed)
                {
                    return;
                }

                currentState = value;
                //Debug.Log($"ChangeState: {currentState}");

                // 使用 GameManager 更新狀態
                GM?.UpdateGameState(currentState);
            }
        }
        public void changeGameState(GameState newState)
        {
            CurrentState = newState;
        }
        // 移動完成狀態的屬性
        // 提供一種簡潔的方式檢查和設置遊戲是否就緒
        #endregion
        
        #region 驗證
        public class SwapValidationResult
        {
            public bool IsValid { get; private set; }
            public string ErrorMessage { get; private set; }

            private SwapValidationResult(bool isValid, string errorMessage = "")
            {
                IsValid = isValid;
                ErrorMessage = errorMessage;
            }

            public static SwapValidationResult Success() => new SwapValidationResult(true);
            public static SwapValidationResult Failure(string message) => new SwapValidationResult(false, message);
        }

        private SwapValidationResult ValidateSwap(int x1, int y1, int x2, int y2, Gem gem1 = null, Gem gem2 = null)
        {
            // 1. 檢查座標是否在遊戲板範圍內
            if (!IsValidPosition(x1, y1) || !IsValidPosition(x2, y2))
            {
                return SwapValidationResult.Failure($"無效的移動位置：從 ({x1},{y1}) 到 ({x2},{y2})");
            }

            // 2. 檢查是否為相鄰位置
            bool isAdjacent = (Math.Abs(x1 - x2) == 1 && y1 == y2) ||  // 水平相鄰
                              (Math.Abs(y1 - y2) == 1 && x1 == x2);     // 垂直相鄰

            if (!isAdjacent)
            {
                return SwapValidationResult.Failure("只允許交換相鄰的寶石");
            }

            // 3. 獲取寶石引用（如果未提供）
            gem1 ??= gems[x1, y1];
            gem2 ??= gems[x2, y2];

            // 4. 檢查寶石是否存在
            if (gem1 == null || gem2 == null)
            {
                return SwapValidationResult.Failure("嘗試交換的寶石為空");
            }

            // 5. 特殊寶石檢查
            bool hasSpecialGem = (gem1.id >= 100 || gem2.id >= 100);
            if (hasSpecialGem)
            {
                return SwapValidationResult.Success();
            }

            // 6. 遊戲狀態檢查
            if (CurrentState != GameState.Swapping && CurrentState != GameState.Ready)
            {
                return SwapValidationResult.Failure($"遊戲狀態不正確: {CurrentState}");
            }

            // 7. 動畫狀態檢查
            if (gem1.isAnimating || gem2.isAnimating)
            {
                return SwapValidationResult.Failure("寶石正在移動中");
            }

            return SwapValidationResult.Success();
        }
        // 驗證座標是否在遊戲板有效範圍內的方法
        // 防止訪問陣列越界，保證遊戲邏輯的安全性
        public bool IsValidPosition(int x, int y)
        {
            // 檢查座標條件：
            // 1. x 座標在 0 到 width-1 之間
            // 2. y 座標在 0 到 height-1 之間
            // 確保座標落在遊戲板的合法範圍內
            return x >= 0 && x < width && y >= 0 && y < height;
        }
        // 確保寶石在遊戲板上的狀態是合法和一致的
        private bool IsGemValid(Gem gem)
        {
            return IsValidPosition(gem.x, gem.y) &&
                   gems[gem.x, gem.y] == gem;
        }
        #endregion

        #region 生命週期方法
        // Awake 方法：在遊戲物件被實例化時立即調用
        // 用於進行初始化設置，在 Start 方法之前執行
        private void Awake()
        {
            // 設置單例實例，確保只有一個遊戲板
            instance = this;
            // 獲取匹配預測器組件
            matchPredictor = GetComponent<MatchPredictor>();

            // 初始化遊戲的各個組件
            InitializeComponents();
            // 強制直向
            Screen.orientation = ScreenOrientation.Portrait;
        }

        // Start 方法：在第一幀更新之前調用
        // 用於執行遊戲開始時需要進行的初始化邏輯
        public void Start()
        {
            // 初始化遊戲板
            InitializeBoard();
        }
        // 初始化遊戲的各個組件
        // 創建支持遊戲運行的各種輔助類別
        private void InitializeComponents()
        {
            

            // 創建匹配查找器：檢測遊戲板上的寶石匹配
            // 負責識別可以消除的寶石組合
            matchFinder = new MatchFinder(this);

            // 創建特殊寶石激活器：處理特殊寶石的觸發效果
            // 管理具有特殊能力的寶石的行為
            specialGemActivator = new SpecialGemActivator(this);
            GM = GameObject.Find("00GameMaster").GetComponent<GameManager>();
        }

        // 初始化遊戲板的主要方法
        // 準備遊戲開始時的遊戲板狀態
        private void InitializeBoard()
        {
            // 創建二維寶石數組
            // 大小由遊戲板的寬度和高度決定
            gems = new Gem[width, height];

            // 設置當前遊戲狀態為處理中
            // 防止玩家在初始化過程中進行操作
            CurrentState = GameState.Processing;

            // 開始設置遊戲板
            // 填充遊戲板上的初始寶石
            SetupBoard();
        }

        // 設置遊戲板的具體方法
        // 在遊戲開始時為每個格子生成寶石
        public void SetupBoard()
        {
            Debug.Log(LevelMapManager.instance.Level);
            int prefabCount;

            switch (LevelMapManager.instance.Level)
            {
                case 1: prefabCount = 3; break;
                case 2: prefabCount = 4; break;
                case 3: prefabCount = 5; break;
                case 4: prefabCount = 6; break;
                case 5: prefabCount = 7; break;
                case 6: prefabCount = 8; break;
                case 7: prefabCount = 9; break;
                default: prefabCount = 3; break;
            }

            GameObject[] nowPrefab = new GameObject[prefabCount];
            for (int i = 0; i < prefabCount; i++)
            {
                nowPrefab[i] = gemPrefabs[i];
            }

            gemPrefabs = nowPrefab;

            // 創建寶石工廠：負責生成和管理遊戲板上的寶石
            // 使用預製體數組作為寶石模板
            gemFactory = new GemFactory(this, gemPrefabs);

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
        // Update 方法：每幀調用一次
        // 用於執行持續性的遊戲邏輯和性能監控
        private void Update()
        {
            // 更新遊戲指標
            // 包括寶石數量、性能metrics等
            UpdateGameMetrics();
        }
        // 更新遊戲指標的方法
        // 監控遊戲性能和遊戲板狀態，提供即時的遊戲運行資訊
        private void UpdateGameMetrics()
        {
            // 更新遊戲板上的寶石數量顯示
            GM?.UpdateGemCount(transform.childCount);

            // 定期清理遊戲板
            // 每300幀執行一次清理操作，優化遊戲性能和資源管理
            if (Time.frameCount % 10 == 0)
            {
                CleanupBoard();
            }
        }

        // 清理遊戲板的方法
        // 定期移除無效或殘留的遊戲物件，優化遊戲性能和資源管理
        private void CleanupBoard()
        {
            foreach (Transform child in transform)
            {
                Gem gemComponent = child.GetComponent<Gem>();

                if (gemComponent != null && !IsGemValid(gemComponent))
                {
                    if (gemComponent.id >= 100)
                    {
                        //Debug.LogError($"★★★發現無效特殊寶石★★★ id: {gemComponent.id} , ({gemComponent.x},{gemComponent.y})");
                        // 嘗試修復特殊寶石
                        if (IsValidPosition(gemComponent.x, gemComponent.y))
                        {
                            // 如果位置是有效的，直接登錄到 gems 陣列
                            gems[gemComponent.x, gemComponent.y] = gemComponent;
                            //Debug.Log($"已修復特殊寶石在位置 ({gemComponent.x},{gemComponent.y})");
                        }
                        else
                        {
                            // 如果位置無效，嘗試找一個最近的有效位置
                            bool found = false;
                            for (int x = 0; x < width && !found; x++)
                            {
                                for (int y = 0; y < height && !found; y++)
                                {
                                    if (gems[x, y] == null)
                                    {
                                        // 找到空位置，重新設置寶石位置
                                        gemComponent.x = x;
                                        gemComponent.y = y;
                                        gems[x, y] = gemComponent;
                                        gemComponent.transform.position = new Vector3(x, y, 0);
                                        found = true;
                                        Debug.Log($"已將特殊寶石重新放置到位置 ({x},{y})");
                                    }
                                }
                            }

                            if (!found)
                            {
                                Debug.LogError("無法找到有效位置，不得不銷毀特殊寶石");
                                Destroy(child.gameObject);
                            }
                        }
                    }
                    else
                    {
                        //Debug.Log($"★★★發現無效普通寶石★★★ id: {gemComponent.id} , ({gemComponent.x},{gemComponent.y})");
                        Destroy(child.gameObject);
                    }
                }
            }
        }

        // OnDestroy 方法：當遊戲物件被銷毀時調用
        // 用於進行必要的資源清理工作
        private void OnDestroy()
        {
            try
            {
                // 停止所有正在運行的協程
                StopAllCoroutines();

                // 遍歷並銷毀遊戲板上的所有子物件
                foreach (Transform child in transform)
                {
                    if (child != null)
                    {
                        Destroy(child.gameObject);
                    }
                }

                // 清空遊戲板陣列
                if (gems != null)
                {
                    for (int x = 0; x < width; x++)
                    {
                        for (int y = 0; y < height; y++)
                        {
                            gems[x, y] = null;
                        }
                    }
                    gems = null;
                }

                // 重置遊戲狀態
                CurrentState = GameState.Ready;
                hasMoveCompleted = true;

                // 重置特殊寶石相關
                if (specialGemActivator != null)
                {
                    specialGemActivator.重置狀態();
                }

                // 重置配對
                gem1 = null;
                gem2 = null;

                // 重置預測器狀態
                if (matchPredictor != null)
                {
                    matchPredictor.StopTimer();
                }

                // 重置由玩家觸發的標記
                由玩家觸發生成 = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"OnDestroy清理時發生錯誤: {e.Message}\n{e.StackTrace}");
            }
        }
        #endregion

        #region 遊戲邏輯方法 交換配對與刪除
        //定義配對變數
        public class MatchInfo
        {
            // 存儲在此匹配中的所有寶石
            // 這個列表包含了所有被視為匹配的寶石
            public List<Gem> matchedGems = new List<Gem>();

            // 標記匹配是否為水平方向
            // 當所有匹配的寶石在同一行時為 true
            public bool isHorizontal;

            // 標記匹配是否為垂直方向
            // 當所有匹配的寶石在同一列時為 true
            public bool isVertical;

            // 快速獲取匹配的寶石數量的屬性
            // 提供了一種便捷的方式來確定匹配的規模
            public int matchCount => matchedGems.Count;
        }
        // 取得寶石座標
        public Gem GetGem(int x, int y)
        {
            // 先檢查座標是否在遊戲板的有效範圍內
            if (IsValidPosition(x, y))
                return gems[x, y];

            // 如果座標無效，返回 null
            return null;
        }
        public void 嘗試交換寶石一(int x1, int y1, int x2, int y2)
        {
            由玩家觸發生成 = true;

            var validationResult = ValidateSwap(x1, y1, x2, y2);
            if (!validationResult.IsValid)
            {
                Debug.LogWarning(validationResult.ErrorMessage);
                return;
            }

            StartCoroutine(啟動交換寶石二(x1, y1, x2, y2));
        }
        private IEnumerator 啟動交換寶石二(int x1, int y1, int x2, int y2)
        {
            var validationResult = ValidateSwap(x1, y1, x2, y2);
            if (!validationResult.IsValid)
            {
                Debug.LogWarning(validationResult.ErrorMessage);
                yield break;
            }

            CurrentState = GameState.Swapping;
            isSwitching = true;

            // 獲取並儲存寶石引用
            gem1 = gems[x1, y1];
            gem2 = gems[x2, y2];

            if (gem1 == null || gem2 == null)
            {
                Debug.LogWarning($"嘗試交換的寶石為空：gem1 @ ({x1},{y1}), gem2 @ ({x2},{y2})");
                isSwitching = false;
                CurrentState = GameState.Ready;
                yield break;
            }

            // 暫時交換位置
            執行交換寶石三(x1, y1, x2, y2);

            // 計算動畫目標位置
            var pos1 = new Vector3(x2, y2, 0);
            var pos2 = new Vector3(x1, y1, 0);

            // 執行移動動畫
            StartCoroutine(gem1.AnimateMove(pos1, GameTiming.SWAP_DURATION));
            StartCoroutine(gem2.AnimateMove(pos2, GameTiming.SWAP_DURATION));

            yield return new WaitForSeconds(GameTiming.SWAP_DURATION);


            // 檢查是否有匹配
            bool hasMatch = false;
            var matches = matchFinder.FindAllMatches();

            // 如果存在特殊寶石，也視為有效交換
            bool hasSpecialGem = (gem1.id >= 100 || gem2.id >= 100);
            hasMatch = matches.Count > 0 || hasSpecialGem;

            if (!hasMatch)
            {
                // 如果沒有匹配，還原位置
                //Debug.Log("無效交換，還原位置");
                執行交換寶石三(x2, y2, x1, y1);  // 交換回原始位置

                // 執行返回動畫
                StartCoroutine(gem1.AnimateMove(new Vector3(x1, y1, 0), GameTiming.SWAP_DURATION));
                StartCoroutine(gem2.AnimateMove(new Vector3(x2, y2, 0), GameTiming.SWAP_DURATION));

                yield return new WaitForSeconds(GameTiming.SWAP_DURATION);
            }
            else
            {
                // 有匹配時，處理匹配邏輯
                交換寶石後再檢查四();
            }

            gem1.isAnimating = false;
            gem2.isAnimating = false;
            isSwitching = false;
            //CurrentState = GameState.Ready;
            matchPredictor?.ResetPredictionTimer();
        }
        private void 執行交換寶石三(int x1, int y1, int x2, int y2)
        {
            lock (swapLock)
            {
                var tempGem = gems[x1, y1];
                gems[x1, y1] = gems[x2, y2];
                gems[x2, y2] = tempGem;

                // 更新寶石的座標屬性
                if (gems[x1, y1] != null)
                {
                    gems[x1, y1].x = x1;
                    gems[x1, y1].y = y1;
                }
                if (gems[x2, y2] != null)
                {
                    gems[x2, y2].x = x2;
                    gems[x2, y2].y = y2;
                }
            }
            CurrentState = GameState.Ready;
        }
        private void 交換寶石後再檢查四()
        {
            try
            {
                var swappedGem1 = gem1;
                var swappedGem2 = gem2;
                var processedGemIds = new HashSet<int>();

                var validationResult = ValidateSwap(gem1.x, gem1.y, gem2.x, gem2.y);
                if (!validationResult.IsValid)
                {
                    Debug.Log(validationResult.ErrorMessage);
                    StartCoroutine(落下寶石五());
                    return;
                }

                CurrentState = GameState.Processing;

                bool hasValidMatch = false;
                bool hasSpecialGem = swappedGem1?.id >= 100 || swappedGem2?.id >= 100;

                // 玩家操作時的處理順序
                if (由玩家觸發生成)
                {
                    Gem specialGem = swappedGem1?.id >= 100 ? swappedGem1 : swappedGem2;
                    Gem normalGem = swappedGem1?.id >= 100 ? swappedGem2 : swappedGem1;

                    // 如果是兩個特殊寶石的組合
                    if (swappedGem1?.id >= 100 && swappedGem2?.id >= 100)
                    {
                        int originalId1 = swappedGem1.id;
                        var (success, resultType) = MatchUtils.CheckSpecialGemCombination(swappedGem1, swappedGem2);
                        if (success)
                        {
                            swappedGem1.id = resultType;
                            specialGemActivator.啟動特殊寶石(swappedGem1);
                            swappedGem1.id = originalId1;
                            hasValidMatch = true;
                            return; // 提前返回，讓特殊寶石處理流程接管
                        }
                    }
                    // 特殊寶石與普通寶石的交換
                    else if (hasSpecialGem && normalGem?.id < 100)
                    {
                        var matches = matchFinder.FindAllMatches();
                        var matchesForNormalGem = matches.Where(match =>
                            match.matchedGems.Any(gem => gem.x == normalGem.x && gem.y == normalGem.y)).ToList();

                        if (matchesForNormalGem.Count > 0)
                        {
                            var matchGroup = matchesForNormalGem
                                .SelectMany(m => m.matchedGems)
                                .Where(gem => gem != null)
                                .GroupBy(gem => gem.id)
                                .SelectMany(group => MatchUtils.FindContinuousGemGroups(group))
                                .Where(group => group.Count >= 4)
                                .FirstOrDefault();

                            if (matchGroup != null)
                            {
                                var (resourceType, isHorizontal, isVertical, _) =
                                    MatchUtils.確認特殊寶石類別(matchGroup);
                                if (resourceType != -1)
                                {
                                    foreach (var groupGem in matchGroup)
                                    {
                                        if (groupGem != null)
                                        {
                                            groupGem.isMatched = true;
                                            if (IsValidPosition(groupGem.x, groupGem.y))
                                            {
                                                gems[groupGem.x, groupGem.y] = null;
                                            }
                                            processedGemIds.Remove(groupGem.GetInstanceID());
                                        }
                                    }

                                    生成特殊寶石(normalGem.x, normalGem.y, resourceType);
                                    hasValidMatch = true;

                                    // 如果有特殊寶石需要啟動，讓它接管後續流程
                                    if (specialGem != null)
                                    {
                                        specialGemActivator.啟動特殊寶石(specialGem);
                                        return;
                                    }
                                }
                            }
                        }

                        // 如果只有特殊寶石需要啟動
                        if (specialGem != null)
                        {
                            specialGemActivator.啟動特殊寶石(specialGem);
                            hasValidMatch = true;
                            return;
                        }

                        // 處理其他可能的普通匹配
                        matches = matchFinder.FindAllMatches();
                        if (matches.Count > 0)
                        {
                            foreach (var match in matches)
                            {
                                foreach (var matchGem in match.matchedGems)
                                {
                                    if (matchGem != null) matchGem.isMatched = true;
                                }
                            }
                            StartCoroutine(處理消除序列一());
                        }
                    }
                    else
                    {
                        // 普通寶石交換的處理
                        var matches = matchFinder.FindAllMatches();
                        if (matches.Count > 0)
                        {
                            foreach (var match in matches)
                            {
                                foreach (var matchGem in match.matchedGems)
                                {
                                    if (matchGem != null) matchGem.isMatched = true;
                                }
                            }
                            StartCoroutine(處理消除序列一());
                            hasValidMatch = true;
                        }
                    }
                }
                else
                {
                    // 非玩家操作的處理
                    var matches = matchFinder.FindAllMatches();
                    if (matches.Count > 0)
                    {
                        foreach (var match in matches)
                        {
                            foreach (var matchGem in match.matchedGems)
                            {
                                if (matchGem != null) matchGem.isMatched = true;
                            }
                        }
                        StartCoroutine(處理消除序列一());
                        hasValidMatch = true;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"處理配對時發生錯誤: {e.Message}\n{e.StackTrace}");
                StartCoroutine(落下寶石五());
            }
        }
        public IEnumerator 落下寶石五()
        {
            yield return new WaitForSeconds(0.03f);
            bool hasFalling;
            do
            {
                hasFalling = false;
                // 從底部開始向上檢查，確保下層寶石先處理
                for (int y = 0; y < height - 1; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (gems[x, y] == null)
                        {
                            // 尋找這一列中最近的上方寶石
                            int above = y + 1;
                            while (above < height && gems[x, above] == null)
                            {
                                above++;
                            }

                            // 如果找到了上方的寶石，讓它下落
                            if (above < height && gems[x, above] != null)
                            {
                                var gem = gems[x, above];
                                gems[x, y] = gem;
                                gems[x, above] = null;
                                gem.y = y;

                                // 執行下落動畫
                                yield return StartCoroutine(gem.AnimateMove(new Vector3(x, y, 0), GameTiming.FALL_DELAY));
                                hasFalling = true;
                            }
                        }
                    }
                }
            } while (hasFalling);

            // 填充空位
            yield return StartCoroutine(填滿空位六(-1));
        }
        public IEnumerator 填滿空位六(int resType)
        {
            CurrentState = GameState.Filling;
            matchPredictor?.StopTimer();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (gems[x, y] == null)
                    {
                        // 檢查下方的寶石是否都已經就位
                        yield return StartCoroutine(
                            檢查下方動畫完成(x, x + 1, 0, y)
                        );

                        生成與掉落寶石七(x, y);
                        yield return new WaitForSeconds(GameTiming.FALL_DELAY);
                    }
                }
            }

            yield return StartCoroutine(等待動畫消失完成());
            yield return 處理消除序列一();
        }
        private void 生成與掉落寶石七(int x, int y)
        {
            // 使用寶石工廠創建新寶石
            gemFactory.CreateGem(x, y);

            // 設置寶石的初始位置（在遊戲板頂部）
            gems[x, y].transform.position = new Vector3(x, height + 1, 0);

            // 執行寶石下落動畫
            StartCoroutine(gems[x, y].AnimateMove(
                new Vector3(x, y, 0),  // 目標位置
                GameTiming.FALL_DELAY * 2         // 動畫時間
            ));
        }
        // 觸發點是玩家移動後產生特殊寶石的座標
        private void 設置觸發點(List<MatchInfo> matches, Gem swappedGem1, Gem swappedGem2)
        {
            if (matches == null || matches.Count == 0) return;

            try
            {
                if (由玩家觸發生成)
                {
                    // 記錄已經設置的觸發點位置
                    var usedPositions = new HashSet<(int x, int y)>();

                    var matchGroups = matches.SelectMany(m => m.matchedGems)
                        .Where(gem => gem != null)
                        .GroupBy(gem => gem.id)
                        .SelectMany(group => 尋找連續寶石組別(group))
                        .Where(group => group.Count >= 4)
                        .ToList();

                    // 確保陣列已經初始化
                    if (playerTriggerX == null || playerTriggerX.Length != matchGroups.Count)
                    {
                        playerTriggerX = new int[matchGroups.Count];
                        playerTriggerY = new int[matchGroups.Count];
                    }

                    // 設置各個匹配組的觸發點
                    for (int i = 0; i < matchGroups.Count; i++)
                    {
                        var group = matchGroups[i];
                        // 尋找在交換寶石位置的寶石，但要確保不在已使用的位置或特殊寶石位置
                        var triggerGem = group.FirstOrDefault(g =>
                            ((swappedGem1 != null && g.x == swappedGem1.x && g.y == swappedGem1.y) ||
                            (swappedGem2 != null && g.x == swappedGem2.x && g.y == swappedGem2.y)) &&
                            !usedPositions.Contains((g.x, g.y)) &&
                            !(gems[g.x, g.y]?.id >= 100));

                        if (triggerGem != null)
                        {
                            playerTriggerX[i] = triggerGem.x;
                            playerTriggerY[i] = triggerGem.y;
                            usedPositions.Add((triggerGem.x, triggerGem.y));
                        }
                        else
                        {
                            // 如果找不到適合的觸發點，找組內其他未使用且非特殊寶石的位置
                            triggerGem = group.FirstOrDefault(g =>
                                !usedPositions.Contains((g.x, g.y)) &&
                                !(gems[g.x, g.y]?.id >= 100));

                            if (triggerGem != null)
                            {
                                playerTriggerX[i] = triggerGem.x;
                                playerTriggerY[i] = triggerGem.y;
                                usedPositions.Add((triggerGem.x, triggerGem.y));
                            }
                        }
                    }
                }
                else
                {
                    // 非玩家操作時，只負責記錄觸發點位置，不生成特殊寶石
                    var matchesByType = matches
                        .Where(m => m != null && m.matchedGems != null && m.matchedGems.Any())
                        .GroupBy(m => m.matchedGems[0].id);

                    foreach (var typeGroup in matchesByType)
                    {
                        foreach (var match in typeGroup)
                        {
                            if (match.matchedGems == null) continue;

                            // 記錄有效的匹配組，但不生成特殊寶石
                            var validGems = match.matchedGems.Where(g => g != null).ToList();
                            if (validGems.Any())
                            {
                                int randomIndex = UnityEngine.Random.Range(0, validGems.Count);
                                triggerX = validGems[randomIndex].x;
                                triggerY = validGems[randomIndex].y;
                                break; // 只需要一個觸發點位置
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"設置觸發點時發生錯誤: {e.Message}\n{e.StackTrace}");
                // 確保即使發生錯誤也有預設值
                if (由玩家觸發生成)
                {
                    playerTriggerX = new int[1] { 0 };
                    playerTriggerY = new int[1] { 0 };
                }
                else
                {
                    triggerX = 0;
                    triggerY = 0;
                }
            }
        }
        public bool 偵測可消除的寶石組合()
        {
            // 特殊寶石交換時直接返回 true 就要啟動特殊寶石的效果
            if (isSwitching && (gem1?.id >= 100 || gem2?.id >= 100))
            {
                return true;
            }

            var matches = matchFinder.FindAllMatches();

            foreach (var match in matches)
            {
                foreach (var gem in match.matchedGems)
                {
                    // 加入特殊寶石的保護，避免其被標記為需要消除
                    if (gem != null && gem.id < 100)  // 只處理普通寶石
                    {
                        gem.isMatched = true;
                    }
                }
            }

            // 只考慮普通寶石的匹配
            return matches.Any(match => match.matchedGems.Any(gem => gem != null && gem.id < 100));
        }
        private List<List<Gem>> 尋找連續寶石組別(IGrouping<int, Gem> group)
        {
            return MatchUtils.FindContinuousGemGroups(group);
        }
        // 啟動消除匹配寶石的整體流程        
        private IEnumerator 處理消除序列一()
        {
            CurrentState = GameState.Processing;

            while (true)
            {
                // 檢查是否有可消除的寶石
                if (!偵測可消除的寶石組合())
                {
                    break;
                }

                // 執行消除流程
                yield return new WaitForSeconds(GameTiming.DESTROY_DELAY);
                準備刪除寶石二();
                yield return new WaitForSeconds(GameTiming.DESTROY_DELAY);

                // 等待特殊寶石效果完成
                while (specialGemActivator.isProcessingEffect)
                {
                    yield return null;
                }

                // 處理寶石下落和填充
                yield return StartCoroutine(落下寶石五());
                yield return StartCoroutine(等待動畫消失完成());
            }

            // 所有效果和連鎖反應都完成後才切換狀態
            if (CurrentState == GameState.Processing)
            {
                CurrentState = GameState.Ready;
                matchPredictor?.ResetPredictionTimer();
            }
        }
        private void 準備刪除寶石二()
        {
            //statusText.text = "消除中";
            // 使用匹配查找器尋找所有可消除的寶石組
            var matches = matchFinder.FindAllMatches();

            // 將所有匹配的寶石展平成一個列表
            // 使用 LINQ 的 SelectMany 方法合併所有匹配組中的寶石
            List<Gem> allMatchedGems = matches.SelectMany(m => m.matchedGems).ToList();

            allMatchedGems = allMatchedGems.Distinct().ToList();  // 去除重複的寶石

            // 啟動銷毀匹配寶石的協程
            // 傳入匹配的寶石列表和觸發消除的相關信息
            StartCoroutine(刪除寶石序列三(allMatchedGems));
        }
        public IEnumerator 刪除寶石序列三(List<Gem> matchedGems)
        {
            var processedGems = new HashSet<Gem>();
            var matches = matchFinder.FindAllMatches();
            var processedGemIds = new HashSet<int>();

            設置觸發點(matches, gem1, gem2);

            var matchGroups = matches.SelectMany(m => m.matchedGems)
                            .Where(gem => gem != null)
                            .GroupBy(gem => gem.id)
                            .SelectMany(group => MatchUtils.FindContinuousGemGroups(group))
                            .Where(group => group.Count >= 4)
                            .ToList();
            matchGroups = matchGroups.Distinct().ToList();

            // 先觸發要被消除的特殊寶石效果
            var specialGems = matchedGems.Where(g =>
                g != null &&
                g.id >= 100 &&
                gems[g.x, g.y] == g &&
                !processedGemIds.Contains(g.GetInstanceID())).ToList();

            foreach (var gem in specialGems)
            {
                specialGemActivator.啟動特殊寶石(gem);
                yield return new WaitForSeconds(GameTiming.DESTROY_DELAY);
            }

            // 清除匹配的普通寶石，並標記要處理的群組
            foreach (var gem in matchedGems.Where(g =>
                g != null &&
                g.id < 100 &&
                gems[g.x, g.y] == g &&
                !processedGemIds.Contains(g.GetInstanceID())))
            {
                processedGems.Add(gem);
                gems[gem.x, gem.y] = null;
                processedGemIds.Remove(gem.GetInstanceID());
            }

            // 玩家觸發的特殊寶石檢查和生成
            if (由玩家觸發生成)
            {
                for (int i = 0; i < matchGroups.Count && i < playerTriggerX.Length; i++)
                {
                    var group = matchGroups[i].ToList();
                    if (group.Count < 4) continue;

                    Debug.Log($"觸發點：({playerTriggerX[i]} , {playerTriggerY[i]})");

                    var (resourceType, isHorizontal, isVertical, _) =
                        MatchUtils.確認特殊寶石類別(group, playerTriggerX[i], playerTriggerY[i]);
                    Debug.Log($"特殊寶石類型：{resourceType}");

                    if (resourceType != -1)
                    {
                        triggerX = playerTriggerX[i];
                        triggerY = playerTriggerY[i];
                        if (IsValidPosition(triggerX, triggerY))
                        {
                            var newGem = 生成特殊寶石(triggerX, triggerY, resourceType);
                            if (newGem != null)
                            {
                                processedGemIds.Add(newGem.GetInstanceID());
                            }

                            foreach (var gem in group)
                            {
                                processedGemIds.Add(gem.GetInstanceID());
                            }
                        }
                    }
                }
                由玩家觸發生成 = false;
            }
            else
            {
                // 非玩家操作的特殊寶石生成
                foreach (var group in matchGroups)
                {
                    if (group.Any(g => processedGemIds.Contains(g.GetInstanceID())))
                    {
                        continue;
                    }

                    var (resourceType, isHorizontal, isVertical, _) = MatchUtils.確認特殊寶石類別(group.ToList());

                    if (resourceType != -1)
                    {
                        var validGems = group.Where(g => !processedGemIds.Contains(g.GetInstanceID())).ToList();
                        if (validGems.Any())
                        {
                            int randomIndex = UnityEngine.Random.Range(0, validGems.Count);
                            var selectedGem = validGems[randomIndex];
                            triggerX = selectedGem.x;
                            triggerY = selectedGem.y;

                            //Debug.Log($"非玩家操作時 - 隨機位置({triggerX},{triggerY})生成 Type:{resourceType} count:{group.Count}");

                            if (IsValidPosition(triggerX, triggerY))
                            {
                                var newGem = 生成特殊寶石(triggerX, triggerY, resourceType);
                                if (newGem != null)
                                {
                                    processedGemIds.Add(newGem.GetInstanceID());
                                }
                                foreach (var gem in group)
                                {
                                    processedGemIds.Add(gem.GetInstanceID());
                                }
                            }
                        }
                    }
                }
            }

            yield return StartCoroutine(送出要刪除的寶石四(processedGems, processedGemIds));
            yield return new WaitForSeconds(GameTiming.DESTROY_DELAY);
            //yield return StartCoroutine(落下寶石五());
        }
        public IEnumerator 送出要刪除的寶石四(HashSet<Gem> gems, HashSet<int> safeIds)
        {
            if (gems == null || gems.Count == 0) yield break;

            // 收集所有要被刪除的寶石
            HashSet<Gem> gemsToClear = new HashSet<Gem>(gems);
            specialGemActivator.收集要被消除的寶石(gemsToClear);

            // 清理 safeIds
            foreach (var gem in gems)
            {
                if (gem != null)
                {
                    safeIds.Remove(gem.GetInstanceID());
                }
            }
        }
        private Gem 生成特殊寶石(int x, int y, int resType)
        {
            if (resGemPrefabs == null || resType >= resGemPrefabs.Length)
            {
                Debug.LogError($"Invalid resource type {resType}");
                return null;
            }

            try
            {
                GameObject gemObj = Instantiate(resGemPrefabs[resType], transform);
                Gem gem = gemObj.GetComponent<Gem>();
                if (gem != null)
                {
                    gem.Init(100 + resType, x, y);
                    gems[x, y] = gem;
                    return gem;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error creating resource gem: {e.Message}");
            }
            return null;
        }
        #region 動畫檢查
        private IEnumerator 等待動畫消失完成()
        {
            bool allAnimationsComplete;
            do
            {
                allAnimationsComplete = true;
                // 掃描所有寶石
                for (int x = 0; x < width && allAnimationsComplete; x++)
                {
                    for (int y = 0; y < height && allAnimationsComplete; y++)
                    {
                        if (gems[x, y]?.isAnimating == true)
                        {
                            allAnimationsComplete = false;
                        }
                    }
                }
                if (!allAnimationsComplete)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            } while (!allAnimationsComplete);
        }
        private IEnumerator 檢查下方動畫完成(int startX, int endX, int startY, int endY)
        {
            bool allAnimationsComplete;
            do
            {
                allAnimationsComplete = true;
                for (int x = startX; x < endX && allAnimationsComplete; x++)
                {
                    for (int y = startY; y < endY && allAnimationsComplete; y++)
                    {
                        if (gems[x, y]?.isAnimating == true)
                        {
                            allAnimationsComplete = false;
                        }
                    }
                }
                if (!allAnimationsComplete)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            } while (!allAnimationsComplete);
        }

        #endregion
        #endregion       
    }
}