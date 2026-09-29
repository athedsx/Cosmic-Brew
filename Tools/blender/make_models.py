# Gera modelos low poly pastel para Cosmic Brew e exporta em FBX.
# Uso: blender --background --python Tools/blender/make_models.py -- <pasta_saida>
# Convenção: frente do modelo virada para -Y no Blender (vira +Z no Unity), pivô nos pés (z = 0), metros.
import bpy, sys, os, math
from mathutils import Vector

out_dir = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.getcwd()
os.makedirs(out_dir, exist_ok=True)
MATS = {}

def reset():
    MATS.clear()
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.materials):
        for b in list(block):
            if b.users == 0:
                block.remove(b)

def mat(name, rgb, emit=0.0, rough=0.7, metal=0.0):
    if name in MATS:
        return MATS[name]
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    col = (*rgb, 1.0)
    bsdf.inputs["Base Color"].default_value = col
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit > 0:
        bsdf.inputs["Emission Color"].default_value = col
        bsdf.inputs["Emission Strength"].default_value = emit
    m.diffuse_color = col
    MATS[name] = m
    return m

def finish(obj, material, flat=True):
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for p in obj.data.polygons:
        p.use_smooth = not flat
    return obj

def sphere(loc, scale, material, seg=14, rings=9, flat=False):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=loc)
    o = bpy.context.object
    o.scale = scale
    return finish(o, material, flat)

def cyl(loc, radius, depth, material, verts=14, rot=(0, 0, 0), flat=False):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.object, material, flat)

