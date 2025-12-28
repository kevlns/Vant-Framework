using System;
using System.Collections.Generic;

namespace Vant.Utils
{
    public static class ArgsHelper
    {
        // Support args being null, single object, or object[]
        public static T GetArg<T>(object args, int index = 0)
        {
            if (args == null) return default;

            if (args is T t)
            {
                if (index == 0) return t;
                return default;
            }

            if (args is object[] arr)
            {
                if (index < 0 || index >= arr.Length) return default;
                var val = arr[index];
                if (val is T vt) return vt;
                try
                {
                    return (T)Convert.ChangeType(val, typeof(T));
                }
                catch
                {
                    return default;
                }
            }

            // If args is not array and not T, try convert when index == 0
            if (index == 0)
            {
                try
                {
                    return (T)Convert.ChangeType(args, typeof(T));
                }
                catch
                {
                    return default;
                }
            }

            return default;
        }
    }
}
