"""Minimal environment kit for the Chronos remake (Blender, headless) -> kit.fbx + preview.

    blender -b --factory-startup --python tools/blender/build_kit.py -- <out_dir>

One clean language: soft-bevelled slabs, no surface clutter. Function is carried by colour in Unity.
Objects (Unity units = metres, tile pitch 2 m, tile top at y=0, pivots at their logical centre):
  Tile, TileEdge (thin rim used for device-controlled tiles), PlateRing, PlatePad, Wall (2 m bar),
  WallPost, LeverBase, LeverHandle (pivot at hinge), LeverArrow, GoalRing, GoalDisc, StartDisc.
"""
import bpy, math, os, sys
from mathutils import Vector, Euler

OUT = os.path.abspath(sys.argv[sys.argv.index('--') + 1])
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def mat(name, rgb):
    m = bpy.data.materials.new(name); m.diffuse_color = (*rgb, 1); return m


M = {n: mat(n, c) for n, c in [('Stone', (0.78, 0.75, 0.70)), ('StoneSide', (0.55, 0.52, 0.50)), ('Rim', (0.6, 0.5, 1.0)),
                               ('Dark', (0.12, 0.13, 0.17)), ('Pad', (0.6, 0.5, 1.0)), ('Wall', (0.36, 0.39, 0.47)),
                               ('Accent', (1.0, 0.7, 0.3)), ('Glow', (1.0, 0.9, 0.6))]}


def finish(ob, name, mats, loc):
    ob.name = name; ob.data.name = name
    ob.data.materials.clear()
    for m in mats: ob.data.materials.append(M[m])
    for p in ob.data.polygons: p.use_smooth = True
    ob.location = loc
    return ob


def bevel_box(name, size, bevel, segs, mats, loc=(0, 0, 0), z_from_top=True):
    bpy.ops.mesh.primitive_cube_add(size=1)
    ob = bpy.context.active_object
    ob.scale = size; bpy.ops.object.transform_apply(scale=True)
    md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = segs; md.limit_method = 'NONE'
    bpy.ops.object.modifier_apply(modifier='b')
    wn = ob.modifiers.new('wn', 'WEIGHTED_NORMAL'); wn.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier='wn')
    if z_from_top:  # put the pivot on the top face centre
        for v in ob.data.vertices: v.co.z -= size[2] / 2
    return finish(ob, name, mats, loc)


def cyl(name, r, h, mats, loc, verts=48, bevel=0.0, z_from_top=True):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h)
    ob = bpy.context.active_object
    if bevel:
        md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = 3; md.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier='b')
    if z_from_top:
        for v in ob.data.vertices: v.co.z -= h / 2
    finish(ob, name, mats, loc)
    for p in ob.data.polygons: p.use_smooth = abs(p.normal.z) < 0.5   # flat caps, smooth sides
    return ob


def torus(name, R, r, mats, loc, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=64, minor_segments=12)
    ob = bpy.context.active_object
    ob.rotation_euler = Euler([math.radians(a) for a in rot]); bpy.ops.object.transform_apply(rotation=True)
    return finish(ob, name, mats, loc)


x = 0.0
def slot():
    global x; x += 3.0; return (x, 0, 0)

