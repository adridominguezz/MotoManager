import sys;sys.path.insert(0,'.')
import numpy as np,xatlas,time
from PIL import Image,ImageFilter
from scipy.spatial import cKDTree
exec(open('render.py').read().split("def render(")[0])
t0=time.time()
lo=np.load(sys.argv[1]);P=lo['P'].astype(np.float64);I=lo['I']
vm,ind,uv=xatlas.parametrize(P.astype(np.float32),I.astype(np.uint32))
P2=P[vm];I2=ind.astype(np.int64);UV2=uv
print('atlas verts',len(P2),'tris',len(I2),time.time()-t0)
fn=np.cross(P2[I2[:,1]]-P2[I2[:,0]],P2[I2[:,2]]-P2[I2[:,0]]);N2=np.zeros_like(P2)
for k in range(3): np.add.at(N2,I2[:,k],fn)
N2/=np.linalg.norm(N2,axis=1,keepdims=True)+1e-12
PO,NO,UVO,IO,_=load('deq.glb')
NO=NO/ (np.linalg.norm(NO,axis=1,keepdims=True)+1e-12)
tree=cKDTree(PO);print('tree',time.time()-t0)
base=np.asarray(Image.open('base4k.png'),dtype=np.float32)/255;mr=np.asarray(Image.open('mr4k.png'),dtype=np.float32)/255
S=int(sys.argv[2]);img=np.zeros((S,S,3),np.float32);gl=np.zeros((S,S),np.float32);cov=np.zeros((S,S),bool)
pts=[];pix=[];nrm=[]
U=UV2*S
for a,b,c in I2:
  xs=U[[a,b,c],0];ys=U[[a,b,c],1]
  x0,x1=int(max(0,np.floor(xs.min()))),int(min(S-1,np.ceil(xs.max())));y0,y1=int(max(0,np.floor(ys.min()))),int(min(S-1,np.ceil(ys.max())))
  gx,gy=np.meshgrid(np.arange(x0,x1+1)+0.5,np.arange(y0,y1+1)+0.5)
  d=(ys[1]-ys[2])*(xs[0]-xs[2])+(xs[2]-xs[1])*(ys[0]-ys[2])
  if abs(d)<1e-12: continue
  w0=((ys[1]-ys[2])*(gx-xs[2])+(xs[2]-xs[1])*(gy-ys[2]))/d;w1=((ys[2]-ys[0])*(gx-xs[2])+(xs[0]-xs[2])*(gy-ys[2]))/d;w2=1-w0-w1
  e=-0.02;m=(w0>=e)&(w1>=e)&(w2>=e)
  if not m.any(): continue
  W=np.stack([w0[m],w1[m],w2[m]],1)
  pts.append(W@P2[[a,b,c]]);nrm.append(W@N2[[a,b,c]]);pix.append(np.stack([gy[m].astype(int),gx[m].astype(int)],1))
pts=np.concatenate(pts);nrm=np.concatenate(nrm);pix=np.concatenate(pix)
nrm/=np.linalg.norm(nrm,axis=1,keepdims=True)+1e-12
print('texels',len(pts),time.time()-t0)
dist,nb=tree.query(pts,k=8)
dots=np.einsum('ijk,ik->ij',NO[nb],nrm)
score=dist+ (dots<0.2)*1.0
best=nb[np.arange(len(nb)),score.argmin(1)]
uvs=UVO[best];h,w=base.shape[:2]
px=np.clip((uvs[:,0]%1*w).astype(int),0,w-1);py=np.clip((uvs[:,1]%1*h).astype(int),0,h-1)
img[pix[:,0],pix[:,1]]=base[py,px];gl[pix[:,0],pix[:,1]]=1-mr[py,px,1];cov[pix[:,0],pix[:,1]]=True
# dilate gutters
for it in range(8):
  im=Image.fromarray((img*255).astype(np.uint8));dil=np.asarray(im.filter(ImageFilter.MaxFilter(3)),np.float32)/255
  cv=np.asarray(Image.fromarray(cov.astype(np.uint8)*255).filter(ImageFilter.MaxFilter(3)))>0
  new=cv&~cov
  # average of covered neighbours approximated with blur
  bl=np.asarray(Image.fromarray((img*255).astype(np.uint8)).filter(ImageFilter.BoxBlur(1)),np.float32)/255
  cb=np.asarray(Image.fromarray(cov.astype(np.uint8)*255).filter(ImageFilter.BoxBlur(1)),np.float32)/255
  fill=bl/np.maximum(cb[...,None],1e-3)
  img[new]=np.clip(fill[new],0,1);cov=cov|new
Image.fromarray((img*255).astype(np.uint8)).save(sys.argv[3]+'_base.png')
Image.fromarray((np.clip(gl,0,1)*255).astype(np.uint8)).save(sys.argv[3]+'_gloss.png')
# UVs stored with glTF convention (v down) -> image rows = v*S; Unity flips v at load
np.savez(sys.argv[3]+'_baked.npz',P=P2,I=I2,UV=UV2,N=N2)
print('done',time.time()-t0)
