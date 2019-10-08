﻿using System;
using System.Collections.Generic;
using com.tinylabproductions.TLPLib.Functional;
using pzd.lib.concurrent;
using pzd.lib.reactive;
using pzdf = pzd.lib.functional;
using Smooth.Pools;
using None = pzd.lib.functional.None;

namespace com.tinylabproductions.TLPLib.Concurrent {
  public static class IHeapFutureExts {
    public static Future<A> asFuture<A>(this IHeapFuture<A> f) => Future.a(f);
  }

  class FutureImpl<A> : IHeapFuture<A>, Promise<A> {
    static readonly Pool<List<Action<A>>> pool = ListPool<Action<A>>.Instance;

    List<Action<A>> listeners = pool.Borrow();
    bool iterating;

    public bool isCompleted => value.isSome;
    public pzdf.Option<A> value { get; private set; } = None._;
    public bool valueOut(out A a) => value.valueOut(out a);

    public override string ToString() => $"{nameof(FutureImpl<A>)}({value})";

    public void complete(A v) {
      if (! tryComplete(v)) throw new IllegalStateException(
        $"Trying to complete future with \"{v}\" but it is already completed with \"{value.__unsafeGet}\""
      );
    }

    public bool tryComplete(A v) {
      // Cannot use fold here because of iOS AOT.
      var ret = value.isNone;
      if (ret) {
        value = F.some(v);
        // completed should be called only once
        completed(v);
      }
      return ret;
    }

    public ISubscription onComplete(Action<A> action) {
      if (value.isSome) {
        action(value.__unsafeGet);
        return Subscription.empty;
      }
      else {
        listeners.Add(action);
        return new Subscription(() => {
          if (iterating || listeners == null) return;
          listeners.Remove(action);
        });
      }
    }

    void completed(A v) {
      iterating = true;
      foreach (var action in listeners) action(v);
      listeners.Clear();
      pool.Release(listeners);
      listeners = null;
    }
  }
}