using UnityEngine;
using TMPro;
using Match3Game;

public class GameManaager : MonoBehaviour
{
    //public static GameManaager instance; // 靜態實例變數
    [SerializeField] public TextMeshPro levelText;
    [SerializeField] public TextMeshPro targetText;
    [SerializeField] public TextMeshPro roundText;
    [SerializeField] public int level = 1;
    [SerializeField] public int target = 5;
    [SerializeField] public int round = 30;
    SpecialGemActivator specialGemActivator;
    Board board;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        board = Board.instance;
        specialGemActivator = new SpecialGemActivator(Board.instance);
        levelText.text = "";
        roundText.text = "Round:" + round;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyUp(KeyCode.Escape))
        {
            Debug.Log("Reset Board");
            Gem gem = Board.instance.gems[0, 0];
            gem.id = 106;
            specialGemActivator.啟動特殊寶石(gem);
        }
    }
    
    public void UpdateLevel()
    {
        level++;
        levelText.text = "Level: " + level;
    }

    public void UpdateTarget()
    {
        target--;
        targetText.text = "x  " + target;
        if(target == 0)
        {
            board.changeGameState(GameState.Completed);
            levelText.text = "Level: " + level + " Completed!";
        }
    }

    public void updateRound()
    {
        round--;
        roundText.text = "Round:" + round;
        if (round == 0)
        {
            board.changeGameState(GameState.Completed);
            levelText.text = "Level: " + level + " Failed!";
        }
    }
}
