using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    public interface IServiceForOracle
    {
        /// <summary>
        /// Executes the DDL specified
        /// </summary>
        /// <param name="ddl">The DDL to execute</param>
        /// <returns>A task indicating he result of the execution</returns>
        Task ExecuteDDLAsync(string ddl);

        /// <summary>
        /// Executes an Oracle Function
        /// </summary>
        /// <typeparam name="T">The espected result of the oracle function</typeparam>
        /// <param name="function">The function to execute, which follows this format: schema.function or schema.package.function</param>
        /// <param name="parameters">The parameters of the function  whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        /// <returns>A task indicating the result of the execution and containing the return value if it is successful</returns>
        Task<T> ExecuteFunctionAsync<T>(string function, params Param[] parameters);

        /// <summary>
        /// Executes an Oracle procedure
        /// </summary>
        /// <param name="procedure">The procedure to run, with the format schema.procedure or schema.package.procedure</param>
        /// <param name="parameters">The parameters for the procedure, whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        /// <returns>A task indicating the result of the execution</returns>
        Task ExecuteProcedureAsync(string procedure, params Param[] parameters);
    }
}
