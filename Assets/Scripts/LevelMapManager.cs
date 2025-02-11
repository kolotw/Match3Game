using UnityEngine;

public class LevelMapManager : MonoBehaviour
{
    public static LevelMapManager instance; // ¿R∫AπÍ®“≈‹º∆
    public int Level = 1;

    private void Awake()
    {
        instance = this;
        Level = 1;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //DontDestroyOnLoad(this);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
