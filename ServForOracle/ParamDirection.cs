using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServForOracle
{
    /// <summary>
    /// The direction of the parameter as defined in the procedure/function/package, IN/OUT/IN OUT
    /// </summary>
    public enum ParamDirection
    {
        Input,
        Output,
        InputOutput
    }
}
