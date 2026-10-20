using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private const int LevelCount = 4;

    [Tooltip("Hidden in web builds, where the browser tab can't be closed.")]
    [SerializeField] private GameObject quitButton;

    private void Start()
    {
        EnsureLevelButtons();

        if (quitButton != null && Application.platform == RuntimePlatform.WebGLPlayer)
        {
            quitButton.SetActive(false);
        }
    }

    // Keeps older checked-in menu scenes usable immediately while the editor
    // builder creates the same four-button layout for future rebuilds.
    private void EnsureLevelButtons()
    {
        Transform levels = transform.Find("Levels");
        if (levels == null)
        {
            Debug.LogError("MainMenu needs a Levels container.", this);
            return;
        }

        List<Button> buttons = new List<Button>();
        for (int i = 0; i < levels.childCount; i++)
        {
            Button button = levels.GetChild(i).GetComponent<Button>();
            if (button != null)
            {
                buttons.Add(button);
            }
        }

        if (buttons.Count == 0)
        {
            Debug.LogError("MainMenu needs at least one level button template.", this);
            return;
        }

        while (buttons.Count < LevelCount)
        {
            Button clone = Instantiate(buttons[0], levels);
            buttons.Add(clone);
        }

        for (int i = 0; i < buttons.Count; i++)
        {
            bool active = i < LevelCount;
            buttons[i].gameObject.SetActive(active);
            if (!active)
            {
                continue;
            }

            string levelName = $"Level{i}";
            buttons[i].gameObject.name = $"Level {i} Button";
            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = $"Level {i}";
            }

            // Replace the event object so cloned buttons do not retain the
            // template's serialized scene target.
            buttons[i].onClick = new Button.ButtonClickedEvent();
            buttons[i].onClick.AddListener(() => LoadLevel(levelName));
        }
    }

    // Each level button passes its scene name.
    public void LoadLevel(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
