using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Internal
{
    internal static class TypeExtensions
    {
        public static bool IsCollection(this Type type)
        {
            return type.GetInterface(nameof(IEnumerable)) != null;
        }

        public static Type GetCollectionUnderType(this Type type)
        {
            if (type == null || !IsCollection(type))
                return null;

            return type.GetElementType() ?? type.GetGenericArguments().Single();
        }
    }
}
