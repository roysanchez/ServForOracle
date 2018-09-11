using Oracle.DataAccess.Types;
using ServForOracle.Models;
using ServForOracle.Tools;
using System;
using System.Collections;
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
    internal static class ProxyFactory
    {
        internal const string NAME = "servForOracleProxies";

        private static AssemblyName ProxiesAssemblyName = new AssemblyName(NAME);
        private static AssemblyBuilder dynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(ProxiesAssemblyName, AssemblyBuilderAccess.Run);
        private static ModuleBuilder dynamicModule = dynamicAssembly.DefineDynamicModule(ProxiesAssemblyName.Name);

        /// <summary>
        /// Read-Only dictionary with all the object types for the OracleDB.
        /// The Key is the Type with the UDTNameAttribute.
        /// The Value is a tuple, with the generated proxy type and the Oracle UDT description.
        /// </summary>
        /// <value>[{<see cref="Type"/> BaseType, (<see cref="Type"/> ProxyType, "HR.CLIENT_OBJ")}]</value>
        public static Dictionary<Type, (Type ProxyType, string UdtName)> Proxies { get; private set; }
        /// <summary>
        /// Read-Only dictionary with all the collections types for the OracleDB.
        /// The Key is an Array of the generated proxy type.
        /// The Value is the Orascle UDT Collection Name.
        /// </summary>
        /// <value>[{<see cref="Type[]"/>, "HR.STRING_LIST"}, {<see cref="Type[]"/>, "HR.NUMBER_LIST"}]</value>
        public static Dictionary<Type, (Type ProxyCollectionType, string UdtCollectionName)> CollectionProxies { get; private set; }
        /// <summary>
        /// Used to check if the UDT type is registered
        /// </summary>
        private static HashSet<string> UDTLists;

        /// <summary>
        /// Looks for all the assemblies in the current AppDomain that aren't either Microsofts, Oracles or System.
        /// In those assemblies then selects all the instantiable classes that have the
        /// <see cref="ServForOracle.UDTCollectionNameAttribute"/> and/or <see cref="ServForOracle.UDTNameAttribute"/>
        /// </summary>
        static ProxyFactory()
        {
            Proxies = new Dictionary<Type, (Type proxyType, string udtName)>();
            CollectionProxies = new Dictionary<Type, (Type ProxyCollectionType, string UdtCollectionName)>();
            UDTLists = new HashSet<string>();
            
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
        /// The Generated <see cref="Assembly"/> assembly
        /// </summary>
        internal static Assembly Assembly => AppDomain.CurrentDomain.GetAssemblies()
                                                                    .Where(c => c.GetName().Name == NAME)
                                                                    .FirstOrDefault();

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

            if (string.IsNullOrWhiteSpace(udtCollectionName))
            {
                return null;
            }

            //Checks to see if the type/udtname combination exists.
            var exists = CollectionProxies
                .Where(c => c.Key == underlyingUserType && c.Value.UdtCollectionName == udtCollectionName)
                .Select(c => c.Value.ProxyCollectionType)
                .FirstOrDefault();
            
            if (exists != null)
            {
                return exists;
            }

            if(!UDTLists.Add(udtCollectionName))
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

            AddNullProperty(proxyTypeDefinition, AddConstructor(proxyTypeDefinition));
            proxyTypeDefinition.CreateType();

            var arrayProxy = underlyingProxyType.MakeArrayType();

            CollectionProxies.Add(underlyingUserType, (arrayProxy, udtCollectionName));

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
        internal static Type GetOrCreateProxyType(Type userType, string overrideUdtName = null)
        {
            if (userType == null)
                return null;
            else if (userType.IsValueType || userType == typeof(string))
                return userType;
            else if (Proxies.TryGetValue(userType, out var _exists))
                return _exists.ProxyType;

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

            Proxies.Add(userType, (proxyTypeDefinition, udtName));

            var attrCtorInfo = typeof(OracleCustomTypeMappingAttribute).GetConstructor(new Type[] { typeof(string) });
            var attrBuilder = new CustomAttributeBuilder(attrCtorInfo, new object[] { udtName });

            proxyTypeDefinition.SetCustomAttribute(attrBuilder);

            var propAttrCtorInfo = typeof(OracleObjectMappingAttribute).GetConstructor(new Type[] { typeof(string) });

            var overridenProperties = userType.GetProperties(BindingFlags.Instance | BindingFlags.Public | 
                                                               BindingFlags.Static | BindingFlags.DeclaredOnly);
            
            foreach (var prop in userType.GetProperties())
            {
                //Skips the property if it is the Null property or if its been overriden by another one.
                if (prop.Name == nameof(TypeFactory.Null) || overridenProperties.Any(c => c.Name == prop.Name && c != prop))
                {
                    continue;
                }

                var udt = GetUdtPropertyNameFromAttribute(prop);
                var propType = prop.PropertyType;

                if (propType.IsValueType || prop.PropertyType == typeof(string))
                {
                    AddProperty(proxyTypeDefinition, prop.Name, udt, propType, propAttrCtorInfo);
                }
                else if (propType.IsCollection())
                {
                    var collectionType = GetOrCreateProxyCollectionType(propType.GetCollectionUnderType());
                    if(collectionType == null)
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

            return proxyType;
        }

        /// <summary>
        /// Creates a constructor based on the parent class of the type that is expected to be either
        /// <see cref="CollectionModel{T}"/> or <see cref="TypeModel"/>
        /// </summary>
        /// <param name="proxyTypeDefinition">The type definition for the proxy that is going to be generated.</param>
        /// <returns>The default constructor for the new type.</returns>
        private static ConstructorBuilder AddConstructor(TypeBuilder proxyTypeDefinition)
        {
            var baseTypeCtor = proxyTypeDefinition.BaseType.GetConstructors()[0];

            var ctor = proxyTypeDefinition.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard | CallingConventions.HasThis, Type.EmptyTypes);
            var gen = ctor.GetILGenerator();
            gen.Emit(OpCodes.Ldarg_0);
            gen.Emit(OpCodes.Call, baseTypeCtor);
            gen.Emit(OpCodes.Ret);

            return ctor;
        }

        /// <summary>
        /// Adds a Null static property to the type definition for the new proxy, it is a requirement for the
        /// connection of Oracles UDTs with .NET POCOs
        /// </summary>
        /// <param name="proxyTypeDefinition">The type definition for the proxy that is going to be generated.</param>
        /// <param name="constructor">The constructor to call in the Null Property</param>
        /// <remarks>
        /// The Null property creates a new instance of the proxy type with the IsNull property set to true
        /// </remarks>
        private static void AddNullProperty(TypeBuilder proxyTypeDefinition, ConstructorBuilder constructor)
        {
            var newProp = proxyTypeDefinition.DefineProperty(nameof(TypeFactory.Null), PropertyAttributes.None, proxyTypeDefinition, Type.EmptyTypes);

            var fieldSetMethod = proxyTypeDefinition.BaseType.GetProperty(nameof(TypeFactory.IsNull)).GetSetMethod();
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


            var setMethodBuilder = proxyTypeDefinition.DefineMethod("set_" + name, methodAttributes, null, new Type[] { typeof(string) });
            ilGenerator = setMethodBuilder.GetILGenerator();
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldarg_1);
            ilGenerator.Emit(OpCodes.Stfld, fieldBuilder);
            ilGenerator.Emit(OpCodes.Ret);

            newProp.SetGetMethod(getMethodBuilder);
            newProp.SetSetMethod(setMethodBuilder);
        }
    }
}
