using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using MonoMod.RuntimeDetour.HookGen;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using Mono.Cecil;
using Modding;

namespace HKDebug.HotReload
{
    public sealed class ObjectHandler
    {
        public ObjectHandler(object o)
        {
            if (o == null) throw new ArgumentNullException("o");
            Object = new WeakReference(o);
            t = o.GetType();
        }
        public override int GetHashCode()
        {
            return t.GetHashCode();
        }
        public bool IsNull()
        {
            return Object.IsAlive || Object.Target == null;
        }
        public override bool Equals(object obj)
        {
            if (Object == null || !Object.IsAlive) return false;
            if (obj == null) return false;
            if(obj is ObjectHandler handler)
            {
                if (!handler.Object.IsAlive) return false;
                if (ReferenceEquals(handler.Object.Target, Object.Target))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (ReferenceEquals(obj, Object.Target))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        private readonly Type t = null;
        private readonly WeakReference Object = null;
    }
    public class HRObjectCache
    {
        public Dictionary<Type, LinkedList<(ObjectHandler, object)>> caches = new Dictionary<Type, LinkedList<(ObjectHandler, object)>>();
        public object TryGetCache(object src)
        {
            if (src == null) return null;
            Type t = src.GetType();
            if (!caches.TryGetValue(t,out var table))
            {
                table = new LinkedList<(ObjectHandler, object)>();
                caches.Add(t, table);
            }
            return table.FirstOrDefault(x => x.Item1.Equals(src)).Item2;
        }
        public void AddCache(object src,object dst)
        {
            if (src == null || dst == null) return;

            if(!caches.TryGetValue(src.GetType(),out var table))
            {
                table = new LinkedList<(ObjectHandler, object)>();
                caches.Add(src.GetType(), table);
            }
            table.AddFirst((new ObjectHandler(src), dst));
        }
        public void Clean()
        {
            foreach(var v in caches)
            {
                var l = v.Value;
                var f = l.First;
                while (f != null)
                {
                    if (f.Value.Item1.IsNull())
                    {
                        var old = f;
                        f = f.Next;
                        l.Remove(old);
                    }
                    else
                    {
                        f = f.Next;
                    }
                }
            }
        }
    }
    public static class HRLCore
    {
        private static readonly Type ModLoaderType = typeof(IMod).Assembly.GetType("Modding.ModLoader");

        private static readonly MethodInfo ModLoaderAddModInstance =
            ModLoaderType.GetMethod("AddModInstance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo ModLoaderUpdateModText =
            ModLoaderType.GetMethod("UpdateModText", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly Type ModInstance = ModLoaderType.GetNestedType("ModInstance");


        public readonly static Dictionary<Type, Type> TypeCaches = new Dictionary<Type, Type>();
        public readonly static LinkedList<Type> NeedInitStatic = new LinkedList<Type>();
        public readonly static HRObjectCache ObjectCaches = new HRObjectCache();
        public static void CleanObjectCache()
        {
            ObjectCaches.Clean();
        }
        public static object ConvertObject(object src)
        {
            //return null;//TODO
            //logger.Log("Convert Object: " + src?.ToString());
            if (src == null) return null;
            Type st = src.GetType();

            var o = ObjectCaches.TryGetCache(src);

            //return null;
            if (o != null)
            {
                //logger.Log("Use Cache: " + o?.ToString());
                if (TypeCaches.ContainsKey(o.GetType())) return ConvertObject(o);
                return o;
            }
            bool isArray = st.IsArray;
            if (isArray)
            {
                if (TypeCaches.ContainsKey(st.GetElementType()))
                {
                    return ((IEnumerable)src).Cast<object>().Select(x => ConvertObject(x)).ToArray();
                }
                else
                {
                    return st;
                }
            }

            if (TypeCaches.TryGetValue(st, out var type))
            {
                //logger.Log("T/F:" + (st == type).ToString());
                //logger.Log("Create Instance: " + st.FullName);
                //return null;
                if (type.IsEnum)
                {
                    string val = src.ToString();
                    return Enum.Parse(type, val, true);
                }
                if (st.IsSubclassOf(typeof(UnityEngine.Component)))
                {
                    return ComponentHelper.ConvertComponent((UnityEngine.Component)src, type);
                }
                o = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);

                Dictionary<string, object> data = new Dictionary<string, object>();
                foreach (var f in st.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    data[f.Name] = f.GetValue(src);
                }
                data["orig_Object"] = src;
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (data.TryGetValue(f.Name, out var val))
                    {
                        if (!f.CustomAttributes.ShouldIgnore())
                        {
                            val = ConvertObject(val);
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
                }
                //logger.Log("Save Cache");
                ObjectCaches.AddCache(src, o);
                if (TypeCaches.ContainsKey(type))
                {
                    return ConvertObject(o);
                }
                else
                {
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
                            }else if(afterHR.GetParameters().Length == 0)
                            {
                                afterHR.Invoke(o, null);
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.Log(e);
                        }
                    }
                    ObjectCaches.AddCache(src, o);
                    return o;
                }
            }
            else
            {
                //logger.Log("Not Catch");
                return src;
            }

        }
        public readonly static MethodInfo MConvertObject = typeof(HRLCore).GetMethod("ConvertObject");
        public static void ToMethod(ILContext iL, MethodBase target)
        {
            ILCursor cur = new ILCursor(iL);
            logger.Log("Method: " + target.Name);
            if (!target.IsStatic)
            {
                cur.Emit(OpCodes.Ldarg_0);
                cur.Emit(OpCodes.Call, MConvertObject);
            }
            ParameterInfo[] ps = target.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                
                //logger.Log("Push arg[" + i + "]: " + ps[i].Name);
                cur.Emit(OpCodes.Ldarg, i + (target.IsStatic ? 0 : 1));
                
                if (!ps[i].CustomAttributes.ShouldIgnore()) {
                    cur.Emit(OpCodes.Call, MConvertObject);
                }
                if (ps[i].ParameterType.IsByRef) cur.Emit(OpCodes.Mkrefany, ps[i].ParameterType.GetElementType());
            }
            //cur.Emit(OpCodes.Call, MBadMethod);
            if (target.IsStatic)
            {
                cur.Emit(OpCodes.Call, target);
            }
            else
            {
                cur.Emit(OpCodes.Callvirt, target);
            }
            cur.Emit(OpCodes.Ret);
        }
        public static Type TryGetType(Type o)
        {
            if(TypeCaches.TryGetValue(o.IsByRef ? o.GetElementType() : o,out var v))
            {
                if (o.IsByRef)
                {
                    return v.MakeByRefType();
                }
                return v;
            }
            return o;
        }
        [Obsolete]
        public static void HBadMethod() => throw new HRBadMethodException();
        public readonly static MethodInfo MBadMethod = typeof(HRLCore).GetMethod("HBadMethod");
        public static void CType(Type st, Type tt)
        {
            if (st == tt)
            {
                logger.LogError("Bad Type");
                return;
            }
            if (st.IsEnum || tt.IsEnum) return;
            if(st.IsGenericType || tt.IsGenericType)
            {
                logger.LogError("无法处理泛型类型: " + st.FullName);
            }
            /*if (st.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() != null)
            {
                //logger.Log("CompilerGeneratedAttribute: " + st.FullName);
                return;
            }*/
            logger.Log("Load Type: " + st.FullName);
            TypeCaches[st] = tt;
            
            foreach (var v in st.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly
                ))
            {
                try
                {
                    
                    HookEndpointManager.Modify(v, new Action<ILContext>(
                        (il) =>
                        {
                            MethodInfo m = tt.GetMethod(v.Name,
                                BindingFlags.Public | BindingFlags.NonPublic |
                                BindingFlags.Instance | BindingFlags.Static, Type.DefaultBinder,
                                v.GetParameters().Select(x => TryGetType(x.ParameterType)).ToArray(),
                                null);
                            
                            if (m == null)
                            {
                                ILCursor cur = new ILCursor(il);
                                cur.Emit(OpCodes.Call, MBadMethod);
                                cur.Emit(OpCodes.Ret);
                                return;
                            }
                            else if(m.CustomAttributes.Any(x=>x.AttributeType.Name == "EmptyMethodAttribute"))
                            {
                                ILCursor cur = new ILCursor(il);
                                cur.Emit(OpCodes.Ldnull);
                                cur.Emit(OpCodes.Ret);
                                return;
                            }
                            else
                            {
                                if (m.CustomAttributes.ShouldIgnore()) return;
                                ToMethod(il, m);
                            }
                        }));
                }catch(Exception e)
                {
                    logger.LogError(e);
                }
            }

            
        }
        public static Type[] GetTypesForAssembly(Assembly ass)
        {
            List<Type> types = new List<Type>();
            void FindInType(Type p,List<Type> list)
            {
                foreach(var v in p.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Static | BindingFlags.Instance))
                {
                    list.Add(v);
                    FindInType(v, list);
                }
            }
            foreach(var v in ass.GetTypes())
            {
                types.Add(v);
                //FindInType(v, types);
            }
            return types.ToArray();
        }
        public static void InitStatic(Type s,Type d)
        {
            if (s == null || d == null) return;
            if (s.IsGenericType || d.IsGenericType)
            {
                return;
            }
            Dictionary<string, object> data = new Dictionary<string, object>();
            foreach (var f in s.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                data[f.Name] = f.GetValue(null);
            }
            foreach (var f in d.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (f.CustomAttributes.ShouldIgnore()) continue;
                if (data.TryGetValue(f.Name, out var val))
                {
                    data[f.Name] = val = ConvertObject(val);
                    if (f.FieldType.IsValueType && val == null) continue;
                    if (val == null)
                    {
                        f.SetValue(null, null);
                        continue;
                    }
                    if (f.FieldType.IsAssignableFrom(val.GetType()))
                    {
                        f.SetValue(null, val);
                        continue;
                    }
                }
            }
            MethodInfo afterHR = d.GetMethod("OnAfterHotReloadStatic", BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic);
            if (afterHR != null)
            {
                try
                {
                    if (afterHR.GetParameters().Length == 1)
                    {
                        afterHR.Invoke(null, new object[]
                        {
                            data
                        });
                    }
                    else if (afterHR.GetParameters().Length == 0)
                    {
                        afterHR.Invoke(null, null);
                    }
                }
                catch (Exception e)
                {
                    Logger.Log(e);
                }
            }
        }
        public static void CAssembly(Assembly src, Assembly dst)
        {
            List<(Type,Type)> ns = new List<(Type, Type)>();
            Type[] dts = GetTypesForAssembly(dst);
            foreach (var v in GetTypesForAssembly(src)
                .Where(
                x => dts
                .Any(
                    x2 => dts
                    .Any(
                        x3 => x2.FullName == x3.FullName
                        )))
                .Select(
                x => (
                x,
                dts.FirstOrDefault(
                    x2 => x2.FullName == x.FullName
                    )
                )
                )
                )
            {
                try
                {
                    if (v.Item2.IsSubclassOf(typeof(Delegate))) continue;
                    if (v.Item2.IsSubclassOf(typeof(Attribute))) continue;
                    if (v.Item2.CustomAttributes.ShouldIgnore()) continue;
                    ns.Add(v);
                    CType(v.x, v.Item2);
                } catch (Exception e)
                {
                    Logger.LogError(e.ToString());
                }
                foreach(var v2 in ns)
                {
                    try
                    {
                        InitStatic(v2.Item1, v2.Item2);
                    }catch(Exception e)
                    {
                        logger.LogError(e);
                    }
                }
            }
            foreach(var v in dts)
            {
                if (v.IsSubclassOf(typeof(Delegate))) continue;
                if (v.IsSubclassOf(typeof(Attribute))) continue;
                if (v.CustomAttributes.ShouldIgnore()) continue;
                foreach (var v2 in TypeCaches.ToArray().Where(x=>x.Key.FullName == v.FullName))
                {
                    if (ns.Any(x => x.Item1 == v2.Key || x.Item2 == v2.Key)) continue;
                    try
                    {
                        CType(v2.Key, v);
                    }catch(Exception e)
                    {
                        logger.LogError(e);
                    }
                }
            }
        }
        static int hrcount = 0;
        public static Assembly LoadAssembly(string path,byte[] ab)
        {
            if (!File.Exists(path)) return null;
            Assembly ass = Assembly.Load(ab);
            assPath[ass] = path;
            if (hrcaches.TryGetValue(path, out var old))
            {
                hrcaches[path] = ass;
                CAssembly(old, ass);
            }
            else
            {
                hrcaches[path] = ass;
                HotLoadMod(ass, MHotReload.isInit);
            }
            return ass;
        }
        public static string PatchPath
        {
            get
            {
                string p = Path.Combine(UnityEngine.Application.dataPath, "HKDebug", "HotReloadMods");
                Directory.CreateDirectory(p);
                return p;
            }
        }
        internal static Dictionary<string, DateTime> cacheTimes = new Dictionary<string, DateTime>();
        public static Dictionary<string, Assembly> hrcaches = new Dictionary<string, Assembly>();
        public static Dictionary<Assembly, string> assPath = new Dictionary<Assembly, string>();
        public static void HotLoadMod(Assembly ass,bool init = false)
        {
            foreach (var vt in ass.GetTypes().Where(x => x.IsSubclassOf(typeof(Mod)) && !x.IsAbstract))
            {
                try
                {
                    Mod m = (Mod)Activator.CreateInstance(vt);
                    MHotReload.mods.Add(m);
                    if (init)
                    {
                        m.Initialize(null);
                    }
                    /*if (ModInstance != null)
                    {
                        object mi = Activator.CreateInstance(ModInstance);
                        ModInstance.GetField("Mod", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .SetValue(mi, m);
                        ModInstance.GetField("Enable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .SetValue(mi, true);
                        ModInstance.GetField("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .SetValue(mi, m.GetName());
                        ModLoaderAddModInstance.Invoke(null, new object[]
                        {
                        vt, mi
                        });
                        ModLoaderUpdateModText.Invoke(null, null);
                    }*/
                }
                catch (Exception e)
                {
                    logger.LogError(e.ToString());
                }
            }
        }
        public static void RefreshAssembly()
        {
            LoadConfig();
            List<string> s = new List<string>();
            s.AddRange(Directory.GetFiles(PatchPath, "*.dll"));
            s.AddRange(Config.modsPath);
            var ss = s.Where(x =>
            {
                if (!File.Exists(x)) return false;
                DateTime wt = File.GetLastWriteTimeUtc(x);
                if(cacheTimes.TryGetValue(x,out var v2))
                {
                    if (v2 >= wt && !Config.ingoreLastWriteTime)
                    {
                        return false;
                    }
                }
                cacheTimes[x] = wt;
                return true;
            }).ToArray();
            hrcount++;
            List<(string,AssemblyDefinition)> ass = new List<(string, AssemblyDefinition)>();
            Dictionary<string, string> anToP = new Dictionary<string, string>();
            Dictionary<string, Assembly> loaded = new Dictionary<string, Assembly>();
            foreach (var v in ss)
            {
                var a = AssemblyDefinition.ReadAssembly(v);
                ass.Add((v, a));
                string on = a.Name.Name;
                
                ANT[on] = a.Name.Name = a.Name.Name + "." + hrcount;
                anToP.Add(a.Name.Name, v);
            }
            Assembly ILoadAssembly((string, AssemblyDefinition) a)
            {
                if (string.IsNullOrEmpty(a.Item1) && a.Item2 == null) return null;
                if (loaded.TryGetValue(a.Item1,out var ass1))
                {
                    return ass1;
                }
                if (a.Item2 == null) return null;
                foreach (var v2 in a.Item2.MainModule.AssemblyReferences)
                {
                    if (ANT.TryGetValue(v2.Name, out var v3))
                    {
                        v2.Name = v3;
                    }
                }
                using (var st = new MemoryStream())
                {
                    logger.Log("Try load path: " + a.Item1);
                    a.Item2.Write(st);
                    a.Item2.Dispose();
                    try
                    {
                        logger.Log("Try load mod: " + a.Item1);
                        var ass2 = LoadAssembly(a.Item1, st.ToArray());
                        loaded[a.Item1] = ass2;
                        return ass2;
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e.ToString());
                    }
                }
                return null;
            }
            Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
            {
                string name = args.Name.Split(',')[0];
                if(anToP.TryGetValue(name,out var v))
                {
                    return ILoadAssembly(ass.FirstOrDefault(x => x.Item1 == v));
                }
                return null;
            }
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            foreach (var v in ass)
            {
                ILoadAssembly(v);
            }
            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        public static void Init()
        {
            LoadConfig();
        }
        private static Dictionary<string, string> ANT = new Dictionary<string, string>();
        public static bool ShouldIgnore(this IEnumerable<CustomAttributeData> attr) => attr.Any(x => x.AttributeType.Name == "HotReloadIgnoreAttribute");
        public static void LoadConfig() => Config = HKDebug.Config.LoadConfig("HotReload", () => new HotReloadConfig());
        public static HotReloadConfig Config = new HotReloadConfig();
        public static SimpleLogger logger = new SimpleLogger("HKDebug.HotReload");
    }
}
