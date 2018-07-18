using System;
using System.Reflection;
using UnityEngine;

namespace SongLoaderPlugin
{
	public static class ReflectionUtil
	{
		public static void SetPrivateField(this object obj, string fieldName, object value)
		{
			var prop = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			prop.SetValue(obj, value);
		}
		
		public static T GetPrivateField<T>(this object obj, string fieldName)
		{
			var prop = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
			var value = prop.GetValue(obj);
			return (T) value;
		}
		
		public static void SetPrivateProperty(this object obj, string propertyName, object value)
		{
			var prop = obj.GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
			prop.SetValue(obj, value, null);
		}

		public static void InvokePrivateMethod(this object obj, string methodName, object[] methodParams)
		{
			MethodInfo dynMethod = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
			dynMethod.Invoke(obj, methodParams);
		}
		
		public static Component CopyComponent(Component original, Type originalType, Type overridingType,
			GameObject destination)
		{
			var copy = destination.AddComponent(overridingType);
			var fields = originalType.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance |
			                                    BindingFlags.GetField);
			foreach (var field in fields)
			{
				field.SetValue(copy, field.GetValue(original));
			}

			return copy;
		}
	}
}
