using System;

public static class GameEvents
{
    public static event Action OnSceneRestarted;
    
    
    public static void RestartScene() => OnSceneRestarted?.Invoke();
}