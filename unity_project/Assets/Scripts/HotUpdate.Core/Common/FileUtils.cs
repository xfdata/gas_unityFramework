using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Framework
{
    public class FileUtils
    {
        public const string AssetsFolderName = "Assets";

        public static string FormatToUnityPath(string path)
        {
            return path.Replace("\\", "/");
        }

        public static string FormatToSysFilePath(string path)
        {
            return path.Replace("/", "\\");
        }

        public static string FullPathToAssetPath(string full_path)
        {
            full_path = FormatToUnityPath(full_path);
            if (!full_path.StartsWith(Application.dataPath))
            {
                return null;
            }
            string ret_path = full_path.Replace(Application.dataPath, "");
            return AssetsFolderName + ret_path;
        }

        public static string GetFileExtension(string path)
        {
            return Path.GetExtension(path).ToLower();
        }

        public static void CheckFileAndCreateDirWhenNeeded(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return;
            }

            string dirPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dirPath) && !Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }
        }

        public static void CheckDirAndCreateWhenNeeded(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                return;
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }

        public static bool SafeWriteAllBytes(string outFile, byte[] outBytes)
        {
            try
            {
                if (string.IsNullOrEmpty(outFile))
                {
                    return false;
                }

                CheckFileAndCreateDirWhenNeeded(outFile);
                if (File.Exists(outFile))
                {
                    File.SetAttributes(outFile, FileAttributes.Normal);
                }
                File.WriteAllBytes(outFile, outBytes);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("SafeWriteAllBytes failed! path = {0} with err = {1}", outFile, ex.Message));
                return false;
            }
        }

        public static bool SafeWriteAllText(string outFile, string text)
        {
            try
            {
                if (string.IsNullOrEmpty(outFile))
                {
                    return false;
                }

                CheckFileAndCreateDirWhenNeeded(outFile);
                if (File.Exists(outFile))
                {
                    File.SetAttributes(outFile, FileAttributes.Normal);
                }
                File.WriteAllText(outFile, text);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("SafeWriteAllText failed! path = {0} with err = {1}", outFile, ex.Message));
                return false;
            }
        }

        public static byte[] SafeReadAllBytes(string inFile)
        {
            try
            {
                if (string.IsNullOrEmpty(inFile))
                {
                    return null;
                }

                if (!File.Exists(inFile))
                {
                    return null;
                }

                File.SetAttributes(inFile, FileAttributes.Normal);
                return File.ReadAllBytes(inFile);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("SafeReadAllBytes failed! path = {0} with err = {1}", inFile, ex.Message));
                return null;
            }
        }

        public static string SafeReadAllText(string inFile)
        {
            try
            {
                if (string.IsNullOrEmpty(inFile))
                {
                    return null;
                }

                if (!File.Exists(inFile))
                {
                    return null;
                }

                File.SetAttributes(inFile, FileAttributes.Normal);
                return File.ReadAllText(inFile);
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("SafeReadAllText failed! path = {0} with err = {1}", inFile, ex.Message));
                return null;
            }
        }

        private static void DeleteDirectory(string dirPath)
        {
            string[] files = Directory.GetFiles(dirPath);
            string[] dirs = Directory.GetDirectories(dirPath);

            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (string dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(dirPath, false);
        }

        public static bool SafeClearDir(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath))
                {
                    return true;
                }

                if (Directory.Exists(folderPath))
                {
                    DeleteDirectory(folderPath);
                }
                Directory.CreateDirectory(folderPath);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("SafeClearDir failed! path = {0} with err = {1}", folderPath, ex.Message));
                return false;
            }
        }

        public static bool SafeDeleteDir(string folderPath)
        {
            try
            {
                if (string.IsNullOrEmpty(folderPath))
                {
                    return true;
                }

                if (Directory.Exists(folderPath))
                {
                    DeleteDirectory(folderPath);
                }
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("SafeDeleteDir failed! path = {0} with err: {1}", folderPath, ex.Message));
                return false;
            }
        }

        public static bool SafeDeleteFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return true;
                }

                if (!File.Exists(filePath))
                {
                    return true;
                }
                File.SetAttributes(filePath, FileAttributes.Normal);
                File.Delete(filePath);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("SafeDeleteFile failed! path = {0} with err: {1}", filePath, ex.Message));
                return false;
            }
        }

        public static bool SafeRenameFile(string sourceFileName, string destFileName)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceFileName))
                {
                    return false;
                }

                if (!File.Exists(sourceFileName))
                {
                    return true;
                }
                SafeDeleteFile(destFileName);
                File.SetAttributes(sourceFileName, FileAttributes.Normal);
                File.Move(sourceFileName, destFileName);
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError(string.Format("SafeRenameFile failed! path = {0} with err: {1}", sourceFileName, ex.Message));
                return false;
            }
        }
        
        public static string FormatFileSize(int size)
        {
            if (size > 1024 * 1024)
            {
                return ZString.Format("{0:0.##} MB", 1.0f * size / 1024 / 1024);
            }
            else if (size > 1024)
            {
                return ZString.Format("{0:0.##} KB", 1.0f * size / 1024);
            }
            else
            {
                return ZString.Concat(size, " byte");
            }
        }

        public static string RelativePath(string rootPath, string path)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

            rootPath = rootPath.Replace('\\', '/');
            path = path.Replace('\\', '/');
