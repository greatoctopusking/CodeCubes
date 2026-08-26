using System.Collections.Generic;

public static class CodeValidator
{
    public struct Error
    {
        public string message;
    }

    public static List<Error> Validate(Start startBlock)
    {
        var errors = new List<Error>();
        if (startBlock == null) return errors;

        DetectCycle(startBlock, errors);
        if (errors.Count > 0) return errors;

        DetectStructureMismatch(startBlock, errors);
        DetectExtraElse(startBlock, errors);
        DetectMissingJudger(startBlock, errors);

        return errors;
    }

    private static void DetectCycle(Start start, List<Error> errors)
    {
        var visited = new HashSet<Code>();
        Code cur = start;

        while (cur != null)
        {
            if (!visited.Add(cur))
            {
                errors.Add(new Error { message = $"Cycle detected: '{cur.name}'" });
                return;
            }
            cur = cur.next;
        }
    }

    private static void DetectStructureMismatch(Start start, List<Error> errors)
    {
        int ifDepth = 0;
        int whileDepth = 0;
        Code cur = start;

        while (cur != null)
        {
            if (cur is If) ifDepth++;
            else if (cur is IfEnd)
            {
                ifDepth--;
                if (ifDepth < 0)
                {
                    errors.Add(new Error { message = $"Extra IfEnd: '{cur.name}'" });
                    ifDepth = 0;
                }
            }
            else if (cur is While) whileDepth++;
            else if (cur is WhileEnd)
            {
                whileDepth--;
                if (whileDepth < 0)
                {
                    errors.Add(new Error { message = $"Extra WhileEnd: '{cur.name}'" });
                    whileDepth = 0;
                }
            }
            else if (cur is Else && ifDepth == 0 && whileDepth == 0)
            {
                errors.Add(new Error { message = $"Else '{cur.name}' outside any If block" });
            }
            cur = cur.next;
        }

        if (ifDepth > 0)
            errors.Add(new Error { message = $"Missing {ifDepth} IfEnd(s)" });
        if (whileDepth > 0)
            errors.Add(new Error { message = $"Missing {whileDepth} WhileEnd(s)" });
    }

    private static void DetectExtraElse(Start start, List<Error> errors)
    {
        Code cur = start;
        while (cur != null)
        {
            if (cur is If ifBlock)
            {
                int elseCount = 0;
                int depth = 0;
                Code scan = ifBlock.next;

                while (scan != null)
                {
                    if (scan is If) depth++;
                    else if (scan is Else)
                    {
                        if (depth == 0)
                        {
                            elseCount++;
                            if (elseCount > 1)
                            {
                                errors.Add(new Error { message = $"If '{ifBlock.name}' has multiple Else blocks" });
                                break;
                            }
                        }
                    }
                    else if (scan is IfEnd && depth == 0) break;
                    scan = scan.next;
                }
            }
            cur = cur.next;
        }
    }

    private static void DetectMissingJudger(Start start, List<Error> errors)
    {
        Code cur = start;
        while (cur != null)
        {
            if (cur is If ifBlock && ifBlock.Judger == null)
                errors.Add(new Error { message = $"If '{ifBlock.name}' missing condition" });
            else if (cur is While whileBlock && whileBlock.Judger == null)
                errors.Add(new Error { message = $"While '{whileBlock.name}' missing condition" });
            cur = cur.next;
        }
    }
}
