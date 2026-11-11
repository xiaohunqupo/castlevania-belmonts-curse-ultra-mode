using System.Collections;
using System.Reflection;
using System.Text;
using BepInEx;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using MotherBase.ActionFramework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UltraMode;

/// <summary>Dev tool: reflects over interop wrappers (resolving the real IL2CPP subtype) and writes game data to text files.</summary>
internal static class Inspector
{
    private static readonly Dictionary<string, Type?> typeCache = new();
    private static readonly string[] skipProps = { "Pointer", "ObjectClass", "WasCollected", "isWrapped", "pooledPtr", "hideFlags" };

    internal static string Dir => Path.Combine(Paths.BepInExRootPath, "ultra_mode_dump");

    internal static void DumpAll()
    {
        Directory.CreateDirectory(Dir);
        Write("mc_actions.txt", DumpSeenActions);
        Write("action_index.txt", DumpActionIndex);
        Write("vania_moves.txt", DumpVaniaMoves);
        Write("parry_candidates.txt", sb =>
        {
            foreach (var data in Features.AttackParry.Candidates())
                sb.AppendLine($"{data.name} seenByPlayer={ActionTracker.SeenData.ContainsKey(data.name)}");
        });
        Write("mirror_actions.txt", sb =>
        {
            foreach (var data in FindAll<ActionData>().Where(d => d.name.Contains("Mirror")).OrderBy(d => d.name))
            {
                sb.AppendLine($"==== {data.name}");
                DumpValue(sb, data, 0, 4, new HashSet<IntPtr>(), true);
            }
        });
        Write("mirrors.txt", sb =>
        {
            var player = Player.Get();
            foreach (var m in FindAll<GPE_Mirror>().Where(m => m.gameObject.scene.IsValid()).OrderBy(m => TransformPath(m.transform)))
            {
                string usable;
                try { usable = player == null ? "?" : m.CanUseMirror(player).ToString(); } catch (Exception e) { usable = $"!{e.GetType().Name}"; }
                var key = m.useMirrorCondition?.TryCast<EntityConditionCheckProgressKey>()?.progressionKeyToCheck?.name;
                sb.AppendLine($"==== {TransformPath(m.transform)} active={m.gameObject.activeInHierarchy} type={m.mirrorType} teleport={m.IsATeleportMirror} canUse={usable} key={key ?? "-"}");
                DumpValue(sb, m.useMirrorCondition, 1, 4, new HashSet<IntPtr>(), true);
            }
        });
        Write("feedbacks.txt", sb =>
        {
            foreach (var f in FindAll<MotherBase.FeedbackManager.Feedback>().OrderBy(f => f.name))
                sb.AppendLine($"{f.name}\tlifetime={(f.hasLifetime ? f.lifetimeInFrame.ToString() : "loop")}");
        });
        Write("charms.txt", DumpCharms);
        Write("spells.txt", DumpSpells);
        Plugin.Logger.LogMessage($"Ultra Mode dump written to {Dir}");
    }

    private static string TransformPath(Transform t) => t.parent == null ? $"{t.gameObject.scene.name}:{t.name}" : $"{TransformPath(t.parent)}/{t.name}";

    private static void Write(string file, Action<StringBuilder> body)
    {
        var sb = new StringBuilder();
        try { body(sb); }
        catch (Exception e) { sb.AppendLine($"!! {e}"); }
        File.WriteAllText(Path.Combine(Dir, file), sb.ToString());
    }

