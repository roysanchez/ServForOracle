using Oracle.DataAccess.Client;
using ServForOracle.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Tools
{
    /// <summary>
    /// Generic class to load both the generated assembly from the <see cref="Internal.ProxyFactory"/> and 
    /// the <see cref="Oracle.DataAccess"/>
    /// </summary>
    internal class Util
    {
        /// <summary>
        /// Checks the assembly token specified
        /// </summary>
        /// <param name="currentToken"></param>
        /// <param name="expectedToken"></param>
        /// <exception cref="Exception">If the assembly is not the one that is being trying to be loaded</exception>
        static public void CheckToken(byte[] currentToken, byte[] expectedToken)
        {
            var msj = "The assembly is not signed by Oracle";
            if (currentToken.Length != expectedToken.Length)
                throw new Exception(msj);

            for (int i = 0; i < currentToken.Length; i++)
                if (currentToken[i] != expectedToken[i])
                    throw new Exception(msj);
        }

        const string OracleDataAccess = "Oracle.DataAccess";

        /// <summary>
        /// Loads the assembly from both the <see cref="Oracle.DataAccess"/> and the generated assembly 
        /// from <see cref="ProxyFactory"/>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <returns>The assembly that is looking for</returns>
        /// <remarks>It's called from <see cref="ServiceForOracle"/> class</remarks>
        /// <exception cref="Exception">catches any generated exception when trying to load the <see cref="Oracle.DataAccess"/> 
        /// Assembly
        /// </exception>
        static public Assembly LoadOracleAssembly(object sender, ResolveEventArgs e)
        {
            var requestedName = new AssemblyName(e.Name);

            if (requestedName.Name == ProxyFactory.NAME)
            {
                return ProxyFactory.Assembly;
            }
            else if (requestedName.Name == OracleDataAccess)
            {
                try
                {
                    var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var assemblyName = Assembly.ReflectionOnlyLoadFrom(Path.Combine(path, $"{OracleDataAccess}.dll")).GetName();

                    CheckToken(assemblyName.GetPublicKeyToken(), requestedName.GetPublicKeyToken());

                    return Assembly.Load(assemblyName);
                }
                catch (Exception ex) when (ex is FileNotFoundException || ex is BadImageFormatException)
                {
                    throw new Exception("Couldn't load the Assembly for Oracle DataAccess. See inner exception for details.",
                        ex);
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to close all the <see cref="OracleConnection"/> pools whe the application is shutting down.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <remarks>It is called from the <see cref="ServiceForOracle"/> class.</remarks>
        public static void CloseOracleConnectionPool(object sender, EventArgs e)
        {
            OracleConnection.ClearAllPools();
        }

        /// <summary>
        /// Checks if the <paramref name="udtName"/> has the correct format of "SCHEMA.UDTNAME"
        /// </summary>
        /// <param name="udtName">The name to check</param>
        /// <returns>true if the format is valid otherwise false</returns>
        public static bool CheckUdtName(string udtName)
        {
            var parts = udtName.Split('.');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                return false;
            }

            return true;
        }
    }
}
