public class TurnRightCode : Code
{
    public override void work()
    {
        if (CodeManager.RobotTarget != null)
            CodeManager.RobotTarget.Rotate(0f, 90f, 0f);
        if (CodeManager.Robot != null)
            AudioManager.Instance?.Play(SoundId.RobotTurn, CodeManager.Robot.transform.position);
        Complete();
    }
}
