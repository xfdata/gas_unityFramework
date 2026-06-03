#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

public sealed class GameplayTagTreeView : TreeView
{
    private readonly GameplayTagDatabase db;
    private readonly Dictionary<int, string> idToPath = new();
    private readonly HashSet<int> usedIds = new();

    public GameplayTagTreeView(TreeViewState state, GameplayTagDatabase db)
        : base(state)
    {
        this.db = db;

        showBorder = true;
        showAlternatingRowBackgrounds = true;

        Reload();
    }

    protected override TreeViewItem BuildRoot()
    {
        idToPath.Clear();
        usedIds.Clear();
        usedIds.Add(0);

        var root = new TreeViewItem
        {
            id = 0,
            depth = -1,
            displayName = "Root"
        };

        var lookup = new Dictionary<string, TreeViewItem>(StringComparer.Ordinal);

        foreach (var tag in db.Tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
                continue;

            var parts = tag.Split('.');
            string path = "";
            TreeViewItem parent = root;

            for (int i = 0; i < parts.Length; i++)
            {
                path = string.IsNullOrEmpty(path)
                    ? parts[i]
                    : path + "." + parts[i];

                if (!lookup.TryGetValue(path, out var item))
                {
                    int id = AllocateStableId(path);

                    item = new TreeViewItem
                    {
                        id = id,
                        displayName = parts[i]
                    };

                    parent.AddChild(item);
                    lookup.Add(path, item);
                    idToPath.Add(id, path);
                }

                parent = item;
            }
        }

        root.children ??= new List<TreeViewItem>();

        SetupDepthsFromParentsAndChildren(root);
        return root;
    }

    public void DirtyReload()
    {
        EditorUtility.SetDirty(db);
        Reload();
    }

    protected override bool CanMultiSelect(TreeViewItem item)
    {
        return false;
    }

    protected override bool CanRename(TreeViewItem item)
    {
        return item != null && item.depth >= 0;
    }

    protected override void RenameEnded(RenameEndedArgs args)
    {
        if (!args.acceptedRename)
            return;

        var item = FindItem(args.itemID, rootItem);
        if (item == null)
            return;

        string oldPath = GetFullPath(item);
        string newName = args.newName?.Trim();

        if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newName))
            return;

        if (string.Equals(item.displayName, newName, StringComparison.Ordinal))
            return;

        Undo.RecordObject(db, "Rename Gameplay Tag");

        if (db.RenameTag(oldPath, newName))
        {
            DirtyReload();
        }
    }

    protected override void ContextClickedItem(int id)
    {
        var item = FindItem(id, rootItem);
        if (item == null)
            return;

        string path = GetFullPath(item);
        if (string.IsNullOrEmpty(path))
            return;

        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("Add Child"), false, () =>
        {
            GameplayAddTagWindow.Show(path, db, DirtyReload);
        });

        menu.AddItem(new GUIContent("Rename"), false, () =>
        {
            BeginRename(item);
        });

        menu.AddSeparator("");

        menu.AddItem(new GUIContent("Delete Recursive"), false, () =>
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Delete Gameplay Tag",
                $"Delete '{path}' and all children?",
                "Delete",
                "Cancel");

            if (!confirm)
                return;

            Undo.RecordObject(db, "Delete Gameplay Tag");

            if (db.RemoveTagRecursive(path))
            {
                DirtyReload();
            }
        });

        menu.ShowAsContext();
    }

    protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
    {
        if (string.IsNullOrEmpty(search))
            return true;

        string path = GetFullPath(item);

        return path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private string GetFullPath(TreeViewItem item)
    {
        if (item == null)
            return string.Empty;

        return idToPath.TryGetValue(item.id, out var path)
            ? path
            : string.Empty;
    }

    private int AllocateStableId(string path)
    {
        int id = StableHash(path);

        while (id == 0 || usedIds.Contains(id))
        {
            unchecked
            {
                id++;
            }
        }

        usedIds.Add(id);
        return id;
    }

    private static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;

            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
#endif