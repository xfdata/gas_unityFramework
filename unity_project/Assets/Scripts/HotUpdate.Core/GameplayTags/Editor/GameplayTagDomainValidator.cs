#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ensures each <see cref="GameplayTagDomain"/> is owned by at most one <see cref="GameplayTagDatabase"/>.
/// </summary>
public static class GameplayTagDomainValidator
{
    public readonly struct DomainOwner
    {
        public readonly GameplayTagDomain Domain;
        public readonly string AssetPath;
        public readonly string DatabaseName;

        public DomainOwner(GameplayTagDomain domain, string assetPath, string databaseName)
        {
            Domain = domain;
            AssetPath = assetPath;
            DatabaseName = databaseName;
        }
    }

    public static List<GameplayTagDatabase> FindAllDatabases()
    {
        var result = new List<GameplayTagDatabase>();
        string[] guids = AssetDatabase.FindAssets("t:GameplayTagDatabase");

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var db = AssetDatabase.LoadAssetAtPath<GameplayTagDatabase>(path);
            if (db != null)
                result.Add(db);
        }

        return result;
    }

    /// <summary>
    /// Returns false when two or more databases share a non-None domain.
    /// </summary>
    public static bool TryValidateUniqueDomains(out string error, GameplayTagDatabase focus = null)
    {
        error = null;
        var owners = new Dictionary<GameplayTagDomain, DomainOwner>();
        var conflicts = new List<string>();

        var databases = FindAllDatabases();
        for (int i = 0; i < databases.Count; i++)
        {
            var db = databases[i];
            if (db == null)
                continue;

            db.EnsureMigrated();
            var domain = db.Domain;
            if (domain == GameplayTagDomain.None)
            {
                if (focus == null || focus == db)
                {
                    conflicts.Add($"Database '{db.name}' Domain 不能为 None ({AssetDatabase.GetAssetPath(db)})");
                }

                continue;
            }

            string path = AssetDatabase.GetAssetPath(db);
            if (owners.TryGetValue(domain, out var existing))
            {
                if (existing.AssetPath == path)
                    continue;

                conflicts.Add(
                    $"Domain '{domain}' 被多个 Database 占用:\n" +
                    $"  - {existing.DatabaseName} ({existing.AssetPath})\n" +
                    $"  - {db.name} ({path})");
            }
            else
            {
                owners[domain] = new DomainOwner(domain, path, db.name);
            }
        }

        if (conflicts.Count == 0)
            return true;

        var sb = new StringBuilder();
        sb.AppendLine("GameplayTag Domain 校验失败：");
        for (int i = 0; i < conflicts.Count; i++)
            sb.AppendLine(conflicts[i]);
        sb.Append("每个 Domain 只能对应一个 GameplayTagDatabase。");
        error = sb.ToString();
        return false;
    }

    public static bool ValidateOrThrow(GameplayTagDatabase focus = null)
    {
        if (TryValidateUniqueDomains(out var error, focus))
            return true;

        throw new InvalidOperationException(error);
    }

    [MenuItem("Tools/GAS/GameplayTags/Validate Domain Uniqueness")]
    private static void ValidateMenu()
    {
        if (TryValidateUniqueDomains(out var error))
        {
            EditorUtility.DisplayDialog(
                "Domain Validation",
                "通过：每个 Domain 仅对应一个 GameplayTagDatabase。",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Domain Validation Failed", error, "OK");
            Debug.LogError(error);
        }
    }
}
#endif

