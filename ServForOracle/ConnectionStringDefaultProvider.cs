using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    public class ConnectionStringDefaultProvider : IConnectionStringProvider
    {
        public ConnectionStringDefaultProvider(string connectionString)
        {
            Value = connectionString;
        }

        public string Value { get; }
    }
}
