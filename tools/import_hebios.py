"""Deterministic supplied ASCII-FBX -> existing PC/Quest pack importer.
Appends only; the first 24 piece records and every existing mesh stay byte-identical.
Requires Pillow for embedded PNG decoding, never executes donor DLL code.
"""
import base64,io,json,math,re,struct,sys
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from tools.skin_metal import Reader,parse,integer,text,array,encode_mesh,multiply,inverse,point,direction
from tools.build_crafting import encode
from tools.crafting_geometry import nail_pose
I=tuple(float(r==c) for r in range(4) for c in range(4))
def mat(body):
 def prop(name,default):
  m=re.search(r'P: "'+name+r'",([^\n]+)',body)
  return [float(v) for v in m[1].split(',')[-3:]] if m else default
 t=prop('Lcl Translation',[0,0,0]);s=prop('Lcl Scaling',[1,1,1])
 def rotation(angles,order):
  m=I
  for a in order:
   c=math.cos(math.radians(angles[a]));sn=math.sin(math.radians(angles[a]));u=(a+1)%3;v=(a+2)%3
   rot=list(I);rot[u*4+u]=rot[v*4+v]=c;rot[u*4+v]=-sn;rot[v*4+u]=sn;m=multiply(m,rot)
  return m
 m=multiply(rotation(prop('PreRotation',[0,0,0]),(2,1,0)),rotation(prop('Lcl Rotation',[0,0,0]),(1,0,2)))
 m=list(m)
 for r in range(3):
  for c in range(3):m[r*4+c]*=s[c]
  m[r*4+3]=t[r]
 return tuple(m)
def unity(m):
 # FBX exporter centimetres/right-handed -> Unity metres/left-handed.
 signs=(-1,1,1,1)
 return tuple(m[r*4+c]*signs[r]*signs[c]*(.01 if c==3 and r<3 else 1) for r in range(4) for c in range(4))
def col(m):return struct.pack('<16f',*(m[r*4+c] for c in range(4) for r in range(4)))
def arr(body,name,typ=float):
 m=re.search(r'\b'+name+r': \*(\d+) \{\s*a: ([^}]+)\}',body)
 if not m:return []
 a=[typ(v) for v in m[2].strip().split(',')];assert len(a)==int(m[1]);return a
def chunks(a,n):return [tuple(a[i:i+n]) for i in range(0,len(a),n)]
def fbx(path):
 s=path.read_text('utf-8-sig');objs={int(k):(kind,name.split('::')[-1],sub,body) for kind,k,name,sub,body in re.findall(r'^\t(\w+): (\d+), "([^"\n]*)", "([^"\n]*)" \{\n(.*?)^\t\}',s,re.M|re.S)}
 edges=[(int(a),int(b)) for a,b in re.findall(r'C: "OO",(\d+),(\d+)',s)]
 parent={a:b for a,b in edges if a in objs and objs[a][0]=='Model'}
 def world(k):return multiply(world(parent[k]),mat(objs[k][3])) if parent.get(k) in objs else mat(objs[k][3])
 def helper(k):
  while k in objs:
   if objs[k][1].startswith('ATTBone_'):return k
   k=parent.get(k)
  raise ValueError('Mesh without named attachment helper')
 return objs,edges,world,helper

