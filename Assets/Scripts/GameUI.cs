using TMPro;
using UnityEngine;

public class GameUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI angularAccelerationText;

    public void ShowCurrentAngularAcceleration(float value)
    {
        angularAccelerationText.text = $"Angular acceleration: {value}";
    }
    
}