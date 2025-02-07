using Match3Game;
using Unity.VisualScripting;
using UnityEngine;

public class ButtonActions : MonoBehaviour
{
    SpecialGemActivator specialGemActivator;

    void Start()
    {
        specialGemActivator = new SpecialGemActivator(Board.instance);
    }
    private void OnMouseUpAsButton()
    {
        Gem gem = Board.instance.gems[0, 0];
        gem.id = 106;
        specialGemActivator.±Ò°Ê¯S®íÄ_¥Û(gem);
    }
}
