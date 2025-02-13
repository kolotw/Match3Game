using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class UI_Controller : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void OnEnable()
    {
        // ����ڤ���
        var root = GetComponent<UIDocument>().rootVisualElement;

        // �����s����
        Button startButton = root.Q<Button>("But_Start");
        Button mapButton = root.Q<Button>("But_Map");

        // ���U���s�I���ƥ�
        if(LevelManager.Instance.Level==0) LevelManager.Instance.Level=1;
        startButton.clicked += () => { SceneManager.LoadScene("Level"); };
        mapButton.clicked += () => { SceneManager.LoadScene("Map"); };
    }
}
