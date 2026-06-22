// /Data/Scripts/BlockRenaming/MambaLogger.cs
// MAMBA MambaLogger/MODULE_TYPE

using System;
using System.IO;
using Sandbox.ModAPI;
using VRage.Utils;

namespace BlockRenaming
{
    public static class MambaLogger
    {
        private static TextWriter _writer = null;
        private static string _modNamespace = "BlockRenaming";
        private static string _modVersion = "1.0.0";
        private static bool _isDebugMode = false;

        /// <summary>
        /// Initializes the logger cleanly using hardcoded context to fully bypass SE sandbox restrictions.
        /// </summary>
        public static void Init(string version, bool debugMode = false)
        {
            try
            {
                _modVersion = version;
                _isDebugMode = debugMode;

                // File name pattern strictly compatible with sandbox Storage API
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                string logFileName = string.Format("{0}_BlockRenamerCore_v{1}.log", timestamp, _modVersion);

                // SE ModAPI local storage file initialization (returns TextWriter)
                _writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(logFileName, typeof(MambaLogger));

                // Standardized log header template
                if (_writer != null)
                {
                    _writer.WriteLine("# ============================================================");
                    _writer.WriteLine(string.Format("# BlockRenamerCore v{0}", _modVersion));
                    _writer.WriteLine(string.Format("# Session started : {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
                    _writer.WriteLine(string.Format("# Log file        : {0}", logFileName));
                    _writer.WriteLine("# ============================================================");
                    _writer.WriteLine();
                    _writer.Flush();
                }
                
                Info("Logger initialized successfully.");
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLine(string.Format("[{0}] ERROR: Could not initialize logger: {1}", _modNamespace, ex.Message));
            }
        }

        public static void Info(string message)
        {
            Log("INFO   ", message);
        }

        public static void Debug(string message)
        {
            if (_isDebugMode)
                Log("DEBUG  ", message);
        }

        public static void Warn(string message)
        {
            Log("WARNING", message);
        }

        public static void Error(string message)
        {
            Log("ERROR  ", message);
        }

        public static void Exception(string context, Exception ex)
        {
            Log("ERROR  ", string.Format("!!! EXCEPTION inside [{0}] !!!", context));
            Log("ERROR  ", string.Format("Message: {0}", ex.Message));
            Log("ERROR  ", string.Format("Stack Trace:\n{0}", ex.StackTrace));
            if (ex.InnerException != null)
            {
                Log("ERROR  ", string.Format("Inner Exception: {0}", ex.InnerException.Message));
            }
        }

        private static void Log(string level, string message)
        {
            if (_writer == null) return;
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                _writer.WriteLine(string.Format("[{0}] [{1}] {2} - {3}", timestamp, level, _modNamespace, message));
                _writer.Flush();
            }
            catch { }
        }

        public static void Close()
        {
            try
            {
                if (_writer != null)
                {
                    Log("INFO   ", "Session ended. Closing log file.");
                    _writer.Flush();
                    _writer.Close();
                    _writer = null;
                }
            }
            catch { }
        }
    }
}