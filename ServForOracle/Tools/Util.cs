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
    internal class Util
    {
        /// <summary>
        /// Checks the assembly token specified
        /// </summary>
        /// <param name="currentToken"></param>
        /// <param name="expectedToken"></param>
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

        public static void CloseOracleConnectionPool(object sender, EventArgs e)
        {
            OracleConnection.ClearAllPools();
        }
    }
}
