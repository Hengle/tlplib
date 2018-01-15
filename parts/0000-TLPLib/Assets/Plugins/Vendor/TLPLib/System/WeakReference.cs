﻿using WR = System.WeakReference;
using com.tinylabproductions.TLPLib.Functional;

namespace com.tinylabproductions.TLPLib.system {
  public static class WeakReference {
    public static WeakReference<A> a<A>(
      A reference, bool trackResurrection = false
    ) where A : class =>
      new WeakReference<A>(reference, trackResurrection);
  }

  public struct WeakReference<A> where A : class {
    readonly WR reference;

    public WeakReference(A reference, bool trackResurrection = false) {
      this.reference = new WR(reference, trackResurrection);
    }

    public bool IsAlive => reference.IsAlive;
    public bool TrackResurrection => reference.TrackResurrection;
    public Option<A> Target => F.opt(reference.Target as A);

    public override string ToString() =>
      $"{nameof(WeakReference<A>)}({Target})";
  }
}