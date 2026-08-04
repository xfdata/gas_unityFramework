#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lists deleted (retired) sibling ids and lets the user move them into the free recycle pool.
/// Only ids in the recycle pool are reused by subsequent AddTag calls.
/// </summary>
public sealed class GameplayTagRecycleWindow : EditorWindow
{
    private GameplayTagDatabase database;
    private Action onChanged;

    private Vector2 scroll;
    private string parentFilter = "";
    private string search = "";
    private readonly HashSet<string> selectedKeys = new(StringComparer.Ordinal);
    private bool selectAllVisible;

    public static void Show(GameplayTagDatabase db, Action onChanged = null, string parentFilter = null)
    {
        if (db == null)
        {
            EditorUtility.DisplayDialog("Recycle Sibling IDs", "GameplayTagDatabase is missing.", "OK");
            return;
        }

        var window = GetWindow<GameplayTagRecycleWindow>(true, "Recycle Sibling IDs", true);
        window.database = db;
        window.onChanged = onChanged;
        window.parentFilter = parentFilter ?? string.Empty;
        window.minSize = new Vector2(560, 420);
        window.selectedKeys.Clear();
        window.selectAllVisible = false;
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        if (database == null)
        {
            EditorGUILayout.HelpBox("No GameplayTagDatabase assigned.", MessageType.Error);
            return;
        }

        database.EnsureMigrated();

        DrawHeader();
        GUILayout.Space(4);
        DrawToolbar();
        GUILayout.Space(4);
        DrawList();
        GUILayout.Space(6);
        DrawFooter();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField(database.name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"Domain: {database.Domain}    Retired: {database.GetRetiredCount()}    Free pool: {database.GetRecycledPoolCount()}",
            EditorStyles.miniLabel);

        EditorGUILayout.HelpBox(
            "删除 Tag 后 siblingId 默认进入「弃用」列表，不会自动复用。\n" +
            "勾选并点击 Recycle 后，Id 进入「可复用池」，之后 Add Tag 会优先使用这些 Id。\n" +
            "注意：若项目资产里仍序列化着旧 Tag 的 value，复用 Id 可能导致旧配置误匹配。请确认该 Id 已无引用。",
            MessageType.Warning);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Parent", GUILayout.Width(48));
            string newFilter = GUILayout.TextField(
                parentFilter ?? string.Empty,
                EditorStyles.toolbarTextField,
                GUILayout.MinWidth(140));
            if (newFilter != parentFilter)
            {
                parentFilter = newFilter;
                selectedKeys.Clear();
                selectAllVisible = false;
            }

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(48)))
            {
                parentFilter = "";
                selectedKeys.Clear();
                selectAllVisible = false;
            }

            GUILayout.Space(8);
            GUILayout.Label("Search", GUILayout.Width(48));
            search = GUILayout.TextField(search ?? string.Empty, EditorStyles.toolbarTextField, GUILayout.MinWidth(100));

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Scan Holes", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                Undo.RecordObject(database, "Scan Retired Sibling Holes");
                int added = database.ScanAndRegisterRetiredHoles();
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssetIfDirty(database);
                EditorUtility.DisplayDialog(
                    "Scan Holes",
                    added > 0
                        ? $"发现并登记了 {added} 个空洞 Id 到弃用列表。"
                        : "没有新的空洞需要登记。",
                    "OK");
                onChanged?.Invoke();
            }
        }
    }

    private void DrawList()
    {
        var retired = database.GetRetiredSlots(string.IsNullOrWhiteSpace(parentFilter) ? null : parentFilter.Trim());
        var recycled = database.GetRecycledSlots(string.IsNullOrWhiteSpace(parentFilter) ? null : parentFilter.Trim());

        if (!string.IsNullOrWhiteSpace(search))
        {
            string s = search.Trim();
            retired = FilterSlots(retired, s);
            recycled = FilterSlots(recycled, s);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField($"弃用中（待回收）: {retired.Count}", EditorStyles.boldLabel);
        DrawColumnHeader(showSelect: true);

        if (retired.Count == 0)
        {
            EditorGUILayout.HelpBox("当前没有弃用 Id。删除 Tag 后会出现在这里。", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < retired.Count; i++)
            {
                DrawRetiredRow(retired[i]);
            }
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField($"可复用池（下次 Add 优先使用）: {recycled.Count}", EditorStyles.boldLabel);
        DrawColumnHeader(showSelect: false);

        if (recycled.Count == 0)
        {
            EditorGUILayout.LabelField("（空）", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < recycled.Count; i++)
            {
                DrawRecycledRow(recycled[i]);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawColumnHeader(bool showSelect)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (showSelect)
            {
                bool newSelectAll = GUILayout.Toggle(selectAllVisible, GUIContent.none, GUILayout.Width(18));
                if (newSelectAll != selectAllVisible)
                {
                    selectAllVisible = newSelectAll;
                    ToggleSelectAllVisible(selectAllVisible);
                }
            }
            else
            {
                GUILayout.Space(22);
            }

            GUILayout.Label("Parent", EditorStyles.miniBoldLabel, GUILayout.Width(180));
            GUILayout.Label("Id", EditorStyles.miniBoldLabel, GUILayout.Width(40));
            GUILayout.Label("Last Path / Note", EditorStyles.miniBoldLabel);
        }
    }

    private void DrawRetiredRow(GameplayTagSiblingSlotInfo slot)
    {
        string key = MakeKey(slot.ParentPath, slot.SiblingId);
        using (new EditorGUILayout.HorizontalScope())
        {
            bool selected = selectedKeys.Contains(key);
            bool next = EditorGUILayout.Toggle(selected, GUILayout.Width(18));
            if (next != selected)
            {
                if (next)
                    selectedKeys.Add(key);
                else
                    selectedKeys.Remove(key);
            }

            EditorGUILayout.SelectableLabel(slot.DisplayParent, GUILayout.Width(180), GUILayout.Height(18));
            EditorGUILayout.SelectableLabel(slot.SiblingId.ToString(), GUILayout.Width(40), GUILayout.Height(18));
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(slot.LastPath) ? "-" : slot.LastPath,
                GUILayout.Height(18));
        }
    }

    private void DrawRecycledRow(GameplayTagSiblingSlotInfo slot)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(22);
            EditorGUILayout.SelectableLabel(slot.DisplayParent, GUILayout.Width(180), GUILayout.Height(18));
            EditorGUILayout.SelectableLabel(slot.SiblingId.ToString(), GUILayout.Width(40), GUILayout.Height(18));
            EditorGUILayout.LabelField("ready to reuse", EditorStyles.miniLabel);
        }
    }

    private void DrawFooter()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField($"Selected: {selectedKeys.Count}");

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(selectedKeys.Count == 0))
            {
                if (GUILayout.Button("Recycle Selected", GUILayout.Width(140), GUILayout.Height(28)))
                {
                    RecycleSelected();
                }
            }

            if (GUILayout.Button("Recycle All Visible", GUILayout.Width(150), GUILayout.Height(28)))
            {
                RecycleAllVisible();
            }

            if (GUILayout.Button("Close", GUILayout.Width(80), GUILayout.Height(28)))
            {
                Close();
            }
        }
    }

    private void ToggleSelectAllVisible(bool select)
    {
        var retired = database.GetRetiredSlots(string.IsNullOrWhiteSpace(parentFilter) ? null : parentFilter.Trim());
        if (!string.IsNullOrWhiteSpace(search))
            retired = FilterSlots(retired, search.Trim());

        selectedKeys.Clear();
        if (!select)
            return;

        for (int i = 0; i < retired.Count; i++)
            selectedKeys.Add(MakeKey(retired[i].ParentPath, retired[i].SiblingId));
    }

    private void RecycleSelected()
    {
        if (selectedKeys.Count == 0)
            return;

        var slots = new List<GameplayTagSiblingSlotInfo>();
        var retired = database.GetRetiredSlots(null);
        var slotByKey = new Dictionary<string, GameplayTagSiblingSlotInfo>(StringComparer.Ordinal);
        for (int i = 0; i < retired.Count; i++)
            slotByKey[MakeKey(retired[i].ParentPath, retired[i].SiblingId)] = retired[i];

        foreach (string key in selectedKeys)
        {
            if (slotByKey.TryGetValue(key, out var slot))
                slots.Add(slot);
        }

        if (!TryRecycleWithReferenceCheck(slots))
            return;

        selectedKeys.Clear();
        selectAllVisible = false;
        onChanged?.Invoke();
    }

    private void RecycleAllVisible()
    {
        var retired = database.GetRetiredSlots(string.IsNullOrWhiteSpace(parentFilter) ? null : parentFilter.Trim());
        if (!string.IsNullOrWhiteSpace(search))
            retired = FilterSlots(retired, search.Trim());

        if (retired.Count == 0)
        {
            EditorUtility.DisplayDialog("Recycle", "当前没有可回收的弃用 Id。", "OK");
            return;
        }

        var slots = new List<GameplayTagSiblingSlotInfo>(retired);

        if (!TryRecycleWithReferenceCheck(slots))
            return;

        selectedKeys.Clear();
        selectAllVisible = false;
        onChanged?.Invoke();
    }

    private bool TryRecycleWithReferenceCheck(
        List<GameplayTagSiblingSlotInfo> slots)
    {
        if (slots == null || slots.Count == 0)
            return false;

        if (!GameplayTagReferenceScanner.TryFindReferencesForRetiredSlots(
                database,
                slots,
                out var hits,
                out var scanError))
        {
            EditorUtility.DisplayDialog("Recycle Blocked", scanError, "OK");
            return false;
        }

        if (hits.Count > 0)
        {
            string body =
                $"检测到 {hits.Count} 处资产仍引用这些 Tag value，回收可能导致错误匹配。\n\n" +
                GameplayTagReferenceScanner.FormatHits(hits) +
                "\n\n仍要强制回收？";

            Debug.LogWarning(body);

            if (!EditorUtility.DisplayDialog(
                    "References Found — Recycle Blocked",
                    body,
                    "Force Recycle",
                    "Cancel"))
            {
                return false;
            }
        }
        else
        {
            if (!EditorUtility.DisplayDialog(
                    "Confirm Recycle",
                    $"将 {slots.Count} 个弃用 siblingId 放入可复用池。\n" +
                    "引用扫描：未发现资产仍序列化这些 value。\n继续？",
                    "Recycle",
                    "Cancel"))
            {
                return false;
            }
        }

        var recycleList = new List<(string parentPath, int siblingId)>(slots.Count);
        for (int i = 0; i < slots.Count; i++)
            recycleList.Add((slots[i].ParentPath, slots[i].SiblingId));

        Undo.RecordObject(database, "Recycle Sibling IDs");
        int count = database.RecycleRetiredIds(recycleList, out var error);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssetIfDirty(database);

        if (count <= 0 && !string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog("Recycle Failed", error, "OK");
            return false;
        }

        ShowNotification(new GUIContent($"Recycled {count} id(s)"));
        return true;
    }

    private static List<GameplayTagSiblingSlotInfo> FilterSlots(
        List<GameplayTagSiblingSlotInfo> source,
        string search)
    {
        var result = new List<GameplayTagSiblingSlotInfo>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            var s = source[i];
            if (s.DisplayParent.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                s.SiblingId.ToString().IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!string.IsNullOrEmpty(s.LastPath) &&
                 s.LastPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                result.Add(s);
            }
        }

        return result;
    }

    private static string MakeKey(string parentPath, int siblingId)
    {
        return (parentPath ?? string.Empty) + "\n" + siblingId;
    }

    private static bool TryParseKey(string key, out string parent, out int siblingId)
    {
        parent = string.Empty;
        siblingId = 0;
        if (string.IsNullOrEmpty(key))
            return false;

        int split = key.LastIndexOf('\n');
        if (split < 0)
            return false;

        parent = key.Substring(0, split);
        return int.TryParse(key.Substring(split + 1), out siblingId);
    }
}
#endif
