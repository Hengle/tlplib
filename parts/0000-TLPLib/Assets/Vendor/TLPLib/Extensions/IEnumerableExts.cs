﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.tinylabproductions.TLPLib.Functional;

namespace com.tinylabproductions.TLPLib.Extensions {
  public static class IEnumerableExts {
    /* This should really be used only for debugging. It is pretty slow. */
    public static String asString(
      this IEnumerable enumerable, 
      bool newlines=true, bool fullClasses=false
    ) {
      var items = (
        from object item in enumerable
        let str = item as String // String is IEnumerable as well
        let enumItem = item as IEnumerable
        select str ?? (
          enumItem == null 
            ? item.ToString() : enumItem.asString(newlines, fullClasses)
        )
      ).ToArray();
      var itemsStr = 
        string.Join(string.Format(",{0} ", newlines ? "\n " : ""), items);
      if (items.Length != 0 && newlines) itemsStr = "\n  " + itemsStr + "\n";

      var type = enumerable.GetType();
      return string.Format(
        "{0}[{1}]",
        fullClasses ? type.FullName : type.Name,
        itemsStr
      );
    }

    public static string mkString<A>(
      this IEnumerable<A> e, string separator, string start = null, string end = null
    ) {
      var sb = new StringBuilder();
      if (start != null) sb.Append(start);
      var first = true;
      foreach (var a in e) {
        if (first) first = false;
        else sb.Append(separator);
        sb.Append(a);
      }
      if (end != null) sb.Append(end);
      return sb.ToString();
    }

    public static IEnumerable<A> Yield<A>(this A any) {
      yield return any;
    }

    public static void each<A>(this IEnumerable<A> enumerable, Act<A> f) {
      foreach (var el in enumerable) f(el);
    }

    public static Option<A> find<A>(this IEnumerable<A> enumerable, Fn<A, bool> predicate) {
      foreach (var a in enumerable) if (predicate(a)) return F.some(a);
      return F.none<A>();
    }

    public static IEnumerable<Tpl<A, int>> zipWithIndex<A>(this IEnumerable<A> enumerable) {
      var idx = 0;
      foreach (var a in enumerable) {
        yield return F.t(a, idx);
        idx += 1;
      }
    }

    public static IEnumerable<A> flatten<A>(this IEnumerable<Option<A>> enumerable)
      { return enumerable.SelectMany(_ => _.asEnum()); }

    public static IEnumerable<B> collect<A, B>(
      this IEnumerable<A> enumerable, Fn<A, Option<B>> collector
    ) {
      foreach (var a in enumerable) {
        var bOpt = collector(a);
        if (bOpt.isDefined) yield return bOpt.get;
      }
    }

    public static Option<B> collectFirst<A, B>(
      this IEnumerable<A> enumerable, Fn<A, Option<B>> collector
    ) {
      foreach (var a in enumerable) {
        var bOpt = collector(a);
        if (bOpt.isDefined) return bOpt;
      }
      return F.none<B>();
    }

    public static HashSet<A> toHashSet<A>(this IEnumerable<A> enumerable) {
      return new HashSet<A>(enumerable);
    }
  }
}