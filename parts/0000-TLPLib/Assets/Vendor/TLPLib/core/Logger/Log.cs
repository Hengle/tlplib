﻿using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using com.tinylabproductions.TLPLib.Collection;
using com.tinylabproductions.TLPLib.Components.DebugConsole;
using com.tinylabproductions.TLPLib.Concurrent;
using com.tinylabproductions.TLPLib.dispose;
using com.tinylabproductions.TLPLib.Data;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Functional;
using com.tinylabproductions.TLPLib.Reactive;
using com.tinylabproductions.TLPLib.Threads;
using com.tinylabproductions.TLPLib.Utilities;
using GenerationAttributes;
using JetBrains.Annotations;
using pzd.lib.exts;
using pzd.lib.functional;
using pzd.lib.serialization;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = System.Object;

namespace com.tinylabproductions.TLPLib.Logger {
  /**
   * This double checks logging levels because string concatenation is more
   * expensive than boolean check.
   *
   * The general rule of thumb is that if your log object doesn't need any
   * processing you can call appropriate logging method by itself. If it does
   * need processing, you should use `if (Log.d.isDebug()) Log.d.debug("foo=" + foo);` style.
   **/
  [PublicAPI] public static class Log {
    public enum Level : byte { VERBOSE = 10, DEBUG = 20, INFO = 30, WARN = 40, ERROR = 50 }
    public static class Level_ {
      public static readonly ISerializedRW<Level> rw = 
        SerializedRW.byte_.map<byte, Level>(b => (Level) b, l => (byte) l);

      public static readonly ISerializedRW<Option<Level>> optRw = 
        SerializedRW.opt(rw).mapNoFail(_ => _, _ => _);
    }

    // InitializeOnLoad is needed to set static variables on main thread.
    // FKRs work without it, but on Gummy Bear repo tests fail
#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod]
    static void init() {}

    public static readonly Level defaultLogLevel =
      Application.isEditor || Debug.isDebugBuild
      ? Level.DEBUG : Level.INFO;

    static readonly bool useConsoleLog = EditorUtils.inBatchMode;

    static Log() {
      DConsole.instance.registrarOnShow(
        NeverDisposeDisposableTracker.instance, "Default Logger",
        (dc, r) => {
          r.registerEnum(
            "level",
            Ref.a(() => @default.level, v => @default.level = v),
            EnumUtils.GetValues<Level>()
          );
        }
      );
    }

    static ILog _default;
    public static ILog @default {
      get => _default ?? (
        _default = useConsoleLog ? (ILog) ConsoleLog.instance : UnityLog.instance
      );
      set => _default = value;
    }

    /// <summary>
    /// Shorthand for <see cref="Log.@default"/>. Allows <code><![CDATA[
    /// if (Log.d.isInfo) Log.d.info("foo");
    /// ]]></code> syntax.
    /// </summary>
    public static ILog d => @default;

    [Conditional("UNITY_EDITOR"), PublicAPI]
    public static void editor(object o) => EditorLog.log(o);
  }

  public struct LogEntry {
    /// <summary>Message for the log entry.</summary>
    public readonly string message;
    /// <summary>key -> value pairs where values make up a set. Things like
    /// type -> (dog or cat or fish) are a good fit here.</summary>
    public readonly ImmutableArray<Tpl<string, string>> tags;
    /// <summary>
    /// key -> value pairs where values can be anything. Things like
    /// bytesRead -> 322344 are a good fit here.
    /// </summary>
    public readonly ImmutableArray<Tpl<string, string>> extras;
    /// <summary>Object which is related to this entry.</summary>
    public readonly object maybeContext;
    /// <summary>A log entry might have backtrace attached to it.</summary>
    public readonly Backtrace? backtrace;
    /// <summary>Whether this entry should be reported to any error tracking that we have.</summary>
    public readonly bool reportToErrorTracking;

    public LogEntry(
      string message,
      ImmutableArray<Tpl<string, string>> tags,
      ImmutableArray<Tpl<string, string>> extras,
      bool reportToErrorTracking = true,
      Backtrace? backtrace = null,
      object context = null
    ) {
      this.message = message;
      this.tags = tags;
      this.extras = extras;
      this.reportToErrorTracking = reportToErrorTracking;
      this.backtrace = backtrace;
      maybeContext = context;
    }

    public override string ToString() {
      var sb = new StringBuilder(message);
      if (maybeContext != null) sb.Append($" (ctx={maybeContext})");
      if (tags.nonEmpty()) sb.Append($"\n{nameof(tags)}={tags.mkStringEnumNewLines()}");
      if (extras.nonEmpty()) sb.Append($"\n{nameof(extras)}={extras.mkStringEnumNewLines()}");
      if (backtrace.HasValue) sb.Append($"\n{backtrace.Value}");
      return sb.ToString();
    }

