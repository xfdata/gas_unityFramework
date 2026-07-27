#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;

public static class UIGeneratedCodeWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static bool WriteIfChanged(string path, string content)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Generated code path is empty.", nameof(path));

        content ??= string.Empty;
        if (File.Exists(path) && string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal))
            return false;

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, content, Utf8NoBom);
            if (File.Exists(path))
                File.Replace(temporaryPath, path, null);
            else
                File.Move(temporaryPath, path);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return true;
    }

    public static void RefreshIfChanged(bool changed)
    {
        if (changed)
            AssetDatabase.Refresh();
    }
}
#endif
