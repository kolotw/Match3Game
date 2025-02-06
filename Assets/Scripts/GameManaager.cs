using UnityEngine;
using TMPro;

public class GameManaager : MonoBehaviour
{
    //public static GameManaager instance; // ¿R∫AπÍ®“≈‹º∆
    [SerializeField] public TextMeshPro levelText;
    [SerializeField] public TextMeshPro targetText;
    [SerializeField] public int level = 1;
    [SerializeField] public int target = 5;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        levelText.text = "";
    }

    // Update is called once per frame
    void Update()
    {
        
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
            levelText.text = "Level: " + level + " Completed!";
        }
    }
}
