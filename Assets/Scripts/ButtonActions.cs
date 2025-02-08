using Match3Game;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ButtonActions : MonoBehaviour
{
    SpecialGemActivator specialGemActivator;

    void Start()
    {
        specialGemActivator = new SpecialGemActivator(Board.instance);
    }
    private void OnMouseUpAsButton()
    {
        if(GameObject.Find("/LevelText").GetComponent<TextMeshPro>().text == "Level: 1 Completed!")
        {
            SceneManager.LoadScene("MainMenu");
        }
        Gem gem = Board.instance.gems[0, 0];
        gem.id = 106;
        specialGemActivator.±Ò°Ê¯S®íÄ_¥Û(gem);
    }
}
