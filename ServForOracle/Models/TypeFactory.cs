using Oracle.DataAccess.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;

namespace ServForOracle.Models
{
    /// <summary>
    /// Common implementation of the Oracle UDT handling, for both objects and collections
    /// </summary>
    public abstract class TypeFactory : IOracleCustomTypeFactory, INullable
    {
        public IOracleCustomType CreateObject()
        {
            return Activator.CreateInstance(this.GetType()) as IOracleCustomType;
        }

        /// <summary>
        /// Must be implemented on the base class
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
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
