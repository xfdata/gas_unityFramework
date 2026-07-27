#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Flags hand-written <c>new GameplayTag(</c> outside generated *Def.gen.cs files.
/// </summary>
public static class GameplayTagHandwrittenValidator
{
    private static readonly Regex NewGameplayTagRegex = new Regex(
        @"new\s+GameplayTag\s*\(",
        RegexOptions.Compiled);

    [MenuItem("Tools/GAS/GameplayTags/Validate No Hand-Written new GameplayTag()")]
    public static void ValidateMenu()
    {
        var hits = ScanProject(out int fileCount);
        if (hits.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "GameplayTag Hand-Written Check",
                $"通过：在 {fileCount} 个 .cs 文件中未发现手写 new GameplayTag(（已忽略 *Def.gen.cs / Catalog.gen.cs）。",
                "OK");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"发现 {hits.Count} 处手写 new GameplayTag(：");
        int n = Math.Min(hits.Count, 40);
        for (int i = 0; i < n; i++)
            sb.AppendLine(hits[i]);
        if (hits.Count > n)
            sb.AppendLine($"... 另有 {hits.Count - n} 处");

        Debug.LogError(sb.ToString());
        EditorUtility.DisplayDialog("GameplayTag Hand-Written Check Failed", sb.ToString(), "OK");
    }

    public static List<string> ScanProject(out int scannedFiles)
    {
        scannedFiles = 0;
        var hits = new List<string>();
        string[] guids = AssetDatabase.FindAssets("t:Script");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                continue;

            if (IsGeneratedTagFile(path))
                continue;

            // Skip editor generator itself and known infrastructure that constructs tags.
            if (path.IndexOf("GameplayTagCodeGenerator.cs", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (path.IndexOf("GameplayTagReferenceScanner.cs", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (path.IndexOf("GameplayTagContainer.cs", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (path.IndexOf("GameplayTagCatalog", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                continue;

            scannedFiles++;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(fullPath);
            }
            catch
            {
                continue;
            }

            for (int line = 0; line < lines.Length; line++)
            {
                if (!NewGameplayTagRegex.IsMatch(lines[line]))
                    continue;

                string trimmed = lines[line].Trim();
                // Allow infrastructure that reconstructs tags from domain+value+mask (container, catalog, scanners).
                if (trimmed.IndexOf("tag.Domain", StringComparison.Ordinal) >= 0 ||
                    trimmed.IndexOf("e.Domain", StringComparison.Ordinal) >= 0 ||
                    trimmed.IndexOf("Domain, Value", StringComparison.Ordinal) >= 0 ||
                    trimmed.IndexOf("Domain, value", StringComparison.Ordinal) >= 0 ||
                    trimmed.IndexOf("d, v, m", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                hits.Add($"{path}:{line + 1}: {trimmed}");
            }
        }

        return hits;
    }

    private static bool IsGeneratedTagFile(string path)
    {
        string name = Path.GetFileName(path);
        return name.EndsWith("Def.gen.cs", StringComparison.OrdinalIgnoreCase)
               || name.Equals("GameplayTagCatalog.gen.cs", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
