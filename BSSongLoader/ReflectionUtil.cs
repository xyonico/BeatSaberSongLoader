using System.Reflection;

namespace BSSongLoader
{
    class ReflectionUtil
    {
        public static void SetPrivateField(object obj, string fieldName, object value)
        {
            var prop = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            prop.SetValue(obj, value);
        }

        public static void InvokePrivateMethod(object obj, string methodName, object[] methodParams)
        {
            MethodInfo dynMethod = obj.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            dynMethod.Invoke(obj, methodParams);
        }
    }
}
