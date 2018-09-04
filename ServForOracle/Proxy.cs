using ServForOracle.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    /// <summary>
    /// Used to create proxies for either Oracle UDT Collections or Objects that don't have the 
    /// <see cref="UDTCollectionNameAttribute"/> or <see cref="UDTNameAttribute"/>
    /// </summary>
    public static class Proxy
    {
        /// <summary>
        /// Creates a collection proxy for the <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The underlying type to use for the collection</typeparam>
        /// <param name="udtCollectionName">The name of the Oracle UDT Collection</param>
        /// <exception cref="Exception">If the <typeparamref name="T"/> is not a CLR or a class with 
        /// the <see cref="UDTNameAttribute"/> it will raise an exception
        /// </exception>
        /// <remarks>Wrapper over <see cref="CreateListType(Type, string)"/></remarks>
        /// <seealso cref="CreateListType(Type, string)"/>
        public static void CreateListType<T>(string udtCollectionName)
        {
            CreateListType(typeof(T), udtCollectionName);
        }

        /// <summary>
        /// Creates a collection proxy for the <paramref name="underlyingType"/>
        /// </summary>
        /// <param name="underlyingType">The underlying type to use for the collection</param>
        /// <param name="udtCollectionName">The name of the Oracle UDT Collection</param>
        /// <seealso cref="CreateListType{T}(string)"/>
        /// <exception cref="ArgumentNullException">if either <paramref name="underlyingType"/> or 
        /// <paramref name="udtCollectionName"/> is null or empty
        /// </exception>
        public static void CreateListType(Type underlyingType, string udtCollectionName)
        {
            if(underlyingType == null)
            {
                throw new ArgumentNullException(nameof(underlyingType));
            }
            else if(string.IsNullOrWhiteSpace(udtCollectionName))
            {
                throw new ArgumentNullException(nameof(udtCollectionName));
            }

            ProxyFactory.GetOrCreateProxyCollectionType(underlyingType, udtCollectionName);
        }

        /// <summary>
        /// Creates an object proxy for the <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The base type for the proxy</typeparam>
        /// <param name="udtName">The name of the Oracle UDT object</param>
        /// <remarks>Wrapper over <see cref="CreateType(Type, string)"/></remarks>
        /// <seealso cref="CreateType(Type, string)"/>
        public static void CreateType<T>(string udtName)
        {
            CreateType(typeof(T), udtName);
        }

        /// <summary>
        /// Creates an object proxy for the <paramref name="type"/>
        /// </summary>
        /// <param name="type">The base type for the proxy</param>
        /// <param name="udtName">The name of the Oracle UDT object</param>
        /// <seealso cref="CreateType{T}(string)"/>
        /// <exception cref="ArgumentNullException">If either the <paramref name="type"/> or <paramref name="udtName"/> 
        /// is null or empty.
        /// </exception>
        public static void CreateType(Type type, string udtName)
        {
            if(type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            else if(string.IsNullOrWhiteSpace(udtName))
            {
                throw new ArgumentNullException(nameof(udtName));
            }

            ProxyFactory.GetOrCreateProxyType(type, udtName);
        }
    }
}
