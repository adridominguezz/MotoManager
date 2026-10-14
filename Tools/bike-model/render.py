import struct,json,sys,io,math
import numpy as np
from PIL import Image
def load(fn):
  f=open(fn,'rb').read();cl=struct.unpack('<I',f[12:16])[0];j=json.loads(f[20:20+cl])
  off=20+cl;bl=struct.unpack('<I',f[off:off+4])[0];bin_=f[off+8:off+8+bl]
  def acc(i):
    a=j['accessors'][i];bv=j['bufferViews'][a['bufferView']]
    dt={5126:np.float32,5125:np.uint32,5123:np.uint16}[a['componentType']]
    n={'SCALAR':1,'VEC2':2,'VEC3':3}[a['type']]
    o=bv.get('byteOffset',0)+a.get('byteOffset',0)
    isz=np.dtype(dt).itemsize;stride=bv.get('byteStride',isz*n)
    raw=np.frombuffer(bin_,dtype=np.uint8,count=bv['byteLength']-a.get('byteOffset',0),offset=o)
    cnt=a['count'];buf=np.zeros((cnt,stride),np.uint8);need=(cnt-1)*stride+isz*n
    rows=np.lib.stride_tricks.as_strided(raw,shape=(cnt,isz*n),strides=(stride,1))
    arr=np.ascontiguousarray(rows).view(dt).reshape(cnt,n)
    return arr if n>1 else arr[:,0]
  p=j['meshes'][0]['primitives'][0]
  P=acc(p['attributes']['POSITION']).astype(np.float64);UV=acc(p['attributes']['TEXCOORD_0']);I=acc(p['indices']).reshape(-1,3)
  N=acc(p['attributes']['NORMAL'])
  mat=j['materials'][0]['pbrMetallicRoughness'];ti=j['textures'][mat['baseColorTexture']['index']]['source']
  bv=j['bufferViews'][j['images'][ti]['bufferView']];tex=Image.open(io.BytesIO(bin_[bv.get('byteOffset',0):bv.get('byteOffset',0)+bv['byteLength']])).convert('RGB')
  node=j['nodes'][0];q=node.get('rotation',[0,0,0,1]);s=node.get('scale',[1,1,1]);t=node.get('translation',[0,0,0])
  x,y,z,w=q;R=np.array([[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w)],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w)],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y)]])
  P=(P*s)@R.T+t;N=N@R.T
  return P,N,UV,I,tex
def render(P,N,UV,I,tex,yaw,pitch,size=700):
  cy,sy=math.cos(yaw),math.sin(yaw);cp,sp=math.cos(pitch),math.sin(pitch)
  Ry=np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]]);Rx=np.array([[1,0,0],[0,cp,-sp],[0,sp,cp]])
  M=Rx@Ry;V=(P-P.mean(0))@M.T;Nn=N@M.T
  sc=size*0.9/max(np.ptp(V[:,0]),np.ptp(V[:,1]));X=(V[:,0]*sc+size/2);Y=(size/2-V[:,1]*sc);Z=V[:,2]
  T=np.asarray(tex,dtype=np.float32)/255;th,tw=T.shape[:2]
  img=np.ones((size,size,3))*0.93;zb=np.full((size,size),-1e9)
  light=np.array([0.4,0.6,0.7]);light/=np.linalg.norm(light)
  for a,b,c in I:
    xs=X[[a,b,c]];ys=Y[[a,b,c]]
    x0,x1=int(max(0,xs.min())),int(min(size-1,xs.max()+1));y0,y1=int(max(0,ys.min())),int(min(size-1,ys.max()+1))
    if x1<x0 or y1<y0: continue
    gx,gy=np.meshgrid(np.arange(x0,x1+1)+0.5,np.arange(y0,y1+1)+0.5)
    d=(ys[1]-ys[2])*(xs[0]-xs[2])+(xs[2]-xs[1])*(ys[0]-ys[2])
    if abs(d)<1e-9: continue
    w0=((ys[1]-ys[2])*(gx-xs[2])+(xs[2]-xs[1])*(gy-ys[2]))/d;w1=((ys[2]-ys[0])*(gx-xs[2])+(xs[0]-xs[2])*(gy-ys[2]))/d;w2=1-w0-w1
    m=(w0>=0)&(w1>=0)&(w2>=0)
    if not m.any(): continue
    z=w0*Z[a]+w1*Z[b]+w2*Z[c]
    sub=zb[y0:y1+1,x0:x1+1];m&=z>sub
    if not m.any(): continue
    u=w0*UV[a,0]+w1*UV[b,0]+w2*UV[c,0];v=w0*UV[a,1]+w1*UV[b,1]+w2*UV[c,1]
    col=T[np.clip((v*th).astype(int)%th,0,th-1),np.clip((u*tw).astype(int)%tw,0,tw-1)]
    n=w0[...,None]*Nn[a]+w1[...,None]*Nn[b]+w2[...,None]*Nn[c];n/=np.linalg.norm(n,axis=-1,keepdims=True)+1e-9
    sh=0.45+0.55*np.clip(np.abs(n@light),0,1)
    sub[m]=z[m];img[y0:y1+1,x0:x1+1][m]=col[m]*sh[m][:,None]
  return Image.fromarray((np.clip(img,0,1)*255).astype(np.uint8))
P,N,UV,I,tex=load(sys.argv[1])
if len(sys.argv)>3: tex=Image.open(sys.argv[3]).convert('RGB')
print('tris',len(I),'size',np.ptp(P,0),'tex',tex.size)
tex.resize((512,512)).save('tex_base.png')
views=[(math.pi/2,0),(math.pi/4,-0.25),(0,0),(math.pi*0.75,-0.2)]
ims=[render(P,N,UV,I,tex,y,p,420) for y,p in views]
o=Image.new('RGB',(420*4,420));[o.paste(im,(i*420,0)) for i,im in enumerate(ims)];o.save(sys.argv[2])
