using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using ServForOracle.Models;
using ServForOracle.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Internal
{
    /// <summary>
    /// Handles all the common interactions (convertion, transformation, extracting values, etc...)
    ///  between the <see cref="Param{T}"/> objects and the <see cref="OracleParameter"/> objects
    /// </summary>
    internal static class ParamHandler
    {
        private const int VARCHAR_MAX_SIZE = 32000;

        //TODO Move the message to a resource
        public static string InvalidClassMessage { get; }
            = "The type {0} doesn't conform with the guidelines for input parameters. "
                + "Please see the documentation on how to use this library.";

        public static string TypeNotConfiguredMessage { get; }
            = "The type {0} is not configured for automatic casting, please open an issue on github. "
                + "In the mean time, you can use the OracleDbType Param create overload to solve it.";


        /// <summary>
        /// Checks if the <paramref name="type"/> is Assignable from any of the values in <see cref="ProxyFactory.CollectionProxies"/>
        /// </summary>
        /// <param name="type">The <see cref="IEnumerable{T}"/> to check if a proxy exists</param>
        /// <param name="collectionUdTName">The Oracle UDT Collection Name if it exists for the <paramref name="type"/></param>
        /// <returns>True if the <paramref name="collectionUdTName"/> is not empty</returns>
        private static bool TryGetCollectionUdtName(Type type, out string collectionUdTName)
        {
            collectionUdTName = ProxyFactory.CollectionProxies
                        .Where(c => c.Key == type.GetCollectionUnderType())
                        .Select(c => c.Value.UdtCollectionName)
                        .FirstOrDefault();

            return !string.IsNullOrWhiteSpace(collectionUdTName);
        }

        /// <summary>
        /// Checks if the type is either from the CLR or a generated proxy.
        /// </summary>
        /// <param name="type">The Type to check</param>
        /// <returns>if the type specified can be used with the library</returns>
        public static bool IsValidParameterType(Type type)
        {
            if (type.IsValueType
                || type == typeof(string)
                || ProxyFactory.Proxies.ContainsKey(type)
                || TryGetCollectionUdtName(type, out var collectionValue)
                )
                return true;

            return false;
        }

        /// <summary>
        /// Creates an oracle parameter as a return value
        /// </summary>
        /// <typeparam name="T">The type expected to be returned by the function</typeparam>
        /// <returns>An <see cref="OracleParameter"/> configured for the return of the specified type</returns>
        public static OracleParameter CreateReturnParam<T>()
        {
            return CreateOracleParam(typeof(T), null, ParameterDirection.ReturnValue);
        }

        /// <summary>
        /// Creates an <see cref="OracleParameter"/> from the param
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the oracle parameter value</typeparam>
        /// <param name="parameter">The <see cref="Param{T}"/> to transform into an <see cref="OracleParameter"/> object</param>
        /// <remarks>This method is called through Reflexion on the <see cref="ServiceForOracle"/> class</remarks>
        /// <returns>A new <see cref="OracleParameter"/> configured with the <see cref="Param{T}"/> value</returns>
        public static OracleParameter CreateOracleParam<T>(Param<T> parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            var paramDirection = ParameterDirection.Input;
            switch (parameter.ParamDirection)
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

            return CreateOracleParam(typeof(T), ProxyFactory.ConvertToProxy<T>(parameter.Value), paramDirection, parameter.OracleType);
        }

        /// <summary>
        /// Creates an <see cref="OracleParameter"/> based on the CLR types, following the mapping table defined here:
        /// //docs.oracle.com/database/121/ODPNT/featUDTs.htm#BABIFHGJ
        /// </summary>
        /// <param name="type">The CLR <see cref="Type"/> to try and map</param>
        /// <param name="value">The value that will be assign to the <see cref="OracleParameter"/>
        /// (can be null only if the direction is <see cref="ParameterDirection.Output"/>)</param>
        /// <param name="direction">The direction of the OracleParameter (IN, OUT, INOUT)</param>
        /// <param name="oracleType">For advance uses, specify directly the OracleDbType to use instead of the standard mapping</param>
        /// <returns>An OracleParameter with the expected OracleDbType that closely maps to the CLR type specified.</returns>
        private static OracleParameter CreateOracleParam(Type type, object value, ParameterDirection direction,
            OracleDbType? oracleType = null)
        {

            if (type == null)
                throw new ArgumentNullException(nameof(type), "The type for the Oracle Parameter can not be null");

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
            else if (type.IsArray && type == typeof(byte[]))
            {
                param.OracleDbType = OracleDbType.Blob;
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
            else if (ProxyFactory.Proxies.TryGetValue(type, out var proxy))
            {
                param.UdtTypeName = proxy.UdtName;
                param.OracleDbType = OracleDbType.Object;
            }
            else if (TryGetCollectionUdtName(type, out var collectionUdtName))
            {
                param.UdtTypeName = collectionUdtName;
                param.OracleDbType = OracleDbType.Array;
            }
            else
                throw new Exception(string.Format(InvalidClassMessage, type.Name));

            param.Direction = direction;
            param.Value = _value;
            return param;
        }

        /// <summary>
        /// Extracts the oracle UDT type name from the <see cref="UDTNameAttribute"/>
        /// </summary>
        /// <param name="type">The <see cref="Type"/> to extract the attribute value from</param>
        /// <returns>A string with the name of the Oracle UDT in the <see cref="UDTNameAttribute"/>, otherwise null</returns>
        private static string GetOracleTypeNameFromAttribute(Type type)
        {
            //TODO throw exception when null
            //TODO never return null
            if (type == null) return null;

            var oracleAttribute = (
                    from attr in type.GetCustomAttributes(false)
                    where attr.GetType() == typeof(UDTNameAttribute)
                    select attr as UDTNameAttribute
                ).FirstOrDefault();

            return oracleAttribute?.Name;
        }

        /// <summary>
        /// Simple abstraction for the common oracle interface that handles <see cref="Nullable{T}"/> types
        /// </summary>
        /// <param name="param"><see cref="OracleParameter"/> object</param>
        /// <param name="isNullable">If the result can be null</param>
        /// <returns>The <see cref="OracleParameter"/> value</returns>
        /// <exception cref="InvalidCastException">If the parameter <paramref name="isNullable"/> is false but
        /// the <paramref name="param"/> is null</exception>
        /// <seealso cref="ExtractValue(dynamic)"/>
        private static object ExtractNullableValue(dynamic param, bool isNullable)
        {
            if (isNullable)
                return ExtractValue(param);
            else if (param.IsNull)
                throw new InvalidCastException("Can't cast null to a non nullable");
            else
                return param.Value;
        }

        /// <summary>
        /// Simple abstraction over the common oracle interface, extracts the value from <see cref="INullable"/> types
        /// </summary>
        /// <param name="param">An <see cref="OracleParameter"/> object</param>
        /// <returns>The <see cref="OracleParameter"/> value</returns>
        /// <exception cref="InvalidCastException">If the </exception>
        /// <seealso cref="ExtractNullableValue(dynamic, bool)"/>
        private static object ExtractValue(dynamic param)
        {
            if (!(param is Oracle.DataAccess.Types.INullable))
                throw new InvalidCastException($"Can't use {nameof(ExtractValue)} for type ${param.GetType().Name}");

            if (param.IsNull)
                return null;
            else
                return param.Value;
        }

        /// <summary>
        /// Converts the oracle type to the corresponding dotnet type, as specified in the following spec:
        /// <see cref="https://docs.oracle.com/database/121/ODPNT/featUDTs.htm#BABIFHGJ"/>
        /// </summary>
        /// <typeparam name="T">The type expected, if <see cref="object"/> is specified then it will always return the value</typeparam>
        /// <param name="oracleParam">The <see cref="OracleParameter"/> to convert from</param>
        /// <exception cref="InvalidCastException">when the <paramref name="oracleParam"/> can't be casted to the expected <typeparamref name="T"/>.</exception>
        /// <returns>The oracle parameter transformed to the correct <typeparamref name="T"/></returns>
        public static T ConvertOracleParameterToBaseType<T>(OracleParameter oracleParam)
        {
            var retType = typeof(T);
            var type = oracleParam.Value.GetType();

            //This will handle Models.TypeModel and any other that don't require casting
            if (ProxyFactory.Proxies.ContainsKey(retType) || type == retType)
            {
                //Check if property IsNull exists
                var prop = type.GetProperty(nameof(TypeFactory.IsNull));
                if (prop != null)
                {
                    dynamic val = prop.GetValue(oracleParam.Value);
                    if (val)
                    {
                        return default(T);
                    }
                }

                return ProxyFactory.ConvertFromProxy<T>(oracleParam.Value, type);
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
                    value = ExtractValue(clob);
                    break;
                case OracleBFile file when retType == typeof(byte[]):
                    value = ExtractValue(file);
                    break;
                case OracleBlob blob when retType == typeof(byte[]):
                    value = ExtractValue(blob);
                    break;
                case OracleDate date when retType == typeof(DateTime) || retType == typeof(DateTime?):
                    value = ExtractNullableValue(date, isNullable);
                    break;
                case OracleIntervalDS interval when retType == typeof(TimeSpan) || retType == typeof(TimeSpan?):
                    value = ExtractNullableValue(interval, isNullable);
                    break;
                case OracleIntervalYM intervalYM when (
                        retType == typeof(long) || retType == typeof(long?) ||
                        retType == typeof(float) || retType == typeof(float?) ||
                        retType == typeof(double) || retType == typeof(double?)
                    ):
                    value = ExtractNullableValue(intervalYM, isNullable);
                    break;
                case OracleBinary binary when retType == typeof(byte[]):
                    value = ExtractValue(binary);
                    break;
                case OracleRef reff when retType == typeof(string):
                    value = ExtractValue(reff);
                    break;
                case OracleTimeStamp timestamp when retType == typeof(DateTime) || retType == typeof(DateTime?):
                    ExtractNullableValue(timestamp, isNullable);
                    break;
                case OracleTimeStampLTZ timestampLTZ when retType == typeof(DateTime) || retType == typeof(DateTime?):
                    ExtractNullableValue(timestampLTZ, isNullable);
                    break;
                case OracleTimeStampTZ timestampTZ when retType == typeof(DateTime) || retType == typeof(DateTime?):
                    ExtractNullableValue(timestampTZ, isNullable);
                    break;
                default:
                    if (ProxyFactory.CollectionProxies.TryGetValue(retType.GetCollectionUnderType(), out var proxyCollection))
                    {
                        var prop = type.GetProperty("Array");
                        value = ProxyFactory.ConvertFromProxy<T>(prop.GetValue(oracleParam.Value), proxyCollection.ProxyCollectionType);
                    }
                    else
                        throw new InvalidCastException($"Can't cast type {type.Name} to {retType.Name} " +
                            $"for oracle type ${proxyCollection.UdtCollectionName}");
                    break;
            }

            return (T)value;
        }
    }
}