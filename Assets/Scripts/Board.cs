using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using TMPro;

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
        Resetting    // 遊戲板重置中
    }

    // Board 類別是遊戲的核心管理類別
    // 負責管理遊戲板的所有邏輯，包括寶石交換、匹配、消除等
    public class Board : MonoBehaviour
    {
        #region 變數
        // 單例模式：確保遊戲中只有一個遊戲板實例
        public static Board instance; // 靜態實例變數
        private readonly object swapLock = new object();
        // 遊戲板的基本配置參數
        public int width = 8;       // 遊戲板寬度（列數）
        public int height = 8;      // 遊戲板高度（行數）
        public float gemMoveSpeed = 5f;  // 寶石移動速度的基礎倍率

        // 二維數組，用於追蹤遊戲板上每個位置的寶石
        [HideInInspector] public Gem[,] gems;

        int triggerX, triggerY; // 觸發點的座標
        int[] playerTriggerX, playerTriggerY; // 玩家操作的觸發點座標

        // 遊戲邏輯相關的組件引用
        [SerializeField] public MatchPredictor matchPredictor;  // 匹配預測器
        [SerializeField] public TextMeshPro statusText;  // 顯示遊戲狀態的UI文字
        [SerializeField] public TextMeshPro gemsText;    // 顯示寶石數量的UI文字
        [SerializeField] public TextMeshPro nowRes;      // 顯示當前解析度的UI文字
        //[SerializeField] public Text playerPlayText;   

        // 寶石預製體數組
        [SerializeField] public GameObject[] gemPrefabs;       // 普通寶石的預製體
        [SerializeField] private GameObject[] resGemPrefabs;    // 特殊資源寶石的預製體

        // 遊戲狀態管理的關鍵變數
        private GameState currentState = GameState.Ready;  // 當前遊戲狀態
        private bool isSwitching = false;  // 是否正在交換寶石
        public Gem gem1, gem2;  // 當前正在交換的兩個寶石

        // 性能監控相關變數
        private float lastUpdateTime;  // 上次更新性能指標的時間
        private int frameCounter;      // 幀計數器
        private float fps;             // 當前幀率

        // 遊戲出錯標記
        public bool onError = false;

        //public bool byPlayer = false; // 是否由玩家操作 
        private bool 由玩家觸發生成 = false;

        // 遊戲邏輯輔助類別
        private GemFactory gemFactory;        // 寶石工廠
        private MatchFinder matchFinder;      // 匹配查找器
        public SpecialGemActivator specialGemActivator;  // 特殊寶石激活器
        private BoardResetter boardResetter;  // 遊戲板重置器

        // 遊戲常數：定義各種動畫和操作的持續時間
        public const float SWAP_DURATION = 0.05f;     // 寶石交換動畫持續時間
        public const float DESTROY_DELAY = 0.01f;     // 寶石消除的延遲時間
        public const float COLLECT_DELAY = 0.5f;    // 收集寶石的延遲時間
        public const float COMPLETE_DELAY = 0.01f;   // 完成操作的延遲時間
        private const float FPS_UPDATE_INTERVAL = 0.5f;  // 性能指標更新間隔
        public const float FALL_DELAY = 0.03f;       // 寶石下落的延遲時間
        #endregion

        #region smartPhone
        [Header("Canvas References")]
        public CanvasScaler canvasScaler;
        public Canvas mainCanvas;

        [Header("Resolution Settings")]
        [Tooltip("目標直向寬高比")]
        public float targetAspect = 1080f / 1785f;

        [Tooltip("參考解析度")]
        public Vector2 referenceResolution = new Vector2(1080, 1785);
        #endregion

        #region 屬性
        // 遊戲狀態屬性
        // 提供對當前遊戲狀態的安全訪問和管理
        public GameState CurrentState
        {
            get => currentState;  // 獲取當前遊戲狀態
            private set  // 私有設置器，確保狀態只能由內部邏輯修改
            {
                currentState = value;
                // 每次狀態改變時，自動更新狀態顯示文字
                UpdateStatusText();
            }
        }

        // 移動完成狀態的屬性
        // 提供一種簡潔的方式檢查和設置遊戲是否就緒
        public bool hasMoveCompleted
        {
            get => CurrentState == GameState.Ready;  // 是否處於就緒狀態
            set => CurrentState = value ? GameState.Ready : GameState.Processing;  // 根據傳入值設置遊戲狀態
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
            // 從當前遊戲物件上獲取 MatchPredictor 腳本
            matchPredictor = GetComponent<MatchPredictor>();

            // 初始化遊戲的各個組件
            // 設置遊戲運行所需的各種輔助類別
            InitializeComponents();
            // 強制直向
            Screen.orientation = ScreenOrientation.Portrait;

            // 獲取當前螢幕寬高
            int width = Screen.width;
            int height = Screen.height;

            nowRes.text = $"{width}x{height}";
        }

        // Start 方法：在第一幀更新之前調用
        // 用於執行遊戲開始時需要進行的初始化邏輯
        public void Start()
        {
            // 初始化遊戲板
            // 設置遊戲板的初始狀態和佈局
            InitializeBoard();

            if (IsMobilePlatform())
            {
                ConfigureMobileResolution();
            }
        }

        #region smartPhoneMethods
        bool IsMobilePlatform()
        {
            return Application.platform == RuntimePlatform.WebGLPlayer &&
                   (SystemInfo.deviceType == DeviceType.Handheld);
        }

        void ConfigureMobileResolution()
        {
            // 強制直向
            Screen.orientation = ScreenOrientation.Portrait;

            // 設置解析度
            Screen.SetResolution(1080, 1785, true);

            // 配置 CanvasScaler
            if (canvasScaler != null)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = referenceResolution;
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 1f;
            }

            // 確保Canvas正確縮放
            if (mainCanvas != null)
            {
                mainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            }

            // 限制幀率
            Application.targetFrameRate = 30;

            Debug.Log($"Mobile Resolution Set: 1080x1785, Aspect: {targetAspect}");
        }

        void OnValidate()
        {
            if (Application.isPlaying && IsMobilePlatform())
            {
                ConfigureMobileResolution();
            }
        }
        #endregion
        // Update 方法：每幀調用一次
        // 用於執行持續性的遊戲邏輯和性能監控
        private void Update()
        {
            // 更新遊戲指標
            // 包括寶石數量、性能metrics等
            UpdateGameMetrics();
            //playerPlayText.text = byPlayer ? "Player's Play" : "AI's Play";
        }

        // OnDestroy 方法：當遊戲物件被銷毀時調用
        // 用於進行必要的資源清理工作
        private void OnDestroy()
        {
            // 清理遊戲資源
            // 停止所有協程，銷毀遊戲物件
            CleanupResources();
        }
        #endregion
        #region 初始化方法
        // 初始化遊戲的各個組件
        // 創建支持遊戲運行的各種輔助類別
        private void InitializeComponents()
        {
            // 創建寶石工廠：負責生成和管理遊戲板上的寶石
            // 使用預製體數組作為寶石模板
            gemFactory = new GemFactory(this, gemPrefabs);

            // 創建匹配查找器：檢測遊戲板上的寶石匹配
            // 負責識別可以消除的寶石組合
            matchFinder = new MatchFinder(this);

            // 創建特殊寶石激活器：處理特殊寶石的觸發效果
            // 管理具有特殊能力的寶石的行為
            specialGemActivator = new SpecialGemActivator(this);

            // 創建遊戲板重置器：管理遊戲板的重置邏輯
            // 在需要時可以將遊戲板恢復到初始狀態
            boardResetter = new BoardResetter(this);
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
        void SetupBoard()
        {
            // 遍歷遊戲板的每一個格子
            // 使用巢狀迴圈覆蓋整個遊戲板
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // 在每個位置使用寶石工廠創建寶石
                    // 確保遊戲板被完全填滿且無初始匹配
                    gemFactory.CreateGem(x, y);
                }
            }

            // 將遊戲狀態設置為就緒
            // 表示遊戲板已準備好，玩家可以開始遊戲
            CurrentState = GameState.Ready;

            // 重置匹配預測計時器
            // 開始監控可能的寶石匹配
            matchPredictor?.ResetPredictionTimer();
        }
        #endregion

        #region 遊戲邏輯方法
        // 根據座標從遊戲板獲取特定位置的寶石
        // 提供一個安全的方法來訪問遊戲板上的寶石
        public Gem GetGem(int x, int y)
        {
            // 先檢查座標是否在遊戲板的有效範圍內
            if (IsValidPosition(x, y))
                return gems[x, y];

            // 如果座標無效，返回 null
            return null;
        }

        // 嘗試交換兩個指定位置的寶石
        // 這是玩家互動的核心方法
        public void 嘗試交換寶石(int x1, int y1, int x2, int y2)
        {
            由玩家觸發生成 = true;
            // 首先驗證移動的有效性
            // 檢查座標是否合法、是否為相鄰寶石等
            if (!ValidateMove(x1, y1, x2, y2)) return;

            // 啟動寶石交換的協程
            // 協程允許執行平滑的動畫和複雜的遊戲邏輯
            StartCoroutine(啟動交換寶石(x1, y1, x2, y2));
        }
        private void 執行交換寶石(int x1, int y1, int x2, int y2)
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
        }
        #endregion
        #region 協程方法
        // 寶石交換的協程方法
        // 處理寶石交換的複雜邏輯，包括動畫、安全檢查和匹配處理

        private IEnumerator 啟動交換寶石(int x1, int y1, int x2, int y2)
        {
            if (!ValidateMove(x1, y1, x2, y2))
            {
                Debug.LogWarning($"無效的交換操作：從 ({x1},{y1}) 到 ({x2},{y2})");
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
            執行交換寶石(x1, y1, x2, y2);

            // 計算動畫目標位置
            var pos1 = new Vector3(x2, y2, 0);
            var pos2 = new Vector3(x1, y1, 0);

            // 執行移動動畫
            StartCoroutine(gem1.AnimateMove(pos1, SWAP_DURATION));
            StartCoroutine(gem2.AnimateMove(pos2, SWAP_DURATION));

            yield return new WaitForSeconds(SWAP_DURATION);

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
                執行交換寶石(x2, y2, x1, y1);  // 交換回原始位置

                // 執行返回動畫
                StartCoroutine(gem1.AnimateMove(new Vector3(x1, y1, 0), SWAP_DURATION));
                StartCoroutine(gem2.AnimateMove(new Vector3(x2, y2, 0), SWAP_DURATION));

                yield return new WaitForSeconds(SWAP_DURATION);
            }
            else
            {
                // 有匹配時，處理匹配邏輯
                交換寶石後再檢查();
            }

            gem1.isAnimating = false;
            gem2.isAnimating = false;
            isSwitching = false;
            CurrentState = GameState.Ready;
            matchPredictor?.ResetPredictionTimer();
        }

        private void 交換寶石後再檢查()
        {
            try
            {
                var swappedGem1 = gem1;
                var swappedGem2 = gem2;
                var processedGemIds = new HashSet<int>();  // 添加安全陣列

                if (!IsValidSwapState())
                {
                    Debug.LogWarning($"交換狀態無效: gem1={gem1?.id ?? -1} at ({gem1?.x ?? -1},{gem1?.y ?? -1}), " +
                                    $"gem2={gem2?.id ?? -1} at ({gem2?.x ?? -1},{gem2?.y ?? -1})");
                    onError = true;
                    StartCoroutine(落下寶石());
                    return;
                }

                bool hasValidMatch = false;
                bool hasSpecialGem = swappedGem1?.id >= 100 || swappedGem2?.id >= 100;

                // 特殊寶石組合的規則
                var combinationRules = new Dictionary<(int, int), int>
        {
            // 相同寶石組合
            {(100, 100), 104}, {(101, 101), 104}, // LineH/V + LineH/V = Cross
            {(102, 102), 105}, // Bomb + Bomb = BigBomb
            {(103, 103), 106}, // Rainbow + Rainbow = DestroyAll
            
            // 不同寶石組合
            {(100, 101), 104}, // LineH + LineV = Cross
            {(100, 102), 110}, // LineH + Bomb = RandomHLines
            {(100, 103), 107}, // LineH + Rainbow = ThreeHLines
            {(101, 102), 111}, // LineV + Bomb = RandomVLines
            {(101, 103), 108}, // LineV + Rainbow = ThreeVLines
            {(102, 103), 109}, // Bomb + Rainbow = MultiBomb
        };

                // 玩家操作時的處理順序
                if (由玩家觸發生成)
                {
                    // 1. 檢查是否是特殊寶石與普通寶石交換
                    Gem specialGem = swappedGem1?.id >= 100 ? swappedGem1 : swappedGem2;
                    Gem normalGem = swappedGem1?.id >= 100 ? swappedGem2 : swappedGem1;

                    // 如果是兩個特殊寶石的組合
                    if (swappedGem1?.id >= 100 && swappedGem2?.id >= 100)
                    {
                        var key = (Math.Min(swappedGem1.id, swappedGem2.id),
                                   Math.Max(swappedGem1.id, swappedGem2.id));

                        if (combinationRules.TryGetValue(key, out int combinedResType))
                        {
                            // 在交換位置生成新的組合特殊寶石
                            //int triggerX = (swappedGem1.x + swappedGem2.x) / 2;
                            //int triggerY = (swappedGem1.y + swappedGem2.y) / 2;

                            //生成特殊寶石(triggerX, triggerY, combinedResType - 100);
                            處理特殊寶石的組合(swappedGem1, swappedGem2);

                            // 移除原本的兩個特殊寶石
                            StartCoroutine(淡出與刪除寶石(new HashSet<Gem> { swappedGem1, swappedGem2 }, processedGemIds));
                            hasValidMatch = true;
                        }
                    }
                    // 特殊寶石與普通寶石的交換
                    else if (hasSpecialGem && normalGem?.id < 100)
                    {
                        // 2. 檢查普通寶石位置的匹配情況
                        var matches = matchFinder.FindAllMatches();
                        var matchesForNormalGem = matches.Where(match =>
                            match.matchedGems.Any(gem => gem.x == normalGem.x && gem.y == normalGem.y)).ToList();

                        if (matchesForNormalGem.Count > 0)
                        {
                            // 找出包含該普通寶石的匹配組
                            var matchGroup = matchesForNormalGem
                                .SelectMany(m => m.matchedGems)
                                .Where(gem => gem != null)
                                .GroupBy(gem => gem.id)
                                .SelectMany(group => GetContinuousGroups(group))
                                .Where(group => group.Count >= 4)
                                .FirstOrDefault();

                            if (matchGroup != null)
                            {
                                // 3. 確認是否可以生成特殊寶石
                                var (resourceType, isHorizontal, isVertical, _) = 確認要生成的特殊寶石(matchGroup);
                                if (resourceType != -1)
                                {
                                    // 4. 先在普通寶石位置生成新的特殊寶石
                                    foreach (var groupGem in matchGroup)
                                    {
                                        if (groupGem != null) groupGem.isMatched = true;
                                    }

                                    // 移除匹配的寶石
                                    foreach (var groupGem in matchGroup)
                                    {
                                        if (groupGem != null && IsValidPosition(groupGem.x, groupGem.y))
                                        {
                                            gems[groupGem.x, groupGem.y] = null;
                                        }
                                    }

                                    foreach (var groupGem in matchGroup)
                                    {
                                        if (groupGem != null && IsValidPosition(groupGem.x, groupGem.y))
                                        {
                                            gems[groupGem.x, groupGem.y] = null;
                                            processedGemIds.Remove(groupGem.GetInstanceID());  // 從安全陣列移除
                                        }
                                    }

                                    StartCoroutine(淡出與刪除寶石(new HashSet<Gem>(matchGroup), processedGemIds));

                                    // 在原本普通寶石的位置生成新的特殊寶石
                                    生成特殊寶石(normalGem.x, normalGem.y, resourceType);

                                    hasValidMatch = true;
                                }
                            }
                        }

                        // 5. 觸發交換來的特殊寶石效果
                        if (specialGem != null)
                        {
                            specialGemActivator.啟動特殊寶石(specialGem);
                            hasValidMatch = true;
                        }

                        // 6. 檢查並處理其他匹配
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
                            StartCoroutine(處理配對序列());
                        }
                    }
                    // 其他情況（兩個特殊寶石或純普通寶石）的處理
                    //else if (hasSpecialGem)
                    //{
                    //    if (swappedGem1?.id >= 100)
                    //    {
                    //        specialGemActivator.啟動特殊寶石(swappedGem1);
                    //        hasValidMatch = true;
                    //    }
                    //    if (swappedGem2?.id >= 100)
                    //    {
                    //        specialGemActivator.啟動特殊寶石(swappedGem2);
                    //        hasValidMatch = true;
                    //    }

                    //    // 檢查並處理後續匹配
                    //    var matches = matchFinder.FindAllMatches();
                    //    if (matches.Count > 0)
                    //    {
                    //        foreach (var match in matches)
                    //        {
                    //            foreach (var matchGem in match.matchedGems)
                    //            {
                    //                if (matchGem != null) matchGem.isMatched = true;
                    //            }
                    //        }
                    //        StartCoroutine(處理配對序列());
                    //    }
                    //}
                    else
                    {
                        // 純普通寶石交換的處理
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
                            StartCoroutine(處理配對序列());
                            hasValidMatch = true;
                        }
                    }
                }
                // 非玩家操作的處理
                else
                {
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
                        StartCoroutine(處理配對序列());
                        hasValidMatch = true;
                    }
                    else if (hasSpecialGem)
                    {
                        if (處理特殊寶石的組合(swappedGem1, swappedGem2))
                        {
                            hasValidMatch = true;
                        }
                    }
                }

                if (!hasValidMatch)
                {
                    Debug.LogWarning("Unexpected: 無效匹配狀態");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"處理配對時發生錯誤: {e.Message}\n{e.StackTrace}");
                onError = true;
                StartCoroutine(落下寶石());
            }
        }

        // 新增一個輔助方法來設置觸發點 triggerX, triggerY
        // 修改 設置觸發點 方法
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
                        .SelectMany(group => GetContinuousGroups(group))
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

        // 新增的輔助方法
        private bool IsValidSwapState()
        {
            try
            {
                // 基本的空值檢查
                if (gem1 == null || gem2 == null)
                {
                    Debug.LogWarning("交換失敗: 有寶石為空");
                    return false;
                }

                // 檢查位置是否在遊戲板範圍內
                if (!IsValidPosition(gem1.x, gem1.y) || !IsValidPosition(gem2.x, gem2.y))
                {
                    Debug.LogWarning($"交換失敗: 位置無效 - Gem1({gem1.x},{gem1.y}), Gem2({gem2.x},{gem2.y})");
                    return false;
                }

                // 確保遊戲板陣列中存在這些寶石
                // 如果是特殊寶石，則放寬狀態檢查
                if (gem1.id >= 100 || gem2.id >= 100)
                {
                    return true;
                }
                else if (gems[gem1.x, gem1.y] == null || gems[gem2.x, gem2.y] == null)
                {
                    Debug.LogWarning("交換失敗: 寶石不在遊戲板中");
                    return false;
                }

                // 一般寶石才需要檢查遊戲狀態
                if (CurrentState != GameState.Swapping && CurrentState != GameState.Ready)
                {
                    Debug.LogWarning($"交換失敗: 遊戲狀態不正確: {CurrentState}");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"IsValidSwapState error: {e.Message}");
                return false;
            }
        }

        private bool 處理特殊寶石的組合(Gem first, Gem second)
        {
            try
            {
                // 如果都不是特殊寶石，直接返回
                if (first.id < 100 && second.id < 100)
                {
                    return false;
                }

                var combinationRules = new Dictionary<(int, int), int>
        {
            // 相同寶石組合
            {(100, 100), 104}, {(101, 101), 104}, // LineH/V + LineH/V = Cross
            {(102, 102), 105}, // Bomb + Bomb = BigBomb
            {(103, 103), 106}, // Rainbow + Rainbow = DestroyAll
            
            // 不同寶石組合
            {(100, 101), 104}, // LineH + LineV = Cross
            {(100, 102), 110}, // LineH + Bomb = RandomHLines
            {(100, 103), 107}, // LineH + Rainbow = ThreeHLines
            {(101, 102), 111}, // LineV + Bomb = RandomVLines
            {(101, 103), 108}, // LineV + Rainbow = ThreeVLines
            {(102, 103), 109}, // Bomb + Rainbow = MultiBomb
        };

                // 處理一般寶石與特殊寶石的組合
                bool firstIsSpecial = first.id >= 100;
                bool secondIsSpecial = second.id >= 100;

                if (firstIsSpecial && secondIsSpecial)
                {
                    // 兩個都是特殊寶石的情況
                    var key = (Math.Min(first.id, second.id), Math.Max(first.id, second.id));
                    if (combinationRules.TryGetValue(key, out int resultId))
                    {
                        // 只創建一個新的特殊寶石效果
                        first.id = resultId;
                        specialGemActivator.啟動特殊寶石(first);
                        // 不要呼叫 second 的效果，它會在觸發特殊寶石時一起被消除
                        return true;
                    }
                }
                else
                {
                    // 一般寶石和特殊寶石的組合
                    Gem specialGem = firstIsSpecial ? first : second;
                    specialGemActivator.啟動特殊寶石(specialGem);
                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"ProcessSpecialGems error: {e.Message}");
                return false;
            }
        }

        // 檢查遊戲板上是否存在寶石匹配
        // 這是遊戲核心邏輯的關鍵方法，用於偵測可消除的寶石組合
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

        // 驗證移動有效性的輔助方法
        // 確保玩家只能進行合法的寶石交換
        private bool ValidateMove(int x1, int y1, int x2, int y2)
        {
            // 檢查座標是否在遊戲板範圍內
            // 防止訪問陣列越界或非法座標
            if (!IsValidPosition(x1, y1) || !IsValidPosition(x2, y2))
            {
                // 記錄無效的移動嘗試，協助除錯
                LogError("無效的移動位置：({0},{1}) 到 ({2},{3})", x1, y1, x2, y2);
                return false;
            }

            // 檢查是否為相鄰位置的交換
            // 只允許上下左右直接相鄰的寶石交換
            bool isAdjacent =
                (Math.Abs(x1 - x2) == 1 && y1 == y2) ||  // 水平相鄰
                (Math.Abs(y1 - y2) == 1 && x1 == x2);    // 垂直相鄰

            if (!isAdjacent)
            {
                // 記錄不相鄰的交換嘗試
                LogError("只允許交換相鄰的寶石：({0},{1}) 到 ({2},{3})", x1, y1, x2, y2);
                return false;
            }

            return true;
        }

        // 處理匹配序列的協程
        // 控制消除寶石的整體流程
        private IEnumerator 處理配對序列()
        {
            // 等待一段短暫的延遲，增加視覺效果
            yield return new WaitForSeconds(DESTROY_DELAY);

            // 銷毀匹配的寶石
            // 傳入是否由玩家互動觸發，以及觸發點的座標
            刪除寶石();

            // 再次等待，控制消除的節奏
            yield return new WaitForSeconds(DESTROY_DELAY);
        }
        #endregion        

        // 處理連鎖反應的協程
        // 檢測並處理填充後可能出現的連續匹配
        private IEnumerator 處理連鎖反應()
        {
            // 短暫等待，確保視覺效果的流暢性
            yield return new WaitForSeconds(0.1f);

            // 持續檢查是否存在匹配
            while (偵測可消除的寶石組合())
            {
                // 等待消除延遲
                yield return new WaitForSeconds(DESTROY_DELAY);

                // 銷毀匹配的寶石（非互動觸發）
                刪除寶石();

                // 再次等待，控制消除的節奏
                yield return new WaitForSeconds(DESTROY_DELAY);
            }

            // 如果當前狀態不是處理中，則將遊戲狀態重置為就緒
            if (CurrentState != GameState.Processing)
            {
                CurrentState = GameState.Ready;

                // 重置匹配預測計時器
                matchPredictor?.ResetPredictionTimer();
            }
        }

        // 檢查指定位置上方是否有懸空的寶石
        // 防止在填充空白位置時出現不合理的寶石掉落
        private bool 檢查上方為空(int x, int y)
        {
            // 檢查該列上方是否有非空的寶石
            for (int above = y + 1; above < height; above++)
            {
                if (gems[x, above] != null)
                    return true;
            }
            return false;
        }

        // 創建並掉落新寶石的方法
        // 在遊戲板空白位置生成新的寶石
        private void 生成與掉落寶石(int x, int y, ref int dropDelay)
        {
            // 使用寶石工廠在指定位置創建新寶石
            gemFactory.CreateGem(x, y);

            // 設置寶石的初始位置（在遊戲板頂部）
            // 添加 dropDelay 增加視覺上的錯開效果
            gems[x, y].transform.position = new Vector3(x, height + dropDelay, 0);

            // 執行寶石下落動畫
            // 使用協程確保動畫平滑
            StartCoroutine(gems[x, y].AnimateMove(
                new Vector3(x, y, 0),  // 目標位置
                FALL_DELAY     // 動畫持續時間
            ));

            // 增加掉落延遲，使多個寶石有不同的掉落間隔
            dropDelay++;
        }
        // 填充遊戲板空白位置的協程
        // 負責在消除寶石後重新填滿遊戲板的空白格子

        #region Match Processing
        // 銷毀遊戲板上匹配的寶石
        // 啟動消除匹配寶石的整體流程
        private void 刪除寶石()
        {
            // 使用匹配查找器尋找所有可消除的寶石組
            var matches = matchFinder.FindAllMatches();

            // 將所有匹配的寶石展平成一個列表
            // 使用 LINQ 的 SelectMany 方法合併所有匹配組中的寶石
            List<Gem> allMatchedGems = matches.SelectMany(m => m.matchedGems).ToList();

            allMatchedGems = allMatchedGems.Distinct().ToList();  // 去除重複的寶石

            // 啟動銷毀匹配寶石的協程
            // 傳入匹配的寶石列表和觸發消除的相關信息
            StartCoroutine(刪除寶石序列(allMatchedGems));
        }

        private List<List<Gem>> GetContinuousGroups(IGrouping<int, Gem> group)
        {
            var result = new List<List<Gem>>();
            var gems = group.ToList();

            // 1. 找出所有水平連續組
            var horizontalGroups = gems.GroupBy(g => g.y)
                .Select(row =>
                {
                    var sortedRow = row.OrderBy(g => g.x).ToList();
                    var continuousGroups = new List<List<Gem>>();
                    var currentGroup = new List<Gem>();

                    for (int i = 0; i < sortedRow.Count; i++)
                    {
                        if (currentGroup.Count == 0)
                        {
                            currentGroup.Add(sortedRow[i]);
                        }
                        else if (sortedRow[i].x == currentGroup.Last().x + 1)
                        {
                            currentGroup.Add(sortedRow[i]);
                        }
                        else
                        {
                            if (currentGroup.Count >= 3)
                            {
                                continuousGroups.Add(new List<Gem>(currentGroup));
                            }
                            currentGroup = new List<Gem> { sortedRow[i] };
                        }
                    }

                    if (currentGroup.Count >= 3)
                    {
                        continuousGroups.Add(currentGroup);
                    }

                    return continuousGroups;
                })
                .SelectMany(g => g)
                .ToList();

            // 2. 找出所有垂直連續組
            var verticalGroups = gems.GroupBy(g => g.x)
                .Select(column =>
                {
                    var sortedColumn = column.OrderBy(g => g.y).ToList();
                    var continuousGroups = new List<List<Gem>>();
                    var currentGroup = new List<Gem>();

                    for (int i = 0; i < sortedColumn.Count; i++)
                    {
                        if (currentGroup.Count == 0)
                        {
                            currentGroup.Add(sortedColumn[i]);
                        }
                        else if (sortedColumn[i].y == currentGroup.Last().y + 1)
                        {
                            currentGroup.Add(sortedColumn[i]);
                        }
                        else
                        {
                            if (currentGroup.Count >= 3)
                            {
                                continuousGroups.Add(new List<Gem>(currentGroup));
                            }
                            currentGroup = new List<Gem> { sortedColumn[i] };
                        }
                    }

                    if (currentGroup.Count >= 3)
                    {
                        continuousGroups.Add(currentGroup);
                    }

                    return continuousGroups;
                })
                .SelectMany(g => g)
                .ToList();

            // 3. 檢測 T 型、L 型和 X 型匹配
            foreach (var gem in gems)
            {
                // 檢查以當前寶石為中心的十字形
                var crossGems = new List<Gem>();
                var horizontalGems = gems.Where(g => g.y == gem.y).OrderBy(g => g.x).ToList();
                var verticalGems = gems.Where(g => g.x == gem.x).OrderBy(g => g.y).ToList();

                // 計算連續的水平和垂直寶石
                var leftGems = horizontalGems.Where(g => g.x < gem.x && g.x >= gem.x - 2).ToList();
                var rightGems = horizontalGems.Where(g => g.x > gem.x && g.x <= gem.x + 2).ToList();
                var upGems = verticalGems.Where(g => g.y > gem.y && g.y <= gem.y + 2).ToList();
                var downGems = verticalGems.Where(g => g.y < gem.y && g.y >= gem.y - 2).ToList();

                // 檢查 T 型匹配
                bool hasHorizontalMatch = (leftGems.Count + rightGems.Count) >= 2;
                bool hasVerticalMatch = (upGems.Count + downGems.Count) >= 2;

                if (hasHorizontalMatch && hasVerticalMatch)
                {
                    var specialGroup = new List<Gem> { gem };
                    specialGroup.AddRange(leftGems);
                    specialGroup.AddRange(rightGems);
                    specialGroup.AddRange(upGems);
                    specialGroup.AddRange(downGems);

                    // 確認是否為有效的特殊形狀
                    if (specialGroup.Count >= 5)
                    {
                        bool isTShape = (leftGems.Count >= 1 && rightGems.Count >= 1 && (upGems.Count >= 2 || downGems.Count >= 2)) ||
                                       (upGems.Count >= 1 && downGems.Count >= 1 && (leftGems.Count >= 2 || rightGems.Count >= 2));

                        bool isLShape = (leftGems.Count >= 2 && (upGems.Count >= 2 || downGems.Count >= 2)) ||
                                       (rightGems.Count >= 2 && (upGems.Count >= 2 || downGems.Count >= 2));

                        bool isXShape = (leftGems.Count >= 1 && rightGems.Count >= 1 && upGems.Count >= 1 && downGems.Count >= 1);

                        if (isTShape || isLShape || isXShape)
                        {
                            if (!result.Any(g => g.Contains(gem)))
                            {
                                result.Add(specialGroup.Distinct().ToList());
                            }
                        }
                    }
                }
            }

            // 將基本的水平和垂直組也加入結果中
            result.AddRange(horizontalGroups);
            result.AddRange(verticalGroups);

            // 移除重複的組合
            return result.Where(group => group.Count >= 4)
                        .Distinct(new ListComparer<Gem>())
                        .ToList();
        }

        // 用於比較兩個寶石列表是否相同的輔助類
        private class ListComparer<T> : IEqualityComparer<List<T>>
        {
            public bool Equals(List<T> x, List<T> y)
            {
                return x.Count == y.Count && !x.Except(y).Any();
            }

            public int GetHashCode(List<T> obj)
            {
                int hash = 17;
                foreach (var item in obj.OrderBy(x => x.GetHashCode()))
                {
                    hash = hash * 31 + item.GetHashCode();
                }
                return hash;
            }
        }
        private IEnumerator 刪除寶石序列(List<Gem> matchedGems)
        {
            var processedGems = new HashSet<Gem>();
            var matches = matchFinder.FindAllMatches();
            var processedGemIds = new HashSet<int>();  // 移到外部，讓玩家和非玩家操作都能使用

            設置觸發點(matches, gem1, gem2);

            var matchGroups = matches.SelectMany(m => m.matchedGems)
                .Where(gem => gem != null)
                .GroupBy(gem => gem.id)
                .SelectMany(group => GetContinuousGroups(group))
                .Where(group => group.Count >= 4)
                .ToList();
            matchGroups = matchGroups.Distinct().ToList();

            // 先觸發要被消除的特殊寶石效果
            var specialGems = matchedGems.Where(g =>
                g != null &&
                g.id >= 100 &&
                gems[g.x, g.y] == g &&
                !processedGemIds.Contains(g.GetInstanceID())).ToList();  // 排除已處理的寶石

            foreach (var gem in specialGems)
            {
                specialGemActivator.啟動特殊寶石(gem);
                yield return new WaitForSeconds(DESTROY_DELAY);
            }

            // 清除匹配的普通寶石時，同時從安全陣列移除
            foreach (var gem in matchedGems.Where(g =>
                g != null &&
                g.id < 100 &&
                gems[g.x, g.y] == g &&
                !processedGemIds.Contains(g.GetInstanceID())))
            {
                processedGems.Add(gem);
                gems[gem.x, gem.y] = null;
                processedGemIds.Remove(gem.GetInstanceID());  // 從安全陣列移除
            }

            // 在淡出與刪除寶石方法中也要處理
            yield return StartCoroutine(淡出與刪除寶石(processedGems, processedGemIds));  // 傳入安全陣列
            yield return new WaitForSeconds(DESTROY_DELAY);

            if (由玩家觸發生成)
            {
                // 使用已經設置好的玩家觸發點來生成特殊寶石
                for (int i = 0; i < matchGroups.Count && i < playerTriggerX.Length; i++)
                {
                    var group = matchGroups[i]
                        .Where(g => !processedGemIds.Contains(g.GetInstanceID()))
                        .ToList();

                    if (group.Count < 4) continue;  // 如果過濾後數量不夠就跳過

                    // 使用已有的 GetContinuousGroups 檢查連續性
                    var continuousGroups = GetContinuousGroups(group.GroupBy(g => g.id).First());
                    if (!continuousGroups.Any(g => g.Count >= 4)) continue;

                    var (resourceType, isHorizontal, isVertical, _) = 確認要生成的特殊寶石(group);
                    if (resourceType != -1)
                    {
                        triggerX = playerTriggerX[i];
                        triggerY = playerTriggerY[i];
                        if (IsValidPosition(triggerX, triggerY))
                        {
                            Debug.Log($"生成特殊寶石byPlayer: ,({triggerX}, {triggerY}),Res: {resourceType} count: {group.Count}");
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
                //設置觸發點(matches, gem1, gem2);
                foreach (var group in matchGroups)
                {
                    if (group.Any(g => processedGemIds.Contains(g.GetInstanceID())))
                    {
                        continue;  // 如果組中有任何已處理的寶石就跳過
                    }

                    // 檢查連續性
                    var continuousGroups = GetContinuousGroups(group.GroupBy(g => g.id).First());
                    if (!continuousGroups.Any(g => g.Count >= 4)) continue;

                    var (resourceType, isHorizontal, isVertical, _) = 確認要生成的特殊寶石(group.ToList());
                    if (resourceType != -1)
                    {
                        // 從該組中隨機選擇一個位置作為特殊寶石的生成點
                        var validGems = group.Where(g => !processedGemIds.Contains(g.GetInstanceID())).ToList();
                        if (validGems.Any())
                        {
                            int randomIndex = UnityEngine.Random.Range(0, validGems.Count);
                            var selectedGem = validGems[randomIndex];
                            triggerX = selectedGem.x;
                            triggerY = selectedGem.y;

                            Debug.Log($"非玩家操作時 - 在組內隨機位置({triggerX},{triggerY})生成特殊寶石 Type:{resourceType}");

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

            yield return StartCoroutine(落下寶石());

        }

        // 寶石淡出效果的協程
        // 提供視覺上平滑的消除動畫
        public IEnumerator 淡出與刪除寶石(HashSet<Gem> gems, HashSet<int> safeIds)
        {
            var fadeData = gems.Select(gem => (
                gem,
                renderer: gem.GetComponent<SpriteRenderer>(),
                startColor: gem.GetComponent<SpriteRenderer>().color
            )).ToList();

            float alpha = 1f;
            while (alpha > 0)
            {
                alpha -= FALL_DELAY;
                foreach (var (gem, renderer, startColor) in fadeData)
                {
                    if (gem != null && gem.gameObject != null && renderer != null)
                    {
                        renderer.color = new Color(
                            startColor.r,
                            startColor.g,
                            startColor.b,
                            alpha
                        );
                    }
                }
                yield return null;
            }

            // 銷毀寶石時同時從安全陣列移除
            foreach (var (gem, _, _) in fadeData)
            {
                if (gem != null && gem.gameObject != null)
                {
                    safeIds.Remove(gem.GetInstanceID());  // 從安全陣列移除
                    Destroy(gem.gameObject);
                }
            }
        }

        private (int resourceType, bool isHorizontal, bool isVertical, List<Gem> matchedGems) 確認要生成的特殊寶石(List<Gem> gems)
        {
            //Debug.Log($"確認要生成的特殊寶石 - 匹配寶石數: {gems.Count} player：{byPlayer}");
            //gems = gems.Distinct().ToList();  // 去除重複的寶石
            if (gems.Count < 4) // 修改為最少需要4個寶石才能生成特殊寶石
            {
                return (-1, false, false, new List<Gem>());
            }

            // 檢查水平方向的連線
            bool hasHorizontalLine = false;
            var horizontalGroups = gems.GroupBy(g => g.y)
                                      .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in horizontalGroups)
            {
                var sortedGems = group.Value.OrderBy(g => g.x).ToList();
                int consecutiveCount = 1;

                for (int i = 1; i < sortedGems.Count; i++)
                {
                    if (sortedGems[i].x == sortedGems[i - 1].x + 1)
                    {
                        consecutiveCount++;
                        if (consecutiveCount >= 2) // 至少需要3個連續的
                        {
                            hasHorizontalLine = true;
                            break;
                        }
                    }
                    else
                    {
                        consecutiveCount = 1;
                    }
                }
            }

            // 檢查垂直方向的連線
            bool hasVerticalLine = false;
            var verticalGroups = gems.GroupBy(g => g.x)
                                    .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var group in verticalGroups)
            {
                var sortedGems = group.Value.OrderBy(g => g.y).ToList();
                int consecutiveCount = 1;

                for (int i = 1; i < sortedGems.Count; i++)
                {
                    if (sortedGems[i].y == sortedGems[i - 1].y + 1)
                    {
                        consecutiveCount++;
                        if (consecutiveCount >= 2) // 至少需要3個連續的
                        {
                            hasVerticalLine = true;
                            break;
                        }
                    }
                    else
                    {
                        consecutiveCount = 1;
                    }
                }
            }

            bool isCornerMatch = hasHorizontalLine && hasVerticalLine;

            // 根據不同的匹配模式決定要生成的特殊寶石類型
            int resourceType;
            if (isCornerMatch)
            {
                if (gems.Count >= 6)
                {
                    //resourceType = 3; // Rainbow
                    return (3, hasHorizontalLine, hasVerticalLine, gems);
                }
                else
                {
                    //resourceType = 2; // Bomb
                    return (2, hasHorizontalLine, hasVerticalLine, gems);
                }
            }
            else
            {
                if (gems.Count >= 5)
                {
                    resourceType = 3; // Rainbow
                }
                else if (hasVerticalLine)
                {
                    resourceType = 1; // 垂直清除線
                }
                else if (hasHorizontalLine)
                {
                    resourceType = 0; // 水平清除線
                }
                else
                {
                    resourceType = -1; // 不生成特殊寶石
                }
            }


            // Debug 輸出，幫助確認生成邏輯
            //Debug.Log($"數: {gems.Count}, 平: {hasHorizontalLine}, " +
            //          $"直: {hasVerticalLine}, 角: {isCornerMatch}, " +
            //          $"型: {resourceType} player：{byPlayer} isSwitching: {isSwitching}");

            return (resourceType, hasHorizontalLine, hasVerticalLine, gems);
        }


        // 創建特殊資源寶石的方法
        // 實際生成特殊寶石遊戲物件的邏輯
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

        // 使寶石下落並填補空白位置的協程
        // 這是遊戲板動態重組的核心邏輯
        private IEnumerator 落下寶石()
        {
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
                                yield return StartCoroutine(gem.AnimateMove(new Vector3(x, y, 0), FALL_DELAY));
                                hasFalling = true;
                            }
                        }
                    }
                }
            } while (hasFalling);

            // 填充空位
            yield return StartCoroutine(填滿空位(-1));
        }

        private IEnumerator 處理單列下落(int x)
        {
            bool hasMoreFalling;
            do
            {
                hasMoreFalling = false;

                // 從底部開始向上檢查
                for (int y = 0; y < height - 1; y++)
                {
                    if (gems[x, y] == null)
                    {
                        // 找到這個位置上方最近的寶石
                        int above = y + 1;
                        while (above < height && gems[x, above] == null)
                        {
                            above++;
                        }

                        // 如果找到了寶石，讓它下落
                        if (above < height && gems[x, above] != null)
                        {
                            var gem = gems[x, above];
                            gems[x, y] = gem;
                            gems[x, above] = null;
                            gem.y = y;

                            // 計算下落距離和時間
                            float fallDistance = above - y;
                            float fallDuration = FALL_DELAY * fallDistance;

                            // 等待這個寶石的下落動畫完成
                            yield return StartCoroutine(gem.AnimateMove(new Vector3(x, y, 0), fallDuration));
                            hasMoreFalling = true;
                        }
                    }
                }
            } while (hasMoreFalling);
        }

        public IEnumerator 填滿空位(int resType)
        {
            CurrentState = GameState.Filling;
            matchPredictor?.StopTimer();

            // 逐列處理
            for (int x = 0; x < width; x++)
            {
                bool columnComplete;
                do
                {
                    columnComplete = true;
                    // 從底部開始檢查
                    for (int y = 0; y < height; y++)
                    {
                        if (gems[x, y] == null)
                        {
                            int dropDelay = 0;
                            生成與掉落寶石(x, y, ref dropDelay);
                            yield return new WaitForSeconds(FALL_DELAY);

                            // 確認是否真的填充了
                            if (gems[x, y] == null)
                            {
                                columnComplete = false;
                                Debug.LogError($"位置 ({x}, {y}) 填充失敗");
                            }
                        }
                    }
                } while (!columnComplete); // 如果這一列還有空位，就重新處理這一列
            }

            yield return 處理連鎖反應();
        }

        private IEnumerator 處理單列填充(int x, int resType)
        {
            int dropDelay = 0;
            bool needsFilling;

            do
            {
                needsFilling = false;

                // 從底部開始向上檢查
                for (int y = 0; y < height; y++)
                {
                    if (gems[x, y] == null)
                    {
                        if (resType == -1)
                        {
                            生成與掉落寶石(x, y, ref dropDelay);
                            needsFilling = true;
                            // 等待這個寶石的下落動畫完成
                            yield return new WaitForSeconds(FALL_DELAY);
                        }
                    }
                }
            } while (needsFilling);
        }
        // 等待單個寶石下落完成的協程
        // 控制個別寶石的下落動畫和回調
        private IEnumerator WaitForGemFall(Gem gem, int x, int y, System.Action onComplete)
        {
            // 執行寶石移動動畫
            // 使用協程確保動畫平滑進行
            yield return StartCoroutine(gem.AnimateMove(
                new Vector3(x, y, 0),  // 目標位置
                FALL_DELAY     // 動畫持續時間，與遊戲速度關聯  
            ));

            // 動畫完成後調用回調函數
            // 通常用於遞減動畫中的寶石計數
            onComplete?.Invoke();
        }

        // 匹配信息類別：封裝寶石匹配的詳細信息
        // 這個類別提供了一種結構化的方式來描述遊戲板上的寶石匹配情況
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

        // 淡出並銷毀指定的寶石列表的協程
        // 提供了一個安全、視覺上平滑的寶石消除機制
        public IEnumerator 消失與刪除寶石(List<Gem> gemsToDestroy, bool autoFill = true)
        {
            if (gemsToDestroy == null || gemsToDestroy.Count == 0)
            {
                yield break;
            }

            var safeGems = gemsToDestroy.Where(gem => gem != null && gem.gameObject != null).ToList();
            if (safeGems.Count == 0) yield break;

            // 從遊戲板中移除寶石
            foreach (var gem in safeGems)
            {
                if (gem != null && IsValidPosition(gem.x, gem.y))
                {
                    gems[gem.x, gem.y] = null;
                }
            }

            // 執行淡出動畫
            float alpha = 1f;
            while (alpha > 0)
            {
                alpha -= Time.deltaTime * 5f;
                foreach (var gem in safeGems)
                {
                    if (gem != null && gem.gameObject != null)
                    {
                        var renderer = gem.GetComponent<SpriteRenderer>();
                        if (renderer != null)
                        {
                            renderer.color = new Color(renderer.color.r, renderer.color.g, renderer.color.b, alpha);
                        }
                    }
                }
                yield return null;
            }

            // 銷毀寶石
            foreach (var gem in safeGems)
            {
                if (gem != null && gem.gameObject != null)
                {
                    Destroy(gem.gameObject);
                }
            }

            // 只有在 autoFill 為 true 時才執行填充
            //if (autoFill)
            {
                yield return StartCoroutine(落下寶石());
            }
        }

        // 更新遊戲狀態文字的方法
        // 根據當前遊戲狀態動態顯示提示信息，提供直觀的遊戲進度反饋
        private void UpdateStatusText()
        {
            // 確保狀態文字組件存在
            if (statusText != null)
            {
                // 使用 switch 表達式根據遊戲狀態設置對應的文字
                // 為玩家提供清晰的當前遊戲階段提示
                statusText.text = currentState switch
                {
                    GameState.Ready => "準備中：可以開始交換寶石",       // 遊戲就緒，玩家可以自由操作
                    GameState.Swapping => "交換中：寶石正在移動",        // 正在執行寶石交換動畫
                    GameState.Processing => "處理中：消除匹配的寶石",     // 正在檢查和消除匹配的寶石
                    GameState.Filling => "填充中：補充新的寶石",         // 正在填充空白位置
                    GameState.Resetting => "重置中：遊戲板恢復初始狀態",  // 遊戲板正在重置
                    _ => "遊戲進行中"                                  // 預設狀態
                };
            }
        }

        // 更新遊戲指標的方法
        // 監控遊戲性能和遊戲板狀態，提供即時的遊戲運行資訊
        private void UpdateGameMetrics()
        {
            // 更新遊戲板上的寶石數量顯示
            // 為玩家提供即時的遊戲板狀態資訊
            if (gemsText != null)
            {
                gemsText.text = "寶石數量：" + transform.childCount;
            }

            // 更新性能指標
            // 持續監控遊戲運行的幀率和性能
            UpdatePerformanceMetrics();

            // 定期清理遊戲板
            // 每300幀執行一次清理操作，優化遊戲性能和資源管理
            if (Time.frameCount % 100 == 0)
            {
                CleanupBoard();
            }
        }

        // 清理遊戲資源的方法
        // 在遊戲結束或場景切換時釋放佔用的遊戲資源
        private void CleanupResources()
        {
            // 立即停止所有正在運行的協程
            // 防止殘留的協程繼續執行，造成不可預期的行為
            StopAllCoroutines();

            // 遍歷並銷毀遊戲板上的所有子物件
            // 確保徹底清理遊戲場景，釋放記憶體
            foreach (Transform child in transform)
            {
                if (child != null)
                {
                    // 完全銷毀遊戲物件，釋放佔用的資源
                    Destroy(child.gameObject);
                }
            }
        }

        // 驗證座標是否在遊戲板有效範圍內的方法
        // 防止訪問陣列越界，保證遊戲邏輯的安全性
        private bool IsValidPosition(int x, int y)
        {
            // 檢查座標條件：
            // 1. x 座標在 0 到 width-1 之間
            // 2. y 座標在 0 到 height-1 之間
            // 確保座標落在遊戲板的合法範圍內
            return x >= 0 && x < width && y >= 0 && y < height;
        }

        // 記錄錯誤信息的輔助方法
        // 提供統一的錯誤日誌記錄機制，方便追蹤和除錯
        private void LogError(string message, params object[] args)
        {
            // 使用格式化字串，在錯誤信息前加上遊戲板的標記
            // 幫助快速定位錯誤來源和上下文
            Debug.LogError($"[遊戲板錯誤] {string.Format(message, args)}");
        }

        // 更新性能指標的方法
        // 監控遊戲的幀率（FPS），並在性能下降時採取調整措施
        private void UpdatePerformanceMetrics()
        {
            // 遞增幀計數器
            // 用於追蹤每個更新週期內的幀數
            frameCounter++;

            // 計算自上次更新以來經過的時間
            // 這是計算實際幀率的關鍵
            float timeElapsed = Time.time - lastUpdateTime;

            // 當經過預設的更新間隔時
            // 定期檢查遊戲的性能表現
            if (timeElapsed >= FPS_UPDATE_INTERVAL)
            {
                // 計算當前幀率
                // 幀率 = 幀數 / 經過的時間
                // 這提供了遊戲在當前硬體上的實際運行性能
                fps = frameCounter / timeElapsed;

                // 重置幀計數器和最後更新時間
                // 為下一個性能檢查週期做準備
                frameCounter = 0;
                lastUpdateTime = Time.time;

                // 如果幀率低於30（表示性能不佳），調整遊戲性能
                // 30 FPS 是遊戲流暢運行的最低建議幀率
                if (fps < 30)
                {
                    // 呼叫性能調整方法
                    // 嘗試通過降低動畫複雜度來提升整體性能
                    AdjustPerformance();
                }
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
                            Debug.Log($"已修復特殊寶石在位置 ({gemComponent.x},{gemComponent.y})");
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
                        Debug.Log($"★★★發現無效普通寶石★★★ id: {gemComponent.id} , ({gemComponent.x},{gemComponent.y})");
                        Destroy(child.gameObject);
                    }
                }
            }
        }
        // 調整遊戲性能的方法
        // 當遊戲幀率低於性能標準時，動態降低遊戲複雜度
        private void AdjustPerformance()
        {
            // 通過增加寶石移動速度來降低動畫複雜度
            // 這個方法實際上是減慢動畫，減輕GPU和CPU的渲染負擔
            // 使用 Mathf.Min 確保速度不會超過最大允許值
            // - 當前速度會乘以1.2，模擬動畫加速
            // - 最大值被限制在10，防止過度加速導致遊戲體驗下降
            gemMoveSpeed = Mathf.Min(gemMoveSpeed * 1.2f, 10f);

            // 性能調整策略說明：
            // 1. 當幀率低於30FPS時觸發
            // 2. 通過減慢動畫來降低計算複雜度
            // 3. 平衡遊戲性能和視覺體驗
        }

        // 驗證寶石是否有效的方法
        // 確保寶石在遊戲板上的狀態是合法和一致的
        private bool IsGemValid(Gem gem)
        {
            // 執行多重驗證檢查，確保寶石的有效性：
            // 1. 檢查寶石座標是否在遊戲板的有效範圍內
            //    使用 IsValidPosition 方法確保 x 和 y 座標在遊戲板界限內
            // 2. 確認遊戲板陣列中該位置的寶石與當前寶石一致
            //    防止因為交換或其他操作導致的不一致狀態
            return IsValidPosition(gem.x, gem.y) &&
                   gems[gem.x, gem.y] == gem;

            // 驗證邏輯解釋：
            // - 座標有效性：防止訪問陣列越界
            // - 一致性檢查：確保寶石在遊戲板陣列中的正確性
            // - 任何一個檢查失敗都意味著寶石無效
        }

        #endregion
    }
}