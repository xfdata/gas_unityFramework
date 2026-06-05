using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "GAS/GameplayTagDatabase")]
public sealed class GameplayTagDatabase : ScriptableObject
{
    private const int MaxDepth = 4;

    [SerializeField]
    private string generatedCodePath = "";

    [FormerlySerializedAs("Tags")]
    [SerializeField]
    private List<string> tags = new();

    public string GeneratedCodePath => generatedCodePath;
    public IReadOnlyList<string> Tags => tags;

    public bool Contains(string tag)
    {
        tag = NormalizeTag(tag);
        return IndexOf(tag) >= 0;
    }

    public bool AddTag(string tag)
    {
        tag = NormalizeTag(tag);

        if (!IsValidTagPath(tag, out var error))
        {
            Debug.LogError($"非法 Tag: {tag}, reason: {error}");
            return false;
        }

        if (Contains(tag))
        {
            Debug.LogError("Tag 已存在: " + tag);
            return false;
        }

        tags.Add(tag);
        Sort();
        return true;
    }

    public bool RemoveTagRecursive(string tag)
    {
        tag = NormalizeTag(tag);

        if (string.IsNullOrEmpty(tag))
            return false;

        int removed = tags.RemoveAll(t => IsSameOrChild(tag, t));
        if (removed <= 0)
            return false;

        Sort();
        return true;
    }

    public void ClearTags()
    {
        tags.Clear();
    }

    public bool RenameTag(string oldPath, string newName)
    {
        oldPath = NormalizeTag(oldPath);
        newName = NormalizeTag(newName);

        if (string.IsNullOrEmpty(oldPath) || string.IsNullOrEmpty(newName))
            return false;

        if (newName.Contains("."))
        {
            Debug.LogError("Rename 只允许输入当前节点名称，不允许包含 '.'");
            return false;
        }

        if (!IsValidTagSegment(newName, out var segmentError))
        {
            Debug.LogError($"非法 Tag 名称: {newName}, reason: {segmentError}");
            return false;
        }

        bool found = false;
        for (int i = 0; i < tags.Count; i++)
        {
            if (IsSameOrChild(oldPath, tags[i]))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogError("找不到要重命名的 Tag: " + oldPath);
            return false;
        }

        string parent = "";
        int index = oldPath.LastIndexOf('.');
        if (index > 0)
            parent = oldPath.Substring(0, index);

        string newPath = string.IsNullOrEmpty(parent)
            ? newName
            : parent + "." + newName;

        if (oldPath == newPath)
            return false;

        if (!IsValidTagPath(newPath, out var pathError))
        {
            Debug.LogError($"非法目标路径: {newPath}, reason: {pathError}");
            return false;
        }

        var existing = new HashSet<string>(tags, StringComparer.Ordinal);
        var changes = new List<(int index, string newTag)>();

        for (int i = 0; i < tags.Count; i++)
        {
            string oldTag = tags[i];

            if (!IsSameOrChild(oldPath, oldTag))
                continue;

            string suffix = oldTag.Substring(oldPath.Length);
            string newTag = newPath + suffix;

            // 目标路径已被非本次重命名范围内的 Tag 占用
            if (existing.Contains(newTag) && !IsSameOrChild(oldPath, newTag))
            {
                Debug.LogError($"Rename 冲突: {newTag} 已存在");
                return false;
            }

            changes.Add((i, newTag));
        }

        foreach (var change in changes)
        {
            tags[change.index] = change.newTag;
        }

        Sort();
        return true;
    }

    private int IndexOf(string tag)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], tag, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private void Sort()
    {
        tags.Sort(StringComparer.Ordinal);
    }

    private static string NormalizeTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return string.Empty;

        var parts = tag.Split('.');
        for (int i = 0; i < parts.Length; i++)
            parts[i] = parts[i].Trim();

        return string.Join(".", parts);
    }

    private static bool IsSameOrChild(string parent, string path)
    {
        return string.Equals(parent, path, StringComparison.Ordinal)
               || path.StartsWith(parent + ".", StringComparison.Ordinal);
    }

    public static bool IsValidTagPath(string tag, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(tag))
        {
            error = "Tag 不能为空";
            return false;
        }

        var parts = tag.Split('.');

        if (parts.Length > MaxDepth)
        {
            error = $"当前 uint 编码最多支持 {MaxDepth} 层";
            return false;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (!IsValidTagSegment(parts[i], out error))
                return false;
        }

        return true;
    }

    private static bool IsValidTagSegment(string segment, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(segment))
        {
            error = "节点名不能为空";
            return false;
        }

        if (!(char.IsLetter(segment[0]) || segment[0] == '_'))
        {
            error = "节点名必须以字母或下划线开头";
            return false;
        }

        for (int i = 1; i < segment.Length; i++)
        {
            char c = segment[i];
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                error = "节点名只能包含字母、数字、下划线";
                return false;
            }
        }

        return true;
    }
}
