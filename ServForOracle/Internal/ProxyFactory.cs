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
    internal static class ProxyFactory
    {
        internal const string NAME = "servForOracleProxies";
        internal const string NULL_PROP = nameof(TypeFactory.Null);

        private static AssemblyName ProxiesAssemblyName = new AssemblyName(NAME);
        private static AssemblyBuilder dynamicAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(ProxiesAssemblyName, AssemblyBuilderAccess.Run);
        private static ModuleBuilder dynamicModule = dynamicAssembly.DefineDynamicModule(ProxiesAssemblyName.Name);

        /// <summary>
        /// Read-Only dictionary with all the object types for the OracleDB.
        /// The Key is the Type with the UDTNameAttribute.
        /// The Value is a tuple, with the generated proxy type and the Oracle UDT description.
        /// </summary>
        /// <example>[{ClientClass, "HR.CLIENT_OBJ"}, {SalesClass, "HR.SALES_OBJ"}]</example>
        public static Dictionary<Type, (Type ProxyType, string UdtName)> Proxies { get; private set; }
        /// <summary>
        /// Read-Only dictionary with all the collections types for the OracleDB.
        /// The Key is an IEnumerable of the generated proxy type.
        /// The Value is the Oracle UDT Collection Name.
        /// </summary>
        /// <example>[{IEnumerable<stringProxy>', "HR.STRING_LIST"}, {IEnumerable<intProxy>, "HR.NUMBER_LIST"}]</example>
        public static Dictionary<Type, string> CollectionProxies { get; private set; }

        static ProxyFactory()
        {
            Proxies = new Dictionary<Type, (Type proxyType, string udtName)>();
            CollectionProxies = new Dictionary<Type, string>();

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

        internal static Assembly Assembly => AppDomain.CurrentDomain.GetAssemblies()
                                                                    .Where(c => c.GetName().Name == NAME)
                                                                    .FirstOrDefault();

        private static string GetUdtName(Type type)
        {
            if (type == null)
                return null;

            return type.GetCustomAttribute<UDTNameAttribute>()?.Name;
        }

        private static string GetUdtCollectionName(Type type)
        {
            if (type == null)
                return null;

            return type.GetCustomAttribute<UDTCollectionNameAttribute>()?.Name;
        }

        private static string GetUdtNameFromAttribute(PropertyInfo property)
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

        //TODO handle list with clr types
        private static Type GetOrCreateProxyCollectionType(Type underlyingUserType)
        {
            if (CollectionProxies.ContainsKey(underlyingUserType))
            {
                return underlyingUserType;
            }

            var udtCollectionName = GetUdtCollectionName(underlyingUserType);

            if (string.IsNullOrWhiteSpace(udtCollectionName))
            {
                return null;
            }
            var underlyingProxyType = GetOrCreateProxyType(underlyingUserType);

            if (underlyingProxyType == null)
            {
                throw new Exception("A list type must have an object type as well");
            }

            var generic = typeof(CollectionModel<>).MakeGenericType(new Type[] { underlyingProxyType });
            var proxyTypeDefinition = dynamicModule.DefineType(underlyingUserType.Name + "ListProxy", TypeAttributes.Public, generic);

            AddNullProperty(proxyTypeDefinition, AddConstructor(proxyTypeDefinition));
            proxyTypeDefinition.CreateType();

            var arrayProxy = typeof(IEnumerable<>).MakeGenericType(new Type[] { underlyingProxyType });

            CollectionProxies.Add(arrayProxy, udtCollectionName);

            return arrayProxy;
        }

        private static Type GetOrCreateProxyType(Type userType)
        {
            if (userType == null)
                return null;
            else if (userType.IsValueType || userType == typeof(string))
                return userType;
            else if (Proxies.TryGetValue(userType, out var _exists))
                return _exists.ProxyType;

            var udtName = GetUdtName(userType);
            
            if (string.IsNullOrWhiteSpace(udtName))
            {
                throw new ArgumentNullException(nameof(udtName), "In order to create a proxy for a class you must use the UDTName attribute" +
                    " or pass it as a parameter");
            }

            var proxyTypeDefinition = dynamicModule.DefineType(userType.Name + "Proxy", TypeAttributes.Public, typeof(TypeModel));

            Proxies.Add(userType, (proxyTypeDefinition, udtName));

            var attrCtorInfo = typeof(OracleCustomTypeMappingAttribute).GetConstructor(new Type[] { typeof(string) });
            var attrBuilder = new CustomAttributeBuilder(attrCtorInfo, new object[] { udtName });

            proxyTypeDefinition.SetCustomAttribute(attrBuilder);

            var propAttrCtorInfo = typeof(OracleObjectMappingAttribute).GetConstructor(new Type[] { typeof(string) });

            foreach (var prop in userType.GetProperties())
            {
                if (prop.Name == NULL_PROP)
                {
                    continue;
                }

                var udt = GetUdtNameFromAttribute(prop);
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

        private static void AddNullProperty(TypeBuilder proxyTypeDefinition, ConstructorBuilder constructor)
        {
            var newProp = proxyTypeDefinition.DefineProperty(NULL_PROP, PropertyAttributes.None, proxyTypeDefinition, Type.EmptyTypes);

            var fieldSetMethod = proxyTypeDefinition.BaseType.GetProperty(nameof(TypeFactory.IsNull)).GetSetMethod();
            var methodAttributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
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

        private static void AddProperty(TypeBuilder proxyTypeDefinition, string name, string udtName, Type propertyType, ConstructorInfo propAttrCtorInfo)
        {
            var newProp = proxyTypeDefinition.DefineProperty(name, PropertyAttributes.None, propertyType, Type.EmptyTypes);
            newProp.SetCustomAttribute(new CustomAttributeBuilder(propAttrCtorInfo, new object[] { udtName }));

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
