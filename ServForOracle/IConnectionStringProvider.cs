using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    /// <summary>
    /// Interface for the connection string so that it can be specified through dependency injection
    /// </summary>
    public interface IConnectionStringProvider
    {
        string Value { get; }
    }
}
