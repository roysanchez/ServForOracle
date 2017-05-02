using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Internal
{
    internal static class ParamHandler
    {
        public static OracleParameter CreateReturnParam<T>()
        {
            return CreateParam(typeof(T), null, ParameterDirection.ReturnValue);
        }

        public static OracleParameter CreateParam<T>(Type type, T value, ParamDirection paramType)
        {
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

            return CreateParam(type, value, paramDirection);
        }

        private static OracleParameter CreateParam(Type type, object value, ParameterDirection direction)
        {
            object _value = value;
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            var param = new OracleParameter();
            if (type.IsValueType)
            {
                if (type == typeof(int) || type == typeof(int?))
                {
                    param.OracleDbType = OracleDbType.Int32;
                }
                else if (type == typeof(double) || type == typeof(double?))
                {
                    param.OracleDbType = OracleDbType.Double;
                }
                else if (type == typeof(DateTime) || type == typeof(DateTime?))
                {
                    param.OracleDbType = OracleDbType.Date;
                }
            }
            else if (type == typeof(string))
            {
                param.OracleDbType = OracleDbType.Varchar2;
                if (direction != ParameterDirection.Input)
                    param.Size = 32000;
            }
            else
            {
                var attrCustomType = (from attr in type.GetCustomAttributes(false)
                                      where attr.GetType() == typeof(OracleCustomTypeMappingAttribute)
                                      select attr as OracleCustomTypeMappingAttribute
                                     ).FirstOrDefault();

                var attrArray = (from prop in type.GetProperties()
                                 from attr in prop.GetCustomAttributes(true)
                                 where attr.GetType() == typeof(OracleArrayMappingAttribute)
                                 select attr as OracleArrayMappingAttribute
                                ).FirstOrDefault();

                if (attrArray != null)
                {
                    param.OracleDbType = OracleDbType.Array;
                }
                else
                    param.OracleDbType = OracleDbType.Object;

                param.UdtTypeName = attrCustomType.UdtTypeName;
            }

            param.Direction = direction;
            param.Value = _value;
            return param;
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
                throw new InvalidCastException($"Can't use {nameof(extractValue)} for type ");

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
                return (T)oracleParam.Value;

            bool isNullable = (retType.IsGenericType && retType.GetGenericTypeDefinition() == typeof(Nullable));

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
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Models.CollectionModel<>))
                    {
                        var collectionArgumentType = type.GetGenericArguments()[0]; //Qoute
                        var collectionType = typeof(IEnumerable<>).MakeGenericType(collectionArgumentType); //IEnumerable<Quote>

                        if (collectionType.IsAssignableFrom(retType))
                        {
                            dynamic temp = oracleParam.Value;
                            value = temp.Array;
                        }
                        else
                            throw new InvalidCastException($"Can't cast type {retType.Name} to " +
                                $"IEnumerable of {collectionArgumentType.Name}");
                    }
                    else
                        throw new InvalidCastException($"Can't cast type {retType.Name} to {type.Name}");
                    break;
            }

            return (T)value;
        }
    }
}
