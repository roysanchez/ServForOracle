using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    public class UDTNameAttribute: Attribute
    {
        public string Name { get; private set; }
        public UDTNameAttribute(string name)
        {
            Name = name;
        }
    }
}
