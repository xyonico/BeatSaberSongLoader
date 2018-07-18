using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SongLoaderPlugin
{
	public class ScriptableObjectPool<T> where T : ScriptableObject
	{
		private readonly List<T> _pool = new List<T>();
		private readonly List<T> _createdObj = new List<T>();

		public T Get()
		{
			if (_pool.Count == 0)
			{
				var newObj = ScriptableObject.CreateInstance<T>();
				_createdObj.Add(newObj);
				return newObj;
			}

			var fromPool = _pool.First();
			_pool.RemoveAt(0);
			return fromPool;
		}

		public void Return(T obj)
		{
			_pool.Add(obj);
		}

		public void ReturnAll()
		{
			_pool.Clear();
			_pool.AddRange(_createdObj);
		}
	}
}