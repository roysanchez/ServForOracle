using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Internal
{
    /// <summary>
    /// Extensions over the <see cref="Type"/> class
    /// </summary>
    internal static class TypeExtensions
    {
        /// <summary>
        /// Checks if the <paramref name="type"/> has the interface <see cref="IEnumerable"/>
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to check</param>
        /// <returns>If the type has the <see cref="IEnumerable"/> interface it means that the type is a collection</returns>
        public static bool IsCollection(this Type type)
        {
            return type.GetInterface(nameof(IEnumerable)) != null;
        }

        /// <summary>
        /// Looks for the underlying <see cref="Type"/> of the collection
        /// </summary>
        /// <param name="type">The collection type</param>
        /// <returns>
        /// <para>If the <paramref name="type"/> is a collection or an array returns the underlying <see cref="Type"/></para>
        /// <para>If <paramref name="type"/> is null or is not a collection returns null</para>
        /// </returns>
        /// <example>
        /// <list type="table">
        /// <listheader>
        /// <term><paramref name="type"/></term>
        /// <term>return value</term>
        /// </listheader>
        /// <item>
        /// <term><see cref="IEnumerable{string}"/></term>
        /// <term><see cref="string"/></term>
        /// </item>
        /// <item>
        /// <term><see cref="int[]"/></term>
        /// <term><see cref="int"/></term>
        /// </item>
        /// </list>
        /// </example>
        public static Type GetCollectionUnderType(this Type type)
        {
            if (type == null || !IsCollection(type))
                return null;

            return type.GetElementType() ?? type.GetGenericArguments().Single();
        }
    }
}
