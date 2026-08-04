#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class GameplayTagCodeGenerator
{
    private sealed class TagNode
    {
        public string Name;
        public string FullPath;
        public int Id;
        public TagNode Parent;
        public SortedDictionary<string, TagNode> Children = new(StringComparer.Ordinal);
    }

    private const string DefaultOutputPathPattern = "Assets/Scripts/HotUpdate.Core/GameplayTags/{0}Def.gen.cs";

    private static readonly Regex GeneratedTagLineRegex = new Regex(
        @"new\s+GameplayTag\s*\(\s*Domain\s*,\s*0x(?<value>[0-9A-Fa-f]+)u\s*,\s*0x(?<mask>[0-9A-Fa-f]+)u\s*\)\s*;\s*//\s*@Tag:(?<path>.+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal) { "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while", "add", "alias", "ascending", "async", "await", "by", "descending", "dynamic", "equals", "from", "get", "global", "group", "into", "join", "let", "nameof", "on", "orderby", "partial", "remove", "select", "set", "unmanaged", "value", "var", "when", "where", "yield" };

    [MenuItem("Tools/GAS/GameplayTags/Generate All Databases")]
    public static void GenerateAllDatabasesMenu()
    {
        try
        {
            GenerateAllDatabases(force: false);
            EditorUtility.DisplayDialog(
                "Generate GameplayTags",
                "已为所有 GameplayTagDatabase 生成代码（含漂移保护）。",
                "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Generate GameplayTags Failed", e.Message, "OK");
        }
    }

    [MenuItem("Tools/GAS/GameplayTags/Generate Selected Database")]
    public static void GenerateSelectedDatabaseMenu()
    {
        var db = Selection.activeObject as GameplayTagDatabase;
        if (db == null)
        {
            EditorUtility.DisplayDialog(
                "Generate GameplayTags",
                "请先在 Project 窗口选中一个 GameplayTagDatabase 资产。",
                "OK");
            return;
        }

        try
        {
            BuildGameplayTags(db, force: false, rebuildCatalog: true);
            EditorUtility.DisplayDialog(
                "Generate GameplayTags",
                $"已生成: {db.name} (Domain={db.Domain})，并刷新 Catalog。",
                "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Generate GameplayTags Failed", e.Message, "OK");
        }
    }

    /// <summary>
    /// Generate every GameplayTagDatabase in the project.
    /// </summary>
    public static void GenerateAllDatabases(bool force = false)
    {
        var databases = GameplayTagDomainValidator.FindAllDatabases();
        if (databases.Count == 0)
        {
            Debug.LogWarning("未找到任何 GameplayTagDatabase。");
            return;
        }

        for (int i = 0; i < databases.Count; i++)
        {
            BuildGameplayTags(databases[i], force, rebuildCatalog: false);
        }

        RebuildGameplayTagCatalog();
    }

    /// <param name="force">
    /// When false, generation aborts if any existing path's value/mask would change.
    /// When true, skips the stability check (use only after intentional id migration).
    /// </param>
    public static void BuildGameplayTags(GameplayTagDatabase db, bool force = false, bool rebuildCatalog = true)
    {
        if (db == null)
            throw new ArgumentNullException(nameof(db));

        db.EnsureMigrated();

        if (db.Domain == GameplayTagDomain.None)
            throw new InvalidOperationException($"GameplayTagDatabase '{db.name}' 的 Domain 不能为 None");

        GameplayTagDomainValidator.ValidateOrThrow(db);

        string className = ToCSharpIdentifier(db.name, "GameplayTags");
        string buildPath = ResolveOutputPath(db, className);

        string directory = Path.GetDirectoryName(buildPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var tree = BuildTree(db);
        ValidateStableIds(tree);
        ValidateGeneratedFieldNames(tree);

        string code = GenerateCode(tree, className, db.Domain);

        if (!force && File.Exists(buildPath))
        {
            ValidateAgainstExistingGenerated(buildPath, tree, out var driftError);
            if (!string.IsNullOrEmpty(driftError))
            {
                throw new InvalidOperationException(
                    driftError +
                    "\n\n若确认要覆盖（会破坏已序列化 Tag value），请使用 Force Generate。");
            }
        }

        File.WriteAllText(buildPath, code, new UTF8Encoding(false));

        AssetDatabase.ImportAsset(buildPath);

        if (rebuildCatalog)
            RebuildGameplayTagCatalog();
        else
            AssetDatabase.Refresh();

        GameplayTagOdinUtility.ClearCache();
        GameplayTagLegacyFixup.ClearLookupCache();
        GameplayTagDebug.ClearCache();

        Debug.Log($"GameplayTags generated: {buildPath} (Domain={db.Domain}, force={force})");
    }

    private const string CatalogOutputPath =
        "Assets/Scripts/HotUpdate.Core/GameplayTags/GameplayTagCatalog.gen.cs";

    /// <summary>
    /// Rebuilds the flat catalog used by debug paths and editor dropdowns from all Databases.
    /// </summary>
    public static void RebuildGameplayTagCatalog()
    {
        var databases = GameplayTagDomainValidator.FindAllDatabases();
        var sb = new StringBuilder(8192);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// AUTO GENERATED. DO NOT EDIT.");
        sb.AppendLine("// Regenerated when any GameplayTagDatabase generates code.");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Flat catalog of all known tags for debug paths and editor dropdowns.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GameplayTagCatalog");
        sb.AppendLine("{");
        sb.AppendLine("    public readonly struct Entry");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly GameplayTagDomain Domain;");
        sb.AppendLine("        public readonly uint Value;");
        sb.AppendLine("        public readonly uint Mask;");
        sb.AppendLine("        public readonly string Path;");
        sb.AppendLine("        public readonly string Library;");
        sb.AppendLine("        public readonly string FieldName;");
        sb.AppendLine();
        sb.AppendLine("        public Entry(");
        sb.AppendLine("            GameplayTagDomain domain,");
        sb.AppendLine("            uint value,");
        sb.AppendLine("            uint mask,");
        sb.AppendLine("            string path,");
        sb.AppendLine("            string library,");
        sb.AppendLine("            string fieldName)");
        sb.AppendLine("        {");
        sb.AppendLine("            Domain = domain;");
        sb.AppendLine("            Value = value;");
        sb.AppendLine("            Mask = mask;");
        sb.AppendLine("            Path = path;");
        sb.AppendLine("            Library = library;");
        sb.AppendLine("            FieldName = fieldName;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public GameplayTag ToTag() => new GameplayTag(Domain, Value, Mask);");
        sb.AppendLine();
        sb.AppendLine("        public string DisplayName => Domain + \"/\" + Library + \"/\" + FieldName;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public static readonly Entry[] All =");
        sb.AppendLine("    {");

        var entries = new List<(GameplayTagDomain domain, string library, string path, string field, uint value, uint mask)>();

        for (int d = 0; d < databases.Count; d++)
        {
            var db = databases[d];
            if (db == null || db.Domain == GameplayTagDomain.None)
                continue;

            db.EnsureMigrated();
            string library = ToCSharpIdentifier(db.name, "GameplayTags");
            var tree = BuildTree(db);
            CollectCatalogEntries(tree, db.Domain, library, entries);
        }

        entries.Sort((a, b) =>
        {
            int c = a.domain.CompareTo(b.domain);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.library, b.library);
            if (c != 0) return c;
            return string.CompareOrdinal(a.path, b.path);
        });

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            sb.AppendLine(
                $"        new Entry(GameplayTagDomain.{e.domain}, 0x{e.value:X8}u, 0x{e.mask:X8}u, \"{e.path}\", \"{e.library}\", \"{e.field}\"),");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");

        string catalogDir = Path.GetDirectoryName(CatalogOutputPath);
        if (!string.IsNullOrEmpty(catalogDir) && !Directory.Exists(catalogDir))
            Directory.CreateDirectory(catalogDir);

        File.WriteAllText(CatalogOutputPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(CatalogOutputPath);
        AssetDatabase.Refresh();
        GameplayTagDebug.ClearCache();
        GameplayTagOdinUtility.ClearCache();

        Debug.Log($"GameplayTagCatalog generated: {CatalogOutputPath} ({entries.Count} tags)");
    }

    private static void CollectCatalogEntries(
        TagNode node,
        GameplayTagDomain domain,
        string library,
        List<(GameplayTagDomain domain, string library, string path, string field, uint value, uint mask)> list)
    {
        if (node.Parent != null)
        {
            BuildValueAndMask(node, out uint value, out uint mask);
            string field = ToGeneratedFieldName(node.FullPath);
            list.Add((domain, library, node.FullPath, field, value, mask));
        }

        foreach (var child in node.Children.Values)
            CollectCatalogEntries(child, domain, library, list);
    }

    /// <summary>
    /// Fails when an existing generated path would change value or mask.
    /// Removed paths only produce a log warning.
    /// </summary>
    private static void ValidateAgainstExistingGenerated(string buildPath, TagNode tree, out string error)
    {
        error = null;
        var existing = ParseGeneratedTagEntries(buildPath);
        if (existing.Count == 0)
            return;

        var newByPath = new Dictionary<string, (uint value, uint mask)>(StringComparer.Ordinal);
        CollectGeneratedValues(tree, newByPath);

        var drifts = new List<string>();
        var removed = new List<string>();

        for (int i = 0; i < existing.Count; i++)
        {
            var item = existing[i];
            if (!newByPath.TryGetValue(item.path, out var neu))
            {
                removed.Add(item.path);
                continue;
            }

            if (neu.value != item.value || neu.mask != item.mask)
            {
                drifts.Add(
                    $"  {item.path}: " +
                    $"was 0x{item.value:X8}/0x{item.mask:X8} -> " +
                    $"now 0x{neu.value:X8}/0x{neu.mask:X8}");
            }
        }

        if (removed.Count > 0)
        {
            Debug.LogWarning(
                $"GameplayTags generate: {removed.Count} path(s) removed from generated code " +
                $"(assets may still reference old values): {string.Join(", ", removed)}");
        }

        if (drifts.Count == 0)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Generate 被拒绝：已有 Tag 的 value/mask 发生漂移（稳定 Id 被破坏）。");
        int limit = Math.Min(drifts.Count, 20);
        for (int i = 0; i < limit; i++)
            sb.AppendLine(drifts[i]);
        if (drifts.Count > limit)
            sb.AppendLine($"  ... 另有 {drifts.Count - limit} 处");
        error = sb.ToString();
    }

    private static void CollectGeneratedValues(TagNode node, Dictionary<string, (uint value, uint mask)> map)
    {
        if (node.Parent != null)
        {
            BuildValueAndMask(node, out uint value, out uint mask);
            map[node.FullPath] = (value, mask);
        }

        foreach (var child in node.Children.Values)
            CollectGeneratedValues(child, map);
    }

    private static TagNode BuildTree(GameplayTagDatabase db)
    {
        var root = new TagNode
        {
            Name = "Root",
            FullPath = ""
        };

        foreach (var entry in db.Entries)
        {
            string tag = entry.path;
            if (!GameplayTagDatabase.IsValidTagPath(tag, out var error))
                throw new InvalidOperationException($"非法 GameplayTag: {tag}, reason: {error}");

            if (entry.siblingId < 1 || entry.siblingId > GameplayTagDatabase.MaxSiblingId)
            {
                throw new InvalidOperationException(
                    $"非法 siblingId: {tag}={entry.siblingId}（有效范围 1..{GameplayTagDatabase.MaxSiblingId}）");
            }

            var parts = tag.Split('.');
            var current = root;
            string full = "";

            for (int depth = 0; depth < parts.Length; depth++)
            {
                string part = parts[depth];
                full = depth == 0 ? part : full + "." + part;

                if (!current.Children.TryGetValue(part, out var child))
                {
                    // Leaf/intermediate nodes must exist in entries with their own siblingId.
                    if (!db.TryGetSiblingId(full, out int siblingId))
                    {
                        throw new InvalidOperationException(
                            $"缺少节点 siblingId: {full}（请重新添加该 Tag 或 Restore from Code）");
                    }

                    child = new TagNode
                    {
                        Name = part,
                        FullPath = full,
                        Parent = current,
                        Id = siblingId
                    };

                    current.Children.Add(part, child);
                }
                else if (depth == parts.Length - 1 && child.Id != entry.siblingId)
                {
                    throw new InvalidOperationException(
                        $"siblingId 不一致: {full} database={entry.siblingId}, tree={child.Id}");
                }

                current = child;
            }
        }

        return root;
    }

    private static void ValidateStableIds(TagNode root)
    {
        ValidateRecursive(root);
    }


    private static void ValidateGeneratedFieldNames(TagNode root)
    {
        var fieldPaths = new Dictionary<string, string>(StringComparer.Ordinal);
        ValidateGeneratedFieldNamesRecursive(root, fieldPaths);
    }

    private static void ValidateGeneratedFieldNamesRecursive(TagNode node, Dictionary<string, string> fieldPaths)
    {
        foreach (var child in node.Children.Values)
        {
            string fieldName = ToGeneratedFieldName(child.FullPath);
            if (fieldPaths.TryGetValue(fieldName, out var existingPath))
            {
                throw new InvalidOperationException(
                    $"GameplayTag generated field collision: '{existingPath}' and '{child.FullPath}' both map to '{fieldName}'.");
            }

            fieldPaths.Add(fieldName, child.FullPath);
            ValidateGeneratedFieldNamesRecursive(child, fieldPaths);
        }
    }

    private static void ValidateRecursive(TagNode node)
    {
        var used = new HashSet<int>();

        foreach (var child in node.Children.Values)
        {
            if (child.Id < 1 || child.Id > GameplayTagDatabase.MaxSiblingId)
            {
                throw new InvalidOperationException(
                    $"非法 siblingId: {child.FullPath}={child.Id}");
            }

            if (!used.Add(child.Id))
            {
                throw new InvalidOperationException(
                    $"同级 siblingId 冲突: parent='{FormatParent(node.FullPath)}', id={child.Id}, path={child.FullPath}");
            }

            ValidateRecursive(child);
        }

        if (node.Children.Count > GameplayTagDatabase.MaxSiblingId)
        {
            throw new InvalidOperationException(
                $"同级 GameplayTag 数量超过 {GameplayTagDatabase.MaxSiblingId}: {FormatParent(node.FullPath)}");
        }
    }

    private static string GenerateCode(TagNode root, string className, GameplayTagDomain domain)
    {
        var sb = new StringBuilder(4096);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// AUTO GENERATED. DO NOT EDIT.");
        sb.AppendLine();
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");
        sb.AppendLine($"    public const GameplayTagDomain Domain = GameplayTagDomain.{domain};");
        sb.AppendLine();

        foreach (var child in root.Children.Values)
        {
            GenerateNode(sb, child, 1);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void GenerateNode(StringBuilder sb, TagNode node, int indent)
    {
        string ind = new string(' ', indent * 4);

        BuildValueAndMask(node, out uint value, out uint mask);

        string fieldName = ToGeneratedFieldName(node.FullPath);

        sb.AppendLine(
            $"{ind}public static readonly GameplayTag {fieldName} = new GameplayTag(Domain, 0x{value:X8}u, 0x{mask:X8}u); // @Tag:{node.FullPath}");

        foreach (var child in node.Children.Values)
        {
            GenerateNode(sb, child, indent);
        }
    }

    public static List<string> ParseTagsFromGeneratedCode(string filePath)
    {
        var parsed = ParseGeneratedTagEntries(filePath);
        var tags = new List<string>(parsed.Count);
        for (int i = 0; i < parsed.Count; i++)
            tags.Add(parsed[i].path);
        return tags;
    }

    public static List<(string path, uint value, uint mask)> ParseGeneratedTagEntries(string filePath)
    {
        var result = new List<(string path, uint value, uint mask)>();

        if (!File.Exists(filePath))
            return result;

        var lines = File.ReadAllLines(filePath);

        foreach (string line in lines)
        {
            var match = GeneratedTagLineRegex.Match(line.Trim());
            if (!match.Success)
                continue;

            string path = match.Groups["path"].Value.Trim();
            if (string.IsNullOrEmpty(path))
                continue;

            if (!uint.TryParse(match.Groups["value"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
                continue;

            if (!uint.TryParse(match.Groups["mask"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint mask))
                continue;

            result.Add((path, value, mask));
        }

        return result;
    }

    public static void RestoreTags(GameplayTagDatabase db)
    {
        if (db == null)
            throw new ArgumentNullException(nameof(db));

        string className = ToCSharpIdentifier(db.name, "GameplayTags");
        string filePath = ResolveOutputPath(db, className);

        if (!File.Exists(filePath))
        {
            Debug.LogWarning($"找不到生成文件: {filePath}");
            return;
        }

        var parsed = ParseGeneratedTagEntries(filePath);

        if (parsed.Count == 0)
        {
            Debug.LogWarning($"生成文件中未找到任何 Tag 标记: {filePath}");
            return;
        }

        // Parents first so Upsert can validate ancestor existence.
        parsed.Sort((a, b) =>
        {
            int da = a.path.Split('.').Length;
            int dbDepth = b.path.Split('.').Length;
            int cmp = da.CompareTo(dbDepth);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.path, b.path);
        });

        var restored = new List<(string path, int siblingId)>(parsed.Count);
        var knownPaths = new HashSet<string>(StringComparer.Ordinal);
        var siblingIdsByParent = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);

        for (int i = 0; i < parsed.Count; i++)
        {
            var item = parsed[i];
            if (!TryExtractSiblingId(item.path, item.value, item.mask, out int siblingId, out var extractError))
                throw new InvalidOperationException($"Restore validation failed: {item.path}, {extractError}");

            if (!GameplayTagDatabase.IsValidTagPath(item.path, out var pathError))
                throw new InvalidOperationException($"Restore validation failed: {item.path}, {pathError}");

            if (!knownPaths.Add(item.path))
                throw new InvalidOperationException($"Restore validation failed: duplicate path {item.path}");

            string parent = GameplayTagDatabase.GetParentPath(item.path);
            if (!siblingIdsByParent.TryGetValue(parent, out var siblingIds))
            {
                siblingIds = new HashSet<int>();
                siblingIdsByParent.Add(parent, siblingIds);
            }

            if (!siblingIds.Add(siblingId))
            {
                throw new InvalidOperationException(
                    $"Restore validation failed: sibling id collision under '{GameplayTagDatabase.FormatParent(parent)}': {siblingId}");
            }

            restored.Add((item.path, siblingId));
        }

        for (int i = 0; i < restored.Count; i++)
        {
            string parent = GameplayTagDatabase.GetParentPath(restored[i].path);

            if (!string.IsNullOrEmpty(parent) && !knownPaths.Contains(parent))
                throw new InvalidOperationException($"Restore validation failed: missing parent {parent}");
        }
        Undo.RecordObject(db, "Restore Gameplay Tags from Code");

        db.ClearTags();

        for (int i = 0; i < parsed.Count; i++)
        {
            var item = parsed[i];
            if (!TryExtractSiblingId(item.path, item.value, item.mask, out int siblingId, out var extractError))
            {
                Debug.LogError($"Restore 失败: {item.path}, {extractError}");
                continue;
            }

            if (!db.UpsertTagWithSiblingId(item.path, siblingId, out var upsertError))
            {
                Debug.LogError($"Restore 失败: {item.path}, {upsertError}");
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssetIfDirty(db);

        Debug.Log($"已从生成文件还原 {parsed.Count} 个 Tag（含稳定 siblingId）: {filePath}");
    }

    private static bool TryExtractSiblingId(
        string path,
        uint value,
        uint mask,
        out int siblingId,
        out string error)
    {
        error = null;
        siblingId = 0;

        var parts = path.Split('.');
        int depth = parts.Length;
        if (depth < 1 || depth > GameplayTagDatabase.MaxDepth)
        {
            error = $"非法深度: {depth}";
            return false;
        }

        siblingId = GameplayTagEncoding.GetSiblingId(value, depth);

        if (siblingId < 1 || siblingId > GameplayTagDatabase.MaxSiblingId)
        {
            error = $"从 value=0x{value:X8} 解析 siblingId 非法: {siblingId}";
            return false;
        }

        // Soft-check: reconstructed value/mask for this node path depth should match mask's deepest byte.
        uint expectedMaskByte = GameplayTagEncoding.GetLevelByteMask(depth);
        if ((mask & expectedMaskByte) == 0)
        {
            error = $"mask=0x{mask:X8} 与路径深度不匹配: {path}";
            return false;
        }

        return true;
    }

    private static void BuildValueAndMask(TagNode node, out uint value, out uint mask)
    {
        value = 0;
        mask = 0;

        var path = new List<TagNode>(4);

        var cur = node;
        while (cur != null && cur.Parent != null)
        {
            path.Add(cur);
            cur = cur.Parent;
        }

        path.Reverse();

        if (path.Count > GameplayTagDatabase.MaxDepth)
            throw new InvalidOperationException($"GameplayTag 层级超过 {GameplayTagDatabase.MaxDepth} 层: {node.FullPath}");

        for (int i = 0; i < path.Count; i++)
        {
            var item = path[i];
            if (item.Id < 1 || item.Id > GameplayTagDatabase.MaxSiblingId)
            {
                throw new InvalidOperationException(
                    $"非法 siblingId: {item.FullPath}={item.Id}");
            }

            GameplayTagEncoding.EncodeSibling(ref value, ref mask, item.Id, i + 1);
        }
    }

    private static string ResolveOutputPath(GameplayTagDatabase db, string className)
    {
        string configuredPath = db.GeneratedCodePath;
        if (string.IsNullOrWhiteSpace(configuredPath))
            return string.Format(DefaultOutputPathPattern, className);

        configuredPath = configuredPath.Trim().Replace('\\', '/');
        if (configuredPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            return configuredPath.Replace("{0}", className);

        return Path.Combine(configuredPath, $"{className}Def.gen.cs").Replace('\\', '/');
    }

    private static string ToCSharpIdentifier(string raw, string fallback)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return fallback;

        var sb = new StringBuilder(raw.Length + 1);

        char first = raw[0];
        if (!(char.IsLetter(first) || first == '_'))
            sb.Append('_');

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }

        string result = sb.ToString();
        if (string.IsNullOrEmpty(result))
            return fallback;
        return CSharpKeywords.Contains(result) ? "_" + result : result;
    }

    private static string ToGeneratedFieldName(string path)
    {
        return ToCSharpIdentifier(path.Replace(".", "_"), "Tag");
    }

    private static string FormatParent(string parentPath)
    {
        return string.IsNullOrEmpty(parentPath) ? "<root>" : parentPath;
    }
}
#endif
