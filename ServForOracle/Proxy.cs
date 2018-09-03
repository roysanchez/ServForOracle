using ServForOracle.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    public static class Proxy
    {
        public static void CreateListType<T>(string udtCollectionName)
        {
            CreateListType(typeof(T), udtCollectionName);
        }

        public static void CreateListType(Type underlyingType, string udtCollectionName)
        {
            if(underlyingType == null)
            {
                throw new ArgumentNullException(nameof(underlyingType));
            }
            else if(string.IsNullOrWhiteSpace(udtCollectionName))
            {
                throw new ArgumentNullException(nameof(udtCollectionName));
            }

            ProxyFactory.GetOrCreateProxyCollectionType(underlyingType, udtCollectionName);
        }

        public static void CreateType<T>(string udtName)
        {
            CreateType(typeof(T), udtName);
        }

        public static void CreateType(Type type, string udtName)
        {
            if(type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }
            else if(string.IsNullOrWhiteSpace(udtName))
            {
                throw new ArgumentNullException(nameof(udtName));
            }

            ProxyFactory.GetOrCreateProxyType(type, udtName);
        }
    }
}
