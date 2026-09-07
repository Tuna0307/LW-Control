using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace LWControl.Diagnostics
{
    // Small managed bridge used by the Lua research probe. XLua can hand an
    // object[] to this delegate reliably; managed code then converts the block
    // index array to the exact SendAoiRequest parameter type before invoking it.
    public static class WorldBlockSender
    {
        public static readonly Action<object[]> SendAoi = InvokeSendAoi;
        public static readonly Action<object[]> SendRectMarch = InvokeSendRectMarch;

        private static void InvokeSendAoi(object[] args)
        {
            if (args == null || args.Length != 9)
                throw new ArgumentException("SendAoi requires manager plus 8 arguments.");
            object target = args[0];
            if (target == null)
                throw new ArgumentNullException("target");

            MethodInfo[] methods = target.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Exception last = null;
            foreach (MethodInfo method in methods)
            {
                if (method.Name != "SendAoiRequest")
                    continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 8)
                    continue;
                try
                {
                    object[] invokeArgs = new object[8];
                    for (int i = 0; i < invokeArgs.Length; i++)
                        invokeArgs[i] = ConvertArgument(args[i + 1], parameters[i].ParameterType);
                    method.Invoke(target, invokeArgs);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }
            throw new MissingMethodException(
                "Compatible WorldPointManager.SendAoiRequest was not found.", last);
        }

        private static void InvokeSendRectMarch(object[] args)
        {
            if (args == null || args.Length != 3)
                throw new ArgumentException("SendRectMarch requires assembly anchor plus tile x/y.");
            object anchor = args[0];
            if (anchor == null)
                throw new ArgumentNullException("anchor");

            Assembly gameAssembly = anchor.GetType().Assembly;
            Type messageType = gameAssembly.GetType("WorldGetRectMarchInfosMessage", true);
            Type requestType = gameAssembly.GetType("WorldGetRectMarchInfosMessage+Request", true);
            object request = Activator.CreateInstance(requestType, true);

            FieldInfo xField = requestType.GetField(
                "x", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo yField = requestType.GetField(
                "y", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (xField == null || yField == null)
                throw new MissingFieldException("WorldGetRectMarchInfosMessage.Request x/y fields were not found.");
            xField.SetValue(request, Convert.ToInt32(args[1]));
            yField.SetValue(request, Convert.ToInt32(args[2]));

            PropertyInfo instanceProperty = messageType.GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            object message = instanceProperty == null ? null : instanceProperty.GetValue(null, null);
            if (message == null)
            {
                MethodInfo getInstance = messageType.GetMethod(
                    "get_Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (getInstance != null)
                    message = getInstance.Invoke(null, null);
            }
            if (message == null)
                throw new MissingMemberException("WorldGetRectMarchInfosMessage.Instance was not found.");

            MethodInfo send = null;
            foreach (MethodInfo method in messageType.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "Send")
                    continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(object[]))
                {
                    send = method;
                    break;
                }
            }
            if (send == null)
                throw new MissingMethodException("WorldGetRectMarchInfosMessage.Send(object[]) was not found.");

            send.Invoke(message, new object[] { new object[] { request } });
        }

        private static object ConvertArgument(object value, Type targetType)
        {
            if (value == null)
                return null;
            if (targetType.IsInstanceOfType(value))
                return value;

            if (targetType.IsGenericType &&
                targetType.GetGenericTypeDefinition() == typeof(List<>) &&
                targetType.GetGenericArguments()[0] == typeof(int))
            {
                var result = new List<int>();
                IEnumerable enumerable = value as IEnumerable;
                if (enumerable == null)
                    throw new InvalidCastException("Block indexes are not enumerable.");
                foreach (object item in enumerable)
                    result.Add(Convert.ToInt32(item));
                return result;
            }

            if (targetType.IsArray && targetType.GetElementType() == typeof(int))
            {
                var result = new List<int>();
                IEnumerable enumerable = value as IEnumerable;
                if (enumerable == null)
                    throw new InvalidCastException("Block indexes are not enumerable.");
                foreach (object item in enumerable)
                    result.Add(Convert.ToInt32(item));
                return result.ToArray();
            }

            return Convert.ChangeType(value, targetType);
        }
    }
}
