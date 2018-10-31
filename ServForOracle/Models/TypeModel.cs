using Oracle.DataAccess.Client;
using Oracle.DataAccess.Types;
using ServForOracle.Internal;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle.Models
{
    /// <summary>
    /// Implements all the oracle requirements for handling of UDT Types (Objects) 
    /// </summary>
    public abstract class TypeModel : TypeFactory, IOracleCustomType
    {
        /// <summary>
        /// It's used on the creation of the controller for generated types on 
        /// <see cref="ServForOracle.Internal.ProxyFactory.AddConstructor(TypeBuilder)"/>
        /// </summary>
        public TypeModel()
        {

        }

        void ProcessProperties(Action<PropertyInfo, OracleObjectMappingAttribute> process)
        {
            var properties = GetType().GetRuntimeProperties();

            foreach (var prop in properties)
            {
                foreach (var attr in prop.GetCustomAttributes(false))
                {
                    if (attr is OracleObjectMappingAttribute)
                    {
                        process(prop, attr as OracleObjectMappingAttribute);
                    }
                }
            }
        }

        public void FromCustomObject(OracleConnection con, IntPtr pUdt)
        {
            ProcessProperties((prop, attr) =>
            {
                if (prop.PropertyType.IsCollection())
                {
                    if (ProxyFactory.CollectionTypes.TryGetValue(prop.PropertyType, out var type))
                    {
                        dynamic list = type.CreateInstance();
                        list.Array = (dynamic) prop.GetValue(this);
                        OracleUdt.SetValue(con, pUdt, attr.AttributeName, list);
                    }
                    else
                    {
                        throw new Exception($"Can't find the type {prop.PropertyType.FullName}, when trying to"
                            + " map to a collection");
                    }
                }
                else
                {
                    OracleUdt.SetValue(con, pUdt, attr.AttributeName, prop.GetValue(this));
                }
            });
        }

        public void ToCustomObject(OracleConnection con, IntPtr pUdt)
        {
            ProcessProperties((prop, attr) =>
            {
                if (prop.PropertyType.IsCollection())
                {
                    dynamic collection = OracleUdt.GetValue(con, pUdt, attr.AttributeName);
                    prop.SetValue(this, collection.Array);
                }
                else
                {
                    prop.SetValue(this, OracleUdt.GetValue(con, pUdt, attr.AttributeName));
                }
            });
        }
    }
}