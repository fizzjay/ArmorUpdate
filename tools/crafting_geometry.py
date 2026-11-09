"""Eight-slot construction adaptation; never touches worn armor geometry."""
import math


def half_mesh(part, sign):
    """Clip closure triangles at x=0, interpolating normal and UV attributes."""
    result = dict(vertices=[], normals=[], uv=[], submeshes=[])
    for sub in part['submeshes']:
        triangles = []
        for tri in sub:
            polygon = [(*part['vertices'][i], *part['normals'][i], *part['uv'][i]) for i in tri]
            if sign < 0 and all(v[0] == 0 for v in polygon):
                continue
            clipped = []
            for a, b in zip(polygon, polygon[1:] + polygon[:1]):
                inside_a, inside_b = a[0] * sign >= 0, b[0] * sign >= 0
                if inside_a:
                    clipped.append(a)
                if inside_a != inside_b:
                    t = -a[0] / (b[0] - a[0])
                    clipped.append(tuple(x + t * (y - x) for x, y in zip(a, b)))
            for i in range(1, len(clipped) - 1):
                corners = (clipped[0], clipped[i], clipped[i + 1])
                if area(corners) < 1e-12:
                    continue
                start = len(result['vertices'])
                for vertex in corners:
                    result['vertices'].append(vertex[:3])
                    normal = vertex[3:6]
                    length = math.sqrt(sum(x*x for x in normal))
                    if length < 1e-8:
                        raise ValueError('Degenerate interpolated closure normal')
                    result['normals'].append(tuple(x / length for x in normal))
                    result['uv'].append(vertex[6:])
                triangles.append([start, start + 1, start + 2])
        result['submeshes'].append(triangles)
    if not result['vertices']:
        raise ValueError('Empty split tasset closure')
    return result


def cross(a, b):
    return (a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0])


def area(triangle):
    a, b, c = triangle
    normal = cross([b[i]-a[i] for i in range(3)], [c[i]-a[i] for i in range(3)])
    return math.sqrt(sum(x*x for x in normal)) / 2


def ray_distance(origin, direction, triangle):
    a,b,c=triangle
    e1=[b[i]-a[i] for i in range(3)];e2=[c[i]-a[i] for i in range(3)]
    h=cross(direction,e2);det=sum(e1[i]*h[i] for i in range(3))
    if abs(det)<1e-10:return None
    inverse=1/det;s=[origin[i]-a[i] for i in range(3)]
    u=inverse*sum(s[i]*h[i] for i in range(3))
    if u<0 or u>1:return None
    q=cross(s,e1);v=inverse*sum(direction[i]*q[i] for i in range(3))
    if v<0 or u+v>1:return None
    distance=inverse*sum(e2[i]*q[i] for i in range(3))
    return distance if distance>.001 else None


def nail_pose(part, blockers=()):
    """Seat an exposed nail on the largest non-degenerate surface triangle.

    Split hips cannot reuse donor nails belonging to the opposite half. These
    generated nail poses require hammer-access verification in the actual game.
    """
    triangles = [[part['vertices'][i] for i in tri] for sub in part['submeshes'] for tri in sub]
    obstacles=[triangle for mesh in (part,*blockers) for sub in mesh['submeshes']
               for tri in sub for triangle in [[mesh['vertices'][i] for i in tri]]]
    triangle=None
    for candidate in sorted(triangles,key=area,reverse=True)[:128]:
        a,b,c=candidate
        n=cross([b[i]-a[i] for i in range(3)],[c[i]-a[i] for i in range(3)])
        length=math.sqrt(sum(x*x for x in n))
        if length<1e-10:continue
        n=[x/length for x in n]
        center=[sum(v[i] for v in candidate)/3 for i in range(3)]
        if all((d:=ray_distance(center,n,t)) is None or d>=.12 for t in obstacles):
            triangle=candidate;break
    if triangle is None:raise ValueError('No clear 12 cm nail approach on ingredient')
    if area(triangle) < 1e-12:
        raise ValueError('No surface for tasset nail')
    a, b, c = triangle
    normal = cross([b[i]-a[i] for i in range(3)], [c[i]-a[i] for i in range(3)])
    length = math.sqrt(sum(x*x for x in normal))
    normal = [x / length for x in normal]
    # Quaternion rotating the native nail's forward axis onto the outward normal.
    rotation = [-normal[1], normal[0], 0., 1. + normal[2]]
    length = math.sqrt(sum(x*x for x in rotation))
    rotation = [1., 0., 0., 0.] if length < 1e-8 else [x / length for x in rotation]
    return (dict(zip('xyz', [sum(v[i] for v in triangle)/3 for i in range(3)])),
            dict(zip('xyzw', rotation)))


def split_tassets(record):
    parts, original = record['parts'], record['plan']
    if len(parts) != 5:
        raise ValueError('Expected four tasset panels and one closure')
    results = []
    for side, sign, indices in (('l', 1, (0, 1)), ('r', -1, (2, 3))):
        chosen = [parts[i] for i in indices] + [half_mesh(parts[4], sign)]
        for part in chosen[:2]:
            if any(v[0] * sign < -1e-6 for v in part['vertices']):
                raise ValueError('Tasset panel crosses the expected body side')
        poses = [nail_pose(p) for p in chosen]
        plan = dict(sources=[original['sources'][i] for i in indices] + [original['sources'][4]],
                    parents=[-1, 0, 1], levels=[0, 1, 2], requiresNails=[False, True, True],
                    nailPositions=[p[0] for p in poses], nailRotations=[p[1] for p in poses],
                    clearanceDegrees=[-2, 45, 45])
        results.append(dict(id=record['id'].replace('.tassets', '.tasset_' + side),
                            glyph=record['glyph'], parts=chosen, plan=plan))
    return results
