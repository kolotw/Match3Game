using System.Collections.Generic;
using System.Linq;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelManager : MonoBehaviour
{
    #region Singleton
    private static LevelManager instance;
    public static LevelManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<LevelManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("LevelManager");
                    instance = go.AddComponent<LevelManager>();
                }
            }
            return instance;
        }
    }
    #endregion

    #region 序列化欄位
    [Header("UI 設置")]
    [SerializeField] private Color lockedColor = Color.gray;
    [SerializeField] private Color nextLevelColor = Color.red;
    [SerializeField] private Color unlockedColor = new Color(0, 0.4f,1);
    [SerializeField] private LayerMask targetLayer = 3;
    #endregion

    #region 私有變數
    private const string LEVEL_KEY = "CurrentLevel";
    private Dictionary<string, SpriteRenderer> mapIcons = new Dictionary<string, SpriteRenderer>();
    private TextMeshPro nextLevelText;
    #endregion

    #region 公開屬性
    [SerializeField] private int currentLevel;
    public int Level
    {
        get => currentLevel;
        set
        {
            currentLevel = value;
            SaveLevel();
            UpdateMapIconsVisuals();
        }
    }
    #endregion

    #region Unity生命週期
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        //LoadLevel();
    }

    private void Start()
    {
        InitializeReferences();
        InitializeMapIcons();
        UpdateMapIconsVisuals();
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "Map")  // 確保只在 Map 場景執行
        {
            InitializeReferences();
            InitializeMapIcons();
            UpdateMapIconsVisuals();
        }
    }
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }
    #endregion

    #region 初始化方法
    private void InitializeReferences()
    {
        try
        {
            nextLevelText = GameObject.Find("Result_Panel/BTN_Next/Text (TMP)")?.GetComponent<TextMeshPro>();            
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Some UI elements not found: {e.Message}");
        }
    }

    private void InitializeMapIcons()
    {
        mapIcons.Clear();
        var icons = GameObject.FindGameObjectsWithTag("mapIcons").Distinct();
        foreach (var icon in icons)
        {
            if (icon.TryGetComponent<SpriteRenderer>(out var renderer))
            {
                mapIcons[icon.name] = renderer;
            }
        }
    }
    #endregion

    #region 存檔相關
    

    private void SaveLevel()
    {
        PlayerPrefs.SetInt(LEVEL_KEY, currentLevel);
        PlayerPrefs.Save();
    }
    #endregion

    #region UI更新
    private void UpdateMapIconsVisuals()
    {
        try {
            foreach (var iconPair in mapIcons)
            {
                if (int.TryParse(iconPair.Key, out int iconLevel))
                {
                    Color targetColor = GetIconColor(iconLevel, currentLevel);
                    iconPair.Value.color = targetColor;
                }
            }
        } catch { }        
    }

    private Color GetIconColor(int iconLevel, int currentLevel)
    {
        if (iconLevel <= currentLevel)
            return unlockedColor;
        //if (iconLevel == currentLevel + 1)
        //    return nextLevelColor;
        return lockedColor;
    }
    #endregion

    #region 輸入處理
    private void HandleMouseClick()
    {
        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit = Physics2D.OverlapPoint(mousePos, targetLayer);
        
        if (hit == null) return;
        Debug.Log($"hit name: {hit.name}");

        if (hit.CompareTag("mapIcons"))
        {
            TryLoadLevel(hit.gameObject.name);
        }
        else if (hit.name == "X_Close")
        {
            SceneManager.LoadScene("MainMenu");
        }
        else if (hit.CompareTag("button"))
        {
            if (hit.name == "BTN_Next")
            {
                nextLevelText = GameObject.Find("/Result_Panel/BTN_Next/Text (TMP)").GetComponent<TextMeshPro>();
                
                if (nextLevelText.text == "Next")
                {
                    SaveLevel();
                    UnlockNextLevel();
                    //LoadLevel();
                    SceneManager.LoadScene("Map");
                }
                else if (nextLevelText.text == "Back")
                {
                    currentLevel--;
                    SceneManager.LoadScene("Map");
                }
            }
        }
        
    }
    #endregion

    #region 關卡載入
    private void TryLoadLevel(string levelName)
    {
        if (!int.TryParse(levelName, out int targetLevel))
        {
            Debug.LogWarning($"無效的關卡名稱: {levelName}");
            return;
        }

        if (IsLevelAccessible(targetLevel))
        {
            LoadLevelScene(targetLevel);
        }
        else
        {
            Debug.Log($"關卡 {targetLevel} 尚未解鎖!");
        }
    }

    private bool IsLevelAccessible(int targetLevel)
    {
        return targetLevel <= currentLevel || targetLevel == currentLevel + 1;
    }

    private void LoadLevelScene(int level)
    {
        Level = level; // 這會觸發保存
        SceneManager.LoadScene("Level");
    }
    #endregion

    #region 公開方法
    public void UnlockNextLevel()
    {
        Level++;
    }
    public void LoadLevel()
    {
        currentLevel = PlayerPrefs.GetInt(LEVEL_KEY, 0);
        SceneManager.LoadScene("Level");
    }
    public void LoadMap()
    {
        SceneManager.LoadScene("Map");
    }
    #endregion
}
