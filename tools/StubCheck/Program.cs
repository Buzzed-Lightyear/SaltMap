// StubCheck <SaltMap.dll built against the stub> <game folder>
// Resolves every type, field and method SaltMap.dll references in "salt" against the
// game's real salt.exe. A field's type and a method's parameter and return types must
// match too (Cecil compares them), because the CLR binds by name and signature.
// Constants cannot be checked this way: they are copied into SaltMap.dll at compile time.
using System;
using System.IO;
using System.Linq;
using Mono.Cecil;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("usage: StubCheck <SaltMap.dll> <game folder>");
            return 2;
        }
        string mod = args[0], game = args[1];

        // Resolve "salt" and MonoGame from the game folder, not from next to the mod.
        DefaultAssemblyResolver resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(game);
        foreach (string d in resolver.GetSearchDirectories().Where(d => d != game).ToArray())
            resolver.RemoveSearchDirectory(d);
        resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));

        ModuleDefinition module = ModuleDefinition.ReadModule(mod, new ReaderParameters { AssemblyResolver = resolver });
        int checkedCount = 0, failed = 0;

        foreach (TypeReference t in module.GetTypeReferences().Where(t => Scope(t) == "salt"))
        {
            checkedCount++;
            if (Try(() => t.Resolve()) == null) { failed++; Console.WriteLine("MISSING TYPE   " + t.FullName); }
        }
        foreach (MemberReference m in module.GetMemberReferences().Where(m => Scope(m.DeclaringType) == "salt"))
        {
            checkedCount++;
            IMemberDefinition found = m is FieldReference f ? (IMemberDefinition)Try(() => f.Resolve())
                : m is MethodReference mr ? Try(() => mr.Resolve()) : null;
            if (found == null) { failed++; Console.WriteLine("MISSING MEMBER " + m.FullName); }
        }
        Console.WriteLine(checkedCount + " references into salt checked, " + failed + " missing.");

        // Constants were copied into the mod at compile time, so compare the stub's own
        // constants with the game's instead.
        int constants = 0, wrong = 0;
        string stub = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(mod)), "salt.dll");
        if (File.Exists(stub))
        {
            ModuleDefinition stubModule = ModuleDefinition.ReadModule(stub);
            ModuleDefinition gameModule = ModuleDefinition.ReadModule(Path.Combine(game, "salt.exe"));
            foreach (TypeDefinition st in stubModule.GetTypes())
            {
                foreach (FieldDefinition sf in st.Fields.Where(x => x.HasConstant))
                {
                    constants++;
                    TypeDefinition gt = gameModule.GetTypes().FirstOrDefault(x => x.FullName == st.FullName);
                    FieldDefinition gf = gt == null ? null : gt.Fields.FirstOrDefault(x => x.Name == sf.Name);
                    if (gf == null || !gf.HasConstant || !Equals(gf.Constant, sf.Constant))
                    {
                        wrong++;
                        Console.WriteLine("WRONG CONSTANT " + st.FullName + "." + sf.Name + " = " + sf.Constant
                            + " (game: " + (gf == null ? "missing" : gf.HasConstant ? gf.Constant : "not constant") + ")");
                    }
                }
            }
            Console.WriteLine(constants + " constants checked, " + wrong + " wrong.");
        }
        else Console.WriteLine("No salt.dll next to the mod; constants not checked.");
        return failed == 0 && wrong == 0 ? 0 : 1;
    }

    static string Scope(TypeReference t)
    {
        while (t != null && t.DeclaringType != null) t = t.DeclaringType;
        return t == null ? null : t.Scope.Name.Replace(".exe", "").Replace(".dll", "");
    }

    static T Try<T>(Func<T> f) where T : class
    {
        try { return f(); } catch (Exception) { return null; }
    }
}