    [PublicAPI] public static LogEntry simple(
      string message, bool reportToErrorTracking = true, 
      Backtrace? backtrace = null, object context = null
    ) => new LogEntry(
      message: message, 
      tags: ImmutableArray<Tpl<string, string>>.Empty,
      extras: ImmutableArray<Tpl<string, string>>.Empty,
      reportToErrorTracking: reportToErrorTracking,
      backtrace: backtrace, context: context
    );

    [PublicAPI] public static LogEntry tags_(
      string message, ImmutableArray<Tpl<string, string>> tags, bool reportToErrorTracking = true, 
      Backtrace? backtrace = null, object context = null
    ) => new LogEntry(
      message: message, tags: tags, extras: ImmutableArray<Tpl<string, string>>.Empty,
      backtrace: backtrace, context: context, reportToErrorTracking: reportToErrorTracking
    );

    [PublicAPI] public static LogEntry extras_(
      string message, ImmutableArray<Tpl<string, string>> extras, bool reportToErrorTracking = true, 
      Backtrace? backtrace = null, object context = null
    ) => new LogEntry(
      message: message, tags: ImmutableArray<Tpl<string, string>>.Empty, extras: extras,
      backtrace: backtrace, context: context, reportToErrorTracking: reportToErrorTracking
    );

    public static LogEntry fromException(
      string message, Exception ex, bool reportToErrorTracking = true, object context = null
    ) {
      var sb = new StringBuilder();

      void appendEx(Exception e) {
        sb.Append("[");
        sb.Append(e.GetType());
        sb.Append("] ");
        sb.Append(e.Message);
      }
      
      sb.Append(message);
      sb.Append(": ");
      appendEx(ex);
      var backtraceBuilder = ImmutableList.CreateBuilder<BacktraceElem>();
      foreach (var bt in Backtrace.fromException(ex)) {
        backtraceBuilder.AddRange(bt.elements.a);
      }

      var idx = 0;
      var cause = ex.InnerException;
      while (cause != null) {
        sb.Append("\nCaused by [");
        sb.Append(idx);
        sb.Append("]: ");
        appendEx(cause);
        foreach (var bt in Backtrace.fromException(ex)) {
          backtraceBuilder.Add(new BacktraceElem($"### Backtrace for [{idx}] ###", F.none_));
          backtraceBuilder.AddRange(bt.elements.a);
        }
        cause = cause.InnerException;
        idx++;
      }

      var backtrace = backtraceBuilder.ToImmutable().toNonEmpty().map(_ => new Backtrace(_));
      
      return simple(sb.ToString(), reportToErrorTracking, backtrace.toNullable(), context);
    }

    [PublicAPI]
    public LogEntry withMessage(string message) =>
      new LogEntry(message, tags, extras, reportToErrorTracking, backtrace, maybeContext);

    [PublicAPI]
    public LogEntry withMessage(Func<string, string> message) =>
      new LogEntry(message(this.message), tags, extras, reportToErrorTracking, backtrace, maybeContext);

    [PublicAPI]
    public LogEntry withExtras(ImmutableArray<Tpl<string, string>> extras) =>
      new LogEntry(message, tags, extras, reportToErrorTracking, backtrace, maybeContext);

    [PublicAPI]
    public LogEntry withExtras(Func<ImmutableArray<Tpl<string, string>>, ImmutableArray<Tpl<string, string>>> extras) =>
      new LogEntry(message, tags, extras(this.extras), reportToErrorTracking, backtrace, maybeContext);

    public static readonly ISerializedRW<ImmutableArray<Tpl<string, string>>> stringTupleArraySerializedRw =
      SerializedRW.immutableArray(SerializedRW.str.tpl(SerializedRW.str));
  }

  [Record] public readonly partial struct LogEvent {
    public readonly Log.Level level;
    public readonly LogEntry entry;
  }

  public interface ILog {
    Log.Level level { get; set; }

    bool willLog(Log.Level l);
    void log(Log.Level l, LogEntry o);
    IRxObservable<LogEvent> messageLogged { get; }
  }

  public static class ILogExts {
    public static bool isVerbose(this ILog log) => log.willLog(Log.Level.VERBOSE);
    public static bool isDebug(this ILog log) => log.willLog(Log.Level.DEBUG);
    public static bool isInfo(this ILog log) => log.willLog(Log.Level.INFO);
    public static bool isWarn(this ILog log) => log.willLog(Log.Level.WARN);

