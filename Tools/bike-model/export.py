import sys;sys.path.insert(0,'.')
import numpy as np,struct,os
from PIL import Image
from mask import classify
OUT='/home/user/MotoManager/Assets/MotoManager/Resources/Models';os.makedirs(OUT,exist_ok=True)
S=1.75;CX=-0.01
for name in ('lo','hi'):
  d=np.load(name+'_baked.npz');P=d['P'];N=d['N'];UV=d['UV'];I=d['I'].copy()
  # glTF (right-handed, bike forward -X) -> bike space (Unity, left-handed, forward +Z, pivot on the ground mid-wheelbase)
  Pu=np.stack([-P[:,2],P[:,1]-P[:,1].min(),-(P[:,0]-CX)],1)*S
  Nu=np.stack([-N[:,2],N[:,1],-N[:,0]],1)
  f=np.cross(Pu[I[:,1]]-Pu[I[:,0]],Pu[I[:,2]]-Pu[I[:,0]]);agree=(np.einsum('ij,ij->i',f,Nu[I[:,0]]+Nu[I[:,1]]+Nu[I[:,2]])>0).mean()
  if agree<0.5: I=I[:,[0,2,1]]
  print(name,'winding agree',round(agree,3),'flipped' if agree<0.5 else '', 'bounds',Pu.min(0).round(3),Pu.max(0).round(3))
  UVu=np.stack([UV[:,0],1-UV[:,1]],1)
  with open(f'{OUT}/bike_{name}.bytes','wb') as fh:
    fh.write(b'MMBK');fh.write(struct.pack('<iii',1,len(Pu),I.size))
    fh.write(Pu.astype('<f4').tobytes());fh.write(Nu.astype('<f4').tobytes());fh.write(UVu.astype('<f4').tobytes())
    fh.write(I.astype('<u2' if len(Pu)<65536 else '<u4').ravel().tobytes())
  base=Image.open(name+'_base.png').convert('RGB');gl=Image.open(name+'_gloss.png').convert('L')
  b=np.asarray(base,np.float32)/255;m=classify(b)
  rgba=np.concatenate([m,(np.asarray(gl,np.float32)/255)[...,None]],-1)
  base.save(f'{OUT}/bike_{name}_base.png',optimize=True)
  Image.fromarray((rgba*255).astype(np.uint8),'RGBA').save(f'{OUT}/bike_{name}_mask.png',optimize=True)
