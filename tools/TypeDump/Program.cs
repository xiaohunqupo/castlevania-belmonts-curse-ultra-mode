using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

// Usage: TypeDump <interopDir> <assemblyName> <typeRegex> [--members] [--out file]
// Lists interop types whose full name matches typeRegex; --members also dumps fields/properties/methods.
var dir = args[0];
var asmName = args[1];
var regex = new Regex(args[2], RegexOptions.IgnoreCase);
var members = args.Contains("--members");
var outIdx = Array.IndexOf(args, "--out");
TextWriter output = outIdx >= 0 ? new StreamWriter(args[outIdx + 1]) : Console.Out;

var paths = Directory.GetFiles(dir, "*.dll")
    .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
    .Concat(Directory.GetFiles(Path.Combine(dir, "..", "core"), "*.dll"))
    .GroupBy(Path.GetFileName).Select(g => g.First());
using var ctx = new MetadataLoadContext(new PathAssemblyResolver(paths));
var asm = ctx.LoadFromAssemblyPath(Path.Combine(dir, asmName));

Type[] types;
try { types = asm.GetTypes(); }
catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }

const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
string N(Type? t) { try { return t is null ? "?" : t.IsGenericType ? $"{t.Name}<{string.Join(",", t.GetGenericArguments().Select(N))}>" : t.Name; } catch { return "?"; } }

foreach (var t in types.Where(t => regex.IsMatch(t.FullName ?? t.Name)).OrderBy(t => t.FullName))
{
    string baseName; try { baseName = N(t.BaseType); } catch { baseName = "?"; }
    output.WriteLine($"{(t.IsEnum ? "enum" : t.IsInterface ? "interface" : "class")} {t.FullName} : {baseName}");
    if (!members) continue;
    try
    {
        if (t.IsEnum)
        {
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                output.WriteLine($"    {f.Name} = {f.GetRawConstantValue()}");
            continue;
        }
        foreach (var f in t.GetFields(flags).Where(f => !f.Name.StartsWith("NativeFieldInfoPtr") && !f.Name.StartsWith("NativeMethodInfoPtr")))
            output.WriteLine($"    field {(f.IsStatic ? "static " : "")}{N(f.FieldType)} {f.Name}");
        foreach (var p in t.GetProperties(flags))
            output.WriteLine($"    prop {N(p.PropertyType)} {p.Name}");
        foreach (var m in t.GetMethods(flags).Where(m => !m.IsSpecialName && !m.Name.StartsWith("_") ))
        {
            string ps; try { ps = string.Join(", ", m.GetParameters().Select(p => $"{N(p.ParameterType)} {p.Name}")); } catch { ps = "?"; }
            output.WriteLine($"    {(m.IsStatic ? "static " : "")}{N(m.ReturnType)} {m.Name}({ps})");
        }
    }
    catch (Exception e) { output.WriteLine($"    !! {e.GetType().Name}: {e.Message}"); }
}
output.Flush();
