"""Model Chronos: a worn wind-up tin-toy robot rabbit (hard surface), Blender headless.

    blender -b --factory-startup --python tools/blender/build_robot.py -- <J|K|L> <out_dir>

Story: "a stubborn rabbit doll, Model Chronos", an imperfect machine built by the scientist.
So: pressed-tin shell panels with bevelled edges, seams, rivets, exposed joints, round lens eyes on
the sides of the head, hinged plate ears and a big wind-up key on the back. Quadruped crouch.
Parts/pivots for the Unity RabbitRig: Body (feet), Head (neck), EarL/EarR (hinge), EyeL/EyeR, Spinner.
"""
import bpy, math, os, sys
from mathutils import Vector, Euler

argv = sys.argv[sys.argv.index('--') + 1:]
VARIANT, OUT = argv[0], os.path.abspath(argv[1])
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene; col = scene.collection


def srgb(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


def mat(name, hexc, rough=0.5, metal=0.0, emit=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']; rgb = srgb(hexc)
    b.inputs['Base Color'].default_value = (*rgb, 1); b.inputs['Roughness'].default_value = rough; b.inputs['Metallic'].default_value = metal
    if emit: b.inputs['Emission Color'].default_value = (*rgb, 1); b.inputs['Emission Strength'].default_value = emit
    m.diffuse_color = (*rgb, 1); return m


PAINT = {  # shell paint, secondary panel, trim metal, joint metal, lens
    'J': ('#A8483A', '#E6DCC6', '#C9A15A', '#4A4D55', '#FFC46A'),   # tin red + cream, brass trim
    'K': ('#5B6470', '#2F333B', '#C9A15A', '#2A2C31', '#FFB547'),   # gunmetal, brass trim
    'L': ('#B87452', '#4E7D72', '#D8B06A', '#3B3F45', '#F7E3A8'),   # copper with verdigris panels
}[VARIANT]
M = dict(Body=mat('Body', PAINT[0], 0.45, 0.3), Accent=mat('Accent', PAINT[1], 0.5, 0.2),
         Metal=mat('Metal', PAINT[2], 0.3, 1.0), Joint=mat('Visor', PAINT[3], 0.4, 0.8), Glow=mat('Glow', PAINT[4], 0.1, emit=3))


def obj_finish(ob, name, m, parent=None, origin=None, smooth_angle=35):
    ob.name = name; ob.data.name = name
    ob.data.materials.clear(); ob.data.materials.append(M[m])
    for p in ob.data.polygons: p.use_smooth = True
    try:
        ob.data.set_sharp_from_angle(angle=math.radians(smooth_angle))
    except Exception:
        pass
    if origin is not None:
        scene.cursor.location = origin
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True); bpy.context.view_layer.objects.active = ob
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    if parent is not None:
        mw = ob.matrix_world.copy(); ob.parent = parent; ob.matrix_world = mw
    return ob


def apply_mods(ob):
    bpy.context.view_layer.objects.active = ob
    for md in list(ob.modifiers): bpy.ops.object.modifier_apply(modifier=md.name)


def box(name, m, loc, size, rot=(0, 0, 0), bevel=0.03, segs=3, parent=None, origin=None, taper=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    ob = bpy.context.active_object; ob.scale = size; ob.rotation_euler = Euler([math.radians(a) for a in rot])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if taper:  # shrink the top face (x,y factors) for a pressed-tin silhouette
        for v in ob.data.vertices:
            if v.co.z > 0: v.co.x *= taper[0]; v.co.y *= taper[1]
    if bevel:
        md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = segs; md.limit_method = 'ANGLE'
        wn = ob.modifiers.new('wn', 'WEIGHTED_NORMAL'); wn.keep_sharp = True
        apply_mods(ob)
    return obj_finish(ob, name, m, parent, origin)


def cyl(name, m, loc, r, depth, rot=(0, 0, 0), verts=24, bevel=0.01, parent=None, origin=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=loc, rotation=[math.radians(a) for a in rot])
    ob = bpy.context.active_object
    if bevel:
        md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = 2; md.limit_method = 'ANGLE'
        apply_mods(ob)
    return obj_finish(ob, name, m, parent, origin)


def ball(name, m, loc, r, parent=None, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=r, location=loc)
    ob = bpy.context.active_object; ob.scale = scale; bpy.ops.object.transform_apply(scale=True)
    return obj_finish(ob, name, m, parent, smooth_angle=80)


def torus(name, m, loc, R, r, rot=(0, 0, 0), parent=None):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, major_segments=28, minor_segments=8, location=loc,
                                     rotation=[math.radians(a) for a in rot])
    return obj_finish(bpy.context.active_object, name, m, parent, smooth_angle=80)


def empty(name, loc, parent=None):
    ob = bpy.data.objects.new(name, None); ob.location = loc; col.objects.link(ob)
    if parent is not None: mw = ob.matrix_world.copy(); ob.parent = parent; ob.matrix_world = mw
    return ob


def rivets(prefix, pts, parent, r=0.012):
    for i, p in enumerate(pts): ball(f'{prefix}{i}', 'Metal', p, r, parent)


root = empty('Rabbit', (0, 0, 0))
body = empty('Body', (0, 0, 0), root)

# ---- torso: two pressed-tin shells joined by a seam band
box('TorsoRear', 'Body', (0, 0.17, 0.34), (0.42, 0.36, 0.36), bevel=0.11, segs=4, parent=body, taper=(0.86, 0.9))
box('TorsoFront', 'Body', (0, -0.11, 0.38), (0.38, 0.26, 0.36), bevel=0.1, segs=4, parent=body, taper=(0.84, 0.88))
box('Seam', 'Joint', (0, 0.025, 0.36), (0.44, 0.045, 0.36), bevel=0.08, segs=3, parent=body, taper=(0.86, 1))
box('ChestPlate', 'Accent', (0, -0.218, 0.37), (0.2, 0.03, 0.2), bevel=0.03, parent=body)
box('BackPlate', 'Accent', (0, 0.2, 0.525), (0.22, 0.24, 0.02), bevel=0.02, parent=body)
rivets('RivetSeam', [(x, 0.025, 0.545) for x in (-0.12, 0, 0.12)] + [(0.205 * s, 0.025, 0.36) for s in (-1, 1)], body)
rivets('RivetChest', [(sx * 0.075, -0.234, sz) for sx in (-1, 1) for sz in (0.3, 0.44)], body, 0.01)

# ---- hind legs: hip discs (joints), thigh plates, long foot plates
for s in (-1, 1):
    cyl(f'HipJoint{s}', 'Joint', (0.205 * s, 0.2, 0.25), 0.12, 0.045, rot=(0, 90, 0), parent=body)
    cyl(f'HipCap{s}', 'Metal', (0.232 * s, 0.2, 0.25), 0.05, 0.02, rot=(0, 90, 0), parent=body)
    box(f'Thigh{s}', 'Body', (0.2 * s, 0.16, 0.15), (0.1, 0.3, 0.13), rot=(-28, 0, 0), bevel=0.04, parent=body)
    box(f'Foot{s}', 'Accent', (0.18 * s, 0.0, 0.03), (0.12, 0.36, 0.06), bevel=0.025, parent=body)
    # front legs: piston-like with ball joints
    ball(f'Shoulder{s}', 'Joint', (0.13 * s, -0.17, 0.27), 0.055, body)
    cyl(f'Foreleg{s}', 'Metal', (0.12 * s, -0.2, 0.15), 0.032, 0.22, rot=(8, 0, 0), parent=body)
    box(f'Paw{s}', 'Accent', (0.12 * s, -0.23, 0.025), (0.09, 0.13, 0.05), bevel=0.02, parent=body)
# tail cog
cyl('TailCog', 'Metal', (0, 0.37, 0.33), 0.06, 0.03, rot=(90, 0, 0), verts=12, parent=body)

# ---- head on a neck pivot
head = empty('Head', (0, -0.2, 0.52), body)
cyl('Neck', 'Joint', (0, -0.2, 0.55), 0.07, 0.12, parent=head)
box('Skull', 'Body', (0, -0.3, 0.66), (0.28, 0.34, 0.26), bevel=0.09, segs=4, parent=head, taper=(0.9, 0.85))
box('Muzzle', 'Accent', (0, -0.48, 0.6), (0.17, 0.12, 0.12), bevel=0.045, segs=3, parent=head)
ball('NoseBolt', 'Metal', (0, -0.545, 0.62), 0.02, head)
box('Jaw', 'Joint', (0, -0.44, 0.535), (0.12, 0.08, 0.03), bevel=0.012, parent=head)
for s, n in ((-1, 'EyeL'), (1, 'EyeR')):   # round lens eyes on the sides, in a brass bezel
    torus(f'Bezel{s}', 'Metal', (0.142 * s, -0.33, 0.69), 0.048, 0.013, rot=(0, 90, 0), parent=head)
    e = cyl(n, 'Glow', (0.146 * s, -0.33, 0.69), 0.038, 0.02, rot=(0, 90, 0), verts=20, bevel=0.006, parent=head)
    obj_finish(e, n, 'Glow', origin=(0.146 * s, -0.33, 0.69))
# ---- hinged plate ears
for s, n in ((-1, 'EarL'), (1, 'EarR')):
    base = (0.085 * s, -0.24, 0.79)
    ear = empty(n, base, head)
    cyl(f'{n}Hinge', 'Joint', base, 0.03, 0.07, rot=(0, 90, 0), parent=ear)
    box(f'{n}Plate', 'Body', (base[0] + 0.012 * s, base[1] + 0.03, base[2] + 0.24), (0.085, 0.028, 0.44), rot=(-14, 6 * s, 0), bevel=0.035, segs=3, parent=ear, taper=(0.75, 1))
    box(f'{n}Inner', 'Accent', (base[0] + 0.012 * s, base[1] + 0.012, base[2] + 0.24), (0.05, 0.02, 0.32), rot=(-14, 6 * s, 0), bevel=0.02, parent=ear, taper=(0.7, 1))
    rivets(f'{n}Rivet', [(base[0] + 0.012 * s, base[1] + 0.012 + 0.03 * k, base[2] + 0.1 + 0.12 * k) for k in range(3)], ear, 0.009)

# ---- the wind-up key: big, upright on the back
spin = empty('Spinner', (0, 0.17, 0.54), body)
cyl('KeyCollar', 'Metal', (0, 0.17, 0.55), 0.05, 0.03, parent=spin)
cyl('KeyShaft', 'Metal', (0, 0.17, 0.62), 0.024, 0.12, parent=spin)
for s in (-1, 1):
    torus(f'KeyBow{s}', 'Metal', (0.1 * s, 0.17, 0.75), 0.08, 0.022, rot=(0, 90, 0), parent=spin)
ball('KeyHub', 'Metal', (0, 0.17, 0.75), 0.035, spin)

polys = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
print(f'[robot {VARIANT}] objects={len(bpy.data.objects)} polys={polys}')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f'Rabbit_{VARIANT}.fbx'), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=False, object_types={'MESH', 'EMPTY'},
                         mesh_smooth_type='FACE', add_leaf_bones=False)

cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); col.objects.link(cam); cam.data.lens = 70
cam.location = (2.4, -3.4, 1.7); cam.rotation_euler = (Vector((0, -0.05, 0.48)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam; scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading; sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.cavity_type = 'BOTH'
sh.show_object_outline = True; sh.show_specular_highlight = True
scene.display.render_aa = '8'; scene.render.resolution_x = scene.render.resolution_y = 900
scene.world = bpy.data.worlds.new('w'); scene.world.color = (0.18, 0.17, 0.2)
scene.render.filepath = os.path.join(OUT, f'preview_{VARIANT}.png'); bpy.ops.render.render(write_still=True)
