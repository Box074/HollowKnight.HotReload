using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Modding;
using HotReload;

namespace Test
{
    public class TestMod : Mod
    {
        public override void Initialize()
        {
            ModHooks.AttackHook += ModHooks_AttackHook;
        }
        [HotReloadIgnore]
        private static void OnAfterHotReloadStatic(Dictionary<string,object> o)
        {
            Logger.Log("Hello,World!");
        }
        [HotReloadIgnore]
        private void OnAfterHotReload(Dictionary<string,object> o)
        {
            Logger.Log("HELLO,WORLD1");
            abbbbbbbbb = -1;
        }
        int abbbbbbbbb = 0;
        //[HotReloadIgnore]
        private void ModHooks_AttackHook([HotReloadIgnore]GlobalEnums.AttackDirection obj)
        {
            var v = new System.Diagnostics.StackTrace();
            //Log(v.ToString());
            Log($"Hello,World!15 { GetType().Assembly.GetName().Name } { this.abbbbbbbbb++ }");
            Log(System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
        }
    }
}
