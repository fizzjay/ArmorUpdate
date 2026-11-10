"""Full assembly nail approach, including workbench-up orientation."""
import io,struct,math
from tools.crafting_geometry import cross,ray_distance

def plans(path):
 s=io.BytesIO(path.read_bytes());s.read(40)
 def r(f): return struct.unpack('<'+f,s.read(struct.calcsize('<'+f)))
 def text():return s.read(r('i')[0]).decode()
 for _ in range(r('i')[0]):
  name=text();text();parts=[]
  for j in range(r('i')[0]):
   src,parent,level,nail=r('Iii?');r('7fi');vs=[r('8f')[:3] for _ in range(r('i')[0])];tris=[]
   for _ in range(r('i')[0]):
    ids=r(str(r('i')[0])+'i')
    tris += [tuple(vs[i] for i in ids[n:n+3]) for n in range(0,len(ids),3)]
   parts.append((nail,vs,tris))
  yield name,parts

def dot(a,b):return sum(x*y for x,y in zip(a,b))
def candidate_surfaces(parts,index):
 surfaces=[t for _,_,ts in parts for t in ts]
 center=[(min(v[a] for _,vs,_ in parts for v in vs)+max(v[a] for _,vs,_ in parts for v in vs))/2 for a in range(3)]
 candidates=[]
 for a,b,c in parts[index][2]:
  n=cross([b[i]-a[i] for i in range(3)],[c[i]-a[i] for i in range(3)]);area=math.sqrt(dot(n,n))
  if area<1e-8:continue
  n=[x/area for x in n];point=[(a[i]+b[i]+c[i])/3 for i in range(3)]
  if dot(n,[point[i]-center[i] for i in range(3)])<-.0001:continue
  candidates.append((area,point,n))
 return sorted(candidates,key=lambda t:-t[0]),surfaces

def choose(candidates,surfaces,up):
 for _,p,n in candidates:
  if dot(n,up)<.25:continue
  tangent=cross(n,(0,1,0))
  if dot(tangent,tangent)<.01:tangent=cross(n,(1,0,0))
  length=math.sqrt(dot(tangent,tangent));tangent=[x/length for x in tangent];bt=cross(n,tangent)
  offsets=[(0,0,0),*[tuple(sign*.015*x for x in v) for v in (tangent,bt) for sign in (1,-1)]]
  if all(not any((d:=ray_distance([p[i]+off[i]+n[i]*.002 for i in range(3)],n,t)) is not None and d>.001 for t in surfaces) for off in offsets):return p,n
 return None

# Prefer original workbench-up; choose another complete-jig orientation only
# when an ingredient cannot be reached from above in that orientation.
DIRECTIONS=[(0,1,0),(0,0,1),(0,0,-1),(1,0,0),(-1,0,0),(0,-1,0)]
DIRECTIONS += [(x,y,z) for x in (-1,0,1) for y in (1,0,-1) for z in (-1,0,1) if sum(v!=0 for v in (x,y,z))>1]

def orientation(parts):
 required=[candidate_surfaces(parts,i) for i,p in enumerate(parts) if p[0]]
 for up in DIRECTIONS:
  length=math.sqrt(dot(up,up));normal=tuple(x/length for x in up)
  chosen=[choose(cs,ss,normal) for cs,ss in required]
  if all(chosen):return up,chosen
 raise ValueError('No common above-table approach for complete jig')
