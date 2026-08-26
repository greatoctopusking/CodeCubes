using UnityEngine;

public class StarLeftCode : BoolCode
{
    public override void work()
    {
        Vector3 dir = Quaternion.Euler(0f, -90f, 0f) * CodeManager.RobotTarget.forward;
        judge = StarFrontCode.CheckStar(dir);
        Complete();
    }
}
