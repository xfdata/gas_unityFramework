using System;
using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Cysharp.Text;
using log4net;
using log4net.Appender;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Framework
{
    public static class Log
    {
        private static ILog log = LogManager.GetLogger("FileLogger");
        private static string initializedLogPath;

        public static void Initialize(string logPath)
        {
            if (logPath == initializedLogPath)
            {
                return;
            }

            initializedLogPath = logPath;

            Application.logMessageReceivedThreaded += onLogMessageReceived;

            var stream = new MemoryStream();

            int length = FileUtils.ReadStreamingAssetAllBytes("log4net.config", null, 0, 0);
            if (length > 0)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(length);
                try
                {
                    FileUtils.ReadStreamingAssetAllBytes("log4net.config", buffer, 0, length);

                    stream.Write(buffer, 0, length);
                    stream.Seek(0, SeekOrigin.Begin);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            GlobalContext.Properties["ApplicationLogPath"] = logPath;
            GlobalContext.Properties["LogFileName"] = "log"; //生成日志的文件名

            log4net.Config.XmlConfigurator.Configure(stream); //加载log4net配置文件

#if UNITY_EDITOR
            EnableLogDebug = EditorPrefs.GetBool("EnableLogDebug", true);
            EnableLogInfo = EditorPrefs.GetBool("EnableLogInfo", true);
            EnableLogWarn = EditorPrefs.GetBool("EnableLogWarn", true);
            EnableLogError = EditorPrefs.GetBool("EnableLogError", true);
            EnableLogDump = EditorPrefs.GetBool("EnableLogDump", true);
#else
            #if LOG_DEBUG
                EnableLogDebug = true;
            #else
                EnableLogDebug = false;
            #endif
            UnityEngine.Debug.Log("EnableLogDebug:"+EnableLogDebug);
            
            #if LOG_INFO
                EnableLogInfo = true;
            #else
                EnableLogInfo = false;
            #endif
            UnityEngine.Debug.Log("EnableLogInfo:"+EnableLogInfo);
            
            #if LOG_WARNING
                EnableLogWarn = true;
            #else
                EnableLogWarn = false;
            #endif
            UnityEngine.Debug.Log("EnableLogWarn:"+EnableLogWarn);
            
            #if LOG_ERROR
                EnableLogError = true;
            #else
                EnableLogError = false;
            #endif
            UnityEngine.Debug.Log("EnableLogError:"+EnableLogError);
            
            #if LOG_DUMP
                EnableLogDump = true;
            #else
                EnableLogDump = false;
            #endif
            UnityEngine.Debug.Log("EnableLogDump:"+EnableLogDump);
#endif
        }

        public static string GetCurrentLogFileName()
        {
            // 获取所有附加器
            var repository = LogManager.GetRepository();
            foreach (IAppender appender in repository.GetAppenders())
            {
                if (appender is RollingFileAppender rollingFileAppender)
                {
                    // 返回当前滚动文件的名称
                    return rollingFileAppender.File;
                }
                else if (appender is FileAppender fileAppender)
                {
                    // 返回文件附加器的名称
                    return fileAppender.File;
                }
            }

            return null; // 如果没有找到合适的附加器，返回 null
        }

        public static void Shutdown()
        {
            LogManager.Shutdown();

            Application.logMessageReceivedThreaded -= onLogMessageReceived;

            initializedLogPath = null;
        }

        [Conditional("SDK_ONEMT")]
        public static void RecordException(string type, string exception)
        {
        }

        private static void onLogMessageReceived(string condition, string stacktrace, LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                {
                    using (var sb = ZString.CreateStringBuilder())
                    {
                        sb.Append(condition);
                        sb.Append("\r\n");
                        sb.Append(stacktrace);
                        string message = sb.ToString();
                        log.Error(message);
                        RecordException("Error", message);
                    }

                    break;
                }
                case LogType.Assert:
                {
                    using (var sb = ZString.CreateStringBuilder())
                    {
                        sb.Append(condition);
                        sb.Append("\r\n");
                        sb.Append(stacktrace);
                        string message = sb.ToString();
                        log.Error(message);
                        RecordException("Assert", message);
                    }

                    break;
                }
                case LogType.Exception:
                {
                    using (var sb = ZString.CreateStringBuilder())
                    {
                        sb.Append(condition);
                        sb.Append("\r\n");
                        sb.Append(stacktrace);
                        string message = sb.ToString();
                        log.Error(message);
                        RecordException("Exception", message);
                    }

                    break;
                }
                case LogType.Warning:
                {
                    using (var sb = ZString.CreateStringBuilder())
                    {
                        sb.Append(condition);
                        sb.Append("\r\n");
                        sb.Append(stacktrace);
                        string message = sb.ToString();
                        log.Warn(message);
                    }

                    break;
                }
                default:
                {
                    using (var sb = ZString.CreateStringBuilder())
                    {
                        sb.Append(condition);
                        sb.Append("\r\n");
                        sb.Append(stacktrace);
                        string message = sb.ToString();
                        log.Debug(message);
                    }

                    break;
                }
            }
        }


        private static bool _enableLogDebug = true;

        public static bool EnableLogDebug
        {
            get { return _enableLogDebug; }
            set
            {
                _enableLogDebug = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool("EnableLogDebug", value);
#endif
            }
        }

        private static bool _enableLogInfo = true;

        public static bool EnableLogInfo
        {
            get { return _enableLogInfo; }
            set
            {
                _enableLogInfo = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool("EnableLogInfo", value);
#endif
            }
        }

        static bool _enableLogWarn = true;

        public static bool EnableLogWarn
        {
            get { return _enableLogWarn; }
            set
            {
                _enableLogWarn = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool("EnableLogWarn", value);
#endif
            }
        }

        static bool _enableLogError = true;

        public static bool EnableLogError
        {
            get { return _enableLogError; }
            set
            {
                _enableLogError = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool("EnableLogError", value);
#endif
            }
        }

        static bool _enableLogDump = true;

        public static bool EnableLogDump
        {
            get { return _enableLogDump; }
            set
            {
                _enableLogDump = value;
#if UNITY_EDITOR
                EditorPrefs.SetBool("EnableLogDump", value);
#endif
            }
        }

#if !UNITY_EDITOR
        [Conditional("LOG_DEBUG")]
#endif
        public static void Debug(string msg)
        {
            if (!EnableLogDebug)
            {
                return;
            }

            UnityEngine.Debug.Log(msg);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_DEBUG")]
#endif
        public static void Debug(object obj)
        {
            if (!EnableLogDebug)
            {
                return;
            }

            UnityEngine.Debug.Log(obj);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_DEBUG")]
#endif
        public static void Debug(string msg, UnityEngine.Object obj)
        {
            if (!EnableLogDebug)
            {
                return;
            }

            UnityEngine.Debug.Log(msg, obj);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_INFO")]
#endif
        public static void Info(string msg)
        {
            if (!EnableLogInfo)
            {
                return;
            }

            UnityEngine.Debug.Log(msg);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_INFO")]
#endif
        public static void Info(string msg, UnityEngine.Object obj)
        {
            if (!EnableLogInfo)
            {
                return;
            }

            UnityEngine.Debug.Log(msg, obj);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_WARNING")]
#endif
        public static void Warn(string msg)
        {
            if (!EnableLogWarn)
            {
                return;
            }

            UnityEngine.Debug.LogWarning(msg);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_WARNING")]
#endif
        public static void Warn(string msg, UnityEngine.Object obj)
        {
            if (!EnableLogWarn)
            {
                return;
            }

            UnityEngine.Debug.LogWarning(msg, obj);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_ERROR")]
#endif
        public static void Error(string msg)
        {
            if (!EnableLogError)
            {
                return;
            }

            UnityEngine.Debug.LogError(msg);
        }

        public static void Error(Exception e)
        {
            if (!EnableLogError)
            {
                return;
            }

            UnityEngine.Debug.LogError(e);
        }
#if !UNITY_EDITOR
        [Conditional("LOG_ERROR")]
#endif
        public static void Error(string msg, UnityEngine.Object obj)
        {
            if (!EnableLogError)
            {
                return;
            }

            UnityEngine.Debug.LogError(msg, obj);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_WARNING")]
#endif
        public static void WarnFormat(string message, params object[] args)
        {
            if (!EnableLogWarn)
            {
                return;
            }

            UnityEngine.Debug.LogWarningFormat(message, args);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_INFO")]
#endif
        public static void InfoFormat(string message, params object[] args)
        {
            if (!EnableLogInfo)
            {
                return;
            }

            UnityEngine.Debug.LogFormat(message, args);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_DEBUG")]
#endif
        public static void DebugFormat(string message, params object[] args)
        {
            if (!EnableLogDebug)
            {
                return;
            }

            UnityEngine.Debug.LogFormat(message, args);
        }

#if !UNITY_EDITOR
        [Conditional("LOG_ERROR")]
#endif
        public static void ErrorFormat(string message, params object[] args)
        {
            if (!EnableLogError)
            {
                return;
            }

            UnityEngine.Debug.LogErrorFormat(message, args);
        }
    }
}
