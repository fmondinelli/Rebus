using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Rebus.Logging
{
    /// <summary>
    /// Logger factory that logs stuff to the console
    /// </summary>
    public class ConsoleLoggerFactory : AbstractRebusLoggerFactory
    {
        /// <summary>
        /// One single log statement
        /// </summary>
        public class LogStatement
        {
            internal LogStatement(LogLevel level, string text, object[] args)
            {
                Level = level;
                Args = args;
                Text = text;
            }

            /// <summary>
            /// The level of this log statement
            /// </summary>
            public LogLevel Level { get; private set; }
            
            /// <summary>
            /// The text (possibly inclusing formatting placeholders) of this log statement
            /// </summary>
            public string Text { get; private set; }
            
            /// <summary>
            /// The values to use for string interpolation
            /// </summary>
            public object[] Args { get; private set; }
        }

        static readonly ConcurrentDictionary<Type, ILog> Loggers = new ConcurrentDictionary<Type, ILog>();

        readonly bool colored;
        readonly List<Func<LogStatement, bool>> filters = new List<Func<LogStatement, bool>>(); 

        LoggingColors colors = new LoggingColors();
        LogLevel minLevel = LogLevel.Debug;
        bool showTimestamps;

        /// <summary>
        /// Constructs the logger factory
        /// </summary>
        public ConsoleLoggerFactory(bool colored)
        {
            this.colored = colored;
        }

        /// <summary>
        /// Gets or sets the colors to use when logging
        /// </summary>
        public LoggingColors Colors
        {
            get { return colors; }
            set
            {
                if (value == null)
                {
                    throw new InvalidOperationException("Attempted to set logging colors to null");
                }
                colors = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum logging level to output to the console
        /// </summary>
        public LogLevel MinLevel
        {
            get { return minLevel; }
            set
            {
                minLevel = value;
                Loggers.Clear();
            }
        }

        /// <summary>
        /// Gets the list of filters that each log statement will be passed through in order to evaluate whether
        /// the given log statement should be output to the console
        /// </summary>
        public IList<Func<LogStatement, bool>> Filters
        {
            get { return filters; }
        }

        /// <summary>
        /// Gets/sets whether timestamps should be shown when logging
        /// </summary>
        public bool ShowTimestamps
        {
            get { return showTimestamps; }
            set
            {
                showTimestamps = value;
                Loggers.Clear();
            }
        }

        /// <summary>
        /// Gets a logger for logging stuff from within the specified type
        /// </summary>
        protected override ILog GetLogger(Type type)
        {
            ILog logger;
            
            if (Loggers.TryGetValue(type, out logger)) return logger;
            
            logger = new ConsoleLogger(type, colors, this, showTimestamps);
            Loggers.TryAdd(type, logger);
            
            return logger;
        }

        class ConsoleLogger : ILog
        {
            readonly LoggingColors loggingColors;
            readonly ConsoleLoggerFactory factory;
            readonly Type type;
            readonly string logLineFormatString;

            public ConsoleLogger(Type type, LoggingColors loggingColors, ConsoleLoggerFactory factory, bool showTimestamps)
            {
                this.type = type;
                this.loggingColors = loggingColors;
                this.factory = factory;

                logLineFormatString = showTimestamps
                                          ? "{0} {1} {2} ({3}): {4}"
                                          : "{1} {2} ({3}): {4}";
            }

            #region ILog Members

            public void Debug(string message, params object[] objs)
            {
                Log(LogLevel.Debug, message, loggingColors.Debug, objs);
            }

            public void Info(string message, params object[] objs)
            {
                Log(LogLevel.Info, message, loggingColors.Info, objs);
            }

            public void Warn(string message, params object[] objs)
            {
                Log(LogLevel.Warn, message, loggingColors.Warn, objs);
            }

            public void Error(Exception exception, string message, params object[] objs)
            {
                Log(LogLevel.Error, string.Format(message, objs) + Environment.NewLine + exception, loggingColors.Error);
            }

            public void Error(string message, params object[] objs)
            {
                Log(LogLevel.Error, message, loggingColors.Error, objs);
            }

            #endregion

            void Log(LogLevel level, string message, ColorSetting colorSetting, params object[] objs)
            {
                if (factory.colored)
                {
                    using (colorSetting.Enter())
                    {
                        Write(level, message, objs);
                    }
                }
                else
                {
                    Write(level, message, objs);
                }
            }

            string LevelString(LogLevel level)
            {
                switch(level)
                {
                    case LogLevel.Debug:
                        return "DEBUG";
                    case LogLevel.Info:
                        return "INFO";
                    case LogLevel.Warn:
                        return "WARN";
                    case LogLevel.Error:
                        return "ERROR";
                    default:
                        throw new ArgumentOutOfRangeException("level");
                }
            }

            void Write(LogLevel level, string message, object[] objs)
            {
                if ((int)level < (int)factory.MinLevel) return;
                if (factory.AbortedByFilter(new LogStatement(level, message, objs))) return;

                var levelString = LevelString(level);

                var threadName = Thread.CurrentThread.Name;
                var typeName = type.FullName;
                try
                {
                    var renderedMessage = string.Format(message, objs);
                    var timeFormat = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");

                    // ReSharper disable EmptyGeneralCatchClause
                    try
                    {
                        Console.WriteLine(logLineFormatString,
                            timeFormat,
                            typeName,
                            levelString,
                            threadName,
                            renderedMessage);
                    }
                    catch
                    {
                        // nothing to do about it if this part fails   
                    }
                    // ReSharper restore EmptyGeneralCatchClause
                }
                catch
                {
                    Warn("Could not render output string: '{0}' with args: {1}", message, string.Join(", ", objs));
                }
            }
        }

        bool AbortedByFilter(LogStatement logStatement)
        {
            return filters.Any(f => !f(logStatement));
        }
    }
}