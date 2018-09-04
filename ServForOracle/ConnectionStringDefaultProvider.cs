using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    /// <summary>
    /// Simple connection string wrapper so that it can be specified through depedency injection
    /// </summary>
    public class ConnectionStringDefaultProvider : IConnectionStringProvider
    {
        public ConnectionStringDefaultProvider(string connectionString)
        {
            Value = connectionString;
        }

        public string Value { get; }
    }
}
