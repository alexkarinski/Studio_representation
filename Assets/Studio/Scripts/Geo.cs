using UnityEngine;

namespace Studio
{
    /// <summary>Хелперы для процедурной геометрии (примитивы без коллайдеров, меш занавеса).</summary>
    public static class Geo
    {
        public static GameObject Group(string name, Transform parent, Vector3 localPos, float rotY = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, rotY, 0);
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
            => Prim(PrimitiveType.Cube, name, parent, pos, size, mat, Quaternion.identity);

        public static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 euler)
            => Prim(PrimitiveType.Cube, name, parent, pos, size, mat, Quaternion.Euler(euler));

        /// <summary>Цилиндр вдоль локальной оси Y (height — полная высота).</summary>
        public static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat)
            => Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(radius * 2, height * 0.5f, radius * 2), mat, Quaternion.identity);

        public static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat, Vector3 euler)
            => Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(radius * 2, height * 0.5f, radius * 2), mat, Quaternion.Euler(euler));

        public static GameObject Sphere(string name, Transform parent, Vector3 pos, float radius, Material mat)
            => Prim(PrimitiveType.Sphere, name, parent, pos, Vector3.one * radius * 2, mat, Quaternion.identity);

        /// <summary>Kapsel zwischen zwei Punkten (Gliedmaßen der Personen).</summary>
        public static GameObject Limb(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var dir = b - a;
            float len = dir.magnitude;
            return Prim(PrimitiveType.Capsule, name, parent, (a + b) * 0.5f,
                new Vector3(radius * 2, (len + radius * 2) * 0.5f, radius * 2), mat,
                len > 1e-4f ? Quaternion.FromToRotation(Vector3.up, dir / len) : Quaternion.identity);
        }

        public static GameObject Quad(string name, Transform parent, Vector3 pos, Vector2 size, Material mat, Vector3 euler)
            => Prim(PrimitiveType.Quad, name, parent, pos, new Vector3(size.x, size.y, 1), mat, Quaternion.Euler(euler));

        /// <summary>Труба между двумя точками (в локальных координатах parent).</summary>
        public static GameObject Rod(string name, Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var dir = b - a;
            var go = Prim(PrimitiveType.Cylinder, name, parent, (a + b) * 0.5f,
                new Vector3(radius * 2, dir.magnitude * 0.5f, radius * 2), mat,
                Quaternion.FromToRotation(Vector3.up, dir.normalized));
            return go;
        }

        /// <summary>
        /// Волнистый меш занавеса: ширина по локальной X, высота по Y, складки по Z.
        /// Лицевая сторона смотрит в +Z.
        /// </summary>
        public static GameObject Curtain(string name, Transform parent, Vector3 pos, float rotY, float width, float height, Material mat,
            float foldWidth = 0.32f, float depth = 0.09f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, rotY, 0);

            int seg = Mathf.Max(8, Mathf.CeilToInt(width / foldWidth * 8));
            var verts = new Vector3[(seg + 1) * 2 * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[seg * 12];
            int vi = 0;
            for (int side = 0; side < 2; side++)
            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                float x = -width / 2 + t * width;
                float z = Mathf.Sin(t * width / foldWidth * Mathf.PI * 2) * depth;
                verts[vi] = new Vector3(x, 0, z);
                uvs[vi++] = new Vector2(t, 0);
                verts[vi] = new Vector3(x, height, z);
                uvs[vi++] = new Vector2(t, 1);
            }
            int ti = 0, back = (seg + 1) * 2;
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                // front (+Z)
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                tris[ti++] = c; tris[ti++] = b; tris[ti++] = d;
                // back
                tris[ti++] = back + a; tris[ti++] = back + c; tris[ti++] = back + b;
                tris[ti++] = back + c; tris[ti++] = back + d; tris[ti++] = back + b;
            }
            var mesh = new Mesh { name = name + "Mesh", vertices = verts, uv = uvs, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>Добавляет к корню объекта BoxCollider по габаритам всех рендереров (для выбора мышью).</summary>
        public static BoxCollider FitCollider(GameObject root)
        {
            var rends = root.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return null;
            var t = root.transform;
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            foreach (var r in rends)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z);
                    var local = t.InverseTransformPoint(corner);
                    min = Vector3.Min(min, local);
                    max = Vector3.Max(max, local);
                }
            }
            var col = root.GetComponent<BoxCollider>();
            if (col == null) col = root.AddComponent<BoxCollider>();
            col.center = (min + max) * 0.5f;
            col.size = max - min;
            return col;
        }
    }
}
