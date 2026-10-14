import sys;sys.path.insert(0,'.')
import struct,json,numpy as np,math
from PIL import Image
exec(open('render.py').read().split("def load(")[0])
d=np.load('welded.npz');Pw=d['P'].astype(np.float32)
f=open('geom_lo.glb','rb').read();cl=struct.unpack('<I',f[12:16])[0];j=json.loads(f[20:20+cl]);off=20+cl;bin_=f[off+8:]
def acc(i,dt,n):
  a=j['accessors'][i];bv=j['bufferViews'][a['bufferView']];o=bv.get('byteOffset',0)+a.get('byteOffset',0)
  return np.frombuffer(bin_,dtype=dt,count=a['count']*n,offset=o).reshape(-1,n) if n>1 else np.frombuffer(bin_,dtype=dt,count=a['count'],offset=o)
p=j['meshes'][0]['primitives'][0]
P=acc(p['attributes']['POSITION'],np.float32,3);it=j['accessors'][p['indices']]['componentType']
I=acc(p['indices'],{5125:np.uint32,5123:np.uint16}[it],1).reshape(-1,3).astype(np.int64)
lut={r.tobytes():i for i,r in enumerate(Pw)}
idx=np.array([lut.get(r.tobytes(),-1) for r in P]);print('unmatched',(idx<0).sum(),'of',len(P))
C=d['C'][idx];R=d['R'][idx];M=d['M'][idx]
np.savez('lo.npz',P=P,I=I,C=C,R=R,M=M)
