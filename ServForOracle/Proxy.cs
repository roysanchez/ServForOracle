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
        /// Creates an object and a collection proxy for the <typeparamref name="T"/>
        /// </summary>
        /// <typeparam name="T">The user type to generate a proxy for</typeparam>
        /// <param name="udtName">The name of the Oracle UDT</param>
        /// <param name="udtCollectionName">The name of the Oracle UDT Collection</param>
        /// <seealso cref="CreateListType{T}(string)"/>
        /// <seealso cref="CreateType{T}(string)"/>
        public static void CreateTypeAndList<T>(string udtName, string udtCollectionName)
        {
            CreateType<T>(udtName);
            CreateListType<T>(udtCollectionName);
        }

        /// <summary>
        /// Creates an object and a collection proxy for the <paramref name="type"/>
        /// </summary>
        /// <param name="type">The user type to generate a proxy for</param>
        /// <param name="udtName">The name of the Oracle UDT</param>
        /// <param name="udtCollectionName">The name of the Oracle UDT Collection</param>
        /// <seealso cref="CreateListType{T}(string)"/>
        /// <seealso cref="CreateType{T}(string)"/>
        public static void CreateTypeAndList(Type type, string udtName, string udtCollectionName)
        {
            CreateType(type, udtName);
            CreateListType(type, udtCollectionName);
        }

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
        /// /// <exception cref="ArgumentException">If the <paramref name="udtCollectionName"/> has an invalid format</exception>
        public static void CreateListType(Type underlyingType, string udtCollectionName)
        {
            if (underlyingType == null)
            {
                throw new ArgumentNullException(nameof(underlyingType));
            }
            else if (string.IsNullOrWhiteSpace(udtCollectionName))
            {
                throw new ArgumentNullException(nameof(udtCollectionName));
            }
            else if (!Tools.Util.CheckUdtName(udtCollectionName))
            {
                throw new ArgumentException("The UDT name must have the format \"SCHEMA.UDTNAME\"", nameof(udtCollectionName));
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
        /// <exception cref="ArgumentException">If the <paramref name="udtName"/> has an invalid format</exception>
        public static void CreateType(Type type, string udtName)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            else if (string.IsNullOrWhiteSpace(udtName))
            {
                throw new ArgumentNullException(nameof(udtName));
            }
            else if (!Tools.Util.CheckUdtName(udtName))
            {
                throw new ArgumentException("The UDT name must have the format \"SCHEMA.UDTNAME\"", nameof(udtName));
            }

            ProxyFactory.GetOrCreateProxyType(type, udtName);
        }

        /// <summary>
        /// Adds the specified type to the existing proxy for the <paramref name="udtName"/>
        /// </summary>
        /// <typeparam name="T">The type to register the existing proxy to</typeparam>
        /// <param name="udtName">the udt name of the existing proxy</param>
        /// <seealso cref="UseExistingCollectionProxyType{T}(string)"/>
        /// <remarks>Doesn't check if the <typeparamref name="T"/> is similar</remarks>
        public static void UseExistingProxyType<T>(string udtName)
        {
            UseExistingProxyType(typeof(T), udtName);
        }

        /// <summary>
        /// Adds the specified type to the existing proxy for the <paramref name="udtName"/>
        /// </summary>
        /// <param name="userType">The type to register the existing proxy to</param>
        /// <param name="udtName">the udt name of the existing proxy</param>
        /// <seealso cref="UseExistingCollectionProxyType(Type, string)"/>
        /// <remarks>Doesn't check if the <paramref name="userType"/> is similar</remarks>
        public static void UseExistingProxyType(Type userType, string udtName)
        {
            if (string.IsNullOrWhiteSpace(udtName))
            {
                throw new ArgumentNullException(nameof(udtName));
            }

            ProxyFactory.AddTypeToExistingProxyType(userType, udtName);
        }

        /// <summary>
        /// Adds the specified collection type to the existing proxy for the <paramref name="collectionUdtName"/>
        /// </summary>
        /// <typeparam name="T">The type to register the existing collection proxy to</typeparam>
        /// <param name="collectionUdtName">the udt collecton name of the existing proxy</param>
        /// <seealso cref="UseExistingProxyType{T}(string)"/>
        /// <remarks>Doesn't check if the <typeparamref name="T"/> is similar</remarks>
        public static void UseExistingCollectionProxyType<T>(string collectionUdtName)
        {
            UseExistingCollectionProxyType(typeof(T), collectionUdtName);
        }

        /// <summary>
        /// Adds the specified collection type to the existing proxy for the <paramref name="collectionUdtName"/>
        /// </summary>
        /// <param name="userType">The type to register the existing collection proxy to</param>
        /// <param name="collectionUdtName">the udt collecton name of the existing proxy</param>
        /// <seealso cref="UseExistingProxyType(Type, string)"/>
        /// <remarks>Doesn't check if the <paramref name="userType"/> is similar</remarks>
        public static void UseExistingCollectionProxyType(Type userType, string collectionUdtName)
        {
            if (string.IsNullOrWhiteSpace(collectionUdtName))
            {
                throw new ArgumentNullException(nameof(collectionUdtName));
            }

            ProxyFactory.AddCollectionTypeToExistingProxyType(userType, collectionUdtName);
        }
    }
}
