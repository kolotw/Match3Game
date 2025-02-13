using Match3Game;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityEngine.UI.Image;

public class ButtonActions : MonoBehaviour
{
    Board board;
    SpecialGemActivator specialGemActivator;
    LevelManager levelManager;
    private TextMeshPro btnText;
    Sprite lvCompleted;
    public LayerMask targetLayer = 3;
    void Start()
    {
        board = Board.instance;
        levelManager = LevelManager.Instance;
        specialGemActivator = new SpecialGemActivator(Board.instance);
        try 
        {
            btnText = GameObject.Find("/Result_Panel/BTN_Next/Text (TMP)").GetComponent<TextMeshPro>();
        } catch { }
        try {
            lvCompleted = GameObject.Find("/LevelCompleted").GetComponent<SpriteRenderer>().sprite;
        } catch { }
        mapIcon();
    }
    void mapIcon()
    {
        GameObject[] maps = GameObject.FindGameObjectsWithTag("mapIcons");
        maps.Distinct();
        int level = LevelManager.Instance.Level;
        foreach(GameObject icon in maps)
        {
            if(int.Parse( icon.name) > level)
            {
                icon.GetComponent<SpriteRenderer>().color = Color.gray;
                if (int.Parse(icon.name) == level + 1)
                {
                    icon.GetComponent<SpriteRenderer>().color = Color.red;
                }
            }
        }
    }
   
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // �˴��ƹ���m�O�_���I����
            Collider2D hit = Physics2D.OverlapPoint(mousePos);

            if (hit != null)
            {
                Debug.Log("�I����: " + hit.gameObject.name);

                if (hit.CompareTag("mapIcons"))
                {
                    selectLevel(hit.gameObject.name);
                }
                else if(hit.name == "X_Close")
                {
                    SceneManager.LoadScene("MainMenu");
                }
            }
        }
    }
    void selectLevel(string lvName)
    {
        int targetLevel;
        if (!int.TryParse(lvName, out targetLevel))
        {
            //Debug.LogError("Invalid level name format");
            return;
        }

        // �p�G�ؼ����d�O�U�@���Τw���ꪺ���d�~���\���J
        if (targetLevel <= levelManager.Level || targetLevel == levelManager.Level + 1)
        {
            levelManager.Level = int.Parse(lvName);
            SceneManager.LoadScene("Level");
        }
    }
    //private void OnMouseUpAsButton()
    //{
    //    if (board!=null)
    //    {
    //        if(board.currentState == GameState.Completed)
    //        {
    //            if (btnText.text == "Next")
    //            {
    //                //levelManager.Level++;
    //                levelManager.SaveLevel();
    //                levelManager.UnlockNextLevel();
    //                SceneManager.LoadScene("Level");
    //            }
    //            else if (btnText.text == "Back")
    //            {
    //                SceneManager.LoadScene("Map");
    //            }
    //            else
    //            {
    //                SceneManager.LoadScene("MainMenu");
    //            }                
    //        }
    //        else
    //        {
    //            SceneManager.LoadScene("Map");
    //            //board.changeGameState(GameState.Ready);
    //            //Gem gem = Board.instance.gems[0, 0];
    //            //gem.id = 106;
    //            //specialGemActivator.�ҰʯS���_��(gem);
    //        }            
    //    }
    //}
}
