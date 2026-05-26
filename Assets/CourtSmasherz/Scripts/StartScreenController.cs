using UnityEngine;

public class StartScreenController : MonoBehaviour
{
    [Header("UI")]
    public GameObject startScreen;
    public GameObject hudCanvas;

    private void Start()
    {
        // Show start screen first
        if (startScreen != null)
            startScreen.SetActive(true);

        // Hide HUD first
        if (hudCanvas != null)
            hudCanvas.SetActive(false);
    }

    public void PlayGame()
    {
        // Hide start screen
        if (startScreen != null)
            startScreen.SetActive(false);

        // Show HUD
        if (hudCanvas != null)
            hudCanvas.SetActive(true);
    }
}