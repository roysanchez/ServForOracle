using ServForOracle.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;

namespace ServForOracle
{
    public class Param<T>: Param
    {
        internal Param(ParamDirection direction, T value, OracleDbType? oracleType = null)
            :base(direction, typeof(T), value, oracleType)
        {
            Value = value;
        }

        public T Value { get; set; }
    }

    /// <summary>
    /// The parameters that are send to Oracle procedure's or function's.
    /// Handles the parameters information that is used to construct the oracleparam at query time.
    /// </summary>
    public abstract class Param
    {
        internal Param(ParamDirection direction, Type type, object value, OracleDbType? oracleType = null)
        {
            if (typeof(ParamDirection) == type || typeof(Param) == type)
                throw new ArgumentException($"Can't use {type.Name} as a parameter type", nameof(type));

            ParamDirection = direction;
            Type = type;
            OracleType = oracleType;
        }

        public ParamDirection ParamDirection { get; set; }
        public Type Type { get; set; }

        internal OracleDbType? OracleType { get; set; }

        /// <summary>
        /// Creates a paramater for an Oracle procedure or function
        /// </summary>
        /// <typeparam name="T">The type of the parameter to send (clr or custom types)</typeparam>
        /// <param name="direction">The direction of the parameter, whether is Input, Output or InputOutput</param>
        /// <param name="value">If it's an input parameter, the value to send</param>
        /// <param name="oracleType">Optional parameter indicating the OracleDbType to which this parameter should
        /// map. It's an advance option, should only be used when the library can't correctly infer the oracle type</param>
        /// <returns>A newly created param object to send to the oracledb</returns>
        /// <remarks>This is the full object factory method, try to use the more specific Input/Output methods.
        /// Only use this one when strictly necessary.</remarks>
        /// <remarks>The output parameters set the return value in the value object property</remarks>
        public static Param<T> Create<T>(ParamDirection direction, T value, OracleDbType? oracleType = null)
        {
            var type = typeof(T);
            if (!ParamHandler.IsValidParameterType(type))
                throw new ArgumentException(string.Format(ParamHandler.InvalidClassMessage, type.Name));

            return new Param<T>(direction, value, oracleType);
        }

        /// <summary>
        /// Creates an input parameter for an Oracle db procedure or function
        /// </summary>
        /// <typeparam name="T">The type of the parameter to send</typeparam>
        /// <param name="value">The value that you're going to send to the db</param>
        /// <returns>A newly created Param object configured as an input for the type specified.</returns>
        public static Param<T> Input<T>(T value = default(T)) => Create(ParamDirection.Input, value);

        /// <summary>
        /// Creates an output parameter for an Oracle db procedure or function
        /// </summary>
        /// <typeparam name="T">The type of the parameter to recieve</typeparam>
        /// <returns>A newly created param object configured as an output for the type specified</returns>
        /// <remarks>The output parameters set the return value in the value object property</remarks>
        public static Param<T> Output<T>() => Create(ParamDirection.Output, default(T));

        /// <summary>
        /// Creates an input/Output parameter for an Oracle db procedure or function
        /// </summary>
        /// <typeparam name="T">The type of the parameter to send and recieve</typeparam>
        /// <param name="value">The value that you're going to send and recieve from the db</param>
        /// <returns>A newly created Param object configured as an input/Output for the type specified.</returns>
        /// <remarks>The output parameters set the return value in the value object property</remarks>
        public static Param<T> InputOutput<T>(T value = default(T)) => Create(ParamDirection.InputOutput, value);
    }
}