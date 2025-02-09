using Match3Game;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonActions : MonoBehaviour
{
    Board board;
    SpecialGemActivator specialGemActivator;

    void Start()
    {
        board = Board.instance;
        specialGemActivator = new SpecialGemActivator(Board.instance);
    }
    private void OnMouseUpAsButton()
    {
        if (board!=null)
        {
            if(board.currentState == GameState.Completed)
            {
                SceneManager.LoadScene("MainMenu");
            }
            board.changeGameState(GameState.Ready);
            Gem gem = Board.instance.gems[0, 0];
            gem.id = 106;
            specialGemActivator.±Ò°Ê¯S®íÄ_¥Û(gem);
        }        
    }
}
