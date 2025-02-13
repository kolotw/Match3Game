using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class UI_Controller : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnEnable()
    {
        // 獲取根元素
        var root = GetComponent<UIDocument>().rootVisualElement;

        // 找到按鈕元素
        Button startButton = root.Q<Button>("But_Start");
        Button mapButton = root.Q<Button>("But_Map");

        // 註冊按鈕點擊事件
        if(LevelManager.Instance.Level==0) LevelManager.Instance.Level=1;
        startButton.clicked += () => { SceneManager.LoadScene("Level"); };
        mapButton.clicked += () => { SceneManager.LoadScene("Map"); };
    }
}