def build():
 original=(ROOT/'pchost/armor.pack').read_bytes();prefix,meshes,pieces=parse(original)
 if len(pieces) not in (24,32):raise ValueError('Expected accepted 24-piece pack or generated 32-piece pack')
 pieces=pieces[:24];meshes=[m for m in meshes if not m['name'].startswith('hebios/')]
 r=Reader(prefix);r.take(8);textures=[]
 for _ in range(r.number()):
  start=r.pos;name=r.text();r.take(16);r.take(r.number())
  if not name.startswith('Hebios/'):textures.append(prefix[start:r.pos])
 materials=[]
 for _ in range(r.number()):
  start=r.pos;name=r.text();r.take(40)
  if not name.startswith('Hebios/'):materials.append(prefix[start:r.pos])
 recipes=json.loads((ROOT/'crafting/hebios-jig-plans.json').read_text());plans={p['prefabName']:p for p in recipes['entries']}
 specs=[('Head','helmet','Helmet'),('Chest','chest','Cuirass'),('LeftShoulder','shoulder_l','Pauldron_Left'),('RightShoulder','shoulder_r','Pauldron_Right'),('LeftForearm','gauntlet_l','Bracer_Left'),('RightForearm','gauntlet_r','Bracer_Right'),('Hips','tasset_l','Tassets'),('Hips','tasset_r','Tassets')]
 records=[];newpieces=[];audit=[]
 for file,slot,suffix in specs:
  objs,edges,world,helper=fbx(ROOT/('hebios armor/Armor_'+file+'.fbx'));plan=json.loads(json.dumps(plans['ATT_HebiosArmor_'+suffix]));sources=plan['sources']
  if file=='Hips':
   plan.update(sources=[50988,50988,63204],parents=[-1,0,1],levels=[0,1,2],requiresNails=[False,True,True],clearanceDegrees=[-2,45,45])
   sources=plan['sources']
  parts=[dict(vertices=[],normals=[],uv=[],submeshes=[[]]) for _ in sources];worn=[];looks=[];bounds=[];materialmap={};texturemap={}
  for key,(kind,name,sub,body) in objs.items():
   if kind!='Video':continue
   png=b''.join(base64.b64decode(re.sub(r'\s','',v)) for v in re.findall(r'"([A-Za-z0-9+/=\s]+)"',body.split('Content:')[1]));im=Image.open(io.BytesIO(png)).convert('RGBA');im=im.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
   texturemap[key]=len(textures);raw=im.tobytes();textures.append(text('Hebios/'+name)+struct.pack('<4i',4,*im.size,1)+integer(len(raw))+raw)
  for key,(kind,name,sub,body) in objs.items():
   if kind!='Material':continue
   color=re.search(r'P: "DiffuseColor",[^\n]*,\s*"A",([^\n]+)',body);rgb=[float(v) for v in color[1].split(',')] if color else [1,1,1]
   tex=-1
   for a,b in edges:
    if b==key and objs[a][0]=='Texture':
     for v,t in edges:
      if t==a and v in texturemap:tex=texturemap[v]
   # Texture -> material edges are OP, unlike the hierarchy OO edges.
   for a,b in re.findall(r'C: "OP",(\d+),(\d+),"DiffuseColor"', (ROOT/('hebios armor/Armor_'+file+'.fbx')).read_text('utf-8-sig')):
    if int(b)==key:
     for v,t in edges:
      if t==int(a) and v in texturemap:tex=texturemap[v]
   materialmap[key]=len(materials);materials.append(text('Hebios/'+name)+struct.pack('<4i6f',tex,-1,-1,-1,*rgb,1,.45 if 'steel' in name or 'metal' in name else 0,.3))
  for gid,(kind,name,sub,body) in objs.items():
   if kind!='Geometry':continue
   model=next(b for a,b in edges if a==gid and objs[b][0]=='Model');modelname=objs[model][1];h=next((k for k,v in objs.items() if v[1]=='ATTBone_wrist_'+('L' if 'Left' in file else 'R')+'_jnt'),None) if modelname.startswith('glove_') else helper(model);bone=objs[h][1][8:];local=unity(multiply(inverse(world(h)),world(model)));globalmat=unity(world(model))
   vv=[(-x*.01,y*.01,z*.01) for x,y,z in chunks(arr(body,'Vertices'),3)];nn=[(-x,y,z) for x,y,z in chunks(arr(body,'Normals'),3)];uv=chunks(arr(body,'UV'),2);uvi=arr(body,'UVIndex',int);ids=arr(body,'PolygonVertexIndex',int)
   skins=[a for a,b in edges if b==gid and objs[a][0]=='Deformer'];baked=vv;skinweights={};skinbones=[];skinbind=[];skinposes=[]
   if skins:
    baked=[[0.,0.,0.] for v in vv]
    for cid in [a for a,b in edges if b in skins and objs[a][2]=='Cluster']:
     b=next(a for a,p in edges if p==cid and objs[a][0]=='Model');skinbones.append(objs[b][1].removeprefix('ATTBone_'))
     flat=arr(objs[cid][3],'Transform');bind=unity(tuple(flat[c*4+r] for r in range(4) for c in range(4)));skinbind.append(inverse(unity(world(b))));pose=multiply(unity(world(b)),bind);skinposes.append(pose)
     for vi,weight in zip(arr(objs[cid][3],'Indexes',int),arr(objs[cid][3],'Weights')):
      skinweights.setdefault(vi,[]).append((len(skinbones)-1,weight));pp=point(pose,vv[vi]);baked[vi]=[a+weight*x for a,x in zip(baked[vi],pp)]
   def normal(vi,corner):
    n=nn[corner] if len(nn)==len(ids) else nn[vi]
    if not skins:return n
    value=[0.,0.,0.]
    for bi,weight in skinweights[vi]:
     v=direction(skinposes[bi],n);value=[a+weight*x for a,x in zip(value,v)]
    length=math.sqrt(sum(x*x for x in value)) or 1
    return tuple(x/length for x in value)
   matid=next((materialmap[a] for a,b in edges if b==model and a in materialmap),-1);mn=next((objs[a][1].lower() for a,b in edges if b==model and a in materialmap),'');low=modelname.lower()
   neutral=low.startswith('glove_');closure=any(x in mn for x in ('binding','leather','clasp','stitch','herald'))
   group=len(sources)-1 if closure else 0
   if not closure and len(sources)>2:
    if file=='Head':group=2 if any(x in low for x in ('shikoro','nape')) else 1 if any(x in low for x in ('cheek','temple')) else 0
    elif file=='Chest':group=1 if any(x in low for x in ('back','rear')) else 0
    elif 'Shoulder' in file:group=0 if 'joint_cap' in low else 1
    elif 'Forearm' in file:group=1 if bone.startswith(('wrist','index','middle','ring','little','thumb')) or 'dorsal' in low else 0
    elif file=='Hips':group=1 if 'rear' in low else 0
   # Split central belt/pennant triangles by body side, never duplicate geometry.
   m=dict(name='hebios/'+slot+'/'+str(255 if neutral else group)+'/'+str(len(ids)//3)+'/'+modelname,v=[],n=[],t=[],uv=[],w=[],bind=[],subs=[[]]);poly=[];corner=0
   for index in ids:
    vi=-index-1 if index<0 else index;poly.append((vi,corner));corner+=1
    if index>=0:continue
    for j in range(1,len(poly)-1):
     tri=[poly[0],poly[j+1],poly[j]];wp=[point(globalmat,baked[v]) for v,c in tri]
     if file=='Hips' and ((sum(p[0] for p in wp)>=0)!=(slot=='tasset_l')):continue
     base=len(m['v']);m['subs'][0].extend((base+i,) for i in range(3))
     for v,c in tri:
      m['v'].append(tuple(baked[v]));m['n'].append(normal(v,c));m['uv'].append(uv[uvi[c]] if uvi else uv[c] if len(uv)==len(ids) else uv[v]);bounds.append(point(globalmat,baked[v]))
     dest=parts[group];base=len(dest['vertices']);dest['submeshes'][0].append(tuple(base+i for i in range(3)))
     for v,c in tri:
      dest['vertices'].append(point(globalmat,baked[v]));dest['normals'].append(direction(globalmat,normal(v,c)));dest['uv'].append(uv[uvi[c]] if uvi else uv[c] if len(uv)==len(ids) else uv[v])
    poly=[]
   if not m['v']:continue
   fields=m['name'].split('/');fields[3]=str(len(m['v'])//3);m['name']='/'.join(fields)
   # Native glove retains authored weights in the imported helper rest frame.
   bones=skinbones
   if bones:
    m['bind']=skinbind
    for vi in [(-v-1 if v<0 else v) for v in ids]:
     ww=sorted(skinweights.get(vi,[(0,1)]),key=lambda p:-p[1])[:4];total=sum(w for _,w in ww);ww += [(0,0)]*(4-len(ww));m['w'].append(tuple(b for b,w in ww)+tuple(w/total for b,w in ww))
    m['w']=[v for i in range(0,len(m['w']),3) for v in (m['w'][i],m['w'][i+2],m['w'][i+1])]
    assert len(m['w'])==len(m['v'])
   mid=len(meshes);meshes.append(m)
   if bones:worn.append(integer(1)+integer(mid)+array('i',[(matid,)])+text(bone)+integer(len(bones))+b''.join(text(b) for b in bones))
   else:worn.append(integer(0)+integer(mid)+array('i',[(matid,)])+text(bone)+col(local))
   if bones:
    look=dict(m);look.update(name=m['name']+'/pickup',w=[],bind=[])
    lookmid=len(meshes);meshes.append(look)
   else:lookmid=mid
   looks.append(integer(lookmid)+array('i',[(matid,)])+col(globalmat))
  assert all(p['vertices'] for p in parts),(slot,[len(p['vertices']) for p in parts])
  # Keep native graph/cost, but author nail seats on the actual supplied geometry.
  poses=[nail_pose(p) for p in parts];plan['nailPositions']=[p[0] for p in poses];plan['nailRotations']=[p[1] for p in poses]
  record=dict(id='hebios.'+slot,glyph='ATT_HebiosArmor_'+suffix+'.png',parts=parts,plan=plan);records.append(record)
  lo=[min(p[a] for p in bounds) for a in range(3)];hi=[max(p[a] for p in bounds) for a in range(3)];center=[(a+b)/2 for a,b in zip(lo,hi)];size=[max(.01,b-a) for a,b in zip(lo,hi)]
  # Exact slot names come from accepted matching piece metadata.
  rr=Reader(pieces[len(newpieces)]['prefix']);rr.text();rr.text();game=rr.text()
  raw=text(record['id'])+text('hebios')+text(game)+text('supplied '+file)+integer(len(worn))+b''.join(worn)+integer(len(looks))+b''.join(looks)+integer(1)+integer(0)+col(I)+struct.pack('<6f',*center,*size)
  newpieces.append(raw);audit.append(dict(id=record['id'],meshes=len(worn),triangles=sum(len(p['vertices'])//3 for p in parts),sources=plan['sources']))
 data=b'ARMR\x01\0\0\0'+integer(len(textures))+b''.join(textures)+integer(len(materials))+b''.join(materials)+integer(len(meshes))+b''.join(m.get('raw') or encode_mesh(m) for m in meshes)+integer(32)+b''.join(p['prefix']+integer(len(p['parts']))+b''.join(part['raw'] for part in p['parts'])+p['suffix'] for p in pieces)+b''.join(newpieces)
 for path in ('pchost/armor.pack','quest/ArmorRestored.armor.pack'):(ROOT/path).write_bytes(data)
 old=(ROOT/'crafting/construction.bin').read_bytes();reader=Reader(old);reader.take(44)
 for _ in range(16):
  reader.text();reader.text()
  for _ in range(reader.number()):
   reader.take(45);reader.take(reader.number()*32)
   for _ in range(reader.number()):reader.take(reader.number()*4)
 old=old[:reader.pos]
 extra=encode(records,recipes['bundleSha256']);(ROOT/'crafting/construction.bin').write_bytes(old[:40]+integer(24)+old[44:]+extra[44:])
 manifest=json.loads((ROOT/'crafting/construction-manifest.json').read_text());manifest['recipes']=manifest['recipes'][:16]+[dict(id=r['id'],reference_only=False,sources=r['plan']['sources'],vertex_counts=[len(p['vertices']) for p in r['parts']]) for r in records]
 import hashlib
 manifest['pack_sha256']=hashlib.sha256((ROOT/'crafting/construction.bin').read_bytes()).hexdigest().upper();(ROOT/'crafting/construction-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
 (ROOT/'crafting/hebios-runtime-manifest.json').write_text(json.dumps(audit,indent=2)+'\n')
 print('Imported',len(newpieces),'Hebios equipment slots;',sum(a['triangles'] for a in audit),'triangles')
if __name__=='__main__':build()
