﻿using com.tinylabproductions.TLPLib.Functional;
using UnityEngine;

namespace com.tinylabproductions.TLPLib.Extensions {
  public static class VectorExts {
    public static Vector2 withX(this Vector2 v, float x) => new Vector2(x, v.y);
    public static Vector2 withY(this Vector2 v, float y) => new Vector2(v.x, y);

    public static Vector3 withX(this Vector3 v, float x) => new Vector3(x, v.y, v.z);
    public static Vector2 withY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    public static Vector3 withZ(this Vector3 v, float z) => new Vector3(v.x, v.y, z);

    public static Vector2 with2(
      this Vector2 v,
#if ENABLE_IL2CPP
      Option<float> x = null,
      Option<float> y = null
#else
      Option<float> x = new Option<float>(), 
      Option<float> y = new Option<float>()
#endif
    ) {
#if ENABLE_IL2CPP
      if (null == x) x = Option<float>.None;
      if (null == y) y = Option<float>.None;
#endif
      return new Vector3(x.getOrElse(v.x), y.getOrElse(v.y));
    }

    public static Vector3 with3(
      this Vector3 v,
#if ENABLE_IL2CPP
      Option<float> x = null,
      Option<float> y = null,
      Option<float> z = null
#else
      Option<float> x = new Option<float>(), 
      Option<float> y = new Option<float>(),
      Option<float> z = new Option<float>()
#endif
    ) {
#if ENABLE_IL2CPP
      if (null == x) x = Option<float>.None;
      if (null == y) y = Option<float>.None;
      if (null == z) z = Option<float>.None; 
#endif
      return new Vector3(x.getOrElse(v.x), y.getOrElse(v.y), z.getOrElse(v.z));
    }

    public static Vector2 rotate90(this Vector2 v) => new Vector2(-v.y, v.x);

    public static Vector2 rotate(this Vector2 v, float degrees) {
      var radians = degrees * Mathf.Deg2Rad;
      var sin = Mathf.Sin(radians);
      var cos = Mathf.Cos(radians);
         
      var tx = v.x;
      var ty = v.y;
 
      return new Vector2(cos * tx - sin * ty, sin * tx + cos * ty);
    }
  }
}
