import argparse
import csv
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

parser = argparse.ArgumentParser(description="Build regional cloud coverage from an installed HP2 port.")
parser.add_argument("game", type=Path)
parser.add_argument("upstream", type=Path)
args = parser.parse_args()
root, src = args.game, args.upstream
with (root / "data/textures/index.csv").open(encoding="utf-8-sig") as f:
    rows = list(csv.DictReader(f))
atlas = np.zeros((1024, 128), dtype=np.uint8)
for idx, track in enumerate((11, 14, 21, 24, 31, 34, 41, 44, 61, 64), 1):
 rs=[r for r in rows if r['file']==f'TRACKS/TEX{track}TRACK.tex' and r['name'].startswith('SKY') and 'REFLECT' not in r['name']]
 side=next(r for r in rs if int(r['width'])==512);cap=next(r for r in rs if 'CAP' in r['name'])
 a=np.asarray(Image.open(root/'data/textures'/side['png']).convert('RGB').filter(ImageFilter.GaussianBlur(2)),dtype=float)/255
 c=np.asarray(Image.open(root/'data/textures'/cap['png']).convert('RGB').filter(ImageFilter.GaussianBlur(2)),dtype=float)/255
 def mask(a,cap=False):
  lum=a@np.array([.2126,.7152,.0722]);lo=np.percentile(lum,12,axis=1,keepdims=True);hi=np.percentile(lum,94,axis=1,keepdims=True)
  n=np.clip((lum-lo)/np.maximum(hi-lo,.16),0,1)
  if track==14:n=.58+.34*n
  if track==24:n=np.maximum(n,(1-(a[:,:,2]-a[:,:,0])*5)*.48)
  if track in (21,34,64):n=np.clip(n*.70+(a[:,:,0]-a[:,:,2]+.16)*1.3,0,1)
  if track==44:n=np.clip(.78-n*.60+np.maximum(a[:,:,2]-a[:,:,0],0)*.45,0,1)
  return np.clip(n,0,1)
 m=mask(a);cm=mask(c,True)
 # Make a continuous periodic panorama; feather the join over 24 source pixels.
 for k in range(24):
  w=.5*(1+math.cos(math.pi*k/24));edge=(m[:,k]+m[:,-1-k])*.5
  m[:,k]=m[:,k]*(1-w)+edge*w;m[:,-1-k]=m[:,-1-k]*(1-w)+edge*w
 def sample(im,x,y):
  h,w=im.shape;xx=np.clip(x,0,w-1);yy=np.clip(y,0,h-1);ix=xx.astype(int);iy=yy.astype(int);fx=xx-ix;fy=yy-iy
  return (im[iy,ix]*(1-fx)+im[iy,np.minimum(ix+1,w-1)]*fx)*(1-fy)+(im[np.minimum(iy+1,h-1),ix]*(1-fx)+im[np.minimum(iy+1,h-1),np.minimum(ix+1,w-1)]*fx)*fy
 u,v=np.meshgrid(np.linspace(0,1,128),np.linspace(0,1,64));theta=u*2*math.pi
 sideval=sample(m,u*511,np.clip(.92-v/.65,0,1)*255)
 radius=np.clip((1-v)/.40,0,1)*.45
 capval=sample(cm,(.5+np.cos(theta)*radius)*255,(.5+np.sin(theta)*radius)*255)
 t=np.clip((v-.52)/.34,0,1);t=t*t*(3-2*t);weather=sideval*(1-t)+capval*t
 # One value at zenith, no longitude spokes.
 t=np.clip((v-.94)/.06,0,1);weather=weather*(1-t)+weather[-1].mean()*t
 tile=np.array(Image.fromarray(np.uint8(weather*255)).filter(ImageFilter.GaussianBlur(.6)));tile[-1,:]=int(tile[-1].mean())
 edge=((tile[:,0].astype(int)+tile[:,-1])/2).astype(np.uint8);tile[:,0]=edge;tile[:,-1]=edge
 atlas[(idx-1)*64:idx*64]=tile
header='// Generated from original game sky textures. Regional sky coverage.\nstatic const unsigned char kRegionalWeather[128*1024] = {\n'+',\n'.join(','.join(map(str,row)) for row in atlas)+'\n};\n'
(src/'recomp/runtime/cloud_weather.h').write_text(header)
