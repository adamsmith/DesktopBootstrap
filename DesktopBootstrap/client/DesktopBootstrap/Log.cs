using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using NLog.Targets;

namespace DesktopBootstrap {

    // Ordering of log levels: Trace, Debug, Info, Warn, Error, Fatal

    public static class Log {

        // TODO: end-of-the-road exception handling fallbacks, e.g. up to MessageBox (e.g. in case of OutOfMemory condition)


        // Our logging strategy/targets:
        // 
        //      1) OutputDebugString (only if registry flag is set) -- everything, formatted with "DesktopBootstrap"
        //      2) Log files
        //      3) Console.WriteLine() if debugging
        //      4) Exception reporting (for now just writing to another log file; will have to read this somehow)

        #region Logging Init

        internal static void Init() {
            // set up NLog
            InitNLog();

            // set up runtime exception handling
            Application.ThreadException += Application_ThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        internal static void InitNLog() {
            var config = new LoggingConfiguration();

            try {
                ConfigurationItemFactory.Default = new ConfigurationItemFactory();
                foreach (var type in typeof(Logger).Assembly.GetTypes()) {
                    ConfigurationItemFactory.Default.RegisterType(type, string.Empty);
                }
            } catch (ReflectionTypeLoadException rtle) {
                // NLog has a bug that manifests itself on .NET framework 2.0 with no service pack
                // wherein when it does its own type registering, it can't handle types that depend
                // on a type in an assembly that hasn't been loaded yet.
                // See: http://nlog-project.org/forum#nabble-td5542525
                // Also: http://msdn.microsoft.com/en-us/library/system.reflection.assembly.gettypes.aspx

                // Start over with a fresh ConfigurationItemFactory
                ConfigurationItemFactory.Default = new ConfigurationItemFactory();
                foreach (var type in rtle.Types) {
                    if (type != null) {
                        ConfigurationItemFactory.Default.RegisterType(type, string.Empty);
                    }
                }
            }

            ConfigurationItemFactory.Default.LayoutRenderers.RegisterDefinition("dateutc",
                typeof(DateUtcLayoutRenderer));
            ConfigurationItemFactory.Default.LayoutRenderers.RegisterDefinition("messagewithexceptioninfo",
                typeof(MessageWithExceptionInfoLayoutRenderer));

            var versionString = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            var basicLayout = "[${dateutc}] DesktopBootstrap-" + versionString +
                ": ${level}: ${messagewithexceptioninfo}";

            var rootLogDir = LibraryIO.FindWritableDirectory(CommonDirectories.LocalAppData,
                CommonDirectories.CurrentExecutingDirectory, CommonDirectories.Temp);


            // Create targets and rules
            var outputDebugStringTarget = new OutputDebugStringTarget();
            outputDebugStringTarget.Layout = basicLayout;
            config.AddTarget("outputdebugstring", outputDebugStringTarget);
            if (Library.IsDebugMode()) {
                config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, outputDebugStringTarget));
            }

