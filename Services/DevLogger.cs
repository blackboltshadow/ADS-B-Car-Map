using AdsbCarMap.Data;

namespace AdsbCarMap.Services
{
    /// <summary>
    /// Entry point for reporting unexpected events. Every event goes to the Android log, and to the developer log when its level is enabled in the settings. Never throws.
    /// </summary>
    public static class DevLogger
    {
        /// <summary>
        /// The store of the developer log, created at the first use. It is kept here and not in <see cref="AppServices"/>, so that reporting an event never depends on the initialization of the other services.
        /// </summary>
        private static readonly Lazy<DevLogStore> StoreInstance = new Lazy<DevLogStore>(() => new DevLogStore());

        /// <summary>
        /// Set to 1 once the global exception handlers are installed.
        /// </summary>
        private static int handlersInstalled;

        /// <summary>
        /// The store of the developer log.
        /// </summary>
        public static DevLogStore Store => StoreInstance.Value;

        /// <summary>
        /// Reports a handled situation worth knowing about.
        /// </summary>
        /// <param name="tag">Part of the application that reports the event.</param>
        /// <param name="message">Description of the event.</param>
        public static void Info(string tag, string message)
        {
            Report(DevLogLevel.Info, tag, message, null, false);
        }

        /// <summary>
        /// Reports something that went wrong while the application continued.
        /// </summary>
        /// <param name="tag">Part of the application that reports the event.</param>
        /// <param name="message">Description of the event.</param>
        /// <param name="exception">Exception that caused the event, or null.</param>
        public static void Warn(string tag, string message, Exception? exception = null)
        {
            Report(DevLogLevel.Warning, tag, message, exception, false);
        }

        /// <summary>
        /// Reports an unexpected failure.
        /// </summary>
        /// <param name="tag">Part of the application that reports the event.</param>
        /// <param name="message">Description of the event.</param>
        /// <param name="exception">Exception that caused the event, or null.</param>
        public static void Error(string tag, string message, Exception? exception = null)
        {
            Report(DevLogLevel.Error, tag, message, exception, false);
        }

        /// <summary>
        /// Reports a failure that may end the process and writes the log to the file at once.
        /// </summary>
        /// <param name="tag">Part of the application that reports the event.</param>
        /// <param name="message">Description of the event.</param>
        /// <param name="exception">Exception that caused the event, or null.</param>
        public static void Fatal(string tag, string message, Exception? exception)
        {
            Report(DevLogLevel.Error, tag, message, exception, true);
        }

        /// <summary>
        /// Starts listening for exceptions that nothing handles and reports them as fatal. Safe to call from every entry point of the process; only the first call has an effect.
        /// </summary>
        public static void InstallGlobalHandlers()
        {
            if (Interlocked.Exchange(ref handlersInstalled, 1) != 0)
            {
                return;
            }

            AppDomain.CurrentDomain.UnhandledException += (object sender, UnhandledExceptionEventArgs args) =>
            {
                Fatal("Unhandled", "Nieobsłużony wyjątek" + (args.IsTerminating ? " (aplikacja się zamknie)" : string.Empty), args.ExceptionObject as Exception);
            };

            TaskScheduler.UnobservedTaskException += (object? sender, UnobservedTaskExceptionEventArgs args) =>
            {
                Error("Unhandled", "Nieobserwowany wyjątek zadania", args.Exception);
            };

            Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (object? sender, Android.Runtime.RaiseThrowableEventArgs args) =>
            {
                Fatal("Unhandled", "Nieobsłużony wyjątek Androida", args.Exception);
            };
        }

        /// <summary>
        /// Writes the event to the Android log and to the developer log.
        /// </summary>
        /// <param name="level">Severity.</param>
        /// <param name="tag">Part of the application that reports the event.</param>
        /// <param name="message">Description of the event.</param>
        /// <param name="exception">Exception that caused the event, or null.</param>
        /// <param name="flush">Whether the log is written to the file at once.</param>
        private static void Report(DevLogLevel level, string tag, string message, Exception? exception, bool flush)
        {
            try
            {
                string text = exception == null ? message : message + ": " + exception.Message;

                switch (level)
                {
                    case DevLogLevel.Error:
                        Android.Util.Log.Error(tag, text);
                        break;
                    case DevLogLevel.Warning:
                        Android.Util.Log.Warn(tag, text);
                        break;
                    default:
                        Android.Util.Log.Info(tag, text);
                        break;
                }

                DevLogStore store = Store;

                store.Log.Add(level, tag, message, exception, DateTime.UtcNow);

                if (flush)
                {
                    store.Flush();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
