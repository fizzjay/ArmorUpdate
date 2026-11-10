"""Audit required crafting API names in the supplied PC/Quest assemblies.

Reads ECMA metadata without loading DLL code. Uses pefile (a dnfile dependency).
Metadata tables are decoded directly, including nested type names, avoiding
the large generated IL2CPP assembly's expensive relationship expansion. This is not C# compilation.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct


def compressed(data, offset):
    first=data[offset]
    if first<128:return first,offset+1
    if first<192:return ((first&63)<<8)|data[offset+1],offset+2
    return ((first&31)<<24)|(data[offset+1]<<16)|(data[offset+2]<<8)|data[offset+3],offset+4


def inventory(path):
    import pefile
    pe=pefile.PE(str(path),fast_load=True)
    try:
        cli=pe.get_data(pe.OPTIONAL_HEADER.DATA_DIRECTORY[14].VirtualAddress,24)
        rva,size=struct.unpack_from('<II',cli,8)
        data=pe.get_data(rva,size)
    finally:
        pe.close()
    if data[:4]!=b'BSJB':raise ValueError('Not managed metadata')
    pos=(16+struct.unpack_from('<I',data,12)[0]+3)&~3
    count=struct.unpack_from('<H',data,pos+2)[0];pos+=4
    streams={}
    for _ in range(count):
        offset,size=struct.unpack_from('<II',data,pos);pos+=8
        end=data.index(0,pos);name=data[pos:end].decode();pos=(end+4)&~3
        streams[name]=data[offset:offset+size]
    strings=streams['#Strings'];blobs=streams['#Blob'];table=streams.get('#~',streams.get('#-'))
    heaps=table[6];valid=struct.unpack_from('<Q',table,8)[0];rows={};pos=24
    for i in range(64):
        if valid&(1<<i):rows[i]=struct.unpack_from('<I',table,pos)[0];pos+=4
    def ix(i):return 4 if rows.get(i,0)>=65536 else 2
    def coded(bits,*tables):return 4 if max(rows.get(i,0) for i in tables)>=(1<<(16-bits)) else 2
    S=4 if heaps&1 else 2;G=4 if heaps&2 else 2;B=4 if heaps&4 else 2
    sizes={0:2+S+3*G,1:coded(2,0,26,35,1)+2*S,
           2:4+2*S+coded(2,2,1,27)+ix(4)+ix(6),3:ix(4),
           4:2+S+B,5:ix(6),6:8+S+B+ix(8)}
    td=coded(2,2,1,27)
    sizes.update({7:ix(8),8:4+S,9:ix(2)+td,10:coded(3,2,1,26,6,27)+S+B,
      11:2+coded(2,4,8,23)+B,
      12:coded(5,6,4,1,2,8,9,10,0,14,23,20,17,26,27,32,35,38,39,40,42,44,43)+coded(3,6,10)+B,
      13:coded(1,4,8)+B,14:2+coded(2,2,6,32)+B,15:6+ix(2),16:4+ix(4),17:B,
      18:ix(2)+ix(20),19:ix(20),20:2+S+td,21:ix(2)+ix(23),22:ix(23),23:2+S+B,
      24:2+ix(6)+coded(1,20,23),25:ix(2)+2*coded(1,6,10),26:S,27:B,
      28:2+coded(1,4,6)+S+ix(26),29:4+ix(4),30:8,31:4,32:16+B+2*S,33:4,34:12,
      35:12+2*B+2*S,36:4+ix(35),37:12+ix(35),38:4+S+B,
      39:8+2*S+coded(2,38,35,39),40:8+S+coded(2,38,35,39),41:2*ix(2)})
    offsets={}
    for i in range(42):offsets[i]=pos;pos+=rows.get(i,0)*sizes[i]
    if rows.get(3,0) or rows.get(5,0):raise ValueError('Unoptimized pointer tables are not supported')
    def value(offset,size):return int.from_bytes(table[offset:offset+size],'little')
    def string(index):return strings[index:strings.index(0,index)].decode('utf8')
    def blob(index):
        length,start=compressed(blobs,index);return blobs[start:start+length]
    types=[]
    for row in range(rows[2]):
        offset=offsets[2]+row*sizes[2]+4
        name=string(value(offset,S));namespace=string(value(offset+S,S));offset+=2*S+coded(2,2,1,27)
        types.append((namespace+'.'+name,value(offset,ix(4)),value(offset+ix(4),ix(6))))
    parents={}
    for row in range(rows.get(41,0)):
        offset=offsets[41]+row*sizes[41]
        parents[value(offset,ix(2))-1]=value(offset+ix(2),ix(2))-1
    def fullname(index):
        raw=types[index][0]
        return fullname(parents[index])+'+'+raw.rsplit('.',1)[-1] if index in parents else raw
    result={}
    for index,(_,field_start,method_start) in enumerate(types):
        name=fullname(index)
        field_end=types[index+1][1] if index+1<len(types) else rows.get(4,0)+1
        method_end=types[index+1][2] if index+1<len(types) else rows.get(6,0)+1
        fields={};methods={}
        for row in range(field_start-1,field_end-1):
            offset=offsets[4]+row*sizes[4]
            fields[string(value(offset+2,S))]=blob(value(offset+2+S,B)).hex()
        for row in range(method_start-1,method_end-1):
            offset=offsets[6]+row*sizes[6]
            method=string(value(offset+8,S));signature=blob(value(offset+8+S,B))
            methods.setdefault(method,[]).append(signature.hex())
        result[name]={'flags':value(offsets[2]+index*sizes[2],4),'fields':fields,'methods':methods}
    return result


SHARED={
 'Alta.Crafting.GenericCraftingRecipe':('pieces resultHash spawnOffset uniqueGlyphs mappedResult','get_Pieces get_ResultHash get_SpawnOffset CreatePage .ctor'),
 'Alta.Crafting.GenericCraftingPieceDefinition':('hash source level mesh collisionMesh position failureHealth isCritical resultHash minChiselHitDamageToFall materialChannel defaultMaterial avoidsAtlasing nailSettings','.ctor'),
 'Alta.Crafting.NailSettings':('devianceToDamage progressToDevianceMultiplier damageSplitPerDot damageOverflowMultiplier devianceMultiplier nailDefinition nails nailTime progressHealth maxDeviance maxDamagePerHit','.ctor'),
 'Alta.Crafting.SerializedTransform':('position rotation scale','.ctor'),
 'Alta.Crafting.GenericCraftingSettings':('','get_GenericNailDefaultDefinition'),
 'Alta.Crafting.GenericCraftingStation':('currentProject','TryAddPiece get_PiecesParent'),
 'Alta.Crafting.IProject':('','get_Recipe'),
 'Alta.Pages.Glyph':('sprite','.ctor'),
 'Alta.Pages.PageCollection':('pageBases','get_PageBases get_Size get_PreferredProvider'),
 'Alta.Pages.PageInstance':('','Generate get_Data Set'),
 'Alta.Pages.Glyph+Positioning':('',''),
 'Alta.Pages.Book':('paperPrefab',''),
}
SERVER={
 'Alta.Crafting.GenericCraftingStation':('currentProject recipeDock trackedPickups isSyncingAllowed syncFull','SpawnResult ComponentReleased ClearProject SetupProjectFromPage SetupLOD ClearLOD ChangedPieces'),
 'Alta.Crafting.GenericCraftingPiece':('project definition','get_CenterPosition get_IsFilled get_IsComplete get_Material ApplySaveData SetEnabled Empty AcceptsComponent Pop'),
 'Alta.Crafting.GenericCraftingComponent':('','get_Material SetupAsRefuse'),
 'Alta.Crafting.PieceSaveData':('hash isComplete material','.ctor'),
 'Alta.Crafting.IProject':('','get_Recipe get_IsComplete get_MappedPieces get_Station Clear SetMaxLevel'),
 'Alta.Saving.SavingSystem':('savingSetups savingTypes','Reinitialize get_LatestChunkVersioning'),
 'Alta.Saving.SavingSystem+SavingSetup':('','.ctor Add FinishInitialization get_Save get_LatestVersion get_LatestSetup get_Hash'),
 'Alta.Chunks.SavingInfoAttribute':('','GetHash'),
 '.NewSaveData`2':('',''),
 '.SaveStructureAttribute':('','.ctor'),
 'Alta.Saving.ISavableSaveBase':('','get_name'),
}


def audit(path,quest):
    types=inventory(path);missing=[];checked=[]
    specs=list(SHARED.items())+([] if quest else list(SERVER.items()))
    for name,(fields,methods) in specs:
        target='Il2Cpp'+name if quest else name
        entry=types.get(target)
        if entry is None:
            missing.append(target);continue
        for field in fields.split():
            if field not in entry['fields'] and not ('get_'+field in entry['methods'] and 'set_'+field in entry['methods']):
                missing.append(target+'.'+field)
        for method in methods.split():
            if method not in entry['methods']:missing.append(target+'.'+method)
        checked.append(target)
    return {'assembly':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),
            'checked_types':sorted(set(checked)),'missing':missing}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pc',type=Path,default=Path('refs/pc/Root.Township.dll'))
    parser.add_argument('--quest',type=Path,default=Path('refs/quest/Il2CppRoot.Township.dll'))
    args=parser.parse_args()
    # The user-supplied ZIPs are already in the repository. Extract only the
    # named assembly into ignored refs when working from a fresh checkout.
    import zipfile
    for path,archive in ((args.pc,Path('root.zip')),(args.quest,Path('rootil2cpp.zip'))):
        if not path.exists() and archive.exists():
            with zipfile.ZipFile(archive) as zipped:
                matches=[i for i in zipped.infolist() if Path(i.filename).name==path.name]
                if len(matches)!=1 or matches[0].file_size>128*1024*1024:
                    raise ValueError('Ambiguous or oversized reference archive')
                path.parent.mkdir(parents=True,exist_ok=True)
                path.write_bytes(zipped.read(matches[0]))
    reports=[audit(args.pc,False),audit(args.quest,True)]
    print(json.dumps(reports,indent=2))
    return 1 if any(r['missing'] for r in reports) else 0


if __name__=='__main__':
    raise SystemExit(main())
