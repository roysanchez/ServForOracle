using ServForOracle.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;

namespace ServForOracle
{
    public class Param
    {
        private Param(ParamDirection direction, Type type, object value, OracleDbType? oracleType = null)
        {
            if (typeof(ParamDirection) == type || typeof(Param) == type)
                throw new ArgumentException($"Can't use {type.Name} as a parameter type", nameof(type));

            ParamDirection = direction;
            Value = value;
            Type = type;
            OracleType = oracleType;
        }

        public ParamDirection ParamDirection { get; set; }
        public object Value { get; set; }
        public Type Type { get; set; }

        internal OracleDbType? OracleType { get; set; }

        public static Param Create<T>(ParamDirection direction, T value, OracleDbType? oracleType = null)
        {
            var type = typeof(T);
            if (!ParamHandler.IsValidParameterType(type))
                throw new ArgumentException(string.Format(ParamHandler.InvalidClassMessage, type.Name));

            return new Param(direction, type, value, oracleType);
        }

        public static Param Input<T>(T value = default(T)) => Create(ParamDirection.Input, value);

        public static Param Output<T>(T value = default(T)) => Create(ParamDirection.Output, value);
    }
}
