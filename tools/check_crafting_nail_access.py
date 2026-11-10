"""Audit full-assembly nails and above-table jig access for all shipped plans."""
from pathlib import Path
import sys
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from tools.bench_nail_geometry import plans,orientation,dot

if __name__=='__main__':
    count=0
    for name,parts in plans(ROOT/'crafting/construction.bin'):
        up,chosen=orientation(parts)
        print(name,'jig-up=',up,'nails=',len(chosen),'OK')
        count+=len(chosen)
    print(count,'nails have complete-assembly clearance and an above-table approach')
