using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace HKDebug.HotReload
{
    class ComponentHelper
    {
        public static Component ConvertComponent(Component src,Type type)
        {
            if (src == null) return null;
            GameObject go = src.gameObject;
            Type st = src.GetType();
            

            Dictionary<string, object> data = new Dictionary<string, object>();
            foreach (var f in st.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                data.Add(f.Name, HRLCore.ConvertObject(f.GetValue(src)));
            }
            //UnityEngine.Object.Destroy(src);
            Component o = go.AddComponent(type);
            foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (data.TryGetValue(f.Name, out var val))
                {
                    if (f.FieldType.IsValueType && val == null) continue;
                    if (val == null)
                    {
                        f.SetValue(o, null);
                        continue;
                    }
                    if (f.FieldType.IsAssignableFrom(val.GetType()))
                    {
                        f.SetValue(o, val);
                        continue;
                    }
                }
            }
            if (HRLCore.TypeCaches.ContainsKey(type))
            {
                return (Component)HRLCore.ConvertObject(o);
            }
            else
            {
                data["orig_Object"] = src;
                MethodInfo afterHR = type.GetMethod("OnAfterHotReload", BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                if (afterHR != null)
                {
                    try
                    {
                        if (afterHR.GetParameters().Length == 1)
                        {
                            afterHR.Invoke(o, new object[]
                            {
                            data
                            });
                        }
                        else if (afterHR.GetParameters().Length == 0)
                        {
                            afterHR.Invoke(o, null);
                        }
                    }
                    catch (Exception e)
                    {
                        HRLCore.logger.Log(e);
                    }
                }
                HRLCore.ObjectCaches.AddCache(src, o);
                return o;
            }
        }
    }
}
