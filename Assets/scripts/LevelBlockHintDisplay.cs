using TMPro;
using UnityEngine;

public class LevelBlockHintDisplay : MonoBehaviour
{
    public static LevelBlockHintDisplay Instance { get; private set; }

    private static readonly Color HintColor = Color.white;
    private static readonly Color ErrorColor = new Color(1f, 0.82f, 0.28f);

    [SerializeField] TMP_Text hintText;

    private LevelData currentData;

    private void Awake()
    {
        Instance = this;
        EnsureHintText();
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Show(LevelData data)
    {
        if (data == null) return;

        currentData = data;
        ShowHintList();
    }

    public void ShowError(string message)
    {
        EnsureHintText();
        if (hintText == null) return;

        hintText.color = ErrorColor;
        hintText.text = string.IsNullOrEmpty(message) ? string.Empty : message;
        hintText.gameObject.SetActive(true);
    }

    public void RestoreHints()
    {
        if (currentData != null)
            ShowHintList();
        else
            Hide();
    }

    public void Hide()
    {
        if (hintText != null)
            hintText.gameObject.SetActive(false);
    }

    private void ShowHintList()
    {
        EnsureHintText();
        if (hintText == null) return;

        hintText.color = HintColor;
        hintText.text = FormatBlockList(currentData != null ? currentData.GetSuggestedBlockNames() : null);
        hintText.gameObject.SetActive(true);
    }

    private static string FormatBlockList(string[] names)
    {
        if (names == null || names.Length == 0)
            return string.Empty;

        return "Suggested Blocks\n\n" + string.Join("   ·   ", names);
    }

    private void EnsureHintText()
    {
        if (hintText != null) return;

        var menu = FindObjectOfType<MenuManager>();
        if (menu == null || menu.inLevelPanel == null) return;

        var go = new GameObject("BlockHintText");
        go.transform.SetParent(menu.inLevelPanel.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 350f);
        rt.anchoredPosition = Vector2.zero;

        hintText = go.AddComponent<TextMeshProUGUI>();
        if (menu.inLevelNameText != null)
        {
            hintText.font = menu.inLevelNameText.font;
            hintText.fontSharedMaterial = menu.inLevelNameText.fontSharedMaterial;
        }

        hintText.fontSize = 28f;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.enableWordWrapping = true;
        hintText.color = Color.white;
        hintText.raycastTarget = false;
    }
}
