"""Model Chronos, cute-mechanical variants (Blender headless).

    blender -b --factory-startup --python tools/blender/build_toy.py -- <M|N|O> <out_dir>

M  vintage wind-up toy : big rounded-tin head, round tin body, stubby legs, front-facing lens eyes
                         with catch-lights, painted cheeks, rivets, seam band, key.
N  plush + machine     : soft plush body, brass joint caps / ear hinges / ear tips, button eyes, key.
O  round rabbit + parts: the smooth quadruped silhouette with a seam band, rivets, lens eyes, key.
Reuses the metaball / primitive helpers from build_rabbits.py.
"""
import os, sys, math
_src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), 'build_rabbits.py'), encoding='utf8').read()
exec(_src[:_src.index("root = {'A': moca")])   # helpers + earlier designs (no export)
from mathutils import Vector


def rivet_row(prefix, pts, m, parent, r=0.013):
    for i, p in enumerate(pts): prim('sphere', f'{prefix}{i}', m, p, scale=(r, r, r), parent=parent)


def key(body, metal_m, y=0.16, z=0.56, big=1.0):
    spin = empty('Spinner', (0, y, z), parent=body)
    prim('cyl', 'KeyCollar', metal_m, (0, y, z + 0.01), scale=(0.05 * big, 0.05 * big, 0.016), parent=spin)
    prim('cyl', 'KeyShaft', metal_m, (0, y, z + 0.08 * big), scale=(0.024, 0.024, 0.07 * big), parent=spin)
    for s in (-1, 1):
        prim('torus', f'KeyBow{s}', metal_m, (0.1 * s * big, y, z + 0.2 * big), scale=(0.08 * big,) * 3, rot=(0, 90, 0), parent=spin,
             major_radius=1.0, minor_radius=0.27)
    prim('sphere', 'KeyHub', metal_m, (0, y, z + 0.2 * big), scale=(0.035,) * 3, parent=spin)


def lens_eyes(head, glass_m, glint_m, bezel_m, x, y, z, r=0.05, lateral=False):
    for s, n in ((-1, 'EyeL'), (1, 'EyeR')):
        if lateral:
            pos = (s * x, y, z); rot = (0, 90, 0)
        else:
            pos = (s * x, y, z); rot = (90, 0, 0)
        prim('torus', f'Bezel{s}', bezel_m, pos, scale=(r * 1.25,) * 3, rot=rot, parent=head, major_radius=1.0, minor_radius=0.22)
        e = prim('sphere', n, glass_m, pos, scale=(r, r * 0.55, r) if not lateral else (r * 0.55, r, r), parent=head)
        finish(e, n, None, origin=pos)
        g = (pos[0] + (0.016 if not lateral else 0.012 * s), pos[1] - (0.03 if not lateral else 0.012), pos[2] + 0.018) if not lateral else \
            (pos[0] + 0.022 * s, pos[1] - 0.012, pos[2] + 0.016)
        prim('sphere', f'Glint{s}', glint_m, g, scale=(r * 0.28,) * 3, parent=e)


