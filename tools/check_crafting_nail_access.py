"""Audit full-assembly exterior hammer clearance for all shipped required nails."""
import struct,io,sys,math
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from tools.crafting_geometry import cross,ray_distance
s=io.BytesIO((ROOT/'crafting/construction.bin').read_bytes());s.read(40)
def r(f):return struct.unpack('<'+f,s.read(struct.calcsize('<'+f)))
def text():return s.read(r('i')[0]).decode()
for _ in range(r('i')[0]):
 name,glyph=text(),text();parts=[];surfaces=[]
 for j in range(r('i')[0]):
  src,parent,level,nail=r('Iii?');r('7fi');vs=[r('8f')[:3] for k in range(r('i')[0])];tris=[]
  for k in range(r('i')[0]):
   ids=r(str(r('i')[0])+'i')
   tris += [tuple(vs[i] for i in ids[n:n+3]) for n in range(0,len(ids),3)]
  parts.append((nail,vs,tris));surfaces+=tris
 center=[(min(v[a] for _,vs,_ in parts for v in vs)+max(v[a] for _,vs,_ in parts for v in vs))/2 for a in range(3)]
 for j,(nail,vs,tris) in enumerate(parts):
  if not nail:continue
  ok=False
  for a,b,c in tris:
   normal=cross([b[i]-a[i] for i in range(3)],[c[i]-a[i] for i in range(3)]);area=math.sqrt(sum(x*x for x in normal))
   if area<1e-8:continue
   normal=[x/area for x in normal];point=[(a[i]+b[i]+c[i])/3 for i in range(3)]
   if sum(normal[i]*(point[i]-center[i]) for i in range(3))<-.0001:continue
   tangent=cross(normal,(0,1,0))
   if sum(x*x for x in tangent)<.01:tangent=cross(normal,(1,0,0))
   length=math.sqrt(sum(x*x for x in tangent));tangent=[x/length for x in tangent];bt=cross(normal,tangent)
   offsets=[(0,0,0),*[tuple(sign*.015*x for x in v) for v in (tangent,bt) for sign in (1,-1)]]
   if all(not any((d:=ray_distance([point[i]+off[i]+normal[i]*.002 for i in range(3)],normal,t)) is not None and d>.001 for t in surfaces) for off in offsets):ok=True;break
  print(name,j,'OK' if ok else 'FAIL')
  if not ok:sys.exit(1)
