"""Validate the supplied ASCII FBX assets without Unity or external uploads.
Produces an import audit, not a runtime armor/recipe registration.
"""
import base64,hashlib,json,re,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def inspect(path):
 raw=path.read_bytes();text=raw.decode('utf-8-sig')
 if not text.startswith('; FBX 7.7.0'):raise ValueError('Expected supplied ASCII FBX 7.7')
 objects={}
 for kind,identifier,name,subtype,body in re.findall(r'^\t(\w+): (\d+), "([^"\n]*)", "([^"\n]*)" \{\n(.*?)^\t\}',text,re.M|re.S):
  key=int(identifier)
  if key in objects:raise ValueError('Duplicate FBX object ID')
  objects[key]=(kind,name.split('::',1)[-1],subtype,body)
 connections=[(typ,int(child),int(parent),prop) for typ,child,parent,prop in re.findall(r'^\s*C: "(OO|OP)",(\d+),(\d+)([^\n]*)',text,re.M)]
 for _,child,parent,_ in connections:
  if child not in objects or parent!=0 and parent not in objects:raise ValueError('Unresolved FBX connection')
 textures=[];meshes=[];bones=[];materials=[]
 for key,(kind,name,subtype,body) in objects.items():
  if kind=='Video':
   content=re.search(r'Content:\s*,\s*"([A-Za-z0-9+/=\s]+)"',body)
   if not content:raise ValueError('Missing embedded texture '+name)
   png=base64.b64decode(re.sub(r'\s','',content.group(1)),validate=True)
   if png[:8]!=b'\x89PNG\r\n\x1a\n' or png[12:16]!=b'IHDR':raise ValueError('Non-PNG embedded image')
   width,height=struct.unpack('>II',png[16:24])
   if not 0<width<=4096 or not 0<height<=4096:raise ValueError('Unbounded embedded texture')
   textures.append(dict(name=name,sha256=hashlib.sha256(png).hexdigest(),width=width,height=height,bytes=len(png)))
  if kind=='Geometry':
   def array(name):
    m=re.search(r'\b'+name+r': \*(\d+) \{\s*a: ([^}]+)\}',body)
    if not m:raise ValueError('Missing geometry array '+name)
    values=[float(v) if name=='Vertices' else int(v) for v in m.group(2).strip().split(',')]
    if len(values)!=int(m.group(1)):raise ValueError('Geometry count mismatch')
    return values
   vs,indices=array('Vertices'),array('PolygonVertexIndex')
   if len(vs)%3:raise ValueError('Non-xyz vertices')
   polygons=[];poly=[]
   for i in indices:
    vertex=-i-1 if i<0 else i
    if not 0<=vertex<len(vs)//3:raise ValueError('Bad vertex index')
    poly.append(vertex)
    if i<0:
     if len(poly)<3:raise ValueError('Degenerate polygon')
     polygons.append(poly);poly=[]
   if poly:raise ValueError('Unterminated polygon')
   models=[objects[parent][1] for typ,child,parent,_ in connections if typ=='OO' and child==key and parent in objects and objects[parent][0]=='Model']
   meshes.append(dict(models=models,vertices=len(vs)//3,polygons=len(polygons),triangles=sum(len(p)-2 for p in polygons)))
  if kind=='Model' and name.startswith('ATTBone_'):bones.append(name)
  if kind=='Material':materials.append(name)
 return dict(file=path.name,sha256=hashlib.sha256(raw).hexdigest(),meshes=meshes,
             mesh_count=len(meshes),triangles=sum(m['triangles'] for m in meshes),
             attachment_helpers=bones,materials=materials,embedded_textures=textures,
             skin_deformers=sum(k=='Deformer' for k,_,_,_ in objects.values()))

def audit():
 files=sorted((ROOT/'hebios armor').glob('*.fbx'))
 if len(files)!=7:raise ValueError('Expected all seven supplied Hebios slot files')
 return dict(status='asset audit only; not registered in game',source='user-supplied FBX with embedded textures',
             files=[inspect(path) for path in files])
if __name__=='__main__':
 report=audit();out=ROOT/'crafting/hebios-import-audit.json';out.write_text(json.dumps(report,indent=2)+'\n')
 for f in report['files']:print(f['file'],f['mesh_count'],'meshes',f['triangles'],'triangles',len(f['embedded_textures']),'embedded PNGs',f['attachment_helpers'])