    private static List<T> FindAll<T>() where T : Object
    {
        var result = new List<T>();
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>()))
        {
            var t = o.TryCast<T>();
            if (t != null) result.Add(t);
        }
        return result;
    }

    private static void DumpSeenActions(StringBuilder sb)
    {
        sb.AppendLine("# Actions started by the player this session (name x count)");
        foreach (var (name, count) in ActionTracker.Seen.OrderBy(kv => kv.Key))
            sb.AppendLine($"{name} x{count}");
        sb.AppendLine();
        foreach (var (name, data) in ActionTracker.SeenData.OrderBy(kv => kv.Key))
        {
            sb.AppendLine($"==== {name}");
            DumpValue(sb, data, 0, 4, new HashSet<IntPtr>(), true);
            sb.AppendLine();
        }
    }

    private static void DumpActionIndex(StringBuilder sb)
    {
        foreach (var a in FindAll<ActionData>().OrderBy(a => a.name))
            sb.AppendLine($"{a.name}\t{a.GetIl2CppType().Name}\tend={a.endFrame}\tchain={a.chainableFrame}\ttimeScale={a.animTimeScale}\tscale={a.scaleMode}");
    }

    private static void DumpCharms(StringBuilder sb)
    {
        var mc = Player.Get();
        var inv = mc != null ? Player.Inventory(mc) : null;
        var owned = new HashSet<IntPtr>();
        if (inv != null)
        {
            var items = inv.GetAllItemsInInventory();
            for (var i = 0; i < items.Count; i++) if (items[i] != null) owned.Add(items[i].Pointer);
        }
        foreach (var charm in FindAll<CharmItemData>().OrderBy(c => c.name))
        {
            sb.AppendLine($"==== {charm.name}  id={charm.itemID} owned={owned.Contains(charm.Pointer)}");
            Line(sb, "  notes", () => charm.notes);
            Line(sb, "  effect", () => charm.charmEffect == null ? "null" : charm.charmEffect.GetIl2CppType().Name);
            var effect = Safe(() => charm.charmEffect?.TryCast<CharmEffectStatModifier>());
            if (effect != null) Bindings(sb, "    effect.stat", Safe(() => effect.statsModifiers));
            var affect = Safe(() => charm.charmAffect);
            Line(sb, "  affect", () => affect == null ? "null" : affect.name);
            var subs = affect != null ? Safe(() => affect.subAssets) : null;
            if (subs != null)
                for (var i = 0; i < subs.Count; i++)
                {
                    var sub = subs[i];
                    if (sub == null) continue;
                    Line(sb, $"    affect[{i}]", () => $"{sub.GetIl2CppType().Name} '{sub.name}'");
                    var statFeature = Safe(() => sub.TryCast<StatModifierAffectFeature>());
                    if (statFeature != null) Bindings(sb, "      apply", Safe(() => statFeature.statsModifiersOnApply));
                }
            var removes = Safe(() => charm.affectsToRemoveOnUnequip);
            if (removes != null)
                for (var i = 0; i < removes.Count; i++) Line(sb, "  removeOnUnequip", () => removes[i]?.name ?? "null");
            sb.AppendLine();
        }
    }

    private static void DumpSpells(StringBuilder sb)
    {
        var mc = Player.Get();
        var inv = mc != null ? Player.Inventory(mc) : null;
        var owned = new HashSet<IntPtr>();
        if (inv != null)
        {
            var items = inv.GetAllItemsInInventory();
            for (var i = 0; i < items.Count; i++) if (items[i] != null) owned.Add(items[i].Pointer);
        }
        var equipped = Safe(() => inv?.GetEquippedSpell());
        foreach (var spell in FindAll<SpellItemData>().OrderBy(s => s.name))
        {
            sb.AppendLine($"==== {spell.name}  id={spell.itemID} owned={owned.Contains(spell.Pointer)} equipped={equipped != null && equipped.Pointer == spell.Pointer}");
            DumpValue(sb, spell, 1, 3, new HashSet<IntPtr>(), true);
            sb.AppendLine();
        }
        sb.AppendLine("#### UseItem triggers");
        foreach (var t in FindAll<UseItem>().OrderBy(t => t.name))
            Line(sb, t.name, () => $"itemType={t.itemType} noItem={t.noItemEquippedAction?.name ?? "-"}");
        sb.AppendLine("\n#### Purification actions and triggers");
        foreach (var o in FindAll<ScriptableObject>().Where(o => o.name.Contains("Purification", StringComparison.OrdinalIgnoreCase)).OrderBy(o => o.name))
        {
            sb.AppendLine($"==== {o.GetIl2CppType().Name} {o.name}");
            DumpValue(sb, o, 1, 3, new HashSet<IntPtr>(), true);
        }
        var input = mc != null ? Player.Comp<PlayerInput>(mc) : null;
        sb.AppendLine("\n#### Player input");
        if (input != null) DumpValue(sb, input.data, 1, 4, new HashSet<IntPtr>(), true);
    }

    private static T? Safe<T>(Func<T?> get) where T : class
    {
        try { return get(); } catch { return null; }
    }

    private static void Line(StringBuilder sb, string label, Func<string?> get)
    {
        string? v;
        try { v = get(); } catch (Exception e) { v = $"!{e.GetType().Name}"; }
        sb.AppendLine($"{label}: {v}");
    }

    private static void Bindings(StringBuilder sb, string label, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<StatAndModifierBinding>? mods)
    {
        if (mods == null) return;
        for (var i = 0; i < mods.Length; i++)
        {
            var b = mods[i];
            Line(sb, label, () =>
            {
                var stat = b.targetStat == null ? "?" : b.targetStat.GetIl2CppType().Name;
                var mod = b.modifier;
                var op = mod?.TryCast<OperationBruteValueStatModifier>();
                var modText = op != null ? $"{op.operation} {op.value}" : mod == null ? "null" : ((Il2CppSystem.Object)mod.Cast<Il2CppSystem.Object>()).GetIl2CppType().Name;
                return $"{stat} {modText}";
            });
        }
    }

    private static void DumpVaniaMoves(StringBuilder sb)
    {
        var mc = Player.Get();
        var moves = mc != null ? Player.Comp<VaniaMovesComponent>(mc) : null;
        sb.AppendLine("# Unlocked");
        if (moves != null)
            foreach (var m in moves.unlockedVaniaMoves) sb.AppendLine(m.name);
        sb.AppendLine("\n# All VaniaMoveItemData");
        foreach (var item in FindAll<VaniaMoveItemData>().OrderBy(i => i.name))
            sb.AppendLine($"{item.name}  id={item.itemID}");
    }

    /// <summary>
    /// Interop hands out wrappers of the declared type (e.g. ActionFeature for every feature in a list). This
    /// re-wraps the pointer as the object's real IL2CPP type, so reflection sees the subtype's properties.
    /// </summary>
    internal static object Resolve(object obj)
    {
        if (obj is not Il2CppSystem.Object il2) return obj;
        string name;
        try { name = il2.GetIl2CppType().FullName; }
        catch { return obj; }
        if (!typeCache.TryGetValue(name, out var t))
        {
            t = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => { try { return a.GetType(name) ?? a.GetType("Il2Cpp" + name); } catch { return null; } })
                .FirstOrDefault(x => x != null);
            typeCache[name] = t;
        }
        if (t == null || t == obj.GetType() || !obj.GetType().IsAssignableFrom(t)) return obj;
        try { return Activator.CreateInstance(t, il2.Pointer)!; }
        catch { return obj; }
    }

    private static bool IsLeafAsset(Object o, bool top)
    {
        if (top) return false;
        return o is ActionTrigger or ItemData or Component or GameObject or AnimGraphAsset or Texture or Sprite or AnimationClip or Material or Mesh
               || o.GetIl2CppType().Name.Contains("Feedback");
    }

    private static void DumpValue(StringBuilder sb, object? value, int indent, int depth, HashSet<IntPtr> visited, bool top = false)
    {
        var pad = new string(' ', indent * 2);
        if (value is not Il2CppObjectBase baseObj) return;
        if (!top && depth <= 0) return;
        if (baseObj is not Il2CppSystem.ValueType && !visited.Add(baseObj.Pointer)) { sb.AppendLine($"{pad}(cycle)"); return; }

        var obj = Resolve(baseObj);
        var type = obj.GetType();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && !skipProps.Contains(p.Name)
                        && p.DeclaringType is { } d && !(d.Assembly.GetName().Name ?? "").StartsWith("UnityEngine")
                        && !(d.Assembly.GetName().Name ?? "").StartsWith("Il2CppInterop")
                        && d != typeof(Il2CppSystem.Object) && d != typeof(Il2CppSystem.ValueType));

        sb.AppendLine($"{pad}[{type.Name}]");
        foreach (var p in props)
        {
            object? v;
            try { v = p.GetValue(obj); }
            catch (Exception e) { sb.AppendLine($"{pad}  {p.Name} = !{(e.InnerException ?? e).GetType().Name}"); continue; }
            WriteMember(sb, p.Name, v, indent + 1, depth - 1, visited);
        }
    }

    private static void WriteMember(StringBuilder sb, string name, object? v, int indent, int depth, HashSet<IntPtr> visited)
    {
        var pad = new string(' ', indent * 2);
        switch (v)
        {
            case null:
                sb.AppendLine($"{pad}{name} = null");
                return;
            case string or bool or int or long or float or double or uint or short or byte or Enum:
            case Vector2 or Vector3 or Vector4 or Color or Quaternion or LayerMask:
                sb.AppendLine($"{pad}{name} = {v}");
                return;
            case Il2CppSystem.Delegate or Il2CppSystem.Type:
                return;
            case AnimationCurve curve:
                sb.AppendLine($"{pad}{name} = AnimationCurve({curve.length} keys)");
                return;
            case Object uo:
                if (uo == null) { sb.AppendLine($"{pad}{name} = null(destroyed)"); return; }
                sb.AppendLine($"{pad}{name} = <{uo.GetIl2CppType().Name} '{uo.name}'>");
                if (!IsLeafAsset(uo, false) && uo is ScriptableObject) DumpValue(sb, uo, indent + 1, depth, visited);
                return;
        }

        if (TryEnumerate(v, out var items, out var count))
        {
            sb.AppendLine($"{pad}{name} = [{count}]");
            var i = 0;
            foreach (var item in items)
            {
                if (i >= 40) { sb.AppendLine($"{pad}  ..."); break; }
                WriteMember(sb, $"[{i++}]", item, indent + 1, depth, visited);
            }
            return;
        }

        if (v is Il2CppObjectBase)
        {
            sb.AppendLine($"{pad}{name} =");
            DumpValue(sb, v, indent + 1, depth, visited);
            return;
        }
        sb.AppendLine($"{pad}{name} = {v}");
    }

    private static bool TryEnumerate(object v, out IEnumerable<object?> items, out int count)
    {
        items = Array.Empty<object?>();
        count = 0;
        var type = v.GetType();
        if (type.Name.StartsWith("Dictionary") || type.Name.StartsWith("HashSet")) return false;
        var countProp = type.GetProperty("Count") ?? type.GetProperty("Length");
        var indexer = type.GetProperties().FirstOrDefault(p => p.GetIndexParameters() is { Length: 1 } ps && ps[0].ParameterType == typeof(int));
        if (countProp == null || indexer == null || countProp.PropertyType != typeof(int)) return false;
        var n = (int)countProp.GetValue(v)!;
        count = n;
        items = Enumerable.Range(0, Math.Min(n, 41)).Select(i => { try { return indexer.GetValue(v, new object[] { i }); } catch { return null; } });
        return true;
    }
}
