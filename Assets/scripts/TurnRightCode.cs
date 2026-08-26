public class TurnRightCode : Code
{
    public override void work()
    {
        if (CodeManager.RobotTarget != null)
            CodeManager.RobotTarget.Rotate(0f, 90f, 0f);
        Complete();
    }
}