#endif
            string newPath = path.Replace(rootPath, "");
            if (newPath.Length > 0 && (newPath[0] == '/' || newPath[0] == '\\'))
            {
                newPath = newPath.Substring(1);
            }
            return newPath;
        }
        public static string RelativePath(DirectoryInfo rootPath, FileSystemInfo path)
        {
            string newPath = path.FullName.Replace(rootPath.FullName, "");
            if (newPath.Length > 0 && (newPath[0] == '/' || newPath[0] == '\\'))
            {
                newPath = newPath.Substring(1);
            }
            return newPath;
        }
        
        public static bool IsFileExists(string fileName)
        {
            return File.Exists(fileName);
        }
        
        public static int ReadPersistentDataAllBytes(string fileName, byte[] buffer, int offset, int count)
        {
            return ReadAllBytes(Path.Combine(Application.persistentDataPath, fileName), buffer, offset, count);
        }
        
        public static int ReadStreamingAssetAllBytes(string fileName, byte[] buffer, int offset, int count)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return XLuaNative.NativeJNIReadAllBytes(fileName, buffer, offset, count);
#else
            return ReadAllBytes(Path.Combine(Application.streamingAssetsPath, fileName), buffer, offset, count);
#endif
        }
        
        public static bool IsAndroidStreamingAssetFile(string fileName)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
        
        public static int ReadAllBytes(string fileName, byte[] buffer, int offset, int count)
        {
            if (!File.Exists(fileName))
            {
                return -1;
            }

            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            
            if (buffer == null)
            {
                return (int)fs.Length;
            }
            
            if (count <= 0) return 0;
            if (offset < 0) offset = 0;
            
            if ((long)offset >= fs.Length)
                return 0;
            
            int toRead = Math.Min(count, buffer.Length);

            fs.Seek(offset, SeekOrigin.Begin);

            int totalRead = 0;
            while (totalRead < toRead)
            {
                int chunk = fs.Read(buffer, totalRead, toRead - totalRead);
                if (chunk <= 0) break; // EOF
                totalRead += chunk;
            }

            return totalRead;
        }

        public static int GetStreamingAssetFileLength(string fileName)
        {
            return ReadStreamingAssetAllBytes(fileName, null, 0, 0);
        }

        public static bool IsStreamingAssetFileExits(string fileName)
        {
            int length = ReadStreamingAssetAllBytes(fileName, null, 0, 0);
            return length >= 0;
        }

        public static long GetFreeDiskSpace()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return 2147483647;
#elif UNITY_IPHONE && !UNITY_EDITOR
            return 2147483647;
#else
            return 2147483647;
