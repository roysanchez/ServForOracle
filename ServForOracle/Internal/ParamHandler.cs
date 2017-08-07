using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using ServForOracle.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Internal
{
    internal static class ParamHandler
    {
        private const int VARCHAR_MAX_SIZE = 32000;
        public static Dictionary<Type, string> Models { get; }
        /// <summary>
        /// Read-Only dictionary with all the collections types for the OracleDB.
        /// The Key is the Type with the Oracle Array Attribute or their collectionType.
        /// The Value is the Oracle Type description.
        /// </summary>
        public static Dictionary<Type, string> Collections { get; }

        //TODO Move the message to a resource
        public static string InvalidClassMessage { get; }
            = "The type {0} doesn't conform with the guidelines for {1}. "
                + "Please see the documentation on how to use this library.";

        public static string TypeNotConfiguredMessage { get; }
            = "The type {0} is not configured for automatic casting, please open an issue on github. "
                + "In the mean time, you can use the OracleDbType Param create overload to solve it.";

        static ParamHandler()
        {
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

            Models = types.Where(t => t.IsSubclassOf(typeof(TypeModel)))
                        .ToDictionary(t => t, t => GetOracleTypeNameFromAttribute(t));


            var tempCol = types
                .Where(t => IsCollectionType(t.BaseType))
                .ToDictionary(t => t, t => GetOracleTypeNameFromAttribute(t));

            Collections = new Dictionary<Type, string>(tempCol);

            //Creates the IEnumerable<Type> for arrays
            foreach (var keyValue in tempCol)
            {
                var generic = typeof(IEnumerable<>).MakeGenericType(keyValue.Key.BaseType.GetGenericArguments()[0]);
                Collections.Add(generic, keyValue.Value);
            }
        }

        private static bool IsCollectionType(Type type)
        {
            if (type == null) return false;
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(CollectionModel<>);
        }

        private static bool TryGetCollectionKeyValue(Type t, out string value)
        {
            value = Collections
                        .Where(c => c.Key.IsAssignableFrom(t))
                        .Select(c => c.Value)
                        .FirstOrDefault();

            return !string.IsNullOrEmpty(value);
        }

        public static bool IsValidParameterType(Type type)
        {
            if (type.IsValueType
                || type == typeof(string)
                || Models.Any(c => c.Key == type)
                || TryGetCollectionKeyValue(type, out var collectionValue)
                )
                return true;

            return false;
        }

        /// <summary>
        /// Creates an oracle parameter as a return value
        /// </summary>
        /// <typeparam name="T">The type spected to be returned by the function</typeparam>
        /// <returns>An OracleParameter configured for the return of the specified type</returns>
        public static OracleParameter CreateReturnParam<T>()
        {
            return CreateOracleParam(typeof(T), null, ParameterDirection.ReturnValue);
        }

        /// <summary>
        /// Creates an OracleParameter with the specfied information
        /// </summary>
        /// <param name="parameter">The Param to transform into an OracleParameter object</param>
        /// <returns></returns>
        public static OracleParameter CreateOracleParam(Param parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            return CreateOracleParam(parameter.Type, parameter.Value, parameter.ParamDirection, parameter.OracleType);
        }

        /// <summary>
        /// Creates an OracleParameter with the specfied information
        /// </summary>
        /// <typeparam name="T">The type of the oracle parameter value</typeparam>
        /// <param name="type">The type that the value represents (in case the value is null)</param>
        /// <param name="value">The value to send</param>
        /// <param name="paramType">The direction of the parameter (IN, OUT, INOUT)</param>
        /// <param name="oracleType">In the case where the caller wants to set explicitly the type of the oracleDb to use</param>
        /// <returns>Returns an OracleParameter with the data specified.</returns>
        public static OracleParameter CreateOracleParam<T>(Type type, T value, ParamDirection paramType,
                OracleDbType? oracleType = null)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var paramDirection = ParameterDirection.Input;
            switch (paramType)
            {
                case ParamDirection.Input:
                    paramDirection = ParameterDirection.Input;
                    break;
                case ParamDirection.Output:
                    paramDirection = ParameterDirection.Output;
                    break;
                case ParamDirection.InputOutput:
                    paramDirection = ParameterDirection.InputOutput;
                    break;
            }

            return CreateOracleParam(type, value, paramDirection, oracleType);
        }

        private static OracleParameter CreateOracleParam(Type type, object value, ParameterDirection direction,
            OracleDbType? oracleType = null)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            object _value = value;

            var param = new OracleParameter();

            if (oracleType.HasValue)
            {
                param.OracleDbType = oracleType.Value;
                if (param.OracleDbType == OracleDbType.Varchar2)
                    param.Size = VARCHAR_MAX_SIZE;
            }
            else if (type.IsValueType)
            {
                if (type == typeof(char) || type == typeof(char?))
                {
                    param.OracleDbType = OracleDbType.Char;
                }
                else if (type == typeof(sbyte) || type == typeof(sbyte?))
                {
                    param.OracleDbType = OracleDbType.Byte;
                }
                else if (type == typeof(short) || type == typeof(short?)
                    || type == typeof(byte) || type == typeof(byte?))
                {
                    param.OracleDbType = OracleDbType.Int16;
                }
                else if (type == typeof(int) || type == typeof(int?))
                {
                    param.OracleDbType = OracleDbType.Int32;
                }
                else if (type == typeof(long) || type == typeof(long))
                {
                    param.OracleDbType = OracleDbType.Int64;
                }
                else if (type == typeof(Single) || type == typeof(Single?)
                    || type == typeof(float) || type == typeof(float?))
                {
                    param.OracleDbType = OracleDbType.Single;
                }
                else if (type == typeof(double) || type == typeof(double?))
                {
                    param.OracleDbType = OracleDbType.Double;
                }
                else if (type == typeof(decimal) || type == typeof(decimal?))
                {
                    param.OracleDbType = OracleDbType.Decimal;
                }
                else if (type == typeof(DateTime) || type == typeof(DateTime?))
                {
                    param.OracleDbType = OracleDbType.Date;
                }
                else
                    throw new Exception(string.Format(TypeNotConfiguredMessage, type.Name));
            }
            else if (type.IsArray)
            {
                if (type == typeof(byte[]))
                {
                    param.OracleDbType = OracleDbType.Blob;
                }
                else if (TryGetCollectionKeyValue(type, out var collectionValue))
                {
                    param.UdtTypeName = collectionValue;
                    param.OracleDbType = OracleDbType.Array;
                }
                else
                    throw new Exception(string.Format(InvalidClassMessage, type.Name, "collections"));
            }
            else if (type == typeof(string))
            {
                param.OracleDbType = OracleDbType.Varchar2;
                if (direction != ParameterDirection.Input)
                    param.Size = VARCHAR_MAX_SIZE;

                if (_value != null && _value is string str && str.Length > VARCHAR_MAX_SIZE)
                {
                    param.OracleDbType = OracleDbType.Clob;
                    param.Size = default(int);
                }
            }
            else if (Models.TryGetValue(type, out var modelValue))
            {
                param.UdtTypeName = modelValue;
                param.OracleDbType = OracleDbType.Object;
            }
            else
                throw new Exception(string.Format(InvalidClassMessage, type.Name, "objects"));

            param.Direction = direction;
            param.Value = _value;
            return param;
        }

        private static string GetOracleTypeNameFromAttribute(Type type)
        {
            //TODO throw exception when null
            //TODO never return null
            if (type == null) return null;

            var oracleAttribute = (
                    from attr in type.GetCustomAttributes(false)
                    where attr.GetType() == typeof(OracleCustomTypeMappingAttribute)
                    select attr as OracleCustomTypeMappingAttribute
                ).FirstOrDefault();

            return oracleAttribute?.UdtTypeName;
        }

        /// <summary>
        /// Simple abstraction for the common oracle interface that handles nullable types
        /// </summary>
        /// <param name="param">Oracle param object</param>
        /// <param name="isNullable">If the type of the result is nullable</param>
        /// <returns>The oracle param value</returns>
        private static object extractNullableValue(dynamic param, bool isNullable)
        {
            if (isNullable)
                return extractValue(param);
            else if (param.IsNull)
                throw new InvalidCastException("Can't cast null to a non nullable");
            else
                return param.Value;
        }

        /// <summary>
        /// Simple abstraction for the common oracle interface
        /// </summary>
        /// <param name="param">Oracle Param object</param>
        /// <returns>The oracle param value</returns>
        private static object extractValue(dynamic param)
        {
            if (!(param is Oracle.DataAccess.Types.INullable))
                throw new InvalidCastException($"Can't use {nameof(extractValue)} for type ${param.GetType().Name}");

            if (param.IsNull)
                return null;
            else
                return param.Value;
        }

        /// <summary>
        /// Converts the oracle type to the corresponding dotnet type, as specified in the following spec:
        /// <see cref="https://docs.oracle.com/database/121/ODPNT/featUDTs.htm#BABIFHGJ"/>
        /// </summary>
        /// <typeparam name="T">The type expected, if Object is specified then it will always return the value</typeparam>
        /// <param name="oracleParam">The Oracle parameter to convert</param>
        /// <exception cref="InvalidCastException">when the oracle parameter can't be cast to the type specified.</exception>
        /// <returns>The oracle parameter with the correct object</returns>
        public static T ConvertOracleParameterToBaseType<T>(OracleParameter oracleParam)
        {
            var retType = typeof(T);
            var type = oracleParam.Value.GetType();

            //This will handle Models.TypeModel and any other that don't require casting
            if (type == retType)
            {
                //Check if property IsNull exists
                if (type.GetProperty("IsNull") != null)
                {
                    dynamic val = oracleParam.Value;
                    if (val.IsNull)
                        return default(T);
                }

                return (T)oracleParam.Value;
            }

            bool isNullable = (retType.IsGenericType && retType.GetGenericTypeDefinition() == typeof(Nullable<>));

            object value = default(T);

            switch (oracleParam.Value)
            {
                case OracleDecimal dec:
                    if (dec.IsNull)
                    {
                        if (isNullable || !retType.IsValueType || retType == typeof(string))
                            value = null;
                        else
                            throw new InvalidCastException($"Can't cast a null value to {retType.Name}");
                    }
                    else if (retType == typeof(int) || retType == typeof(int?))
                        value = dec.ToInt32();
                    else if (retType == typeof(float) || retType == typeof(float?) || retType == typeof(Single))
                        value = dec.ToSingle();
                    else if (retType == typeof(double) || retType == typeof(double?))
                        value = dec.ToDouble();
                    else if (retType == typeof(decimal) || retType == typeof(decimal?))
                        value = dec.Value;
                    else if (retType == typeof(byte) || retType == typeof(byte?))
                        value = dec.ToByte();
                    else if (retType == typeof(string))
                        value = dec.ToString();
                    else
                        throw new InvalidCastException($"Can't cast OracleDecimal to {retType.Name}");
                    break;
                case OracleString str when retType == typeof(string):
                    if (str.IsNull)
                        value = null;
                    else
                        value = str.ToString();
                    break;
                case OracleClob clob when retType == typeof(string):
                    value = extractValue(clob);
                    break;
                case OracleBFile file when retType == typeof(byte[]):
                    value = extractValue(file);
                    break;
                case OracleBlob blob when retType == typeof(byte[]):
                    value = extractValue(blob);
                    break;
                case OracleDate date when retType == typeof(DateTime) || retType == typeof(DateTime?):
                    value = extractNullableValue(date, isNullable);
                    break;
                case OracleIntervalDS interval when retType == typeof(TimeSpan) || retType == typeof(TimeSpan?):
                    value = extractNullableValue(interval, isNullable);
                    break;
                case OracleIntervalYM intervalYM when (
                        retType == typeof(long) || retType == typeof(long?) ||
                        retType == typeof(float) || retType == typeof(float?) ||
                        retType == typeof(double) || retType == typeof(double?)
                    ):
                    value = extractNullableValue(intervalYM, isNullable);
                    break;
                case OracleBinary binary when retType == typeof(byte[]):
                    value = extractValue(binary);
                    break;
                case OracleRef reff when retType == typeof(string):
                    value = extractValue(reff);
                    break;
                case OracleTimeStamp timestamp when retType == typeof(DateTime) || retType == typeof(DateTime?):
                    extractNullableValue(timestamp, isNullable);
                    break;
                case OracleTimeStampLTZ timestampLTZ when retType == typeof(DateTime) || retType == typeof(DateTime?):
                    extractNullableValue(timestampLTZ, isNullable);
                    break;
                case OracleTimeStampTZ timestampTZ when retType == typeof(DateTime) || retType == typeof(DateTime?):
                    extractNullableValue(timestampTZ, isNullable);
                    break;
                default:
                    if (TryGetCollectionKeyValue(retType, out var oracleType))
                    {
                        dynamic temp = oracleParam.Value;
                        value = temp.Array;
                    }
                    else
                        throw new InvalidCastException($"Can't cast type {type.Name} to {retType.Name} " +
                            $"for oracle type ${oracleType}");
                    break;
            }

            return (T)value;
        }
    }
}