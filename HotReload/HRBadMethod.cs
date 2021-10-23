using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HKDebug.HotReload
{
    public class HRBadMethodException : Exception
    {
        public HRBadMethodException(): base("Try to call broken method")
        {

        }
    }
}
