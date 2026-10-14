import numpy as np
from PIL import Image
def classify(rgb):
  # rgb float 0..1, (...,3) -> weights (...,3): primary (white fairing/suit), secondary (navy), tertiary (orange)
  r,g,b=rgb[...,0],rgb[...,1],rgb[...,2]
  mx=rgb.max(-1);mn=rgb.min(-1);v=mx;s=np.where(mx>1e-4,(mx-mn)/np.maximum(mx,1e-4),0)
  # hue in degrees
  d=np.maximum(mx-mn,1e-6)
  h=np.where(mx==r,((g-b)/d)%6,np.where(mx==g,(b-r)/d+2,(r-g)/d+4))*60
  def ramp(x,a,b_): return np.clip((x-a)/(b_-a),0,1)
  white=ramp(v,0.55,0.75)*(1-ramp(s,0.12,0.25))
  chroma=mx-mn
  # Navy: a real blue (absolute chroma), so near-black rubber and parts with a bluish cast stay as they are.
  navy=ramp(chroma,0.08,0.14)*np.clip((28-np.abs(h-222))/10,0,1)*ramp(v,0.15,0.22)*(1-ramp(v,0.7,0.85))
  orange=ramp(s,0.45,0.6)*np.clip((18-np.abs(h-30))/8,0,1)*ramp(v,0.4,0.55)
  return np.stack([white,navy,orange],-1)
if __name__=='__main__':
  im=np.asarray(Image.open('base4k.png').resize((2048,2048),Image.LANCZOS),dtype=np.float32)/255
  m=classify(im)
  print('coverage',m.reshape(-1,3).mean(0))
  Image.fromarray((m*255).astype(np.uint8)).save('mask2k.png')
  Image.fromarray((im*255).astype(np.uint8)).save('base2k.png')
  Image.fromarray((m*255).astype(np.uint8)).resize((512,512)).save('mask_prev.png')
