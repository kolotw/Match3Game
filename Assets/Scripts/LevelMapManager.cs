using UnityEngine;

public class LevelMapManager : MonoBehaviour
{
    public static LevelMapManager instance; // �R�A����ܼ�
    public int Level = 0;

    private void Awake()
    {
        instance = this;
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
