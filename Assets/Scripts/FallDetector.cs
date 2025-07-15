using UnityEngine;

public class FallDetector : MonoBehaviour
{
    [SerializeField] private Transform player;

    [SerializeField] private float fallBoundaryY = -10f;

    private void Update()
    {
        if (player.position.y < fallBoundaryY)
            GameEvents.RestartScene();
    }
    
}