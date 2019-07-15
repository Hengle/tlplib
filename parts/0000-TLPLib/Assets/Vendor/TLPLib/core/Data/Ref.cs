﻿using System;
using com.tinylabproductions.TLPLib.Functional;
using pzd.lib.functional;
using UnityEngine;

namespace com.tinylabproductions.TLPLib.Data {
  public interface Ref<A> : Val<A> {
    new A value { get; set; }
  }

  /* Simple heap-allocated reference. */
  public sealed class SimpleRef<A> : Ref<A> {
    // For access using ref keyword.
    public A value;

    A Val<A>.value => value;

    A Ref<A>.value {
      get { return value;}
      set { this.value = value; }
    }

    public SimpleRef(A value) { this.value = value; }

    public static implicit operator A(SimpleRef<A> r) => r.value;
    public override string ToString() => $"{nameof(SimpleRef<A>)}({value})";
  }

  public class LambdaRef<A> : Ref<A> {
    readonly Func<A> get;
    readonly Action<A> set;

    public LambdaRef(Func<A> get, Action<A> set) {
      this.get = get;
      this.set = set;
    }

    public A value {
      get { return get(); }
      set { set(value); }
    }

    public override string ToString() => $"λRef({value})";
  }

  /// <summary>
  /// Lazy reference that wraps another <see cref="Ref{A}"/>, but does not initialize it
  /// until first access.
  ///
  /// Very useful in making <see cref="PrefVal{A}"/>s inspectable.
  ///
  /// For example:
  /// <code>
  /// [Inspect, UsedImplicitly]
  /// LazyRef&lt;string&gt; adbAdditions = Ref.lazy(() =&gt; prefVals.adbAdditions);
  /// </code>
  /// </summary>
  public class LazyRef<A> : Ref<A> {
    [HideInInspector]
    public readonly LazyVal<Ref<A>> backing;

    public LazyRef(LazyVal<Ref<A>> backing) { this.backing = backing; }

    public A value {
      get => backing.strict.value;
      set => backing.strict.value = value;
    }

    public override string ToString() => backing.strict.ToString();
  }

  public static class Ref {
    public static Ref<A> a<A>(A value) => new SimpleRef<A>(value);
    public static Ref<A> a<A>(Func<A> get, Action<A> set) => new LambdaRef<A>(get, set);
    public static LazyRef<A> lazy<A>(Func<Ref<A>> backing) => lazy(F.lazy(backing));
    public static LazyRef<A> lazy<A>(LazyVal<Ref<A>> backing) => new LazyRef<A>(backing);

    public static Ref<B> map<A, B>(this Ref<A> r, Func<A, B> map, Func<B, A> contraMap) =>
      a(() => map(r.value), b => r.value = contraMap(b));

    public static Ref<B> map<A, B>(this Ref<A> r, Func<A, B> map, Func<B, Option<A>> contraMap) =>
      a(() => map(r.value), b => {
        foreach (var a in contraMap(b))
          r.value = a;
      });
  }
}
