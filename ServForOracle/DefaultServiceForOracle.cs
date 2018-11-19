using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    public class DefaultServiceForOracle : ServiceForOracle, IServiceForOracle
    {
        /// <summary>
        /// Creates a default Oracle Service Implementation
        /// </summary>
        /// <param name="connectionString">A connection string using the format: 
        /// "Data Source=TNSNAMEorIP:Port/DBNAME; Pooling=TrueOrFalse;User id=DBUSER; password=USERPWD;"</param>
        /// <remarks>For all the possible configurations see: https://docs.oracle.com/cd/B28359_01/win.111/b28375/featConnecting.htm </remarks>
        public DefaultServiceForOracle(string connectionString)
            :base(new ConnectionStringDefaultProvider(connectionString))
        {
        }

        /// <summary>
        /// Creates a default Oracle Service implementation
        /// </summary>
        /// <param name="TNSOrIP">The IP of the database or the TNSName of the database that is configured in the machine or in the same directory
        /// as this library with the name tnsnames.ora</param>
        /// <param name="Port">The port used to connect to the database. Defaults to 1521</param>
        /// <param name="Name">The database name. Defaults to ORCL</param>
        /// <param name="Username">The username of the db</param>
        /// <param name="Password">The password of the user</param>
        /// <param name="Pooling">A boolean indicating if pooling is enabled. Defaults to false</param>
        public DefaultServiceForOracle(string TNSOrIP, string Username, string Password,
            string Name = "ORCL", bool Pooling = false, int Port = 1521)
            :base(new ConnectionStringDefaultProvider($"Data Source={TNSOrIP}:{Port}/{Name}; " +
                $"Pooling={Pooling.ToString().ToLower()};" +
                $"User id={Username}; password={Password};"))
        {
        }

        /// <summary>
        /// Executes an Oracle Function
        /// </summary>
        /// <typeparam name="T">The espected result of the oracle function</typeparam>
        /// <param name="function">The function to execute, which follows this format: schema.function or schema.package.function</param>
        /// <param name="parameters">The parameters of the function  whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        /// <returns>A task indicating the result of the execution and containing the return value if it is successful</returns>
        public async new Task<T> ExecuteFunctionAsync<T>(string function, params Param[] parameters) => 
            await base.ExecuteFunctionAsync<T>(function, parameters);

        /// <summary>
        /// Executes an Oracle Function
        /// </summary>
        /// <typeparam name="T">The espected result of the oracle function</typeparam>
        /// <param name="function">The function to execute, which follows this format: schema.function or schema.package.function</param>
        /// <param name="parameters">The parameters of the function  whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        /// <returns>The result of the execution and containing the return value if it is successful</returns>
        public new T ExecuteFunction<T>(string function, params Param[] parameters) =>
            base.ExecuteFunction<T>(function, parameters);

        /// <summary>
        /// Executes the DDL specified
        /// </summary>
        /// <param name="ddl">The DDL to execute</param>
        /// <returns>A task indicating he result of the execution</returns>
        public async new Task ExecuteDDLAsync(string ddl) => 
            await base.ExecuteDDLAsync(ddl);

        /// <summary>
        /// Executes the DDL specified
        /// </summary>
        /// <param name="ddl">The DDL to execute</param>
        public new void ExecuteDDL(string ddl) =>
            base.ExecuteDDL(ddl);

        /// <summary>
        /// Executes an Oracle procedure
        /// </summary>
        /// <param name="procedure">The procedure to run, with the format schema.procedure or schema.package.procedure</param>
        /// <param name="parameters">The parameters for the procedure, whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        /// <returns>A task indicating the result of the execution</returns>
        public async new Task ExecuteProcedureAsync(string procedure, params Param[] parameters) => 
            await base.ExecuteProcedureAsync(procedure, parameters);

        /// <summary>
        /// Executes an Oracle procedure
        /// </summary>
        /// <param name="procedure">The procedure to run, with the format schema.procedure or schema.package.procedure</param>
        /// <param name="parameters">The parameters for the procedure, whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        public new void ExecuteProcedure(string procedure, params Param[] parameters) =>
            base.ExecuteProcedure(procedure, parameters);

        public async new Task<T> ExecuteFunctionWithRefReturnAsync<T>(string function, params Param[] parameters) =>
            await base.ExecuteFunctionWithRefReturnAsync<T>(function, parameters);
    }
}
