﻿using System;
using System.Collections.Generic;
using com.tinylabproductions.TLPLib.Extensions;
using com.tinylabproductions.TLPLib.Functional;

namespace com.tinylabproductions.TLPLib.Data.typeclasses {
  public interface Comparable<in A> {
    CompareResult compare(A a1, A a2);
  }

  public static class Comparable {
    public static readonly Comparable<int> integer = new intComparable();

    public static Comparable<A> lambda<A>(Fn<A, A, CompareResult> compare) =>
      new Lambda<A>(compare);

    class Lambda<A> : Comparable<A> {
      readonly Fn<A, A, CompareResult> _compare;

      public Lambda(Fn<A, A, CompareResult> compare) { _compare = compare; }

      public CompareResult compare(A a1, A a2) => _compare(a1, a2);
    }

    class intComparable : Comparable<int> {
      public CompareResult compare(int a1, int a2) => a1.CompareTo(a2).asCmpRes();
    }
  }

  public static class ComparableOps {
    public static Comparable<B> comap<A, B>(this Comparable<A> cmp, Fn<B, A> mapper) =>
      Comparable.lambda<B>((b1, b2) => cmp.compare(mapper(b1), mapper(b2)));

    public static Option<A> max<A, C>(
      this C c, Comparable<A> comparable
    ) where C : IEnumerable<A> => minMax(c, comparable, CompareResult.GT);

    public static Option<A> min<A, C>(
      this C c, Comparable<A> comparable
    ) where C : IEnumerable<A> => minMax(c, comparable, CompareResult.LT);

    static Option<A> minMax<A, C>(
      this C c, Comparable<A> comparable, CompareResult lookFor
    ) where C : IEnumerable<A> {
      var current = Option<A>.None;
      foreach (var a in c) {
        if (current.isEmpty) current = a.some();
        else {
          if (comparable.compare(a, current.get) == lookFor) current = a.some();
        }
      }
      return current;
    }
  }
}