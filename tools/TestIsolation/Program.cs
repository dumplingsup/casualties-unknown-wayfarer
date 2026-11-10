using Mono.Cecil;
using Mono.Cecil.Cil;

// Only instrument disposable copies. The distributable DLL and installed game remain unchanged.
bool verifyOnly = args.Length == 3 && args[2] == "--verify";
if (args.Length != 2 && !verifyOnly) throw new Exception("Usage: TestIsolation <test-root> <data-dir>");
var root = Path.GetFullPath(args[0]);
var data = Path.GetFullPath(args[1]);
if (!root.Contains(Path.Combine(".local", "cu-test")) || !data.StartsWith(Path.GetDirectoryName(root) + Path.DirectorySeparatorChar))
    throw new Exception("Requires workspace .local/cu-test sandbox");
Directory.CreateDirectory(data);
var targets = new[] { "CasualtiesUnknown_Data/Managed/Assembly-CSharp.dll", "BepInEx/plugins/CUCoreLib.dll" };
foreach (var relative in targets)
{
    var path = Path.Combine(root, relative);
    var resolver = new DefaultAssemblyResolver();
    resolver.AddSearchDirectory(Path.Combine(root, "CasualtiesUnknown_Data/Managed"));
    resolver.AddSearchDirectory(Path.Combine(root, "BepInEx/core"));
    using var module = ModuleDefinition.ReadModule(path, new ReaderParameters { ReadWrite = false, AssemblyResolver = resolver });
    int paths = 0, prefs = 0, achievements = 0;
    int redirected = 0;
    IEnumerable<TypeDefinition> Walk(IEnumerable<TypeDefinition> types) {
        foreach (var t in types) { yield return t; foreach (var n in Walk(t.NestedTypes)) yield return n; }
    }
    foreach (var t in Walk(module.Types)) foreach (var m in t.Methods)
    {
        if (!m.HasBody) continue;
        if (verifyOnly)
        {
            foreach (var ins in m.Body.Instructions)
            {
                if (ins.OpCode == OpCodes.Ldstr && (string)ins.Operand == data) redirected++;
                if (ins.Operand is MethodReference mr)
                {
                    if (mr.DeclaringType.FullName == "UnityEngine.Application" && mr.Name == "get_persistentDataPath") paths++;
                    if (mr.DeclaringType.FullName == "UnityEngine.PlayerPrefs" &&
                        new[] { "SetInt", "SetFloat", "SetString", "DeleteKey", "DeleteAll", "Save" }.Contains(mr.Name)) prefs++;
                }
            }
            if (t.Name == "SteamManager" && m.Name == "TryUnlockAchievement" &&
                m.Body.Instructions.Any(i => i.OpCode != OpCodes.Nop && i.OpCode != OpCodes.Ret)) achievements++;
            continue;
        }
        if (t.Name == "SteamManager" && m.Name == "TryUnlockAchievement")
        { m.Body.Instructions.Clear(); m.Body.ExceptionHandlers.Clear(); m.Body.Variables.Clear(); m.Body.Instructions.Add(Instruction.Create(OpCodes.Ret)); achievements++; continue; }
        foreach (var ins in m.Body.Instructions.ToArray())
        {
            if (ins.Operand is not MethodReference method) continue;
            if (method.DeclaringType.FullName == "UnityEngine.Application" && method.Name == "get_persistentDataPath")
            { ins.OpCode = OpCodes.Ldstr; ins.Operand = data; paths++; }
            if (method.DeclaringType.FullName == "UnityEngine.PlayerPrefs" &&
                new[] { "SetInt", "SetFloat", "SetString", "DeleteKey", "DeleteAll", "Save" }.Contains(method.Name))
            {
                var il = m.Body.GetILProcessor();
                foreach (var parameter in method.Parameters) il.InsertBefore(ins, Instruction.Create(OpCodes.Pop));
                ins.OpCode = OpCodes.Nop; ins.Operand = null; prefs++;
            }
        }
    }
    if (verifyOnly)
    {
        Console.WriteLine($"{relative}: redirected={redirected}, unsafePaths={paths}, unsafePrefs={prefs}, unsafeAchievements={achievements}");
        if (redirected == 0 || paths != 0 || prefs != 0 || achievements != 0)
            throw new Exception("Isolation verification failed");
        continue;
    }
    var output = path + ".isolated"; module.Write(output);
    module.Dispose();
    File.Move(output, path, true);
    Console.WriteLine($"{relative}: redirectedPaths={paths}, blockedPrefsWrites={prefs}, disabledAchievements={achievements}");
    if (paths == 0) throw new Exception("No persistentDataPath calls found; isolation not established");
}
