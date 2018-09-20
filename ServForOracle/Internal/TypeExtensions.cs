using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            return 
                type.IsArray
                ||
                (type.IsInterface && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ||
                (type != typeof(string) && !type.IsValueType && type.GetInterface(nameof(IEnumerable)) != null);
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

            var ga = type.GetGenericArguments();
            if (ga.Length > 0)
                return ga.Single();
            else
                return type.GetElementType();
        }

        /// <summary>
        /// Looks for a property that is assignable to the <paramref name="assignableCollectionType"/> specified.
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to check the properties on</param>
        /// <param name="name">The name of the property</param>
        /// <param name="assignableCollectionType">The <see cref="Type"/> to check if it is assignable to</param>
        /// <returns>If found, the property with the assignable type</returns>
        /// <remarks>This extension method checks if one of the properties is <see cref="Type.IsAssignableFrom(Type)"/>
        /// from the <paramref name="assignableCollectionType"/> specified.
        /// </remarks>
        public static PropertyInfo GetCollectionProperty(this Type type, string name, Type assignableCollectionType)
        {
            return type.GetProperties()
                .Where(c => c.Name == name && assignableCollectionType.IsAssignableFrom(c.PropertyType))
                .FirstOrDefault();
        }

        /// <summary>
        /// Looks for the property with the specified <paramref name="name"/> as well as checks if the property is a 
        /// collection and its assignable with specified <paramref name="typeOrAssignable"/>
        /// </summary>
        /// <param name="type">The type to look the property into</param>
        /// <param name="name">The name of the property to look</param>
        /// <param name="typeOrAssignable">The type of the property or an assignable type of the property</param>
        /// <returns>If it exists, the property with the specified <paramref name="typeOrAssignable"/></returns>
        public static PropertyInfo GetTypeOrCollectionProperty(this Type type, string name, Type typeOrAssignable)
        {
            if (typeOrAssignable.IsCollection())
            {
                return type.GetCollectionProperty(name, typeOrAssignable);
            }
            else
            {
                return type.GetProperty(name, typeOrAssignable);
            }
        }

        /// <summary>
        /// Gets a collection property with the <paramref name="underlyingType"/>
        /// </summary>
        /// <param name="type">The type to look the property into</param>
        /// <param name="name">The name of the property</param>
        /// <param name="underlyingType">The underlying <see cref="Type"/> of the collection</param>
        /// <returns>If it exists, the collection property with the <paramref name="underlyingType"/></returns>
        public static PropertyInfo GetCollectionPropertyByUnderlyingType(this Type type, string name, Type underlyingType)
        {
            return type.GetProperties()
                .Where(c => c.Name == name && c.PropertyType.GetCollectionUnderType() == underlyingType)
                .FirstOrDefault();
        }
    }
}
