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
    internal class OutParam
    {
        public OutParam(Param param, OracleParameter oracleParameter)
        {
            ServiceParameter = param;
            OutParameter = oracleParameter;
        }

        public Param ServiceParameter { get; set; }
        public OracleParameter OutParameter { get; set; }

        private static MethodInfo ConverterBase = typeof(ParamHandler).GetMethod("ConvertOracleParameterToBaseType");


        public void SetParamValue()
        {
            try
            {
                var converter = ConverterBase.MakeGenericMethod(ServiceParameter.Type);
                ServiceParameter.Value = converter.Invoke(null, new[] { OutParameter });
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
}
