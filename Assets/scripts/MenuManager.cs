using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance { get; private set; }

    [Header("State Panels")]
    public GameObject titlePanel;
    public GameObject levelSelectPanel;
    public GameObject inLevelPanel;
    public GameObject levelCompletePanel;
    public GameObject levelFailedPanel;

    [Header("Level Select")]
    public GameObject levelButtonPrefab;
    public Transform levelButtonContainer;

    [Header("Text References")]
    public TMP_Text titleText;
    public TMP_Text inLevelNameText;
    public TMP_Text completeLevelNameText;
    public TMP_Text failedLevelNameText;

    [Header("Status")]
    public TMP_Text statusText;

    [Header("Buttons")]
    public Button startButton;
    public Button leaveButton;
    public Button nextButton;
    public Button completeLeaveButton;
    public Button failRetryButton;
    public Button failLeaveButton;

    private LevelManager levelManager;
    private int pendingLevelIndex;
    private LevelBlockHintDisplay blockHintDisplay;

    private void Start()
    {
        Instance = this;
        levelManager = FindObjectOfType<LevelManager>();
        blockHintDisplay = GetComponent<LevelBlockHintDisplay>();
        if (blockHintDisplay == null)
            blockHintDisplay = gameObject.AddComponent<LevelBlockHintDisplay>();
        BindButtons();
        DisablePanelBackgroundRaycasts();
        ShowTitle();
    }

    private void DisablePanelBackgroundRaycasts()
    {
        DisableBackgroundRaycast(titlePanel);
        DisableBackgroundRaycast(levelSelectPanel);
        DisableBackgroundRaycast(inLevelPanel);
        DisableBackgroundRaycast(levelCompletePanel);
        DisableBackgroundRaycast(levelFailedPanel);
    }

    private static void DisableBackgroundRaycast(GameObject panel)
    {
        if (panel == null)
            return;

        var image = panel.GetComponent<Image>();
        if (image != null)
            image.raycastTarget = false;
    }

    public void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }

    public void ClearStatus()
    {
        if (statusText != null) statusText.text = "";
    }

    private void BindButtons()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
        if (completeLeaveButton != null) completeLeaveButton.onClick.AddListener(OnLeaveClicked);
        if (failRetryButton != null) failRetryButton.onClick.AddListener(OnRetryClicked);
        if (failLeaveButton != null) failLeaveButton.onClick.AddListener(OnLeaveClicked);
    }

    private void HideAll()
    {
        if (titlePanel != null) titlePanel.SetActive(false);
        if (levelSelectPanel != null) levelSelectPanel.SetActive(false);
        if (inLevelPanel != null) inLevelPanel.SetActive(false);
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        if (levelFailedPanel != null) levelFailedPanel.SetActive(false);
        blockHintDisplay?.Hide();
    }

    public void ShowTitle()
    {
        HideAll();
        if (titlePanel != null) titlePanel.SetActive(true);
        if (titleText != null) titleText.text = "Coding Blocks";
        AudioManager.Instance?.PlayBootAndAmbience();
    }

    public void ShowLevelSelect()
    {
        HideAll();
        if (levelSelectPanel != null) levelSelectPanel.SetActive(true);

        var leftColumn = levelButtonContainer.Find("LeftColumn");
        var rightColumn = levelButtonContainer.Find("RightColumn");

        if (leftColumn != null && rightColumn != null)
        {
            foreach (Transform child in leftColumn)
                Destroy(child.gameObject);
            foreach (Transform child in rightColumn)
                Destroy(child.gameObject);
        }
        else
        {
            foreach (Transform child in levelButtonContainer)
                Destroy(child.gameObject);
        }

        if (levelButtonPrefab != null)
        {
            for (int i = 0; i < levelManager.levels.Count; i++)
            {
                var level = levelManager.levels[i];
                Transform parent = levelButtonContainer;
                if (leftColumn != null && rightColumn != null)
                    parent = i < 10 ? leftColumn : rightColumn;

                var btnObj = Instantiate(levelButtonPrefab, parent);
                var text = btnObj.GetComponentInChildren<TMP_Text>();
                if (text != null) text.text = $"Level {level.levelNumber}";
                var btn = btnObj.GetComponent<Button>();
                if (btn != null)
                {
                    int index = i;
                    btn.onClick.AddListener(() => OnLevelSelected(index));
                }
            }
        }
    }

    public void ShowInLevel()
    {
        HideAll();
        if (inLevelPanel != null) inLevelPanel.SetActive(true);
        UpdateLevelName();
        blockHintDisplay?.Show(levelManager.currentLevelData);
    }

    public void ShowLevelComplete()
    {
        HideAll();
        if (levelCompletePanel != null) levelCompletePanel.SetActive(true);
        if (completeLevelNameText != null)
            completeLevelNameText.text = $"Level {levelManager.currentLevelData.levelNumber} Complete!";
        AudioManager.Instance?.Play(SoundId.LevelComplete);
    }

    public void ShowLevelFailed()
    {
        HideAll();
        if (levelFailedPanel != null) levelFailedPanel.SetActive(true);
        if (failedLevelNameText != null)
            failedLevelNameText.text = $"Level {levelManager.currentLevelData.levelNumber}";
        SetButtonLabel(failRetryButton, "Retry");
        SetButtonLabel(failLeaveButton, "Leave");
        LayoutFailOptionsCentered();
        AudioManager.Instance?.Play(SoundId.LevelFail);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text != null) text.text = label;
    }

    private void LayoutFailOptionsCentered()
    {
        if (levelFailedPanel == null) return;

        if (failedLevelNameText != null)
        {
            failedLevelNameText.transform.SetSiblingIndex(0);
            failedLevelNameText.alignment = TextAlignmentOptions.Center;
            failedLevelNameText.enableWordWrapping = false;
            failedLevelNameText.margin = Vector4.zero;
            PlaceFailItem(failedLevelNameText.rectTransform, -120f, new Vector2(480f, 80f));
        }

        if (failRetryButton != null)
        {
            failRetryButton.transform.SetSiblingIndex(1);
            PlaceFailItem(failRetryButton.GetComponent<RectTransform>(), -250f, new Vector2(200f, 40f));
        }

        if (failLeaveButton != null)
        {
            failLeaveButton.transform.SetSiblingIndex(2);
            PlaceFailItem(failLeaveButton.GetComponent<RectTransform>(), -360f, new Vector2(200f, 40f));
        }
    }

    private static void PlaceFailItem(RectTransform rt, float y, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(396f, y);
        rt.sizeDelta = size;
        rt.localScale = Vector3.one;
    }

    private void UpdateLevelName()
    {
        var name = $"Level {levelManager.currentLevelData.levelNumber}";
        if (inLevelNameText != null) inLevelNameText.text = name;
    }

    private void OnStartClicked()
    {
        AudioManager.Instance?.Play(SoundId.UiClick);
        ShowLevelSelect();
    }

    private void OnLeaveClicked()
    {
        AudioManager.Instance?.Play(SoundId.UiClick);
        levelManager.StopLevel();
        ShowLevelSelect();
    }

    private void OnNextClicked()
    {
        AudioManager.Instance?.Play(SoundId.UiClick);
        pendingLevelIndex = levelManager.currentLevelIndex + 1;
        if (pendingLevelIndex >= levelManager.levels.Count)
        {
            ShowLevelSelect();
            return;
        }
        levelManager.LoadLevelByIndex(pendingLevelIndex);
        ShowInLevel();
    }

    private void OnRetryClicked()
    {
        AudioManager.Instance?.Play(SoundId.UiClick);
        levelManager.ReloadLevel();
        ShowInLevel();
    }

    private void OnLevelSelected(int index)
    {
        AudioManager.Instance?.Play(SoundId.UiClick);
        pendingLevelIndex = index;
        levelManager.LoadLevelByIndex(index);
        ShowInLevel();
        inLevelNameText.text = $"Level {levelManager.currentLevelData.levelNumber}";
    }
}
