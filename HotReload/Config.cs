using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace HKDebug
{
    public static class Config
    {
        public static T LoadConfig<T>(string name, Func<T> notfound) where T : class, new()
        {
            T con;
            string cp = Path.Combine(UnityEngine.Application.dataPath, "HKDebug", "Config", name + ".json");
            if (!File.Exists(cp))
            {
                if (notfound != null)
                {
                    con = notfound();
                }
                else
                {
                    con = new T();
                }
                File.WriteAllText(cp, JsonConvert.SerializeObject(con, Formatting.Indented));
                return con;
            }
            con = JsonConvert.DeserializeObject<T>(File.ReadAllText(cp));
            return con;
        }
    }
}
