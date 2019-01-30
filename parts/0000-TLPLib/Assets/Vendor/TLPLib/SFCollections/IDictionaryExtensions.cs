﻿using System;
using System.Collections.Generic;
using com.tinylabproductions.TLPLib.Functional;

namespace Smooth.Collections {

	/// <summary>
	/// Extension methods for IDictionary<>s.
	/// </summary>
	public static class IDictionaryExtensions {

		/// <summary>
		/// Analog to IDictionary&lt;K, V&gt;.TryGetValue(K, out V) that returns an option instead of using an out parameter.
		/// </summary>
		public static Option<V> TryGet<K, V>(this IDictionary<K, V> dictionary, K key) {
			V value;
		  return dictionary.TryGetValue(key, out value) ? new Option<V>(value) : Option<V>.None;
		}
	}
}
