using UnityEngine;
using UnityEngine.UI;
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
        Resetting    // 遊戲板重置中
    }

    // Board 類別是遊戲的核心管理類別
    // 負責管理遊戲板的所有邏輯，包括寶石交換、匹配、消除等
    public class Board : MonoBehaviour
    {
        #region Variables
        // 單例模式：確保遊戲中只有一個遊戲板實例
        public static Board instance;

        // 遊戲板的基本配置參數
        public int width = 8;       // 遊戲板寬度（列數）
        public int height = 8;      // 遊戲板高度（行數）
        public float gemMoveSpeed = 5f;  // 寶石移動速度的基礎倍率

        // 二維數組，用於追蹤遊戲板上每個位置的寶石
        [HideInInspector] public Gem[,] gems;

        // 遊戲邏輯相關的組件引用
        [SerializeField] public MatchPredictor matchPredictor;  // 匹配預測器
        [SerializeField] public Text statusText;  // 顯示遊戲狀態的UI文字
        [SerializeField] public Text gemsText;    // 顯示寶石數量的UI文字

        // 寶石預製體數組
        [SerializeField] private GameObject[] gemPrefabs;       // 普通寶石的預製體
        [SerializeField] private GameObject[] resGemPrefabs;    // 特殊資源寶石的預製體

        // 遊戲狀態管理的關鍵變數
        private GameState currentState = GameState.Ready;  // 當前遊戲狀態
        private bool isSwitching = false;  // 是否正在交換寶石
        private Gem gem1, gem2;  // 當前正在交換的兩個寶石

        // 性能監控相關變數
        private float lastUpdateTime;  // 上次更新性能指標的時間
        private int frameCounter;      // 幀計數器
        private float fps;             // 當前幀率

        // 遊戲出錯標記
        public bool onError = false;

        // 遊戲邏輯輔助類別
        private GemFactory gemFactory;        // 寶石工廠
        private MatchFinder matchFinder;      // 匹配查找器
        public SpecialGemActivator specialGemActivator;  // 特殊寶石激活器
        private BoardResetter boardResetter;  // 遊戲板重置器

        // 遊戲常數：定義各種動畫和操作的持續時間
        public const float SWAP_DURATION = 0.2f;     // 寶石交換動畫持續時間
        public const float DESTROY_DELAY = 0.2f;     // 寶石消除的延遲時間
        public const float COLLECT_DELAY = 0.01f;    // 收集寶石的延遲時間
        public const float COMPLETE_DELAY = 0.01f;   // 完成操作的延遲時間
        private const float FPS_UPDATE_INTERVAL = 0.5f;  // 性能指標更新間隔
        #endregion
        #region Properties
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

        #region Unity Lifecycle
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
        }

        // Start 方法：在第一幀更新之前調用
        // 用於執行遊戲開始時需要進行的初始化邏輯
        public void Start()
        {
            // 初始化遊戲板
            // 設置遊戲板的初始狀態和佈局
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
            // 清理遊戲資源
            // 停止所有協程，銷毀遊戲物件
            CleanupResources();
        }
        #endregion
        #region Initialization
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

        #region Game Logic
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
        public void TrySwapGems(int x1, int y1, int x2, int y2)
        {
            // 首先驗證移動的有效性
            // 檢查座標是否合法、是否為相鄰寶石等
            if (!ValidateMove(x1, y1, x2, y2)) return;

            // 啟動寶石交換的協程
            // 協程允許執行平滑的動畫和複雜的遊戲邏輯
            StartCoroutine(SwapGemsRoutine(x1, y1, x2, y2));
        }
        #endregion
        #region Coroutines
        // 寶石交換的協程方法
        // 處理寶石交換的複雜邏輯，包括動畫、安全檢查和匹配處理
        private IEnumerator SwapGemsRoutine(int x1, int y1, int x2, int y2)
        {
            // 再次驗證移動的安全性
            // 防止非法或不可預期的交換操作
            if (!ValidateMove(x1, y1, x2, y2))
            {
                // 記錄無效的交換嘗試，幫助除錯
                Debug.LogWarning($"無效的交換操作：從 ({x1},{y1}) 到 ({x2},{y2})");
                // 立即結束協程
                yield break;
            }

            // 設置遊戲狀態為交換中
            // 防止在交換過程中進行其他操作
            CurrentState = GameState.Swapping;
            isSwitching = true;

            // 從遊戲板陣列中獲取要交換的兩個寶石
            gem1 = gems[x1, y1];
            gem2 = gems[x2, y2];

            // 安全性檢查：確保兩個寶石都存在
            if (gem1 == null || gem2 == null)
            {
                // 記錄空寶石的警告信息
                Debug.LogWarning($"嘗試交換的寶石為空：gem1 @ ({x1},{y1}), gem2 @ ({x2},{y2})");
                // 重置交換狀態
                isSwitching = false;
                CurrentState = GameState.Ready;
                // 結束協程
                yield break;
            }

            // 更新遊戲板陣列中的寶石位置
            // 交換寶石在陣列中的實際位置
            gems[x1, y1] = gem2;
            gems[x2, y2] = gem1;

            // 計算寶石移動的目標位置
            var pos1 = new Vector3(x2, y2, 0);
            var pos2 = new Vector3(x1, y1, 0);

            // 根據遊戲移動速度計算交換動畫持續時間
            float swapDuration = SWAP_DURATION / gemMoveSpeed;

            // 執行寶石移動動畫
            // 使用協程確保動畫平滑且同步
            StartCoroutine(gem1.AnimateMove(pos1, swapDuration));
            StartCoroutine(gem2.AnimateMove(pos2, swapDuration));

            // 等待動畫完成
            yield return new WaitForSeconds(swapDuration);

            // 更新寶石的座標屬性
            gem1.x = x2;
            gem1.y = y2;
            gem2.x = x1;
            gem2.y = y1;

            // 處理交換後的匹配邏輯
            ProcessMatchesAfterSwap();

            // 重置動畫和交換狀態
            gem1.isAnimating = false;
            gem2.isAnimating = false;
            isSwitching = false;
            CurrentState = GameState.Ready;

            // 重置匹配預測計時器
            matchPredictor?.ResetPredictionTimer();
        }

        // 處理寶石交換後的匹配邏輯
        // 檢查交換是否產生匹配，處理特殊寶石等情況
        private void ProcessMatchesAfterSwap()
        {
            if (gem1 == null || gem2 == null)
            {
                Debug.LogWarning("嘗試處理匹配時，寶石為空");
                onError = true;
                StartCoroutine(MakeGemsFall());
                return;
            }

            // 確定移動方向
            bool isHorizontalMove = gem1.y == gem2.y;
            int triggerX = isHorizontalMove ? Math.Min(gem1.x, gem2.x) : gem1.x;
            int triggerY = isHorizontalMove ? gem1.y : Math.Min(gem1.y, gem2.y);

            bool hasMatches = CheckForMatches();
            if (hasMatches)
            {
                // 尋找匹配組
                var matchGroups = FindMatchGroups(isHorizontalMove, triggerX, triggerY);

                // 處理每個匹配組
                foreach (var group in matchGroups)
                {
                    // 使用新的整合方法
                    var (resourceType, isHorizontal, isVertical, matchedGems) = DetectMatchAndDetermineResourceType(group);

                    // 如果決定了有效的資源寶石類型，則創建資源寶石
                    if (resourceType != -1)
                    {
                        ProcessResourceGemCreation(matchedGems, true, triggerX, triggerY, resourceType);
                    }

                    // 記錄匹配信息，用於調試
                    Debug.Log($"匹配組：數量={matchedGems.Count}, 水平={isHorizontal}, 垂直={isVertical}, 資源類型={resourceType}");
                }

                // 啟動匹配處理序列
                StartCoroutine(ProcessMatchSequence());
            }

            // 處理特殊寶石（ID 103）的情況
            if (gem1.id == 103 || gem2.id == 103)
            {
                specialGemActivator.ActivateSpecialGem(gem1.id == 103 ? gem1 : gem2);
            }
            // 處理其他特殊寶石（ID >= 100）的情況
            else if (gem1.id >= 100 || gem2.id >= 100)
            {
                specialGemActivator.ActivateSpecialGem(gem1.id >= 100 ? gem1 : gem2);
            }
            // 如果沒有匹配，將寶石換回原位
            else if (!hasMatches)
            {
                SwapBack();
            }
        }
        // 輔助方法：找出連續的同ID寶石組
        // 尋找連續的同ID寶石組的輔助方法
        // 尋找連續的同ID寶石組的輔助方法，支持更複雜的匹配檢測
        private List<List<Gem>> FindMatchGroups(bool isHorizontalMove, int triggerX, int triggerY)
        {
            var matchGroups = new List<List<Gem>>();

            // 篩選出所有同ID的寶石
            var potentialMatches = gems
                .Cast<Gem>()
                .Where(g => g != null && g.id == gem1.id)
                .ToList();

            // 檢查水平和垂直方向的匹配
            var horizontalGroup = potentialMatches
                .Where(g => g.y == triggerY)
                .OrderBy(g => g.x)
                .ToList();

            var verticalGroup = potentialMatches
                .Where(g => g.x == triggerX)
                .OrderBy(g => g.y)
                .ToList();

            // 分別檢查水平和垂直方向的連續匹配
            List<Gem> horizontalConsecutive = FindConsecutiveGems(horizontalGroup, true);
            List<Gem> verticalConsecutive = FindConsecutiveGems(verticalGroup, false);

            // 根據匹配數量加入匹配組
            if (horizontalConsecutive.Count >= 4)
                matchGroups.Add(horizontalConsecutive);

            if (verticalConsecutive.Count >= 4)
                matchGroups.Add(verticalConsecutive);

            return matchGroups;
        }
        // 查找連續寶石的輔助方法
        private List<Gem> FindConsecutiveGems(List<Gem> gems, bool isHorizontal)
        {
            if (gems.Count < 4) return new List<Gem>();

            var consecutiveGroups = new List<List<Gem>>();
            var currentGroup = new List<Gem> { gems[0] };

            for (int i = 1; i < gems.Count; i++)
            {
                bool isConsecutive = isHorizontal
                    ? (gems[i].x - gems[i - 1].x == 1)
                    : (gems[i].y - gems[i - 1].y == 1);

                if (isConsecutive)
                {
                    currentGroup.Add(gems[i]);
                }
                else
                {
                    if (currentGroup.Count >= 4)
                        consecutiveGroups.Add(currentGroup);

                    currentGroup = new List<Gem> { gems[i] };
                }
            }

            // 檢查最後一組
            if (currentGroup.Count >= 4)
                consecutiveGroups.Add(currentGroup);

            // 返回最長的連續組
            return consecutiveGroups.OrderByDescending(g => g.Count).FirstOrDefault() ?? new List<Gem>();
        }
        // 檢查遊戲板上是否存在寶石匹配
        // 這是遊戲核心邏輯的關鍵方法，用於偵測可消除的寶石組合
        public bool CheckForMatches()
        {
            // 特殊寶石（ID >= 100）不進行匹配檢查
            // 這是為了防止特殊寶石意外觸發消除邏輯
            if (isSwitching && (gem1.id >= 100 || gem2.id >= 100))
                return false;

            // 使用匹配查找器尋找所有可能的匹配
            var matches = matchFinder.FindAllMatches();

            // 遍歷所有匹配的寶石組
            foreach (var match in matches)
            {
                foreach (var gem in match.matchedGems)
                {
                    // 安全性檢查：確保寶石不為空
                    // 標記可以被消除的寶石
                    if (gem != null)
                    {
                        gem.isMatched = true;
                    }
                }
            }

            // 返回是否找到匹配（匹配組的數量大於0）
            return matches.Count > 0;
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
        private IEnumerator ProcessMatchSequence()
        {
            // 等待一段短暫的延遲，增加視覺效果
            yield return new WaitForSeconds(DESTROY_DELAY);

            // 銷毀匹配的寶石
            // 傳入是否由玩家互動觸發，以及觸發點的座標
            DestroyMatches(true, gem1.x, gem1.y);

            // 再次等待，控制消除的節奏
            yield return new WaitForSeconds(DESTROY_DELAY);

            // 處理特殊寶石（ID >= 100）的情況
            if (gem1.id >= 100 || gem2.id >= 100)
            {
                // 激活特殊寶石的特殊效果
                specialGemActivator.ActivateSpecialGem(gem1.id >= 100 ? gem1 : gem2);
            }
        }
        #endregion
        // 當交換的寶石沒有匹配時，將寶石換回原位
        private void SwapBack()
        {
            // 安全性檢查：確保交換的寶石存在且有效
            if (gem1 == null || gem2 == null ||
                gem1.gameObject == null || gem2.gameObject == null)
            {
                return;
            }

            // 在遊戲板陣列中交換回原來的位置
            var tempGem = gems[gem1.x, gem1.y];
            gems[gem1.x, gem1.y] = gems[gem2.x, gem2.y];
            gems[gem2.x, gem2.y] = tempGem;

            // 檢查是否可以啟動返回動畫
            if (gem1 != null && gem1.gameObject != null &&
                gem2 != null && gem2.gameObject != null)
            {
                // 執行寶石返回原位的動畫
                StartCoroutine(gem1.AnimateMove(new Vector3(gem2.x, gem2.y, 0), SWAP_DURATION / gemMoveSpeed));
                StartCoroutine(gem2.AnimateMove(new Vector3(gem1.x, gem1.y, 0), SWAP_DURATION / gemMoveSpeed));
            }
        }
        // 填充遊戲板空白位置的協程
        // 負責在消除寶石後重新填滿遊戲板的空白格子
        public IEnumerator FillEmptySpaces()
        {
            // 設置遊戲狀態為填充中
            // 防止玩家在填充過程中進行其他操作
            CurrentState = GameState.Filling;

            // 暫停匹配預測計時器
            // 避免在填充過程中進行不必要的預測
            matchPredictor?.StopTimer();

            // 遍歷遊戲板的每一列
            for (int x = 0; x < width; x++)
            {
                // 記錄掉落延遲，用於創建視覺上不同步的掉落效果
                int dropDelay = 0;

                // 遍歷每一行
                for (int y = 0; y < height; y++)
                {
                    // 檢查當前位置是否為空，且上方沒有懸空的寶石
                    if (gems[x, y] == null && !HasFloatingGemsAbove(x, y))
                    {
                        // 創建並掉落新寶石
                        CreateAndDropGem(x, y, ref dropDelay);

                        // 短暫等待，增加寶石掉落的視覺效果
                        yield return new WaitForSeconds(0.1f);
                    }
                }
            }

            // 處理可能的連鎖反應
            // 檢查填充後是否會產生新的匹配
            yield return ProcessChainReactions();
        }

        // 處理連鎖反應的協程
        // 檢測並處理填充後可能出現的連續匹配
        private IEnumerator ProcessChainReactions()
        {
            // 短暫等待，確保視覺效果的流暢性
            yield return new WaitForSeconds(0.1f);

            // 持續檢查是否存在匹配
            while (CheckForMatches())
            {
                // 等待消除延遲
                yield return new WaitForSeconds(DESTROY_DELAY);

                // 銷毀匹配的寶石（非互動觸發）
                DestroyMatches(false);

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
        private bool HasFloatingGemsAbove(int x, int y)
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
        private void CreateAndDropGem(int x, int y, ref int dropDelay)
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
                0.3f / gemMoveSpeed     // 動畫持續時間
            ));

            // 增加掉落延遲，使多個寶石有不同的掉落間隔
            dropDelay++;
        }
        // 填充遊戲板空白位置的協程
        // 負責在消除寶石後重新填滿遊戲板的空白格子
        
        #region Match Processing
        // 銷毀遊戲板上匹配的寶石
        // 啟動消除匹配寶石的整體流程
        private void DestroyMatches(bool isFromInteraction = false, int interactX = -1, int interactY = -1)
        {
            // 使用匹配查找器尋找所有可消除的寶石組
            var matches = matchFinder.FindAllMatches();

            // 將所有匹配的寶石展平成一個列表
            // 使用 LINQ 的 SelectMany 方法合併所有匹配組中的寶石
            List<Gem> allMatchedGems = matches.SelectMany(m => m.matchedGems).ToList();

            // 啟動銷毀匹配寶石的協程
            // 傳入匹配的寶石列表和觸發消除的相關信息
            StartCoroutine(DestroyMatchesSequence(allMatchedGems, isFromInteraction, interactX, interactY));
        }

        // 銷毀匹配寶石的序列協程
        // 處理匹配消除的複雜邏輯，包括特殊寶石生成
        private IEnumerator DestroyMatchesSequence(List<Gem> matchedGems, bool isFromInteraction, int interactX, int interactY)
        {
            // 用於追蹤已處理的寶石，防止重複處理
            HashSet<Gem> processedGems = new HashSet<Gem>();

            // 用於按寶石類型分組匹配的寶石
            // 方便後續特殊寶石的生成邏輯
            Dictionary<int, List<Gem>> matchGroups = new Dictionary<int, List<Gem>>();

            // 遍歷所有匹配的寶石
            foreach (var gem in matchedGems)
            {
                // 確保寶石有效且未被處理
                if (gem != null && !processedGems.Contains(gem) && gems[gem.x, gem.y] == gem)
                {
                    // 標記為已處理
                    processedGems.Add(gem);

                    // 從遊戲板陣列中移除
                    gems[gem.x, gem.y] = null;

                    // 按寶石ID分組
                    // 便於後續根據匹配特徵生成特殊寶石
                    if (!matchGroups.ContainsKey(gem.id))
                    {
                        matchGroups[gem.id] = new List<Gem>();
                    }
                    matchGroups[gem.id].Add(gem);
                }
            }

            // 執行寶石淡出效果
            // 提供視覺上平滑的消除體驗
            yield return StartCoroutine(FadeOutGems(processedGems));

            // 等待銷毀延遲
            // 控制消除的節奏感
            yield return new WaitForSeconds(DESTROY_DELAY);

            // 對於大量匹配的寶石組，生成特殊資源寶石
            foreach (var group in matchGroups)
            {
                var gems = group.Value;
                if (gems.Count >= 4)
                {
                    var (resourceType, isHorizontal, isVertical, _) = DetectMatchAndDetermineResourceType(gems);
                    ProcessResourceGemCreation(gems, isFromInteraction, interactX, interactY, resourceType);
                }
            }

            // 觸發寶石下落，填補空白位置
            yield return StartCoroutine(MakeGemsFall());
        }

        // 寶石淡出效果的協程
        // 提供視覺上平滑的消除動畫
        private IEnumerator FadeOutGems(HashSet<Gem> gems)
        {
            // 準備淡出效果的相關數據
            // 收集每個寶石的渲染器和初始顏色
            var fadeData = gems.Select(gem => (
                gem,
                renderer: gem.GetComponent<SpriteRenderer>(),
                startColor: gem.GetComponent<SpriteRenderer>().color
            )).ToList();

            // 逐漸降低透明度
            float alpha = 1f;
            while (alpha > 0)
            {
                // 根據遊戲速度計算透明度遞減速率
                alpha -= Time.deltaTime * gemMoveSpeed * 3f;

                // 安全地更新每個寶石的顏色透明度
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
                yield return null;  // 等待下一幀
            }

            // 銷毀已完成淡出的寶石遊戲物件
            foreach (var (gem, _, _) in fadeData)
            {
                if (gem != null && gem.gameObject != null)
                {
                    Destroy(gem.gameObject);
                }
            }
        }
        // 處理特殊資源寶石的創建邏輯
        // 根據匹配的寶石特徵決定是否及如何生成特殊寶石
        private void ProcessResourceGemCreation(List<Gem> gems, bool isFromInteraction, int interactX, int interactY, int resourceType)
        {
            if (resourceType == -1) return; // 如果沒有有效的資源寶石類型，直接返回

            int createX, createY;
            if (isFromInteraction)
            {
                createX = interactX;
                createY = interactY;
            }
            else
            {
                (createX, createY) = CalculateResourceGemPosition(gems);
            }

            if (IsValidPosition(createX, createY) && this.gems[createX, createY] == null)
            {
                CreateResourceGem(createX, createY, resourceType);
            }
            else
            {
                Debug.LogWarning($"無法在 ({createX}, {createY}) 創建資源寶石。位置無效或已被佔用。");
            }
        }
        private (int x, int y) CalculateResourceGemPosition(List<Gem> gems)
        {
            if (gems == null || gems.Count == 0)
                return (-1, -1);

            // 計算所有寶石的平均位置
            float avgX = (float)gems.Average(g => g.x);
            float avgY = (float)gems.Average(g => g.y);

            // 找到最接近平均位置的寶石
            var closestGem = gems.OrderBy(g =>
                Math.Pow(g.x - avgX, 2) + Math.Pow(g.y - avgY, 2)).First();

            return (closestGem.x, closestGem.y);
        }
        // 計算特殊資源寶石的最佳創建位置
        // 邏輯：在匹配的寶石群中找一個最中間的位置
        private (int x, int y) CalculateResourceGemPosition(List<Gem> gems, bool isHorizontal, bool isVertical)
        {
            if (isHorizontal)
            {
                // 水平匹配：在 x 座標中間位置創建
                // 首先按 x 座標排序，然後選取中間的寶石
                var orderedGems = gems.OrderBy(g => g.x).ToList();
                return (orderedGems[orderedGems.Count / 2].x, gems[0].y);
            }
            else if (isVertical)
            {
                // 垂直匹配：在 y 座標中間位置創建
                // 首先按 y 座標排序，然後選取中間的寶石
                var orderedGems = gems.OrderBy(g => g.y).ToList();
                return (gems[0].x, orderedGems[orderedGems.Count / 2].y);
            }
            else
            {
                // 如果既不是水平也不是垂直匹配（理論上不太可能），
                // 則在第一個寶石的位置創建
                return (gems[0].x, gems[0].y);
            }
        }
        private (int resourceType, bool isHorizontal, bool isVertical, List<Gem> matchedGems) DetectMatchAndDetermineResourceType(List<Gem> gems)
        {
            if (gems.Count < 2)
                return (-1, false, false, new List<Gem>());

            // 按 x 和 y 座標對寶石進行分組
            var groupedByX = gems.GroupBy(g => g.x).Where(g => g.Count() >= 2).ToList();
            var groupedByY = gems.GroupBy(g => g.y).Where(g => g.Count() >= 2).ToList();

            bool isHorizontal = groupedByY.Any();
            bool isVertical = groupedByX.Any();

            // 檢查是否為轉角型匹配（L型或T型）
            bool isCornerMatch = false;
            if (gems.Count >= 5)
            {
                var uniqueX = gems.Select(g => g.x).Distinct().ToList();
                var uniqueY = gems.Select(g => g.y).Distinct().ToList();
                isCornerMatch = uniqueX.Count > 1 && uniqueY.Count > 1 &&
                                groupedByX.Count > 1 && groupedByY.Count > 1;
            }

            // 如果是轉角型匹配，同時設定水平和垂直為真
            if (isHorizontal && isVertical)
            {
                isCornerMatch = true;
            }
            //if (isCornerMatch)
            //{
            //    isHorizontal = true;
            //    isVertical = true;
            //}

            // 決定資源寶石類型
            int resourceType;
            if (isCornerMatch && gems.Count >= 5)
            {
                resourceType = 2; // Cross 類型 Bomb
            }
            else if ((isHorizontal || isVertical) && gems.Count >= 5)
            {
                resourceType = 3; // 特殊清除線寶石 B
            }
            else if (isHorizontal)
            {
                resourceType = 0; // 普通橫向清除線寶石 A
            }
            else if (isVertical)
            {
                resourceType = 1; // 普通直向清除線寶石 A
            }
            else
            {
                resourceType = -1; // 無特殊寶石
            }

            // 記錄檢測結果以便除錯
            Debug.Log($"匹配檢測：寶石數={gems.Count}, 水平={isHorizontal}, 垂直={isVertical}, 轉角={isCornerMatch}, 資源類型={resourceType}");

            return (resourceType, isHorizontal, isVertical, gems);
        }
        // 創建特殊資源寶石的方法
        // 實際生成特殊寶石遊戲物件的邏輯
        private void CreateResourceGem(int x, int y, int resType)
        {
            // 使用預先定義的特殊寶石預製體實例化
            GameObject gemObj = Instantiate(resGemPrefabs[resType], transform);

            // 獲取寶石組件
            Gem gem = gemObj.GetComponent<Gem>();

            // 初始化寶石
            // resType 加上 100 作為特殊寶石的唯一標識
            gem.Init(100 + resType, x, y);

            // 將新創建的特殊寶石放置到遊戲板陣列中
            gems[x, y] = gem;
        }
        // 使寶石下落並填補空白位置的協程
        // 這是遊戲板動態重組的核心邏輯
        private IEnumerator MakeGemsFall()
        {
            // 標記是否有寶石正在下落的變數
            bool hasFalling;

            do
            {
                // 重置下落標記
                hasFalling = false;
                // 追蹤正在執行動畫的寶石數量
                int animatingGems = 0;

                // 遍歷遊戲板的每一列
                for (int x = 0; x < width; x++)
                {
                    // 遍歷每一行，從下往上檢查
                    for (int y = 0; y < height - 1; y++)
                    {
                        // 如果當前位置為空，且上方有寶石
                        if (gems[x, y] == null && gems[x, y + 1] != null)
                        {
                            // 獲取上方的寶石
                            var gem = gems[x, y + 1];

                            // 再次確認寶石的有效性
                            if (gem != null)
                            {
                                // 將寶石移動到空白位置
                                gems[x, y] = gem;
                                gems[x, y + 1] = null;

                                // 更新寶石的 y 座標
                                gem.y = y;

                                // 增加動畫中的寶石計數
                                animatingGems++;

                                // 啟動寶石下落協程
                                // 並在完成時遞減動畫寶石計數
                                StartCoroutine(WaitForGemFall(gem, x, y, () => animatingGems--));

                                // 標記有寶石下落
                                hasFalling = true;
                            }
                        }
                    }
                }

                // 如果有寶石正在下落，等待所有動畫完成
                if (hasFalling)
                {
                    while (animatingGems > 0)
                    {
                        yield return new WaitForSeconds(0.05f);
                    }
                }
            }
            // 如果有寶石下落，則重複整個過程
            // 確保所有可能下落的寶石都被移動
            while (hasFalling);

            // 填充剩餘的空白位置
            yield return StartCoroutine(FillEmptySpaces());
        }

        // 等待單個寶石下落完成的協程
        // 控制個別寶石的下落動畫和回調
        private IEnumerator WaitForGemFall(Gem gem, int x, int y, System.Action onComplete)
        {
            // 執行寶石移動動畫
            // 使用協程確保動畫平滑進行
            yield return StartCoroutine(gem.AnimateMove(
                new Vector3(x, y, 0),  // 目標位置
                0.3f / gemMoveSpeed     // 動畫持續時間，與遊戲速度關聯
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
        public IEnumerator FadeAndDestroyGems(List<Gem> gemsToDestroy)
        {
            // 安全性檢查：如果傳入的寶石列表為空，直接結束協程
            if (gemsToDestroy == null || gemsToDestroy.Count == 0) yield break;

            // 創建一個安全的寶石列表，過濾掉無效或已損壞的寶石
            // 這一步確保只處理可用的遊戲物件，防止運行時錯誤
            var safeGems = new List<Gem>();
            foreach (var gem in gemsToDestroy)
            {
                // 多重安全檢查：
                // 1. 寶石不為空
                // 2. 遊戲物件存在
                // 3. 能成功獲取精靈渲染器
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
                            // 記錄渲染器遺失的警告，幫助追蹤潛在問題
                            Debug.LogWarning($"寶石 ({gem.x}, {gem.y}) 的 SpriteRenderer 已遺失");
                        }
                    }
                    catch (Exception e)
                    {
                        // 捕獲並記錄任何意外的異常，提供除錯資訊
                        Debug.LogError($"處理寶石時發生異常: {e.Message}");
                    }
                }
            }

            // 再次檢查：如果沒有有效的寶石，結束協程
            if (safeGems.Count == 0) yield break;

            // 從遊戲板陣列中移除即將被銷毀的寶石
            // 這一步防止被移除的寶石在後續邏輯中被誤用
            foreach (var gem in safeGems)
            {
                if (gem != null && IsValidGemPosition(gem))
                {
                    gems[gem.x, gem.y] = null;
                }
            }

            // 準備淡出效果的相關數據
            // 收集每個寶石的渲染器和初始顏色，為動畫做準備
            var fadeData = safeGems
                .Select(gem => (
                    Gem: gem,
                    Renderer: gem.GetComponent<SpriteRenderer>(),
                    StartColor: gem.GetComponent<SpriteRenderer>().color
                ))
                .ToList();

            // 執行淡出動畫
            // 通過逐漸降低透明度來實現漸變消失效果
            float alpha = 1f;
            while (alpha > 0)
            {
                // 根據遊戲速度計算透明度遞減速率
                alpha -= Time.deltaTime * gemMoveSpeed * 3f;

                // 安全地更新每個寶石的顏色透明度
                foreach (var (gem, renderer, startColor) in fadeData)
                {
                    if (gem != null && gem.gameObject != null && renderer != null)
                    {
                        renderer.color = new Color(
                            startColor.r,
                            startColor.g,
                            startColor.b,
                            Mathf.Clamp01(alpha)  // 確保透明度在0-1之間
                        );
                    }
                }
                yield return null;  // 等待下一幀
            }

            // 銷毀已完成淡出的寶石遊戲物件
            foreach (var (gem, _, _) in fadeData)
            {
                if (gem != null && gem.gameObject != null)
                {
                    Destroy(gem.gameObject);
                }
            }

            // 觸發寶石下落，填補空白位置
            yield return StartCoroutine(MakeGemsFall());
        }

        // 輔助方法：驗證寶石位置是否有效
        // 確保寶石的座標在遊戲板的合法範圍內
        private bool IsValidGemPosition(Gem gem)
        {
            // 檢查條件：
            // 1. 寶石不為空
            // 2. x座標在遊戲板寬度範圍內
            // 3. y座標在遊戲板高度範圍內
            return gem != null &&
                   gem.x >= 0 && gem.x < width &&
                   gem.y >= 0 && gem.y < height;
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
            if (Time.frameCount % 300 == 0)
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
            // 遍歷遊戲板上的所有子物件
            // 這包括遊戲板上的所有寶石和其他遊戲物件
            foreach (Transform child in transform)
            {
                // 嘗試獲取寶石組件
                // 確定這個子物件是否是一個有效的寶石
                Gem gemComponent = child.GetComponent<Gem>();

                // 如果找到寶石組件，並且該寶石在遊戲板上已經無效
                // 將其從遊戲場景中移除
                if (gemComponent != null && !IsGemValid(gemComponent))
                {
                    // 銷毀無效的遊戲物件
                    // 釋放佔用的記憶體和資源
                    Destroy(child.gameObject);
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