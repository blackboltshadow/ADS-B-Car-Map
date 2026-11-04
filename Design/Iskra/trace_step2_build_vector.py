from PIL import Image,ImageDraw
import pickle,math
T='C:/Users/Krzysztof/AppData/Local/Temp/prev/'
y0,y1,prof=pickle.load(open(T+'prof.pkl','rb'))
rot=Image.open(T+'rotc.png').convert('RGB');m=Image.open(T+'mask.png')
px=rot.load();mp=m.load()
CX=530
k=64.0/(y1-y0)
def U(dx):return 54+dx*k
def V(y):return 20+(y-y0)*k
def dp(pts,eps):
    if len(pts)<3:return pts
    a,b=pts[0],pts[-1]
    dx,dy=b[0]-a[0],b[1]-a[1];n=math.hypot(dx,dy) or 1
    best,bi=-1,0
    for i in range(1,len(pts)-1):
        d=abs(dy*(pts[i][0]-a[0])-dx*(pts[i][1]-a[1]))/n
        if d>best:best,bi=d,i
    if best>eps:
        return dp(pts[:bi+1],eps)[:-1]+dp(pts[bi:],eps)
    return [a,b]
# right half outline: down the right edge from nose to tail (points (x,y) with x=half width)
right=[(w,y) for y,w in prof]
right=[(0,y0)]+right+[(0,y1)]
simp=dp(right,2.2)
print(len(simp))
pts=[(U(w),V(y)) for w,y in simp]
left=[(108-x,y) for x,y in reversed(pts)]
poly=pts+left
def f(v):return ('%.1f'%v).rstrip('0').rstrip('.')
sil='M'+' L'.join(f(x)+','+f(y) for x,y in poly)+'Z'
pal={'W':(252,232,205),'P':(244,160,140),'R':(222,12,15),'D':(110,20,25),'N':(15,30,55),'G':(75,90,115),'Y':(230,190,40)}
hexc={'W':'FFF7F3EE','P':'FFF4A6AB','R':'FFE0161B','D':'FF7A1A20','N':'FF0F1E37','G':'FF46556E','Y':'FFE6BE28'}
def cls(c):
    return min(pal,key=lambda n:sum((a-b)**2 for a,b in zip(pal[n],c)))
cs=12.0
ncols=int(60/ (cs*k))+1
cells={}
y=y0
j=0
import itertools
rowsN=int((y1-y0)/cs)+1
for j in range(rowsN):
    ya=y0+j*cs;yb=ya+cs
    for i in range(0,ncols):
        xa=CX+i*cs;xb=xa+cs
        tot=[0,0,0];n=0;tot_px=0
        for yy in range(int(ya),int(min(yb,y1+1))):
            for xx in range(int(xa),int(min(xb,1300))):
                tot_px+=1
                if mp[xx,yy]:
                    r,g,b=px[xx,yy];tot[0]+=r;tot[1]+=g;tot[2]+=b;n+=1
        if n>=0.35*cs*cs:
            cells[(i,j)]=cls(tuple(t/n for t in tot))
# merge horizontal runs per colour, mirrored
paths={c:[] for c in pal if c!='W'}
cw=cs*k
for (i,j),c in cells.items():
    pass
for j in range(rowsN):
    i=0
    while i<ncols:
        c=cells.get((i,j))
        if c is None or c=='W':
            i+=1;continue
        e=i
        while cells.get((e+1,j))==c:e+=1
        ya=V(y0+j*cs);h=cw
        xr0=54+i*cw;xr1=54+(e+1)*cw
        paths[c].append('M%s,%sh%sv%sh-%sz'%(f(xr0),f(ya),f(xr1-xr0),f(h+0.05),f(xr1-xr0)))
        xl0=54-(e+1)*cw
        paths[c].append('M%s,%sh%sv%sh-%sz'%(f(xl0),f(ya),f(xr1-xr0),f(h+0.05),f(xr1-xr0)))
        i=e+1
out=[]
out.append('<?xml version="1.0" encoding="utf-8"?>')
out.append('<vector xmlns:android="http://schemas.android.com/apk/res/android"\n    android:width="108dp"\n    android:height="108dp"\n    android:viewportWidth="108"\n    android:viewportHeight="108">')
out.append('  <group\n      android:scaleX="0.62"\n      android:scaleY="0.62"\n      android:pivotX="54"\n      android:pivotY="54">\n    <path\n        android:fillColor="#FF1565C0"\n        android:pathData="M54,54m-54,0a54,54 0,1 1,108 0a54,54 0,1 1,-108 0" />')
out.append('    <group\n        android:scaleX="1.35"\n        android:scaleY="1.35"\n        android:pivotX="54"\n        android:pivotY="52"\n        android:translateY="2">')
out.append('      <path\n          android:fillColor="#FFF7F3EE"\n          android:pathData="%s" />'%sil)
out.append('      <group>\n        <clip-path android:pathData="%s" />'%sil)
for c in ['P','R','D','Y','G','N']:
    if paths[c]:
        out.append('        <path\n            android:fillColor="#%s"\n            android:pathData="%s" />'%(hexc[c],''.join(paths[c])))
out.append('      </group>\n    </group>\n  </group>\n</vector>\n')
open(r'C:\projekty\AdsbCarMap\Resources\drawable\ic_splash.xml','w',encoding='utf-8',newline='\n').write('\n'.join(out))
print({c:len(v) for c,v in paths.items()})
# preview
S=8
img=Image.new('RGB',(108*S,108*S),(30,34,40))
d=ImageDraw.Draw(img)
d.ellipse((54*S-33.5*S,54*S-33.5*S,54*S+33.5*S,54*S+33.5*S),fill='#1565C0')
sc=0.62*1.35
def tr(x,y):return ((54+(x-54)*sc)*S,(54+(52+(y-52)*1.35+2-54)*0.62)*S) if False else ((54+(x-54)*sc)*S,(54+((y-52)*1.35+52+2-54)*0.62)*S)
plane=Image.new('RGBA',img.size,(0,0,0,0));pd=ImageDraw.Draw(plane)
# base circle scaled .62? no: circle is full here
pd.polygon([tr(x,y) for x,y in poly],fill='#F5F2EE')
mk=Image.new('L',img.size,0);ImageDraw.Draw(mk).polygon([tr(x,y) for x,y in poly],fill=255)
lay=Image.new('RGBA',img.size,(0,0,0,0));ld=ImageDraw.Draw(lay)
for c in paths:
    for s in paths[c]:
        import re
        mm=re.match(r'M([\d.-]+),([\d.-]+)h([\d.-]+)v([\d.-]+)',s)
        x,y,w,h=map(float,mm.groups())
        ld.polygon([tr(x,y),tr(x+w,y),tr(x+w,y+h),tr(x,y+h)],fill='#'+hexc[c][2:])
plane.alpha_composite(Image.composite(lay,Image.new('RGBA',img.size,(0,0,0,0)),mk))
img=img.convert('RGBA');img.alpha_composite(plane);img.convert('RGB').save(T+'splash3.png')
