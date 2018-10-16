using ConcurrentCollections;
using Oracle.DataAccess.Types;
using ServForOracle.Models;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Internal
{
    internal static partial class ProxyFactory
    {
        /// <summary>
        /// The Generated <see cref="Assembly"/> assembly
        /// </summary>
        internal static Assembly Assembly => AppDomain.CurrentDomain.GetAssemblies()
                                                                    .Where(c => c.GetName().Name == NAME)
                                                                    .FirstOrDefault();

        /// <summary>
        /// Read-Only dictionary with all the object types for the OracleDB.
        /// The Key is the Type with the UDTNameAttribute.
        /// The Value is a tuple, with the generated proxy type and the Oracle UDT description.
        /// </summary>
        /// <value>[{<see cref="Type"/> BaseType, (<see cref="Type"/> ProxyType, "HR.CLIENT_OBJ")}]</value>
        internal static ConcurrentDictionary<Type, (Type ProxyType, string UdtName)> Proxies { get; private set; }
        /// <summary>
        /// Read-Only dictionary with all the collections types for the OracleDB.
        /// The Key is an Array of the generated proxy type.
        /// The Value is the Orascle UDT Collection Name.
        /// </summary>
        /// <value>[{<see cref="Type[]"/>, "HR.STRING_LIST"}, {<see cref="Type[]"/>, "HR.NUMBER_LIST"}]</value>
        internal static ConcurrentDictionary<Type, (Type ProxyCollectionType, string UdtCollectionName)> CollectionProxies { get; private set; }

        /// <summary>
        /// Used to check if the UDT type is registered
        /// </summary>
        internal static ConcurrentHashSet<string> UDTLists;

        /// <summary>
        /// Checks if the <paramref name="proxyType"/> is registered
        /// </summary>
        /// <param name="proxyType">The <see cref="Type"/> to check</param>
        /// <returns>True if the <paramref name="proxyType"/> is registered as a proxy</returns>
        public static bool IsValidProxyType(Type proxyType)
        {
            if (proxyType == null)
            {
                return false;
            }

            return Proxies.ContainsKey(proxyType) || (
                proxyType.IsCollection() && CollectionProxies.ContainsKey(proxyType.GetCollectionUnderType())
            );
        }

        /// <summary>
        /// Maps an existing proxy to another user type
        /// </summary>
        /// <param name="userType">The user <see cref="Type"/> to map</param>
        /// <param name="udtName">The name of the udt that was already mapped</param>
        /// <remarks>
        /// The <paramref name="udtName"/> has to be registered either through the <see cref="UDTNameAttribute"/>
        /// or using the method <see cref="GetOrCreateProxyType(Type, string)"/>
        /// </remarks>
        /// <seealso cref="AddCollectionTypeToExistingProxyType(Type, string)"/>
        internal static void AddTypeToExistingProxyType(Type userType, string udtName)
        {
            if (string.IsNullOrWhiteSpace(udtName))
                throw new ArgumentNullException(nameof(udtName));
            if (userType == null)
                throw new ArgumentNullException(nameof(userType));
            if (!UDTLists.Any(c => c == udtName))
                throw new ArgumentException("The udtName has to be registered", nameof(udtName));

            var proxy = GetProxyTypeFromUdtName(udtName);

            if (proxy != default)
            {
                Proxies.TryAdd(userType, proxy);
            }
        }

        /// <summary>
        /// Maps an existing collection proxy to another user type
        /// </summary>
        /// <param name="userType">The user <see cref="Type"/> to map</param>
        /// <param name="udtCollectionName">The name of the udt collection that was already mapped</param>
        /// <remarks>
        /// The <paramref name="udtCollectionName"/> has to be registered either through the <see cref="UDTCollectionNameAttribute"/>
        /// or using the method <see cref="GetOrCreateProxyCollectionType(Type, string)(Type, string)"/>
        /// </remarks>
        /// <seealso cref="AddTypeToExistingProxyType(Type, string)"/>
        internal static void AddCollectionTypeToExistingProxyType(Type userType, string udtCollectionName)
        {
            if (string.IsNullOrWhiteSpace(udtCollectionName))
                throw new ArgumentNullException(nameof(udtCollectionName));
            if (userType == null)
                throw new ArgumentNullException(nameof(userType));
            if (!UDTLists.Any(c => c == udtCollectionName))
                throw new ArgumentException("The udtName has to be registered", nameof(udtCollectionName));

            var proxy = GetCollectionProxyTypeFromUdtName(udtCollectionName);

            if (proxy != default)
            {
                CollectionProxies.TryAdd(userType, proxy);
            }
        }

        /// <summary>
        /// Automapper can't convert to generated types, in the meanwhile this will do
        /// </summary>
        /// <param name="value">The value to try an convert</param>
        /// <param name="userType">The type of the <paramref name="value"/></param>
        /// <returns>If its a proxy then returns the converted type otherwise is left as is</returns>
        /// <seealso cref="ConvertToProxy(object, Type)"/>
        internal static dynamic ConvertToProxy<T>(T value)
        {
            return ConvertToProxy(value, typeof(T));
        }

        /// <summary>
        /// Automapper can't convert to generated types, in the meanwhile this will do
        /// </summary>
        /// <param name="value">The value to try an convert</param>
        /// <param name="userType">The type of the <paramref name="value"/></param>
        /// <returns>If its a proxy then returns the converted type otherwise is left as is</returns>
        /// <seealso cref="ConvertToProxy{T}(T)"/>
        internal static dynamic ConvertToProxy(dynamic value, Type userType, Type proxyOverrideType = null)
        {
            if (value != null && userType.IsAssignableFrom(value.GetType()))
            {
                (Type proxyType, string) proxy = (null, null);

                if (proxyOverrideType != null || Proxies.TryGetValue(userType, out proxy))
                {
                    var proxyType = proxyOverrideType ?? proxy.proxyType;
                    var instance = Activator.CreateInstance(proxyType);

                    var proxyProperties = proxyType.GetProperties();

                    //goes through the user type properties
                    foreach (var prop in userType.GetProperties())
                    {
                        if (prop.Name == nameof(TypeFactory.Null) || prop.Name == nameof(TypeFactory.IsNull)
                            || !proxyProperties.Any(p => p.Name == prop.Name))
                        {
                            continue;
                        }

                        //checks if the proxy property is the same type as the user property
                        var proxyProp = proxyType.GetProperty(prop.Name, prop.PropertyType);
                        if (proxyProp != null)
                        {
                            proxyProp.SetValue(instance, prop.GetValue(value));
                        }
                        else if (IsValidProxyType(prop.PropertyType))
                        {
                            var propProxyType = GetProxyTypeFromUserType(prop.PropertyType);
                            proxyProp = proxyType.GetTypeOrCollectionProperty(prop.Name, propProxyType);

                            if (proxyProp != null)
                            {
                                proxyProp.SetValue(instance, ConvertToProxy(prop.GetValue(value), prop.PropertyType, propProxyType));
                            }
                            else
                            {
                                throw new Exception($"Error trying to convert the type {prop.PropertyType.Name} to {propProxyType.Name}"
                                    + $", for the property {prop.Name} in the type {userType.Name}");
                            }
                        }
                        else
                        {
                            throw new Exception($"Error trying to convert the type {prop.PropertyType.Name}"
                                    + $", for the property {prop.Name} in the type {userType.Name}");
                        }
                    }

                    return instance;
                }
                else if (
                    userType.IsCollection()
                    &&
                    (proxyOverrideType != null ||
                    CollectionProxies.TryGetValue(userType.GetCollectionUnderType(), out proxy)
                   ))
                {
                    var proxyType = proxyOverrideType ?? proxy.proxyType;
                    var proxyUnderType = proxyType.GetCollectionUnderType();
                    var userUnderType = userType.GetCollectionUnderType();

                    //if it's the same type then just transform to the corresponding list
                    if (proxyUnderType == userUnderType)
                    {
                        if (proxyType.IsArray)
                            return Enumerable.ToArray(value);
                        else
                            return Enumerable.AsEnumerable(Enumerable.ToList(value));
                    }
                    else
                    {
                        var listType = typeof(List<>).MakeGenericType(proxyUnderType);
                        dynamic list = Activator.CreateInstance(listType);

                        foreach (var v in value as IEnumerable)
                        {
                            list.Add(ConvertToProxy(v, userUnderType, proxyUnderType));
                        }

                        return Enumerable.AsEnumerable(list);
                    }
                }
            }

            return value;
        }

        /// <summary>
        /// Converts from proxy types to userTypes
        /// </summary>
        /// <typeparam name="T">The expected user type</typeparam>
        /// <param name="value">The value to transform</param>
        /// <param name="proxyType">The proxy type of the value</param>
        /// <returns>The value transformed to the <typeparamref name="T"/></returns>
        internal static T ConvertFromProxy<T>(object value, Type proxyType)
        {
            return (T)ConvertFromProxy(value, proxyType, typeof(T));
        }

        /// <summary>
        /// Convert from <paramref name="proxyType"/> to <paramref name="userType"/>
        /// </summary>
        /// <param name="value">The value to transform</param>
        /// <param name="proxyType">The proxy <see cref="Type"/> of the value</param>
        /// <param name="userType">The user <see cref="Type"/> to convert to</param>
        /// <returns>The value transformed to the <paramref name="userType"/></returns>
        internal static dynamic ConvertFromProxy(dynamic value, Type proxyType, Type userType)
        {
            if (value != null)
            {
                if (proxyType == userType)
                {
                    return value;
                }
                else if (Proxies.TryGetValue(userType, out var proxy) && proxy.ProxyType == proxyType)
                {
                    var instance = Activator.CreateInstance(userType);

                    //properties of the proxy
                    foreach (var prop in proxyType.GetProperties())
                    {
                        if (prop.Name == nameof(TypeFactory.Null) || prop.Name == nameof(TypeFactory.IsNull))
                            continue;

                        //if its the same type don't do anything else
                        var userProp = userType.GetProperty(prop.Name, prop.PropertyType);
                        if (userProp != null)
                        {
                            userProp.SetValue(instance, prop.GetValue(value));
                        }
                        else if (IsValidProxyType(prop.PropertyType))
                        {
                            var userPropType = GetUserTypeFromProxyType(prop.PropertyType);
                            userProp = userType.GetProperty(prop.Name, userPropType) ??
                                userType.GetCollectionPropertyByUnderlyingType(prop.Name, userPropType);
                            if (userProp != null)
                            {
                                userProp.SetValue(instance, ConvertFromProxy(prop.GetValue(value), prop.PropertyType,
                                     userProp.PropertyType));
                            }
                            else
                            {
                                throw new Exception($"Error trying to convert the type {prop.PropertyType.Name} to {userPropType.Name}"
                                    + $", for the property {prop.Name} in the type {userType.Name}");
                            }
                        }
                        else
                        {
                            throw new Exception($"Error trying to convert the type {prop.PropertyType.Name}"
                                    + $", for the property {prop.Name} in the type {userType.Name}");
                        }
                    }

                    return instance;
                }
                else if (
                    proxyType.IsCollection() &&
                    userType.IsCollection() &&
                    CollectionProxies.TryGetValue(userType.GetCollectionUnderType(), out var proxyCollection) &&
                    proxyCollection.ProxyCollectionType == proxyType
                )
                {

                    var proxyUnderType = proxyType.GetCollectionUnderType();
                    var userUnderType = userType.GetCollectionUnderType();

                    //if the type is the same, then skip the convertion below
                    if (proxyUnderType == userUnderType)
                    {
                        if (userType.IsArray)
                            return Enumerable.ToArray(value);
                        else
                            return Enumerable.AsEnumerable(Enumerable.ToList(value));
                    }
                    else
                    {
                        var listType = typeof(List<>).MakeGenericType(userUnderType);
                        dynamic list = Activator.CreateInstance(listType);

                        foreach (var v in value as IEnumerable)
                        {
                            list.Add(ConvertFromProxy(v, proxyUnderType, userUnderType));
                        }

                        if (userType.IsArray)
                        {
                            return list.ToArray();
                        }
                        else
                        {
                            return Enumerable.AsEnumerable(list);
                        }
                    }
                }
            }

            return value;
        }

        /// <summary>
        /// Gets or Creates if it doesn't exists a proxy list class for the <paramref name="underlyingUserType"/>
        /// </summary>
        /// <param name="underlyingUserType">The underlying <see cref="Type"/> for the generated proxy</param>
        /// <param name="overrideUdtCollectioName">Only used on <see cref="Proxy"/> to specify the name of the linked
        /// UDTName instead of reading it from <see cref="UDTCollectionNameAttribute"/></param>
        /// <returns>
        /// The generated proxy list <see cref="Type"/>
        /// <para>
        /// <list type="bullet">
        /// <item><term>
        /// If the <paramref name="underlyingUserType"/> doesn't have the <see cref="UDTCollectionNameAttribute"/> and the
        /// <paramref name="overrideUdtCollectioName"/> is not especified returns null
        /// </term></item>
        /// <item><term>
        /// If there is already a proxy created with the same <paramref name="underlyingUserType"/> and 
        /// <paramref name="overrideUdtCollectioName"/>, returns the created proxy
        /// </term></item>
        /// </list>
        /// </para>
        /// </returns>
        /// <exception cref="ArgumentException">
        /// If the UDT extracted either through the <see cref="UDTCollectionNameAttribute"/> or the 
        /// <paramref name="overrideUdtCollectioName"/> is already register with another <paramref name="underlyingUserType"/>
        /// </exception>
        /// <exception cref="Exception">
        /// If it can't get or create a proxy for the <paramref name="underlyingUserType"/>
        /// </exception>
        /// <seealso cref="GetOrCreateProxyType(Type, string)"/>
        /// <remarks>The generated proxy is a descendant of the <see cref="CollectionModel{T}"/> class.</remarks>
        internal static Type GetOrCreateProxyCollectionType(Type underlyingUserType, string overrideUdtCollectioName = null)
        {
            var udtCollectionName = overrideUdtCollectioName ?? GetUdtCollectionNameFromAtribute(underlyingUserType);

            //Checks to see if the type already exists, and if the udtname is specified then that it is for that one.
            var exists = CollectionProxies
                .Where(c => c.Key == underlyingUserType &&
                 (string.IsNullOrWhiteSpace(udtCollectionName) || c.Value.UdtCollectionName == udtCollectionName))
                .Select(c => c.Value.ProxyCollectionType);

            if (exists.Count() == 1)
            {
                return exists.First();
            }

            if (string.IsNullOrWhiteSpace(udtCollectionName))
            {
                return null;
            }

            if (!UDTLists.Add(udtCollectionName))
            {
                throw new ArgumentException(nameof(udtCollectionName), $"The UDT collection key '{udtCollectionName}' is registered.");
            }

            var underlyingProxyType = GetOrCreateProxyType(underlyingUserType);

            if (underlyingProxyType == null)
            {
                throw new Exception("A collection type must have an object type or a CLR type underneath.");
            }

            var generic = typeof(CollectionModel<>).MakeGenericType(new Type[] { underlyingProxyType });

            var proxyTypeDefinition = dynamicModule.DefineType(underlyingUserType.Name + "ListProxy", TypeAttributes.Public, generic);

            var attrCtorInfo = typeof(OracleCustomTypeMappingAttribute).GetConstructor(new Type[] { typeof(string) });
            var attrBuilder = new CustomAttributeBuilder(attrCtorInfo, new object[] { udtCollectionName });

            proxyTypeDefinition.SetCustomAttribute(attrBuilder);

            ConstructorInfo genericConstructor = null;
            if (generic is TypeBuilder)
                genericConstructor = TypeBuilder.GetConstructor(generic, typeof(CollectionModel<>).GetConstructor(Type.EmptyTypes));
            else
                genericConstructor = generic.GetConstructor(Type.EmptyTypes);

            var setMethod = typeof(CollectionModel<>).GetProperty(nameof(TypeFactory.IsNull)).GetSetMethod();

            AddNullProperty(proxyTypeDefinition, AddConstructor(proxyTypeDefinition, genericConstructor), setMethod);
            proxyTypeDefinition.CreateType();

            var arrayProxy = typeof(IEnumerable<>).MakeGenericType(new Type[] { underlyingProxyType });

            CollectionProxies.GetOrAdd(underlyingUserType, (arrayProxy, udtCollectionName));

            return arrayProxy;
        }

        /// <summary>
        /// Gets or creates a proxy type of <paramref name="userType"/> that implements all the Oracle UDT requirements
        /// for communicating with the database.
        /// </summary>
        /// <param name="userType">A base type to generate a new proxy over</param>
        /// <param name="overrideUdtName">An Oracle UDT Name to use instead of the one extracted from the 
        /// <see cref="UDTNameAttribute"/>
        /// </param>
        /// <returns>
        /// The created proxy <see cref="Type"/>
        /// <para>
        /// <list type="bullet">
        /// <item><term>
        /// If there is already a proxy for the <paramref name="userType"/> returns it
        /// </term></item>
        /// If the <paramref name="userType"/> is null returns null.
        /// </list>
        /// </para>
        /// </returns>
        /// <exception cref="ArgumentNullException">If it can't find the UDT Name either through the 
        /// <see cref="UDTNameAttribute"/> or the <paramref name="overrideUdtName"/>
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If the UDT is already registered
        /// </exception>
        /// <exception cref="ArgumentException">
        /// If one of the properties is a collection of a type that was not previously defined
        /// </exception>
        /// <remarks>
        /// <para>The generated proxy has the <see cref="OracleCustomTypeMappingAttribute"/> set to the name of the UDT.</para>
        /// <para>The generated proxy is a descendant of the <see cref="TypeModel"/> class.</para>
        /// </remarks>
        /// <seealso cref="GetOrCreateProxyCollectionType(Type, string)"/>
        internal static Type GetOrCreateProxyType(Type userType, string overrideUdtName = null, 
            Dictionary<string, string> replacedUdtPropertiesName = null)
        {
            if (userType == null)
                return null;
            else if (userType.IsValueType || userType == typeof(string))
                return userType;
            else if (Proxies.TryGetValue(userType, out var _exists))
                return _exists.ProxyType;
            else if (ProxiesBeingWorked.TryGetValue(userType, out var workingProxyType))
                return workingProxyType;

            replacedUdtPropertiesName = replacedUdtPropertiesName ?? new Dictionary<string, string>();
            var udtName = overrideUdtName ?? GetUdtNameFromAttribute(userType);

            if (string.IsNullOrWhiteSpace(udtName))
            {
                throw new ArgumentNullException(nameof(udtName), "In order to create a proxy for a class you must use the UDTName attribute" +
                    " or pass it as a parameter");
            }

            if (!UDTLists.Add(udtName))
            {
                throw new ArgumentException(nameof(udtName), $"The UDT key '{udtName}' is already registered.");
            }

            var proxyTypeDefinition = dynamicModule.DefineType(userType.Name + "Proxy", TypeAttributes.Public, typeof(TypeModel));

            ProxiesBeingWorked.GetOrAdd(userType, proxyTypeDefinition);

            var attrCtorInfo = typeof(OracleCustomTypeMappingAttribute).GetConstructor(new Type[] { typeof(string) });
            var attrBuilder = new CustomAttributeBuilder(attrCtorInfo, new object[] { udtName });

            proxyTypeDefinition.SetCustomAttribute(attrBuilder);

            var propAttrCtorInfo = typeof(OracleObjectMappingAttribute).GetConstructor(new Type[] { typeof(string) });

            var overridenProperties = userType.GetProperties(BindingFlags.Instance | BindingFlags.Public |
                                                               BindingFlags.Static | BindingFlags.DeclaredOnly);

            foreach (var prop in userType.GetProperties())
            {
                //Skips the property if it is the Null property, if its been overriden by another one or is not serializable.
                if (prop.Name == nameof(TypeFactory.Null) || overridenProperties.Any(c => c.Name == prop.Name && c != prop)
                    || !IsPropertySerializable(prop))
                {
                    continue;
                }

                replacedUdtPropertiesName.TryGetValue(prop.Name, out var udt);
                udt = udt ?? GetUdtPropertyNameFromAttribute(prop);
                var propType = prop.PropertyType;

                if (propType.IsValueType || prop.PropertyType == typeof(string))
                {
                    AddProperty(proxyTypeDefinition, prop.Name, udt, propType, propAttrCtorInfo);
                }
                else if (propType.IsCollection())
                {
                    var collectionType = GetOrCreateProxyCollectionType(propType.GetCollectionUnderType());
                    if (collectionType == null)
                    {
                        throw new ArgumentException($"The collection property {prop.Name}:{propType.Name} for the type {userType.Name}, must "
                            + $"have the {nameof(UDTCollectionNameAttribute)} set in the {propType.GetCollectionUnderType()} class with the "
                            + "Oracle UDT collection name.");
                    }
                    AddProperty(proxyTypeDefinition, prop.Name, udt, collectionType, propAttrCtorInfo);
                }
                else if (propType.IsClass)
                {
                    AddProperty(proxyTypeDefinition, prop.Name, udt, GetOrCreateProxyType(propType), propAttrCtorInfo);
                }
            }

            AddNullProperty(proxyTypeDefinition, AddConstructor(proxyTypeDefinition));
            var proxyType = proxyTypeDefinition.CreateType();

            Proxies.GetOrAdd(userType, (proxyType, udtName));
            ProxiesBeingWorked.TryRemove(userType, out _);

            return proxyType;
        }
    }
}
