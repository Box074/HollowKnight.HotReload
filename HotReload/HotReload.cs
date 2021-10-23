using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Modding;

namespace HKDebug.HotReload
{
    class MHotReload : Mod
    {
        public static bool isInit = false;
        public static List<Mod> mods = new List<Mod>();
        public MHotReload() : base("HotReload")
        {
            Log("Try to load mods");
            HRLCore.RefreshAssembly();
        }
        public override void Initialize()
        {
            
            foreach(var v in mods)
            {
                isInit = true;
                Log("Initialize mod: " + v);
                try
                {
                    v.Initialize(null);
                }catch(Exception e)
                {
                    v.LogError(e.ToString());
                }
            }
            ModHooks.HeroUpdateHook += ModHooks_HeroUpdateHook;
        }

        private void ModHooks_HeroUpdateHook()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F5))
            {
                HRLCore.RefreshAssembly();
            }
        }

        public override string GetVersion() => "1.0.0";
        
    }
}
