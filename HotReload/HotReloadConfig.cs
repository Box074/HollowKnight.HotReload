using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HKDebug.HotReload
{
    public class HotReloadConfig
    {
        public List<string> modsPath = new List<string>();
        public bool ingoreLastWriteTime = false;
    }
}
