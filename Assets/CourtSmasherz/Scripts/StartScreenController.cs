using UnityEngine;

namespace CourtSmasherz
{
    public class StartScreenController : MonoBehaviour
    {
        [Header("Title Screen")]
        public GameObject startScreen;

        [Header("UI Flow")]
        public MainMenuController mainMenu;

        private void Start()
        {
            if (startScreen != null)
            {
                startScreen.SetActive(true);
            }

            if (mainMenu != null)
            {
                mainMenu.HideMenu();
            }
        }

        public void PlayGame()
        {
            if (startScreen != null)
            {
                startScreen.SetActive(false);
            }

            if (mainMenu != null)
            {
                mainMenu.ShowMenu();
            }
        }
    }
}