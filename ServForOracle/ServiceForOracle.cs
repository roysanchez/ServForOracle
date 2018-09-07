using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;
using System.Runtime;
using System.Reflection;
using ServForOracle.Internal;
using System.IO;
using ServForOracle.Tools;

namespace ServForOracle
{
    public abstract class ServiceForOracle
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionString"></param>
        /// <exception cref="ArgumentNullException">If either the <paramref name="connectionString"/> or its 
        /// <see cref="IConnectionStringProvider.Value"/> property is null</exception>
        public ServiceForOracle(IConnectionStringProvider connectionString)
        {
            if (connectionString == null || string.IsNullOrEmpty(connectionString.Value))
                throw new ArgumentNullException(nameof(connectionString));

            DbConnection = new OracleConnection(connectionString.Value);
            connectionCreatedExternally = false;
        }

        /// <summary>
        /// Use a pre-existing Oracle connection
        /// </summary>
        /// <param name="oracleConnection"></param>
        public ServiceForOracle(OracleConnection oracleConnection)
        {
            DbConnection = oracleConnection;
            connectionCreatedExternally = true;
        }
        
        /// <summary>
        /// Oracle connection used to execute all the commands
        /// </summary>
        public OracleConnection DbConnection { get; private set; }


        private readonly bool connectionCreatedExternally;
        private static readonly MethodInfo ConverterBase = typeof(ParamHandler).GetMethod("CreateOracleParam");

        /// <summary>
        /// Opens the <see cref="OracleConnection"/> and creates a <see cref="OracleCommand"/>
        /// </summary>
        /// <param name="proc">The procedure or function to execute</param>
        /// <param name="type">The type of the command, either a procedure/function or DDL query</param>
        /// <returns>The new command</returns>
        /// <remarks>
        /// <para>If the connection was created externally it doesn't close the connection after the command is executed</para>
        /// <para>Doesn't continue on the same context</para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">If the <paramref name="proc"/> is null or whitespace</exception>
        private async Task<OracleCommand> CreateCommandAndOpenConnectionAsync(string proc, CommandType type)
        {
            if (string.IsNullOrWhiteSpace(proc))
                throw new ArgumentNullException(nameof(proc));

            await DbConnection.OpenAsync().ConfigureAwait(false);
            return CreateCommand(proc, type);
        }

        /// <summary>
        /// Opens the <see cref="OracleConnection"/> and creates a <see cref="OracleCommand"/>
        /// </summary>
        /// <param name="proc">The procedure or function to execute</param>
        /// <param name="type">The type of the command, either a procedure/function or DDL query</param>
        /// <returns>The new command</returns>
        /// <remarks>If the connection was created externally it doesn't close the connection after the command is executed</remarks>
        /// <exception cref="ArgumentNullException">If the <paramref name="proc"/> is null or whitespace</exception>
        private OracleCommand CreateCommandAndOpenConnection(string proc, CommandType type)
        {
            if (string.IsNullOrWhiteSpace(proc))
                throw new ArgumentNullException(nameof(proc));

            DbConnection.Open();
            return CreateCommand(proc, type);
        }

        /// <summary>
        /// Creates an <see cref="OracleCommand"/> for the <see cref="DbConnection"/>
        /// </summary>
        /// <param name="proc">the procedure or function to execute</param>
        /// <param name="type">The type of command</param>
        /// <returns>The new oracle command with the proper format</returns>
        /// <remarks>If the connection was not created externally then it will we closed on command's dispose</remarks>
        private OracleCommand CreateCommand(string proc, CommandType type)
        {
            var cmd = DbConnection.CreateCommand();
            cmd.CommandType = type;
            cmd.CommandText = proc.Replace("\r\n", "\n");

            cmd.Disposed += (object sender, EventArgs e) =>
            {
                if (!connectionCreatedExternally)
                    DbConnection.Close();
            };

            return cmd;
        }

