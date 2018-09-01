using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class UDTCollectionNameAttribute: Attribute
    {
        public string Name { get; private set; }
        public UDTCollectionNameAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            Name = name.ToUpper();
        }
    }
}
