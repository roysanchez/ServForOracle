﻿using Oracle.DataAccess.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;

namespace ServForOracle.Models
{
    /// <summary>
    /// Implements all the oracle requirements for handling of UDT Collection Types (Lists/Varrays) 
    /// </summary>
    /// <typeparam name="T">The type that has <see cref="OracleCustomTypeMappingAttribute"/> and 
    /// implements the <see cref="TypeModel"/>
    /// </typeparam>
    public abstract class CollectionModel<T> : TypeFactory, IOracleCustomType, IOracleArrayTypeFactory
    {
        /// <summary>
        /// It's used on the creation of the controller for generated types on 
        /// <see cref="ServForOracle.Internal.ProxyFactory.AddConstructor(System.Reflection.Emit.TypeBuilder)"/>
        /// </summary>
        public CollectionModel()
        {

        }

        [OracleArrayMapping]
        public T[] Array { get; set; }

        private OracleUdtStatus[] m_statusArray;

        public Array CreateArray(int numElems)
        {
            return new T[numElems];
        }

        public Array CreateStatusArray(int numElems)
        {
            return new OracleUdtStatus[numElems];
        }

        public void FromCustomObject(OracleConnection con, IntPtr pUdt)
        {
            OracleUdt.SetValue(con, pUdt, 0, Array, m_statusArray);
        }

        public void ToCustomObject(OracleConnection con, IntPtr pUdt)
        {
            Array = (T[])OracleUdt.GetValue(con, pUdt, 0, out object objectStatusArray);
            m_statusArray = (OracleUdtStatus[])objectStatusArray;
        }
    }
}