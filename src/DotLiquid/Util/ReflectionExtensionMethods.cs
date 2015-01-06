using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotLiquid.Util
{
    using System.Reflection;

    public static class ReflectionExtensionMethods
    {
        public static object InvokeWithNamedParameters(this MethodBase self, object obj, IDictionary<string, object> namedParameters)
        {
            return self.Invoke(obj, MapParameters(self, namedParameters));
        }

        public static object[] MapParameters(MethodBase method, IDictionary<string, object> namedParameters)
        {
            string[] paramNames = method.GetParameters().Select(p => p.Name).ToArray();
            object[] parameters = new object[paramNames.Length];
            for (int i = 0; i < parameters.Length; ++i)
            {
                parameters[i] = Type.Missing;
            }

            //var indexInCollection = 0;
            foreach (var item in namedParameters)
            {
                var paramName = item.Key;
                var paramIndex = Array.IndexOf(paramNames, paramName);
                if (paramIndex == -1) // skip not found parameters
                    continue;
                
                //var paramIndex = index == -1 ? indexInCollection : index;
                parameters[paramIndex] = item.Value;
                //indexInCollection++;
            }
            return parameters;
        }
    }
}
