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
        public static object InvokeWithNamedParameters(this MethodBase self, object obj, List<object> paramaters)
        {
            var allArgs = new List<object>();

            var positionedParameters = paramaters.Where(x=>!(x is Tuple<string, object>)).ToArray();
            var namedParameters = paramaters.Where(x => (x is Tuple<string, object>)).Select(x => x as Tuple<string, object>).ToDictionary(x => x.Item1, x => x.Item2);

            allArgs.AddRange(positionedParameters);
            allArgs.AddRange(MapParameters(self, namedParameters));

            return self.Invoke(obj, allArgs.ToArray());
        }

        public static object[] MapParameters(MethodBase method, IDictionary<string, object> namedParameters)
        {
            var paramNames = method.GetParameters().Select(p => p.Name).ToArray();
            var parameters = new object[paramNames.Length];
            for (var i = 0; i < parameters.Length; ++i)
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
