using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Determinism lint (D2-057): lists every arithmetic operation in TC2.Core whose type is float (System.Single). Mono - the
// Unity editor's runtime - may carry float arithmetic out at double precision and keep float locals in double registers,
// where CoreCLR (headless) and IL2CPP (the Web build) round every step to single: the same code then lives a different world.
// The rule: floats are for storage; arithmetic is done in double and cast to float when stored.
// Usage: dotnet floatcheck.dll <core dir> [--list]   (exit 1 when any operation is found)
static class FloatCheck
{
    static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "unity/TinyConsequences2/Assets/TC2/Core";
        var files = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        var opts = new CSharpParseOptions(LanguageVersion.CSharp9);
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), opts, f)).ToList();
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
        var refs = tpa.Where(p => p.EndsWith(".dll")).Select(p => MetadataReference.CreateFromFile(p)).ToList();
        var comp = CSharpCompilation.Create("check", trees, refs, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var errors = comp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            foreach (var e in errors.Take(10)) Console.WriteLine("compile: " + e);
            return 2;
        }
        int found = 0;
        var byFile = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var tree in trees)
        {
            var model = comp.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot().DescendantNodes())
            {
                ITypeSymbol t = null;
                string what = null;
                if (node is BinaryExpressionSyntax be && IsArithmetic(be.Kind()))
                {
                    t = model.GetTypeInfo(be).Type; what = be.ToString();
                }
                else if (node is AssignmentExpressionSyntax ae && !ae.IsKind(SyntaxKind.SimpleAssignmentExpression))
                {
                    // a compound assignment computes in the target's type: float op= float is float arithmetic
                    var lt = model.GetTypeInfo(ae.Left).Type; var rt = model.GetTypeInfo(ae.Right).ConvertedType;
                    if (lt?.SpecialType == SpecialType.System_Single && model.GetTypeInfo(ae.Right).Type?.SpecialType != SpecialType.System_Double) { t = lt; what = ae.ToString(); }
                }
                else if (node is PrefixUnaryExpressionSyntax pu && (pu.IsKind(SyntaxKind.PreIncrementExpression) || pu.IsKind(SyntaxKind.PreDecrementExpression)))
                {
                    t = model.GetTypeInfo(pu).Type; what = pu.ToString();
                }
                else if (node is PostfixUnaryExpressionSyntax po && (po.IsKind(SyntaxKind.PostIncrementExpression) || po.IsKind(SyntaxKind.PostDecrementExpression)))
                {
                    t = model.GetTypeInfo(po).Type; what = po.ToString();
                }
                if (t == null || t.SpecialType != SpecialType.System_Single) continue;
                found++;
                var span = node.GetLocation().GetLineSpan();
                string rel = Path.GetRelativePath(dir, span.Path).Replace('\\', '/');
                byFile[rel] = (byFile.TryGetValue(rel, out int c) ? c : 0) + 1;
                if (args.Contains("--list")) Console.WriteLine(rel + ":" + (span.StartLinePosition.Line + 1) + "  " + Squash(what));
            }
        }
        foreach (var kv in byFile) Console.WriteLine(kv.Value.ToString().PadLeft(5) + "  " + kv.Key);
        Console.WriteLine(found == 0 ? "no float arithmetic in " + files.Length + " files" : found + " float operations in " + byFile.Count + " files");
        return found == 0 ? 0 : 1;
    }

    static bool IsArithmetic(SyntaxKind k) => k == SyntaxKind.AddExpression || k == SyntaxKind.SubtractExpression || k == SyntaxKind.MultiplyExpression ||
                                               k == SyntaxKind.DivideExpression || k == SyntaxKind.ModuloExpression;

    static string Squash(string s) { s = string.Join(" ", s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries)); return s.Length > 110 ? s.Substring(0, 110) + "..." : s; }
}
