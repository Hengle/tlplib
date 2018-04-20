﻿using UnityEngine;
using UnityEngine.UI;

namespace com.tinylabproductions.TLPLib.Tween.fun_tween.serialization.tweeners {
  [AddComponentMenu("")]
  public class Shadow_EffectColor : SerializedTweener<Color, Shadow> {
    public Shadow_EffectColor() : base(TweenOps.color, TweenMutators.shadowEffectColor, Defaults.color) { }
  }
}