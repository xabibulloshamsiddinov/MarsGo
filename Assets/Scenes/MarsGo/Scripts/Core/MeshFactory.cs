using System.Collections.Generic;
using UnityEngine;

namespace MarsGo
{
    /// <summary>Procedural meshes for parts Unity has no primitive for (cones, tori, prisms).</summary>
    public static class MeshFactory
    {
        static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Truncated cone along Y, centred on the origin. topRadius 0 = cone.</summary>
        public static Mesh Frustum(float topRadius, float bottomRadius, float height, int segments = 24)
        {
            string key = $"f{topRadius:F3}_{bottomRadius:F3}_{height:F3}_{segments}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            float h = height * 0.5f;
            float slope = (bottomRadius - topRadius) / height;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                var normal = new Vector3(dir.x, slope, dir.z).normalized;
                v.Add(dir * topRadius + Vector3.up * h);
                v.Add(dir * bottomRadius - Vector3.up * h);
                n.Add(normal);
                n.Add(normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                t.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
            AddCap(v, n, t, topRadius, h, Vector3.up, segments);
            AddCap(v, n, t, bottomRadius, -h, Vector3.down, segments);
            return Store(key, v, n, t);
        }

        static void AddCap(List<Vector3> v, List<Vector3> n, List<int> t, float r, float y, Vector3 normal, int segments)
        {
            if (r <= 0.0001f) return;
            int center = v.Count;
            v.Add(new Vector3(0f, y, 0f));
            n.Add(normal);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                v.Add(new Vector3(Mathf.Sin(a) * r, y, Mathf.Cos(a) * r));
                n.Add(normal);
            }
            for (int i = 0; i < segments; i++)
            {
                if (normal.y > 0) t.AddRange(new[] { center, center + 1 + i, center + 2 + i });
                else t.AddRange(new[] { center, center + 2 + i, center + 1 + i });
            }
        }

        /// <summary>Ring in the XZ plane (like Godot's TorusMesh inner/outer radius).</summary>
        public static Mesh Torus(float innerRadius, float outerRadius, int rings = 32, int sides = 12)
        {
            string key = $"t{innerRadius:F3}_{outerRadius:F3}_{rings}_{sides}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            float major = (innerRadius + outerRadius) * 0.5f;
            float minor = (outerRadius - innerRadius) * 0.5f;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float a = i * Mathf.PI * 2f / rings;
                var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2f / sides;
                    var normal = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    v.Add(dir * major + normal * minor);
                    n.Add(normal);
                }
            }
            int stride = sides + 1;
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a = i * stride + j, b = a + stride;
                    t.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
                }
            return Store(key, v, n, t);
        }

        /// <summary>
        /// Fin: right-triangle cross-section in XY (vertical edge at -X, slope down to +X),
        /// extruded along Z. Same as Godot's PrismMesh with left_to_right = 0.
        /// </summary>
        public static Mesh Prism(Vector3 size)
        {
            string key = $"p{size.x:F3}_{size.y:F3}_{size.z:F3}";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            float x = size.x * 0.5f, y = size.y * 0.5f, z = size.z * 0.5f;
            var a = new Vector3(-x, -y, 0f);
            var b = new Vector3(x, -y, 0f);
            var c = new Vector3(-x, y, 0f);
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var t = new List<int>();
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                int s = v.Count;
                var normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                v.AddRange(new[] { p0, p1, p2, p3 });
                n.AddRange(new[] { normal, normal, normal, normal });
                t.AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
            }
            void Tri(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                int s = v.Count;
                var normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
                v.AddRange(new[] { p0, p1, p2 });
                n.AddRange(new[] { normal, normal, normal });
                t.AddRange(new[] { s, s + 1, s + 2 });
            }
            var front = new Vector3(0f, 0f, -z);
            var back = new Vector3(0f, 0f, z);
            Tri(a + front, c + front, b + front);
            Tri(a + back, b + back, c + back);
            Quad(a + front, b + front, b + back, a + back);        // bottom
            Quad(a + front, a + back, c + back, c + front);        // inner (vertical) edge
            Quad(b + front, c + front, c + back, b + back);        // slope
            return Store(key, v, n, t);
        }

        static Mesh Store(string key, List<Vector3> v, List<Vector3> n, List<int> t)
        {
            // Make every triangle face the way its vertex normals point (Unity: clockwise = front).
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 p0 = v[t[i]], p1 = v[t[i + 1]], p2 = v[t[i + 2]];
                var face = Vector3.Cross(p1 - p0, p2 - p0);
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f)
                    (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            // White vertex colours: particle/unlit shaders multiply by them.
            var white = new List<Color>(v.Count);
            for (int i = 0; i < v.Count; i++) white.Add(Color.white);
            mesh.SetColors(white);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            Cache[key] = mesh;
            return mesh;
        }

        /// <summary>Adds a visual-only part (no collider) under a parent.</summary>
        public static GameObject Part(Transform parent, string name, Mesh mesh, Material mat,
                                      Vector3 localPos, Quaternion localRot, Vector3? scale = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return go;
        }

        static Mesh _cube;

        /// <summary>Unity's built-in cube mesh (taken from a primitive once).</summary>
        public static Mesh Cube
        {
            get
            {
                if (_cube != null) return _cube;
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _cube = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
                return _cube;
            }
        }

        static Mesh _sphere;

        public static Mesh Sphere
        {
            get
            {
                if (_sphere != null) return _sphere;
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _sphere = tmp.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(tmp);
                return _sphere;
            }
        }
    }
}
