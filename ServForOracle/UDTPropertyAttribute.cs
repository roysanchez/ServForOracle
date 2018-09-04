using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    /// <summary>
    /// Attribute that specifies the Oracle UDT Object property name
    /// </summary>
    /// <remarks>The attribute is optional as the library will try to use the property name if it doesn't find this attribute</remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class UDTPropertyAttribute: Attribute
    {
        /// <summary>
        /// The Oracle UDT Object property name
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// </summary>
        /// <param name="name">The name of the UDT object property.</param>
        /// <remarks>It transforms the <paramref name="name"/> to uppercase.</remarks>
        /// <exception cref="ArgumentNullException">If the <paramref name="name"/> is either null or whitespace.</exception>
        public UDTPropertyAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            Name = name;
        }
    }
}
