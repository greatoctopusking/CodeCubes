using UnityEngine;

public class StarCollector : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var star = other.GetComponent<Star>();
        if (star != null && !star.collected)
        {
            LevelManager.Instance?.CollectStar(star);
        }
    }
}