# Blender Z-up: "top at z=0" here becomes "top at y=0" in Unity.
bevel_box('Tile', (1.9, 1.9, 0.55), 0.09, 4, ['Stone'], slot())
# rim: a thin frame inset on the top face (device-controlled tiles get this in the device colour)
bpy.ops.mesh.primitive_cube_add(size=1); outer = bpy.context.active_object
outer.scale = (1.74, 1.74, 0.03); bpy.ops.object.transform_apply(scale=True)
bpy.ops.mesh.primitive_cube_add(size=1); inner = bpy.context.active_object
inner.scale = (1.56, 1.56, 0.2); bpy.ops.object.transform_apply(scale=True)
bo = outer.modifiers.new('cut', 'BOOLEAN'); bo.object = inner; bo.operation = 'DIFFERENCE'
bpy.context.view_layer.objects.active = outer; bpy.ops.object.modifier_apply(modifier='cut')
bpy.data.objects.remove(inner)
for v in outer.data.vertices: v.co.z += 0.012
finish(outer, 'TileEdge', ['Rim'], slot())
cyl('PlateRing', 0.62, 0.05, ['Dark'], slot(), bevel=0.02)
for v in bpy.data.objects['PlateRing'].data.vertices: v.co.z += 0.03
cyl('PlatePad', 0.48, 0.1, ['Pad'], slot(), bevel=0.03)
for v in bpy.data.objects['PlatePad'].data.vertices: v.co.z += 0.1
# wall: low rounded bar spanning one tile edge (2 m), visual height 0.7 (collider stays 2 m tall in Unity)
bevel_box('Wall', (2.0, 0.34, 0.7), 0.12, 4, ['Wall'], slot())
for v in bpy.data.objects['Wall'].data.vertices: v.co.z += 0.7
cyl('WallPost', 0.2, 0.74, ['Wall'], slot(), verts=24, bevel=0.06)
for v in bpy.data.objects['WallPost'].data.vertices: v.co.z += 0.74
cyl('LeverBase', 0.5, 0.18, ['Wall'], slot(), bevel=0.05)
for v in bpy.data.objects['LeverBase'].data.vertices: v.co.z += 0.18
# handle: pivot at the hinge, pointing up (+Z) — Unity tilts it toward the push direction
bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=0.06, depth=0.8, location=(0, 0, 0.4))
h = bpy.context.active_object; bpy.ops.object.transform_apply(location=True)
bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.14, location=(0, 0, 0.82))
k = bpy.context.active_object; bpy.ops.object.transform_apply(location=True)
bpy.ops.object.select_all(action='DESELECT'); h.select_set(True); k.select_set(True)
bpy.context.view_layer.objects.active = h; bpy.ops.object.join()
finish(h, 'LeverHandle', ['Accent'], slot())
# arrow decal on the floor beside the lever: points along +Y (Unity +Z) = push direction
bpy.ops.mesh.primitive_plane_add(size=1); a = bpy.context.active_object
bm_verts = [(-0.18, -0.25, 0), (0.18, -0.25, 0), (0.18, 0.05, 0), (0.34, 0.05, 0), (0, 0.38, 0), (-0.34, 0.05, 0), (-0.18, 0.05, 0)]
me = bpy.data.meshes.new('LeverArrow'); me.from_pydata(bm_verts, [], [list(range(7))]); me.update()
arrow = bpy.data.objects.new('LeverArrow', me); scene.collection.objects.link(arrow)
bpy.data.objects.remove(a)
for v in arrow.data.vertices: v.co.z = 0.01
finish(arrow, 'LeverArrow', ['Accent'], slot())
torus('GoalRing', 0.62, 0.05, ['Glow'], slot(), rot=(0, 0, 0))
for v in bpy.data.objects['GoalRing'].data.vertices: v.co.z += 0.06
cyl('GoalDisc', 0.55, 0.02, ['Glow'], slot())
for v in bpy.data.objects['GoalDisc'].data.vertices: v.co.z += 0.03
cyl('StartDisc', 0.55, 0.02, ['Accent'], slot())
for v in bpy.data.objects['StartDisc'].data.vertices: v.co.z += 0.03

for ob in bpy.data.objects: ob.location = (0, 0, 0)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'kit.fbx'), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH'},
                         mesh_smooth_type='FACE', add_leaf_bones=False)

# preview: lay pieces out in a row
for i, ob in enumerate(bpy.data.objects): ob.location = ((i % 6) * 2.6, (i // 6) * 2.6, 0)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam)
cam.location = (6.5, -9.0, 9.0); cam.rotation_euler = (Vector((6.5, 1.3, 0)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam; scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading; sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.show_object_outline = True
scene.render.resolution_x, scene.render.resolution_y = 1400, 800
scene.world = bpy.data.worlds.new('w'); scene.world.color = (0.18, 0.17, 0.22)
scene.render.filepath = os.path.join(OUT, 'kit_preview.png')
bpy.ops.render.render(write_still=True)
print('[kit] objects', [o.name for o in bpy.data.objects if o.type == 'MESH'])
