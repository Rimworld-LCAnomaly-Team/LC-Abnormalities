#requires -Version 5.1
<#
Deterministic Mimicry sprite painter. No external packages, downloaded art or fonts.
All geometry is painted at 2x resolution and resolved into straight-alpha ARGB.
Body contours come exclusively from the supplied masks, including enclosed holes.
#>
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

public static class NothingThereArt {
    const int S = 2;
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
    // Eyelid, veined sclera, iris limbus, radial fibers, pupil and two catchlights.
    static void Eye(Graphics g,float x,float y,float w,float h,float angle,bool blue) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(w/100f,h/52f);
        Paint(g,"M -57 0 C -33 -39 22 -41 58 -6 C 31 42 -26 43 -57 0 Z","D84A4F","650B18",4);
        Paint(g,"M -49 0 C -20 -28 20 -29 49 -5 C 26 28 -20 29 -49 0 Z","FCF3D9","A7B6AC",3);
        Stroke(g,"M -44 -1 L -32 -3 L -25 -11 M -33 -3 L -28 5 M 43 -5 L 32 -5 L 24 7 M 32 -5 L 29 -13","AD5860",1.1f);
        Ellipse(g,-20,-23,42,46,blue?"68B8FF":"4EF2AA",blue?"1346A2":"08754D",2.7f);
        for(int k=0;k<18;k++) {
            double a=k*Math.PI/9; float xx=(float)Math.Cos(a),yy=(float)Math.Sin(a);
            using(Pen p=new Pen(C(blue?"3589D7":"149E72"),.9f))g.DrawLine(p,xx*12,yy*15,xx*18,yy*21);
        }
        Ellipse(g,-6,-18,13,36,"050C12","020609",1);
        using(Brush b=new SolidBrush(Color.White)) {g.FillEllipse(b,-11,-15,10,10);g.FillEllipse(b,7,8,4,4);}
        Stroke(g,"M -50 -3 C -14 -33 23 -31 50 -8","36070D",3.2f);
        Stroke(g,"M -32 29 C -5 38 28 27 38 19","EB6564",1.5f);
        g.Restore(st);
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
    static Bitmap Weapon() {
        Bitmap hi=Canvas(512*S);using(Graphics g=G(hi)) {
            g.ScaleTransform(S,S);
            // Local coordinates: hilt at (0,0), point at (0,440).
            // Basis maps precisely from (104,420) to (398,92).
            using(Matrix m=new Matrix(.745f,.668f,.668f,-.745f,104,420))g.MultiplyTransform(m);
            Ellipse(g,-9,-8,18,18,"50535B","15161D",4);
            Paint(g,"M -6 10 L -6 65 L 7 68 L 7 10 Z","45434A","14151B",3);
            for(int j=0;j<7;j++)Stroke(g,"M -5 "+(14+j*7)+" L 6 "+(19+j*7),"737077",1.5f);
            Paint(g,"M -13 64 L -15 77 L 16 81 L 19 68 Z","92919A","29262C",3);
            Stroke(g,"M -12 72 L 16 75","CF2834",3);
            // Full forged blade beneath the irregular living spine.
            Paint(g,"M -17 78 C -27 145 -27 223 -17 292 C -9 352 -3 404 0 440 C 38 390 55 309 57 230 C 58 155 34 102 17 80 Z","343943","15171E",4.4f);
            Paint(g,"M 28 111 C 53 210 43 333 0 440 C 31 396 59 317 57 230 C 57 179 41 126 28 111 Z","7A8392","343B48",2);
            Stroke(g,"M 53 216 C 56 291 34 380 3 431","B5BFCC",1.9f);
            Stroke(g,"M 28 158 C 34 216 27 286 15 324","720E1C",3.1f);
            // Three dorsal spurs, then the two smaller opposing teeth.
            Paint(g,"M -20 147 C -48 145 -55 161 -58 178 C -43 167 -29 175 -19 174 Z","E8DFCA","8C7E6A",3);
            Paint(g,"M -24 207 C -55 211 -60 235 -55 252 C -46 232 -31 239 -19 234 Z","F1E8D5","968574",3);
            Paint(g,"M -18 269 C -45 279 -46 301 -38 314 C -34 295 -17 298 -10 291 Z","DDD7C6","8C7E6A",3);
            Paint(g,"M 25 139 C 48 143 51 160 51 170 L 25 158 Z","DDD5C3","897562",3);
            Paint(g,"M 29 199 L 48 219 L 27 220 Z","DCD5C5","8A796A",3);
            Paint(g,"M -12 79 C -31 95 -35 123 -28 145 L -37 155 L -29 174 C -35 199 -28 207 -31 226 L -21 241 L -26 256 C -14 273 -22 292 -9 309 L -11 328 C 1 342 0 360 1 382 C 19 350 24 312 27 290 L 37 277 L 27 265 C 36 239 30 228 35 205 L 26 191 L 31 175 C 28 143 22 113 9 84 Z","D9262E","690B13",4.1f);
            Paint(g,"M -23 112 C -5 129 -16 170 -4 191 C 1 204 -3 232 8 243 C 13 223 10 200 15 184 C 6 148 19 125 -1 100 Z","B21D25","4D0A0E",2.5f);
            Paint(g,"M -20 219 C -2 234 -10 267 4 287 L -1 307 L 15 293 C 17 272 8 248 16 232 L 5 216 Z","D33036","810D18",2.6f);
            Paint(g,"M 17 151 L 36 172 L 26 174 L 36 193 L 19 185 Z","BB1C27","580911",2.6f);
            Paint(g,"M -24 263 L -38 285 L -20 281 L -20 308 L -9 288 Z","B51B23","5D0910",2.5f);
            for(int k=0;k<9;k++) {
                int v=105+k*23;
                Stroke(g,"M -20 "+v+" C -26 "+(v+14)+" -13 "+(v+24)+" -10 "+(v+36),"590C14",2.1f);
                Stroke(g,"M 17 "+(v+5)+" C 23 "+(v+16)+" 17 "+(v+22)+" 20 "+(v+30),"EA5758",1.5f);
            }
            Stroke(g,"M -18 120 C -21 134 -17 143 -18 151 M -13 246 L -7 265 M 7 304 L 6 320","F57870",2);
            // Coordinates correspond to the specified base and upper eyes.
            Eye(g,-1,139,48,43,5,false);
            Eye(g,-9,277,25,29,-9,true);
        }
        using(hi)return Resolve(hi,512);
    }
    // Every body shares a normalized 300 x 360 pattern; the mask supplies its anatomy.
    static void Coat(Graphics g,string facing) {
        Paint(g,"M -30 -25 L 330 -25 L 330 390 L -30 390 Z","DA2E34","640A13",0);
        using(LinearGradientBrush sh=new LinearGradientBrush(new Rectangle(0,0,300,360),C("500910"),C("B8161E"),0f)) {
            ColorBlend cb=new ColorBlend();cb.Colors=new Color[]{C("500910"),C("BF1D27"),C("D02B32"),C("920F1A"),C("48090F")};cb.Positions=new float[]{0,.2f,.4f,.72f,1};sh.InterpolationColors=cb;g.FillRectangle(sh,-5,-5,310,370);
        }
        if(facing=="north") {
            Paint(g,"M 143 -8 C 131 75 146 145 137 210 L 150 350 L 164 213 C 151 145 170 62 158 -8 Z","5C0A12","26080D",3);
            for(int j=0;j<7;j++) {
                int y=38+j*37;
                Stroke(g,"M 144 "+y+" C 110 "+(y-4)+" 72 "+(y-32)+" 29 "+(y+1),"560C15",6);
                Stroke(g,"M 140 "+(y+8)+" C 97 "+(y+7)+" 75 "+(y-14)+" 37 "+(y+6),"E3474A",2.5f);
                Stroke(g,"M 160 "+y+" C 196 "+(y-4)+" 236 "+(y-32)+" 278 "+(y+1),"500A13",6);
                Stroke(g,"M 165 "+(y+8)+" C 204 "+(y+7)+" 233 "+(y-14)+" 270 "+(y+6),"BF2935",2.2f);
            }
            for(int j=0;j<5;j++) {
                int y=29+j*25;
                Paint(g,"M 142 "+y+" C 145 "+(y-8)+" 157 "+(y-8)+" 161 "+y+" L 151 "+(y+15)+" Z","E1D6BB","8C7460",2.2f);
            }
        } else if(facing=="east") {
            Paint(g,"M 14 20 C 62 2 140 19 196 78 C 225 117 233 188 266 244 L 216 323 C 200 224 144 205 115 160 C 85 119 62 86 9 86 Z","D62C34","790C16",4);
            for(int j=0;j<6;j++) {
                int y=j*32;
                Stroke(g,"M 39 "+(30+y)+" C 111 "+(24+y)+" 131 "+(89+y)+" 192 "+(112+y),"500A12",4);
                Stroke(g,"M 46 "+(38+y)+" C 108 "+(33+y)+" 137 "+(98+y)+" 185 "+(120+y),"EF5254",2);
            }
            Eye(g,150,153,57,40,-65,true);
            Eye(g,294,220,42,37,-70,false);
        } else {
            // Shirt is a painted solid object, never a transparent neck cutout.
            Paint(g,"M 111 -6 L 187 -6 L 194 48 L 153 94 L 108 46 Z","F4F6F9","B8C5D8",3);
            Paint(g,"M 116 3 L 148 31 L 129 62 L 105 26 Z","FFFFFF","D0D8E5",3);
            Paint(g,"M 181 1 L 151 31 L 171 61 L 194 25 Z","F9FAFC","BBC8D8",3);
            Paint(g,"M 146 28 L 160 30 L 167 43 L 155 56 L 140 43 Z","F0444D","A20E1A",2.5f);
            Paint(g,"M 151 52 L 160 54 L 173 109 L 157 128 L 145 111 Z","D41E28","7A0D14",3);
            Paint(g,"M 209 -9 C 215 41 240 62 228 102 C 208 141 143 159 111 213 C 93 253 73 288 57 337 L 25 295 C 39 214 62 159 117 111 C 164 79 164 40 180 -8 Z","D42C35","7A0A15",4);
            Paint(g,"M 226 47 C 238 111 157 156 123 197 C 106 224 111 239 92 256 L 94 214 C 110 158 194 117 208 71 Z","BC1E29","5A0911",2.7f);
            for(int j=0;j<7;j++) {
                int y=34+j*28;
                Stroke(g,"M 207 "+y+" C 217 "+(y+34)+" 120 "+(y+66)+" 96 "+(y+99),"570C14",3.2f);
                Stroke(g,"M 201 "+(y+9)+" C 193 "+(y+39)+" 126 "+(y+67)+" 109 "+(y+91),"EB5254",1.8f);
            }
            Paint(g,"M 80 128 C 100 126 103 112 104 103 C 118 119 113 139 96 151 Z","EEE4CC","907A64",3);
            Paint(g,"M 66 160 C 91 156 96 144 98 134 C 113 157 97 176 81 181 Z","EEE4CC","907A64",3);
            Paint(g,"M 54 197 C 79 185 80 175 82 166 C 97 188 84 209 65 218 Z","E2D9C3","8C7A64",3);
            Eye(g,162,216,89,54,-15,false);
            // Fine recessed scars: deliberate short marks, no noisy procedural speckle.
            Stroke(g,"M 237 142 L 247 165 L 238 181 M 53 238 L 46 259 M 218 259 L 228 284","540A12",2.7f);
            Stroke(g,"M 242 145 L 250 162 M 213 265 L 222 283","ED5354",1.3f);
        }
        // Weighted hanging coat folds, also visible on side and back.
        for(int j=0;j<6;j++) {
            int x=29+j*48;
            Stroke(g,"M "+x+" 281 C "+(x+9)+" 309 "+(x-8)+" 337 "+(x-10)+" 373","480A11",4);
            Stroke(g,"M "+(x+6)+" 291 C "+(x+15)+" 317 "+(x+1)+" 341 "+(x+2)+" 356","C6323B",2);
        }
        Stroke(g,"M 15 344 C 84 361 224 361 288 345","33080F",4);
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
                float ink=Inside[k]?Math.Max(0,Math.Min(1,5.0f-Distance[k])):1;
                a[i]=(byte)(a[i]*(1-ink)+Ink.B*ink);a[i+1]=(byte)(a[i+1]*(1-ink)+Ink.G*ink);a[i+2]=(byte)(a[i+2]*(1-ink)+Ink.R*ink);
            }
            Write(b,a);
        }
    }
    static Bitmap Worn(string body,string facing) {
        BodyMask mask=new BodyMask(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+body+"_"+facing+".png"));
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);g.TranslateTransform(mask.Bounds.X,mask.Bounds.Y);
                g.ScaleTransform(mask.Bounds.Width/300f,mask.Bounds.Height/360f);Coat(g,facing);
                if(facing=="east") {
                    // Anchor small profile details to each body's real contour, not
                    // to the empty corners of its rectangular bounding box.
                    GraphicsState st=g.Save();
                    g.TranslateTransform(mask.Edge(.18f,true)-16,65);
                    Paint(g,"M -20 -7 L 7 3 L 9 38 L -10 28 Z","F4F6F9","CBD4E1",2.6f);
                    Paint(g,"M -17 -5 L -3 9 L -11 23 L -23 10 Z","FFFFFF","D6DDE7",2.2f);
                    Paint(g,"M -2 11 L 7 15 L 4 27 L 11 54 L -1 49 L -5 27 Z","E3333E","790B16",2.3f);
                    g.Restore(st);
                    for(int j=0;j<2;j++) {
                        float fraction=.25f+j*.10f;st=g.Save();
                        g.TranslateTransform(mask.Edge(fraction,false)+7,fraction*360);
                        Paint(g,"M -8 10 C 9 9 25 3 29 -14 C 38 7 27 23 8 28 Z","E7DDC8","8C7A64",2.8f);
                        Stroke(g,"M 3 16 L 22 5","F4EBD9",1.2f);g.Restore(st);
                    }
                }
            }
            Bitmap b=Resolve(hi,512);mask.Clip(b);return b;
        }
    }
    static Bitmap SuitItem() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                using(GraphicsPath shape=P("M 219 80 C 199 86 181 101 156 110 L 116 242 L 135 279 L 168 260 L 153 414 C 209 439 309 440 367 414 L 348 260 L 379 280 L 397 242 L 358 111 C 334 105 311 86 293 80 L 257 103 Z")) {
                    GraphicsState st=g.Save();g.SetClip(shape);g.TranslateTransform(115,78);g.ScaleTransform(282f/300,356f/360);Coat(g,"south");g.Restore(st);
                    using(Pen p=new Pen(Ink,4.5f)){p.LineJoin=LineJoin.Round;g.DrawPath(p,shape);}
                }
                Stroke(g,"M 159 128 C 175 163 170 224 166 258 M 356 129 C 340 165 344 225 348 260","430A12",4);
                Stroke(g,"M 150 162 L 128 241 L 140 259 M 369 175 L 385 241","EC5253",2.2f);
                Stroke(g,"M 183 414 C 229 425 297 425 342 414","E64B50",2);
            }
            return Resolve(hi,512);
        }
    }
    static Bitmap Gift() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                Paint(g,"M 112 238 C 114 196 150 165 201 165 C 234 119 295 139 318 174 C 358 165 395 200 400 242 C 412 283 387 336 342 343 C 301 384 237 369 222 341 C 174 354 119 314 112 278 Z","BA5960","68121E",5);
                Stroke(g,"M 129 251 C 130 214 151 202 174 196 M 309 333 C 343 346 381 313 386 284","DF8281",3);
                Eye(g,194,221,117,68,-18,false);
                Eye(g,318,232,112,69,21,false);
                Eye(g,260,302,111,66,-6,true);
                Stroke(g,"M 207 164 L 216 187 M 342 322 L 356 331 M 157 297 L 171 313","65121C",3);
            }
            return Resolve(hi,512);
        }
    }
    static Bitmap Cheek(Bitmap master,Rectangle box,bool mirror) {
        Bitmap b=Canvas(512);
        using(Bitmap crop=new Bitmap(300*S,246*S,PixelFormat.Format32bppArgb)) {
            using(Graphics g=G(crop))g.DrawImage(master,new Rectangle(0,0,crop.Width,crop.Height),new Rectangle(108,132,300,246),GraphicsUnit.Pixel);
            if(mirror)crop.RotateFlip(RotateFlipType.RotateNoneFlipX);
            using(Graphics g=G(b))using(ImageAttributes ia=new ImageAttributes()){
                ia.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(crop,box,0,0,crop.Width,crop.Height,GraphicsUnit.Pixel,ia);
            }
        }
        return b;
    }
    static Bitmap Ability(Bitmap sword) {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                string border="M 14 4 L 114 4 L 114 11 L 123 14 L 123 114 L 114 114 L 111 123 L 14 123 L 14 116 L 5 112 L 5 14 L 13 14 Z";
                using(GraphicsPath p=P(border)) {
                    using(PathGradientBrush b=new PathGradientBrush(p)) {b.CenterColor=C("4C101B");b.SurroundColors=new Color[]{C("120608")};g.FillPath(b,p);}
                    using(Pen pen=new Pen(C("746043"),3))g.DrawPath(pen,p);
                }
                using(Pen p=new Pen(C("801321"),2))g.DrawEllipse(p,20,20,89,89);
                using(Pen p=new Pen(C("C42A35"),1))g.DrawEllipse(p,24,24,81,81);
                for(int j=0;j<12;j++) {
                    double a=j*Math.PI/6;using(Pen p=new Pen(C("B03A3E"),1.5f))g.DrawLine(p,64+(float)Math.Cos(a)*39,64+(float)Math.Sin(a)*39,64+(float)Math.Cos(a)*43,64+(float)Math.Sin(a)*43);
                }
                Paint(g,"M 15 20 C 33 70 65 97 114 111 C 65 110 28 78 15 20 Z","DD2637","7E0C1B",0);
                Paint(g,"M 18 24 C 44 68 73 92 113 109 C 65 95 32 63 18 24 Z","FFF3E5","FFBBAA",0);
                g.DrawImage(sword,new Rectangle(5,5,118,118));
                Stroke(g,"M 18 8 L 110 8 M 9 22 L 9 106 M 21 119 L 106 119","971D2B",1.5f);
                Paint(g,"M 10 9 L 20 9 L 10 20 Z","C8AB76","695137",1);
                Paint(g,"M 118 118 L 108 118 L 118 108 Z","C8AB76","695137",1);
            }
            return Resolve(hi,128);
        }
    }
    static void Verify() {
        if(files.Count!=24)throw new Exception("Expected exactly 24 deliverables, got "+files.Count);
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])) {
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid size/format: "+files[j]);
            byte[] a=Read(b);int opaque=0,visible=0;int minX=512,minY=512,maxX=-1,maxY=-1;
            for(int k=0;k<a.Length/4;k++) {if(a[k*4+3]==255)opaque++;if(a[k*4+3]>0){visible++;minX=Math.Min(minX,k%b.Width);maxX=Math.Max(maxX,k%b.Width);minY=Math.Min(minY,k/b.Width);maxY=Math.Max(maxY,k/b.Width);}}
            bool empty=files[j].EndsWith("Accessory_north.png",StringComparison.OrdinalIgnoreCase);
            if(empty?visible!=0:opaque==0)throw new Exception("Invalid alpha population: "+files[j]);
            if(files[j].EndsWith("Accessory_south.png")&&(minX<212||minY<236||maxX>=248||maxY>=266))throw new Exception("South cheek outside box");
            if(files[j].EndsWith("Accessory_east.png")&&(minX<259||minY<237||maxX>=294||maxY>=265))throw new Exception("East cheek outside box");
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3}, visible={4}, bbox=({5},{6})-({7},{8})",files[j].Substring(root.Length+1),b.Width,b.Height,opaque,visible,minX,minY,maxX,maxY);
        }
        Console.WriteLine("SUCCESS: all 24 PNGs verified; accessory north is fully transparent.");
    }
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();
        using(Bitmap weapon=Weapon()) {
            Save(weapon,"Things/Item/Equipment/WeaponMelee/EgoWeapon_NothingThere.png",512);
            using(Bitmap icon=Resolve(weapon,256))Save(icon,"UI/Commands/ExtractEGO_NothingThere_Weapon.png",256);
            using(Bitmap icon=Ability(weapon))Save(icon,"UI/WeaponSkills/NothingThere/Mimicry_Goodbye.png",128);
        }
        foreach(string body in new string[]{"Male","Female","Thin","Hulk","Fat"})foreach(string face in new string[]{"south","north","east"})
            using(Bitmap suit=Worn(body,face))Save(suit,"Things/Pawn/Humanlike/Apparel/NothingThereSuit/NothingThereSuit_"+body+"_"+face+".png",512);
        using(Bitmap suit=SuitItem()) {
            Save(suit,"Things/Pawn/Humanlike/Apparel/NothingThereSuit/NothingThereSuit.png",512);
            using(Bitmap icon=Resolve(suit,128))Save(icon,"UI/Commands/ExtractEGO_NothingThere_Armor.png",128);
        }
        using(Bitmap gift=Gift()) {
            string dir="Things/Pawn/Humanlike/Apparel/Accessory/NothingThereAccessory";
            Save(gift,dir+".png",512);
            using(Bitmap b=Cheek(gift,new Rectangle(212,236,36,30),false))Save(b,dir+"_south.png",512);
            using(Bitmap b=Cheek(gift,new Rectangle(259,237,35,28),true))Save(b,dir+"_east.png",512);
            using(Bitmap b=Canvas(512))Save(b,dir+"_north.png",512);
        }
        Verify();
    }
}
'@
if (-not ('NothingThereArt' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[NothingThereArt]::Run($ModRoot)
