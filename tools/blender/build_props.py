"""Puzzle props for the Chronos remake (Blender, headless) -> props.fbx + preview.

    blender -b --factory-startup --python tools/blender/build_props.py -- <out_dir>

Each prop is split into one mesh per material so Unity can colour every part from the stage palette.
Pivots sit at the prop's centre. Sizes match the original colliders (Unity metres, Blender Z = Unity Y):
  Crate*      pushable box, 1.8 x 1.8 footprint, 2.0 tall      CrateHalf*  same, 1.0 tall
  Core*       crystal crate (BoxB), same size as Crate
  Piston*     moving block, 2 x 2 x 2
  LaserWall*  thin wall that carries an emitter, 2 wide, 2 tall, 0.5 thick (thin along Blender Y)
  Emitter*    laser head, beam along +Z (Unity +Y; Unity rotates it onto the beam direction)
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector

OUT = os.path.abspath(sys.argv[sys.argv.index('--') + 1])
os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

PREVIEW = {'Crate': (0.55, 0.62, 0.64), 'Trim': (0.14, 0.18, 0.2), 'Mark': (0.1, 0.14, 0.16), 'Core': (0.5, 0.9, 1.0),
           'Piston': (0.2, 0.22, 0.24), 'Hazard': (0.95, 0.75, 0.3), 'Lens': (1.0, 0.3, 0.25), 'Panel': (0.4, 0.45, 0.48)}
M = {}
for n, c in PREVIEW.items():
    m = bpy.data.materials.new(n); m.diffuse_color = (*c, 1); M[n] = m


def join(obs, name, mat):
    bpy.ops.object.select_all(action='DESELECT')
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    if len(obs) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    ob.name = name; ob.data.name = name
    ob.data.materials.clear(); ob.data.materials.append(M[mat])
    wn = ob.modifiers.new('wn', 'WEIGHTED_NORMAL'); wn.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier='wn')
    for p in ob.data.polygons: p.use_smooth = True
    ob['part'] = True
    return ob


def box(size, loc=(0, 0, 0), bevel=0.03, segs=2, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    ob = bpy.context.active_object; ob.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = segs; md.limit_method = 'NONE'
        bpy.ops.object.modifier_apply(modifier='b')
    return ob


def cyl(r, h, loc=(0, 0, 0), verts=32, rot=(0, 0, 0), bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=loc, rotation=rot)
    ob = bpy.context.active_object
    if bevel:
        md = ob.modifiers.new('b', 'BEVEL'); md.width = bevel; md.segments = 2; md.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier='b')
    return ob


def frame(w, h, t):
    """12 bevelled edge bars + 8 corner blocks of a w x w x h box, bar thickness t."""
    obs = []
    hw, hh = w / 2 - t / 2, h / 2 - t / 2
    for sx in (-1, 1):
        for sy in (-1, 1):
            obs.append(box((t, t, h - 2 * t), (sx * hw, sy * hw, 0), 0.025))
    for sz in (-1, 1):
        for s in (-1, 1):
            obs.append(box((w - 2 * t, t, t), (0, s * hw, sz * hh), 0.025))
            obs.append(box((t, w - 2 * t, t), (s * hw, 0, sz * hh), 0.025))
    for sx in (-1, 1):
        for sy in (-1, 1):
            for sz in (-1, 1):
                obs.append(box((t * 1.18, t * 1.18, t * 1.18), (sx * hw, sy * hw, sz * hh), 0.04))
    return obs


def crate(prefix, h):
    w, t = 1.8, 0.16
    join([box((w - 0.12, w - 0.12, h - 0.12), bevel=0.05)], prefix + 'Body', 'Crate')
    join(frame(w, h, t), prefix + 'Frame', 'Trim')
    # stencil band: a recessed-looking dark strip across each side + a small tag plate on top
    band_h = 0.22 if h > 1.5 else 0.16
    bz = h * 0.12
    obs = []
    for s in (-1, 1):
        obs.append(box((w - 2 * t - 0.06, 0.03, band_h), (0, s * (w / 2 - 0.05), bz), 0.008))
        obs.append(box((0.03, w - 2 * t - 0.06, band_h), (s * (w / 2 - 0.05), 0, bz), 0.008))
    obs.append(box((0.5, 0.26, 0.03), (0.3, 0.3, h / 2 - 0.05), 0.008))
    join(obs, prefix + 'Mark', 'Mark')


crate('Crate', 2.0)
crate('CrateHalf', 1.0)

# ---- core crate (BoxB): open cage with a glowing crystal column inside
w, h, t = 1.8, 2.0, 0.16
cage = frame(w, h, t)
for sz in (-1, 1):   # solid top and bottom plates
    cage.append(box((w - 0.1, w - 0.1, 0.14), (0, 0, sz * (h / 2 - 0.07)), 0.03))
for sx in (-1, 1):   # thin mid rails
    cage.append(box((0.07, w - 2 * t, 0.07), (sx * (w / 2 - t / 2), 0, 0), 0.01))
    cage.append(box((w - 2 * t, 0.07, 0.07), (0, sx * (w / 2 - t / 2), 0), 0.01))
join(cage, 'CoreCage', 'Trim')
c = cyl(0.42, h - 0.42, verts=6, bevel=0.03)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT'); bpy.ops.object.mode_set(mode='OBJECT')
join([c], 'CoreCrystal', 'Core')
join([cyl(0.56, 0.1, (0, 0, s * (h / 2 - 0.2)), verts=24, bevel=0.02) for s in (-1, 1)], 'CoreCollar', 'Crate')

# ---- piston block (moving obstacle): heavy dark block, chevron hazard band, bolted top
join([box((1.94, 1.94, 1.94), bevel=0.1, segs=3)], 'PistonBody', 'Piston')
obs = []
n = 7
for side in range(4):
    ang = side * math.pi / 2
    for i in range(n):
        u = -0.83 + i * (1.66 / (n - 1))
        p = Vector((u, -0.985, 0.0)); p.rotate(__import__('mathutils').Euler((0, 0, ang)))
        obs.append(box((0.13, 0.04, 0.42), tuple(p), 0.01, 1, rot=(0, math.radians(35), ang)))
join(obs, 'PistonStripe', 'Hazard')
obs = [box((1.98, 1.98, 0.08), (0, 0, z), 0.02) for z in (0.34, -0.34)]
for sx in (-1, 1):
    for sy in (-1, 1):
        obs.append(cyl(0.11, 0.08, (sx * 0.66, sy * 0.66, 0.99), verts=16, bevel=0.02))
join(obs, 'PistonTrim', 'Trim')

# ---- laser wall: panel with vent slits and a frame (2 wide along X, 2 tall along Z, 0.5 thick along Y)
join([box((1.9, 0.44, 1.9), bevel=0.05)], 'LaserWallBody', 'Panel')
obs = [box((2.0, 0.5, 0.12), (0, 0, z), 0.03) for z in (0.94, -0.94)]
obs += [box((0.12, 0.5, 1.76), (x, 0, 0), 0.03) for x in (0.94, -0.94)]
join(obs, 'LaserWallFrame', 'Trim')
join([box((1.2, 0.47, 0.05), (0, 0, z), 0.01) for z in (-0.55, -0.4, -0.25)] +
     [box((1.2, 0.47, 0.05), (0, 0, z), 0.01) for z in (0.45, 0.6)], 'LaserWallVent', 'Mark')

# ---- emitter: housing + collar fins + lens, beam leaves along +Z
obs = [cyl(0.26, 0.42, (0, 0, -0.06), verts=24, bevel=0.03), box((0.62, 0.62, 0.12), (0, 0, -0.27), 0.03)]
join(obs, 'EmitterBody', 'Trim')
obs = [cyl(0.3, 0.06, (0, 0, z), verts=24, bevel=0.015) for z in (0.0, 0.1)]
join(obs, 'EmitterCollar', 'Piston')
join([cyl(0.15, 0.06, (0, 0, 0.17), verts=24, bevel=0.02)], 'EmitterLens', 'Lens')

parts = [o for o in bpy.data.objects if o.get('part')]
for o in bpy.data.objects:
    if not o.get('part'): bpy.data.objects.remove(o)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'props.fbx'), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH'},
                         mesh_smooth_type='FACE', add_leaf_bones=False)

# preview: one assembled prop per slot
groups = [('Crate',), ('CrateHalf',), ('Core',), ('Piston',), ('LaserWall',), ('Emitter',)]
for o in parts:
    for i, (g,) in enumerate(groups):
        if o.name.startswith(g) and not (g == 'Crate' and o.name.startswith('CrateHalf')):
            o.location = (i * 2.8, 0, 0)
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam)
cam.location = (7.0, -17.0, 9.0); cam.rotation_euler = (Vector((7.0, 0, 0)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam; scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading; sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.show_object_outline = True
scene.render.resolution_x, scene.render.resolution_y = 1600, 600
scene.world = bpy.data.worlds.new('w'); scene.world.color = (0.12, 0.13, 0.15)
scene.render.filepath = os.path.join(OUT, 'props_preview.png')
bpy.ops.render.render(write_still=True)
print('[props] objects', sorted(o.name for o in parts))
