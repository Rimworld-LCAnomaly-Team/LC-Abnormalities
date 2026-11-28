#requires -Version 5.1
# Deterministic Group E painter: C# GDI+, 3x supersampling, mask-only inputs.

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

public static class GroupEArt {
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
    static Rectangle Bounds(Bitmap b,int cutoff) {
        byte[] a=Read(b);int l=b.Width,t=b.Height,r=-1,bot=-1;
        for(int k=0;k<a.Length/4;k++)if(a[k*4+3]>cutoff){int x=k%b.Width,y=k/b.Width;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bot=Math.Max(bot,y);}
        return r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,bot+1);
    }
    static readonly string[] Names={"LittleRedRidingHoodedMercenary","Schadenfreude","Ppodae"};
    static readonly string[] Skills={"CrimsonScar_HuntBarrage","Gaze_KeyholeSawstorm","SoCute_FrenzyMaul"};
    static readonly Dictionary<string,BodyMask> masks=new Dictionary<string,BodyMask>();
    static readonly Dictionary<string,Rectangle> registered=new Dictionary<string,Rectangle>();
    static readonly HashSet<string> empty=new HashSet<string>();
    static string Apparel="Things/Pawn/Humanlike/Apparel/";
    static string WeaponPath(int theme) {return "Things/Item/Equipment/"+(theme==0?"WeaponRanged/":"WeaponMelee/")+"EgoWeapon_"+Names[theme]+".png";}
    static string F(float v){return v.ToString("0.###",CultureInfo.InvariantCulture);}
    static void Rivet(Graphics g,float x,float y,float r) {
        Ellipse(g,x-r,y-r,r*2,r*2,"E9D6A0","5B5148",1);
        Stroke(g,"M "+F(x-r*.4f)+" "+F(y)+" L "+F(x+r*.4f)+" "+F(y),"4C3F3C",.7f);
    }
    static void Keyhole(Graphics g,float x,float y,float scale) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        Ellipse(g,-25,-36,50,50,"F9D492","6D513C",3);
        Paint(g,"M -11 -2 L 11 -2 L 20 34 L -20 34 Z","D7AD6D","66503D",3);
        Ellipse(g,-17,-28,34,34,"FF8280","9A0826",2);
        Paint(g,"M -7 -2 L 7 -2 L 13 26 L -13 26 Z","FF5A65","B31032",2);
        Stroke(g,"M -9 -20 C -6 -25 0 -25 5 -23","FFE2B6",2);
        g.Restore(st);
    }
    static void Paw(Graphics g,float x,float y,float scale) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        Paint(g,"M -23 11 C -25 -3 -13 -17 0 -9 C 15 -20 28 -3 24 11 C 20 23 7 20 0 18 C -10 22 -22 23 -23 11 Z","F6C6D4","B96C87",2);
        for(int k=0;k<4;k++) {GraphicsState q=g.Save();g.TranslateTransform(-26+k*17,-24+(k==0||k==3?8:0));g.RotateTransform(-25+k*17);Ellipse(g,-7,-11,14,22,"FFE0E6","D899AE",1.6f);g.Restore(q);}
        g.Restore(st);
    }
    static void Fur(Graphics g,int seed,bool rear) {
        Random r=new Random(seed);
        for(int i=0;i<170;i++) {
            float x=r.Next(-8,310),y=r.Next(8,360),len=r.Next(5,14);
            Stroke(g,"M "+F(x)+" "+F(y)+" C "+F(x-4)+" "+F(y+len*.4f)+" "+F(x+4)+" "+F(y+len*.7f)+" "+F(x+1)+" "+F(y+len),i%3==0?"ACAFB8":"FAF8F4",i%3==0?1.2f:2);
        }
    }
    static void Buckle(Graphics g,float x,float y) {
        Paint(g,"M "+F(x)+" "+F(y)+" L "+F(x+25)+" "+F(y)+" L "+F(x+25)+" "+F(y+24)+" L "+F(x)+" "+F(y+24)+" Z","D9C59B","71685C",2);
        Paint(g,"M "+F(x+6)+" "+F(y+6)+" L "+F(x+19)+" "+F(y+6)+" L "+F(x+19)+" "+F(y+18)+" L "+F(x+6)+" "+F(y+18)+" Z","332831","231D25",1);
        Stroke(g,"M "+F(x+10)+" "+F(y+12)+" L "+F(x+26)+" "+F(y+12),"F2DAB1",2);
    }
    // Coordinates are normalized to each exact vanilla mask's bounds, never traced polygons.
    static void Coat(Graphics g,int theme,string facing) {
        bool south=facing=="south",north=facing=="north";
        if(theme==0) {
            Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z","42434B","191C25",0);
            for(int i=0;i<6;i++)Stroke(g,"M "+(35+i*46)+" 85 C "+(15+i*46)+" 190 "+(45+i*46)+" 270 "+(30+i*46)+" 370","56545D",2);
            Paint(g,"M 75 49 L 149 24 L 225 49 L 211 192 L 150 215 L 89 192 Z","47414A","22212B",4);
            Stroke(g,"M 99 65 L 109 183 L 150 197 L 192 183 L 203 65","797078",2);
            if(north) {
                Paint(g,"M 95 -20 C 119 -43 180 -43 206 -18 L 249 19 L 309 58 L 300 326 L 277 346 L 259 329 L 233 366 L 214 345 L 179 372 L 160 345 L 132 368 L 111 342 L 81 365 L 68 335 L 32 345 L -9 320 L -9 61 L 53 20 Z","F13F49","790D24",4);
                Paint(g,"M 100 -18 C 122 -37 180 -37 202 -18 L 216 27 C 201 61 174 86 151 93 C 126 82 99 59 85 27 Z","EA3948","9E1731",3);
                Stroke(g,"M 105 0 C 115 35 132 57 151 70 C 171 55 189 34 197 0","FF6464",2.8f);
                Stroke(g,"M 95 34 C 119 83 182 84 207 34 M 77 78 C 49 179 64 259 43 315 M 226 78 C 253 189 235 273 265 319 M 151 103 L 150 323","A31732",5);
                Stroke(g,"M 102 -8 C 129 -22 176 -23 197 -5 M 89 89 C 72 179 89 246 77 310 M 170 103 C 191 200 175 251 187 315","FF6665",3);
                Stroke(g,"M 176 177 L 204 206 M 171 188 L 197 219 M 166 201 L 187 225","431423",3);
            } else {
                Paint(g,"M 99 -18 C 125 -41 172 -40 201 -18 L 243 14 L 311 49 L 296 135 L 274 124 L 287 316 L 265 345 L 251 329 L 234 359 L 213 342 L 217 193 L 207 88 L 155 61 L 98 91 L 83 211 L 87 344 L 65 333 L 44 357 L 28 332 L 4 340 L 18 130 L -9 141 L -8 50 L 57 16 Z","F4444B","850D27",4);
                Paint(g,"M 94 -10 C 113 -32 184 -33 207 -7 L 197 34 L 154 64 L 105 37 Z","D82C40","6D1129",3);
                Stroke(g,"M 106 2 C 133 19 172 21 195 1 M 34 67 C 50 110 35 186 46 266 M 263 62 C 244 149 261 218 251 304","FF6967",3);
                Stroke(g,"M 79 87 L 96 193 L 110 310 M 223 88 L 202 193 L 191 310","171922",13);
                Stroke(g,"M 79 87 L 96 193 L 110 310 M 223 88 L 202 193 L 191 310","78615D",4);
                for(int k=0;k<4;k++) {int x=112+k*20;Paint(g,"M "+x+" 108 L "+(x+10)+" 108 L "+(x+10)+" 144 L "+x+" 144 Z","D7AF7C","735B49",1.5f);}
                Stroke(g,"M 146 156 L 171 177 M 141 168 L 165 190 M 138 181 L 158 202","DF3046",3);
                Paint(g,"M 30 229 L 272 229 L 272 253 L 30 253 Z","4E3539","211D27",3);Buckle(g,south?138:177,229);
                for(int k=0;k<2;k++){int x=55+k*155;Paint(g,"M "+x+" 252 L "+(x+38)+" 252 L "+(x+38)+" 294 L "+x+" 294 Z","5B4546","2D2730",3);Stroke(g,"M "+x+" 263 L "+(x+38)+" 263","B6907B",2);Rivet(g,x+19,270,2);}
                if(!south)Stroke(g,"M 216 42 C 191 108 222 177 211 232","FF5B60",4);
            }
        } else if(theme==1) {
            Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z","535E69","222937",0);
            for(int k=0;k<6;k++){int x=k*60;Paint(g,"M "+x+" 43 L "+(x+52)+" 37 L "+(x+47)+" 325 L "+x+" 343 Z",k%2==0?"46515E":"3A4654","252D3A",2);Stroke(g,"M "+(x+5)+" 62 L "+(x+5)+" 312","AA8959",2);}
            Paint(g,"M 5 32 L 62 5 L 100 41 L 78 100 L 3 108 L -9 68 Z","83909A","303A48",4);
            Paint(g,"M 238 5 L 296 32 L 309 68 L 297 108 L 222 100 L 200 41 Z","76838F","2C3645",4);
            Paint(g,"M 100 -14 L 130 -4 L 150 40 L 169 -4 L 200 -14 L 212 56 L 151 89 L 89 56 Z","78818A","323A49",3);
            Paint(g,"M 78 91 L 111 64 L 190 64 L 223 91 L 215 201 L 151 223 L 85 201 Z","656F7B","2C3544",4);
            Paint(g,"M 100 95 L 200 95 L 198 184 L 151 202 L 103 184 Z","202632","0E1420",3);
            if(north){Paint(g,"M 112 65 L 188 65 L 210 181 L 151 211 L 91 181 Z","8F754E","3C3C40",3);Keyhole(g,151,139,1.1f);}
            else Keyhole(g,south?150:173,141,south?.94f:.8f);
            Paint(g,"M -9 227 L 309 227 L 309 253 L -9 253 Z","3C353A","191D28",3);Buckle(g,south?138:183,228);
            Stroke(g,"M 148 258 L 148 374","161B27",7);Stroke(g,"M 140 262 L 140 351 M 160 262 L 160 351","B39662",2);
            for(int y=275;y<350;y+=28){Rivet(g,129,y,3);Rivet(g,171,y,3);}
            foreach(Point p in new Point[]{new Point(18,52),new Point(59,28),new Point(73,76),new Point(281,52),new Point(241,28),new Point(227,76),new Point(95,106),new Point(205,106),new Point(98,183),new Point(202,183)})Rivet(g,p.X,p.Y,3.5f);
            for(int k=0;k<3;k++){int x=26+k*108;Stroke(g,"M "+x+" 302 L "+(x+20)+" 284 L "+(x+20)+" 264","BA985F",2);Rivet(g,x,302,2);}
        } else {
            Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z","FFFEFA","C5C8D1",0);Fur(g,196+north.GetHashCode(),north);
            Paint(g,"M 72 56 L 110 39 L 150 68 L 191 39 L 228 56 L 211 211 L 91 211 Z","F8F6F2","B9BDC9",3);
            Stroke(g,"M 75 76 L 99 231 M 225 76 L 201 231","403640",11);Stroke(g,"M 75 76 L 99 231 M 225 76 L 201 231","B82D49",6);
            Paint(g,"M 95 -18 L 205 -18 L 215 33 L 202 51 L 186 46 L 170 63 L 150 52 L 130 63 L 114 46 L 98 51 L 85 33 Z","FFFFFF","D3D5DB",3);
            Paint(g,"M -8 37 L 47 11 L 77 32 L 89 61 L 73 77 L 59 67 L 43 89 L 28 77 L 11 91 L -8 75 Z","FFFFFF","C9CCD5",3);
            Paint(g,"M 253 11 L 308 37 L 308 75 L 289 91 L 272 77 L 257 89 L 241 67 L 227 77 L 211 61 L 223 32 Z","FFFFFF","C9CCD5",3);
            Paint(g,"M -8 306 C 82 325 221 325 308 306 L 308 357 L 290 366 L 274 352 L 254 371 L 237 358 L 217 374 L 198 360 L 177 375 L 153 361 L 130 375 L 109 360 L 87 374 L 67 358 L 46 371 L 28 352 L 9 366 L -8 357 Z","FFFFFF","C9CCD4",3);
            Paint(g,"M 11 224 C 78 237 225 237 289 224 L 290 251 C 216 267 82 267 10 251 Z","E34D64","851E38",3);Buckle(g,south?139:180,235);
            for(int x=28;x<282;x+=23)Ellipse(g,x,243,3,3,"E4B397","AA5660",.5f);
            if(south) {Ellipse(g,112,105,77,86,"FFFCF7","E0D7DD",2);Paw(g,150,152,.9f);}
            else if(north){Paint(g,"M 113 71 L 186 71 L 192 160 L 150 180 L 107 160 Z","D1B0B9","756773",3);Paw(g,150,130,.67f);Stroke(g,"M 120 195 C 95 212 115 228 139 218","AB7285",2);}
            else Paw(g,184,146,.65f);
            for(int k=0;k<3;k++)Stroke(g,"M "+(185+k*10)+" 284 L "+(176+k*10)+" 303","A93450",2.1f);
            for(int k=0;k<10;k++){int x=12+k*29;Stroke(g,"M "+x+" 334 L "+(x+3)+" 349 L "+(x+9)+" 339","EFF0F2",2);}
        }
    }
    static Bitmap Worn(int theme,string body,string face) {
        string key=body+"_"+face;BodyMask m;
        if(!masks.TryGetValue(key,out m)){m=new BodyMask(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+key+".png"));masks[key]=m;}
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);g.TranslateTransform(m.Bounds.X,m.Bounds.Y);g.ScaleTransform(m.Bounds.Width/300f,m.Bounds.Height/360f);Coat(g,theme,face);}
            Bitmap b=Resolve(hi,512);m.Clip(b);return b;
        }
    }
    static Bitmap SuitItem(int theme) {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                using(GraphicsPath p=P("M 205 57 L 254 77 L 305 57 L 350 89 L 389 111 L 429 249 L 407 286 L 367 269 L 389 438 C 306 466 202 466 122 438 L 145 269 L 105 286 L 83 249 L 123 111 L 163 89 Z")) {
                    GraphicsState st=g.Save();g.SetClip(p);g.TranslateTransform(83,57);g.ScaleTransform(346f/300,400f/360);Coat(g,theme,"south");g.Restore(st);
                    using(Pen pen=new Pen(Ink,4)){pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);}
                }
                Stroke(g,"M 134 137 L 153 218 L 145 269 M 376 137 L 359 218 L 367 269",theme==2?"A2A6B4":"181923",3);
            }
            return Resolve(hi,512);
        }
    }
    // Render isolated gear at 3x, then register its nonzero-alpha bounds at 3x before resolving.
    static Bitmap Fit(Bitmap hi,Rectangle target) {
        Rectangle b=Bounds(hi,0);
        if(b.IsEmpty)throw new Exception("Cannot register empty painting");
        using(Bitmap fitted=Canvas(512*S)) {
            using(Graphics g=G(fitted))using(ImageAttributes ia=new ImageAttributes()) {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(hi,new Rectangle(target.X*S,target.Y*S,target.Width*S,target.Height*S),b.X,b.Y,b.Width,b.Height,GraphicsUnit.Pixel,ia);
            }
            Bitmap result=Resolve(fitted,512);
            // Bicubic's faint support pixels must not move exact equipment/head registration.
            byte[] a=Read(result);
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)if(!target.Contains(x,y)){int i=(y*512+x)*4;a[i]=a[i+1]=a[i+2]=a[i+3]=0;}
            Write(result,a);return result;
        }
    }
    static void Gun(Graphics g) {
        Paint(g,"M 57 179 L 117 177 L 139 206 L 184 208 L 197 239 L 172 264 L 152 264 L 134 307 L 84 306 L 98 247 L 69 228 Z","56606D","222533",4);
        Paint(g,"M 105 233 L 146 246 L 127 300 L 90 296 Z","952F40","3F1E2B",3);
        for(int k=0;k<5;k++)Stroke(g,"M "+(103-k*3)+" "+(252+k*9)+" L "+(132-k*3)+" "+(258+k*9),"D26A66",2);
        Paint(g,"M 168 212 L 205 211 L 217 230 L 201 250 L 164 250 L 168 237 L 191 237 L 196 229 L 177 228 Z","918A87","313440",3);
        Paint(g,"M 117 169 L 182 153 L 244 164 L 435 164 L 454 179 L 454 210 L 245 217 L 213 233 L 124 222 Z","727D89","282B3A",4);
        Paint(g,"M 249 176 L 435 176 L 442 186 L 442 201 L 249 204 Z","A32A3D","4F192B",2);
        Stroke(g,"M 261 180 L 425 180 M 259 207 L 428 207","D45B61",2);
        Paint(g,"M 154 165 L 219 167 L 234 186 L 222 219 L 159 220 L 143 201 Z","8B8E96","343746",3);
        for(int k=0;k<4;k++){int x=160+k*16;Paint(g,"M "+x+" 178 L "+(x+10)+" 178 L "+(x+9)+" 209 L "+x+" 209 Z","262735","171824",1);Stroke(g,"M "+(x+3)+" 180 L "+(x+3)+" 204","A3A4AA",1);}
        Paint(g,"M 421 156 L 435 156 L 439 166 L 420 166 Z","C4B99E","51505A",2);
        Paint(g,"M 128 158 L 145 158 L 153 171 L 128 175 Z","D4C3A0","64575A",2);
        Stroke(g,"M 280 187 L 304 198 M 301 185 L 323 196 M 324 183 L 346 194","F57873",2.5f);
        Rivet(g,130,206,4);Rivet(g,248,189,3);Rivet(g,399,191,3);
        Stroke(g,"M 84 309 C 62 332 54 313 67 301","A68B6F",3);
    }
    static void Sickle(Graphics g) {
        Paint(g,"M 102 278 L 127 266 L 203 399 L 179 415 Z","7F4E55","241D2A",4);
        for(int k=0;k<10;k++){int x=110+k*7,y=283+k*12;Stroke(g,"M "+x+" "+y+" L "+(x+21)+" "+(y-7),k%2==0?"B58A7D":"472E38",3);}
        Paint(g,"M 94 265 L 117 251 L 146 285 L 118 304 Z","C1AC9B","4B424C",3);
        Paint(g,"M 111 269 C 27 175 63 77 163 45 C 268 11 383 70 412 175 C 376 110 293 81 232 102 C 165 120 144 176 164 228 L 182 252 L 152 281 Z","E14753","670C28",4);
        Paint(g,"M 163 45 C 267 11 383 70 412 175 C 368 95 275 69 209 91 C 141 112 118 166 136 218 C 94 152 107 89 163 45 Z","FF7773","B0213A",1.5f);
        Stroke(g,"M 179 62 C 265 37 355 81 384 122","FFC0A2",2.5f);
        Stroke(g,"M 101 136 C 86 184 114 233 141 252","3F172A",4);
        for(int k=0;k<3;k++)Stroke(g,"M "+(183+k*23)+" 66 L "+(171+k*23)+" 88","76152D",3);
        Rivet(g,121,274,5);Rivet(g,189,402,4);
    }
    static void Saw(Graphics g,float x,float y,float radius,int teeth) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(radius/100,radius/100);
        using(GraphicsPath p=new GraphicsPath()) {
            List<PointF> pts=new List<PointF>();
            for(int k=0;k<teeth;k++)for(int t=0;t<4;t++) {double a=(k+t/4.0)*Math.PI*2/teeth;float r=t<2?100:87;pts.Add(new PointF((float)Math.Cos(a)*r,(float)Math.Sin(a)*r));}
            p.AddPolygon(pts.ToArray());using(Brush b=new SolidBrush(C("B2B6B7")))g.FillPath(b,p);using(Pen pen=new Pen(Ink,2.2f)){pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);}
        }
        Ellipse(g,-80,-80,160,160,"626F7B","232C3A",3);Ellipse(g,-64,-64,128,128,"303846","171E2B",2);
        for(int k=0;k<8;k++){double a=k*Math.PI/4;Rivet(g,(float)Math.Cos(a)*70,(float)Math.Sin(a)*70,3);}
        Keyhole(g,0,0,1.35f);Stroke(g,"M -61 -31 C -40 -72 18 -76 49 -45","A8AFB4",2);g.Restore(st);
    }
    static void Halberd(Graphics g) {
        Paint(g,"M 54 449 L 337 128 L 357 147 L 76 471 Z","8B959B","28313F",4);
        Stroke(g,"M 68 443 L 324 150","C5BBA2",3);
        Paint(g,"M 64 426 L 155 324 L 177 343 L 84 450 Z","51454D","201D2B",3);
        for(int k=0;k<9;k++){int x=77+k*9,y=417-k*10;Stroke(g,"M "+x+" "+y+" L "+(x+17)+" "+(y+14),"9C7770",3);}
        Paint(g,"M 250 130 L 294 87 L 348 108 L 403 176 L 383 223 L 340 241 L 301 202 Z","616D7A","252B39",4);
        Saw(g,359,137,112,28);
        Paint(g,"M 273 206 L 318 233 L 270 276 L 238 271 L 255 240 Z","949D9E","34404B",3);
        Stroke(g,"M 265 237 L 283 225","DDC290",2);Rivet(g,283,209,5);Rivet(g,72,454,4);
    }
    static void ClawPaw(Graphics g) {
        // Steel talons sit behind the plush knuckles; red tips are deliberate tiny accents.
        for(int k=0;k<4;k++){
            GraphicsState st=g.Save();g.TranslateTransform(205+k*46,155+(k==0||k==3?16:0));g.RotateTransform(-23+k*10);
            Paint(g,"M -11 3 C -25 -28 -8 -56 4 -76 C 3 -45 25 -26 12 5 Z","F5F1EB","8B909E",3);
            Paint(g,"M 4 -76 C 3 -58 7 -45 11 -36 L -5 -43 Z","F27079","9F1638",1.6f);g.Restore(st);
        }
        Paint(g,"M 137 227 C 128 198 141 168 168 169 C 172 139 201 132 222 149 C 242 119 276 126 287 147 C 317 127 348 143 347 168 C 378 166 391 193 382 215 C 405 255 376 288 344 291 L 192 298 C 154 293 132 270 137 227 Z","FFFFFF","C2C6D1",4);
        for(int k=0;k<5;k++) {int x=157+k*43;Stroke(g,"M "+x+" 190 C "+(x-8)+" 203 "+(x+2)+" 219 "+(x-3)+" 231","A7AAB7",2);Stroke(g,"M "+(x+8)+" 187 C "+x+" 208 "+(x+13)+" 213 "+(x+9)+" 226","FFFFFF",3);}
        Paw(g,268,242,.95f);
        Paint(g,"M 155 273 C 212 296 314 297 368 276 L 372 304 C 297 326 211 324 151 302 Z","DE465C","7E1B38",3);Buckle(g,248,291);
        for(int x=180;x<351;x+=22)Rivet(g,x,301,2);
        for(int k=0;k<11;k++){int x=150+k*21;Stroke(g,"M "+x+" 258 L "+(x+6)+" 270 L "+(x+11)+" 260","FDFCF9",2);}
    }
    static Rectangle WeaponBox(int theme,int companion) {
        if(companion==1)return Rectangle.FromLTRB(104,178,496,334);
        if(companion==2)return Rectangle.FromLTRB(46,64,468,422);
        return theme==0?Rectangle.FromLTRB(46,108,478,438):theme==1?Rectangle.FromLTRB(45,22,498,475):Rectangle.FromLTRB(131,116,393,300);
    }
    static Bitmap Weapon(int theme,int companion) {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                if(companion==1)Gun(g);else if(companion==2)Sickle(g);
                else if(theme==0){GraphicsState st=g.Save();g.TranslateTransform(0,27);g.ScaleTransform(.96f,.78f);Gun(g);g.Restore(st);st=g.Save();g.TranslateTransform(157,121);g.ScaleTransform(.72f,.75f);g.RotateTransform(9);Sickle(g);g.Restore(st);}
                else if(theme==1)Halberd(g);else ClawPaw(g);
            }
            return Fit(hi,WeaponBox(theme,companion));
        }
    }
    static void Ears(Graphics g,bool rear,bool side) {
        Paint(g,"M 48 73 C 107 36 207 36 267 73 L 259 86 C 192 59 123 60 55 86 Z","FFFEFA","A8AABB",3);
        Paint(g,"M 86 64 C 87 40 68 5 40 2 C 11 -1 -2 19 5 49 L 16 86 L 22 82 C 25 105 46 112 60 97 L 64 101 L 72 84 L 80 87 Z","FFFFFF","BEC1CD",4);
        Paint(g,"M 224 64 C 224 40 243 5 271 2 C 300 -1 313 19 306 49 L 295 86 L 289 82 C 286 105 265 112 251 97 L 247 101 L 239 84 L 231 87 Z","FFFFFF","BEC1CD",4);
        if(!rear){Paint(g,"M 30 17 C 48 12 68 40 72 64 L 56 88 C 40 97 25 72 22 48 C 19 32 21 22 30 17 Z","FFE0E8","D08FA7",2);Paint(g,"M 281 17 C 263 12 243 40 239 64 L 255 88 C 271 97 286 72 289 48 C 292 32 290 22 281 17 Z","FFE0E8","D08FA7",2);Stroke(g,"M 33 22 C 29 42 38 64 50 75 M 278 22 C 282 42 273 64 261 75","F8BFCE",2);}
        else {Stroke(g,"M 27 20 C 19 40 29 70 44 84 M 284 20 C 292 40 282 70 267 84","E3E4E8",3);}
        Stroke(g,"M 10 40 L 18 55 L 21 47 M 301 40 L 293 55 L 290 47","FFFFFF",3);
        if(side){Stroke(g,"M 233 63 C 240 44 250 28 260 20","F7F5F5",3);}
    }
    static Rectangle AccessoryBox(int theme,string face) {
        if(theme==0)return face=="item"?Rectangle.FromLTRB(72,185,440,327):face=="south"?Rectangle.FromLTRB(219,258,294,273):Rectangle.FromLTRB(255,258,304,273);
        if(theme==1)return face=="item"?Rectangle.FromLTRB(102,72,410,440):face=="south"?Rectangle.FromLTRB(189,314,231,363):Rectangle.FromLTRB(256,314,298,363);
        return face=="item"?Rectangle.FromLTRB(86,189,426,323):face=="east"?Rectangle.FromLTRB(206,168,314,239):Rectangle.FromLTRB(192,168,320,239);
    }
    static Bitmap Accessory(int theme,string face) {
        if(theme<2&&face=="north")return Canvas(512);
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                if(theme==0){
                    Paint(g,"M 48 173 C 93 151 159 167 205 183 C 262 157 339 156 395 177 L 381 246 L 310 252 L 265 270 L 211 252 L 148 272 L 95 250 L 55 238 Z","6A2C3B","251B2B",4);
                    Paint(g,"M 52 177 C 156 191 257 192 390 178 L 384 196 C 265 219 153 213 54 195 Z","9C3C4A","471D30",2);
                    Stroke(g,"M 67 216 C 151 237 246 218 318 233 M 111 249 L 162 236 L 201 243","B05A62",3);
                    Stroke(g,"M 285 183 L 307 219 M 302 178 L 325 214 M 319 175 L 341 208","C52743",4);
                    for(int k=0;k<8;k++){int x=93+k*31;Stroke(g,"M "+x+" 190 L "+(x+5)+" 200","D5A193",1.5f);}
                } else if(theme==1) {
                    Ellipse(g,137,87,160,206,"B49A69","45454D",4);
                    Paint(g,"M 150 119 L 171 97 L 266 97 L 286 120 L 286 267 L 264 291 L 171 291 L 150 267 Z","9A8055","353C49",4);
                    Paint(g,"M 169 124 L 268 124 L 268 264 L 169 264 Z","272B38","101824",3);Keyhole(g,219,195,1.75f);
                    foreach(Point p in new Point[]{new Point(163,115),new Point(276,115),new Point(163,274),new Point(276,274)})Rivet(g,p.X,p.Y,4);
                    Stroke(g,"M 206 292 L 207 318 L 233 318 L 233 292","B39B6F",5);
                } else Ears(g,face=="north",face=="east");
            }
            return Fit(hi,AccessoryBox(theme,face));
        }
    }
    static void Frame(Graphics g,int theme) {
        Paint(g,"M 17 5 L 111 5 L 123 17 L 123 111 L 111 123 L 17 123 L 5 111 L 5 17 Z",theme==2?"35313F":"39202B","10121E",3);
        Stroke(g,"M 19 8 L 109 8 L 120 19 L 120 109 L 109 120 L 19 120 L 8 109 L 8 19 Z","CEAD70",2);
        Stroke(g,"M 23 14 L 105 14 L 114 23 L 114 105 L 105 114 L 23 114 L 14 105 L 14 23 Z",theme==2?"C58F9C":"9E3049",1.5f);
        for(int k=0;k<4;k++){GraphicsState st=g.Save();g.TranslateTransform(64,64);g.RotateTransform(k*90);Paint(g,"M -4 -54 L 0 -59 L 4 -54 L 0 -49 Z","F1D194","8D634C",.7f);g.Restore(st);}
    }
    static Bitmap Icon(int theme,int kind,Bitmap art) {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);Frame(g,theme);
                if(kind<2){Rectangle r=Bounds(art,0);float scale=Math.Min((kind==0?77f:94f)/r.Width,(kind==0?88f:86f)/r.Height);int w=(int)(r.Width*scale),h=(int)(r.Height*scale);g.DrawImage(art,new Rectangle(64-w/2,65-h/2,w,h),r,GraphicsUnit.Pixel);}
                else if(theme==0){
                    for(int k=0;k<3;k++){int y=35+k*19;Stroke(g,"M 28 "+(y+15)+" L 91 "+y,"6D1F36",7);Stroke(g,"M 30 "+(y+14)+" L 98 "+(y-2),"F35361",3);Ellipse(g,93,y-6,9,9,"FFF0B7","F24E5E",1);}
                    Paint(g,"M 24 87 C 47 114 95 101 106 55 C 89 81 61 89 44 81 L 36 69 Z","FF9B8E","AE1A3F",2);
                    Stroke(g,"M 34 84 L 47 103","C1A084",6);
                } else if(theme==1){Saw(g,64,65,44,22);}
                else {Paw(g,60,65,1.15f);for(int k=0;k<3;k++){int x=53+k*17;Paint(g,"M "+x+" 34 C "+(x-15)+" 59 "+(x-22)+" 78 "+(x-21)+" 99 L "+(x-12)+" 92 C "+(x-12)+" 70 "+(x-4)+" 47 "+x+" 34 Z","FF8995","C01240",1);Stroke(g,"M "+(x-2)+" 42 L "+(x-16)+" 87","FFE6D4",1.3f);}}
            }
            return Resolve(hi,128);
        }
    }
    static void RegisteredSave(Bitmap b,string path,Rectangle box,bool transparent) {
        Save(b,path,512);string full=Path.Combine(root,"Cont/Textures",path);if(transparent)empty.Add(full);else registered[full]=box;
    }
    static void Verify() {
        if(files.Count!=74||new HashSet<string>(files).Count!=74)throw new Exception("Expected exactly 74 unique outputs, got "+files.Count);
        int[] counts=new int[3];
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])) {
            string name=Path.GetFileNameWithoutExtension(files[j]);int theme=name.Contains(Names[0])||name.Contains("CrimsonScar")?0:name.Contains(Names[1])||name.Contains("KeyholeSawstorm")?1:2;counts[theme]++;
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid format: "+name);
            byte[] a=Read(b);int opaque=0,visible=0;
            for(int k=0;k<a.Length/4;k++){if(a[k*4+3]==255)opaque++;if(a[k*4+3]>0)visible++;else if(a[k*4]!=0||a[k*4+1]!=0||a[k*4+2]!=0)throw new Exception("Hidden RGB: "+name);}
            Rectangle box=Bounds(b,0);
            if(empty.Contains(files[j])){if(visible!=0)throw new Exception("North accessory must be transparent: "+name);}
            else if(opaque==0||visible==b.Width*b.Height)throw new Exception("Invalid alpha: "+name);
            string prefix=Names[theme]+"Suit_";
            if(name.StartsWith(prefix)) {
                string key=name.Substring(prefix.Length);BodyMask m=masks[key];
                if(!name.EndsWith("_south")&&!name.EndsWith("_north")&&!name.EndsWith("_east"))throw new Exception("Non-lowercase direction: "+name);
                foreach(string f in Directory.GetFiles(Path.GetDirectoryName(files[j])))if(string.Equals(Path.GetFileName(f),name+".png",StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(f)!=name+".png")throw new Exception("Wrong on-disk filename case: "+f);
                for(int k=0;k<m.Inside.Length;k++) {
                    if(m.Inside[k]&&a[k*4+3]<128)throw new Exception("Unsealed interior: "+name);
                    if(!m.Inside[k]&&m.Original[k*4+3]==0&&a[k*4+3]!=0)throw new Exception("Mask leak: "+name);
                    if(m.Inside[k]&&m.Distance[k]<=1.42f&&(Math.Abs(a[k*4]-Ink.B)>1||Math.Abs(a[k*4+1]-Ink.G)>1||Math.Abs(a[k*4+2]-Ink.R)>1))throw new Exception("Missing inward ink: "+name);
                }
                using(Bitmap original=new Bitmap(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+key+".png")))if(box!=Bounds(original,0))throw new Exception("Mask bbox mismatch: "+name);
            }
            Rectangle target;
            if(registered.TryGetValue(files[j],out target)&&(Math.Abs(box.Left-target.Left)>1||Math.Abs(box.Top-target.Top)>1||Math.Abs(box.Right-target.Right)>1||Math.Abs(box.Bottom-target.Bottom)>1))throw new Exception("Registration mismatch: "+name+box+" target="+target);
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3}, visible={4}, bbox=({5},{6})-({7},{8})",name,b.Width,b.Height,opaque,visible,box.Left,box.Top,box.Right-1,box.Bottom-1);
        }
        if(counts[0]!=26||counts[1]!=24||counts[2]!=24)throw new Exception("Wrong per-group counts: "+string.Join(",",counts));
        Console.WriteLine("SUCCESS: all 74 PNGs (26 + 24 + 24) passed format, alpha, zero hidden RGB, sealed-mask, inward-ink, lowercase filenames and equipment/head registration checks.");
    }
    static void Thumbnail(Graphics g,string rel,Rectangle dst) {
        using(Bitmap b=new Bitmap(Path.Combine(root,"Cont/Textures",rel))) {
            Rectangle src=Bounds(b,0);if(src.IsEmpty)return;
            float scale=Math.Min((float)dst.Width/src.Width,(float)dst.Height/src.Height);int w=(int)(src.Width*scale),h=(int)(src.Height*scale);
            g.DrawImage(b,new Rectangle(dst.X+(dst.Width-w)/2,dst.Y+(dst.Height-h)/2,w,h),src,GraphicsUnit.Pixel);
        }
    }
    static void Review() {
        string[] labels={"FEMALE / S","FEMALE / N","FEMALE / E","MALE / S","WEAPON","ACCESSORY + S/N/E","ARMOR ICON","WEAPON ICON","SKILL ICON","GUN","SICKLE"};
        string[] titles={"WAW  /  CRIMSON SCAR","HE  /  GAZE","TETH  /  SO CUTE!!!"};
        using(Bitmap sheet=new Bitmap(2232,1110,PixelFormat.Format32bppArgb))using(Graphics g=G(sheet))using(Font title=new Font(FontFamily.GenericSansSerif,17,FontStyle.Bold))using(Font label=new Font(FontFamily.GenericSansSerif,9,FontStyle.Bold))using(Brush text=new SolidBrush(C("EDE1CC"))) {
            g.Clear(C("171923"));g.DrawString("GROUP E  /  74 REDESIGNED E.G.O. ASSETS",title,text,20,12);g.DrawString("3x GDI+ / exact sealed vanilla masks / #140A0C ink / original equipment and head registration",label,text,22,44);
            for(int theme=0;theme<3;theme++) {
                int y=75+theme*340;g.DrawString(titles[theme],title,text,20,y);
                string suit=Apparel+Names[theme]+"Suit/"+Names[theme]+"Suit";
                string acc=Apparel+"Accessory/"+Names[theme]+"Accessory";
                string[] paths={suit+"_Female_south.png",suit+"_Female_north.png",suit+"_Female_east.png",suit+"_Male_south.png",WeaponPath(theme),acc+".png","UI/Commands/ExtractEGO_"+Names[theme]+"_Armor.png","UI/Commands/ExtractEGO_"+Names[theme]+"_Weapon.png","UI/WeaponSkills/"+Names[theme]+"/"+Skills[theme]+".png","Things/Item/Equipment/WeaponRanged/EgoWeapon_CrimsonScar_Gun.png","Things/Item/Equipment/WeaponRanged/EgoWeapon_CrimsonScar_Sickle.png"};
                for(int col=0;col<11;col++) {
                    int x=16+col*200,top=y+36;
                    using(Brush bg=new SolidBrush(C("30343F")))g.FillRectangle(bg,x,top,192,290);
                    for(int yy=0;yy<14;yy++)for(int xx=0;xx<9;xx++)if((xx+yy)%2==0)using(Brush bg=new SolidBrush(C("383D48")))g.FillRectangle(bg,x+6+xx*20,top+24+yy*18,20,18);
                    g.DrawString(labels[col],label,text,x+7,top+6);
                    if(col>=9&&theme!=0){g.DrawString("--",label,text,x+87,top+140);continue;}
                    Rectangle dst=new Rectangle(x+12,top+35,168,240);
                    if(col==5)dst=new Rectangle(x+13,top+35,166,150);
                    if(col>=6&&col<=8)dst=new Rectangle(x+25,top+75,142,142);
                    Thumbnail(g,paths[col],dst);
                    if(col==5) {
                        string[] faces={"south","north","east"};
                        for(int k=0;k<3;k++) {g.DrawString(faces[k].Substring(0,1).ToUpperInvariant(),label,text,x+19+k*58,top+195);Thumbnail(g,acc+"_"+faces[k]+".png",new Rectangle(x+7+k*61,top+215,54,48));}
                    }
                }
            }
            string output=Path.Combine(Path.GetTempPath(),"Vanilla-GroupE-review.png");sheet.Save(output,ImageFormat.Png);Console.WriteLine("REVIEW "+output);
        }
    }
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();masks.Clear();registered.Clear();empty.Clear();
        for(int theme=0;theme<3;theme++) {
            using(Bitmap weapon=Weapon(theme,0)) {
                RegisteredSave(weapon,WeaponPath(theme),WeaponBox(theme,0),false);
                using(Bitmap icon=Icon(theme,1,weapon))Save(icon,"UI/Commands/ExtractEGO_"+Names[theme]+"_Weapon.png",128);
            }
            foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})using(Bitmap b=Worn(theme,body,face))Save(b,Apparel+Names[theme]+"Suit/"+Names[theme]+"Suit_"+body+"_"+face+".png",512);
            using(Bitmap suit=SuitItem(theme)) {
                Save(suit,Apparel+Names[theme]+"Suit/"+Names[theme]+"Suit.png",512);
                using(Bitmap icon=Icon(theme,0,suit))Save(icon,"UI/Commands/ExtractEGO_"+Names[theme]+"_Armor.png",128);
            }
            foreach(string face in new string[]{"item","south","north","east"})using(Bitmap b=Accessory(theme,face))RegisteredSave(b,Apparel+"Accessory/"+Names[theme]+"Accessory"+(face=="item"?"":"_"+face)+".png",AccessoryBox(theme,face),theme<2&&face=="north");
            using(Bitmap icon=Icon(theme,2,null))Save(icon,"UI/WeaponSkills/"+Names[theme]+"/"+Skills[theme]+".png",128);
        }
        for(int companion=1;companion<=2;companion++)using(Bitmap b=Weapon(0,companion))RegisteredSave(b,"Things/Item/Equipment/WeaponRanged/EgoWeapon_CrimsonScar_"+(companion==1?"Gun":"Sickle")+".png",WeaponBox(0,companion),false);
        Verify();Review();
    }
}
'@
if (-not ('GroupEArt' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[GroupEArt]::Run($ModRoot)

