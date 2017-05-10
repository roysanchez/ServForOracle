using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Models
{
    public abstract class TypeModel : TypeFactory, IOracleCustomType
    {
        void ProcessProperties(Action<PropertyInfo, OracleObjectMappingAttribute> process)
        {
            var properties = GetType().GetRuntimeProperties();

            foreach (var prop in properties)
            {
                foreach (var attr in prop.GetCustomAttributes(false))
                {
                    if(attr is OracleObjectMappingAttribute)
                    {
                        process(prop, attr as OracleObjectMappingAttribute);
                    }
                }
            }
        }

        public void FromCustomObject(OracleConnection con, IntPtr pUdt)
        {
            ProcessProperties((prop, attr) =>
            {
                OracleUdt.SetValue(con, pUdt, attr.AttributeName, prop.GetValue(this));
            });
        }

        public void ToCustomObject(OracleConnection con, IntPtr pUdt)
        {
            ProcessProperties((prop, attr) =>
            {
                prop.SetValue(this, OracleUdt.GetValue(con, pUdt, attr.AttributeName));
            });
        }
    }
}