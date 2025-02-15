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

    #region �ǦC�����
    [Header("UI �]�m")]
    [SerializeField] private Color lockedColor = Color.gray;
    [SerializeField] private Color nextLevelColor = Color.red;
    [SerializeField] private Color unlockedColor = new Color(0, 0.4f,1);
    [SerializeField] private LayerMask targetLayer = 3;
    #endregion

    #region �p���ܼ�
    private const string LEVEL_KEY = "CurrentLevel";
    private Dictionary<string, SpriteRenderer> mapIcons = new Dictionary<string, SpriteRenderer>();
    private TextMeshPro nextLevelText;
    #endregion

    #region ���}�ݩ�
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

    #region Unity�ͩR�g��
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
        if (scene.name == "Map")  // �T�O�u�b Map ��������
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

    #region ��l�Ƥ�k
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

    #region �s�ɬ���
    

    private void SaveLevel()
    {
        PlayerPrefs.SetInt(LEVEL_KEY, currentLevel);
        PlayerPrefs.Save();
    }
    #endregion

    #region UI��s
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

    #region ��J�B�z
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

    #region ���d���J
    private void TryLoadLevel(string levelName)
    {
        if (!int.TryParse(levelName, out int targetLevel))
        {
            Debug.LogWarning($"�L�Ī����d�W��: {levelName}");
            return;
        }

        if (IsLevelAccessible(targetLevel))
        {
            LoadLevelScene(targetLevel);
        }
        else
        {
            Debug.Log($"���d {targetLevel} �|������!");
        }
    }

    private bool IsLevelAccessible(int targetLevel)
    {
        return targetLevel <= currentLevel || targetLevel == currentLevel + 1;
    }

    private void LoadLevelScene(int level)
    {
        Level = level; // �o�|Ĳ�o�O�s
        SceneManager.LoadScene("Level");
    }
    #endregion

    #region ���}��k
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
