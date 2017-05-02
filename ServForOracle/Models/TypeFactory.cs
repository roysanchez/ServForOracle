using Oracle.DataAccess.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;

namespace ServForOracle.Models
{
    public abstract class TypeFactory : IOracleCustomTypeFactory, INullable
    {
        public IOracleCustomType CreateObject()
        {
            var x = Activator.CreateInstance(this.GetType()) as IOracleCustomType;
            return x;
        }

        public static Object Null
        {
            get
            {
                throw new NotImplementedException("Must be implemented in the base class");
            }
        }

        public bool IsNull { get; set; }
    }
}
