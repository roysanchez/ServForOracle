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
    internal class OutParam<T>: OutParam
    {
        public OutParam(Param<T> param, OracleParameter oracleParameter)
            :base(param, oracleParameter)
        {
            ServiceParameter = param;
            OutParameter = oracleParameter;
        }

        public new Param<T> ServiceParameter { get; set; }

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

    internal abstract class OutParam
    {
        public OutParam(Param param, OracleParameter oracleParameter)
        {
            ServiceParameter = param;
            OutParameter = oracleParameter;
        }

        public Param ServiceParameter { get; set; }
        public OracleParameter OutParameter { get; set; }

        public abstract void SetParamValue();
    }
}
