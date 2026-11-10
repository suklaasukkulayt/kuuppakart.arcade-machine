# Construction Site Essentials 1.1.0 candidate

This isolated successor is based on the exact 1.0.1 package project and retains the original 12 prop prefabs and CSE_Showcase scene. It is still the free product; this local candidate does not change the listing or public release.

## Additions

- Demo/CSE_InspectionGallery.unity lays all 12 props out at the prefab's authored scale and includes a one-metre reference. RoadSign and PortableWorkLight are rotated for visibility only in the display scene.
- Demo/CSE_Detail_SmallEquipment.unity and Demo/CSE_Detail_SiteEquipment.unity show close true-scale groups without changing the product prefabs.
- Prefabs/CollisionOptions/ contains optional compound primitive collider prefabs. They use source-part bounds, with a capsule for the cone, and remain static. They contain no Rigidbody or interaction behavior. They are approximate alternatives; the original static non-convex MeshCollider prefabs are retained as the default. The Wheelbarrow's tray box is a solid AABB that fills the basin.
- FBX import settings request generated secondary UVs with an eight-pixel pack margin for lightmap workflows. This adds UV2; it does not mean lighting has been baked.
- The Built-In WorkLight lens material restores the original `_EMISSION` keyword and uses the baked-emissive GI flag so Unity does not suppress its emission as black; the asset still has no Light component.
- Editor/CSEUpgradeTools.cs can rebuild the additive gallery, detail views and collision options, validate the imported meshes and capture the Unity cameras.

## Scope and compatibility

The 12 source meshes, default prefab hierarchy, original GUIDs, material maps, default colliders, and original showcase are carried forward. The candidate is configured for Unity 6000.3.25f1 and Built-In rendering, matching its included Built-In materials. URP materials are retained. HDRP materials are not supplied. The pack still has one geometry detail level per prop and no animation or runtime scripts.

## Validation state

Native validation in Unity 6000.3.25f1 with Built-In rendering completed with zero check errors. It verified all 12 imported UV2 arrays as finite, in-range and vertex-aligned, retained the original default MeshColliders, and confirmed the Built-In WorkLight emission keyword, map and GI flag. The three inspection cameras were captured through Unity `Camera.Render`. UV2 data checks do not validate a baked lightmap. Exact archive hash, clean-import result and independent review are tracked in the dated candidate release receipt. This is a local candidate; the free public v1.0.1 remains unchanged.

