import sys;sys.path.insert(0,'.')
import struct,json,numpy as np
from PIL import Image
exec(open('render.py').read().split("def render(")[0])
from mask import classify
P,N,UV,I,_=load('deq.glb')
base=np.asarray(Image.open('base4k.png'),dtype=np.float32)/255
mr=np.asarray(Image.open('mr4k.png'),dtype=np.float32)/255
h,w=base.shape[:2]
px=np.clip((UV[:,0]%1*w).astype(int),0,w-1);py=np.clip((UV[:,1]%1*h).astype(int),0,h-1)
col=base[py,px];rough=mr[py,px,1];metal=mr[py,px,2]
key=np.round(P/2e-5).astype(np.int64)
uniq,inv=np.unique(key,axis=0,return_inverse=True);inv=inv.ravel()
n=len(uniq);print('welded',len(P),'->',n)
cnt=np.bincount(inv,minlength=n)[:,None]
Pw=np.zeros((n,3));np.add.at(Pw,inv,P);Pw/=cnt
Cw=np.zeros((n,3));np.add.at(Cw,inv,col);Cw/=cnt
Rw=np.bincount(inv,weights=rough,minlength=n)/cnt[:,0]
Mw=np.bincount(inv,weights=metal,minlength=n)/cnt[:,0]
Iw=inv[I]
keep=(Iw[:,0]!=Iw[:,1])&(Iw[:,1]!=Iw[:,2])&(Iw[:,0]!=Iw[:,2]);Iw=Iw[keep]
np.savez('welded.npz',P=Pw,C=Cw,R=Rw,M=Mw)
# minimal glb: positions + indices
pos=Pw.astype(np.float32).tobytes();idx=Iw.astype(np.uint32).ravel().tobytes()
j={"asset":{"version":"2.0"},"scene":0,"scenes":[{"nodes":[0]}],"nodes":[{"mesh":0}],
 "meshes":[{"primitives":[{"attributes":{"POSITION":0},"indices":1}]}],
 "buffers":[{"byteLength":len(pos)+len(idx)}],
 "bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":len(pos),"target":34962},{"buffer":0,"byteOffset":len(pos),"byteLength":len(idx),"target":34963}],
 "accessors":[{"bufferView":0,"componentType":5126,"count":n,"type":"VEC3","min":Pw.min(0).tolist(),"max":Pw.max(0).tolist()},
              {"bufferView":1,"componentType":5125,"count":Iw.size,"type":"SCALAR"}]}
js=json.dumps(j).encode();js+=b' '*((4-len(js)%4)%4);binb=pos+idx;binb+=b'\0'*((4-len(binb)%4)%4)
out=struct.pack('<4sII',b'glTF',2,12+8+len(js)+8+len(binb))+struct.pack('<I4s',len(js),b'JSON')+js+struct.pack('<I4s',len(binb),b'BIN\0')+binb
open('geom.glb','wb').write(out);print('tris',len(Iw))
