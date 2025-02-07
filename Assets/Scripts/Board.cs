using UnityEngine;
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

        //public bool byPlayer = false; // 是否由玩家操作 
        private bool 由玩家觸發生成 = false;

        // 遊戲邏輯輔助類別
        private GemFactory gemFactory;        // 寶石工廠
        private MatchFinder matchFinder;      // 匹配查找器
        public SpecialGemActivator specialGemActivator;  // 特殊寶石激活器


        // 遊戲常數：定義各種動畫和操作的持續時間
        public const float SWAP_DURATION = 0.2f;     // 寶石交換動畫持續時間
        public const float DESTROY_DELAY = 0.3f;     // 寶石消除的延遲時間（改短一點）
        public const float FADE_DELAY = 0.3f;        // 淡出延遲（大幅縮短）
        public const float COLLECT_DELAY = 0.3f;     // 收集寶石的延遲時間
        public const float COMPLETE_DELAY = 0.1f;    // 完成操作的延遲時間（稍微加長）
        public const float FALL_DELAY = 0.01f;        // 寶石下落的延遲時間（與消除同步）
        public const float SPECIAL_EFFECT_DELAY = 0.5f;  // 新增：特殊寶石效果延遲

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
            matchPredictor = GetComponent<MatchPredictor>();

            // 初始化遊戲的各個組件
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
            InitializeBoard();
        }

        // Update 方法：每幀調用一次
        // 用於執行持續性的遊戲邏輯和性能監控
        private void Update()
        {
            // 更新遊戲指標
            // 包括寶石數量、性能metrics等
            UpdateGameMetrics();
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

                // 重置狀態文字
                if (statusText != null)
                {
                    statusText.text = "遊戲已停止";
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
        public void 嘗試交換寶石一(int x1, int y1, int x2, int y2)
        {
            由玩家觸發生成 = true;
            // 首先驗證移動的有效性
            // 檢查座標是否合法、是否為相鄰寶石等
            if (!ValidateMove(x1, y1, x2, y2)) return;

            // 啟動寶石交換的協程
            // 協程允許執行平滑的動畫和複雜的遊戲邏輯
            StartCoroutine(啟動交換寶石二(x1, y1, x2, y2));
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
        #endregion
        #region 協程方法
        // 寶石交換的協程方法
        // 處理寶石交換的複雜邏輯，包括動畫、安全檢查和匹配處理

        private IEnumerator 啟動交換寶石二(int x1, int y1, int x2, int y2)
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
            執行交換寶石三(x1, y1, x2, y2);

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
                執行交換寶石三(x2, y2, x1, y1);  // 交換回原始位置

                // 執行返回動畫
                StartCoroutine(gem1.AnimateMove(new Vector3(x1, y1, 0), SWAP_DURATION));
                StartCoroutine(gem2.AnimateMove(new Vector3(x2, y2, 0), SWAP_DURATION));

                yield return new WaitForSeconds(SWAP_DURATION);
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
        private IEnumerator 處理配對序列()
        {
            CurrentState = GameState.Processing;

            // 等待一段短暫的延遲，增加視覺效果
            yield return new WaitForSeconds(DESTROY_DELAY);

            // 銷毀匹配的寶石
            刪除寶石();

            // 再次等待
            yield return new WaitForSeconds(DESTROY_DELAY);

            // 等待特殊寶石效果完成
            while (specialGemActivator.isProcessingEffect)
            {
                yield return null;
            }

            yield return StartCoroutine(落下寶石五());

            // 處理可能的連鎖反應
            yield return StartCoroutine(處理連鎖反應());

            // 確保所有動畫都完成
            bool allAnimationsComplete;
            do
            {
                allAnimationsComplete = true;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (gems[x, y] != null && gems[x, y].isAnimating)
                        {
                            allAnimationsComplete = false;
                            break;
                        }
                    }
                }
                if (!allAnimationsComplete)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            } while (!allAnimationsComplete);

            // 確保沒有新的連鎖反應
            if (偵測可消除的寶石組合())
            {
                yield return StartCoroutine(處理配對序列());
                yield break;
            }

            // 所有效果和連鎖反應都完成後，才設為 Ready
            if (CurrentState == GameState.Processing)
            {
                CurrentState = GameState.Ready;
                matchPredictor?.ResetPredictionTimer();
            }
        }
        private void 交換寶石後再檢查四()
        {
            try
            {                
                var swappedGem1 = gem1;
                var swappedGem2 = gem2;
                var processedGemIds = new HashSet<int>();

                if (!IsValidSwapState())
                {
                    Debug.LogWarning($"交換狀態無效: gem1={gem1?.id ?? -1} at ({gem1?.x ?? -1},{gem1?.y ?? -1}), " +
                                    $"gem2={gem2?.id ?? -1} at ({gem2?.x ?? -1},{gem2?.y ?? -1})");
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
                                var (resourceType, isHorizontal, isVertical, _) = MatchUtils.確認特殊寶石類別(matchGroup);
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
                            StartCoroutine(處理配對序列());
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
                            StartCoroutine(處理配對序列());
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
                        StartCoroutine(處理配對序列());
                        hasValidMatch = true;
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
                StartCoroutine(落下寶石五());
            }
        }
        public IEnumerator 落下寶石五()
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
            yield return StartCoroutine(填滿空位六(-1));
        }

        public IEnumerator 填滿空位六(int resType)
        {
            CurrentState = GameState.Filling;
            matchPredictor?.StopTimer();

            // 從下到上，從左到右填充
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (gems[x, y] == null)
                    {
                        // 檢查下方的寶石是否都已經就位
                        bool lowerGemsSettled = true;
                        for (int checkY = 0; checkY < y; checkY++)
                        {
                            if (gems[x, checkY] == null || gems[x, checkY].isAnimating)
                            {
                                lowerGemsSettled = false;
                                break;
                            }
                        }

                        // 只有當下方寶石都就位時才生成新寶石
                        if (lowerGemsSettled)
                        {
                            int dropDelay = 0;
                            生成與掉落寶石七(x, y, ref dropDelay);
                            // 等待這個寶石的動畫完成
                            yield return new WaitForSeconds(FALL_DELAY);
                        }
                        else
                        {
                            // 如果下方寶石未就位，稍後再檢查這個位置
                            x--;
                            yield return new WaitForSeconds(0.1f);
                        }
                    }
                }
            }

            // 等待所有寶石動畫完成
            bool animationsCompleted;
            do
            {
                animationsCompleted = true;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (gems[x, y] != null && gems[x, y].isAnimating)
                        {
                            animationsCompleted = false;
                            break;
                        }
                    }
                }
                if (!animationsCompleted)
                {
                    yield return new WaitForSeconds(0.1f);
                }
            } while (!animationsCompleted);

            yield return 處理連鎖反應();
        }

        private void 生成與掉落寶石七(int x, int y, ref int dropDelay)
        {
            // 使用寶石工廠創建新寶石
            gemFactory.CreateGem(x, y);

            // 設置寶石的初始位置（在遊戲板頂部）
            gems[x, y].transform.position = new Vector3(x, height + 1, 0);

            // 執行寶石下落動畫
            StartCoroutine(gems[x, y].AnimateMove(
                new Vector3(x, y, 0),  // 目標位置
                FALL_DELAY * 2         // 動畫時間
            ));
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

        #endregion        

        // 處理連鎖反應的協程
        // 檢測並處理填充後可能出現的連續匹配
        private IEnumerator 處理連鎖反應()
        {
            // 確保在處理連鎖反應時維持 Processing 狀態
            CurrentState = GameState.Processing;

            yield return new WaitForSeconds(0.1f);

            bool hasChainReaction = false;
            do
            {
                hasChainReaction = false;

                if (偵測可消除的寶石組合())
                {
                    hasChainReaction = true;
                    yield return new WaitForSeconds(DESTROY_DELAY);
                    刪除寶石();
                    yield return new WaitForSeconds(DESTROY_DELAY);

                    // 等待所有寶石的動畫完成
                    bool animationsCompleted;
                    do
                    {
                        animationsCompleted = true;
                        for (int x = 0; x < width; x++)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                if (gems[x, y] != null && gems[x, y].isAnimating)
                                {
                                    animationsCompleted = false;
                                    break;
                                }
                            }
                        }
                        if (!animationsCompleted)
                        {
                            yield return new WaitForSeconds(0.1f);
                        }
                    } while (!animationsCompleted);
                }

            } while (hasChainReaction);

            // 確認特殊寶石效果也完成
            while (specialGemActivator.isProcessingEffect)
            {
                yield return null;
            }

            // 所有效果和動畫都完成後，檢查是否可以切換到 Ready 狀態
            // 只有在沒有其他處理中的操作時才切換狀態
            if (CurrentState == GameState.Processing)
            {
                CurrentState = GameState.Ready;                
                matchPredictor?.ResetPredictionTimer();
            }
        }


        // 填充遊戲板空白位置的協程
        // 負責在消除寶石後重新填滿遊戲板的空白格子

        #region Match Processing
        // 銷毀遊戲板上匹配的寶石
        // 啟動消除匹配寶石的整體流程
        private void 刪除寶石()
        {
            statusText.text = "消除中";
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

        private List<List<Gem>> 尋找連續寶石組別(IGrouping<int, Gem> group)
        {
            return MatchUtils.FindContinuousGemGroups(group);
        }
        // 修改 刪除寶石序列 中的特殊寶石生成邏輯
        private IEnumerator 刪除寶石序列(List<Gem> matchedGems)
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
                yield return new WaitForSeconds(DESTROY_DELAY);
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
                // 檢查是否為特殊寶石組合
                if (gem1?.id >= 100 && gem2?.id >= 100)
                {
                    int originalId1 = gem1.id;
                    var (success, resultType) = MatchUtils.CheckSpecialGemCombination(gem1, gem2);
                    if (success)
                    {
                        gem1.id = resultType;
                        specialGemActivator.啟動特殊寶石(gem1);
                        gem1.id = originalId1;
                        由玩家觸發生成 = false;
                        yield break;
                    }
                }

                // 使用已經設置好的玩家觸發點來生成特殊寶石
                for (int i = 0; i < matchGroups.Count && i < playerTriggerX.Length; i++)
                {
                    var group = matchGroups[i]
                        .Where(g => !processedGemIds.Contains(g.GetInstanceID()))
                        .ToList();

                    if (group.Count < 4) continue;

                    // 使用 MatchUtils.確認特殊寶石類別 來判斷是否可以生成特殊寶石
                    Debug.Log($"確認特殊寶石類別 觸發點XY: ({playerTriggerX[i]}, {playerTriggerY[i]})");
                    var (resourceType, isHorizontal, isVertical, _) = MatchUtils.確認特殊寶石類別(group, playerTriggerX[i], playerTriggerY[i]);

                    if (resourceType != -1)
                    {
                        triggerX = playerTriggerX[i];
                        triggerY = playerTriggerY[i];
                        if (IsValidPosition(triggerX, triggerY))
                        {                            
                            var newGem = 生成特殊寶石(triggerX, triggerY, resourceType);
                            Debug.Log($"生成特殊寶石 create byPlayer: ({triggerX}, {triggerY}),Res: {resourceType} count: {group.Count}");
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

                            Debug.Log($"非玩家操作時 - 隨機位置({triggerX},{triggerY})生成 Type:{resourceType} count:{group.Count}");

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

            yield return StartCoroutine(淡出與刪除寶石(processedGems, processedGemIds));
            yield return new WaitForSeconds(DESTROY_DELAY);
            yield return StartCoroutine(落下寶石五());
        }

        // 寶石淡出效果的協程
        // 提供視覺上平滑的消除動畫
        public IEnumerator 淡出與刪除寶石(HashSet<Gem> gems, HashSet<int> safeIds)
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
        public IEnumerator 消失與刪除一般寶石(List<Gem> gemsToDestroy)
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
            specialGemActivator.收集要被消除的寶石(new HashSet<Gem>(safeGems));

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
                    GameState.Ready => "可以開始交換寶石",       // 遊戲就緒，玩家可以自由操作
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
            if (Time.frameCount % 30 == 0)
            {
                CleanupBoard();
            }
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
            if (timeElapsed >= 0.5f)
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