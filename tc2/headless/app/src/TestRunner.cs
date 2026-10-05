using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace TC2.Headless
{
    /// <summary>
    /// A minimal NUnit-compatible runner for the Core tests outside Unity: [TestFixture] classes,
    /// [Test] and [TestCase] methods, [SetUp]/[TearDown], [Category], [Explicit]. Asserts are NUnit's own.
    /// </summary>
    public static class TestRunner
    {
        public static int Run(Assembly asm, string filter, string category, bool includeExplicit, bool verbose)
        {
            var fixtures = asm.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttributes(typeof(TestFixtureAttribute), true).Length > 0)
                .OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
            int pass = 0, fail = 0, skip = 0;
            var failures = new List<string>();
            var total = Stopwatch.StartNew();
            foreach (var fx in fixtures)
            {
                var methods = fx.GetMethods(BindingFlags.Public | BindingFlags.Instance).OrderBy(m => m.MetadataToken).ToList();
                var setup = methods.FirstOrDefault(m => m.GetCustomAttributes(typeof(SetUpAttribute), true).Length > 0);
                var teardown = methods.FirstOrDefault(m => m.GetCustomAttributes(typeof(TearDownAttribute), true).Length > 0);
                string[] fxCats = fx.GetCustomAttributes(typeof(CategoryAttribute), true).Cast<CategoryAttribute>().Select(c => c.Name).ToArray();
                foreach (var m in methods)
                {
                    bool isTest = m.GetCustomAttributes(typeof(TestAttribute), true).Length > 0;
                    var cases = m.GetCustomAttributes(typeof(TestCaseAttribute), true).Cast<TestCaseAttribute>().ToList();
                    if (!isTest && cases.Count == 0) continue;
                    bool isExplicit = m.GetCustomAttributes(typeof(ExplicitAttribute), true).Length > 0;
                    string[] cats = fxCats.Concat(m.GetCustomAttributes(typeof(CategoryAttribute), true).Cast<CategoryAttribute>().Select(c => c.Name)).ToArray();
                    var argSets = cases.Count > 0 ? cases.Select(c => c.Arguments).ToList() : new List<object[]> { null };
                    foreach (var args in argSets)
                    {
                        string name = fx.Name + "." + m.Name + (args != null ? "(" + string.Join(",", args.Select(a => a == null ? "null" : Convert.ToString(a, System.Globalization.CultureInfo.InvariantCulture))) + ")" : "");
                        if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (category != null && !cats.Contains(category, StringComparer.OrdinalIgnoreCase)) continue;
                        if (isExplicit && !includeExplicit && (filter == null || name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)) { skip++; continue; }
                        var sw = Stopwatch.StartNew();
                        object inst = Activator.CreateInstance(fx);
                        try
                        {
                            setup?.Invoke(inst, null);
                            object[] callArgs = args == null ? null : ConvertArgs(m, args);
                            m.Invoke(inst, callArgs);
                            pass++;
                            if (verbose) Console.WriteLine("  ok   " + name + "  (" + sw.ElapsedMilliseconds + " ms)");
                        }
                        catch (TargetInvocationException tie) when (tie.InnerException is IgnoreException || tie.InnerException is InconclusiveException)
                        {
                            skip++;
                            Console.WriteLine("  skip " + name + ": " + tie.InnerException.Message);
                        }
                        catch (TargetInvocationException tie) when (tie.InnerException is SuccessException)
                        {
                            pass++;
                        }
                        catch (Exception ex)
                        {
                            var inner = ex is TargetInvocationException t && t.InnerException != null ? t.InnerException : ex;
                            fail++;
                            string msg = "  FAIL " + name + "  (" + sw.ElapsedMilliseconds + " ms)\n" + Indent(inner.Message) + "\n" + Indent(Trim(inner.StackTrace));
                            failures.Add(msg);
                            Console.WriteLine(msg);
                        }
                        finally
                        {
                            try { teardown?.Invoke(inst, null); } catch (Exception ex) { Console.WriteLine("  teardown error in " + name + ": " + ex.Message); }
                        }
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine("TESTS: " + pass + " passed, " + fail + " failed, " + skip + " skipped  (" + total.ElapsedMilliseconds + " ms)");
            return fail == 0 ? 0 : 1;
        }

        static object[] ConvertArgs(MethodInfo m, object[] args)
        {
            var ps = m.GetParameters();
            var r = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
                r[i] = i < args.Length && args[i] != null && ps[i].ParameterType != args[i].GetType() ? Convert.ChangeType(args[i], ps[i].ParameterType, System.Globalization.CultureInfo.InvariantCulture) : (i < args.Length ? args[i] : null);
            return r;
        }

        static string Indent(string s) => s == null ? "" : "      " + s.Replace("\n", "\n      ");
        static string Trim(string st)
        {
            if (st == null) return "";
            var lines = st.Split('\n').Where(l => !l.Contains("System.Reflection") && !l.Contains("NUnit.Framework")).Take(8);
            return string.Join("\n", lines);
        }
    }
}
