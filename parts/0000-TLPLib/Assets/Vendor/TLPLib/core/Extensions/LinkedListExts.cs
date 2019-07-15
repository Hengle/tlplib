﻿using System.Collections.Generic;
using com.tinylabproductions.TLPLib.Collection;
using com.tinylabproductions.TLPLib.Functional;
using pzd.lib.functional;

namespace com.tinylabproductions.TLPLib.Extensions {
  public static class LinkedListExts {
    public static bool isEmpty<A>(this ReadOnlyLinkedList<A> list) => list.Count == 0;
    public static bool isEmpty<A>(this LinkedList<A> list) => list.Count == 0;

    /* Removes first element and returns it. */
    public static Option<A> shift<A>(this LinkedList<A> list) {
      var a = F.opt(list.First);
      list.RemoveFirst();
      return a.map(_ => _.Value);
    }

    /* Removes last element and returns it. */
    public static Option<A> pop<A>(this LinkedList<A> list) {
      var a = F.opt(list.Last);
      list.RemoveLast();
      return a.map(_ => _.Value);
    }

    public static LinkedListNode<A> nextOrFirst<A>(
      this LinkedListNode<A> node
    ) => node.Next ?? node.List.First;
  }
}