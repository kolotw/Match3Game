using Match3Game;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonActions : MonoBehaviour
{
    Board board;
    SpecialGemActivator specialGemActivator;
    LevelMapManager levelManager;
    void Start()
    {
        board = Board.instance;
        levelManager = LevelMapManager.instance;
        specialGemActivator = new SpecialGemActivator(Board.instance);
    }
    private void OnMouseUpAsButton()
    {
        if (board!=null)
        {
            if(board.currentState == GameState.Completed)
            {
                if (gameObject.name == "BTN_Next")
                {
                    levelManager.Level++;
                    SceneManager.LoadScene("Level");
                }
                else
                {
                    SceneManager.LoadScene("MainMenu");
                }                
            }
            else
            {
                board.changeGameState(GameState.Ready);
                Gem gem = Board.instance.gems[0, 0];
                gem.id = 106;
                specialGemActivator.±Ò°Ê¯S®íÄ_¥Û(gem);
            }
            
        }        
    }
}
