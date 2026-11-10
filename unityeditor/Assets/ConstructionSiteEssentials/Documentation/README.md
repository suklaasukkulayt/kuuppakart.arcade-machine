# Construction Site Essentials — Unity package 1.1.0 candidate

This local successor carries forward the 12 props and materials from v1.0.1 and adds an inspection gallery, optional collider profiles, and generated secondary UV settings. The 1.0.1 product files and original showcase remain represented in this candidate with their existing paths and GUIDs.

## Included

- 12 static construction props with the existing default prefabs, material sets and static non-convex MeshColliders.
- Original CSE_Showcase scene, plus CSE_InspectionGallery with the 12 assets at authored prefab scale and a one-metre reference. RoadSign and PortableWorkLight are rotated only as display instances so their sign face and lamp lenses face the catalogue camera.
- CSE_Detail_SmallEquipment and CSE_Detail_SiteEquipment provide close views of small props and the barrier, wheelbarrow and work light. They use unchanged source prefabs at authored scale.
- Optional primitive compound colliders under Prefabs/CollisionOptions. These are static approximations from source-part bounds; they do not add Rigidbody or interaction behavior.
- Built-In and URP material sets. HDRP materials are not supplied.
- Generated UV2 requested through the included model importer for static-lighting workflows; no lightmap is baked or included.
- Editor tools and documentation for rebuilding, validation, limitations and import settings.

## Import and inspect

Use a clean Unity project. The candidate targets Unity 6000.3.25f1. The included project was configured for Built-In rendering, and the pack also includes URP materials.

Open CSE_InspectionGallery.unity for a true-scale inventory, or use the two CSE_Detail scenes for closer inspection. The original CSE_Showcase scene is preserved. The default prefabs retain their static MeshColliders; select a prefab under Prefabs/CollisionOptions only if its approximate primitive collision is suitable for your scene.

## Known limitations

Each model has one authored geometry detail level. The WorkLight is a static display asset and does not include a Light component. The props have no animation or runtime behavior. The Wheelbarrow collider option uses a solid tray AABB that fills the basin, so it is unsuitable where the open tray must remain collidable. Generated UV2 does not replace a scene-specific lightmap bake or bake inspection. See UPGRADE_NOTES.md and UPGRADE_VALIDATION.md for successor scope and editor results.

