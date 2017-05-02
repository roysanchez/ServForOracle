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

namespace ServForOracle
{
    public abstract class OracleBaseService
    {
        public OracleBaseService(IConnectionStringProvider connectionString)
        {
            ConnectionString = connectionString.Value;
        }

        private string ConnectionString { get; }

        private async Task<OracleCommand> CreateCommandAsync(string proc, CommandType type)
        {
            if (String.IsNullOrEmpty(proc))
                throw new ArgumentNullException(nameof(proc));

            OracleConnection con = new OracleConnection(ConnectionString);

            await con.OpenAsync();
            var cmd = con.CreateCommand();
            cmd.CommandType = type;
            cmd.CommandText = proc.Replace("\r\n", "\n");
            
            cmd.Disposed += (object sender, EventArgs e) =>
            {
                con.Close();
            };

            return cmd;
        }

        protected async Task ExecuteQueryAsync(string query)
        {
            using (var cmd = await CreateCommandAsync(query, CommandType.Text))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Executes an Oracle Function
        /// </summary>
        /// <typeparam name="T">The espected result of the oracle function</typeparam>
        /// <param name="function">The function to execute, which follows this format: schema.function or schema.package.function</param>
        /// <param name="parameters">The parameters of the function  whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        /// <returns>A task indicating the result of the execution and containing the return value if it is successful</returns>
        protected async Task<T> ExecuteFunctionAsync<T>(string function, params Param[] parameters)
        {
            using (var cmd = await CreateCommandAsync(function, CommandType.StoredProcedure))
            {
                var ret = ParamHandler.CreateReturnParam<T>();
                cmd.Parameters.Add(ret);
                
                await ExecuteInnerAsync(cmd, parameters);

                return ParamHandler.ConvertOracleParameterToBaseType<T>(ret);
            }
        }

        /// <summary>
        /// Executes an Oracle procedure
        /// </summary>
        /// <param name="procedure">The procedure to run, with the format schema.procedure or schema.package.procedure</param>
        /// <param name="parameters">The parameters for the procedure, whether IN, OUT or IN OUT</param>
        /// <remarks>The OUT and IN OUT parameters get their values set by this method</remarks>
        /// <returns>A task indicating the result of the execution</returns>
        protected async Task ExecuteProcedureAsync(string procedure, params Param[] parameters)
        {
            using (var cmd = await CreateCommandAsync(procedure, CommandType.StoredProcedure))
            {
                await ExecuteInnerAsync(cmd, parameters);
            }
        }

        /// <summary>
        /// Calls the Oracle Executer for the procedure or function specified in the command 
        /// </summary>
        /// <param name="cmd">The OracleCommand that specifies the query to execute</param>
        /// <param name="parameters">The parameters to send</param>
        /// <returns>A Task indicating the status of the execution</returns>
        private async Task ExecuteInnerAsync(OracleCommand cmd, Param[] parameters)
        {
            var outParameters = new List<OutParam>();
            foreach (var param in parameters)
            {
                var oracleParam = ParamHandler.CreateParam(param.Type, param.Value, param.ParamDirection);
                cmd.Parameters.Add(oracleParam);

                if (oracleParam.Direction == ParameterDirection.Output || oracleParam.Direction == ParameterDirection.InputOutput)
                    outParameters.Add(new OutParam(param, oracleParam));
            }
            
            await cmd.ExecuteNonQueryAsync();
            
            foreach (var param in outParameters)
                param.SetParamValue();
        }
    }
}
