import numpy as np,sys,math
import sys; sys.path.insert(0, ".")
from PIL import Image
from mask import classify
def recolor(base,m,cols):
  lum=(base*[0.299,0.587,0.114]).sum(-1,keepdims=True)
  out=base.copy()
  refs=[0.85,0.22,0.6]
  for k in range(3):
    c=np.array(cols[k],np.float32)/255
    tinted=np.clip(c*lum/refs[k],0,1)
    w=m[...,k:k+1]
    out=out*(1-w)+tinted*w
  return out
if __name__=='__main__':
  base=np.asarray(Image.open('base2k.png'),dtype=np.float32)/255
  m=np.asarray(Image.open('mask2k.png'),dtype=np.float32)/255
  out=recolor(base,m,[(210,20,25),(25,25,28),(250,200,0)])
  Image.fromarray((out*255).astype(np.uint8)).save('recolored2k.png')
