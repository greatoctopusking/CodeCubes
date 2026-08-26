using UnityEngine;

public class StarFrontCode : BoolCode
{
    public override void work()
    {
        judge = CheckStar(CodeManager.RobotTarget.forward);
        Complete();
    }

    public static bool CheckStar(Vector3 dir)
    {
        float cellSize = LevelManager.Instance != null ? LevelManager.Instance.cellSize : 1f;
        Vector3 checkPos = CodeManager.RobotTarget.position + dir * cellSize;
        foreach (var star in Object.FindObjectsOfType<Star>())
        {
            if (!star.collected && star.orderIndex == LevelManager.Instance.nextStarIndex)
            {
                float dist = Vector3.Distance(star.transform.position, checkPos);
                if (dist < 0.4f) return true;
            }
        }
        return false;
    }
}
