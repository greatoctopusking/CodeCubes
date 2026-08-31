using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Stage C board pool validation (Edit Mode, no OpenXR).
/// -batchmode -nographics -quit -executeMethod CodeBlockBoardValidation.ValidateAndExit
/// </summary>
public static class CodeBlockBoardValidation
{
    private const string ScenePath = "Assets/Scenes/Garage Scene.unity";
    private const string ReportPath = "Logs/CodeBlockBoardValidation.txt";

    [MenuItem("VRPG/Validate Code Block Board Pool")]
    public static void ValidateFromMenu()
    {
        var ok = Run(out var report);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Code Block Board", ok ? "PASSED" : "FAILED\nSee Logs/CodeBlockBoardValidation.txt", "OK");
    }

    public static void ValidateAndExit()
    {
        try
        {
            var ok = Run(out var report);
            File.WriteAllText(ReportPath, report, Encoding.UTF8);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }
        catch (Exception ex)
        {
            var fail = "RESULT=FAIL\nEXCEPTION=" + ex;
            File.WriteAllText(ReportPath, fail, Encoding.UTF8);
            Debug.LogError(fail);
            EditorApplication.Exit(1);
        }
    }

    private static bool Run(out string report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Stage C: CodeBlockBoard ===");
        bool ok = true;

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var catalog = AssetDatabase.LoadAssetAtPath<CodeBlockCatalog>("Assets/Resources/CodeBlockCatalog.asset");
        if (catalog == null)
            catalog = Resources.Load<CodeBlockCatalog>("CodeBlockCatalog");

        var board = UnityEngine.Object.FindObjectOfType<CodeBlockBoard>();
        if (catalog == null || board == null)
        {
            report = "RESULT=FAIL\nMissing Catalog or CodeBlockBoard";
            return false;
        }

        typeof(CodeBlockBoard).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, board);
        if (board.catalog == null)
            board.catalog = catalog;

        var codes = new List<Code>();
        foreach (var code in board.GetComponentsInChildren<Code>(true))
        {
            if (code.transform == board.transform)
                continue;
            if (code.GetComponentInParent<CodeBlockBoard>() != board)
                continue;
            codes.Add(code);
        }

        // Strip any leftover pool bindings so matching is tested cleanly.
        foreach (var code in codes)
        {
            var pool = code.GetComponent<CodeBlockPoolItem>();
            if (pool != null)
                UnityEngine.Object.DestroyImmediate(pool);
        }

        sb.AppendLine($"Catalog entries={catalog.EntryCount}, TotalBlockCount={catalog.TotalBlockCount}");
        sb.AppendLine($"Scene board Code count={codes.Count}");

        int matched = 0;
        var byType = new Dictionary<string, int>();
        var failures = new List<string>();

        foreach (var code in codes)
        {
            if (!code)
                continue;

            if (!catalog.TryGetEntryForGameObject(code.gameObject, out var entry) || entry == null)
            {
                failures.Add($"NO_MATCH name='{code.name}' type={code.GetType().Name}");
                continue;
            }

            matched++;
            if (!byType.ContainsKey(entry.displayName))
                byType[entry.displayName] = 0;
            byType[entry.displayName]++;
        }

        InvokePrivate(board, "BuildSlots");
        var slots = board.GetComponentsInChildren<CodeBlockSlot>(true);
        sb.AppendLine($"Matched={matched}/{codes.Count}");
        sb.AppendLine($"Slots built={slots.Length}");
        foreach (var kv in byType)
            sb.AppendLine($"  {kv.Key}: {kv.Value}");

        foreach (var f in failures)
            sb.AppendLine("  " + f);

        ok &= matched == codes.Count && matched == catalog.TotalBlockCount && slots.Length == catalog.TotalBlockCount;

        // G1-style: grab one, slot empty, return.
        CodeBlockSlot sampleSlot = null;
        Code sampleCode = null;
        foreach (var slot in slots)
        {
            if (slot == null || slot.IsEmpty) continue;
            var code = slot.GetComponentInChildren<Code>(true);
            if (code == null) continue;
            sampleSlot = slot;
            sampleCode = code;
            break;
        }

        if (sampleSlot == null || sampleCode == null)
        {
            ok = false;
            sb.AppendLine("FAIL: no occupied slot for grab/return test");
        }
        else
        {
            sampleSlot.ReleaseShelfBlock(sampleCode.gameObject);
            ok &= sampleSlot.IsEmpty && sampleCode.GetComponent<CodeBlockShelfInstance>() == null;
            sb.AppendLine(sampleSlot.IsEmpty ? "PASS: grab empties slot" : "FAIL: grab did not empty slot");

            bool returned = board.ReturnBlock(sampleCode);
            ok &= returned && sampleCode.GetComponent<CodeBlockShelfInstance>() != null;
            sb.AppendLine(returned ? "PASS: ReturnBlock" : "FAIL: ReturnBlock");
            sb.AppendLine(sampleCode.GetComponent<CodeBlockShelfInstance>() != null
                ? "PASS: ShelfInstance restored"
                : "FAIL: ShelfInstance missing after return");
        }

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        sb.AppendLine(ok && failures.Count == 0 ? "RESULT=PASS" : "RESULT=FAIL");
        report = sb.ToString();
        File.WriteAllText(ReportPath, report, Encoding.UTF8);
        return ok && failures.Count == 0;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, null);
    }
}
