using UnityEngine;

namespace Showcase
{
    public class MainMenuController : MonoBehaviour
    {
        public void LoadShowcaseScene()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadScene("Showcase_Scene");
            }
            else
            {
                Debug.LogError("[MainMenuController] GameManager Instance not found!");
            }
        }

        public void LoadSandboxScene()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadScene("Sandbox_Scene");
            }
            else
            {
                Debug.LogError("[MainMenuController] GameManager Instance not found!");
            }
        }

        public void QuitApplication()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ExitGame();
            }
            else
            {
                Debug.LogError("[MainMenuController] GameManager Instance not found!");
            }
        }
    }
}
