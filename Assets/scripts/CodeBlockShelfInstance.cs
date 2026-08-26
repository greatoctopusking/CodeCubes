using UnityEngine;

/// <summary>
/// Marks a code block as sitting on the board shelf. Removed when the player grabs it.
/// </summary>
public class CodeBlockShelfInstance : MonoBehaviour
{
    public CodeBlockSlot sourceSlot;
    public GameObject sourcePrefab;
}
