import sys
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageEnhance
S=sys.argv[1]; A='C:/Users/Personel/Desktop/FarmFury_Arcade/Tools/storyline/assets/'
W,H=2560,1440
bg=Image.open(S+'/backdrop-main.png').convert('RGB')
bg=bg.resize((W,int(bg.height*W/bg.width)),Image.LANCZOS)
bg=bg.crop((0,40,W,40+H)) if bg.height>=H+40 else bg.resize((W,H))
bg=ImageEnhance.Color(bg).enhance(0.85)
bg=ImageEnhance.Brightness(bg).enhance(0.42)
# extra darkening behind the logo strip (soft oval)
m=Image.new('L',(W,H),0); d=ImageDraw.Draw(m)
d.ellipse((330,420,2230,1020),fill=120); m=m.filter(ImageFilter.GaussianBlur(120))
bg=Image.composite(Image.new('RGB',(W,H),(10,6,4)),bg,m).convert('RGBA')
def logo(path,h):
    im=Image.open(path).convert('RGBA'); bb=im.getbbox(); im=im.crop(bb)
    return im.resize((int(im.width*h/im.height),h),Image.LANCZOS)
def paste(im,x,y):
    sh=Image.new('RGBA',im.size,(0,0,0,0)); sh.putalpha(im.split()[3].point(lambda a:int(a*0.7)))
    sh=sh.filter(ImageFilter.GaussianBlur(14))
    bg.alpha_composite(sh,(x+6,y+10)); bg.alpha_composite(im,(x,y))
cy=720
ff=logo('C:/Users/Personel/Desktop/FarmFury_Technical/FarmFury_Artwork/Rough_Icons/Logo_game.png',360)
ar=logo(A+'arcade-logo.png',215); st=logo(A+'stampede-logo.png',200)
fx=600; paste(ff,fx,cy-ff.height//2)
dx=fx+ff.width+70
ImageDraw.Draw(bg).line((dx,cy-170,dx,cy+170),fill=(255,196,40,220),width=5)
right_l=dx+70; right_r=2000; rw=right_r-right_l
gap=60; tot=ar.width+gap+st.width; x0=right_l+(rw-tot)//2
ty=cy-180
paste(ar,x0,ty+(215-ar.height)); paste(st,x0+ar.width+gap,ty+(215-st.height)//2+8)
f1=ImageFont.truetype('C:/Users/Personel/Desktop/FarmFury_Arcade/Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf',78)
f2=ImageFont.truetype('C:/Users/Personel/Desktop/FarmFury_Arcade/Assets/TextMesh Pro/Examples & Extras/Fonts/Bangers.ttf',54)
dr=ImageDraw.Draw(bg)
def text(s,f,y,col):
    w=dr.textlength(s,font=f); x=right_l+(rw-w)/2
    dr.text((x,y),s,font=f,fill=col,stroke_width=5,stroke_fill=(40,18,6))
text('FREE ON THE APP STORE & GOOGLE PLAY',f1,cy+60,(255,255,255))
text('FARMFURYGAMES.COM',f2,cy+150,(255,196,40))
out=bg.convert('RGB'); out.save(S+'/youtube_banner_franchise_v2.png')
p=out.copy(); d=ImageDraw.Draw(p)
d.rectangle((0,508,W,931),outline=(0,255,0),width=4); d.rectangle((507,508,2053,931),outline=(255,0,0),width=4)
p.resize((1280,720)).save(S+'/preview.png')
out.crop((0,508,W,931)).save(S+'/desktop_strip.png')
