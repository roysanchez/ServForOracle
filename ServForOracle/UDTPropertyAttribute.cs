using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UDTPropertyAttribute: Attribute
    {
        public string Name { get; private set; }

        public UDTPropertyAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            Name = name;
        }
    }
}
