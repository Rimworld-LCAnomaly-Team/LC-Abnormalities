#requires -Version 5.1
# Group B: PunishingBird, VoidDream, ScorchedGirl. Deterministic painter: C# GDI+, 3x supersampling, mask-only inputs.

[CmdletBinding()]
param([string]$ModRoot)
$ErrorActionPreference = 'Stop'
if (-not $ModRoot) { $ModRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }
Add-Type -AssemblyName System.Drawing
$source = @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class GroupBArt {
    const int S = 3;
    static readonly Color Ink = C("140A0C");
    static string root;
    static List<string> files = new List<string>();
    static List<int> sizes = new List<int>();
    static Color C(string h) { return ColorTranslator.FromHtml("#" + h); }
    static Bitmap Canvas(int n) { return new Bitmap(n,n,PixelFormat.Format32bppArgb); }
    static Graphics G(Bitmap b) {
        Graphics g=Graphics.FromImage(b);
        g.SmoothingMode=SmoothingMode.HighQuality;
        g.InterpolationMode=InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode=PixelOffsetMode.HighQuality;
        g.CompositingQuality=CompositingQuality.HighQuality;
        return g;
    }
    // A deliberately small invariant-culture SVG-like path vocabulary.
    static GraphicsPath P(string data) {
        MatchCollection t=Regex.Matches(data,@"[MLCZ]|-?\d+(?:\.\d+)?");
        GraphicsPath p=new GraphicsPath(); PointF at=new PointF(); int i=0;
        while(i<t.Count) {
            string op=t[i++].Value;
            if(op=="Z") {p.CloseFigure(); continue;}
            int n=op=="C"?6:2; float[] a=new float[n];
            for(int j=0;j<n;j++) a[j]=float.Parse(t[i++].Value,CultureInfo.InvariantCulture);
            PointF end=new PointF(a[n-2],a[n-1]);
            if(op=="M") p.StartFigure();
            else if(op=="L") p.AddLine(at,end);
            else if(op=="C") p.AddBezier(at,new PointF(a[0],a[1]),new PointF(a[2],a[3]),end);
            at=end;
        }
        return p;
    }
    static void Paint(Graphics g,string d,string a,string b,float width=3.6f) {
        using(GraphicsPath p=P(d)) {
            RectangleF r=p.GetBounds(); if(r.Width<1)r.Width=1;if(r.Height<1)r.Height=1;
            using(LinearGradientBrush br=new LinearGradientBrush(r,C(a),C(b),48f)) g.FillPath(br,p);
            if(width>0) using(Pen pen=new Pen(Ink,width)) {pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);}
        }
    }
    static void Stroke(Graphics g,string d,string color,float width) {
        using(GraphicsPath p=P(d)) using(Pen pen=new Pen(C(color),width)) {
            pen.StartCap=pen.EndCap=LineCap.Round; pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);
        }
    }
    static void Ellipse(Graphics g,float x,float y,float w,float h,string a,string b,float line) {
        using(GraphicsPath p=new GraphicsPath()) {
            p.AddEllipse(x,y,w,h);
            using(PathGradientBrush br=new PathGradientBrush(p)) {
                br.CenterPoint=new PointF(x+w*.35f,y+h*.30f);
                br.CenterColor=C(a);br.SurroundColors=new Color[]{C(b)};g.FillPath(br,p);
            }
            if(line>0)using(Pen pen=new Pen(Ink,line)) g.DrawPath(pen,p);
        }
    }
    static Bitmap Resolve(Bitmap hi,int n) {
        Bitmap b=Canvas(n);
        using(Graphics g=G(b)) using(ImageAttributes ia=new ImageAttributes()) {
            ia.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(hi,new Rectangle(0,0,n,n),0,0,hi.Width,hi.Height,GraphicsUnit.Pixel,ia);
        }
        // GDI+ stores straight alpha here; clear hidden RGB to avoid fringe-prone exports.
        byte[] data=Read(b);for(int i=0;i<data.Length;i+=4)if(data[i+3]==0)data[i]=data[i+1]=data[i+2]=0;Write(b,data);
        return b;
    }
    static byte[] Read(Bitmap b) {
        BitmapData d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {byte[] a=new byte[b.Width*b.Height*4];for(int y=0;y<b.Height;y++)Marshal.Copy(IntPtr.Add(d.Scan0,y*d.Stride),a,y*b.Width*4,b.Width*4);return a;}
        finally {b.UnlockBits(d);}
    }
    static void Write(Bitmap b,byte[] a) {
        BitmapData d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try {for(int y=0;y<b.Height;y++)Marshal.Copy(a,y*b.Width*4,IntPtr.Add(d.Scan0,y*d.Stride),b.Width*4);}
        finally {b.UnlockBits(d);}
    }
    static void Save(Bitmap b,string path,int size) {
        string full=Path.Combine(root,"Cont/Textures",path);Directory.CreateDirectory(Path.GetDirectoryName(full));
        b.Save(full,ImageFormat.Png);files.Add(full);sizes.Add(size);
    }
    sealed class BodyMask {
        public bool[] Inside; public byte[] Original; public Rectangle Bounds; public float[] Distance;
        public BodyMask(string path) {
            using(Bitmap src=new Bitmap(path)) {
                if(src.Width!=512||src.Height!=512)throw new Exception("Mask must be 512 square: "+path);
                using(Bitmap b=Canvas(512)) {using(Graphics g=G(b))g.DrawImageUnscaled(src,0,0);Original=Read(b);}
            }
            int n=512*512;bool[] outside=new bool[n];int[] q=new int[n];int head=0,tail=0;
            // Seed EVERY border pixel, robust even if (0,0) is solid.
            for(int x=0;x<512;x++){Enqueue(x,outside,q,ref tail);Enqueue(511*512+x,outside,q,ref tail);}
            for(int y=1;y<511;y++){Enqueue(y*512,outside,q,ref tail);Enqueue(y*512+511,outside,q,ref tail);}
            while(head<tail) {
                int k=q[head++],x=k%512,y=k/512;
                if(x>0)Enqueue(k-1,outside,q,ref tail);if(x<511)Enqueue(k+1,outside,q,ref tail);
                if(y>0)Enqueue(k-512,outside,q,ref tail);if(y<511)Enqueue(k+512,outside,q,ref tail);
            }
            Inside=new bool[n];Distance=new float[n];int minX=512,minY=512,maxX=-1,maxY=-1,holes=0;
            for(int k=0;k<n;k++) {
                Inside[k]=!outside[k];Distance[k]=Inside[k]?10000:0;
                if(Inside[k]) {int x=k%512,y=k/512;minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);if(Original[k*4+3]<128)holes++;}
            }
            if(maxX<0)throw new Exception("Empty body mask: "+path);
            Bounds=Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);
            // Two-pass 8-connected chamfer distance: smooth diagonal contour, no stair-step tracing.
            for(int y=0;y<512;y++)for(int x=0;x<512;x++) {
                int k=y*512+x;if(x>0)D(k,k-1,1);if(y>0){D(k,k-512,1);if(x>0)D(k,k-513,1.414214f);if(x<511)D(k,k-511,1.414214f);}
            }
            for(int y=511;y>=0;y--)for(int x=511;x>=0;x--) {
                int k=y*512+x;if(x<511)D(k,k+1,1);if(y<511){D(k,k+512,1);if(x>0)D(k,k+511,1.414214f);if(x<511)D(k,k+513,1.414214f);}
            }
            Console.WriteLine("MASK {0}: bbox={1}, enclosed pixels filled={2}",Path.GetFileNameWithoutExtension(path),Bounds,holes);
        }
        void D(int k,int other,float delta){Distance[k]=Math.Min(Distance[k],Distance[other]+delta);}
        void Enqueue(int k,bool[] ext,int[] q,ref int tail){if(!ext[k]&&Original[k*4+3]<128){ext[k]=true;q[tail++]=k;}}
        public float Edge(float height,bool right) {
            int y=Bounds.Y+(int)(Bounds.Height*height);
            int x=right?Bounds.Right-1:Bounds.Left,step=right?-1:1;
            while(x>=Bounds.Left&&x<Bounds.Right&&!Inside[y*512+x])x+=step;
            return (x-Bounds.Left)*300f/Bounds.Width;
        }
        public void Clip(Bitmap b) {
            byte[] a=Read(b);
            for(int k=0;k<Inside.Length;k++) {
                int i=k*4;byte alpha=0;
                if(Inside[k])alpha=Distance[k]<=1.42f&&Original[i+3]>=128?Original[i+3]:(byte)255;
                else if(Original[i+3]>0)alpha=Original[i+3];
                a[i+3]=alpha;
                if(alpha==0){a[i]=a[i+1]=a[i+2]=0;continue;}
                float ink=Inside[k]?Math.Max(0,Math.Min(1,4.8f-Distance[k])):1;
                a[i]=(byte)(a[i]*(1-ink)+Ink.B*ink);a[i+1]=(byte)(a[i+1]*(1-ink)+Ink.G*ink);a[i+2]=(byte)(a[i+2]*(1-ink)+Ink.R*ink);
            }
            Write(b,a);
        }
    }
    static readonly Dictionary<string,BodyMask> masks=new Dictionary<string,BodyMask>();
    static Bitmap Worn(string body,string facing) {
        BodyMask mask=new BodyMask(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+body+"_"+facing+".png"));
        masks[body+"_"+facing]=mask;
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {g.ScaleTransform(S,S);g.TranslateTransform(mask.Bounds.X,mask.Bounds.Y);g.ScaleTransform(mask.Bounds.Width/300f,mask.Bounds.Height/360f);Coat(g,facing);}
            Bitmap b=Resolve(hi,512);mask.Clip(b);return b;
        }
    }
    static Bitmap SuitItem() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                using(GraphicsPath shape=P("M 212 62 L 250 80 L 300 62 L 346 85 L 388 108 L 424 253 L 402 289 L 365 273 L 387 434 C 307 464 203 464 125 434 L 146 273 L 110 289 L 88 253 L 124 108 L 166 85 Z")) {
                    GraphicsState st=g.Save();g.SetClip(shape);g.TranslateTransform(88,61);g.ScaleTransform(336f/300,390f/360);Coat(g,"south");g.Restore(st);
                    using(Pen p=new Pen(Ink,3.8f)){p.LineJoin=LineJoin.Round;g.DrawPath(p,shape);}
                }
                Stroke(g,"M 142 126 L 153 205 L 146 273 M 370 126 L 359 205 L 365 273",set==0?"91A2B2":set==1?"AA88CD":"77656B",3.2f);
                Stroke(g,"M 124 259 L 143 264 M 369 264 L 398 259",set==0?"FFFFFF":set==1?Gold:"DE6A3D",3);
            }
            return Resolve(hi,512);
        }
    }
    static Rectangle Bounds(Bitmap b,int cutoff) {
        byte[] a=Read(b);int l=b.Width,t=b.Height,r=-1,bot=-1;
        for(int k=0;k<a.Length/4;k++)if(a[k*4+3]>cutoff){int x=k%b.Width,y=k/b.Width;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bot=Math.Max(bot,y);}
        return r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,bot+1);
    }
    static int set;
    static string[] names={"PunishingBird","VoidDream","ScorchedGirl"};
    static string[] skills={"Beak_PunishingBarrage","EngulfingDream_LullabyBurst","FourthMatchFlame_MatchIgnition"};
    static string Gold="E7BA59";
    static void Star(Graphics g,float x,float y,float s) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
        Paint(g,"M 0 -10 L 3 -3 L 10 0 L 3 3 L 0 10 L -3 3 L -10 0 L -3 -3 Z","FFF0AE","D59B41",1);g.Restore(st);
    }
    static void Moon(Graphics g,float x,float y,float s) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
        Paint(g,"M 7 -16 C -22 -17 -22 20 7 16 C -6 10 -6 -9 7 -16 Z","FFF0AE","B77B34",1.5f);g.Restore(st);
    }
    static void Flame(Graphics g,float x,float y,float s) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
        Paint(g,"M 0 35 C -39 28 -31 0 -19 -15 C -22 3 -8 1 -11 -17 C -14 -35 4 -41 0 -58 C 34 -34 8 -17 23 -9 L 29 -26 C 49 9 29 37 0 35 Z","FFBD3F","C62B25",2.4f);
        Paint(g,"M 1 27 C -18 23 -15 8 -3 -7 C 4 2 0 8 11 1 C 22 18 14 29 1 27 Z","FFF4AC","FF9A28",0);g.Restore(st);
    }
    static void Feather(Graphics g,float x,float y,float s) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
        Paint(g,"M -11 45 C -30 13 -18 -36 14 -54 C 24 -24 27 14 -11 45 Z","FFFFFF","C7D3DD",2.3f);
        Stroke(g,"M -11 45 C 0 17 6 -11 14 -48 M -2 14 L -17 0 M 4 -7 L -12 -21 M -7 28 L 13 15","8395A4",1.4f);
        Paint(g,"M 9 -26 L 14 -54 C 21 -34 20 -26 19 -16 Z","FF4C52","BE1232",1);g.Restore(st);
    }
    static void Horns(Graphics g,bool rear) {
        Stroke(g,"M 42 65 C 92 47 208 47 258 65","140A0C",15);
        Stroke(g,"M 42 65 C 92 47 208 47 258 65","D1A34F",8);
        for(int side=0;side<2;side++) {
            GraphicsState st=g.Save();if(side==1){g.TranslateTransform(300,0);g.ScaleTransform(-1,1);}
            Paint(g,"M 100 66 C 76 93 30 91 12 61 C -11 18 22 -19 62 -5 C 97 6 92 45 62 47 C 42 49 32 28 46 19 C 39 40 65 40 67 24 C 67 9 43 4 31 21 C 15 45 45 69 63 59 L 83 45 Z",rear?"BAA3DF":"E6D7FF","755294",3);
            Stroke(g,"M 20 54 L 35 49 M 18 36 L 31 35 M 28 14 L 36 23 M 47 3 L 49 16 M 66 9 L 61 21 M 84 50 L 74 61 M 62 68 L 60 81","EEC66B",4);
            g.Restore(st);
        }
    }
    static void SleepEye(Graphics g,float x,float y,float s) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
        Paint(g,"M -36 -2 C -15 -21 16 -21 36 -2 C 18 20 -19 20 -36 -2 Z","FFE99A","D7A046",2);
        Stroke(g,"M -26 -2 C -9 10 12 10 27 -2 M -16 5 L -19 12 M 0 8 L 0 16 M 17 5 L 20 12","4D265B",3);g.Restore(st);
    }
    static void Coat(Graphics g,string facing) {
        bool front=facing=="south",back=facing=="north",east=facing=="east";
        string light=set==0?"FFFFFF":set==1?"59338A":"3A363C";
        string dark=set==0?"CFDAE1":set==1?"24133F":"16151C";
        Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z",light,dark,0);
        // Long, weighted folds and sleeve seams, all clipped by the vanilla silhouette.
        for(int k=0;k<7;k++) {
            int x=k*52-8;
            Stroke(g,"M "+x+" 67 C "+(x-14)+" 167 "+(x+19)+" 246 "+(x-5)+" 371",set==0?"AFBDC8":set==1?"211337":"17141C",4);
            Stroke(g,"M "+(x+8)+" 83 C "+(x-2)+" 180 "+(x+26)+" 270 "+(x+6)+" 358",set==0?"FFFFFF":set==1?"76519D":"51464B",2);
        }
        if(set==0) {
            Paint(g,"M 104 -12 L 196 -12 L 213 36 L 176 78 L 150 45 L 123 78 L 88 36 Z","30303A","0D1018",3);
            if(front||east)Paint(g,east?"M 170 70 L 224 85 L 209 120 L 177 135 L 158 108 Z":"M 89 87 L 151 99 L 211 86 L 200 115 L 152 138 L 99 115 Z","FA4254","A80E2E",3);
            if(back) {
                Paint(g,"M 93 91 C 107 130 137 145 151 172 C 166 145 194 131 209 91 C 183 104 168 119 151 132 C 135 119 113 103 93 91 Z","FFFFFF","BDCAD7",2);
                Stroke(g,"M 151 132 L 151 197","9EAEBB",2);
            }
            // Crisp overlapping feather tips on shoulders and coat skirt.
            for(int j=0;j<2;j++)for(int k=0;k<10;k++) {
                int x=k*34-15,y=j==0?62:296;
                Paint(g,"M "+x+" "+y+" C "+(x+10)+" "+(y+10)+" "+(x+22)+" "+(y+9)+" "+(x+34)+" "+y+" L "+(x+23)+" "+(y+43)+" L "+(x+12)+" "+(y+30)+" L "+x+" "+(y+49)+" Z","FFFFFF","D1DBE3",1.6f);
            }
            Stroke(g,"M 148 150 L 148 283","A6B4BE",2);
            if(!back)for(int y=160;y<=264;y+=34)Ellipse(g,east?203:155,y,7,7,"FFF1B4","B17A2B",1);
            Stroke(g,"M 12 277 L 45 280 M 255 280 L 291 277",Gold,4);
        } else if(set==1) {
            Paint(g,"M 103 -10 L 195 -10 L 224 42 L 185 94 L 151 53 L 117 94 L 76 42 Z","B599D1","60467B",3);
            Stroke(g,"M 117 87 L 140 209 L 126 353 M 185 87 L 164 209 L 182 353","BE9DDD",6);
            Stroke(g,"M -10 331 C 75 363 225 363 310 331","D6B56B",5);
            Stroke(g,"M -10 321 C 75 353 225 353 310 321","9C7BBC",2);
            for(int k=0;k<15;k++){int x=22+(k*67)%260,y=108+(k*43)%205;Star(g,x,y,k%3==0?.7f:.43f);}
            Moon(g,back?151:east?194:103,back?118:176,.85f);
            SleepEye(g,east?197:150,back?208:130,east?.62f:.85f);
            for(int y=211;y<310;y+=28)Ellipse(g,151,y,5,5,"FFEFC0","BC883F",.8f);
            for(int x=12;x<300;x+=24)Stroke(g,"M "+x+" 350 L "+(x+3)+" 370","B38FCD",2);
        } else {
            Paint(g,"M 95 -12 L 205 -12 L 219 43 L 181 138 L 150 103 L 117 138 L 82 43 Z","C13A38","5A1C2A",3);
            Paint(g,"M 95 9 L 142 48 L 120 127 L 87 67 L 70 27 Z","4B4046","201D25",3);
            Paint(g,"M 205 9 L 158 48 L 180 127 L 213 67 L 230 27 Z","4B4046","201D25",3);
            for(int k=0;k<4;k++) {
                int y=128+k*27;
                Paint(g,"M 70 "+y+" L 225 "+(y+34)+" L 222 "+(y+54)+" L 73 "+(y+20)+" Z","A59C96","625F64",2);
                Stroke(g,"M 78 "+(y+9)+" L 216 "+(y+40),"C4B6A5",1.2f);
            }
            Paint(g,"M 117 253 L 150 270 L 184 253 L 206 360 L 151 338 L 95 360 Z","DB4935","661B29",2);
            for(int k=0;k<12;k++) {
                int x=9+k*26,y=302+(k%3)*18;
                Stroke(g,"M "+x+" "+y+" L "+(x+9)+" "+(y-15)+" L "+(x+5)+" "+(y-29),"E55B38",2);
                Ellipse(g,x+2,y-16,3,4,"FFD568","D24A2F",0);
            }
            if(front)Flame(g,151,150,.72f);
            else {Flame(g,east?199:150,170,.32f);for(int k=0;k<8;k++)Star(g,63+(k*43)%190,90+(k*29)%140,.25f);}
        }
    }
    // All large object designs are authored in local coordinates, then fitted to the original registration.
    static Bitmap Fit(Bitmap hi,Rectangle target,int n) {
        Rectangle box=Bounds(hi,0);
        using(Bitmap dst=Canvas(n*S)) {
            using(Graphics g=G(dst))using(ImageAttributes ia=new ImageAttributes()) {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(hi,new Rectangle(target.X*S,target.Y*S,target.Width*S,target.Height*S),box.X,box.Y,box.Width,box.Height,GraphicsUnit.Pixel,ia);
            }
            return Resolve(dst,n);
        }
    }
    static Rectangle WeaponTarget() {return set==0?Rectangle.FromLTRB(30,112,478,400):set==1?Rectangle.FromLTRB(78,102,454,410):Rectangle.FromLTRB(30,128,480,384);}
    static Bitmap Weapon() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                if(set==0) {
                    Paint(g,"M 63 219 L 160 223 L 172 255 L 132 383 L 72 376 L 91 264 L 45 250 Z","34343E","101018",4);
                    for(int k=0;k<5;k++)Stroke(g,"M "+(88-k*3)+" "+(281+k*16)+" L "+(145-k*5)+" "+(288+k*16),"70717D",3);
                    Paint(g,"M 159 238 C 220 231 236 263 220 292 C 202 318 164 310 143 296 L 149 281 C 195 297 218 277 201 261 L 157 259 Z","FF4357","B50E31",4);
                    Stroke(g,"M 182 247 C 174 268 183 280 191 280",Gold,5);
                    Paint(g,"M 47 190 L 91 145 L 221 136 L 368 158 L 474 207 L 383 238 L 194 251 L 51 235 Z","FFFFFF","CAD4DE",4);
                    Paint(g,"M 218 166 L 371 175 L 474 207 L 365 213 L 224 207 Z","FDFEFF","CDD9E1",2);
                    Paint(g,"M 205 217 L 366 218 L 434 209 L 381 240 L 203 253 L 153 238 Z","34333B","10101A",3);
                    Stroke(g,"M 77 194 C 135 164 182 162 221 167 M 232 176 L 356 184",Gold,4);
                    Ellipse(g,183,173,21,19,"FF4A5A","8E1129",2);
                    Ellipse(g,188,175,5,5,"FFFFFF","FFF0CE",0);
                    for(int k=0;k<3;k++)Stroke(g,"M "+(93+k*29)+" 210 L "+(118+k*29)+" 195","8F9DAB",2);
                } else if(set==1) {
                    Paint(g,"M 106 242 L 183 238 L 207 272 L 165 396 L 116 386 L 131 285 L 94 276 Z","613C83","271B3D",4);
                    Stroke(g,"M 135 290 L 153 300 L 136 371",Gold,5);
                    Paint(g,"M 104 178 C 167 139 271 139 353 165 L 420 195 L 431 246 L 366 277 L 152 278 L 96 245 Z","70449A","302047",4);
                    Paint(g,"M 320 170 L 402 181 L 427 195 L 424 250 L 397 264 L 320 267 Z","E7C675","966139",3);
                    Ellipse(g,385,186,45,71,"62527D","171324",3);
                    Ellipse(g,395,199,23,44,"E6BDFD","7049B2",2);
                    Ellipse(g,174,165,123,100,"AC75E7","39205C",4);
                    Stroke(g,"M 236 183 C 187 172 187 239 233 245 C 277 247 278 198 242 197 C 216 193 211 225 236 226 C 254 226 250 213 240 214","EBC6FF",5);
                    GraphicsState st=g.Save();g.TranslateTransform(80,103);g.ScaleTransform(.85f,.85f);Horns(g,false);g.Restore(st);
                    Stroke(g,"M 159 265 C 219 288 296 282 339 265",Gold,5);
                    for(int k=0;k<3;k++)Star(g,303+k*29,205,.48f);
                    Moon(g,132,222,.75f);
                } else {
                    Paint(g,"M 68 241 L 170 242 L 193 272 L 152 379 L 92 374 L 110 283 L 54 272 Z","484047","17151B",4);
                    Stroke(g,"M 121 281 L 149 294 L 129 349","B2A096",5);
                    Paint(g,"M 53 169 L 302 155 L 406 178 L 425 224 L 401 267 L 78 267 L 46 235 Z","4E4144","17151C",4);
                    Paint(g,"M 133 182 L 323 180 L 351 203 L 336 240 L 122 240 Z","9E362A","3C1D25",3);
                    for(int k=0;k<5;k++) {
                        int x=137+k*36;Paint(g,"M "+x+" 190 L "+(x+22)+" 190 L "+(x+13)+" 229 L "+(x-9)+" 229 Z","F78B38","C23D2C",2);
                    }
                    Paint(g,"M 341 164 L 393 171 L 407 191 L 401 250 L 346 265 Z","5D5355","201A23",3);
                    Stroke(g,"M 348 181 L 354 243 M 381 185 L 384 240","FE9B43",4);
                    Paint(g,"M 66 150 L 280 139 L 299 155 L 74 174 Z","9C8172","51444B",3);
                    for(int k=0;k<4;k++)Stroke(g,"M "+(83+k*62)+" 172 L "+(97+k*62)+" 185 L "+(94+k*62)+" 200","FF9B47",2);
                    GraphicsState st=g.Save();g.TranslateTransform(429,212);g.RotateTransform(90);Flame(g,0,0,.8f);g.Restore(st);
                    Stroke(g,"M 179 256 L 221 270 L 229 293 L 190 302","D34730",6);
                }
            }
            return Fit(hi,WeaponTarget(),512);
        }
    }
    static Rectangle AccessoryTarget(bool item,string face) {
        if(set==0)return item?Rectangle.FromLTRB(147,72,365,440):Rectangle.FromLTRB(237,300,276,349);
        if(set==1)return item?Rectangle.FromLTRB(72,181,440,331):Rectangle.FromLTRB(203,170,310,208);
        return item?Rectangle.FromLTRB(72,177,439,335):face=="east"?Rectangle.FromLTRB(269,254,304,268):Rectangle.FromLTRB(236,259,276,274);
    }
    static Bitmap Accessory(bool item,string face) {
        if(set!=1&&face=="north")return Canvas(512);
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                if(set==0)Feather(g,200,230,3);
                else if(set==1){g.TranslateTransform(60,160);Horns(g,face=="north");}
                else {
                    Paint(g,"M 65 240 L 366 229 L 370 249 L 66 260 Z","F4D08C","966846",3);
                    Stroke(g,"M 76 245 L 351 237","FFF0C6",2);
                    Ellipse(g,349,222,39,35,"FBA23D","A32C2B",3);
                    if(item)Flame(g,371,207,.8f);
                    else Stroke(g,"M 359 228 L 376 237","FFD58C",4);
                }
            }
            return Fit(hi,AccessoryTarget(item,face),512);
        }
    }
    static void Frame(Graphics g) {
        Paint(g,"M 17 5 L 111 5 L 123 17 L 123 111 L 111 123 L 17 123 L 5 111 L 5 17 Z",set==0?"30323D":set==1?"38234E":"41232B","111019",3);
        Stroke(g,"M 19 8 L 109 8 L 120 19 L 120 109 L 109 120 L 19 120 L 8 109 L 8 19 Z",Gold,2);
        Stroke(g,"M 23 14 L 105 14 L 114 23 L 114 105 L 105 114 L 23 114 L 14 105 L 14 23 Z",set==0?"BF3046":set==1?"9672BC":"B64A32",1.5f);
        Star(g,64,13,.45f);Star(g,64,115,.35f);
    }
    static Bitmap Icon(Bitmap art,bool armor) {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);Frame(g);Rectangle r=Bounds(art,0);
                if(armor)g.DrawImage(art,new Rectangle(30,23,68,82),r,GraphicsUnit.Pixel);
                else {g.TranslateTransform(64,65);g.RotateTransform(-28);g.DrawImage(art,new Rectangle(-48,-30,96,60),r,GraphicsUnit.Pixel);}
            }
            return Resolve(hi,128);
        }
    }
    static Bitmap Ability() {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);Frame(g);
                if(set==0) {
                    for(int k=0;k<3;k++){int y=44+k*21;Stroke(g,"M 31 "+y+" L 97 "+(y-7),"C62B43",7);Stroke(g,"M 40 "+(y-2)+" L 100 "+(y-9),"FFF0D6",2);}
                    Paint(g,"M 26 36 C 45 23 65 38 73 51 L 107 62 L 72 73 L 48 94 L 27 81 L 40 62 Z","FFFFFF","B9C8D5",3);
                    Paint(g,"M 59 55 L 105 62 L 65 67 Z","F5C16B","C44B3C",2);
                    Ellipse(g,47,47,9,9,"F53D51","97122C",1.5f);
                } else if(set==1) {
                    for(int k=0;k<3;k++){int y=42+k*24;Stroke(g,"M 25 "+y+" C 42 "+(y-20)+" 81 "+(y+20)+" 104 "+y,"9674CE",4);}
                    SleepEye(g,64,62,1.05f);Moon(g,92,32,.45f);Star(g,34,92,.7f);Star(g,95,91,.45f);
                } else {
                    for(int k=0;k<4;k++) {GraphicsState st=g.Save();g.TranslateTransform(64,86);g.RotateTransform((k-1.5f)*25);Paint(g,"M -3 -38 L 3 -38 L 3 17 L -3 17 Z","FFE0A1","95634B",1.3f);Ellipse(g,-5,-46,10,13,"FFAC39","9F2A2B",1);g.Restore(st);}
                    Flame(g,64,58,.66f);for(int k=0;k<10;k++)Star(g,26+(k*37)%79,27+(k*19)%72,.23f);
                }
            }
            return Resolve(hi,128);
        }
    }
    static bool Near(Rectangle a,Rectangle b,int tolerance) {
        return Math.Abs(a.Left-b.Left)<=tolerance&&Math.Abs(a.Top-b.Top)<=tolerance&&Math.Abs(a.Right-b.Right)<=tolerance&&Math.Abs(a.Bottom-b.Bottom)<=tolerance;
    }
    static void Verify() {
        if(files.Count!=72||new HashSet<string>(files,StringComparer.OrdinalIgnoreCase).Count!=72)throw new Exception("Expected exactly 72 unique outputs");
        int[] counts=new int[3];
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])) {
            string name=Path.GetFileNameWithoutExtension(files[j]);set=Array.FindIndex(names,n=>files[j].Contains(n));counts[set]++;
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid format: "+name);
            byte[] a=Read(b);int opaque=0,visible=0;
            for(int k=0;k<a.Length/4;k++){if(a[k*4+3]==255)opaque++;if(a[k*4+3]>0)visible++;else if(a[k*4]!=0||a[k*4+1]!=0||a[k*4+2]!=0)throw new Exception("Hidden RGB: "+name);}
            Rectangle box=Bounds(b,0);bool blank=name==names[set]+"Accessory_north"&&set!=1;
            if(blank){if(visible!=0)throw new Exception("North accessory must be transparent: "+name);}
            else if(opaque==0||visible==b.Width*b.Height)throw new Exception("Invalid alpha: "+name);
            string prefix=names[set]+"Suit_";
            if(name.StartsWith(prefix)) {
                string key=name.Substring(prefix.Length).Replace("North","north");BodyMask m=masks[key];
                for(int k=0;k<m.Inside.Length;k++) {
                    if(m.Inside[k]&&a[k*4+3]<128)throw new Exception("Unsealed interior: "+name);
                    if(!m.Inside[k]&&m.Original[k*4+3]==0&&a[k*4+3]!=0)throw new Exception("Mask leak: "+name);
                    if(m.Inside[k]&&m.Distance[k]<=1.42f&&(Math.Abs(a[k*4]-Ink.B)>1||Math.Abs(a[k*4+1]-Ink.G)>1||Math.Abs(a[k*4+2]-Ink.R)>1))throw new Exception("Missing inward ink: "+name);
                }
                using(Bitmap original=new Bitmap(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+key+".png")))if(box!=Bounds(original,0))throw new Exception("Mask bbox mismatch: "+name);
            }
            if(name.StartsWith(names[set]+"Accessory")&&!blank) {
                bool item=name==names[set]+"Accessory";string face=item?"item":name.Substring((names[set]+"Accessory_").Length);
                if(!Near(box,AccessoryTarget(item,face),2))throw new Exception("Head registration: "+name+box);
            }
            if(name.StartsWith("EgoWeapon_")&&!Near(box,WeaponTarget(),2))throw new Exception("Weapon registration: "+name+box);
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3} visible={4} bbox={5}",name,b.Width,b.Height,opaque,visible,box);
        }
        for(int i=0;i<3;i++) {
            if(counts[i]!=24)throw new Exception("Expected 24 outputs for "+names[i]);
            string dir=Path.Combine(root,"Cont/Textures/Things/Pawn/Humanlike/Apparel/"+names[i]+"Suit");bool exact=false;
            foreach(string f in Directory.GetFiles(dir))if(Path.GetFileName(f)==names[i]+"Suit_Fat_North.png")exact=true;
            if(!exact)throw new Exception("Missing exact Fat_North filename: "+names[i]);
        }
        Console.WriteLine("SUCCESS: all 72 PNGs passed format, alpha, hidden RGB, sealed-mask, inward-ink, bbox, head registration and filename checks.");
    }
    static void Review() {
        string[] labels={"Female S","Female N","Female E","Male S","Weapon","Accessory","Armor Icon","Weapon Icon","Skill Icon"};
        using(Bitmap sheet=CanvasRect(2160,900))using(Graphics g=G(sheet))using(Font font=new Font(FontFamily.GenericSansSerif,12,FontStyle.Bold))using(Brush text=new SolidBrush(C("F4E7D2"))) {
            g.Clear(C("181922"));
            for(int row=0;row<3;row++) {
                string n=names[row];int top=row*300;g.DrawString(n+" / TETH",font,text,12,top+5);
                string apparel="Things/Pawn/Humanlike/Apparel/"+n+"Suit/"+n+"Suit";
                string[] paths={apparel+"_Female_south.png",apparel+"_Female_north.png",apparel+"_Female_east.png",apparel+"_Male_south.png","Things/Item/Equipment/WeaponRanged/EgoWeapon_"+n+".png","Things/Pawn/Humanlike/Apparel/Accessory/"+n+"Accessory.png","UI/Commands/ExtractEGO_"+n+"_Armor.png","UI/Commands/ExtractEGO_"+n+"_Weapon.png","UI/WeaponSkills/"+n+"/"+skills[row]+".png"};
                for(int col=0;col<9;col++) {
                    int x=col*240,y=top+30;
                    for(int yy=0;yy<11;yy++)for(int xx=0;xx<10;xx++)using(Brush bg=new SolidBrush(C((xx+yy)%2==0?"30333E":"383C47")))g.FillRectangle(bg,x+4+xx*23,y+27+yy*21,23,21);
                    g.DrawString(labels[col],font,text,x+12,y+3);
                    using(Bitmap art=new Bitmap(Path.Combine(root,"Cont/Textures",paths[col]))) {
                        Rectangle src=col>=6?new Rectangle(0,0,128,128):Bounds(art,0);
                        if(col<4)src=Rectangle.Inflate(src,12,12);
                        float scale=Math.Min(218f/src.Width,218f/src.Height);int w=(int)(src.Width*scale),h=(int)(src.Height*scale);
                        g.DrawImage(art,new Rectangle(x+120-w/2,y+143-h/2,w,h),src,GraphicsUnit.Pixel);
                    }
                }
            }
            string output=Path.Combine(Path.GetTempPath(),"Vanilla-GroupB-review.png");sheet.Save(output,ImageFormat.Png);Console.WriteLine("REVIEW "+output);
        }
    }
    static Bitmap CanvasRect(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();masks.Clear();
        for(set=0;set<3;set++) {
            string n=names[set];
            using(Bitmap weapon=Weapon()) {Save(weapon,"Things/Item/Equipment/WeaponRanged/EgoWeapon_"+n+".png",512);using(Bitmap icon=Icon(weapon,false))Save(icon,"UI/Commands/ExtractEGO_"+n+"_Weapon.png",128);}
            foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})
                using(Bitmap b=Worn(body,face))Save(b,"Things/Pawn/Humanlike/Apparel/"+n+"Suit/"+n+"Suit_"+body+"_"+(body=="Fat"&&face=="north"?"North":face)+".png",512);
            using(Bitmap suit=SuitItem()) {Save(suit,"Things/Pawn/Humanlike/Apparel/"+n+"Suit/"+n+"Suit.png",512);using(Bitmap icon=Icon(suit,true))Save(icon,"UI/Commands/ExtractEGO_"+n+"_Armor.png",128);}
            foreach(string face in new string[]{"item","south","north","east"})using(Bitmap b=Accessory(face=="item",face))Save(b,"Things/Pawn/Humanlike/Apparel/Accessory/"+n+"Accessory"+(face=="item"?"":"_"+face)+".png",512);
            using(Bitmap icon=Ability())Save(icon,"UI/WeaponSkills/"+n+"/"+skills[set]+".png",128);
        }
        Verify();Review();
    }

}
'@
if (-not ('GroupBArt' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[GroupBArt]::Run($ModRoot)
