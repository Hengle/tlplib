﻿using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;

namespace com.tinylabproductions.TLPLib.Tween.fun_tween.serialization.tween_callbacks {
  [AddComponentMenu("")]
  public class Callback_UnityEvent : SerializedTweenCallback {

    #region Unity Serialized Fields
#pragma warning disable 649
    // ReSharper disable FieldCanBeMadeReadOnly.Local
    [SerializeField] InvokeOn _invokeOn;
    [SerializeField, NotNull] UnityEvent _onEvent;
    // ReSharper restore FieldCanBeMadeReadOnly.Local
#pragma warning restore 649
    #endregion

    protected override TweenCallback createCallback() => 
      new TweenCallback(evt => {
        if (shouldInvoke(_invokeOn, evt)) _onEvent.Invoke();
      });
    
    public override string ToString() => $"Unity Event @ {_invokeOn}";
    #if UNITY_EDITOR
    public override void setDuration(float dur) { throw new System.NotImplementedException(); }
    #endif
  }
}