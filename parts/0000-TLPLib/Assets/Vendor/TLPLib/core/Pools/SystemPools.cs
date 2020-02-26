using System;
using System.Collections.Generic;
using System.Text;
using Smooth.Dispose;

namespace Smooth.Pools {
	/// <summary>
	/// Singleton List<T> pool.
	/// </summary>
	public static class ListPool<T> {
		private static readonly Pool<List<T>> _Instance = new Pool<List<T>>(
			() => new List<T>(),
			list => list.Clear());

		/// <summary>
		/// Singleton List<T> pool instance.
		/// </summary>
		public static Pool<List<T>> Instance { get { return _Instance; } }
	}

	/// <summary>
	/// Singleton LinkedList<T> pool.
	/// </summary>
	public static class LinkedListPool<T> {
		private static readonly Pool<LinkedList<T>> _Instance = new Pool<LinkedList<T>>(
			() => new LinkedList<T>(),
			list => {
				var node = list.First;
				while (node != null) {
					list.RemoveFirst();
					LinkedListNodePool<T>.Instance.Release(node);
					node = list.First;
				}
			}
		);

		/// <summary>
		/// Singleton LinkedList<T> pool instance.
		/// </summary>
		public static Pool<LinkedList<T>> Instance { get { return _Instance; } }
	}

	/// <summary>
	/// Singleton LinkedListNode<T> pool.
	/// </summary>
	public static class LinkedListNodePool<T> {
		private static readonly PoolWithInitializer<LinkedListNode<T>, T> _Instance = new PoolWithInitializer<LinkedListNode<T>, T>(
			() => new LinkedListNode<T>(default(T)),
			node => node.Value = default(T),
			(node, value) => node.Value = value
		);

		/// <summary>
		/// Singleton LinkedListNode<T> pool instance.
		/// </summary>
		public static PoolWithInitializer<LinkedListNode<T>, T> Instance { get { return _Instance; } }
	}

	/// <summary>
	/// Singleton StringBuilder pool.
	/// </summary>
	public static class StringBuilderPool {
		private static readonly Pool<StringBuilder> _Instance = new Pool<StringBuilder>(
			() => new StringBuilder(),
			sb => sb.Length = 0);

		/// <summary>
		/// Singleton StringBuilder pool instance.
		/// </summary>
		public static Pool<StringBuilder> Instance { get { return _Instance; } }
	}
}
