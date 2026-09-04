using UnityEngine;

public class StarRemainCode : BoolCode
{
    public override void work()
    {
        if (LevelManager.Instance == null)
        {
            judge = false;
            Complete();
            return;
        }

        foreach (var star in Object.FindObjectsOfType<Star>())
        {
            if (!star.collected)
            {
                judge = true;
                Complete();
                return;
            }
        }

        judge = false;
        Complete();
    }
}
