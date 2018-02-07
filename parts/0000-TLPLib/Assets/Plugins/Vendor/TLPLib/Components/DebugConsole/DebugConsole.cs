﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using com.tinylabproductions.TLPLib.Concurrent;
using com.tinylabproductions.TLPLib.dispose;
using com.tinylabproductions.TLPLib.Data;
using com.tinylabproductions.TLPLib.Data.typeclasses;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Functional;
using com.tinylabproductions.TLPLib.Reactive;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.tinylabproductions.TLPLib.Components.DebugConsole {
  public class DConsole {
    public enum Direction { Left, Up, Right, Down }

    public struct Command {
      public readonly string cmdGroup, name;
      public readonly Action run;

      public Command(string cmdGroup, string name, Action run) {
        this.cmdGroup = cmdGroup;
        this.name = name;
        this.run = run;
      }
    }

    struct Instance {
      public readonly DebugConsoleBinding view;

      public Instance(DebugConsoleBinding view) {
        this.view = view;
      }
    }

    public static DConsole instance { get; } = new DConsole();
    public static readonly ImmutableArray<bool> bools = ImmutableArray.Create(true, false);

    DConsole() {
      var r = registrarFor(nameof(DConsole));
      r.register("Self-test", () => "self-test");
      r.register("Future Self-test", () => Future.delay(Duration.fromSeconds(1), () => "after 1 s"));
    }

    public delegate void OnShow(DConsole console);

    readonly Dictionary<string, List<Command>> commands = new Dictionary<string, List<Command>>();
    public event OnShow onShow;

    Option<Instance> current = F.none<Instance>();

    public static readonly ImmutableList<int> DEFAULT_MOUSE_SEQUENCE = ImmutableList.Create(0, 1, 3, 2, 0, 2, 3, 1, 0);
    public static readonly ImmutableList<Direction> DEFAULT_DIRECTION_SEQUENCE =
      ImmutableList.Create(
        Direction.Left, Direction.Right,
        Direction.Left, Direction.Right,
        Direction.Left, Direction.Up,
        Direction.Right, Direction.Down,
        Direction.Right, Direction.Up
      );

    public static readonly DebugSequenceMouseData DEFAULT_MOUSE_DATA = new DebugSequenceMouseData();
    public class DebugSequenceMouseData {
      public readonly int width, height;
      public readonly ImmutableList<int> sequence;

      public DebugSequenceMouseData(int width=2, int height=2, ImmutableList<int> sequence=null) {
        this.width = width;
        this.height = height;
        this.sequence = sequence ?? DEFAULT_MOUSE_SEQUENCE;
      }
    }

    public static readonly DebugSequenceDirectionData DEFAULT_DIRECTION_DATA = new DebugSequenceDirectionData();
    public class DebugSequenceDirectionData {
      public readonly string horizonalAxisName, verticalAxisName;
      public readonly Duration timeframe;
      public readonly ImmutableList<Direction> sequence;

      public DebugSequenceDirectionData(
        string horizonalAxisName="Horizontal",
        string verticalAxisName="Vertical",
        Duration timeframe=default(Duration),
        ImmutableList<Direction> sequence=null
      ) {
        this.horizonalAxisName = horizonalAxisName;
        this.verticalAxisName = verticalAxisName;
        this.timeframe = timeframe == default(Duration) ? 5.seconds() : timeframe;
        sequence = sequence ?? DEFAULT_DIRECTION_SEQUENCE;
        this.sequence = sequence;

        for (var idx = 0; idx < sequence.Count - 1; idx++) {
          var current = sequence[idx];
          var next = sequence[idx + 1];
          if (current == next) throw new ArgumentException(
            $"{nameof(DebugSequenceDirectionData)} sequence can't contain subsequent elements! " +
            $"Found {current} at {idx} & {idx + 1}.",
            nameof(sequence)
          );
        }
      }
    }

    public static IObservable<Unit> registerDebugSequence(
      IDisposableTracker tracker,
      DebugSequenceMouseData mouseData=null,
      Option<DebugSequenceDirectionData> directionDataOpt=default,
      DebugConsoleBinding binding=null
    ) {
      Option.ensureValue(ref directionDataOpt);

      binding = binding ?? Resources.Load<DebugConsoleBinding>("Debug Console Prefab");
      mouseData = mouseData ?? DEFAULT_MOUSE_DATA;

      var mouseObs =
        new RegionClickObservable(mouseData.width, mouseData.height)
        .sequenceWithinTimeframe(tracker, mouseData.sequence, 3);

      var directionObs = directionDataOpt.fold(
        Observable<Unit>.empty,
        directionData => {
          var directions = Observable.everyFrame.collect(_ => {
            var horizontal = Input.GetAxisRaw(directionData.horizonalAxisName);
            var vertical = Input.GetAxisRaw(directionData.verticalAxisName);
            // Both are equal, can't decide.
            if (Math.Abs(horizontal - vertical) < 0.001f) return Option<Direction>.None;
            return
              Math.Abs(horizontal) > Math.Abs(vertical)
              ? F.some(horizontal > 0 ? Direction.Right : Direction.Left)
              : F.some(vertical > 0 ? Direction.Up : Direction.Down);
          }).changedValues();

          return
            directions
            .withinTimeframe(directionData.sequence.Count, directionData.timeframe)
            .filter(l => l.Select(t => t._1).SequenceEqual(directionData.sequence))
            .discardValue();
        }
      );

      var obs = mouseObs.join(directionObs);
      obs.subscribe(tracker, _ => instance.show(binding));
      return obs;
    }

    public ISubscription register(Command command) {
      var list = commands.get(command.cmdGroup).getOrElse(() => {
        var lst = new List<Command>();
        commands[command.cmdGroup] = lst;
        return lst;
      });
      list.Add(command);
      return new Subscription(() => list.Remove(command));
    }

    public DConsoleRegistrar registrarFor(string prefix) =>
      new DConsoleRegistrar(this, prefix);

    public void registerHashSet<A>(
      string name, Ref<ImmutableHashSet<A>> pv, IEnumerable<A> options
    ) {
      var r = registrarFor(name);
      r.register("List", () => pv.value.asDebugString());
      r.register("Clear", () => {
        pv.value = ImmutableHashSet<A>.Empty;
        return pv.value.asDebugString();
      });
      foreach (var f in options) {
        r.register($"{f}?", () => pv.value.Contains(f));
        r.register($"Toggle {f}", () => {
          pv.value = pv.value.toggle(f);
          return $"in set={pv.value.Contains(f)}";
        });
      }
    }

    public void show(DebugConsoleBinding binding) {
      destroy();
      onShow?.Invoke(this);
      onShow = null;

      var view = binding.clone();
      Object.DontDestroyOnLoad(view);

      var currentGroupButtons = ImmutableList<ButtonBinding>.Empty;
      setupList(view.commands, () => currentGroupButtons);

      var commandGroups = commands.OrderBy(_ => _.Key).Select(commandGroup => {
        var button = addButton(view.buttonPrefab, view.commandGroups.holder.transform);
        button.text.text = commandGroup.Key;
        button.button.onClick.AddListener(() =>
          currentGroupButtons = showGroup(view, commandGroup.Key, commandGroup.Value)
        );
        return button;
      }).ToImmutableList();
      setupList(view.commandGroups, () => commandGroups);

      Application.logMessageReceivedThreaded += onLogMessageReceived;
      view.closeButton.onClick.AddListener(destroy);

      current = new Instance(view).some();
    }

    static void setupList(DebugConsoleListBinding listBinding, Fn<ImmutableList<ButtonBinding>> contents) {
      listBinding.clearFilterButton.onClick.AddListener(() => listBinding.filterInput.text = "");
      listBinding.filterInput.onValueChanged.AddListener(value => {
        foreach (var button in contents()) {
          var active = button.text.text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
          button.gameObject.SetActive(active);
        }
      });
    }

    static ImmutableList<ButtonBinding> showGroup(
      DebugConsoleBinding view, string groupName, IEnumerable<Command> commands
    ) {
      view.commandGroupLabel.text = groupName;
      var commandsHolder = view.commands.holder;
      foreach (var t in commandsHolder.transform.children()) Object.Destroy(t.gameObject);
      return commands.Select(command => {
        var button = addButton(view.buttonPrefab, commandsHolder.transform);
        button.text.text = command.name;
        button.button.onClick.AddListener(() => command.run());
        return button;
      }).ToImmutableList();
    }

    static ButtonBinding addButton(ButtonBinding prefab, Transform target) {
      var button = prefab.clone();
      // Parent of RectTransform is being set with parent property.
      // Consider using the SetParent method instead, with the worldPositionStays
      // argument set to false. This will retain local orientation and scale rather
      // than world orientation and scale, which can prevent common UI scaling issues.
      button.GetComponent<RectTransform>().SetParent(target, worldPositionStays: false);
      return button;
    }

    void onLogMessageReceived(string message, string stackTrace, LogType type) {
      foreach (var instance in current) {
        ASync.OnMainThread(() => {
          var entry = instance.view.logEntryPrefab.clone();
          var shortText = $"{DateTime.Now}  {type}  {message}";

          entry.text = shortText;
          entry.GetComponent<RectTransform>().SetParent(
            instance.view.logEntriesHolder.transform, worldPositionStays: false
          );
          entry.transform.SetAsFirstSibling();
        });
      }
    }

    public void destroy() {
      foreach (var instance in current) {
        Application.logMessageReceivedThreaded -= onLogMessageReceived;
        Object.Destroy(instance.view.gameObject);
      }
      current = current.none;
    }
  }

  public delegate Option<Obj> HasObjFn<Obj>();

  public struct DConsoleRegistrar {
    public readonly DConsole console;
    public readonly string commandGroup;

    public DConsoleRegistrar(DConsole console, string commandGroup) {
      this.console = console;
      this.commandGroup = commandGroup;
    }

    static readonly HasObjFn<Unit> unitSomeFn = () => F.some(F.unit);

    public ISubscription register(string name, Action run) =>
      register(name, () => { run(); return F.unit; });
    public ISubscription register<A>(string name, Fn<A> run) =>
      register(name, unitSomeFn, _ => run());
    public ISubscription register<A>(string name, Fn<Future<A>> run) =>
      register(name, unitSomeFn, _ => run());
    public ISubscription register<Obj>(string name, HasObjFn<Obj> objOpt, Act<Obj> run) =>
      register(name, objOpt, obj => { run(obj); return F.unit; });
    public ISubscription register<Obj, A>(string name, HasObjFn<Obj> objOpt, Fn<Obj, A> run) =>
      register(name, objOpt, obj => Future.successful(run(obj)));
    public ISubscription register<Obj, A>(string name, HasObjFn<Obj> objOpt, Fn<Obj, Future<A>> run) {
      var prefixedName = $"[DC|{commandGroup}]> {name}";
      return console.register(new DConsole.Command(commandGroup, name, () => {
        var opt = objOpt();
        if (opt.isSome) {
          var returnFuture = run(opt.get);
          Act<A> onComplete = t => Debug.Log($"{prefixedName} done: {t}");
          // Check perhaps it is completed immediately.
          returnFuture.value.voidFold(
            () => {
              Debug.Log($"{prefixedName} starting.");
              returnFuture.onComplete(onComplete);
            },
            onComplete
          );
        }
        else Debug.Log($"{prefixedName} not running: {typeof(Obj)} is None.");
      }));
    }

    public void registerToggle(string name, Ref<bool> r, string comment=null) =>
      registerToggle(name, () => r.value, v => r.value = v, comment);

    public void registerToggle(string name, Fn<bool> getter, Act<bool> setter, string comment=null) {
      register($"{name}?", getter);
      register($"Toggle {name}", () => {
        setter(!getter());
        return comment == null ? getter().ToString() : $"{comment}: value={getter()}";
      });
    }

    public void registerNumeric<A>(
      string name, Ref<A> a, Numeric<A> num, A step,
      ImmutableList<A> quickSetValues = null
    ) {
      register($"{name}?", () => a.value);
      register($"{name} += {step}", () => a.value = num.add(a.value, step));
      register($"{name} -= {step}", () => a.value = num.subtract(a.value, step));
      if (quickSetValues != null) {
        foreach (var value in quickSetValues)
          register($"{name} = {value}", () => a.value = value);
      }
    }

    public void registerNumeric<A>(
      string name, Ref<A> a, Numeric<A> num,
      ImmutableList<A> quickSetValues = null
    ) =>
      registerNumeric(name, a, num, num.fromInt(1), quickSetValues);

    public void registerNumericOpt<A>(
      string name, Ref<Option<A>> aOpt, A showOnNone, Numeric<A> num,
      ImmutableList<A> quickSetValues = null
    ) {
      register($"Clear {name}", () => aOpt.value = Option<A>.None);
      register($"{name} opt?", () => aOpt.value);
      registerNumeric(
        name, Ref.a(
          () => aOpt.value.getOrElse(showOnNone),
          v => aOpt.value = v.some()
        ), num, quickSetValues
      );
    }

    public void registerCountdown(string name, uint count, Action act) {
      var countdown = count;
      register(name, () => {
        countdown--;
        if (countdown == 0) {
          act();
          countdown = count;
          return $"{name} EXECUTED.";
        }
        return $"Press me {countdown} more times to execute.";
      });
    }

    public void registerEnum<A>(
      string name, Ref<A> reference, IEnumerable<A> enumerable, string comment = null
    ) {
      register($"{name}?", () => {
        var v = reference.value;
        return comment == null ? v.ToString() : $"{comment}: value={v}";
      });
      foreach (var a in enumerable)
        register($"{name}={a}", () => {
          reference.value = a;
          return comment == null ? a.ToString() : $"{comment}: value={a}";
        });
    }

    static readonly Option<bool>[] OPT_BOOLS = {F.none<bool>(), F.some(false), F.some(true)};
    public void registerBools(string name, Ref<Option<bool>> reference, string comment = null) =>
      registerEnum(name, reference, OPT_BOOLS, comment);
  }
}