def cone(loc, r1, r2, depth, material, verts=12, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    return finish(bpy.context.object, material, True)

def box(loc, size, material, bevel=0.0, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = o.modifiers.new("bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        bpy.ops.object.modifier_apply(modifier="bevel")
    return finish(o, material, False)

def torus(loc, major, minor, material, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=24, minor_segments=8, location=loc, rotation=rot)
    return finish(bpy.context.object, material, False)

def export(name):
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.join()
    o = bpy.context.object
    o.name = name
    # pivô nos pés, no centro
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    path = os.path.join(out_dir, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", bake_space_transform=True)
    print("EXPORTADO", path)

# ------------------------------------------------------------------ astronauta perdido
def astronaut():
    reset()
    suit = mat("Astro_Suit", (0.97, 0.94, 0.9))
    trim = mat("Astro_Trim", (1.0, 0.66, 0.58))
    visor = mat("Astro_Visor", (0.36, 0.33, 0.62), rough=0.15, metal=0.3)
    glow = mat("Astro_Glow", (0.55, 0.92, 1.0), emit=2.0)
    boot = mat("Astro_Boot", (0.55, 0.5, 0.68))
    # pernas e botas
    for x in (-0.13, 0.13):
        cyl((x, 0, 0.26), 0.1, 0.34, suit)
        box((x, -0.03, 0.07), (0.2, 0.28, 0.14), boot, bevel=0.05)
    # corpo
    sphere((0, 0, 0.62), (0.3, 0.26, 0.3), suit)
    cyl((0, 0, 0.47), 0.29, 0.07, trim)                      # cinto
    box((0, -0.24, 0.66), (0.2, 0.06, 0.14), trim, bevel=0.03)  # painel no peito
    sphere((-0.05, -0.28, 0.68), (0.025, 0.02, 0.025), glow)
    sphere((0.05, -0.28, 0.68), (0.025, 0.02, 0.025), mat("Astro_Btn", (1.0, 0.8, 0.45), emit=1.5))
    # mochila
    box((0, 0.25, 0.7), (0.4, 0.18, 0.44), mat("Astro_Pack", (0.8, 0.76, 0.95)), bevel=0.06)
    cyl((0.12, 0.34, 0.95), 0.03, 0.18, trim)
    # braços
    for s in (-1, 1):
        sphere((s * 0.33, -0.02, 0.62), (0.09, 0.09, 0.2), suit)
        sphere((s * 0.36, -0.06, 0.44), (0.075, 0.075, 0.075), trim)
    # capacete grande
    sphere((0, 0, 1.02), (0.34, 0.34, 0.32), suit, seg=18, rings=12)
    sphere((0, -0.12, 1.03), (0.27, 0.25, 0.22), visor, seg=18, rings=12)
    torus((0, 0, 0.78), 0.2, 0.05, trim)
    # anteninha com luz
    cyl((0.2, 0.05, 1.36), 0.012, 0.18, boot)
    sphere((0.2, 0.05, 1.46), (0.04, 0.04, 0.04), glow)
    export("Astronaut")

# ------------------------------------------------------------------ robô cliente
def robot():
    reset()
    shell = mat("Bot_Shell", (0.64, 0.92, 0.8))
    dark = mat("Bot_Dark", (0.3, 0.27, 0.45), rough=0.4)
    eye = mat("Bot_Eye", (1.0, 0.86, 0.5), emit=3.0)
    pink = mat("Bot_Pink", (1.0, 0.7, 0.8))
    glow = mat("Bot_Glow", (0.6, 0.9, 1.0), emit=2.5)
    # base flutuante (anel brilhante) e corpo redondo
    torus((0, 0, 0.18), 0.2, 0.05, glow)
    cone((0, 0, 0.3), 0.12, 0.26, 0.2, dark)
    sphere((0, 0, 0.72), (0.4, 0.4, 0.38), shell, seg=18, rings=12)
    cyl((0, 0, 0.72), 0.405, 0.06, pink, verts=24)            # faixa
    # olho grande
    cyl((0, -0.33, 0.8), 0.19, 0.12, dark, verts=20, rot=(math.radians(90), 0, 0))
    cyl((0, -0.39, 0.8), 0.13, 0.04, eye, verts=20, rot=(math.radians(90), 0, 0))
    sphere((0.05, -0.41, 0.84), (0.035, 0.02, 0.035), mat("Bot_Shine", (1, 1, 1), emit=1.0))
    # bracinhos
    for s in (-1, 1):
        cyl((s * 0.44, -0.05, 0.62), 0.03, 0.2, dark, rot=(0, math.radians(90), 0))
        sphere((s * 0.54, -0.07, 0.6), (0.07, 0.07, 0.07), pink)
    # antena
    cyl((0, 0, 1.16), 0.018, 0.2, dark)
    sphere((0, 0, 1.28), (0.06, 0.06, 0.06), pink)
    export("RobotCustomer")

# ------------------------------------------------------------------ nave pequena
def ship():
    reset()
    hull = mat("Ship_Hull", (1.0, 0.8, 0.7))
    stripe = mat("Ship_Stripe", (0.78, 0.7, 0.96))
    glass = mat("Ship_Glass", (0.5, 0.78, 0.95), rough=0.1, metal=0.2)
    fire = mat("Ship_Fire", (1.0, 0.7, 0.45), emit=4.0)
    dark = mat("Ship_Dark", (0.34, 0.3, 0.48))
    # casco em forma de gota, deitado ao longo de Y (frente em -Y)
    sphere((0, 0, 0.55), (0.55, 1.0, 0.45), hull, seg=20, rings=12)
    torus((0, 0, 0.55), 0.52, 0.06, stripe, rot=(0, 0, 0))
    sphere((0, -0.35, 0.85), (0.3, 0.42, 0.25), glass, seg=18, rings=10)
    # asas e aletas
    for s in (-1, 1):
        box((s * 0.62, 0.25, 0.45), (0.5, 0.5, 0.06), stripe, bevel=0.03, rot=(0, math.radians(s * -12), 0))
        cyl((s * 0.86, 0.35, 0.45), 0.08, 0.35, hull, rot=(math.radians(90), 0, 0))
    box((0, 0.75, 0.95), (0.06, 0.45, 0.4), stripe, bevel=0.03, rot=(math.radians(-20), 0, 0))
    # motor
    cyl((0, 0.98, 0.55), 0.22, 0.2, dark, rot=(math.radians(90), 0, 0))
    cone((0, 1.18, 0.55), 0.16, 0.02, 0.3, fire, rot=(math.radians(-90), 0, 0))
    # pezinhos de pouso
    for x, y in ((-0.35, -0.4), (0.35, -0.4), (0, 0.55)):
        cyl((x, y, 0.1), 0.03, 0.22, dark)
        cyl((x, y, 0.02), 0.08, 0.04, dark)
    export("SmallShip")

# ------------------------------------------------------------------ estação: vaporizador de Leite Nebuloso
def milk_table():
    reset()
    top = mat("Milk_TableTop", (1.0, 0.8, 0.86))
    leg = mat("Milk_TableLeg", (0.66, 0.6, 0.82))
    cyl((0, 0, 0.84), 0.5, 0.08, top, verts=24)
    torus((0, 0, 0.84), 0.5, 0.04, leg)
    cyl((0, 0, 0.42), 0.07, 0.8, leg)
    cyl((0, 0, 0.03), 0.3, 0.06, leg, verts=20)
    export("MilkTable")

def milk_steamer():
    reset()
    body = mat("Milk_Body", (0.8, 0.72, 0.97))
    cream = mat("Milk_Cream", (1.0, 0.95, 0.9))
    metal = mat("Milk_Metal", (0.86, 0.86, 0.92), rough=0.3, metal=0.6)
    glow = mat("Milk_Glow", (1.0, 0.72, 0.88), emit=2.5)
    # tanque arredondado com cúpula de vidro de "nebulosa"
    cyl((0, 0, 0.2), 0.22, 0.4, body, verts=20)
    sphere((0, 0, 0.4), (0.22, 0.22, 0.12), body, seg=20, rings=10)
    sphere((0, 0, 0.55), (0.14, 0.14, 0.16), glow, seg=16, rings=10)
    torus((0, 0, 0.42), 0.2, 0.03, cream)
    cyl((0, 0, 0.02), 0.25, 0.04, cream, verts=20)
    # bico de vapor e jarrinha
    cyl((0.12, -0.24, 0.3), 0.02, 0.22, metal, rot=(math.radians(35), 0, 0))
    cyl((0.12, -0.33, 0.1), 0.07, 0.16, metal, verts=16)
    torus((0.12, -0.33, 0.18), 0.07, 0.012, metal)
    # botões
    sphere((-0.1, -0.21, 0.28), (0.035, 0.02, 0.035), mat("Milk_Btn", (0.6, 0.92, 0.8), emit=1.2))
    sphere((-0.02, -0.22, 0.28), (0.035, 0.02, 0.035), mat("Milk_Btn2", (1.0, 0.85, 0.5), emit=1.2))
    export("MilkSteamer")

# ------------------------------------------------------------------ lixeira (corpo + tampa separada)
def trash():
    reset()
    body = mat("Trash_Body", (0.66, 0.9, 0.82))
    band = mat("Trash_Band", (1.0, 0.72, 0.62))
    dark = mat("Trash_Dark", (0.34, 0.3, 0.48))
    cyl((0, 0, 0.4), 0.3, 0.72, body, verts=20)
    cyl((0, 0, 0.4), 0.305, 0.1, band, verts=20)
    torus((0, 0, 0.76), 0.29, 0.03, band)
    cyl((0, 0, 0.03), 0.26, 0.06, dark, verts=20)
    for a in range(3):  # símbolo de reciclagem simplificado: três estrelinhas
        ang = math.radians(90 + a * 120)
        sphere((math.cos(ang) * 0.08, -0.3, 0.52 + math.sin(ang) * 0.06), (0.03, 0.015, 0.03), mat("Trash_Star", (1.0, 0.86, 0.5), emit=1.0))
    export("TrashBody")
    reset()
    cyl((0, 0, 0.04), 0.32, 0.08, mat("Trash_Lid", (0.8, 0.72, 0.97)), verts=20)
    sphere((0, 0, 0.1), (0.3, 0.3, 0.06), mat("Trash_Lid", (0.8, 0.72, 0.97)), seg=20, rings=8)
    cyl((0, 0, 0.18), 0.03, 0.08, mat("Trash_Dark", (0.34, 0.3, 0.48)))
    sphere((0, 0, 0.24), (0.07, 0.07, 0.05), mat("Trash_Band", (1.0, 0.72, 0.62)))
    export("TrashLid")

astronaut()
robot()
ship()
milk_table()
milk_steamer()
trash()
