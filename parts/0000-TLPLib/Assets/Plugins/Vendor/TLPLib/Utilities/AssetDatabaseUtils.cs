﻿#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.tinylabproductions.TLPLib.Filesystem;
using com.tinylabproductions.TLPLib.Logger;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace com.tinylabproductions.TLPLib.Utilities {
  public static class AssetDatabaseUtils {
    public static IEnumerable<A> getPrefabsOfType<A>() {
      var prefabGuids = AssetDatabase.FindAssets("t:prefab");

      var prefabs = prefabGuids
        .Select(loadMainAssetByGuid)
        .OfType<GameObject>();

      var components = new List<A>();

      foreach (var go in prefabs) {
        go.GetComponentsInChildren<A>(includeInactive: true, results: components);
        foreach (var c in components) yield return c;
      }
    }

    /// <summary>
    /// Sometimes Unity fails to find scriptable objects using the t: selector.
    /// 
    /// Our known workaround:
    /// 1. Open asset references window.
    /// 2. Find all instances of your scriptable object.
    /// 3. Show Actions > Set Dirty
    /// 4. Save project.
    /// 5. Profit!
    /// </summary>
    /// <typeparam name="A"></typeparam>
    /// <returns></returns>
    public static IEnumerable<A> getScriptableObjectsOfType<A>() where A : ScriptableObject =>
      AssetDatabase.FindAssets($"t:{typeof(A).Name}")
      .Select(loadMainAssetByGuid)
      .OfType<A>();

    public static Object loadMainAssetByGuid(string guid) =>
      AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
    
    public static string copyAssetAndGetPath<T>(T obj, PathStr path) where T: Object {
      var originalPath = AssetDatabase.GetAssetPath(obj);
      var newPath = path.unityPath + "/" + obj.name + Path.GetExtension(originalPath);
      if (Log.d.isDebug())
        Log.d.debug($"{nameof(AssetDatabaseUtils)}#{nameof(copyAssetAndGetPath)}: " +
          $"copying asset from {originalPath} to {newPath}");
      if (!AssetDatabase.CopyAsset(originalPath, newPath))
        throw new Exception($"Couldn't copy asset from {originalPath} to {newPath}");
      return newPath;
    }

    static bool _assetsAreBeingEdited;

    public static void startAssetEditing() {
      if (!_assetsAreBeingEdited)
        AssetDatabase.StartAssetEditing();
      _assetsAreBeingEdited = true;
    }
    public static void stopAssetEditing() {
      if (_assetsAreBeingEdited)
        AssetDatabase.StopAssetEditing();
      _assetsAreBeingEdited = false;
    }
  }
}
#endif
