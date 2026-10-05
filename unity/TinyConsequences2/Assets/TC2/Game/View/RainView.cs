using TC2.Core.Foundation;
using UnityEngine;
using UnityEngine.Rendering;

namespace TC2.Game
{
    /// <summary>
    /// Rain and snow round the camera (D2-033): a cylinder of falling streaks that follows the camera close to the
    /// ground, as strong as the day's rain where the camera is (a storm's included), flakes when it is freezing, leaning
    /// with the storm's wind. Reads state only. Gone above a few hundred metres, where the clouds show the weather.
    /// </summary>
    public sealed class RainView : MonoBehaviour
    {
        public SimHost Host;
        public CameraRig Rig;
        GameObject _go;
        Material _mat;
        float _shown, _sunBase = -1f;
        static readonly int RainId = Shader.PropertyToID("_Rain");

        void Start()
        {
            _mat = Materials.Load("Rain");
            _go = new GameObject("TC2 Rain");
            _go.transform.SetParent(transform, false);
            _go.AddComponent<MeshFilter>().sharedMesh = Cylinder(28);
            var mr = _go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            _go.SetActive(false);
        }

        static Mesh Cylinder(int seg)
        {
            var v = new Vector3[(seg + 1) * 2]; var uv = new Vector2[v.Length]; var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                var p = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 2] = p + Vector3.down; v[i * 2 + 1] = p + Vector3.up;
                uv[i * 2] = new Vector2(i / (float)seg * 6f, 0); uv[i * 2 + 1] = new Vector2(i / (float)seg * 6f, 1);
            }
            for (int i = 0; i < seg; i++)
            {
                int b = i * 2;
                tri[i * 6] = b; tri[i * 6 + 1] = b + 1; tri[i * 6 + 2] = b + 2;
                tri[i * 6 + 3] = b + 1; tri[i * 6 + 4] = b + 3; tri[i * 6 + 5] = b + 2;
            }
            var m = new Mesh { name = "tc2-rain", vertices = v, uv = uv, triangles = tri };
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 1e4f);
            return m;
        }

        void LateUpdate()
        {
            var w = Host != null ? Host.World : null;
            if (w == null || Rig == null || Rig.Cam == null || _go == null) return;
            var P = w.Planet;
            Vector3 cam = Rig.Cam.transform.position;
            float alt = cam.magnitude - (float)P.Radius;
            int c = P.Grid.CellOf(new Vec3d(cam.x, cam.y, cam.z).Normalized);
            float rain = Mathf.Clamp01(P.RainToday[c] / 1.4f);
            float wind = 0f;
            for (int k = 0; k < P.Storms.Count; k++)
            {
                var st = P.Storms[k];
                double r = Vec3d.ArcDistance(st.At, new Vec3d(cam.x, cam.y, cam.z).Normalized, P.Radius);
                wind = Mathf.Max(wind, (float)TC2.Core.Planet.Storms.WindAt(st, r));
            }
            if (wind > 15f) rain = Mathf.Max(rain, Mathf.Clamp01((wind - 15f) / 25f));
            float target = alt < 700f ? rain * (1f - Mathf.InverseLerp(300f, 700f, alt)) : 0f;
            _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * 0.5f);   // weather comes and goes softly
            bool on = _shown > 0.01f;
            if (_go.activeSelf != on) _go.SetActive(on);
            // under heavy rain the day is overcast
            if (Rig.Sun != null)
            {
                if (_sunBase < 0f) _sunBase = Rig.Sun.intensity;
                Rig.Sun.intensity = _sunBase * (1f - 0.5f * _shown);
            }
            if (!on) return;
            Vector3 up = cam.normalized;
            _go.transform.position = cam;
            _go.transform.rotation = Quaternion.FromToRotation(Vector3.up, up);
            // wide enough that streaks fall through the distance in focus (tilt-shift blurs what hugs the camera)
            float radius = Mathf.Clamp(alt * 1.3f, 40f, 260f);
            _go.transform.localScale = new Vector3(radius, radius * 0.9f, radius);
            float snow = P.Temp[c] < 0.5f ? 1f : 0f;
            _mat.SetVector(RainId, new Vector4(_shown, snow, Time.unscaledTime, Mathf.Clamp(wind / 40f, 0f, 1.2f)));
        }
    }
}
