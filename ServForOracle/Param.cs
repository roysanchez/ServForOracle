using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    public class Param
    {
        private Param(ParamDirection direction, Type type, object value)
        {
            if (typeof(ParamDirection) == type || typeof(Param) == type)
                throw new ArgumentException($"Can't use {type.Name} as a parameter type", nameof(type));

            ParamDirection = direction;
            Value = value;
            Type = type;
        }

        public ParamDirection ParamDirection { get; set; }
        public object Value { get; set; }
        public Type Type { get; set; }

        public static Param Create<T>(ParamDirection direction, T value)
        {
            return new Param(direction, typeof(T), value);
        }

        public static Param Input<T>(T value = default(T))
        {
            return new Param(ParamDirection.Input, typeof(T), value);
        }

        public static Param Output<T>(T value = default(T))
        {
            return new Param(ParamDirection.Output, typeof(T), value);
        }
    }
}
