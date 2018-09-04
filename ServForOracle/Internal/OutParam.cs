using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Internal
{
    /// <summary>
    /// Type casted implementation of the <see cref="OutParam"/> base class
    /// </summary>
    /// <typeparam name="T">The expected <see cref="Type"/> to be returned by the <see cref="OracleParameter"/></typeparam>
    internal class OutParam<T>: OutParam
    {
        public OutParam(Param<T> param, OracleParameter oracleParameter)
            :base(param, oracleParameter)
        {
            ServiceParameter = param;
            OutParameter = oracleParameter;
        }

        /// <summary>
        /// The parameter definition set by the user
        /// </summary>
        public new Param<T> ServiceParameter { get; set; }

        /// <summary>
        /// Sets the value of the <see cref="ServiceParameter"/> by calling 
        /// <see cref="ParamHandler.ConvertOracleParameterToBaseType{T}(OracleParameter)"/>
        /// </summary>
        /// <exception cref="Exception">catches and gives more context to any exception generated in the convertion process</exception>
        public override void SetParamValue()
        {
            try
            {
                ServiceParameter.Value = ParamHandler.ConvertOracleParameterToBaseType<T>(OutParameter);
            }
            catch (Exception ex)
            {
                throw new Exception(
                    $"Error trying to set value for type {ServiceParameter.Type.Name} " +
                    $"with direction {ServiceParameter.ParamDirection}. " +
                    "See inner exception for details",
                    ex);
            }
        }
    }

    /// <summary>
    /// The class is used to segregate the <see cref="ParamDirection.Output"/>/<see cref="ParamDirection.InputOutput"/>
    /// <see cref="OracleParameter"/> objects from the <see cref="ParamDirection.Input"/> for later processing, as they
    /// required to be casted to the expected <see cref="Type"/>.
    /// </summary>
    internal abstract class OutParam
    {
        public OutParam(Param param, OracleParameter oracleParameter)
        {
            ServiceParameter = param;
            OutParameter = oracleParameter;
        }

        /// <summary>
        /// The parameter definition send by the user
        /// </summary>
        public Param ServiceParameter { get; set; }
        /// <summary>
        /// The <see cref="OracleParameter"/> recieved by the library after executing the command
        /// </summary>
        public OracleParameter OutParameter { get; set; }

        /// <summary>
        /// Sets the value of the <see cref="OutParam.ServiceParameter"/> from the <see cref="OutParam.OutParameter"/>
        /// </summary>
        public abstract void SetParamValue();
    }
}
