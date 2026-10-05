using System.IO;
using UnityEditor;
using UnityEngine;

namespace TC2.EditorTools
{
    /// <summary>Batch helpers: -executeMethod TC2.EditorTools.BatchTools.HashTest [-tc2Out FILE] (runs Core under the editor's Mono).</summary>
    public static class BatchTools
    {
        public static void HashTest()
        {
            string probe = TC2.Core.Diagnostics.CrossRuntime.Probe();
            string outFile = null;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == "-tc2Out") outFile = a[i + 1];
            if (outFile != null) File.WriteAllText(outFile, probe);
            Debug.Log("[TC2] hashtest " + probe);
        }
    }
}
