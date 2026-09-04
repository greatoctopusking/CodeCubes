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

    private Button failRetryKeepButton;
    private Button failRetryClearButton;
    private LevelManager levelManager;
    private int pendingLevelIndex;
    private LevelBlockHintDisplay blockHintDisplay;
    private bool completeTextLayoutCached;
    private Vector2 completeTextDefaultPos;
    private Vector2 completeTextDefaultSize;
    private float completeTextDefaultFontSize;
    private Vector4 completeTextDefaultMargin;

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
        if (statusText != null)
            statusText.text = string.Empty;

        if (!string.IsNullOrEmpty(msg) &&
            levelManager != null &&
            levelManager.IsLevelActive)
        {
            LevelBlockHintDisplay.Instance?.ShowError(msg);
            return;
        }

        if (statusText != null)
            statusText.text = msg ?? string.Empty;
    }

    public void ClearStatus()
    {
        if (statusText != null)
            statusText.text = string.Empty;

        if (levelManager != null && levelManager.IsLevelActive)
            LevelBlockHintDisplay.Instance?.RestoreHints();
    }

    private void BindButtons()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeaveClicked);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextClicked);
        if (completeLeaveButton != null) completeLeaveButton.onClick.AddListener(OnLeaveClicked);
        EnsureFailRetryButtons();
        if (failRetryKeepButton != null) failRetryKeepButton.onClick.AddListener(OnRetryKeepClicked);
        if (failRetryClearButton != null) failRetryClearButton.onClick.AddListener(OnRetryClearClicked);
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
        AlignColumnMiddle(leftColumn);
        AlignColumnMiddle(rightColumn);

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
                if (level == null)
                    continue;

                Transform parent = levelButtonContainer;
                if (leftColumn != null && rightColumn != null)
                {
                    int perColumn = (levelManager.levels.Count + 1) / 2;
                    parent = i < perColumn ? leftColumn : rightColumn;
                }

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

        if (levelButtonContainer is RectTransform containerRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRt);
    }

    private static void AlignColumnMiddle(Transform column)
    {
        if (column == null)
            return;

        var layout = column.GetComponent<VerticalLayoutGroup>();
        if (layout != null)
            layout.childAlignment = TextAnchor.MiddleCenter;
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

        bool isLast = levelManager != null && levelManager.IsLastLevel;
        if (completeLevelNameText != null)
        {
            CacheCompleteTextLayout();
            completeLevelNameText.enableWordWrapping = true;
            completeLevelNameText.alignment = TextAlignmentOptions.Center;
            if (isLast)
            {
                completeLevelNameText.text = "Congratulations!\nYou've finished all the levels.";
                completeLevelNameText.fontSize = 40f;
                completeLevelNameText.margin = Vector4.zero;
                var rt = completeLevelNameText.rectTransform;
                rt.anchoredPosition = new Vector2(396f, -140f);
                rt.sizeDelta = new Vector2(560f, 160f);
            }
            else
            {
                RestoreCompleteTextLayout();
                completeLevelNameText.text = $"Level {levelManager.currentLevelData.levelNumber} Complete!";
            }
        }

        if (nextButton != null)
            nextButton.gameObject.SetActive(!isLast);

        AudioManager.Instance?.Play(SoundId.LevelComplete);
    }

    public void ShowLevelFailed(string reason = null)
    {
        HideAll();
        if (levelFailedPanel != null) levelFailedPanel.SetActive(true);
        if (failedLevelNameText != null)
        {
            var levelName = $"Level {levelManager.currentLevelData.levelNumber}";
            failedLevelNameText.enableWordWrapping = !string.IsNullOrEmpty(reason);
            failedLevelNameText.text = string.IsNullOrEmpty(reason)
                ? levelName
                : $"{levelName}\n{reason}";
        }
        SetButtonLabel(failRetryKeepButton, "Retry (Keep)");
        SetButtonLabel(failRetryClearButton, "Retry (Clear)");
        SetButtonLabel(failLeaveButton, "Leave");
        LayoutFailOptionsCentered();
        AudioManager.Instance?.Play(SoundId.LevelFail);
    }

    private void EnsureFailRetryButtons()
    {
        if (failRetryButton == null || failRetryClearButton != null)
            return;

        failRetryKeepButton = failRetryButton;
        failRetryKeepButton.gameObject.name = "RetryKeep";

        var clone = Instantiate(failRetryButton.gameObject, failRetryButton.transform.parent);
        clone.name = "RetryClear";
        failRetryClearButton = clone.GetComponent<Button>();
        failRetryClearButton.onClick.RemoveAllListeners();
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null) return;
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text == null) return;

        text.text = label;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = 24f;
    }

    private void LayoutFailOptionsCentered()
    {
        if (levelFailedPanel == null) return;

        if (failedLevelNameText != null)
        {
            failedLevelNameText.transform.SetSiblingIndex(0);
            failedLevelNameText.alignment = TextAlignmentOptions.Center;
            failedLevelNameText.enableWordWrapping = true;
            failedLevelNameText.margin = Vector4.zero;
            PlaceFailItem(failedLevelNameText.rectTransform, -140f, new Vector2(560f, 140f));
        }

        if (failRetryKeepButton != null)
        {
            failRetryKeepButton.transform.SetSiblingIndex(1);
            PlaceFailItem(failRetryKeepButton.GetComponent<RectTransform>(), -230f, new Vector2(260f, 40f));
        }

        if (failRetryClearButton != null)
        {
            failRetryClearButton.transform.SetSiblingIndex(2);
            PlaceFailItem(failRetryClearButton.GetComponent<RectTransform>(), -278f, new Vector2(260f, 40f));
        }

        if (failLeaveButton != null)
        {
            failLeaveButton.transform.SetSiblingIndex(3);
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

    private void CacheCompleteTextLayout()
    {
        if (completeTextLayoutCached || completeLevelNameText == null)
            return;

        var rt = completeLevelNameText.rectTransform;
        completeTextDefaultPos = rt.anchoredPosition;
        completeTextDefaultSize = rt.sizeDelta;
        completeTextDefaultFontSize = completeLevelNameText.fontSize;
        completeTextDefaultMargin = completeLevelNameText.margin;
        completeTextLayoutCached = true;
    }

    private void RestoreCompleteTextLayout()
    {
        if (!completeTextLayoutCached || completeLevelNameText == null)
            return;

        var rt = completeLevelNameText.rectTransform;
        rt.anchoredPosition = completeTextDefaultPos;
        rt.sizeDelta = completeTextDefaultSize;
        completeLevelNameText.fontSize = completeTextDefaultFontSize;
        completeLevelNameText.margin = completeTextDefaultMargin;
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
        while (pendingLevelIndex < levelManager.levels.Count &&
               levelManager.levels[pendingLevelIndex] == null)
            pendingLevelIndex++;

        if (pendingLevelIndex >= levelManager.levels.Count)
        {
            ShowLevelSelect();
            return;
        }
        levelManager.LoadLevelByIndex(pendingLevelIndex);
        ShowInLevel();
    }

    private void OnRetryKeepClicked()
    {
        RetryLevel(keepWorkspace: true);
    }

    private void OnRetryClearClicked()
    {
        RetryLevel(keepWorkspace: false);
    }

    private void RetryLevel(bool keepWorkspace)
    {
        AudioManager.Instance?.Play(SoundId.UiClick);
        levelManager.ReloadLevel(keepWorkspace);
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
