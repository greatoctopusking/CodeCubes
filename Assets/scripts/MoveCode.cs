using UnityEngine;

public class MoveCode : Code
{
    public float speed = 2f;
    private bool isMoving = false;
    private float movedDistance = 0f;
    private Vector3 targetPosition;
    private Vector3 moveDirection;
    private float moveTargetDistance;

    override public void work()
    {
        if (isMoving) return;
        moveDirection = CodeManager.RobotTarget.forward;
        moveTargetDistance = LevelManager.Instance != null ? LevelManager.Instance.cellSize : 1f;
        targetPosition = CodeManager.RobotTarget.position + moveDirection * moveTargetDistance;

        if (LevelManager.Instance != null && !LevelManager.Instance.IsWithinGrid(targetPosition))
        {
            Complete();
            return;
        }

        isMoving = true;
        movedDistance = 0f;

        if (CodeManager.Robot != null)
            AudioManager.Instance?.Play(SoundId.RobotMove, CodeManager.Robot.transform.position);

        if (CodeManager.RobotAnimator != null)
        {
            CodeManager.RobotAnimator.SetBool("Walk_Anim", true);
        }
    }

    private void Update()
    {
        if (!isMoving) return;
        if (CodeManager.RobotTarget == null)
        {
            isMoving = false;
            Complete();
            return;
        }

        float step = speed * Time.deltaTime;
        step = Mathf.Min(step, moveTargetDistance - movedDistance);
        CodeManager.RobotTarget.Translate(moveDirection * step, Space.World);
        movedDistance += step;

        if (movedDistance >= moveTargetDistance)
        {
            CodeManager.RobotTarget.position = targetPosition;
            isMoving = false;

            if (CodeManager.RobotAnimator != null)
            {
                CodeManager.RobotAnimator.SetBool("Walk_Anim", false);
            }

            Complete();
        }
    }

    private void OnDestroy()
    {
        if (isMoving && CodeManager.RobotAnimator != null)
        {
            CodeManager.RobotAnimator.SetBool("Walk_Anim", false);
        }
    }
}