def vintage():
    tin = mat('Body', srgb('#F1E6D0'), 0.35, 0.15)
    paint = mat('Accent', srgb('#D2483B'), 0.4, 0.1)
    metal = mat('Metal', srgb('#CFA453'), 0.3, 1.0)
    dark = mat('Visor', srgb('#1E2230'), 0.15)
    glint = mat('Glow', srgb('#FFFFFF'), 0.1, emit=2)
    root = empty('Rabbit', (0, 0, 0))
    body = meta('Body', [
        ('CUBE', (0, 0.1, 0.27), 0.12, (0.07, 0.12, 0.05), None),                       # round tin torso
        ('ELLIPSOID', (0.13, 0.18, 0.15), 0.1, (0.75, 1.2, 0.9), None),                # haunches
        ('ELLIPSOID', (-0.13, 0.18, 0.15), 0.1, (0.75, 1.2, 0.9), None),
    ], tin, origin=(0, 0, 0), parent=root)
    prim('cyl', 'SeamBand', paint, (0, 0.06, 0.27), scale=(0.205, 0.205, 0.03), rot=(90, 0, 0), parent=body)
    rivet_row('Rivet', [(0.0, 0.06, 0.48), (0.15, 0.06, 0.42), (-0.15, 0.06, 0.42)], metal, body)
    for s in (-1, 1):
        prim('sphere', f'Foot{s}', paint, (0.15 * s, 0.02, 0.04), scale=(0.08, 0.15, 0.045), parent=body)
        prim('cyl', f'Paw{s}', metal, (0.1 * s, -0.17, 0.08), scale=(0.035, 0.035, 0.08), parent=body)
        prim('sphere', f'PawTip{s}', paint, (0.1 * s, -0.19, 0.03), scale=(0.055, 0.07, 0.035), parent=body)
        prim('cyl', f'HipBolt{s}', metal, (0.205 * s, 0.18, 0.16), scale=(0.05, 0.05, 0.015), rot=(0, 90, 0), parent=body)
    prim('sphere', 'Tail', tin, (0, 0.36, 0.28), scale=(0.07,) * 3, parent=body)
    head = meta('Head', [('CUBE', (0, -0.16, 0.66), 0.13, (0.1, 0.08, 0.08), None)], tin, origin=(0, -0.12, 0.47), parent=body)
    fy = front_y(head, 0, 0.62) or -0.4
    prim('sphere', 'Muzzle', tin, (0, fy + 0.02, 0.58), scale=(0.1, 0.06, 0.07), parent=head)
    prim('sphere', 'Nose', paint, (0, fy - 0.03, 0.6), scale=(0.025, 0.02, 0.02), parent=head)
    for s in (-1, 1): prim('sphere', f'Cheek{s}', paint, (0.14 * s, fy + 0.03, 0.59), scale=(0.045, 0.012, 0.03), parent=head)
    lens_eyes(head, dark, glint, metal, 0.095, fy + 0.012, 0.69, r=0.048)
    for s, n in ((-1, 'EarL'), (1, 'EarR')):
        base = (0.09 * s, -0.12, 0.86)
        ear = empty(n, base, parent=head)
        prim('cyl', f'{n}Hinge', metal, base, scale=(0.03, 0.03, 0.04), rot=(0, 90, 0), parent=ear)
        prim('cube', f'{n}Plate', tin, (base[0] + 0.02 * s, base[1] + 0.02, base[2] + 0.21), scale=(0.07, 0.022, 0.2), rot=(-10, 10 * s, 0), parent=ear, bevel=(0.06, 5))
        prim('cube', f'{n}Inner', paint, (base[0] + 0.02 * s, base[1] + 0.0, base[2] + 0.21), scale=(0.04, 0.012, 0.14), rot=(-10, 10 * s, 0), parent=ear, bevel=(0.035, 4))
    key(body, metal, y=0.13, z=0.46)
    return root


def plush():
    felt = mat('Body', srgb('#DCD1C1'), 0.9)
    inner = mat('Accent', srgb('#E9B9A8'), 0.9)
    metal = mat('Metal', srgb('#CFA453'), 0.3, 1.0)
    dark = mat('Visor', srgb('#22202A'), 0.2)
    glint = mat('Glow', srgb('#FFFFFF'), 0.1, emit=2)
    root = empty('Rabbit', (0, 0, 0))
    body = meta('Body', [
        ('ELLIPSOID', (0, 0.1, 0.27), 0.27, (0.85, 1.05, 0.85), None),
        ('ELLIPSOID', (0.14, 0.17, 0.15), 0.15, (0.8, 1.15, 0.9), None),
        ('ELLIPSOID', (-0.14, 0.17, 0.15), 0.15, (0.8, 1.15, 0.9), None),
        ('ELLIPSOID', (0.13, -0.01, 0.04), 0.09, (0.8, 1.7, 0.5), None),
        ('ELLIPSOID', (-0.13, -0.01, 0.04), 0.09, (0.8, 1.7, 0.5), None),
        ('ELLIPSOID', (0.09, -0.17, 0.07), 0.07, (0.85, 1.1, 1.2), None),
        ('ELLIPSOID', (-0.09, -0.17, 0.07), 0.07, (0.85, 1.1, 1.2), None),
        ('BALL', (0, 0.4, 0.27), 0.08, None, None),
    ], felt, origin=(0, 0, 0), parent=root)
    for s in (-1, 1):
        prim('cyl', f'HipCap{s}', metal, (0.235 * s, 0.17, 0.17), scale=(0.07, 0.07, 0.016), rot=(0, 90, 0), parent=body)
        prim('cyl', f'ShoulderCap{s}', metal, (0.17 * s, -0.1, 0.27), scale=(0.045, 0.045, 0.014), rot=(0, 90, 0), parent=body)
    head = meta('Head', [
        ('ELLIPSOID', (0, -0.16, 0.62), 0.24, (1.0, 0.95, 0.88), None),
        ('ELLIPSOID', (0, -0.33, 0.55), 0.1, (1.0, 0.8, 0.75), None),
    ], felt, origin=(0, -0.12, 0.42), parent=body)
    fy = front_y(head, 0.1, 0.64) or -0.38
    for s, n in ((-1, 'EyeL'), (1, 'EyeR')):        # button eyes with brass rims
        pos = (0.1 * s, fy + 0.012, 0.64)
        prim('torus', f'Rim{s}', metal, pos, scale=(0.05,) * 3, rot=(90, 0, 0), parent=head, major_radius=1.0, minor_radius=0.2)
        e = prim('sphere', n, dark, pos, scale=(0.042, 0.022, 0.042), parent=head); finish(e, n, None, origin=pos)
        prim('sphere', f'Glint{s}', glint, (pos[0] + 0.012, pos[1] - 0.02, pos[2] + 0.014), scale=(0.011,) * 3, parent=e)
    prim('sphere', 'Nose', inner, (0, -0.43, 0.56), scale=(0.026, 0.02, 0.02), parent=head)
    for s, n in ((-1, 'EarL'), (1, 'EarR')):
        base = (0.09 * s, -0.14, 0.82)
        ear = meta(n, [
            ('ELLIPSOID', (base[0] + 0.01 * s, base[1] + 0.02, base[2] + 0.16), 0.14, (0.6, 0.32, 1.25), (-8, 8 * s, 0)),
            ('ELLIPSOID', (base[0] + 0.03 * s, base[1] + 0.05, base[2] + 0.36), 0.12, (0.6, 0.32, 1.1), (-14, 10 * s, 0)),
        ], felt, origin=base, parent=head)
        prim('sphere', f'{n}Inner', inner, (base[0] + 0.015 * s, base[1] - 0.012, base[2] + 0.25), scale=(0.045, 0.012, 0.16), rot=(-10, 0, 8 * s), parent=ear)
        prim('cyl', f'{n}Hinge', metal, base, scale=(0.035, 0.035, 0.05), rot=(0, 90, 0), parent=ear)
        prim('cone', f'{n}Tip', metal, (base[0] + 0.045 * s, base[1] + 0.07, base[2] + 0.5), scale=(0.05, 0.03, 0.05), rot=(-14, 10 * s, 0), parent=ear)
    key(body, metal, y=0.12, z=0.5)
    return root


