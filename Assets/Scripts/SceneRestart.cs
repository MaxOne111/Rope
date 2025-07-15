using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneRestart : MonoBehaviour
{
    [SerializeField] private KeyCode restartSceneKey;

    public static event Action OnRestarted;
    
    private void OnEnable()
    {
        GameEvents.OnSceneRestarted += RestartScene;
    }

    private void Update()
    {
        if(Input.GetKeyDown(restartSceneKey))
            GameEvents.RestartScene();
    }

    private void RestartScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    private void OnDisable()
    {
        GameEvents.OnSceneRestarted -= RestartScene;
    }
}