        /// <summary>
        /// Executes the DDL specified
        /// </summary>
        /// <param name="ddl">The DDL to execute</param>
        /// <returns>A task indicating he result of the execution</returns>
        /// <remarks>Doesn't continue on the same context</remarks>
        protected async Task ExecuteDDLAsync(string ddl)
        {
            using (var cmd = await CreateCommandAndOpenConnectionAsync(ddl, CommandType.Text).ConfigureAwait(false))
            {
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Executes the DDL specified
        /// </summary>
        /// <param name="ddl">The DDL to execute</param>
        protected void ExecuteDDL(string ddl)
        {
            using (var cmd = CreateCommandAndOpenConnection(ddl, CommandType.Text))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Creates the <see cref="OracleParameter"/> for the return value
        /// </summary>
        /// <typeparam name="T">The expected type to be returned</typeparam>
        /// <param name="cmd">The oracle command to add the parameter to</param>
        /// <returns>The return oracle parameter</returns>
        private OracleParameter CreateReturnParameter<T>(OracleCommand cmd)
        {
            var ret = ParamHandler.CreateReturnParam<T>();
            cmd.Parameters.Add(ret);
            return ret;
        }

        /// <summary>
        /// Executes an Oracle Function
        /// </summary>
        /// <typeparam name="T">The espected result of the oracle function</typeparam>
        /// <param name="function">The function to execute, which follows this format: schema.function or schema.package.function</param>
        /// <param name="parameters">The parameters of the function  whether IN, OUT or IN OUT</param>
        /// <remarks>
        /// <para>The OUT and IN OUT parameters get their values set by this method</para>
        /// <para>Doesn't continue on the same context</para>
        /// </remarks>
        /// <returns>A task indicating the result of the execution and containing the return value if it is successful</returns>
        protected async Task<T> ExecuteFunctionAsync<T>(string function, params Param[] parameters)
        {
            using (var cmd = await CreateCommandAndOpenConnectionAsync(function, CommandType.StoredProcedure).ConfigureAwait(false))
            {
                var ret = CreateReturnParameter<T>(cmd);

                await ExecuteInnerAsync(cmd, parameters).ConfigureAwait(false);
                try
                {
                    return ParamHandler.ConvertOracleParameterToBaseType<T>(ret);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error converting the return value to type {typeof(T).Name} " +
                        $"in function {function}. See inner exception for details", ex);
                }
            }
        }

        /// <summary>
        /// Executes an Oracle Function
        /// </summary>
        /// <typeparam name="T">The espected result of the oracle function</typeparam>
        /// <param name="function">The function to execute, which follows this format: schema.function or schema.package.function</param>
        /// <param name="parameters">The parameters of the function  whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        /// <returns>The result of the execution and containing the return value if it is successful</returns>
        protected T ExecuteFunction<T>(string function, params Param[] parameters)
        {
            using (var cmd = CreateCommandAndOpenConnection(function, CommandType.StoredProcedure))
            {
                var ret = CreateReturnParameter<T>(cmd);

                ExecuteInner(cmd, parameters);

                try
                {
                    return ParamHandler.ConvertOracleParameterToBaseType<T>(ret);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error converting the return value to type {typeof(T).Name} " +
                        $"in function {function}. See inner exception for details", ex);
                }
            }
        }

        /// <summary>
        /// Executes an Oracle procedure
        /// </summary>
        /// <param name="procedure">The procedure to run, with the format schema.procedure or schema.package.procedure</param>
        /// <param name="parameters">The parameters for the procedure, whether IN, OUT or IN OUT</param>
        /// <remarks>
        /// <para>The OUT and IN OUT parameters get their values set by this method</para>
        /// <para>Doesn't continue on the same context</para>
        /// </remarks>
        /// <returns>A task indicating the result of the execution</returns>
        protected async Task ExecuteProcedureAsync(string procedure, params Param[] parameters)
        {
            using (var cmd = await CreateCommandAndOpenConnectionAsync(procedure, CommandType.StoredProcedure).ConfigureAwait(false))
            {
                try
                {
                    await ExecuteInnerAsync(cmd, parameters).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing {cmd.CommandText}. See inner exception for details.", ex);
                }
            }
        }

        /// <summary>
        /// Executes an Oracle procedure
        /// </summary>
        /// <param name="procedure">The procedure to run, with the format schema.procedure or schema.package.procedure</param>
        /// <param name="parameters">The parameters for the procedure, whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        protected void ExecuteProcedure(string procedure, params Param[] parameters)
        {
            using (var cmd = CreateCommandAndOpenConnection(procedure, CommandType.StoredProcedure))
            {
                try
                {
                    ExecuteInner(cmd, parameters);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error executing {cmd.CommandText}. See inner exception for details.", ex);
                }
            }
        }

        /// <summary>
        /// Calls the Oracle Executer for the procedure or function specified in the command 
        /// </summary>
        /// <param name="cmd">The OracleCommand that specifies the query to execute</param>
        /// <param name="parameters">The parameters to send</param>
        /// <returns>A Task indicating the status of the execution</returns>
        /// <remarks>Doesn't continue on the same context</remarks>
        private async Task ExecuteInnerAsync(OracleCommand cmd, Param[] parameters)
        {
            var outParameters = new List<OutParam>();
            foreach (var param in parameters)
            {
                var genericMethod = ConverterBase.MakeGenericMethod(param.Type);
                var oracleParam = genericMethod.Invoke(null, new[] { param }) as OracleParameter;
                
                cmd.Parameters.Add(oracleParam);

                if (oracleParam.Direction == ParameterDirection.Output || oracleParam.Direction == ParameterDirection.InputOutput)
                {
                    var genericOutParam = typeof(OutParam<>).MakeGenericType(param.Type);
                    
                    outParameters.Add(Activator.CreateInstance(genericOutParam, new object[] { param, oracleParam }) as OutParam);
                }
            }

            try
            {
                await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error executing {cmd.CommandText}. See inner exception for details.", ex);
            }

            foreach (var param in outParameters)
                param.SetParamValue();
        }

        /// <summary>
        /// Calls the Oracle Executer for the procedure or function specified in the command 
        /// </summary>
        /// <param name="cmd">The OracleCommand that specifies the query to execute</param>
        /// <param name="parameters">The parameters to send</param>
        private void ExecuteInner(OracleCommand cmd, Param[] parameters)
        {
            var outParameters = new List<OutParam>();
            foreach (var param in parameters)
            {
                var genericMethod = ConverterBase.MakeGenericMethod(param.Type);
                var oracleParam = genericMethod.Invoke(null, new[] { param }) as OracleParameter;

                cmd.Parameters.Add(oracleParam);

                if (oracleParam.Direction == ParameterDirection.Output || oracleParam.Direction == ParameterDirection.InputOutput)
                {
                    var genericOutParam = typeof(OutParam<>).MakeGenericType(param.Type);

                    outParameters.Add(Activator.CreateInstance(genericOutParam, new object[] { param, oracleParam }) as OutParam);
                }
            }

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error executing {cmd.CommandText}. See inner exception for details.", ex);
            }

            foreach (var param in outParameters)
                param.SetParamValue();
        }

        /// <summary>
        /// Loads the Oracle.DataAccess library in case it couldn't use the default version and the generated proxy classes
        /// </summary>
        /// <remarks>stackoverflow.com/questions/277817/compile-a-version-agnostic-dll-in-net</remarks>
        static ServiceForOracle()
        {
            AppDomain.CurrentDomain.AssemblyResolve += Util.LoadOracleAssembly;
            AppDomain.CurrentDomain.ProcessExit += Util.CloseOracleConnectionPool;    
        }
    }
}