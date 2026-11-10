"""Model the Chronos remake rabbits in Blender (headless) and export FBX + a shape preview.

    blender -b --factory-startup --python tools/blender/build_rabbits.py -- <A|B|C> <out_dir>

Forms are built from metaballs (implicit surfaces that blend into one soft shape), converted to
meshes, decimated to game budgets and exported as separate, correctly pivoted parts so the Unity
RabbitRig can animate them:  Body (pivot at feet), Head (pivot at neck), EarL/EarR (pivot at ear
base), EyeL/EyeR (pivot at eye centre), Spinner (pivot at its axle), plus static detail parts.
Blender is Z-up and the rabbit faces -Y here; the FBX export maps that to Unity's +Z forward.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Euler, Quaternion

argv = sys.argv[sys.argv.index('--') + 1:]
VARIANT, OUT = argv[0], os.path.abspath(argv[1])
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
col = scene.collection

# ------------------------------------------------------------------ materials (names are remapped in Unity)
def mat(name, rgb, rough=0.5, metal=0.0, emit=0.0):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*rgb, 1)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if emit:
        b.inputs['Emission Color'].default_value = (*rgb, 1)
        b.inputs['Emission Strength'].default_value = emit
    m.diffuse_color = (*rgb, 1)  # workbench preview colour
    return m


def srgb(h):
    h = h.lstrip('#'); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(((x + 0.055) / 1.055) ** 2.4 if x > 0.04045 else x / 12.92 for x in c)


# ------------------------------------------------------------------ helpers
def finish(ob, name, material, origin=None, decimate=None, parent=None, smooth=True):
    ob.name = name; ob.data.name = name
    if material is not None:
        ob.data.materials.clear(); ob.data.materials.append(material)
    if decimate:
        md = ob.modifiers.new('dec', 'DECIMATE'); md.ratio = decimate
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier='dec')
    if smooth:
        for p in ob.data.polygons: p.use_smooth = True
    if origin is not None:
        scene.cursor.location = origin
        bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    if parent is not None:
        mw = ob.matrix_world.copy(); ob.parent = parent; ob.matrix_world = mw
    return ob


def meta(name, elements, material, origin=None, res=0.02, decimate=0.35, parent=None, threshold=0.05):
    """elements: (type, co, radius, size(xyz) or None, euler_deg or None)"""
    mb = bpy.data.metaballs.new(name + '_mb')
    mb.resolution = res; mb.render_resolution = res; mb.threshold = threshold
    ob = bpy.data.objects.new(name + '_mb', mb); col.objects.link(ob)
    for typ, co, r, size, rot in elements:
        # radius is given as the intended visible half-size; at threshold 0.05 the surface sits at ~0.8 r
        e = mb.elements.new(); e.type = typ; e.co = co; e.radius = r * 1.26 * (1.0 if typ == 'BALL' else 1.0)
        e.stiffness = 2.0
        if size is not None: e.size_x, e.size_y, e.size_z = size
        if rot is not None: e.rotation = Euler([math.radians(a) for a in rot]).to_quaternion()
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT'); ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.convert(target='MESH')
    mesh_ob = bpy.context.view_layer.objects.active
    return finish(mesh_ob, name, material, origin, decimate, parent)


def prim(kind, name, material, loc, scale=(1, 1, 1), rot=(0, 0, 0), parent=None, subdiv=0, bevel=None, **kw):
    if kind == 'sphere': bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, location=loc, **kw)
    elif kind == 'cube': bpy.ops.mesh.primitive_cube_add(location=loc, **kw)
    elif kind == 'cyl': bpy.ops.mesh.primitive_cylinder_add(vertices=24, location=loc, **kw)
    elif kind == 'torus': bpy.ops.mesh.primitive_torus_add(location=loc, major_segments=32, minor_segments=10, **kw)
    elif kind == 'cone': bpy.ops.mesh.primitive_cone_add(vertices=24, location=loc, **kw)
    ob = bpy.context.active_object
    ob.scale = scale; ob.rotation_euler = Euler([math.radians(a) for a in rot])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        md = ob.modifiers.new('bev', 'BEVEL'); md.width = bevel[0]; md.segments = bevel[1]; md.limit_method = 'NONE'
        bpy.ops.object.modifier_apply(modifier='bev')
    if subdiv:
        md = ob.modifiers.new('sub', 'SUBSURF'); md.levels = subdiv; md.render_levels = subdiv
        bpy.ops.object.modifier_apply(modifier='sub')
    return finish(ob, name, material, None, None, parent)


def empty(name, loc, parent=None):
    ob = bpy.data.objects.new(name, None); ob.location = loc; col.objects.link(ob)
    if parent is not None:
        mw = ob.matrix_world.copy(); ob.parent = parent; ob.matrix_world = mw
    return ob


def front_y(ob, x, z):
    """y of the mesh surface facing -Y nearest to (x, z) (for placing face details)."""
    best = None
    mw = ob.matrix_world
    for v in ob.data.vertices:
        p = mw @ v.co
        if abs(p.x - x) < 0.06 and abs(p.z - z) < 0.06:
            best = p.y if best is None else min(best, p.y)
    return best


# ------------------------------------------------------------------ designs
def moca():
    body_m = mat('Body', srgb('#F3EEE4'), 0.55)
    acc_m = mat('Accent', srgb('#FF7B47'), 0.5)
    visor_m = mat('Visor', srgb('#1B2033'), 0.18)
    glow_m = mat('Glow', srgb('#FFE7A8'), 0.3, emit=3)
    metal_m = mat('Metal', srgb('#D9A35A'), 0.35, metal=1)
    root = empty('Rabbit', (0, 0, 0))
    body = meta('Body', [
        ('ELLIPSOID', (0, 0.03, 0.40), 0.42, (0.80, 0.75, 0.80), None),           # torso
        ('ELLIPSOID', (0.31, -0.06, 0.40), 0.16, (0.55, 0.55, 0.9), (0, 20, 0)),   # arm nubs
        ('ELLIPSOID', (-0.31, -0.06, 0.40), 0.16, (0.55, 0.55, 0.9), (0, -20, 0)),
        ('BALL', (0, 0.36, 0.33), 0.15, None, None),                               # tail pom
    ], body_m, origin=(0, 0, 0), parent=root)
    meta('Feet', [
        ('ELLIPSOID', (0.19, -0.06, 0.09), 0.16, (0.95, 1.4, 0.55), None),
        ('ELLIPSOID', (-0.19, -0.06, 0.09), 0.16, (0.95, 1.4, 0.55), None),
    ], acc_m, parent=body)
    head = meta('Head', [
        ('CUBE', (0, -0.02, 0.98), 0.24, (0.19, 0.12, 0.10), None),               # soft rounded box head
    ], body_m, origin=(0, 0, 0.72), parent=body)
    fy = front_y(head, 0, 0.98) or -0.40
    prim('cube', 'Visor', visor_m, (0, fy + 0.015, 0.975), scale=(0.33, 0.03, 0.2), parent=head, bevel=(0.12, 6))
    eyes = []
    for s, n in ((-1, 'EyeL'), (1, 'EyeR')):
        e = prim('sphere', n, glow_m, (0.13 * s, fy - 0.022, 0.99), scale=(0.055, 0.02, 0.085), parent=head)
        finish(e, n, None, origin=(0.13 * s, fy - 0.022, 0.99)); eyes.append(e)
    prim('sphere', 'Blush', acc_m, (0.0, fy - 0.012, 0.88), scale=(0.035, 0.015, 0.02), parent=head)
    ears = []
    for s, n in ((-1, 'EarL'), (1, 'EarR')):
        base = (0.17 * s, 0.03, 1.22)
        ear = meta(n, [
            ('ELLIPSOID', (base[0], base[1], base[2] + 0.17), 0.2, (0.55, 0.32, 1.0), (0, 10 * s, 0)),
            ('ELLIPSOID', (base[0] + 0.05 * s, base[1] + 0.03, base[2] + 0.42), 0.2, (0.58, 0.32, 1.0), (0, 12 * s, 0)),
            ('ELLIPSOID', (base[0] + 0.09 * s, base[1] + 0.06, base[2] + 0.63), 0.15, (0.6, 0.35, 0.8), (0, 16 * s, 0)),
        ], body_m, origin=base, parent=head)
        meta(n + 'Inner', [
            ('ELLIPSOID', (base[0] + 0.03 * s, base[1] - 0.06, base[2] + 0.32), 0.15, (0.4, 0.12, 1.2), (0, 12 * s, 0)),
        ], acc_m, parent=ear)
        ears.append(ear)
    spin = empty('Spinner', (0, 0.42, 0.62), parent=body)
    prim('cyl', 'KeyShaft', metal_m, (0, 0.47, 0.62), scale=(0.035, 0.035, 0.08), rot=(90, 0, 0), parent=spin)
    for s in (-1, 1):
        prim('torus', f'KeyBow{s}', metal_m, (0.1 * s, 0.56, 0.62), scale=(0.07, 0.07, 0.07), rot=(90, 0, 0), parent=spin,
             major_radius=1.0, minor_radius=0.32)
    return root


def tock():
    body_m = mat('Body', srgb('#8C97B5'), 0.5)
    belly_m = mat('Accent', srgb('#EEE6D6'), 0.6)
    eye_m = mat('Visor', srgb('#141826'), 0.1)
    glow_m = mat('Glow', srgb('#FFF3CC'), 0.2, emit=2.5)
    metal_m = mat('Metal', srgb('#D7A452'), 0.3, metal=1)
    inner_m = mat('Inner', srgb('#F2A08F'), 0.6)
    root = empty('Rabbit', (0, 0, 0))
    body = meta('Body', [
        ('ELLIPSOID', (0, 0.06, 0.52), 0.36, (0.72, 0.8, 1.15), (-10, 0, 0)),     # upright torso
        ('ELLIPSOID', (0.17, 0.12, 0.27), 0.22, (0.6, 1.05, 0.85), None),         # haunches
        ('ELLIPSOID', (-0.17, 0.12, 0.27), 0.22, (0.6, 1.05, 0.85), None),
        ('ELLIPSOID', (0.16, -0.1, 0.05), 0.15, (0.55, 1.7, 0.35), None),         # long feet
        ('ELLIPSOID', (-0.16, -0.1, 0.05), 0.15, (0.55, 1.7, 0.35), None),
        ('ELLIPSOID', (0.18, -0.12, 0.55), 0.1, (0.6, 0.6, 1.6), (0, 0, 15)),     # arms
        ('ELLIPSOID', (-0.18, -0.12, 0.55), 0.1, (0.6, 0.6, 1.6), (0, 0, -15)),
        ('BALL', (0, 0.34, 0.33), 0.12, None, None),
    ], body_m, origin=(0, 0, 0), parent=root)
    meta('Belly', [('ELLIPSOID', (0, -0.12, 0.5), 0.26, (0.65, 0.35, 1.05), (-10, 0, 0))], belly_m, parent=body)
    head = meta('Head', [
        ('ELLIPSOID', (0, -0.06, 1.02), 0.26, (0.95, 1.05, 0.9), None),
        ('ELLIPSOID', (0, -0.27, 0.96), 0.12, (1.0, 0.85, 0.75), None),          # muzzle
    ], body_m, origin=(0, 0, 0.84), parent=body)
    fy = front_y(head, 0.12, 1.05) or -0.25
    eyes = []
    for s, n in ((-1, 'EyeL'), (1, 'EyeR')):
        e = prim('sphere', n, eye_m, (0.12 * s, fy + 0.02, 1.06), scale=(0.045, 0.03, 0.055), parent=head)
        finish(e, n, None, origin=(0.12 * s, fy + 0.02, 1.06)); eyes.append(e)
    prim('sphere', 'Nose', inner_m, (0, -0.375, 0.99), scale=(0.035, 0.025, 0.025), parent=head)
    prim('torus', 'Monocle', metal_m, (0.12, fy - 0.0, 1.06), scale=(0.075, 0.075, 0.075), rot=(90, 0, 0), parent=head,
         major_radius=1.0, minor_radius=0.12)
    ears = []
    for s, n in ((-1, 'EarL'), (1, 'EarR')):
        base = (0.1 * s, 0.02, 1.22)
        ear = meta(n, [
            ('ELLIPSOID', (base[0], base[1], base[2] + 0.22), 0.18, (0.5, 0.28, 1.4), (0, 6 * s, 0)),
            ('ELLIPSOID', (base[0] + 0.03 * s, base[1] + 0.03, base[2] + 0.55), 0.17, (0.52, 0.28, 1.3), (-8, 8 * s, 0)),
        ], body_m, origin=base, parent=head)
        meta(n + 'Inner', [('ELLIPSOID', (base[0] + 0.015 * s, base[1] - 0.05, base[2] + 0.38), 0.13, (0.38, 0.1, 1.9), (-4, 7 * s, 0))],
             inner_m, parent=ear)
        ears.append(ear)
    spin = empty('Spinner', (0, -0.24, 0.62), parent=body)
    prim('cyl', 'Watch', metal_m, (0, -0.25, 0.62), scale=(0.11, 0.11, 0.025), rot=(90 - 10, 0, 0), parent=body)
    prim('cyl', 'WatchFace', belly_m, (0, -0.276, 0.624), scale=(0.09, 0.09, 0.006), rot=(90 - 10, 0, 0), parent=body)
    prim('cube', 'Hand', eye_m, (0, -0.285, 0.655), scale=(0.008, 0.004, 0.035), rot=(-10, 0, 0), parent=spin)
    return root


def mochi():
    body_m = mat('Body', srgb('#FBF4E8'), 0.6)
    acc_m = mat('Accent', srgb('#E8704E'), 0.5)
    eye_m = mat('Visor', srgb('#1E1A22'), 0.15)
    glass_m = mat('Glow', srgb('#FFD27A'), 0.2, emit=2.0)
    metal_m = mat('Metal', srgb('#C98F45'), 0.35, metal=1)
    root = empty('Rabbit', (0, 0, 0))
    body = meta('Body', [
        ('ELLIPSOID', (0, 0.02, 0.36), 0.5, (1.0, 0.95, 0.72), None),            # mochi blob (body+head in one)
        ('ELLIPSOID', (0, -0.06, 0.62), 0.36, (0.95, 0.85, 0.7), None),
        ('ELLIPSOID', (0.2, -0.3, 0.1), 0.12, (0.9, 1.0, 0.6), None),            # front paws
        ('ELLIPSOID', (-0.2, -0.3, 0.1), 0.12, (0.9, 1.0, 0.6), None),
    ], body_m, origin=(0, 0, 0), parent=root)
    head = empty('Head', (0, -0.1, 0.62), parent=body)        # head is part of the blob: rig moves details only
    fy = front_y(body, 0.14, 0.58) or -0.42
    eyes = []
    for s, n in ((-1, 'EyeL'), (1, 'EyeR')):
        e = prim('sphere', n, eye_m, (0.14 * s, fy + 0.01, 0.58), scale=(0.04, 0.02, 0.06), parent=head)
        finish(e, n, None, origin=(0.14 * s, fy + 0.01, 0.58)); eyes.append(e)
    for s in (-1, 1):
        prim('sphere', f'Cheek{s}', acc_m, (0.24 * s, fy + 0.04, 0.5), scale=(0.06, 0.02, 0.035), parent=head)
    ears = []
    for s, n in ((-1, 'EarL'), (1, 'EarR')):
        base = (0.2 * s, 0.0, 0.85)
        ear = meta(n, [
            ('ELLIPSOID', (base[0] + 0.04 * s, base[1], base[2] + 0.14), 0.2, (0.6, 0.38, 1.0), (0, 25 * s, 0)),
            ('ELLIPSOID', (base[0] + 0.11 * s, base[1] + 0.02, base[2] + 0.3), 0.16, (0.65, 0.4, 0.8), (0, 35 * s, 0)),
        ], body_m, origin=base, parent=head)
        meta(n + 'Inner', [('ELLIPSOID', (base[0] + 0.07 * s, base[1] - 0.07, base[2] + 0.2), 0.12, (0.45, 0.12, 1.0), (0, 30 * s, 0))],
             acc_m, parent=ear)
        ears.append(ear)
    spin = empty('Spinner', (0, 0.42, 0.5), parent=body)
    prim('cyl', 'CapTop', metal_m, (0, 0.44, 0.66), scale=(0.11, 0.11, 0.02), parent=spin)
    prim('cyl', 'CapBot', metal_m, (0, 0.44, 0.34), scale=(0.11, 0.11, 0.02), parent=spin)
    prim('cone', 'GlassTop', glass_m, (0, 0.44, 0.58), scale=(0.09, 0.09, 0.07), rot=(180, 0, 0), parent=spin)
    prim('cone', 'GlassBot', glass_m, (0, 0.44, 0.42), scale=(0.09, 0.09, 0.07), parent=spin)
    return root


def side_x(ob, y, z, tol=0.05):
    """x of the mesh surface on the +X side nearest to (y, z) (for lateral rabbit eyes)."""
    best = None; mw = ob.matrix_world
    for v in ob.data.vertices:
        p = mw @ v.co
        if abs(p.y - y) < tol and abs(p.z - z) < tol and p.x > 0:
            best = p.x if best is None else max(best, p.x)
    return best


def hare_body(body_m, belly_m, root, upright=1.0):
    """Shared athletic hare body: sitting on long hind feet, front legs reaching down."""
    body = meta('Body', [
        ('ELLIPSOID', (0, 0.07, 0.50), 0.30, (0.72, 0.9, 1.15), (-18 * upright, 0, 0)),   # torso, leaning forward
        ('ELLIPSOID', (0, -0.08, 0.66), 0.17, (0.75, 0.8, 0.9), None),                    # chest
        ('ELLIPSOID', (0.16, 0.16, 0.25), 0.21, (0.62, 1.15, 0.92), None),                # haunches
        ('ELLIPSOID', (-0.16, 0.16, 0.25), 0.21, (0.62, 1.15, 0.92), None),
        ('ELLIPSOID', (0.14, -0.06, 0.035), 0.13, (0.55, 2.0, 0.3), None),                # long hind feet
        ('ELLIPSOID', (-0.14, -0.06, 0.035), 0.13, (0.55, 2.0, 0.3), None),
        ('ELLIPSOID', (0.11, -0.2, 0.28), 0.07, (0.7, 0.7, 2.6), (8, 0, 0)),              # slender front legs
        ('ELLIPSOID', (-0.11, -0.2, 0.28), 0.07, (0.7, 0.7, 2.6), (8, 0, 0)),
        ('ELLIPSOID', (0.11, -0.25, 0.04), 0.07, (0.8, 1.3, 0.5), None),                  # front paws
        ('ELLIPSOID', (-0.11, -0.25, 0.04), 0.07, (0.8, 1.3, 0.5), None),
        ('BALL', (0, 0.38, 0.33), 0.09, None, None),                                       # small tail
    ], body_m, origin=(0, 0, 0), parent=root)
    meta('Belly', [('ELLIPSOID', (0, -0.15, 0.55), 0.19, (0.65, 0.35, 1.1), (-18 * upright, 0, 0))], belly_m, parent=body)
    return body


def hare_head(body_m, body, z=0.98, y=-0.12):
    return meta('Head', [
        ('ELLIPSOID', (0, y, z), 0.19, (0.78, 1.2, 0.86), (-8, 0, 0)),                   # elongated skull
        ('ELLIPSOID', (0, y - 0.15, z - 0.05), 0.095, (0.95, 0.9, 0.8), None),           # short round muzzle
        ('ELLIPSOID', (0.055, y - 0.12, z - 0.07), 0.06, (1.0, 1.0, 0.9), None),         # cheeks
        ('ELLIPSOID', (-0.055, y - 0.12, z - 0.07), 0.06, (1.0, 1.0, 0.9), None),
        ('ELLIPSOID', (0, y + 0.08, z - 0.12), 0.12, (0.8, 0.9, 1.0), None),             # neck blend
    ], body_m, origin=(0, y + 0.05, z - 0.16), parent=body)


def lateral_eyes(head, eye_m, z, y):
    sx = side_x(head, y, z) or 0.13
    eyes = []
    for s, n in ((-1, 'EyeL'), (1, 'EyeR')):
        e = prim('sphere', n, eye_m, (s * (sx - 0.006), y, z), scale=(0.018, 0.045, 0.03), rot=(0, 0, -18 * s), parent=head)
        finish(e, n, None, origin=(s * (sx - 0.006), y, z)); eyes.append(e)
    return eyes


def long_ears(head, body_m, inner_m, base_z, sweep=10, length=1.0, base_y=-0.02):
    ears = []
    for s, n in ((-1, 'EarL'), (1, 'EarR')):
        base = (0.075 * s, base_y, base_z)
        ear = meta(n, [
            ('ELLIPSOID', (base[0] + 0.01 * s, base[1] + 0.02, base[2] + 0.2 * length), 0.15, (0.55, 0.32, 1.45 * length), (-sweep, 6 * s, 0)),
            ('ELLIPSOID', (base[0] + 0.03 * s, base[1] + 0.08 * length, base[2] + 0.48 * length), 0.13, (0.55, 0.32, 1.35 * length), (-sweep - 6, 8 * s, 0)),
        ], body_m, origin=base, parent=head)
        # inner ear: a smooth flattened ellipsoid set into the front face of the ear
        prim('sphere', n + 'Inner', inner_m, (base[0] + 0.016 * s, base[1] - 0.012 + 0.03 * length, base[2] + 0.34 * length),
             scale=(0.045, 0.012, 0.2 * length), rot=(-sweep - 3, 0, 7 * s), parent=ear)
        ears.append(ear)
    return ears


def sable():
    body_m = mat('Body', srgb('#3B404C'), 0.45)
    belly_m = mat('Accent', srgb('#E7E0D2'), 0.6)
    eye_m = mat('Glow', srgb('#FFB547'), 0.2, emit=3)
    metal_m = mat('Metal', srgb('#C99A55'), 0.35, metal=1)
    inner_m = mat('Inner', srgb('#E7E0D2'), 0.6)
    root = empty('Rabbit', (0, 0, 0))
    body = hare_body(body_m, belly_m, root)
    head = hare_head(body_m, body)
    lateral_eyes(head, eye_m, 1.0, -0.2)
    prim('sphere', 'Nose', metal_m, (0, -0.395, 0.925), scale=(0.022, 0.016, 0.016), parent=head)
    long_ears(head, body_m, inner_m, 1.12, sweep=14)
    prim('torus', 'Collar', metal_m, (0, -0.05, 0.8), scale=(0.17, 0.17, 0.17), rot=(-25, 0, 0), parent=body,
         major_radius=1.0, minor_radius=0.09)
    for i, (y, z) in enumerate([(0.05, 0.83), (0.15, 0.76), (0.24, 0.66), (0.3, 0.54)]):   # mechanical vertebrae
        prim('cube', f'Spine{i}', metal_m, (0, y + 0.135 - i * 0.012, z), scale=(0.035, 0.03, 0.022), rot=(-30 - i * 12, 0, 0), parent=body, bevel=(0.01, 2))
    spin = empty('Spinner', (0.29, 0.16, 0.26), parent=body)                              # hip gear
    prim('cyl', 'HipGear', metal_m, (0.31, 0.16, 0.26), scale=(0.07, 0.07, 0.012), rot=(0, 90, 0), parent=spin)
    return root


def ivory():
    body_m = mat('Body', srgb('#EDE7DA'), 0.35)
    belly_m = mat('Accent', srgb('#F7F3EA'), 0.5)
    eye_m = mat('Visor', srgb('#1A1C24'), 0.1)
    metal_m = mat('Metal', srgb('#D3A24E'), 0.3, metal=1)
    inner_m = mat('Inner', srgb('#E9C2B2'), 0.6)
    root = empty('Rabbit', (0, 0, 0))
    body = meta('Body', [                                                                   # natural crouched rabbit
        ('ELLIPSOID', (0, 0.1, 0.34), 0.33, (0.72, 1.15, 0.85), (-10, 0, 0)),           # arched back
        ('ELLIPSOID', (0, -0.14, 0.38), 0.2, (0.8, 0.8, 1.0), None),                    # chest
        ('ELLIPSOID', (0.17, 0.14, 0.2), 0.2, (0.6, 1.2, 0.9), None),                   # haunches
        ('ELLIPSOID', (-0.17, 0.14, 0.2), 0.2, (0.6, 1.2, 0.9), None),
        ('ELLIPSOID', (0.15, -0.05, 0.035), 0.12, (0.55, 2.1, 0.3), None),
        ('ELLIPSOID', (-0.15, -0.05, 0.035), 0.12, (0.55, 2.1, 0.3), None),
        ('ELLIPSOID', (0.1, -0.26, 0.12), 0.07, (0.7, 0.8, 1.8), None),
        ('ELLIPSOID', (-0.1, -0.26, 0.12), 0.07, (0.7, 0.8, 1.8), None),
        ('BALL', (0, 0.48, 0.3), 0.08, None, None),
    ], body_m, origin=(0, 0, 0), parent=root)
    head = hare_head(body_m, body, z=0.66, y=-0.3)
    lateral_eyes(head, eye_m, 0.69, -0.38)
    prim('sphere', 'Nose', inner_m, (0, -0.575, 0.6), scale=(0.022, 0.016, 0.016), parent=head)
    long_ears(head, body_m, inner_m, 0.74, sweep=24, length=0.95, base_y=-0.26)
    for s in (-1, 1):
        prim('torus', f'HipRing{s}', metal_m, (0.33 * s, 0.14, 0.22), scale=(0.12, 0.12, 0.12), rot=(0, 90, 0), parent=body,
             major_radius=1.0, minor_radius=0.07)
    prim('torus', 'Collar', metal_m, (0, -0.2, 0.52), scale=(0.15, 0.15, 0.15), rot=(-50, 0, 0), parent=body, major_radius=1.0, minor_radius=0.07)
    spin = empty('Spinner', (0, 0.36, 0.52), parent=body)
    prim('cyl', 'KeyShaft', metal_m, (0, 0.4, 0.52), scale=(0.025, 0.025, 0.06), rot=(90, 0, 0), parent=spin)
    for s in (-1, 1):
        prim('torus', f'KeyBow{s}', metal_m, (0.07 * s, 0.47, 0.52), scale=(0.05, 0.05, 0.05), rot=(90, 0, 0), parent=spin, major_radius=1.0, minor_radius=0.3)
    return root


def drift():
    body_m = mat('Body', srgb('#2F333D'), 0.5)
    belly_m = mat('Accent', srgb('#D9D2C3'), 0.6)
    eye_m = mat('Glow', srgb('#FFC46B'), 0.2, emit=3)
    scarf_m = mat('Metal', srgb('#E8664A'), 0.7)     # remapped as the accent cloth colour in Unity
    inner_m = mat('Inner', srgb('#D9D2C3'), 0.6)
    root = empty('Rabbit', (0, 0, 0))
    body = hare_body(body_m, belly_m, root, upright=0.8)
    head = hare_head(body_m, body)
    lateral_eyes(head, eye_m, 1.0, -0.2)
    long_ears(head, body_m, inner_m, 1.12, sweep=18)
    meta('Scarf', [                                                                        # wrap + trailing tail
        ('ELLIPSOID', (0, -0.04, 0.8), 0.2, (1.0, 1.0, 0.35), (-20, 0, 0)),
        ('ELLIPSOID', (0.06, 0.16, 0.77), 0.1, (0.95, 1.5, 0.4), (-10, 0, -20)),
        ('ELLIPSOID', (0.13, 0.3, 0.7), 0.095, (0.95, 1.6, 0.36), (-18, 0, -28)),
        ('ELLIPSOID', (0.2, 0.44, 0.62), 0.09, (0.95, 1.6, 0.34), (-25, 0, -34)),
        ('ELLIPSOID', (0.26, 0.57, 0.53), 0.085, (0.95, 1.5, 0.32), (-30, 0, -40)),
    ], scarf_m, parent=body)
    spin = empty('Spinner', (0, 0.0, 0.8), parent=body)                                     # pocket watch on the scarf knot
    prim('cyl', 'Watch', belly_m, (0, -0.22, 0.78), scale=(0.05, 0.05, 0.012), rot=(70, 0, 0), parent=spin)
    return root


def quad(body_hex, chest_hex, eye_hex, eye_glow, inner_hex, metal_hex='#D0A050'):
    """Quadruped clockwork rabbit in a natural crouch with a large wind-up key on its back.
    The back is nearly level so the key reads from the high iso game camera."""
    body_m = mat('Body', srgb(body_hex), 0.4)
    chest_m = mat('Accent', srgb(chest_hex), 0.5)
    eye_m = mat('Glow' if eye_glow else 'Visor', srgb(eye_hex), 0.15, emit=3 if eye_glow else 0)
    metal_m = mat('Metal', srgb(metal_hex), 0.3, metal=1)
    inner_m = mat('Inner', srgb(inner_hex), 0.6)
    root = empty('Rabbit', (0, 0, 0))
    body = meta('Body', [
        ('ELLIPSOID', (0, 0.12, 0.34), 0.33, (0.72, 1.2, 0.78), (-6, 0, 0)),     # long, level back
        ('ELLIPSOID', (0, -0.16, 0.37), 0.2, (0.8, 0.8, 1.0), None),             # chest
        ('ELLIPSOID', (0.17, 0.18, 0.21), 0.2, (0.6, 1.25, 0.92), None),         # haunches
        ('ELLIPSOID', (-0.17, 0.18, 0.21), 0.2, (0.6, 1.25, 0.92), None),
        ('ELLIPSOID', (0.15, -0.02, 0.035), 0.12, (0.55, 2.2, 0.3), None),       # long hind feet
        ('ELLIPSOID', (-0.15, -0.02, 0.035), 0.12, (0.55, 2.2, 0.3), None),
        ('ELLIPSOID', (0.1, -0.27, 0.13), 0.065, (0.7, 0.8, 1.9), None),         # front legs
        ('ELLIPSOID', (-0.1, -0.27, 0.13), 0.065, (0.7, 0.8, 1.9), None),
        ('ELLIPSOID', (0.1, -0.31, 0.03), 0.065, (0.85, 1.3, 0.5), None),        # front paws
        ('ELLIPSOID', (-0.1, -0.31, 0.03), 0.065, (0.85, 1.3, 0.5), None),
        ('BALL', (0, 0.52, 0.32), 0.075, None, None),                             # tail
    ], body_m, origin=(0, 0, 0), parent=root)
    meta('Chest', [('ELLIPSOID', (0, -0.27, 0.36), 0.15, (0.7, 0.35, 1.1), (-15, 0, 0))], chest_m, parent=body)
    head = hare_head(body_m, body, z=0.66, y=-0.32)
    lateral_eyes(head, eye_m, 0.69, -0.4)
    prim('sphere', 'Nose', inner_m, (0, -0.595, 0.6), scale=(0.022, 0.016, 0.016), parent=head)
    long_ears(head, body_m, inner_m, 0.76, sweep=26, length=0.98, base_y=-0.28)
    # brass collar (the only seam: keeps the silhouette clean)
    prim('torus', 'Collar', metal_m, (0, -0.22, 0.52), scale=(0.15, 0.15, 0.15), rot=(-48, 0, 0), parent=body, major_radius=1.0, minor_radius=0.07)
    # the wind-up key: big, upright on the back so the iso camera sees it from almost any angle
    spin = empty('Spinner', (0, 0.18, 0.62), parent=body)
    prim('cyl', 'KeyShaft', metal_m, (0, 0.18, 0.66), scale=(0.028, 0.028, 0.07), parent=spin)
    prim('cyl', 'KeyCollar', metal_m, (0, 0.18, 0.61), scale=(0.055, 0.055, 0.02), parent=spin)
    for s in (-1, 1):
        prim('torus', f'KeyBow{s}', metal_m, (0.105 * s, 0.18, 0.79), scale=(0.085, 0.085, 0.085), rot=(0, 90, 0), parent=spin,
             major_radius=1.0, minor_radius=0.26)
    prim('sphere', 'KeyHub', metal_m, (0, 0.18, 0.79), scale=(0.04, 0.04, 0.04), parent=spin)
    return root


root = {'A': moca, 'B': tock, 'C': mochi, 'D': sable, 'E': ivory, 'F': drift,
        'G': lambda: quad('#3B404C', '#E3DCCD', '#FFB547', True, '#E3DCCD'),          # graphite
        'H': lambda: quad('#ECE5D8', '#F7F3EA', '#1C1E26', False, '#E6BFAE'),         # ivory
        'I': lambda: quad('#4F7D74', '#E6DDC8', '#FFD27A', True, '#E6DDC8'),          # verdigris
        }[VARIANT]()

# ------------------------------------------------------------------ report + export
tris = sum(len(o.data.polygons) for o in bpy.data.objects if o.type == 'MESH')
print(f'[rabbit {VARIANT}] objects={len(bpy.data.objects)} polys={tris}')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f'Rabbit_{VARIANT}.fbx'), use_selection=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=False, object_types={'MESH', 'EMPTY'}, mesh_smooth_type='FACE',
                         add_leaf_bones=False)

# ------------------------------------------------------------------ shape preview (workbench, 3/4 front)
cam_data = bpy.data.cameras.new('cam'); cam_data.lens = 70
cam = bpy.data.objects.new('cam', cam_data); col.objects.link(cam)
cam.location = (2.9, -4.6, 1.9)
cam.rotation_euler = (Vector((0, 0, 0.85)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam
scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading
sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.cavity_type = 'BOTH'
sh.show_object_outline = True; sh.show_specular_highlight = True
scene.display.render_aa = '8'
scene.render.resolution_x = scene.render.resolution_y = 900
scene.render.film_transparent = False
scene.world = bpy.data.worlds.new('w'); scene.world.color = (0.18, 0.17, 0.22)
scene.render.filepath = os.path.join(OUT, f'preview_{VARIANT}.png')
bpy.ops.render.render(write_still=True)
print('[rabbit] wrote', scene.render.filepath)
