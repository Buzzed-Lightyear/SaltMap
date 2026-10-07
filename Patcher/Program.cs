// SaltPatcher: adds one guarded call to the start of ProjectTower.Game1.Initialize
// in salt.exe that loads Mods\SaltMap.dll and runs SaltMap.Entry.Init().
//
//   SaltPatcher <game folder>             patch (keeps the original as salt.exe.orig)
//   SaltPatcher <game folder> --status    report whether salt.exe is patched
//   SaltPatcher <game folder> --restore   put salt.exe.orig back
//   SaltPatcher <game folder> --force     patch even if earlier hand-made edits are found
//
// Everything the mod does lives in the mod DLL. This tool only has to run again
// when salt.exe itself is replaced, for example by a game update.
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class Program
{
    const string GameExe = "salt.exe";
    const string BackupExe = "salt.exe.orig";
    const string TargetType = "ProjectTower.Game1";
    const string TargetMethod = "Initialize";
    const string ModFolder = "Mods";
    const string ModDll = "SaltMap.dll";
    const string EntryType = "SaltMap.Entry";     // also the marker that says "already patched"
    const string EntryMethod = "Init";
    const string LoaderErrorLog = "SaltMap-loader-error.log";

    // Strings left in salt.exe by the patches made by hand in dnSpy during the first sessions.
    static readonly string[] HandPatchMarkers = { "ssmap.udp", "pos.json", "salt_pos.log" };

    static int Main(string[] args)
    {
        string dir = args.FirstOrDefault(a => !a.StartsWith("--"));
        if (dir == null)
        {
            Console.Error.WriteLine("usage: SaltPatcher <game folder> [--status | --restore | --force]");
            return 2;
        }
        string exe = Path.Combine(dir, GameExe);
        string backup = Path.Combine(dir, BackupExe);
        if (!File.Exists(exe))
        {
            Console.Error.WriteLine("Not found: " + exe);
            return 2;
        }
        try
        {
            if (args.Contains("--restore")) return Restore(exe, backup);
            if (args.Contains("--status")) return Status(exe);
            return Patch(exe, backup, args.Contains("--force"));
        }
        catch (IOException e)
        {
            Console.Error.WriteLine("File error (is the game running?): " + e.Message);
            return 1;
        }
    }

    static int Status(string exe)
    {
        using (AssemblyDefinition asm = Read(exe))
        {
            MethodDefinition init = FindTarget(asm);
            if (init == null) return TargetMissing();
            Console.WriteLine(HasString(init, EntryType) ? "patched" : "not patched");
            return 0;
        }
    }

    static int Restore(string exe, string backup)
    {
        if (!File.Exists(backup))
        {
            Console.Error.WriteLine("No backup at " + backup + ". Use Steam's \"Verify integrity of game files\" instead.");
            return 1;
        }
        File.Copy(backup, exe, true);
        Console.WriteLine("Restored " + exe + " from the backup.");
        return 0;
    }

    static int Patch(string exe, string backup, bool force)
    {
        string tmp = exe + ".patching";
        using (AssemblyDefinition asm = Read(exe))
        {
            MethodDefinition init = FindTarget(asm);
            if (init == null) return TargetMissing();
            if (HasString(init, EntryType))
            {
                Console.WriteLine("Already patched; nothing to do.");
                return 0;
            }

            string leftover = init.DeclaringType.Methods
                .Where(m => m.HasBody)
                .SelectMany(m => m.Body.Instructions)
                .Where(i => i.OpCode == OpCodes.Ldstr)
                .Select(i => (string)i.Operand)
                .FirstOrDefault(s => HandPatchMarkers.Any(s.Contains));
            if (leftover != null && !force)
            {
                Console.Error.WriteLine("salt.exe still contains an earlier hand-made patch (found \"" + leftover + "\").");
                Console.Error.WriteLine("Restore the original first (your salt.exe.bak, or Steam's \"Verify integrity of");
                Console.Error.WriteLine("game files\"), then run this again. Use --force to patch it as it is.");
                return 1;
            }

            // salt.exe is unpatched at this point, so it is the copy worth keeping.
            // This also refreshes the backup after a game update.
            File.Copy(exe, backup, true);

            Inject(asm.MainModule, init);
            asm.Write(tmp);
        }
        File.Copy(tmp, exe, true);
        File.Delete(tmp);
        Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(exe)), ModFolder));
        Console.WriteLine("Patched " + exe);
        Console.WriteLine("Previous salt.exe kept as " + backup);
        return 0;
    }

    static AssemblyDefinition Read(string exe)
    {
        // Read from memory so the file is not held open while it is replaced.
        return AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(exe)));
    }

    static MethodDefinition FindTarget(AssemblyDefinition asm)
    {
        TypeDefinition type = asm.MainModule.GetType(TargetType);
        return type == null ? null
            : type.Methods.FirstOrDefault(m => m.Name == TargetMethod && m.Parameters.Count == 0 && m.HasBody);
    }

    static int TargetMissing()
    {
        Console.Error.WriteLine("Could not find " + TargetType + "." + TargetMethod + "() in salt.exe.");
        Console.Error.WriteLine("The game's code has changed; the patcher needs a new hook point.");
        return 1;
    }

    static bool HasString(MethodDefinition method, string text)
    {
        return method.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == text);
    }

    // Adds the equivalent of this to the top of the method:
    //
    //   try {
    //       Assembly.LoadFrom(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Mods", "SaltMap.dll"))
    //           .GetType("SaltMap.Entry").GetMethod("Init").Invoke(null, null);
    //   } catch (Exception ex) {
    //       try { File.WriteAllText(Path.Combine(<base dir>, "SaltMap-loader-error.log"), ex.ToString()); }
    //       catch (Exception) { }
    //   }
    //
    // so a missing or broken mod can never stop the game from starting.
    static void Inject(ModuleDefinition module, MethodDefinition method)
    {
        // References are built against the game's own core library, not the one
        // this tool happens to run on.
        IMetadataScope corlib = module.TypeSystem.CoreLibrary;
        TypeReference tString = module.TypeSystem.String;
        TypeReference tObject = module.TypeSystem.Object;
        TypeReference tVoid = module.TypeSystem.Void;
        TypeReference tAppDomain = new TypeReference("System", "AppDomain", module, corlib);
        TypeReference tException = new TypeReference("System", "Exception", module, corlib);
        TypeReference tType = new TypeReference("System", "Type", module, corlib);
        TypeReference tAssembly = new TypeReference("System.Reflection", "Assembly", module, corlib);
        TypeReference tMethodInfo = new TypeReference("System.Reflection", "MethodInfo", module, corlib);
        TypeReference tMethodBase = new TypeReference("System.Reflection", "MethodBase", module, corlib);
        TypeReference tPath = new TypeReference("System.IO", "Path", module, corlib);
        TypeReference tFile = new TypeReference("System.IO", "File", module, corlib);

        MethodReference getDomain = Ref(tAppDomain, "get_CurrentDomain", tAppDomain, true);
        MethodReference getBaseDir = Ref(tAppDomain, "get_BaseDirectory", tString, false);
        MethodReference combine2 = Ref(tPath, "Combine", tString, true, tString, tString);
        MethodReference combine3 = Ref(tPath, "Combine", tString, true, tString, tString, tString);
        MethodReference loadFrom = Ref(tAssembly, "LoadFrom", tAssembly, true, tString);
        MethodReference getType = Ref(tAssembly, "GetType", tType, false, tString);
        MethodReference getMethod = Ref(tType, "GetMethod", tMethodInfo, false, tString);
        MethodReference invoke = Ref(tMethodBase, "Invoke", tObject, false, tObject, new ArrayType(tObject));
        MethodReference toString = Ref(tObject, "ToString", tString, false);
        MethodReference writeAllText = Ref(tFile, "WriteAllText", tVoid, true, tString, tString);

        MethodBody body = method.Body;
        ILProcessor il = body.GetILProcessor();
        Instruction original = body.Instructions[0];
        VariableDefinition ex = new VariableDefinition(tException);
        body.Variables.Add(ex);
        body.InitLocals = true;

        Instruction[] load =
        {
            il.Create(OpCodes.Call, getDomain),
            il.Create(OpCodes.Callvirt, getBaseDir),
            il.Create(OpCodes.Ldstr, ModFolder),
            il.Create(OpCodes.Ldstr, ModDll),
            il.Create(OpCodes.Call, combine3),
            il.Create(OpCodes.Call, loadFrom),
            il.Create(OpCodes.Ldstr, EntryType),
            il.Create(OpCodes.Callvirt, getType),
            il.Create(OpCodes.Ldstr, EntryMethod),
            il.Create(OpCodes.Callvirt, getMethod),
            il.Create(OpCodes.Ldnull),
            il.Create(OpCodes.Ldnull),
            il.Create(OpCodes.Callvirt, invoke),
            il.Create(OpCodes.Pop),
            il.Create(OpCodes.Leave, original),
        };
        Instruction[] report =
        {
            il.Create(OpCodes.Stloc, ex),
            il.Create(OpCodes.Call, getDomain),
            il.Create(OpCodes.Callvirt, getBaseDir),
            il.Create(OpCodes.Ldstr, LoaderErrorLog),
            il.Create(OpCodes.Call, combine2),
            il.Create(OpCodes.Ldloc, ex),
            il.Create(OpCodes.Callvirt, toString),
            il.Create(OpCodes.Call, writeAllText),
            il.Create(OpCodes.Leave, original),
        };
        Instruction[] swallow =
        {
            il.Create(OpCodes.Pop),
            il.Create(OpCodes.Leave, original),
        };
        foreach (Instruction i in load.Concat(report).Concat(swallow))
            il.InsertBefore(original, i);

        // Inner handlers must be listed before the ones that enclose them.
        body.ExceptionHandlers.Insert(0, new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            CatchType = tException,
            TryStart = report[1], TryEnd = swallow[0],
            HandlerStart = swallow[0], HandlerEnd = original,
        });
        body.ExceptionHandlers.Insert(1, new ExceptionHandler(ExceptionHandlerType.Catch)
        {
            CatchType = tException,
            TryStart = load[0], TryEnd = report[0],
            HandlerStart = report[0], HandlerEnd = original,
        });
    }

    static MethodReference Ref(TypeReference declaringType, string name, TypeReference returnType,
        bool isStatic, params TypeReference[] parameters)
    {
        MethodReference m = new MethodReference(name, returnType, declaringType) { HasThis = !isStatic };
        foreach (TypeReference p in parameters) m.Parameters.Add(new ParameterDefinition(p));
        return m;
    }
}
