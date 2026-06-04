#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class RagCodeKnowledgeBuilderWindow : EditorWindow
{
    private const string IncludeDirsKey = "RAG.CodeKnowledge.IncludeDirs";
    private const string PythonPathKey = "RAG.CodeKnowledge.PythonPath";
    private const string ToolsDirKey = "RAG.CodeKnowledge.ToolsDir";
    private const string OutDirKey = "RAG.CodeKnowledge.OutDir";
    private const string IncludeClassContentKey = "RAG.CodeKnowledge.IncludeClassContent";

    private readonly List<string> includeDirs = new List<string>();
    private Vector2 scroll;
    private string pythonPath = "python";
    private string toolsDir;
    private string outDir = "ProjectKnowledge/raw";
    private bool includeClassContent;
    private string lastOutput = string.Empty;
    private bool isRunning;
    private Process process;
    private StringBuilder stdout;
    private StringBuilder stderr;

    [MenuItem("Tools/RAG/Code Knowledge Builder")]
    public static void Open()
    {
        GetWindow<RagCodeKnowledgeBuilderWindow>("Code Knowledge");
    }

    private void OnEnable()
    {
        pythonPath = EditorPrefs.GetString(PythonPathKey, "python");
        toolsDir = EditorPrefs.GetString(ToolsDirKey, GuessToolsDir());
        outDir = EditorPrefs.GetString(OutDirKey, "ProjectKnowledge/raw");
        includeClassContent = EditorPrefs.GetBool(IncludeClassContentKey, false);

        includeDirs.Clear();
        var savedIncludeDirs = EditorPrefs.GetString(IncludeDirsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(savedIncludeDirs))
        {
            includeDirs.AddRange(
                savedIncludeDirs
                    .Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(NormalizeUnityPath)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct());
        }
    }

    private void OnDisable()
    {
        SavePrefs();
        StopWatchingProcess();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("RAG Code Knowledge", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        using (new EditorGUI.DisabledScope(isRunning))
        {
            DrawPathSettings();
            EditorGUILayout.Space(8f);
            DrawIncludeDirs();
            EditorGUILayout.Space(8f);
            DrawBuildOptions();
        }

        EditorGUILayout.Space(8f);
        DrawActions();
        EditorGUILayout.Space(8f);
        DrawOutput();

        EditorGUILayout.EndScrollView();
    }

    private void DrawPathSettings()
    {
        EditorGUILayout.LabelField("Paths", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            pythonPath = EditorGUILayout.TextField("Python", pythonPath);
            if (GUILayout.Button("...", GUILayout.Width(32f)))
            {
                var selected = EditorUtility.OpenFilePanel("Select Python", string.Empty, "exe");
                if (!string.IsNullOrEmpty(selected))
                    pythonPath = selected;
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            toolsDir = EditorGUILayout.TextField("Tools Dir", toolsDir);
            if (GUILayout.Button("...", GUILayout.Width(32f)))
            {
                var selected = EditorUtility.OpenFolderPanel("Select tools directory", toolsDir, string.Empty);
                if (!string.IsNullOrEmpty(selected))
                    toolsDir = selected;
            }
        }

        outDir = EditorGUILayout.TextField("Output Dir", outDir);
    }

    private void DrawIncludeDirs()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Include Scripts Dirs", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Add Selected", GUILayout.Width(104f)))
                AddSelectedProjectFolders();

            if (GUILayout.Button("Browse", GUILayout.Width(72f)))
                BrowseAndAddIncludeDir();
        }

        if (includeDirs.Count == 0)
        {
            EditorGUILayout.HelpBox("No include directory selected. The builder will scan the whole project except exclude_dirs.", MessageType.Info);
            return;
        }

        for (var i = 0; i < includeDirs.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                includeDirs[i] = NormalizeUnityPath(EditorGUILayout.TextField(includeDirs[i]));
                if (GUILayout.Button("-", GUILayout.Width(24f)))
                {
                    includeDirs.RemoveAt(i);
                    i--;
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Clear", GUILayout.Width(72f)))
                includeDirs.Clear();
        }
    }

    private void DrawBuildOptions()
    {
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        includeClassContent = EditorGUILayout.Toggle("Include Class Content", includeClassContent);
    }

    private void DrawActions()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(isRunning))
            {
                if (GUILayout.Button("Build", GUILayout.Height(30f)))
                    Build();
            }

            using (new EditorGUI.DisabledScope(!isRunning))
            {
                if (GUILayout.Button("Stop", GUILayout.Width(72f), GUILayout.Height(30f)))
                    StopProcess();
            }
        }
    }

    private void DrawOutput()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.SelectableLabel(
                string.IsNullOrEmpty(lastOutput) ? "No output yet." : lastOutput,
                GUILayout.MinHeight(100f));
        }
    }

    private void Build()
    {
        SavePrefs();

        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var scriptPath = Path.Combine(toolsDir, "rag", "build_code_kb_raw.py");
        var configDir = Path.Combine(toolsDir, "rag", "config");

        if (!File.Exists(scriptPath))
        {
            EditorUtility.DisplayDialog("RAG", "Cannot find build_code_kb_raw.py. Please check Tools Dir.", "OK");
            return;
        }

        if (!Directory.Exists(configDir))
        {
            EditorUtility.DisplayDialog("RAG", "Cannot find rag/config. Please check Tools Dir.", "OK");
            return;
        }

        var args = new List<string>
        {
            Quote(scriptPath),
            Quote(projectRoot),
            "--config",
            Quote(configDir),
            "--out",
            Quote(outDir)
        };

        var validIncludeDirs = includeDirs
            .Select(NormalizeUnityPath)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (validIncludeDirs.Count > 0)
        {
            args.Add("--include");
            args.Add(Quote(string.Join(",", validIncludeDirs)));
        }

        if (includeClassContent)
            args.Add("--include-class-content");

        stdout = new StringBuilder();
        stderr = new StringBuilder();
        lastOutput = "Running...\n" + pythonPath + " " + string.Join(" ", args);

        var startInfo = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = string.Join(" ", args),
            WorkingDirectory = toolsDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        try
        {
            process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => AppendLine(stdout, e.Data);
            process.ErrorDataReceived += (_, e) => AppendLine(stderr, e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            isRunning = true;
            EditorApplication.update += WatchProcess;
        }
        catch (Exception ex)
        {
            isRunning = false;
            lastOutput = ex.ToString();
            Debug.LogError(lastOutput);
        }
    }

    private void WatchProcess()
    {
        if (process == null)
            return;

        if (!process.HasExited)
        {
            Repaint();
            return;
        }

        var exitCode = process.ExitCode;
        process.WaitForExit();

        var output = stdout == null ? string.Empty : stdout.ToString();
        var error = stderr == null ? string.Empty : stderr.ToString();
        lastOutput = BuildFinalOutput(exitCode, output, error);

        if (exitCode == 0)
        {
            Debug.Log(lastOutput);
            AssetDatabase.Refresh();
        }
        else
        {
            Debug.LogError(lastOutput);
        }

        StopWatchingProcess();
        Repaint();
    }

    private void StopProcess()
    {
        if (process == null || process.HasExited)
            return;

        try
        {
            process.Kill();
            lastOutput = "Build stopped.";
        }
        catch (Exception ex)
        {
            lastOutput = ex.ToString();
            Debug.LogError(lastOutput);
        }
        finally
        {
            StopWatchingProcess();
            Repaint();
        }
    }

    private void StopWatchingProcess()
    {
        EditorApplication.update -= WatchProcess;
        isRunning = false;

        if (process != null)
        {
            process.Dispose();
            process = null;
        }
    }

    private void AddSelectedProjectFolders()
    {
        foreach (var obj in Selection.objects)
        {
            var assetPath = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(assetPath) || !AssetDatabase.IsValidFolder(assetPath))
                continue;

            AddIncludeDir(assetPath);
        }
    }

    private void BrowseAndAddIncludeDir()
    {
        var selected = EditorUtility.OpenFolderPanel("Select Scripts Directory", Application.dataPath, string.Empty);
        if (string.IsNullOrEmpty(selected))
            return;

        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var relative = MakeProjectRelativePath(projectRoot, selected);
        if (string.IsNullOrEmpty(relative))
        {
            EditorUtility.DisplayDialog("RAG", "Please select a folder inside this Unity project.", "OK");
            return;
        }

        AddIncludeDir(relative);
    }

    private void AddIncludeDir(string path)
    {
        var normalized = NormalizeUnityPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        if (!includeDirs.Contains(normalized))
            includeDirs.Add(normalized);
    }

    private static string MakeProjectRelativePath(string projectRoot, string fullPath)
    {
        var root = NormalizeSystemPath(projectRoot).TrimEnd('/') + "/";
        var path = NormalizeSystemPath(fullPath).TrimEnd('/');

        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        return NormalizeUnityPath(path.Substring(root.Length));
    }

    private static string GuessToolsDir()
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var siblingTools = Path.GetFullPath(Path.Combine(projectRoot, "..", "tools"));
        if (Directory.Exists(Path.Combine(siblingTools, "rag")))
            return siblingTools;

        var innerTools = Path.Combine(projectRoot, "tools");
        return Directory.Exists(Path.Combine(innerTools, "rag")) ? innerTools : siblingTools;
    }

    private static string NormalizeUnityPath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
    }

    private static string NormalizeSystemPath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void AppendLine(StringBuilder builder, string line)
    {
        if (builder == null || line == null)
            return;

        builder.AppendLine(line);
    }

    private static string BuildFinalOutput(int exitCode, string output, string error)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Exit Code: " + exitCode);

        if (!string.IsNullOrWhiteSpace(output))
        {
            builder.AppendLine();
            builder.AppendLine("stdout:");
            builder.AppendLine(output.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            builder.AppendLine();
            builder.AppendLine("stderr:");
            builder.AppendLine(error.TrimEnd());
        }

        return builder.ToString();
    }

    private void SavePrefs()
    {
        EditorPrefs.SetString(PythonPathKey, pythonPath ?? "python");
        EditorPrefs.SetString(ToolsDirKey, toolsDir ?? string.Empty);
        EditorPrefs.SetString(OutDirKey, outDir ?? "ProjectKnowledge/raw");
        EditorPrefs.SetBool(IncludeClassContentKey, includeClassContent);
        EditorPrefs.SetString(IncludeDirsKey, string.Join("|", includeDirs.Select(NormalizeUnityPath)));
    }
}
#endif
