#requires -Version 5.1
# Deterministic Penitence painter: C# GDI+, 3x supersampling, mask-only inputs.

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

public static class OneSinArt {
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
    // Braided elliptical briar, including the visible rear band on north-facing heads.
    static void Briar(Graphics g,float x,float y,float w,float h,bool rear) {
        for(int strand=0;strand<2;strand++) {
            using(GraphicsPath p=new GraphicsPath()) {
                for(int k=0;k<=120;k++) {
                    double a=k*Math.PI/60;
                    float xx=x+w*.5f+(float)Math.Cos(a)*w*.43f;
                    float yy=y+h*.5f+(float)Math.Sin(a)*h*.23f+(float)Math.Sin(a*12+strand*Math.PI)*h*.065f;
                    if(k==0)p.StartFigure();else p.AddLine(p.GetLastPoint(),new PointF(xx,yy));
                    // First point must exist before GetLastPoint is used.
                    if(k==0)p.AddLine(xx,yy,xx,yy);
                }
                using(Pen pen=new Pen(Ink,h*.14f)){pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);}
                using(Pen pen=new Pen(C(strand==0?"6E241C":"9E4438"),h*.075f))g.DrawPath(pen,p);
            }
        }
        for(int k=0;k<18;k++) {
            double a=k*Math.PI/9;float cx=(float)Math.Cos(a),sy=(float)Math.Sin(a);
            float xx=x+w*.5f+cx*w*.43f,yy=y+h*.5f+sy*h*.23f;
            using(GraphicsPath p=new GraphicsPath()) {
                p.AddPolygon(new PointF[]{new PointF(xx-3,yy+2),new PointF(xx+cx*w*.055f-3,yy+sy*h*.26f-4),new PointF(xx+4,yy-2)});
                using(Brush b=new SolidBrush(C("6B231B")))g.FillPath(b,p);
                using(Pen pen=new Pen(Ink,1.6f))g.DrawPath(pen,p);
                using(Pen pen=new Pen(C("9E4438"),.8f))g.DrawLine(pen,xx,yy,xx+cx*w*.038f-2,yy+sy*h*.17f-2);
            }
        }
        if(rear)Stroke(g,"M "+(x+w*.15f).ToString(CultureInfo.InvariantCulture)+" "+(y+h*.55f).ToString(CultureInfo.InvariantCulture)+" C "+(x+w*.3f).ToString(CultureInfo.InvariantCulture)+" "+(y+h*.85f).ToString(CultureInfo.InvariantCulture)+" "+(x+w*.7f).ToString(CultureInfo.InvariantCulture)+" "+(y+h*.85f).ToString(CultureInfo.InvariantCulture)+" "+(x+w*.85f).ToString(CultureInfo.InvariantCulture)+" "+(y+h*.55f).ToString(CultureInfo.InvariantCulture),"A65343",1.3f);
    }
    static void Cross(Graphics g,float x,float y,float scale,string light,string dark) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        Paint(g,"M -9 -48 L 9 -48 L 9 -19 L 32 -19 L 32 -2 L 9 -2 L 9 49 L -9 49 L -9 -2 L -32 -2 L -32 -19 L -9 -19 Z",light,dark,3);
        Stroke(g,"M -4 -41 L -4 -12 L -25 -12 M 3 4 L 3 40","FFF7DE",1.8f);g.Restore(st);
    }
    static void Coat(Graphics g,string facing) {
        Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z","C8C4BC","8F8B85",0);
        for(int k=0;k<9;k++) {
            int x=k*39-6;
            Stroke(g,"M "+x+" 80 C "+(x-20)+" 177 "+(x+16)+" 265 "+(x-8)+" 370","77736F",5);
            Stroke(g,"M "+(x+8)+" 98 C "+(x-5)+" 196 "+(x+24)+" 291 "+(x+4)+" 357","DBD7CF",2.5f);
        }
        // Layered armored shoulder pieces and breastplate, with narrow garment seams.
        Paint(g,"M -4 21 L 54 5 L 94 36 L 77 91 L 17 106 L -8 77 Z","681F23","471115",4);
        Paint(g,"M 246 5 L 304 21 L 309 77 L 283 106 L 223 91 L 206 36 Z","773039","471115",4);
        Stroke(g,"M 4 43 L 48 25 L 76 45 M 226 45 L 252 25 L 295 44","B36A66",2.4f);
        Paint(g,"M 79 58 L 113 31 L 187 31 L 222 58 L 212 164 L 150 192 L 88 164 Z","681F23","471115",4);
        Paint(g,"M 94 79 L 150 100 L 206 79 L 200 150 L 150 173 L 100 150 Z","793239","52191E",2.3f);
        // Dark leather harnesses remain legible against the light textile.
        Stroke(g,"M 51 75 L 93 205 L 104 316 M 249 75 L 207 205 L 196 316","231B1D",12);
        Stroke(g,"M 51 75 L 93 205 L 104 316 M 249 75 L 207 205 L 196 316","68504A",5);
        Paint(g,"M 7 310 C 80 329 225 329 294 310 L 307 355 C 231 380 72 379 -7 354 Z","681F23","471115",4);
        Stroke(g,"M 6 342 C 94 365 213 365 296 342","AF7067",2.5f);
        Paint(g,"M 30 226 C 97 241 201 241 274 226 L 276 246 C 204 261 99 261 26 246 Z","3A2B2B","20181B",3);
        Paint(g,"M 135 232 L 166 232 L 166 255 L 135 255 Z","B9AF94","746B5B",2);
        Paint(g,"M 141 237 L 160 237 L 160 249 L 141 249 Z","37282A","21191D",1);
        if(facing=="south") {
            // Filled clerical collar: no circular opening and no transparent neck pixels.
            Paint(g,"M 111 -12 L 189 -12 L 193 32 L 169 64 L 131 64 L 107 32 Z","39414A","1D242D",3);
            Paint(g,"M 120 3 L 146 16 L 135 39 L 111 26 Z","59616B","222831",2);
            Paint(g,"M 180 3 L 154 16 L 165 39 L 189 26 Z","535B66","222831",2);
            Paint(g,"M 143 13 L 157 13 L 161 29 L 151 40 L 139 29 Z","FFFFFF","E9E7E0",2);
            Paint(g,"M 146 37 L 155 37 L 164 81 L 150 96 L 137 81 Z","FFFFFF","D8D8D3",2);
            Cross(g,150,140,.52f,"EFE6D6","AC2633");
            Briar(g,119,127,62,31,false);
        } else if(facing=="north") {
            Paint(g,"M 112 -10 L 188 -10 L 181 34 L 119 34 Z","3D444C","1D242D",3);
            Paint(g,"M 136 43 L 164 43 L 172 201 L 150 221 L 128 201 Z","4C171D","271016",3);
            Cross(g,150,117,1.05f,"BAADB0","651E28");
            Briar(g,117,91,66,34,true);
            Stroke(g,"M 150 181 L 150 307","5D3436",4);
        } else {
            Paint(g,"M 174 -9 L 244 -9 L 254 34 L 215 59 L 181 31 Z","454C55","222831",3);
            Paint(g,"M 231 18 L 242 24 L 231 74 L 216 85 L 219 65 Z","FFFFFF","D8D8D3",2);
            Paint(g,"M 106 58 L 157 31 L 207 65 L 185 154 L 139 179 L 99 132 Z","773039","471115",4);
            for(int k=0;k<3;k++)Stroke(g,"M "+(160+k*22)+" 172 C "+(175+k*22)+" 229 "+(157+k*22)+" 281 "+(179+k*22)+" 324",k==0?"504B49":"E2DED4",3);
            Cross(g,154,109,.45f,"C6BDB3","94313A");
        }
        // Rivets use explicit metallic shadows and small specular points.
        foreach(Point pt in new Point[]{new Point(28,52),new Point(64,65),new Point(236,65),new Point(276,52),new Point(97,98),new Point(203,98),new Point(30,337),new Point(72,348),new Point(226,348),new Point(272,337)}) {
            Ellipse(g,pt.X-3,pt.Y-3,6,6,"E2D8BC","716B64",1.1f);
        }
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
                Stroke(g,"M 142 126 L 153 205 L 146 273 M 370 126 L 359 205 L 365 273","4E3839",3.2f);
                Stroke(g,"M 124 259 L 143 264 M 369 264 L 398 259","D4C9BC",3);
            }
            return Resolve(hi,512);
        }
    }
    static void Halo(Graphics g,float x,float y,float radius,int strength) {
        using(GraphicsPath p=new GraphicsPath()) {
            p.AddEllipse(x-radius,y-radius,radius*2,radius*2);
            using(PathGradientBrush b=new PathGradientBrush(p)){b.CenterPoint=new PointF(x,y);b.CenterColor=Color.FromArgb(strength,244,222,154);b.SurroundColors=new Color[]{Color.FromArgb(0,244,222,154)};g.FillPath(b,p);}
        }
    }
    static Bitmap Weapon() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);Halo(g,365,240,107,48);
                // Grip left, heavy cruciform mace head right: preserve the original horizontal pose.
                Paint(g,"M 55 232 L 273 232 L 286 254 L 273 269 L 55 269 Z","7A8294","282E40",3.8f);
                Paint(g,"M 65 235 L 222 235 L 222 266 L 65 266 Z","4A3433","201B23",3);
                for(int k=0;k<15;k++) {
                    int x=69+k*10;Stroke(g,"M "+x+" 237 L "+(x+7)+" 264","80625A",2);Stroke(g,"M "+(x+3)+" 237 L "+(x+10)+" 264","2A2026",1.4f);
                }
                Paint(g,"M 54 229 L 67 229 L 67 272 L 54 272 L 47 261 L 47 240 Z","A7ACB6","42495E",3);
                Paint(g,"M 225 222 L 243 222 L 243 278 L 225 278 Z","9EA4AD","343B4C",3);
                Stroke(g,"M 248 241 L 294 241","CDD0D0",2);
                Paint(g,"M 330 147 L 392 147 L 392 204 L 454 204 L 454 277 L 392 277 L 392 332 L 330 332 L 330 277 L 271 277 L 271 204 L 330 204 Z","7A8294","1D2338",4.2f);
                Paint(g,"M 340 158 L 382 158 L 382 214 L 443 214 L 443 265 L 382 265 L 382 320 L 340 320 L 340 265 L 282 265 L 282 214 L 340 214 Z","313A51","1D2338",2.2f);
                Stroke(g,"M 333 151 L 333 208 L 275 208 L 275 271 M 396 208 L 449 208 L 449 268 M 334 280 L 334 327","BCC1CA",2.4f);
                Stroke(g,"M 345 163 L 378 163 M 398 217 L 438 217 M 345 313 L 377 313","7A8294",1.4f);
                for(int k=0;k<6;k++)Stroke(g,"M 345 "+(169+k*4)+" L 375 "+(166+k*4),"475168",.8f);
                Paint(g,"M 327 248 C 314 230 316 209 331 197 C 345 185 376 185 391 199 C 406 213 404 236 393 248 L 389 268 L 378 274 L 377 288 L 346 288 L 343 274 L 331 268 Z","EDE6DA","AFA596",3.8f);
                Paint(g,"M 323 226 C 327 203 345 195 359 196 C 339 211 343 228 339 239 L 330 253 Z","FFF5E5","D5CBBB",0);
                Paint(g,"M 379 201 C 400 210 400 230 389 249 L 382 261 L 371 251 L 379 233 Z","C3B8A7","9F9385",0);
                Paint(g,"M 331 222 C 337 216 349 218 353 226 L 349 240 L 338 243 L 330 236 Z","252637","090C15",2.3f);
                Paint(g,"M 368 226 C 376 218 386 219 391 225 L 391 237 L 379 244 L 370 239 Z","252637","090C15",2.3f);
                Paint(g,"M 360 239 L 352 257 L 365 260 L 369 255 Z","3A3034","15131E",1.6f);
                Stroke(g,"M 335 255 L 344 251 M 382 252 L 390 257 M 345 269 C 354 273 369 273 379 268","71625A",2);
                for(int k=0;k<5;k++)Paint(g,"M "+(346+k*6)+" 271 L "+(351+k*6)+" 271 L "+(351+k*6)+" 283 L "+(346+k*6)+" 283 Z","F6EEDF","B3A591",1);
                Stroke(g,"M 357 196 L 353 205 L 359 210 L 355 216","9D9185",1.1f);
                Briar(g,309,182,105,54,false);Stroke(g,"M 339 206 L 341 216 M 382 204 L 380 216","6E241C",1.6f);
            }
            return Resolve(hi,512);
        }
    }
    static Rectangle Bounds(Bitmap b,int cutoff) {
        byte[] a=Read(b);int l=b.Width,t=b.Height,r=-1,bot=-1;
        for(int k=0;k<a.Length/4;k++)if(a[k*4+3]>cutoff){int x=k%b.Width,y=k/b.Width;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bot=Math.Max(bot,y);}
        return r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,bot+1);
    }
    static Bitmap Crown(bool item,string face) {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {g.ScaleTransform(S,S);Briar(g,12,12,280,100,face=="north");}
            {
                Rectangle bounds=Bounds(hi,0);Rectangle target=item?Rectangle.FromLTRB(118*S,200*S,392*S,301*S):Rectangle.FromLTRB(171*S,213*S,339*S,275*S);
                using(Bitmap b=Canvas(512*S)) {
                    using(Graphics g=G(b))using(ImageAttributes ia=new ImageAttributes()){ia.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(hi,target,bounds.X,bounds.Y,bounds.Width,bounds.Height,GraphicsUnit.Pixel,ia);}
                    return Resolve(b,512);
                }
            }
        }
    }
    static Bitmap ArmorIcon(Bitmap suit) {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);Rectangle r=Bounds(suit,0);g.DrawImage(suit,new Rectangle(22,16,84,96),r,GraphicsUnit.Pixel);}
            return Resolve(hi,128);
        }
    }
    static Bitmap WeaponIcon(Bitmap weapon) {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);Halo(g,83,40,36,75);g.TranslateTransform(64,65);g.RotateTransform(-39);g.ScaleTransform(.252f,.252f);g.TranslateTransform(-256,-240);g.DrawImageUnscaled(weapon,0,0);
            }
            return Resolve(hi,128);
        }
    }
    static Bitmap Ability() {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                Paint(g,"M 17 5 L 111 5 L 123 17 L 123 111 L 111 123 L 17 123 L 5 111 L 5 17 Z","39202B","100F1A",3);
                Stroke(g,"M 19 7 L 109 7 L 121 19 L 121 109 L 109 121 L 19 121 L 7 109 L 7 19 Z","B79B61",2);
                Stroke(g,"M 22 12 L 106 12 L 116 22 L 116 106 L 106 116 L 22 116 L 12 106 L 12 22 Z","8D2834",2);
                Halo(g,64,60,49,160);
                for(int k=0;k<16;k++) {
                    double a=k*Math.PI/8;float cx=(float)Math.Cos(a),sy=(float)Math.Sin(a);
                    using(Pen p=new Pen(Color.FromArgb(115,232,207,149),1.2f))g.DrawLine(p,64+cx*29,60+sy*29,64+cx*43,60+sy*43);
                }
                Cross(g,64,63,.83f,"FFF5DC","BD985F");Briar(g,31,48,66,37,false);
                Paint(g,"M 59 17 L 64 12 L 69 17 L 64 22 Z","E8CB8C","8D653E",1);
                Paint(g,"M 59 111 L 64 106 L 69 111 L 64 116 Z","C3313D","671722",1);
            }
            return Resolve(hi,128);
        }
    }
    static void Verify() {
        if(files.Count!=24)throw new Exception("Expected 24 outputs, got "+files.Count);
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])) {
            string name=Path.GetFileNameWithoutExtension(files[j]);
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid format: "+name);
            byte[] a=Read(b);int opaque=0,visible=0;
            for(int k=0;k<a.Length/4;k++){if(a[k*4+3]==255)opaque++;if(a[k*4+3]>0)visible++;else if(a[k*4]!=0||a[k*4+1]!=0||a[k*4+2]!=0)throw new Exception("Hidden RGB: "+name);}
            Rectangle box=Bounds(b,0);if(opaque==0||visible==b.Width*b.Height)throw new Exception("Invalid alpha: "+name);
            if(name.StartsWith("OneSinSuit_")) {
                string key=name.Substring("OneSinSuit_".Length).Replace("North","north");BodyMask m=masks[key];
                for(int k=0;k<m.Inside.Length;k++) {
                    if(m.Inside[k]&&a[k*4+3]<128)throw new Exception("Unsealed interior: "+name);
                    if(!m.Inside[k]&&m.Original[k*4+3]==0&&a[k*4+3]!=0)throw new Exception("Mask leak: "+name);
                    if(m.Inside[k]&&m.Distance[k]<=1.42f&&(Math.Abs(a[k*4]-Ink.B)>1||Math.Abs(a[k*4+1]-Ink.G)>1||Math.Abs(a[k*4+2]-Ink.R)>1))throw new Exception("Missing inward ink: "+name);
                }
                using(Bitmap original=new Bitmap(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+key+".png")))if(box!=Bounds(original,0))throw new Exception("Mask bbox mismatch: "+name);
            }
            if(name.StartsWith("OneSinAccessory")) {
                Rectangle target=name=="OneSinAccessory"?Rectangle.FromLTRB(118,200,392,301):Rectangle.FromLTRB(171,213,339,275);
                if(Math.Abs(box.Left-target.Left)>2||Math.Abs(box.Top-target.Top)>2||Math.Abs(box.Right-target.Right)>2||Math.Abs(box.Bottom-target.Bottom)>2)throw new Exception("Head coordinate bbox: "+name+box);
            }
            if(name=="EgoWeapon_OneSin") {
                Rectangle solid=Bounds(b,128);if(solid.Width<390||solid.Height>215||solid.Left<43||solid.Right>460)throw new Exception("Horizontal weapon bbox: "+solid);
            }
            if(name=="ExtractEGO_OneSin_Armor"&&(box.Left<20||box.Right>108||box.Top<14||box.Bottom>114))throw new Exception("Armor UI bbox");
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3}, visible={4}, bbox=({5},{6})-({7},{8})",name,b.Width,b.Height,opaque,visible,box.Left,box.Top,box.Right-1,box.Bottom-1);
        }
        string dir=Path.Combine(root,"Cont/Textures/Things/Pawn/Humanlike/Apparel/OneSinSuit");
        bool exact=false;foreach(string f in Directory.GetFiles(dir))if(Path.GetFileName(f)=="OneSinSuit_Fat_North.png")exact=true;
        if(!exact)throw new Exception("Missing case-sensitive Fat_North filename");
        Console.WriteLine("SUCCESS: all 24 PNGs passed format, alpha, sealed-mask, inward-ink and coordinate checks.");
    }
    static void Review() {
        string[] rel={"Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit_Female_south.png","Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit_Female_north.png","Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit_Female_east.png","Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit_Male_south.png","Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit_Hulk_south.png","Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit_Fat_south.png","Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit_Thin_south.png","Things/Item/Equipment/WeaponMelee/EgoWeapon_OneSin.png","Things/Pawn/Humanlike/Apparel/Accessory/OneSinAccessory.png","UI/Commands/ExtractEGO_OneSin_Armor.png","UI/Commands/ExtractEGO_OneSin_Weapon.png","UI/WeaponSkills/OneSin/Penitence_Absolution.png"};
        string[] labels={"FEMALE / SOUTH","FEMALE / NORTH","FEMALE / EAST","MALE / SOUTH","HULK / SOUTH","FAT / SOUTH","THIN / SOUTH","PENITENCE / CROSS MACE","CROWN OF THORNS","EXTRACT / ARMOR - 128px","EXTRACT / WEAPON - 128px","ABSOLUTION - 128px"};
        using(Bitmap sheet=new Bitmap(1600,1310,PixelFormat.Format32bppArgb))using(Graphics g=G(sheet))using(Font title=new Font(FontFamily.GenericSansSerif,23,FontStyle.Bold))using(Font label=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold))using(Brush white=new SolidBrush(C("EEE6D5"))) {
            g.Clear(C("171A24"));g.DrawString("ONE SIN  /  PENITENCE",title,white,26,16);g.DrawString("3x procedural GDI+ | sealed vanilla body masks | ivory, mahogany & iron",label,white,28,58);
            for(int j=0;j<12;j++) {
                int x=(j%4)*400,y=100+(j/4)*400;
                using(Brush bg=new SolidBrush(C("303440")))g.FillRectangle(bg,x+8,y,384,390);
                for(int yy=0;yy<16;yy++)for(int xx=0;xx<16;xx++)if((xx+yy)%2==0)using(Brush br=new SolidBrush(C("363B47")))g.FillRectangle(br,x+16+xx*23,y+29+yy*22,23,22);
                g.DrawString(labels[j],label,white,x+20,y+7);
                using(Bitmap b=new Bitmap(Path.Combine(root,"Cont/Textures",rel[j]))) {
                    Rectangle src=Bounds(b,0);Rectangle dst;
                    if(j<7) {src=Rectangle.Inflate(src,16,16);float scale=Math.Min(340f/src.Width,340f/src.Height);int w=(int)(src.Width*scale),h=(int)(src.Height*scale);dst=new Rectangle(x+200-w/2,y+205-h/2,w,h);}
                    else if(j==8)dst=new Rectangle(x+35,y+141,330,122);
                    else if(j>=9){src=new Rectangle(0,0,128,128);dst=new Rectangle(x+72,y+77,256,256);}
                    else {src=Rectangle.Inflate(src,12,12);dst=new Rectangle(x+20,y+90,360,210);}
                    g.DrawImage(b,dst,src,GraphicsUnit.Pixel);
                }
            }
            string output=Path.Combine(Path.GetTempPath(),"Vanilla-OneSin-review.png");sheet.Save(output,ImageFormat.Png);Console.WriteLine("REVIEW "+output);
        }
    }
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();masks.Clear();
        using(Bitmap weapon=Weapon()) {
            Save(weapon,"Things/Item/Equipment/WeaponMelee/EgoWeapon_OneSin.png",512);
            using(Bitmap icon=WeaponIcon(weapon))Save(icon,"UI/Commands/ExtractEGO_OneSin_Weapon.png",128);
        }
        foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})
            using(Bitmap b=Worn(body,face))Save(b,"Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit_"+body+"_"+(body=="Fat"&&face=="north"?"North":face)+".png",512);
        using(Bitmap suit=SuitItem()) {
            Save(suit,"Things/Pawn/Humanlike/Apparel/OneSinSuit/OneSinSuit.png",512);
            using(Bitmap icon=ArmorIcon(suit))Save(icon,"UI/Commands/ExtractEGO_OneSin_Armor.png",128);
        }
        foreach(string face in new string[]{"item","south","north","east"})using(Bitmap b=Crown(face=="item",face))Save(b,"Things/Pawn/Humanlike/Apparel/Accessory/OneSinAccessory"+(face=="item"?"":"_"+face)+".png",512);
        using(Bitmap icon=Ability())Save(icon,"UI/WeaponSkills/OneSin/Penitence_Absolution.png",128);
        Verify();Review();
    }
}
'@
if (-not ('OneSinArt' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[OneSinArt]::Run($ModRoot)
