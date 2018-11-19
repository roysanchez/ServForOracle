using ConcurrentCollections;
using Oracle.DataAccess.Types;
using ServForOracle.Models;
using ServForOracle.Tools;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Internal
{
    /// <summary>
    /// The class that handles all the creating and management of the proxies generated to map to the Oracle UDT standard set
    /// by the ODP.NET Native library.
    /// </summary>
    internal static partial class ProxyFactory
    {
        internal const string NAME = "servForOracleProxies";

        private static AssemblyName ProxiesAssemblyName = new AssemblyName(NAME);
        private static AssemblyBuilder dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(ProxiesAssemblyName, AssemblyBuilderAccess.Run);
        private static ModuleBuilder dynamicModule = dynamicAssembly.DefineDynamicModule(ProxiesAssemblyName.Name);

        /// <summary>
        /// Proxies objects that are being worked on
        /// </summary>
        private static ConcurrentDictionary<Type, Type> ProxiesBeingWorked;
        
        /// <summary>
        /// Looks for all the assemblies in the current AppDomain that aren't either Microsofts, Oracles or System.
        /// In those assemblies then selects all the instantiable classes that have the
        /// <see cref="ServForOracle.UDTCollectionNameAttribute"/> and/or <see cref="ServForOracle.UDTNameAttribute"/>
        /// </summary>
        static ProxyFactory()
        {
            Proxies = new ConcurrentDictionary<Type, (Type ProxyType, string UdtName)>();
            ProxiesBeingWorked = new ConcurrentDictionary<Type, Type>();
            CollectionProxies = new ConcurrentDictionary<Type, (Type ProxyCollectionType, string UdtCollectionName)>();
            CollectionTypes = new ConcurrentDictionary<Type, Type>();

            UDTLists = new ConcurrentHashSet<string>();

            var executing = Assembly.GetExecutingAssembly();

            var assemblies =
                    from assembly in AppDomain.CurrentDomain.GetAssemblies()
                    where assembly != executing
                       && !assembly.GlobalAssemblyCache
                       //&& assembly.Location == executing.Location
                       && !assembly.FullName.StartsWith("Microsoft")
                       && !assembly.FullName.StartsWith("System")
                       && !assembly.FullName.StartsWith("Oracle")
                       && !assembly.FullName.StartsWith("xunit")
                    select assembly;

            var types = assemblies.SelectMany(a => a.GetTypes())
                        .Where(t => t.IsClass && !t.IsSealed && !t.IsAbstract);

            foreach (var type in types.Where(t => t.GetCustomAttribute<UDTCollectionNameAttribute>() != null))
            {
                GetOrCreateProxyCollectionType(type);
            }

            foreach (var type in types.Where(t => t.GetCustomAttribute<UDTNameAttribute>() != null))
            {
                GetOrCreateProxyType(type);
            }
        }

        /// <summary>
        /// Looks for the property of the <paramref name="userType"/> that match the <paramref name="proxyPropertyType"/>
        /// </summary>
        /// <param name="userType">The <see cref="Type"/> that declares the property</param>
        /// <param name="proxyPropertyType">The <see cref="Type"/> of the property to look for</param>
        /// <param name="propertyName">The name of the property in the <paramref name="userType"/></param>
        /// <returns>The <see cref="PropertyInfo"/> for the property if exists otherwise null</returns>
        private static PropertyInfo GetUserPropertyFromProxyPropertyType(Type userType, Type proxyPropertyType,
            string propertyName)
        {
            if (proxyPropertyType.IsCollection())
            {
                return CollectionProxies
                    .Where(c => c.Value.ProxyCollectionType == proxyPropertyType)
                    .Select(c => userType.GetProperty(propertyName, c.Key.MakeArrayType()))
                    .FirstOrDefault(prop => prop != null);

            }
            else
            {
                return Proxies
                    .Where(c => c.Value.ProxyType == proxyPropertyType)
                    .Select(c => userType.GetProperty(propertyName, c.Key))
                    .FirstOrDefault(prop => prop != null);
            }
        }

        /// <summary>
        /// Looks for the proxy type from a user type
        /// </summary>
        /// <param name="userType">The user type to check for</param>
        /// <returns>The proxy type for the <paramref name="userType"/> if it exists</returns>
        /// <remarks></remarks>
        private static Type GetProxyTypeFromUserType(Type userType)
        {
            if (userType.IsCollection())
            {
                CollectionProxies.TryGetValue(userType.GetCollectionUnderType(), out var collection);
                return collection.ProxyCollectionType;
            }
            else
            {
                Proxies.TryGetValue(userType, out var proxy);
                return proxy.ProxyType;
            }
        }

        /// <summary>
        /// Gets the first collection proxy type registered to the <paramref name="udtCollectionName"/>
        /// </summary>
        /// <param name="udtCollectionName">The udt collection name to look for</param>
        /// <returns>The first collection registered with that name or the default value otherwise</returns>
        private static (Type ProxyType, string UDTCollectionName) GetCollectionProxyTypeFromUdtName(string udtCollectionName)
        {
            return CollectionProxies.Where(c => c.Value.UdtCollectionName == udtCollectionName)
                                        .Select(c => c.Value)
                                        .FirstOrDefault();
        }

        /// <summary>
        /// Gets the first proxy type registered to the <paramref name="udtName"/>
        /// </summary>
        /// <param name="udtName">The udt name to look for</param>
        /// <returns>The first type regsitered with that name or the default value otherwise</returns>
        private static (Type ProxyType, string UDTName) GetProxyTypeFromUdtName(string udtName)
        {
            return Proxies.Where(c => c.Value.UdtName == udtName)
                    .Select(c => c.Value)
                    .FirstOrDefault();
        }

        /// <summary>
        /// Gets the UDT Name from the <see cref="UDTNameAttribute"/>
        /// </summary>
        /// <param name="type">The <see cref="Type"/> with the <see cref="UDTNameAttribute>"/></param>
        /// <returns>the name on the <see cref="UDTNameAttribute"/></returns>
        /// <remarks>If the <paramref name="type"/> doesn't have the <see cref="UDTNameAttribute"/> returns null</remarks>
        /// <seealso cref="GetUdtCollectionNameFromAtribute(Type)"/>
        /// <see cref="GetUdtPropertyNameFromAttribute(PropertyInfo)"/>
        private static string GetUdtNameFromAttribute(Type type)
        {
            if (type == null)
                return null;

            return type.GetCustomAttribute<UDTNameAttribute>()?.Name;
        }

        /// <summary>
        /// Gets the UDT Collection Name from the <see cref="UDTCollectionNameAttribute"/>
        /// </summary>
        /// <param name="type">The <see cref="Type"/> with the <see cref="UDTCollectionNameAttribute"/></param>
        /// <returns>The name on the <see cref="UDTCollectionNameAttribute"/></returns>
        /// <remarks>If the <paramref name="type"/> doesn't have the <see cref="UDTCollectionNameAttribute"/> returns null</remarks>
        /// <seealso cref="GetUdtNameFromAttribute(Type)"/>
        /// <see cref="GetUdtPropertyNameFromAttribute(PropertyInfo)"/>
        private static string GetUdtCollectionNameFromAtribute(Type type)
        {
            if (type == null)
                return null;

            return type.GetCustomAttribute<UDTCollectionNameAttribute>()?.Name;
        }

        /// <summary>
        /// Gets the UDT Name from the <see cref="UDTPropertyAttribute"/>
        /// </summary>
        /// <param name="property">The property with the <see cref="UDTPropertyAttribute"/></param>
        /// <returns>The name on the <see cref="UDTPropertyAttribute"/> or the name of the property</returns>
        /// <seealso cref="GetUdtNameFromAttribute(Type)"/>
        /// <seealso cref="GetUdtCollectionNameFromAtribute(Type)"/>
        private static string GetUdtPropertyNameFromAttribute(PropertyInfo property)
        {
            if (property == null)
                return null;

            var attr = property.GetCustomAttribute<UDTPropertyAttribute>();
            if (attr != null)
            {
                return attr.Name;
            }
            else
            {
                return property.Name.ToUpper();
            }
        }

        /// <summary>
        /// Creates a constructor based on the parent class of the type that is expected to be either
        /// <see cref="CollectionModel{T}"/> or <see cref="TypeModel"/>
        /// </summary>
        /// <param name="proxyTypeDefinition">The type definition for the proxy that is going to be generated.</param>
        /// <param name="constructor">The constructor to use as a subcall</param>
        /// <returns>The default constructor for the new type.</returns>
        private static ConstructorBuilder AddConstructor(TypeBuilder proxyTypeDefinition, ConstructorInfo constructor = null)
        {
            var objectCtor = constructor ?? proxyTypeDefinition.BaseType.GetConstructors()[0];

            var ctor = proxyTypeDefinition.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard | CallingConventions.HasThis, Type.EmptyTypes);
            var gen = ctor.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, objectCtor);
            gen.Emit(OpCodes.Ret);

            return ctor;
        }

        /// <summary>
        /// Adds a Null static property to the type definition for the new proxy, it is a requirement for the
        /// connection of Oracles UDTs with .NET POCOs
        /// </summary>
        /// <param name="proxyTypeDefinition">The type definition for the proxy that is going to be generated.</param>
        /// <param name="constructor">The constructor to call in the Null Property</param>
        /// <param name="setMethod">The set method for the IsNull property</param>
        /// <remarks>
        /// The Null property creates a new instance of the proxy type with the IsNull property set to true
        /// </remarks>
        private static void AddNullProperty(TypeBuilder proxyTypeDefinition, ConstructorBuilder constructor, MethodInfo setMethod = null)
        {
            var newProp = proxyTypeDefinition.DefineProperty(nameof(TypeFactory.Null), PropertyAttributes.None, proxyTypeDefinition, Type.EmptyTypes);

            var fieldSetMethod = setMethod ?? proxyTypeDefinition.BaseType.GetProperty(nameof(TypeFactory.IsNull)).GetSetMethod();
            var methodAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.Static | MethodAttributes.HideBySig;
            var getMethodBuilder = proxyTypeDefinition.DefineMethod("get_Null", methodAttributes, proxyTypeDefinition, Type.EmptyTypes);

            var ilGenerator = getMethodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Nop);
            ilGenerator.Emit(OpCodes.Newobj, constructor);
            ilGenerator.Emit(OpCodes.Dup);
            ilGenerator.Emit(OpCodes.Ldc_I4_1);
            ilGenerator.Emit(OpCodes.Callvirt, fieldSetMethod);
            ilGenerator.Emit(OpCodes.Ret);

            newProp.SetGetMethod(getMethodBuilder);
        }

        /// <summary>
        /// Adds a new property to the proxy type definition, with the <see cref="OracleObjectMappingAttribute"/> set
        /// to the <paramref name="udtPropName"/> specified
        /// </summary>
        /// <param name="proxyTypeDefinition">The proxy type definition to add the property to</param>
        /// <param name="name">The name of the new property</param>
        /// <param name="udtPropName">The Oracle UDT property name.</param>
        /// <param name="propertyType">The type returned by the property</param>
        /// <param name="propAttrCtorInfo">The <see cref="OracleObjectMappingAttribute"/> constructor</param>
        private static void AddProperty(TypeBuilder proxyTypeDefinition, string name, string udtPropName, Type propertyType, ConstructorInfo propAttrCtorInfo)
        {
            var newProp = proxyTypeDefinition.DefineProperty(name, PropertyAttributes.None, propertyType, Type.EmptyTypes);
            newProp.SetCustomAttribute(new CustomAttributeBuilder(propAttrCtorInfo, new object[] { udtPropName }));

            var fieldBuilder = proxyTypeDefinition.DefineField("_" + name, propertyType, FieldAttributes.Private);
            var methodAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            var getMethodBuilder = proxyTypeDefinition.DefineMethod("get_" + name, methodAttributes, propertyType, Type.EmptyTypes);
            var ilGenerator = getMethodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldfld, fieldBuilder);
            ilGenerator.Emit(OpCodes.Ret);


            var setMethodBuilder = proxyTypeDefinition.DefineMethod("set_" + name, methodAttributes, null, new Type[] { propertyType });
            ilGenerator = setMethodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Stfld, fieldBuilder);
            ilGenerator.Emit(OpCodes.Ret);

            newProp.SetGetMethod(getMethodBuilder);
            newProp.SetSetMethod(setMethodBuilder);
        }

        /// <summary>
        /// Checks if the property is serializable, meaning it doesn't have any kind of attribute specifying otherwise
        /// </summary>
        /// <param name="property">The property to check</param>
        /// <returns>True if it is serializable otherwise false</returns>
        /// <remarks>It checks if the field exists and </remarks>
        private static bool IsPropertySerializable(PropertyInfo property)
        {
            if (property == null)
                return false;

            if(MemberInfoHasInvalidAttribute(property))
            {
                return false;
            }
            
            var typeFields = property.DeclaringType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            var field = typeFields.FirstOrDefault(f => f.Name.Equals($"<{property.Name}>k__BackingField", StringComparison.InvariantCultureIgnoreCase)
                                                  || f.Name.Equals($"{property.Name}Field", StringComparison.InvariantCultureIgnoreCase));

            if (field != null)
            {
                return !MemberInfoHasInvalidAttribute(field);
            }
            
            return false;
        }

        /// <summary>
        /// Check if the MemberInfo has any of the following attributes: <see cref="NonSerializedAttribute"/>,
        /// <see cref="System.Runtime.Serialization.IgnoreDataMemberAttribute"/> or 
        /// <see cref="System.Xml.Serialization.XmlIgnoreAttribute"/>
        /// </summary>
        /// <param name="member">The field or property to check</param>
        /// <returns>True if any of the attributes exists false otherwise</returns>
        private static bool MemberInfoHasInvalidAttribute(MemberInfo member)
        {
            if (member.GetCustomAttribute<NonSerializedAttribute>() != null ||
                    member.GetCustomAttribute<System.Runtime.Serialization.IgnoreDataMemberAttribute>() != null ||
                    member.GetCustomAttribute<System.Xml.Serialization.XmlIgnoreAttribute>() != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
