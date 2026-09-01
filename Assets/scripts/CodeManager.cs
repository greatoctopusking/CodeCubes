using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class CodeManager : MonoBehaviour
{
    public GameObject robot = null;

    public bool IsExecuting => playRoutine != null;

    private Coroutine playRoutine = null;
    private Stack<While> loopStack = new Stack<While>();

    private bool wasLeftTriggerPressed = false;

    public InputActionAsset inputActions;
    private InputAction leftTriggerAction;

    public static GameObject Robot { get; private set; }
    public static Animator RobotAnimator { get; private set; }
    public static Transform RobotTarget { get; private set; }
    private void Awake()
    {
        if (robot != null)
        {
            Robot = robot;
            RobotTarget = robot.transform;
            RobotAnimator = robot.GetComponent<Animator>();
        }

        BindLeftTrigger();
    }

    private void BindLeftTrigger()
    {
        if (inputActions == null)
            return;

        var leftMap = inputActions.FindActionMap("XRI Left Interaction");
        leftTriggerAction = leftMap != null
            ? leftMap.FindAction("Activate")
            : inputActions.FindAction("Activate");

        if (leftTriggerAction != null)
            leftTriggerAction.Enable();
        else
            Debug.LogError("[CM] Left Activate action not found");
    }

    private void OnDestroy()
    {
        if (leftTriggerAction != null)
        {
            leftTriggerAction.Disable();
        }
    }

    private IEnumerator PlayCoroutine(Start startBlock)
    {
        if (RobotAnimator != null)
        {
            RobotAnimator.SetBool("Open_Anim", true);
            if (Robot != null)
                AudioManager.Instance?.Play(SoundId.RobotBoot, Robot.transform.position);
            else
                AudioManager.Instance?.Play(SoundId.RobotBoot);
            yield return new WaitForSeconds(4.8f);
        }
        else
        {
            Debug.Log("[PlayCoroutine] RobotAnimator is null!");
            AudioManager.Instance?.Play(SoundId.RobotBoot);
        }

        Code cur = startBlock;
        if (cur == null)
        {
            Debug.LogWarning("[CM] No workspace Start block to execute");
            FinishPlayRoutine();
            yield break;
        }

        while (cur != null)
        {
            bool completed = false;
            System.Action handler = () => completed = true;

            cur.OnComplete += handler;

            cur.SetHighlight(true);

            cur.work();

            yield return new WaitUntil(() => completed);

            cur.SetHighlight(false);

            cur.OnComplete -= handler;
            
            if (cur is While whileBlock)
            {
                whileBlock.Judger?.work();
                if (whileBlock.Judger?.judge == true)
                {
                    loopStack.Push(whileBlock);
                }
                else
                {
                    cur = FindMatchingWhileEnd(whileBlock)?.next;
                    if (cur == null) break;
                    continue;
                }
            }

            if (cur is WhileEnd)
            {
                if (loopStack.Count == 0) break;

                While loopStart = loopStack.Peek();
                loopStart.Judger?.work();
                if (loopStart.Judger?.judge == true)
                {
                    cur = loopStart.next;
                    continue;
                }
                else
                {
                    loopStack.Pop();
                    cur = cur.next;
                    continue;
                }
            }

            if (cur is If ifBlock)
            {
                ifBlock.Judger?.work();
                if (ifBlock.Judger?.judge == true)
                {
                    cur = ifBlock.next;
                    if (cur == null) break;
                    continue;
                }
                else
                {
                    var elseBlock = FindMatchingElse(ifBlock);
                    if (elseBlock != null)
                    {
                        cur = elseBlock.next;
                    }
                    else
                    {
                        var endBlock = FindMatchingIfEnd(ifBlock);
                        cur = endBlock?.next;
                    }
                    if (cur == null) break;
                    continue;
                }
            }

            if (cur is Else)
            {
                var endBlock = FindMatchingIfEnd((Else)cur);
                cur = endBlock?.next;
                if (cur == null) break;
                continue;
            }

            if (cur is IfEnd)
            {
                cur = cur.next;
                continue;
            }

            if (cur.next == null && loopStack.Count > 0)
            {
                While loopStart = loopStack.Peek();

                loopStart.Judger?.work();
                if (loopStart.Judger?.judge == true)
                {
                    cur = loopStart.next;
                    continue;
                }
                else
                {
                    loopStack.Pop();
                    cur = FindMatchingWhileEnd(loopStart)?.next;
                    if (cur == null) break;
                    continue;
                }
            }
            
            cur = cur.next;
        }
        
        if (RobotAnimator != null)
        {
            RobotAnimator.SetBool("Walk_Anim", false);
            RobotAnimator.SetBool("Open_Anim", false);
        }

        FinishPlayRoutine();
    }

    private void FinishPlayRoutine()
    {
        playRoutine = null;
        if (LevelManager.Instance != null && LevelManager.Instance.IsLevelActive)
            LevelManager.Instance.OnRunFinished();
    }

    void Update()
    {
        CheckLeftTrigger();
    }

    private void CheckLeftTrigger()
    {
        if (leftTriggerAction == null)
        {
            BindLeftTrigger();
            if (leftTriggerAction == null)
                return;
        }
        
        bool pressed = leftTriggerAction.IsPressed();

        if (pressed && !wasLeftTriggerPressed)
        {
            ToggleCodeExecution();
        }

        wasLeftTriggerPressed = pressed;
    }

    public void StopExecution()
    {
        if (playRoutine != null)
        {
            StopAllCoroutines();
            playRoutine = null;
            if (RobotAnimator != null)
            {
                RobotAnimator.SetBool("Walk_Anim", false);
                RobotAnimator.SetBool("Open_Anim", false);
            }
            loopStack.Clear();
        }
    }

    private void ToggleCodeExecution()
    {
        bool inLevel = LevelManager.Instance != null && LevelManager.Instance.IsLevelActive;

        if (playRoutine != null)
        {
            if (inLevel)
                return;

            StopExecution();
            ResetAllBlocks(FindProgramStart());
            return;
        }

        if (inLevel && (LevelManager.Instance.HasStartedThisAttempt || LevelManager.Instance.IsLevelResolved))
            return;

        var start = FindProgramStart();
        if (start == null)
        {
            const string msg = "Connect your program to the Start block on the ground.";
            MenuManager.Instance?.SetStatus(msg);
            AudioManager.Instance?.Play(SoundId.ValidationFail);
            Debug.LogWarning($"[CM] {msg}");
            return;
        }

        var errors = CodeValidator.Validate(start);
        if (errors.Count > 0)
        {
            var msg = string.Join("\n", errors.ConvertAll(e => e.message));
            MenuManager.Instance?.SetStatus(msg);
            AudioManager.Instance?.Play(SoundId.ValidationFail);
            Debug.LogWarning($"[CM] Validation failed:\n{msg}");
            return;
        }
        MenuManager.Instance?.ClearStatus();

        RobotFacingIndicator.Instance?.Hide();
        LevelBlockHintDisplay.Instance?.Hide();

        if (inLevel)
            LevelManager.Instance.NotifyRunStarted();

        loopStack.Clear();
        ResetAllBlocks(start);
        AudioManager.Instance?.Play(SoundId.ProgramStart);
        Debug.Log($"[CM] Run '{start.name}'");
        playRoutine = StartCoroutine(PlayCoroutine(start));
    }

    private WhileEnd FindMatchingWhileEnd(While whileBlock)
    {
        int depth = 0;
        Code cur = whileBlock.next;

        while (cur != null)
        {
            if (cur is While) depth++;
            else if (cur is WhileEnd)
            {
                if (depth == 0) return (WhileEnd)cur;
                depth--;
            }
            cur = cur.next;
        }
        return null;
    }

    private Else FindMatchingElse(If ifBlock)
    {
        int depth = 0;
        Code cur = ifBlock.next;

        while (cur != null)
        {
            if (cur is If) depth++;
            else if (cur is Else)
            {
                if (depth == 0) return (Else)cur;
                depth--;
            }
            cur = cur.next;
        }
        return null;
    }

    private IfEnd FindMatchingIfEnd(Code fromBlock)
    {
        int depth = 0;
        Code cur = fromBlock.next;

        while (cur != null)
        {
            if (cur is If) depth++;
            else if (cur is IfEnd)
            {
                if (depth == 0) return (IfEnd)cur;
                depth--;
            }
            cur = cur.next;
        }
        return null;
    }

    private static Start FindProgramStart()
    {
        Start connected = null;
        Start workspace = null;

        foreach (var start in FindObjectsOfType<Start>())
        {
            if (start == null || start.GetComponent<CodeBlockShelfInstance>() != null)
                continue;

            if (workspace == null)
                workspace = start;

            if (start.next != null && connected == null)
                connected = start;
        }

        return connected != null ? connected : workspace;
    }

    private void ResetAllBlocks(Start start)
    {
        Code cur = start;
        while (cur != null)
        {
            cur.ResetState();
            cur = cur.next;
        }
    }
}