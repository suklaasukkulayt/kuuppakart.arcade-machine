# Construction Site Essentials 1.1.0 — Unity validation

- Unity Editor: 6000.3.25f1
- Render pipeline: Built-In
- Checked at UTC: 2026-10-16T08:55:16.1222652Z

| Asset | Triangles | UV2 vertices | UV2 result | Default collider |
|---|---:|---:|---|---|
| TrafficCone | 1548 | 2263 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| SafetyBarrier | 2172 | 2812 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| HardHat | 4032 | 4626 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| Wheelbarrow | 4688 | 6807 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| Shovel | 592 | 979 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| CementBag | 3898 | 4456 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| Toolbox | 4164 | 4987 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| RoadSign | 1542 | 2053 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| PortableWorkLight | 5156 | 6904 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| BrickStack | 3384 | 4991 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| RebarBundle | 6480 | 11148 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |
| CautionSign | 3912 | 5547 | PASS: finite, in-range, vertex-aligned | Static MeshCollider retained |

- Built-In WorkLight lens emission: PASS; keyword=True; emission map=True; GI flags=BakedEmissive

UV2 presence/range is a data check only; it does not prove that a lightmap bake is artifact-free. The original 1.0.1 prefabs and their static MeshColliders remain unchanged. Primitive collision variants are optional approximations.
