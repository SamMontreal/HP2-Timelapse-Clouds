import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

from struct import unpack_from

def read_track_skies(path):
    """Decode the 8-bit PS2 sky panorama and cap straight from an unpacked track."""
    raw = path.read_bytes()
    def chunks(start, end):
        while start + 8 <= end:
            tag, length = unpack_from('<II', raw, start)
            begin = start + 8
            start = begin + length
            if start > end:
                raise ValueError(f'{path}: truncated texture chunk')
            yield tag, begin, start
    images = {}
    for tag, start, end in chunks(0, len(raw)):
        if tag != 0xB0300001:
            continue
        blocks = {kind: (begin, stop) for kind, begin, stop in chunks(start, end)}
        info_start, info_end = blocks[0x30300003]
        data_start, data_end = blocks[0x30300004]
        data = memoryview(raw)[(data_start + 127) & ~127:data_end]
        for record in range(info_start, info_end - 163, 164):
            name = raw[record + 8:record + 32].split(b'\0')[0]
            if not name.startswith(b'SKY') or b'REFLECT' in name:
                continue
            width, height, bpp = unpack_from('<HHB', raw, record + 0x24)
            side = width == 512
            if not side and b'CAP' not in name:
                continue
            if (width, height, bpp) not in ((512, 256, 8), (256, 256, 8)):
                raise ValueError(f'{path}: unsupported sky {name!r} ({width}x{height}, {bpp} bpp)')
            pixels, clut = unpack_from('<II', raw, record + 0x30)
            indices = np.frombuffer(data, np.uint8, width * height, pixels)
            if raw[record + 0x55] == 1:
                y, x = np.indices((height, width))
                cy, cx = y % 16, x % 16
                column = cy // 4
                swap = ((cy // 2) & 1) ^ (column & 1)
                address = ((y // 64) * 64 * width + ((y % 64) // 16) * 16 * width
                           + column * 4 * width + (cy & 1) * 2 * width + ((cy // 2) & 1)
                           + (x // 128) * 256 + ((x % 128) // 16) * 32 + (cx // 8) * 2
                           + (cx & 1) * 4 + ((cx // 2) & 1) * 8 + (((cx // 4) & 1) ^ swap) * 16)
                indices = indices[address]
            else:
                indices = indices.reshape(height, width)
            palette = np.frombuffer(data, np.uint8, 1024, clut).reshape(256, 4)
            order = np.arange(256)
            order ^= np.where(np.isin(order & 0x18, [0x08, 0x10]), 0x18, 0)
            images[side] = Image.fromarray(palette[order][indices, :3])
    if set(images) != {True, False}:
        raise ValueError(f'{path}: missing sky panorama or cap')
    return images[True], images[False]


parser = argparse.ArgumentParser(description="Build regional cloud coverage from an installed HP2 port.")
parser.add_argument("game", type=Path)
parser.add_argument("upstream", type=Path)
args = parser.parse_args()
root, src = args.game, args.upstream
atlas = np.zeros((1024, 128), dtype=np.uint8)
for idx, track in enumerate((11, 14, 21, 24, 31, 34, 41, 44, 61, 64), 1):
 side, cap = read_track_skies(root / "data" / "TRACKS" / f"TEX{track}TRACK.tex")
 a=np.asarray(side.filter(ImageFilter.GaussianBlur(2)),dtype=float)/255
 c=np.asarray(cap.filter(ImageFilter.GaussianBlur(2)),dtype=float)/255
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
def shape_brick(resolution=40):
    """Bake a periodic 3D field. Arrays are z, y, x to match the shader's slice layout."""
    rng = np.random.default_rng(31722)
    grid = np.indices((resolution,) * 3, dtype=np.float32) / resolution

    def value_noise(cells):
        coordinates = grid * cells
        i = coordinates.astype(np.int32)
        f = coordinates - i
        f = f*f*(3.0 - 2.0*f)
        lattice = rng.random((cells,) * 3)
        result = np.zeros(grid.shape[1:])
        for dz in (0, 1):
            for dy in (0, 1):
                for dx in (0, 1):
                    weight = (f[0] if dz else 1-f[0]) * (f[1] if dy else 1-f[1]) * (f[2] if dx else 1-f[2])
                    result += weight * lattice[(i[0]+dz)%cells,(i[1]+dy)%cells,(i[2]+dx)%cells]
        return result

    def billows(cells):
        coordinates = grid * cells
        i = coordinates.astype(np.int32)
        f = coordinates - i
        seeds = rng.uniform(.12,.88,(cells,cells,cells,3))
        nearest = np.full(grid.shape[1:],3.0)
        for dz in (-1,0,1):
            for dy in (-1,0,1):
                for dx in (-1,0,1):
                    p = seeds[(i[0]+dz)%cells,(i[1]+dy)%cells,(i[2]+dx)%cells]
                    dist = (p[...,0]+dz-f[0])**2 + (p[...,1]+dy-f[1])**2 + (p[...,2]+dx-f[2])**2
                    np.minimum(nearest,dist,out=nearest)
        return np.clip(1-np.sqrt(nearest),0,1)

    def normalize(field):
        lo, hi = np.percentile(field,[1,99])
        return np.uint8(np.rint(np.clip((field-lo)/(hi-lo),0.,1.)*255))

    mass = .74*value_noise(3) + .26*value_noise(6)
    lobes = .77*billows(6) + .23*billows(11)
    erosion = .60*billows(12) + .40*value_noise(15)
    return np.stack([normalize(field) for field in (mass,lobes,erosion)],axis=-1)

# Preserve regional coverage in red. The other channels contain the periodic noise
# brick, with a one-texel wrap border so hardware bilinear filtering stays in its slice.
brick = shape_brick()
packed = np.zeros((1024,128,4),np.uint8)
packed[:,:,0] = atlas
side = brick.shape[0]
pitch = side+2
for z, plane in enumerate(brick):
    x, y = (z%3)*pitch, (z//3)*pitch
    packed[y:y+pitch,x:x+pitch,1:4] = np.pad(plane,((1,1),(1,1),(0,0)),mode='wrap')

# Bake the animated geometry once. Only one interpolated tile is uploaded per frame.
# Each circular lobe has its own life cycle, lateral slip and peak height. The
# envelopes close smoothly at birth/death so nothing jumps at the time wrap.
frames, side = 42, 128
rng = np.random.default_rng(9275)
z, x = np.indices((side,side), dtype=np.float32) / side
floor = .18
caps = np.full((frames,side,side),floor,dtype=np.float32)
bases = caps.copy()
groups = [(.17,.19,.145,.89),(.70,.28,.12,.78),(.44,.68,.163,.95),
          (.91,.78,.115,.83),(.19,.86,.075,.56),(.84,.02,.065,.46)]
for cx,cz,size,height in groups:
    for idx in range(18):
        stem = idx < 3
        phase, angle = rng.random(), rng.uniform(0,math.tau)
        offset = size * (rng.uniform(0,.4) if stem else rng.uniform(.19,1.22))
        px, pz = cx + math.cos(angle)*offset*1.2, cz + math.sin(angle)*offset*.84
        radius = size * (rng.uniform(.68,.90) if stem else rng.uniform(.32,.69))
        crest = height * rng.uniform(.85,1.13) * (1.0-.30*offset/size)
        turns = 1 if stem else int(rng.choice([1,2,2,3]))
        slip = size * rng.uniform(.14,.41)
        for i in range(frames):
            age = (phase + i*turns/frames) % 1.
            if stem:
                pulse = .5-.5*math.cos(math.tau*age)
                spread, lift = .78+.22*pulse, .48+.48*pulse
                dx, dz = .29*slip*math.sin(math.tau*age), .32*slip*(2*pulse-1)
            else:
                rise = min(age/.58,1.)
                fade = min((1.-age)/.42,1.)
                lift = rise*rise*(3.-2.*rise)*fade*fade*(3.-2.*fade)
                spread = math.sin(math.pi*age)**.85
                dx, dz = .32*slip*math.sin(math.tau*age), slip*(1.8*age-.72)
            if spread < .005:
                continue
            rx=(x-px-dx+.5)%1.-.5
            rz=(z-pz-dz+.5)%1.-.5
            dome=np.sqrt(np.maximum(0.,1.-(rx/(radius*spread*1.10))**2-(rz/(radius*spread))**2))
            np.maximum(caps[i],floor+max(.01,crest-floor)*lift*dome,out=caps[i])
            np.minimum(bases[i],floor-(.07+.075*lift)*spread*dome,out=bases[i])
thickness=caps-bases
occupied=thickness>0.
stages=[]
for i in range(frames):
    # The marcher skips only columns empty in BOTH interpolation endpoints.
    reached=occupied[i] | occupied[(i+1)%frames]
    distance=np.zeros((side,side),np.uint8)
    while not np.all(reached):
        distance[~reached]+=1
        wide=reached|np.roll(reached,1,axis=1)|np.roll(reached,-1,axis=1)
        reached=wide|np.roll(wide,1,axis=0)|np.roll(wide,-1,axis=0)
    field=np.stack([caps[i],bases[i],thickness[i]],axis=-1)
    rgba=np.empty((side,side,4),np.uint8)
    rgba[:,:,0]=distance
    rgba[:,:,1:]=np.rint(np.clip(field,0.,1.)*255).astype(np.uint8)
    stages.append(np.pad(rgba,((1,1),(0,0),(0,0)),mode='wrap'))
packed[767:897] = stages[0]
def words(array):
    rows=array.copy().view('<u4').reshape(-1,128)
    return ',\n'.join(','.join(f'0x{int(v):08x}' for v in row) for row in rows)
header='// Regional weather, periodic noise and one active cumulus stage (RGBA8).\n'
header+='static const unsigned kCloudAtlas[128*1024] = {\n'+words(packed)+'\n};\n'
header+='static constexpr int kCloudStageCount = '+str(frames)+';\n'
header+='static const unsigned kCloudStages[kCloudStageCount][130*128] = {\n'
header+=',\n'.join('{'+words(stage)+'}' for stage in stages)+'\n};\n'
(src/'recomp/runtime/cloud_weather.h').write_text(header,encoding='ascii')
