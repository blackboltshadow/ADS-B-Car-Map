from PIL import Image,ImageDraw,ImageFilter
import math
T='C:/Users/Krzysztof/AppData/Local/Temp/prev/'
im=Image.open(r'M:\Pictures\2026\20260731_RudnikiAirShow_1\TS-11_Iskra\Obrobione\KWR_7522.jpg').convert('RGB').resize((2000,1600),Image.LANCZOS)
nose=(470,382);tail=(1200,960)
ang=math.degrees(math.atan2(tail[1]-nose[1],tail[0]-nose[0]))
rot=im.rotate(-(90-ang),resample=Image.BICUBIC,center=((nose[0]+tail[0])/2,(nose[1]+tail[1])/2),fillcolor=(10,60,130))
OX,OY=300,100
rot=rot.crop((OX,OY,OX+1300,OY+1400))
W,H=rot.size
px=rot.load()
CUT=1062
m=Image.new('L',(W,H),0);mp=m.load()
for y in range(H):
    for x in range(W):
        r,g,b=px[x,y]
        mp[x,y]=0 if (b>r+55 and y<CUT) or y>=CUT else 255
        if y>=CUT: mp[x,y]=0
# fill holes: flood sky from corner
inv=m.point(lambda v:255-v)
ImageDraw.floodfill(inv,(2,2),128)
inv2=inv.point(lambda v:0 if v==128 else 255)
m=inv2
m=m.filter(ImageFilter.MinFilter(15)).filter(ImageFilter.MaxFilter(15))
m=m.point(lambda v:255 if v>127 else 0)
m.save(T+'mask.png')
CX=530
mp=m.load()
rows={}
ys=[y for y in range(H) if any(mp[x,y] for x in range(0,W,1))]
y0,y1=min(ys),max(ys)
print(y0,y1)
# right half extent per row
def rext(y):
    xs=[x for x in range(CX,W) if mp[x,y]]
    return max(xs)-CX if xs else 0
prof=[(y,rext(y)) for y in range(y0,y1+1)]
print(prof[::60])
import pickle
pickle.dump((y0,y1,prof),open(T+'prof.pkl','wb'))
rot.save(T+'rotc.png')