#endif
        }
        
        static bool IsFileExcluded(FileInfo file, string[] excludeFileExts)
        {
            if (excludeFileExts == null || excludeFileExts.Length == 0)
            {
                return false;
            }
            int idx = Array.IndexOf(excludeFileExts, file.Extension);
            if (idx == -1)
                return false;
            return true;
        }
        public static void CopyDirectory(string srcDirPath, string destDirPath, string[] excludeFileExts, SearchOption searchOption)
        {
            DirectoryInfo srcDir = new DirectoryInfo(srcDirPath);
            DirectoryInfo destDir = new DirectoryInfo(destDirPath);

            if (!destDir.Exists)
            {
                Log.Debug($"Create Directory {destDir.FullName}");
                destDir.Create();
            }

            foreach (var child in srcDir.EnumerateDirectories("*", searchOption))
            {
                string newPath = child.FullName.Replace(srcDir.FullName, destDir.FullName);
                Directory.CreateDirectory(newPath);

                Log.Debug($"Create Directory {newPath}");
            }

            foreach (var child in srcDir.EnumerateFiles("*", searchOption))
            {
                if (IsFileExcluded(child, excludeFileExts))
                {
                    continue;
                }
                string newPath = child.FullName.Replace(srcDir.FullName, destDir.FullName);
                File.Copy(child.FullName, newPath, true);
                //await CopyFileAsync(child.FullName, newPath);

                Log.Debug($"Copy File {child.FullName} => {newPath}");
            }
        }
        public static async UniTask CopyDirectoryAsync(string srcDirPath, string destDirPath, string[] excludeFileExts, SearchOption searchOption)
        {
            await UniTask.RunOnThreadPool(() => CopyDirectory(srcDirPath, destDirPath, excludeFileExts, searchOption));
        }
        public static UniTask DeleteDirectoryAsync(string dirPath)
        {
            return UniTask.RunOnThreadPool(() =>
            {
                if (Directory.Exists(dirPath))
                {
                    Directory.Delete(dirPath, true);
                }
            });
        }
        public static async Task CopyFileAsync(string srcFile, string destFile)
        {
            using (var src = new FileStream(srcFile, FileMode.Open, FileAccess.Read))
            {
                var dir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                using (var dest = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                {
                    await src.CopyToAsync(dest).ConfigureAwait(false);
                }
            }
        }

        private const int DefaultChunkSize = 1024 * 1024;

        public static bool CopyStreamingAssetFile(
            string srcFile, string destFile, 
            int chunkSize = DefaultChunkSize)
        {
            int length = ReadStreamingAssetAllBytes(srcFile, null, 0, 0);
            if (length <= 0) return false;

            var dir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024);

            int bufSize = Mathf.Min(chunkSize, length);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufSize);

            try
            {
                int pos = 0;
                while (pos < length)
                {
                    int toRead = Math.Min(bufSize, length - pos);
          
                    int readed = ReadStreamingAssetAllBytes(srcFile, buffer, pos, toRead);
                    if (readed != toRead)
                        throw new IOException($"Read size mismatch: expect {toRead}, got {readed} ({srcFile})");

                    fs.Write(buffer, 0, readed);
                    pos += readed;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }

        public static async Task<bool> CopyStreamingAssetFileAsync(
            string srcFile, string destFile, 
            int chunkSize = DefaultChunkSize,
            IProgress<long> progressBytes = null,
            CancellationToken ct = default)
        {
            int length = ReadStreamingAssetAllBytes(srcFile, null, 0, 0);
            if (length < 0) return false;

            var dir = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await using var fs = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);

            int bufSize = Mathf.Min(chunkSize, length);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufSize);

            try
            {
                int pos = 0;
                while (pos < length)
                {
                    ct.ThrowIfCancellationRequested();

                    int toRead = Math.Min(bufSize, length - pos);
          
                    int readed = ReadStreamingAssetAllBytes(srcFile, buffer, pos, toRead);
                    if (readed != toRead)
                        throw new IOException($"Read size mismatch: expect {toRead}, got {readed} ({srcFile})");

                    await fs.WriteAsync(buffer, 0, readed, ct).ConfigureAwait(false);
                    pos += readed;
                    progressBytes?.Report(pos);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return true;
        }
        
        // 合并文件夹
        public static void MergeFolder(string srcFolder, string destFolder)
        {
            if (!Directory.Exists(srcFolder))
            {
                return;
            }
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            DirectoryInfo dir = new DirectoryInfo(srcFolder);
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string targetFilePath = Path.Combine(destFolder, file.Name);
                file.CopyTo(targetFilePath, true);
            }
            DirectoryInfo[] dirs = dir.GetDirectories();
            foreach (DirectoryInfo subDir in dirs)
            {
                string newDestFolder = Path.Combine(destFolder, subDir.Name);
                MergeFolder(subDir.FullName, newDestFolder);
            }
        }
        public static void SelectFileInExplorer(string path)
        {
#if UNITY_EDITOR_WIN
            Process.Start("explorer.exe", "/select," + path);
#elif UNITY_EDITOR && UNITY_STANDALONE_OSX
            Process.Start("open", "-R " + path);
#endif
        }

        public static void OpenFile(string fileName)
        {
            System.Diagnostics.Process.Start(fileName);
        }
        
        public static long GetFileSize(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return -1;
            }
            FileInfo fileInfo = new FileInfo(fileName);
            return fileInfo.Length;
        }
        
    }
}