    public static void log(this ILog log, Log.Level l, string message) =>
      log.log(l, LogEntry.simple(message));

    public static void verbose(this ILog log, string msg, Object context = null) =>
      log.log(Log.Level.VERBOSE, LogEntry.simple(msg, context: context));
    public static void debug(this ILog log, string msg, Object context = null) =>
      log.log(Log.Level.DEBUG, LogEntry.simple(msg, context: context));
    public static void info(this ILog log, string msg, Object context = null) =>
      log.log(Log.Level.INFO, LogEntry.simple(msg, context: context));
    public static void warn(this ILog log, string msg, Object context = null) =>
      log.warn(LogEntry.simple(msg, context: context));
    public static void warn(this ILog log, LogEntry entry) =>
      log.log(Log.Level.WARN, entry);
    public static void error(this ILog log, string msg, Object context = null) =>
      log.error(LogEntry.simple(msg, context: context));
    public static void error(this ILog log, LogEntry entry) =>
      log.log(Log.Level.ERROR, entry);
    public static void error(this ILog log, Exception ex, Object context = null) =>
      log.error(ex.Message, ex, context);
    public static void error(this ILog log, string msg, Exception ex, object context = null) =>
      log.error(LogEntry.fromException(msg, ex, context: context));
  }

  /**
   * Useful for logging from inside Application.logMessageReceivedThreaded, because
   * log calls are silently ignored from inside the handlers. Just make sure not to
   * get into an endless loop.
   **/
  public class DeferToMainThreadLog : ILog {
    readonly ILog backing;

    public DeferToMainThreadLog(ILog backing) { this.backing = backing; }

    public Log.Level level {
      get => backing.level;
      set => backing.level = value;
    }

    public bool willLog(Log.Level l) => backing.willLog(l);
    public void log(Log.Level l, LogEntry entry) =>
      defer(() => backing.log(l, entry));

    static void defer(Action a) => ASync.OnMainThread(a, runNowIfOnMainThread: false);

    public IRxObservable<LogEvent> messageLogged => backing.messageLogged;
  }

  public abstract class LogBase : ILog {
    readonly ISubject<LogEvent> _messageLogged = new Subject<LogEvent>();
    public IRxObservable<LogEvent> messageLogged => _messageLogged;
    // Can't use Unity time, because it is not thread safe
    static readonly DateTime initAt = DateTime.Now;

    public Log.Level level { get; set; } = Log.defaultLogLevel;
    public bool willLog(Log.Level l) => l >= level;

    public void log(Log.Level l, LogEntry entry) {
      logInner(l, entry.withMessage(line(l.ToString(), entry.message)));
      var logEvent = new LogEvent(l, entry);
      if (OnMainThread.isMainThread) _messageLogged.push(logEvent);
      else {
        // extracted method to avoid closure allocation when running on main thread
        logOnMainThread(logEvent);
      }
    }

    void logOnMainThread(LogEvent logEvent) => OnMainThread.run(() => _messageLogged.push(logEvent));

    protected abstract void logInner(Log.Level l, LogEntry entry);

    static string line(string level, object o) => $"[{(DateTime.Now - initAt).TotalSeconds:F3}|{thread}|{level}]> {o}";

    static string thread => (OnMainThread.isMainThread ? "Tm" : "T") + Thread.CurrentThread.ManagedThreadId;
  }

  /** Useful for batch mode to log to the log file without the stacktraces. */
  public class ConsoleLog : LogBase {
    public static readonly ConsoleLog instance = new ConsoleLog();
    ConsoleLog() {}

    protected override void logInner(Log.Level l, LogEntry entry) =>
      Console.WriteLine(entry.ToString());
  }

  [PublicAPI]
  public class NoOpLog : LogBase {
    public static readonly NoOpLog instance = new NoOpLog();
    NoOpLog() {}

    protected override void logInner(Log.Level l, LogEntry entry) {}
  }

  class EditorLog {
    public static readonly string logfilePath;
    public static readonly StreamWriter logfile;

    static EditorLog() {
      logfilePath = Application.temporaryCachePath + "/unity-editor-runtime.log";
      if (Log.d.isInfo()) Log.d.info("Editor Runtime Logfile: " + logfilePath);
      logfile = new StreamWriter(
        File.Open(logfilePath, FileMode.Append, FileAccess.Write, FileShare.Read)
      ) { AutoFlush = true };

      log("\n\nLog opened at " + DateTime.Now + "\n\n");
    }

    [Conditional("UNITY_EDITOR")]
    public static void log(object o) { logfile.WriteLine(o); }
  }
}