            var consoleTarget = new ColoredConsoleTarget();
            consoleTarget.Layout = basicLayout;
            config.AddTarget("console", consoleTarget);
            if (Debugger.IsAttached) {
                config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, consoleTarget));
            }

            if (rootLogDir != null) {
                var basicLogFileTarget = new FileTarget();
                var logDirectory = Path.Combine(rootLogDir, "Logs");
                basicLogFileTarget.FileName = Path.Combine(logDirectory, "DesktopBootstrap.log");
                basicLogFileTarget.ArchiveFileName = Path.Combine(logDirectory, "DesktopBootstrap-{#}.log");
                basicLogFileTarget.ArchiveAboveSize = 1024 * 1024; // 1 MB
                basicLogFileTarget.ArchiveNumbering = ArchiveNumberingMode.Rolling;
                basicLogFileTarget.MaxArchiveFiles = 14;
                basicLogFileTarget.Encoding = UTF8Encoding.UTF8;
                basicLogFileTarget.ConcurrentWrites = false;
                basicLogFileTarget.KeepFileOpen = false;
                basicLogFileTarget.Layout = basicLayout;
                config.AddTarget("file", basicLogFileTarget);
                config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, basicLogFileTarget));

                var errorLogFileTarget = new FileTarget();
                var errorLogDirectory = Path.Combine(rootLogDir, "ErrorLogs");
                errorLogFileTarget.FileName = Path.Combine(logDirectory, "DesktopBootstrapError.log");
                errorLogFileTarget.ArchiveFileName = Path.Combine(logDirectory, "DesktopBootstrapError-{#}.log");
                errorLogFileTarget.ArchiveAboveSize = 1024 * 1024; // 1 MB
                errorLogFileTarget.ArchiveNumbering = ArchiveNumberingMode.Rolling;
                errorLogFileTarget.MaxArchiveFiles = 14;
                errorLogFileTarget.Encoding = UTF8Encoding.UTF8;
                errorLogFileTarget.ConcurrentWrites = true;
                errorLogFileTarget.KeepFileOpen = false;
                errorLogFileTarget.Layout = basicLayout;
                config.AddTarget("errorfile", errorLogFileTarget);
                config.LoggingRules.Add(new LoggingRule("*", LogLevel.Error, errorLogFileTarget));
            }


            // Activate the configuration
            LogManager.ThrowExceptions = false; // swallow logging exceptions
            LogManager.Configuration = config;
        }

        #endregion

        #region Top Level Exception Handlers

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            if (e.ExceptionObject is Exception) {
                // see comments on http://msdn.microsoft.com/en-us/library/system.unhandledexceptioneventargs.exceptionobject.aspx
                Log.Logger.ErrorException(string.Format(
                    "AppDomain top level unhandled exception (Terminating={0})", e.IsTerminating),
                    e.ExceptionObject as Exception);
            } else {
                Log.Logger.Error("AppDomain top level unhandled exception", e,
                    new OtherInfo("Terminating", e.IsTerminating));
            }
        }

        private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e) {
            Log.Logger.ErrorException("Application top level unhandled exception", e.Exception);
        }

        #endregion

        public static Logger Logger {
            get { return LogManager.GetLogger("Foo"); }
        }

    }

    #region Date UTC Layout Renderer

    /// <summary>
    /// Our own layout renderer for DateTime in UTC, since the one provided by NLog is local time only.
    /// </summary>
    [LayoutRenderer("dateutc")]
    [ThreadAgnostic]
    public class DateUtcLayoutRenderer : LayoutRenderer {

        /// <summary>
        /// Renders the current date and appends it to the specified <see cref="StringBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="StringBuilder"/> to append the rendered data to.</param>
        /// <param name="logEvent">Logging event.</param>
        protected override void Append(StringBuilder builder, LogEventInfo logEvent) {
            builder.Append(logEvent.TimeStamp.ToUniversalTime().ToString("dd/MMM/yyyy HH:mm:ss.ffff",
                CultureInfo.InvariantCulture));
        }
    }

    #endregion

    #region Message-With-Exception Layout Renderer

    // 99% of the code in here is about describing exceptions.

    /// <summary>
    /// Our own layout renderer for including exception info, since the default exception renderer excludes 
    /// a ton of info, including e.g. InnerException's.
    /// </summary>
    [LayoutRenderer("messagewithexceptioninfo")]
    [ThreadAgnostic]
    public class MessageWithExceptionInfoLayoutRenderer : LayoutRenderer {

        protected override void Append(StringBuilder builder, LogEventInfo logEvent) {
            builder.Append(logEvent.FormattedMessage);

            // build and use extended info if there's an exception or there's some Parameter that is not
            // just the Message.
            bool useExtendedInfo = (logEvent.Exception != null ||
                (logEvent.Parameters != null && logEvent.Parameters.Length > 0 &&
                (logEvent.Parameters.Length > 1 || !logEvent.FormattedMessage.Equals(logEvent.Parameters[0]))));

            if (useExtendedInfo) {
                var otherInfo = new List<OtherInfo>();

                if (logEvent.Exception != null) {
                    DescribeExecutionEnvironment(otherInfo);
                    DescribeException(otherInfo, logEvent.Exception);
                }
                
                if (logEvent.Parameters != null && logEvent.Parameters.Length > 0) {
                    for (int i = 0; i < logEvent.Parameters.Length; i++) {
                        if (logEvent.FormattedMessage.Equals(logEvent.Parameters[i])) {
                            continue; // this won't add any info / be useful
                        }

                        if (logEvent.Parameters[i] is OtherInfo) {
                            // just add it to the top level list of OtherInfo's
                            otherInfo.Add((OtherInfo)logEvent.Parameters[i]);

                        } else if (logEvent.Parameters[i] is IEnumerable<OtherInfo>) {
                            otherInfo.AddRange((IEnumerable<OtherInfo>)logEvent.Parameters[i]);

                        } else if (logEvent.Parameters[i] is Exception) {
                            var exceptionInfo = new List<OtherInfo>();
                            DescribeException(exceptionInfo, (Exception)logEvent.Parameters[i]);
                            otherInfo.Add(new OtherInfo("Parameter" + i, exceptionInfo));

                        } else {
                            otherInfo.Add(new OtherInfo("Parameter" + i, logEvent.Parameters[i].ToString()));
                        }
                    }
                }

                builder.Append(" [");
                builder.Append(SerializeKeyValueList(otherInfo));
                builder.Append("]");
            }
        }

        private static void DescribeExecutionEnvironment(List<OtherInfo> targetList) {
            targetList.Add(new OtherInfo("ThreadName", Thread.CurrentThread.Name));
            targetList.Add(new OtherInfo("ThreadCulture", Thread.CurrentThread.CurrentCulture.EnglishName));
            targetList.Add(new OtherInfo("IsBackgroundThread", Thread.CurrentThread.IsBackground));
            targetList.Add(new OtherInfo("IsThreadPoolThread", Thread.CurrentThread.IsThreadPoolThread));
        }

        // Recursively describe exceptions and their InnerExceptions.
        private static void DescribeException(List<OtherInfo> targetList, Exception e) {
            targetList.Add(new OtherInfo("Message", e.Message));
            targetList.Add(new OtherInfo("StackTrace", e.StackTrace));
            targetList.Add(new OtherInfo("Source", e.Source));
            targetList.Add(new OtherInfo("Type", e.GetType().Name));

            if (e is COMException) {
                var comException = (COMException)e;
                targetList.Add(new OtherInfo("ErrorCode", comException.ErrorCode));
            
            } else if (e is FileNotFoundException) {
                var fnfe = (FileNotFoundException)e;
                targetList.Add(new OtherInfo("FileName", fnfe.FileName));
                targetList.Add(new OtherInfo("FusionLog", fnfe.FusionLog));

            } else if (e is IOException || e is InvalidCastException) {
                // BOTH IOException and InvalidCastException have proteched HResult properties that have become
                // public in .NET 4.0.  But we're on .NET 2.0 so we'll have to do some reflection to try to get 
                // them.

                try {
                    var property = e.GetType().GetProperty("HResult", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                    if (property != null) {
                        var hResult = property.GetValue(e, null);
                        if (hResult != null) {
                            targetList.Add(new OtherInfo("HResult", hResult.ToString()));
                        }
                    }
                } catch (Exception eHResult) {
                    targetList.Add(new OtherInfo("HResult", "Exception getting HResult: " + eHResult.Message));
                }
            }

            // Recurse to describe InnerException's.  Prepend the resulting keys with "Inner" before
            // adding them to our targetList.
            if (e.InnerException != null) {
                var innerExceptionList = new List<OtherInfo>();
                DescribeException(innerExceptionList, e.InnerException);
                foreach (var x in innerExceptionList) {
                    targetList.Add(new OtherInfo("Inner" + x.Key, x.Value));
                }
            }
        }

        // dict.Values can only contain string's and List<OtherInfo>'s.  the latter are recursively
        // converted into xml trees
        private static string SerializeKeyValueList(List<OtherInfo> list) {
            try {
                var xmlDoc = new XmlDocument();
                var rootNode = xmlDoc.CreateElement("OtherInfo");
                xmlDoc.AppendChild(rootNode);

                SerializeKeyValueList(list, rootNode, xmlDoc);

                return xmlDoc.OuterXml;
            } catch (Exception e) {
                return "Exception serializing dictionary: " + e.Message;
            }
        }

        // recursively convert dictionaries into xml trees
        private static void SerializeKeyValueList(List<OtherInfo> list, XmlElement parentNode, XmlDocument rootDocument) {
            var duplicateKeyTracker = new Dictionary<string, bool>();  // .NET 2.0 doesn't have HashSet<T>

            foreach (var pair in list) {
                if (duplicateKeyTracker.ContainsKey(pair.Key)) {
                    // ERROR!
                    continue;
                }
                duplicateKeyTracker.Add(pair.Key, true);

                var pairNode = rootDocument.CreateElement(pair.Key);

                if (pair.Value == null) {
                    pairNode.InnerText = string.Empty;
                } else if (pair.Value is string) {
                    pairNode.InnerText = (string)pair.Value;
                } else if (pair.Value is List<OtherInfo>) {
                    SerializeKeyValueList((List<OtherInfo>)pair.Value, pairNode, rootDocument);
                } else {
                    pairNode.InnerText = pair.Value.ToString();
                }

                parentNode.AppendChild(pairNode);
            }
        }
    }

    #endregion

    #region OtherInfo

    public class OtherInfo {

        public OtherInfo() {
        }

        public OtherInfo(string key, object value) {
            Key = key;
            Value = value;
        }

        public string Key { get; set; }
        public object Value { get; set; }
    }

    #endregion

}