def roundparts():
    body_m = mat('Body', srgb('#EEE8DC'), 0.4, 0.1)
    seam = mat('Accent', srgb('#3A3E48'), 0.4, 0.4)
    metal = mat('Metal', srgb('#CFA453'), 0.3, 1.0)
    glass = mat('Visor', srgb('#1C2030'), 0.1)
    glint = mat('Glow', srgb('#FFD48A'), 0.1, emit=2.5)
    inner = mat('Inner', srgb('#E6BFAE'), 0.6)
    root = quad('#EEE8DC', '#E3DCCD', '#1C2030', False, '#E6BFAE')   # base silhouette (named materials reused)
    body = bpy.data.objects['Body']; head = bpy.data.objects['Head']
    for n in ('EyeL', 'EyeR'):
        ob = bpy.data.objects.get(n)
        if ob: bpy.data.objects.remove(ob)
    prim('torus', 'SeamBand', seam, (0, 0.06, 0.36), scale=(0.24, 0.27, 0.24), rot=(90, 0, 0), parent=body, major_radius=1.0, minor_radius=0.06)
    rivet_row('Rivet', [(0.0, 0.0, 0.62), (0.2, 0.06, 0.52), (-0.2, 0.06, 0.52), (0.26, 0.06, 0.36), (-0.26, 0.06, 0.36)], metal, body)
    sx = side_x(head, -0.4, 0.69) or 0.13
    lens_eyes(head, glass, glint, metal, sx + 0.005, -0.4, 0.69, r=0.04, lateral=True)
    return root


root = {'M': vintage, 'N': plush, 'O': roundparts}[VARIANT]()
print(f'[toy {VARIANT}] objects={len(bpy.data.objects)}')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f'Rabbit_{VARIANT}.fbx'), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=False, object_types={'MESH', 'EMPTY'},
                         mesh_smooth_type='FACE', add_leaf_bones=False)
cam_data = bpy.data.cameras.new('cam'); cam_data.lens = 70
cam = bpy.data.objects.new('cam', cam_data); col.objects.link(cam)
cam.location = (2.2, -3.2, 1.6); cam.rotation_euler = (Vector((0, -0.05, 0.45)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam; scene.render.engine = 'BLENDER_WORKBENCH'
sh = scene.display.shading; sh.light = 'STUDIO'; sh.color_type = 'MATERIAL'; sh.show_cavity = True; sh.cavity_type = 'BOTH'
sh.show_object_outline = True; sh.show_specular_highlight = True
scene.display.render_aa = '8'; scene.render.resolution_x = scene.render.resolution_y = 900
scene.world = bpy.data.worlds.new('w'); scene.world.color = (0.2, 0.19, 0.22)
scene.render.filepath = os.path.join(OUT, f'preview_{VARIANT}.png'); bpy.ops.render.render(write_still=True)
