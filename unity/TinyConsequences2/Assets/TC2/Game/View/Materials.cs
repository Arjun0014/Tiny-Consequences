using UnityEngine;

namespace TC2.Game
{
    /// <summary>Loads the project's material assets (Resources/TC2Materials, made by ProjectSetup) as runtime copies.</summary>
    public static class Materials
    {
        public static Material Load(string name)
        {
            var m = Resources.Load<Material>("TC2Materials/" + name);
            if (m != null) return new Material(m);
            Debug.LogWarning("[TC2] material asset missing: " + name + "; building one from its shader (instancing variants may be stripped)");
            return new Material(Resources.Load<Shader>("TC2Shaders/" + name));
        }
    }
}
