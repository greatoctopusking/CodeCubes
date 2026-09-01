using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Final Edit Mode validation for stages D–G (automatable).
/// -batchmode -nographics -quit -executeMethod PoolSystemFinalValidation.ValidateAndExit
/// </summary>
public static class PoolSystemFinalValidation
{
    private const string ScenePath = "Assets/Scenes/Garage Scene.unity";
    private const string ReportPath = "Logs/PoolSystemFinalValidation.txt";

    [MenuItem("VRPG/Validate Pool System (Final D-G)")]
    public static void ValidateFromMenu()
    {
        var ok = Run(out var report);
        Debug.Log(report);
        EditorUtility.DisplayDialog("Pool System Final", ok ? "PASSED" : "FAILED\nSee Logs/PoolSystemFinalValidation.txt", "OK");
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
        bool ok = true;
        sb.AppendLine("=== Final Pool System Validation (D / E / F / G) ===");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var board = UnityEngine.Object.FindObjectOfType<CodeBlockBoard>();
        var trash = UnityEngine.Object.FindObjectOfType<TrashCan>();
        var levelManager = UnityEngine.Object.FindObjectOfType<LevelManager>();
        var catalog = AssetDatabase.LoadAssetAtPath<CodeBlockCatalog>("Assets/Resources/CodeBlockCatalog.asset");

        sb.AppendLine("-- Stage F --");
        ok &= Check(sb, board != null, "F1 CodeBlockBoard present");
        ok &= Check(sb, trash != null && trash.GetComponent<Collider>() is Collider tc && tc.isTrigger, "F2 TrashCan trigger");
        ok &= Check(sb, catalog != null && catalog.TotalBlockCount > 0, $"F3 Catalog TotalBlockCount={catalog?.TotalBlockCount}");
        ok &= Check(sb, levelManager != null, "F/E LevelManager present");

        bool garageInBuild = false;
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.enabled && s.path.Replace('\\', '/').EndsWith("Assets/Scenes/Garage Scene.unity", StringComparison.Ordinal))
            {
                garageInBuild = true;
                break;
            }
        }
        ok &= Check(sb, garageInBuild, "F5 Garage Scene in Build Settings");

        if (board == null || catalog == null)
        {
            sb.AppendLine("RESULT=FAIL");
            report = sb.ToString();
            return false;
        }

        typeof(CodeBlockBoard).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, board);
        if (board.catalog == null)
            board.catalog = catalog;

        // Clean stale pool items then build slots.
        foreach (var code in board.GetComponentsInChildren<Code>(true))
        {
            var pool = code.GetComponent<CodeBlockPoolItem>();
            if (pool != null)
                UnityEngine.Object.DestroyImmediate(pool);
        }

        InvokePrivate(board, "BuildSlots");
        var slotComps = board.GetComponentsInChildren<CodeBlockSlot>(true);
        int expectedBlocks = catalog.TotalBlockCount;
        ok &= Check(sb, slotComps.Length == expectedBlocks, $"F4/C2 slots built={slotComps.Length} expected={expectedBlocks}");

        sb.AppendLine("-- Stage D / G --");

        // D3/D4 wiring
        ok &= Check(sb,
            typeof(TrashCan).GetField("codeManager", BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "D3/G7 TrashCan execute lockout field");
        ok &= Check(sb,
            typeof(TrashCan).GetMethod("HasExistingVisual", BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "H3 HasExistingVisual present");

        // G1 pull 3 blocks
        var pulled = new List<(CodeBlockSlot slot, Code code)>();
        foreach (var slot in slotComps)
        {
            if (pulled.Count >= 3)
                break;
            if (slot == null || slot.IsEmpty)
                continue;
            var code = slot.GetComponentInChildren<Code>(true);
            if (code == null)
                continue;
            slot.ReleaseShelfBlock(code.gameObject);
            pulled.Add((slot, code));
        }

        ok &= Check(sb, pulled.Count == 3, $"G1 pulled {pulled.Count} blocks");
        foreach (var p in pulled)
            ok &= Check(sb, p.slot.IsEmpty && p.code.GetComponent<CodeBlockShelfInstance>() == null, $"G1 empty after grab {p.code.name}");

        int shelf = 0;
        foreach (var slot in slotComps)
        {
            if (slot != null && !slot.IsEmpty)
                shelf++;
        }
        ok &= Check(sb, shelf + pulled.Count == expectedBlocks, $"G9 conservation shelf({shelf})+workspace({pulled.Count})={expectedBlocks}");

        // G2 ReturnBlock (trash success path)
        if (pulled.Count > 0)
        {
            var sample = pulled[0].code;
            bool returned = board.ReturnBlock(sample);
            ok &= Check(sb, returned, "D2/G2 ReturnBlock");
            ok &= Check(sb, sample != null && sample.GetComponent<CodeBlockShelfInstance>() != null, "D2/G2 ShelfInstance restored");
            pulled.RemoveAt(0);
        }

        // E/G6 ClearWorkspace returns remaining workspace blocks
        board.ClearWorkspace();
        int occupied = 0;
        foreach (var slot in slotComps)
        {
            if (slot != null && !slot.IsEmpty)
                occupied++;
        }
        ok &= Check(sb, occupied == expectedBlocks, $"E/G6 ClearWorkspace occupied={occupied}");

        bool allRestored = true;
        foreach (var p in pulled)
        {
            if (p.code == null || p.code.GetComponent<CodeBlockShelfInstance>() == null)
            {
                allRestored = false;
                break;
            }
        }
        ok &= Check(sb, allRestored, "E/G6 workspace blocks restored (not destroyed)");

        // G5 full reject
        var sampleSlot = FindOccupied(slotComps);
        if (sampleSlot != null)
        {
            var sample = sampleSlot.GetComponentInChildren<Code>(true);
            sampleSlot.ReleaseShelfBlock(sample.gameObject);
            board.ReturnBlock(sample);

            var extraGo = new GameObject("OverflowFinal");
            var extraCode = (Code)extraGo.AddComponent(sample.GetType());
            var extraPool = extraGo.AddComponent<CodeBlockPoolItem>();
            var pool = sample.GetComponent<CodeBlockPoolItem>();
            extraPool.sourcePrefab = pool != null ? pool.sourcePrefab : null;
            bool overflow = board.ReturnBlock(extraCode);
            ok &= Check(sb, !overflow && extraCode != null, "G5 full-slot reject without Destroy");
            UnityEngine.Object.DestroyImmediate(extraGo);
        }

        // H2: deferred detach API exists
        ok &= Check(sb,
            typeof(CodeBlockSlot).GetMethod("ReleaseShelfBlock", new[] { typeof(GameObject), typeof(bool) }) != null,
            "H2 ReleaseShelfBlock supports deferWorldDetach");

        // H4: IsUnderBoard should be gone
        ok &= Check(sb,
            typeof(CodeBlockBoard).GetMethod("IsUnderBoard", BindingFlags.Instance | BindingFlags.NonPublic) == null,
            "H4 IsUnderBoard removed");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        sb.AppendLine(ok ? "RESULT=PASS" : "RESULT=FAIL");
        report = sb.ToString();
        File.WriteAllText(ReportPath, report, Encoding.UTF8);
        return ok;
    }

    private static CodeBlockSlot FindOccupied(CodeBlockSlot[] slots)
    {
        foreach (var slot in slots)
        {
            if (slot != null && !slot.IsEmpty && slot.GetComponentInChildren<Code>(true) != null)
                return slot;
        }
        return null;
    }

    private static bool Check(StringBuilder sb, bool condition, string label)
    {
        sb.AppendLine((condition ? "PASS: " : "FAIL: ") + label);
        return condition;
    }

    private static void InvokePrivate(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(target.GetType().Name, methodName);
        method.Invoke(target, null);
    }
}
