using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    /// <summary>
    /// Attribute that specifies the Oracle UDT Name, must have the format "SCHEMA.UDTOBJECTNAME"
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class UDTCollectionNameAttribute : Attribute
    {
        /// <summary>
        /// The Oracle UDT Collection name
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// </summary>
        /// <param name="name">The name of the Oracle UDT Collection</param>
        /// <exception cref="ArgumentNullException">The <paramref name="name"/> is null or whitespace</exception>
        /// <exception cref="ArgumentException">The <paramref name="name"/> has the incorrect format</exception>
        /// <remarks>
        /// <para>The <paramref name="name"/> must have the format "SCHEMA.UDTNAME"</para>
        /// <para>The <paramref name="name"/> it's transformed to uppercase</para>
        /// </remarks>
        public UDTCollectionNameAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            var parts = name.Split('.');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new ArgumentException("The UDT name must have the format \"SCHEMA.UDTNAME\"", nameof(name));
            }

            Name = name.ToUpper();
        }
    }
}
