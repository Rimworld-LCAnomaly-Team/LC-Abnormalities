#requires -Version 5.1
# Group D deterministic GDI+ painter. Exact OneSin BodyMask sealing and 3x resolve.
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

public static class GroupDArt {
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

    static int set;
    static readonly string[] names={"AllAroundHelper","JudgementBird","BigBird"};
    static readonly string[] skills={"GrinderMk4_OverdriveRush","Justitia_PaleJudgement","Lamp_LanternSalvation"};
    static string N {get{return names[set];}}
    static void Line(Graphics g,float x,float y,float xx,float yy,string c,float w) {
        using(Pen p=new Pen(C(c),w)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,x,y,xx,yy);}
    }
    static void Poly(Graphics g,PointF[] pts,string a,string b,float w) {
        using(GraphicsPath p=new GraphicsPath()){p.AddPolygon(pts);RectangleF r=p.GetBounds();if(r.Height<1)r.Height=1;if(r.Width<1)r.Width=1;
        using(LinearGradientBrush br=new LinearGradientBrush(r,C(a),C(b),55))g.FillPath(br,p);
        if(w>0)using(Pen pen=new Pen(Ink,w)){pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);}}
    }
    static void Glow(Graphics g,float x,float y,float r,string color,int alpha) {
        using(GraphicsPath p=new GraphicsPath()){p.AddEllipse(x-r,y-r,r*2,r*2);using(PathGradientBrush br=new PathGradientBrush(p)){
        Color c=C(color);br.CenterPoint=new PointF(x,y);br.CenterColor=Color.FromArgb(alpha,c);br.SurroundColors=new Color[]{Color.FromArgb(0,c)};g.FillPath(br,p);}}
    }
    static void WatchingEye(Graphics g,float x,float y,float w,float angle) {
        GraphicsState s=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(w/60,w/60);
        Glow(g,0,0,48,"F6B52F",85);
        Paint(g,"M -31 0 C -14 -21 15 -21 31 0 C 14 22 -14 22 -31 0 Z","FFE69A","A56719",2.8f);
        Stroke(g,"M -23 -2 C -9 -13 12 -13 23 -2","FFF5C5",1.5f);
        Ellipse(g,-9,-13,18,26,"9D5C13","3C220F",1.6f);
        Ellipse(g,-4,-11,8,22,"100D0E","09080B",0);
        Ellipse(g,-6,-9,5,6,"FFFBE0","FFF0BA",0);g.Restore(s);
    }
    static void ScaleEmblem(Graphics g,float x,float y,float scale) {
        GraphicsState s=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        Stroke(g,"M 0 -39 L 0 35 M -34 -24 L 34 -24 M -16 36 L 16 36","140A0C",8);
        Stroke(g,"M 0 -39 L 0 35 M -34 -24 L 34 -24 M -16 36 L 16 36","C9A35B",4);
        for(int k=-1;k<=1;k+=2){float xx=k*29;Line(g,xx,-23,xx-15,9,"E8C67D",1.8f);Line(g,xx,-23,xx+15,9,"E8C67D",1.8f);
            Poly(g,new PointF[]{new PointF(xx-17,9),new PointF(xx+17,9),new PointF(xx+10,20),new PointF(xx-10,20)},"EDD496","95713B",2);}
        Paint(g,"M 0 -47 L 7 -39 L 0 -31 L -7 -39 Z","FFF0B4","AE8240",2);g.Restore(s);
    }
    static void Feather(Graphics g,float x,float y,float w,float h,float tilt) {
        GraphicsState s=g.Save();g.TranslateTransform(x,y);g.RotateTransform(tilt);g.ScaleTransform(w/40,h/90);
        Paint(g,"M 0 -12 C 26 -5 24 30 0 80 C -19 43 -28 4 0 -12 Z","333843","0C0D14",2.5f);
        Stroke(g,"M 0 1 L 0 66 M 0 29 L -10 19 M 0 40 L 11 27 M 0 51 L -7 40","52515A",1.1f);g.Restore(s);
    }
    static void Hazard(Graphics g,float x,float y,float w) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);
        Paint(g,"M 0 0 L "+w+" 0 L "+w+" 15 L 0 15 Z","E8C247","AA7E22",2);
        g.SetClip(new RectangleF(1,1,w-2,13));for(int k=-1;k<w/15+1;k++){
            float xx=k*15;Poly(g,new PointF[]{new PointF(xx,0),new PointF(xx+7,0),new PointF(xx+19,15),new PointF(xx+12,15)},"22262C","161820",0);}
        g.Restore(st);
    }
    static void Coat(Graphics g,string facing) {
        bool front=facing=="south",back=facing=="north",side=facing=="east";
        if(set==0) {
            Paint(g,"M -30 -30 L 330 -30 L 330 400 L -30 400 Z","F7FAF6","A2ADB0",0);
            // Recessed mechanical armatures beneath segmented ceramic panels.
            Paint(g,"M 45 -10 L 83 19 L 102 110 L 77 260 L 18 292 L -10 250 Z","444D55","171D24",3);
            Paint(g,"M 255 -10 L 217 19 L 198 110 L 223 260 L 282 292 L 310 250 Z","444D55","171D24",3);
            for(int i=0;i<6;i++){Line(g,27,101+i*23,65,112+i*23,"778289",3);Line(g,235,112+i*23,273,101+i*23,"778289",3);}
            Paint(g,"M -5 25 L 51 6 L 90 39 L 79 91 L 12 106 L -8 70 Z","FFFFFF","BAC6C8",4);
            Paint(g,"M 249 6 L 305 25 L 308 70 L 288 106 L 221 91 L 210 39 Z","FFFFFF","BAC6C8",4);
            Paint(g,"M 87 46 L 150 30 L 213 46 L 213 177 L 190 214 L 110 214 L 87 177 Z","FFFFFF","CDD5D3",4);
            Stroke(g,"M 101 58 L 101 171 L 119 194 M 199 58 L 199 171 L 181 194","FFFFFF",3);
            Stroke(g,"M 150 184 L 150 337 M 85 245 L 58 346 M 215 245 L 242 346","64747B",3);
            Paint(g,"M 27 228 L 273 228 L 278 252 L 22 252 Z","37434A","1B222B",3);
            Paint(g,"M 126 228 L 174 228 L 174 252 L 126 252 Z","C9D0CD","646F74",2);
            for(int i=0;i<4;i++)Ellipse(g,36+i*74,37,6,6,"F4F5DB","4F5D65",1);
            Paint(g,"M 111 -14 L 189 -14 L 194 28 L 176 47 L 124 47 L 106 28 Z","5B6971","18212A",3);
            if(front){
                Paint(g,"M 103 92 L 197 92 L 200 116 L 100 116 Z","323942","11151C",3);
                Glow(g,150,104,38,"FF2947",105);
                Stroke(g,"M 111 102 L 189 102","A50827",10);Stroke(g,"M 111 101 L 189 101","FF2D4D",5);Stroke(g,"M 118 99 L 180 99","FFE4CE",1.5f);
                for(int i=0;i<3;i++)Line(g,116+i*7,143,116+i*7,155,"A51F36",2);
                Hazard(g,27,65,41);Hazard(g,232,65,41);
            } else {
                Paint(g,"M 124 52 L 176 52 L 181 204 L 150 218 L 119 204 Z","56616A","242C35",3);
                for(int i=0;i<9;i++)Line(g,132,67+i*14,168,67+i*14,"0F151F",5);
                Stroke(g,"M 150 60 L 150 193","E33B42",2);
                Hazard(g,91,225,118);if(side)Hazard(g,226,79,50);
            }
            Paint(g,"M -7 321 L 81 332 L 101 360 L -10 366 Z","F8FAF5","A5B2B5",3);
            Paint(g,"M 219 332 L 307 321 L 310 366 L 199 360 Z","F8FAF5","A5B2B5",3);
            Stroke(g,"M 13 337 L 72 344 M 228 344 L 287 337","B21C31",7);
        } else if(set==1) {
            Paint(g,"M -30 -30 L 330 -30 L 330 400 L -30 400 Z","323440","090A12",0);
            for(int i=0;i<7;i++){
                float x=25+i*42;Line(g,x,145,x-13,360,"494750",2);Line(g,x+6,220,x+16,360,"14131D",5);}
            Paint(g,"M 48 7 L 102 31 L 115 112 L 67 145 L -7 80 Z","484650","17151F",4);
            Paint(g,"M 252 7 L 198 31 L 185 112 L 233 145 L 307 80 Z","484650","17151F",4);
            // Broad opaque overlapping off-white linen, with narrow folded hems.
            for(int i=0;i<5;i++){
                float y=28+i*25;
                Poly(g,new PointF[]{new PointF(-8,y),new PointF(92,y-19),new PointF(309,y+60),new PointF(309,y+82),new PointF(88,y+4),new PointF(-8,y+22)},"F6F0E7","C9C1B5",2.5f);
                Line(g,98,y-12,278,y+54,"FFFCF1",1.8f);
            }
            for(int i=0;i<4;i++){
                float y=68+i*27;
                Poly(g,new PointF[]{new PointF(304,y-26),new PointF(209,y-43),new PointF(64,y+14),new PointF(61,y+37),new PointF(210,y-20),new PointF(304,y-4)},"EAE4DA","C7BFAF",2);
                Line(g,214,y-36,285,y-22,"FFFAEC",1.3f);
            }
            Paint(g,"M 107 -15 L 146 -2 L 150 52 L 113 32 Z","44424A","11101B",3);
            Paint(g,"M 193 -15 L 154 -2 L 150 52 L 187 32 Z","44424A","11101B",3);
            Stroke(g,"M 111 -7 L 142 4 M 189 -7 L 158 4","CBA45A",2);
            Stroke(g,"M 72 185 L 219 273","140A0C",22);Stroke(g,"M 72 185 L 219 273","99783D",15);Stroke(g,"M 72 182 L 219 270","E2C57E",2);
            ScaleEmblem(g,side?190:150,back?212:218,side?.48f:.67f);
            Stroke(g,"M 15 347 C 90 369 215 369 288 347","9B7F4B",5);
            Stroke(g,"M 15 341 C 90 363 215 363 288 341","E1C88E",1.5f);
        } else {
            Paint(g,"M -30 -30 L 330 -30 L 330 400 L -30 400 Z","292C36","080A12",0);
            for(int row=0;row<4;row++)for(int i=0;i<7;i++)Feather(g,i*49+(row%2)*24-12,65+row*78,53,110,(i-3)*3);
            // Mantle layers fan away from the high neck.
            for(int i=0;i<5;i++) {Feather(g,55+i*7,16+i*13,54,110,30+i*5);Feather(g,245-i*7,16+i*13,54,110,-30-i*5);}
            Paint(g,"M 105 -16 L 143 -1 L 150 52 L 111 27 Z","40404A","0B0D14",3);
            Paint(g,"M 195 -16 L 157 -1 L 150 52 L 189 27 Z","40404A","0B0D14",3);
            Stroke(g,"M 147 56 L 147 335","957441",3);Stroke(g,"M 154 56 L 154 335","1A1720",3);
            for(int i=0;i<6;i++)Ellipse(g,143,108+i*39,8,9,"E0BB6B","685127",1.4f);
            if(side){WatchingEye(g,192,79,45,-12);WatchingEye(g,115,176,44,14);WatchingEye(g,201,270,42,10);WatchingEye(g,36,116,31,-22);}
            else {WatchingEye(g,65,78,44,-15);WatchingEye(g,235,78,44,15);WatchingEye(g,104,170,48,8);WatchingEye(g,213,218,43,-15);WatchingEye(g,63,286,44,-12);WatchingEye(g,167,316,40,12);if(back)WatchingEye(g,152,77,57,0);}
            Stroke(g,"M 17 350 C 88 369 212 369 285 350","7A6138",3);
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
    static Rectangle Bounds(Bitmap b,int cutoff) {
        byte[] a=Read(b);int l=b.Width,t=b.Height,r=-1,bot=-1;
        for(int k=0;k<a.Length/4;k++)if(a[k*4+3]>cutoff){int x=k%b.Width,y=k/b.Width;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bot=Math.Max(bot,y);}
        return r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,bot+1);
    }

    static void Saw(Graphics g,float x,float y,float r) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);
        PointF[] teeth=new PointF[72];
        for(int i=0;i<72;i++){double a=i*Math.PI/36;float rr=i%3==0?r:r*(i%3==1?.84f:.87f);teeth[i]=new PointF((float)Math.Cos(a)*rr,(float)Math.Sin(a)*rr);}
        Poly(g,teeth,"9CAEB6","26303B",3);
        Ellipse(g,-r*.79f,-r*.79f,r*1.58f,r*1.58f,"FBFFFA","AEBFC2",3);
        Ellipse(g,-r*.57f,-r*.57f,r*1.14f,r*1.14f,"EAF0EB","7F939B",2);
        for(int k=0;k<6;k++) {double a=k*Math.PI/3;float cx=(float)Math.Cos(a),sy=(float)Math.Sin(a);
            Line(g,cx*r*.24f,sy*r*.24f,cx*r*.48f,sy*r*.48f,"394953",r*.09f);
            Ellipse(g,cx*r*.66f-3,sy*r*.66f-3,6,6,"F8FAE6","4C555C",1);}
        Ellipse(g,-r*.18f,-r*.18f,r*.36f,r*.36f,"E5ECE7","33444F",2);
        Ellipse(g,-r*.07f,-r*.07f,r*.14f,r*.14f,"FF4255","8F1026",1);
        Stroke(g,"M -67 -42 C -40 -30 -52 3 -34 16 M 13 -68 L 24 -47 L 19 -20 M 31 38 L 51 58 L 57 43","A01930",5);
        Stroke(g,"M -64 -40 L -56 -28 M 30 42 L 44 52","EF4D51",1.4f);g.Restore(st);
    }
    static void Lantern(Graphics g,float x,float y,float scale) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        Glow(g,0,0,91,"FFC24E",130);
        Paint(g,"M -39 -50 L 39 -50 L 48 41 L 26 62 L -26 62 L -48 41 Z","D0A85C","6E4F2E",4);
        Paint(g,"M -29 -42 L 29 -42 L 35 35 L 18 47 L -18 47 L -35 35 Z","57382A","151623",3);
        Glow(g,0,7,43,"FFD350",250);
        Paint(g,"M 0 39 C -39 32 -22 8 -15 -2 C -16 14 5 10 -3 -31 C 29 -13 29 9 19 21 C 29 18 30 12 30 9 C 43 38 17 46 0 39 Z","FFF5AA","E79523",2);
        Paint(g,"M 0 33 C -14 21 5 15 6 -2 C 22 19 19 35 0 33 Z","FFFFFF","FFE984",0);
        for(int i=-1;i<=1;i++){float xx=i*25;Line(g,xx,-42,xx*1.22f,37,"251D1C",7);Line(g,xx-1,-41,xx*1.22f-1,37,"E0BE73",2);}
        Paint(g,"M -48 -51 L -27 -66 L -12 -71 L 12 -71 L 27 -66 L 48 -51 L 41 -43 L -41 -43 Z","EBD496","856030",3);
        Paint(g,"M -45 40 L 45 40 L 31 61 L -31 61 Z","DCC081","79542D",3);
        Stroke(g,"M -16 -71 C -29 -101 30 -101 16 -71","140A0C",7);Stroke(g,"M -16 -71 C -29 -101 30 -101 16 -71","C9A465",3);
        Stroke(g,"M -22 -60 L 22 -60 M -28 49 L 28 49","FFF0B8",2);
        g.Restore(st);
    }
    static Bitmap Fit(Bitmap hi,Rectangle target,int size) {
        Rectangle b=Bounds(hi,0);using(Bitmap temp=Canvas(size*S)){
            using(Graphics g=G(temp))using(ImageAttributes ia=new ImageAttributes()){ia.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(hi,new Rectangle(target.X*S,target.Y*S,target.Width*S,target.Height*S),b.X,b.Y,b.Width,b.Height,GraphicsUnit.Pixel,ia);}
            return Resolve(temp,size);}
    }
    static Rectangle WeaponTarget {get{return set==0?Rectangle.FromLTRB(46,16,502,468):set==1?Rectangle.FromLTRB(61,43,469,450):Rectangle.FromLTRB(28,146,476,350);}}
    static Bitmap Weapon() {
        using(Bitmap hi=Canvas(512*S)){
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);
                if(set==0){
                    // Hilt bottom left, toothed working head at top right.
                    Paint(g,"M 64 424 L 231 247 L 269 267 L 93 452 L 58 450 Z","6C7B83","222C37",4);
                    for(int k=0;k<11;k++)Line(g,75+k*11,418-k*12,93+k*11,434-k*12,"111720",5);
                    Paint(g,"M 145 299 L 222 198 L 319 154 L 371 207 L 299 302 L 210 367 Z","FCFFF9","BAC6C7",4);
                    Stroke(g,"M 159 302 L 211 324 L 294 239","D4DCD8",3);
                    Paint(g,"M 203 236 L 224 216 L 286 277 L 268 299 Z","EF4150","9E0F2A",3);
                    Hazard(g,193,292,50);
                    Saw(g,355,144,128);
                    Paint(g,"M 257 198 L 281 177 L 345 206 L 317 235 Z","FFFFFF","C7D2D1",4);
                    Stroke(g,"M 266 199 L 300 209","FFFFFF",3);
                    Stroke(g,"M 164 296 L 183 310 L 172 327 M 217 353 L 229 330","A61F31",3);
                    Ellipse(g,54,431,30,28,"8D9DA3","303B47",3);
                } else if(set==1){
                    Paint(g,"M 88 397 L 186 292 L 211 315 L 113 422 Z","55515B","181522",4);
                    for(int i=0;i<10;i++){float x=100+i*9,y=390-i*10;Poly(g,new PointF[]{new PointF(x-8,y),new PointF(x+6,y-11),new PointF(x+26,y+7),new PointF(x+13,y+20)},"EAE4DA","B7B1A7",1.5f);}
                    Paint(g,"M 188 291 L 370 64 L 452 49 L 435 131 L 224 330 Z","535864","10121E",4);
                    Paint(g,"M 205 291 L 374 76 L 441 60 L 415 106 L 222 312 Z","1B1C29","070912",2);
                    Stroke(g,"M 211 312 L 430 128 L 449 56","C0E3ED",4);Stroke(g,"M 225 309 L 431 126","739CAA",1.7f);
                    for(int i=0;i<6;i++){float x=223+i*22,y=260-i*26;
                        Poly(g,new PointF[]{new PointF(x-15,y),new PointF(x-1,y-20),new PointF(x+52,y+1),new PointF(x+34,y+18)},"F4EEE4","BEB9AF",2);
                        Line(g,x-8,y-1,x+34,y+13,"FFFCF2",1);}
                    Paint(g,"M 129 274 L 152 263 L 269 366 L 262 390 L 240 368 L 143 290 Z","E3C37F","78542F",4);
                    ScaleEmblem(g,199,306,.72f);
                    Paint(g,"M 74 405 L 92 392 L 119 420 L 107 439 L 83 440 L 69 423 Z","DAB976","72512E",3);
                    Ellipse(g,85,410,15,17,"E6F6EC","6BA2AA",2);
                } else {
                    Paint(g,"M 37 239 L 326 239 L 353 253 L 326 270 L 37 270 L 27 256 Z","4A4C51","10141D",4);
                    Stroke(g,"M 43 243 L 320 243","829092",2);
                    for(int i=0;i<8;i++)Line(g,66+i*12,241,76+i*12,267,"6A5540",3);
                    Paint(g,"M 28 235 L 48 235 L 48 274 L 28 274 L 22 256 Z","AC8C52","4B3A27",3);
                    Paint(g,"M 282 232 L 301 232 L 301 276 L 282 276 Z","E3BD70","72522E",3);
                    Lantern(g,388,249,1.06f);
                    Stroke(g,"M 313 249 C 325 220 339 214 350 222","BE9C5C",6);
                }
            }return Fit(hi,WeaponTarget,512);
        }
    }
    static Rectangle AccessoryTarget(bool item,string face) {
        if(set==0)return item?Rectangle.FromLTRB(72,187,440,326):face=="east"?Rectangle.FromLTRB(252,223,299,236):Rectangle.FromLTRB(215,223,298,236);
        if(set==1)return item?Rectangle.FromLTRB(72,182,440,330):face=="east"?Rectangle.FromLTRB(252,222,300,236):Rectangle.FromLTRB(215,222,298,236);
        return item?Rectangle.FromLTRB(72,162,440,350):Rectangle.FromLTRB(200,162,313,212);
    }
    static Bitmap Accessory(bool item,string face) {
        if(set==0&&face=="north")return Canvas(512);
        using(Bitmap hi=Canvas(512*S)){
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);
                if(set==0){
                    Paint(g,"M 22 33 L 55 18 L 246 18 L 278 33 L 269 77 L 243 89 L 57 89 L 31 77 Z","5E707B","18232C",3);
                    Paint(g,"M 42 37 L 258 37 L 251 66 L 49 66 Z","580B21","110D19",3);
                    Glow(g,150,51,38,"FF284B",100);
                    Stroke(g,"M 57 49 L 244 49","C01131",13);Stroke(g,"M 61 47 L 240 47","FF4257",6);Stroke(g,"M 71 44 L 231 44","FFE1CD",1.5f);
                    for(int i=0;i<2;i++)Ellipse(g,30+i*231,41,10,18,"9CACAE","293B47",1.7f);
                    Stroke(g,"M 55 25 L 244 25","C3D3D5",2);
                }else if(set==1){
                    Paint(g,"M 20 25 C 80 7 224 8 280 25 L 272 83 C 194 99 94 98 29 81 Z","FAF4E9","CCC4B5",3);
                    for(int i=0;i<4;i++){float y=28+i*15;Stroke(g,"M 27 "+y+" C 101 "+(y+18)+" 200 "+(y-7)+" 274 "+(y+4),i%2==0?"B9AE9C":"FFFCF2",2);}
                    Stroke(g,"M 72 18 L 82 88 M 174 16 L 163 91 M 230 22 L 244 87","D1C6B6",1.7f);
                    if(face=="north"){
                        Paint(g,"M 130 42 L 166 61 L 127 85 L 113 64 Z","F5EEE2","B4A995",2);
                        Paint(g,"M 158 60 L 204 34 L 196 83 L 166 76 Z","EEE7D8","B4A995",2);
                        Paint(g,"M 145 54 L 168 53 L 174 77 L 148 81 Z","FFFAED","BCAF99",2);
                    }
                }else{
                    Paint(g,"M 20 67 C 54 22 233 22 282 64 L 269 91 C 193 59 105 57 32 94 Z","242731","080B13",3);
                    for(int i=0;i<11;i++)Feather(g,27+i*23,49,33,50,(i-5)*5);
                    if(face=="north")WatchingEye(g,150,56,46,0);
                    else {WatchingEye(g,78,55,49,-9);WatchingEye(g,150,46,64,0);WatchingEye(g,225,55,49,9);}
                }
            }return Fit(hi,AccessoryTarget(item,face),512);
        }
    }
    static void Frame(Graphics g) {
        Paint(g,"M 18 5 L 110 5 L 123 18 L 123 110 L 110 123 L 18 123 L 5 110 L 5 18 Z","34212B","0D101C",3);
        Stroke(g,"M 20 8 L 108 8 L 120 20 L 120 108 L 108 120 L 20 120 L 8 108 L 8 20 Z","C7A361",2);
        Stroke(g,"M 23 14 L 105 14 L 114 23 L 114 105 L 105 114 L 23 114 L 14 105 L 14 23 Z",set==0?"AC2A40":set==1?"728A94":"8C602C",1.5f);
        for(int i=0;i<4;i++){GraphicsState s=g.Save();g.TranslateTransform(64,64);g.RotateTransform(i*90);Paint(g,"M -4 -53 L 0 -57 L 4 -53 L 0 -49 Z","F6D592","A47843",1);g.Restore(s);}
    }
    static Bitmap Icon(Bitmap asset,bool armor) {
        using(Bitmap hi=Canvas(128*S)){
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);Frame(g);Glow(g,64,65,47,set==0?"F13D52":set==1?"A2E6EF":"F4C251",80);
                Rectangle src=Bounds(asset,0);float sc=Math.Min(92f/src.Width,91f/src.Height);int w=(int)(src.Width*sc),h=(int)(src.Height*sc);
                g.DrawImage(asset,new Rectangle(64-w/2,65-h/2,w,h),src,GraphicsUnit.Pixel);
                // Corner tier bars remain legible at native UI scale.
                for(int i=0;i<set+2;i++)Line(g,47+i*9,108,51+i*9,108,"E5C17B",2);
            }return Resolve(hi,128);
        }
    }
    static Bitmap Ability() {
        using(Bitmap hi=Canvas(128*S)){
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);Frame(g);
                if(set==0){
                    Glow(g,69,62,47,"FF3144",130);
                    Stroke(g,"M 21 80 C 28 37 67 24 99 38 M 26 94 C 38 110 88 107 104 79","F6B966",3);
                    Stroke(g,"M 20 65 L 39 52 M 23 90 L 42 77 M 85 30 L 104 22","EB4B4D",4);
                    GraphicsState s=g.Save();g.TranslateTransform(67,64);g.ScaleTransform(.31f,.31f);Saw(g,0,0,112);g.Restore(s);
                    Paint(g,"M 24 100 L 48 85 L 41 104 L 70 94 L 49 113 Z","FFE8B3","C73242",1);
                } else if(set==1){
                    Glow(g,65,63,47,"B3FAFF",140);
                    Stroke(g,"M 26 95 C 58 90 80 55 102 26","5B8EAB",17);
                    Stroke(g,"M 26 95 C 58 90 80 55 102 26","B6F2F4",8);
                    Stroke(g,"M 26 95 C 58 90 80 55 102 26","FFFFFF",2.8f);
                    ScaleEmblem(g,58,64,.72f);
                    Stroke(g,"M 34 25 L 29 40 M 97 82 L 104 91","E7F8F4",1.5f);
                } else {
                    Glow(g,64,64,49,"FBC354",180);Lantern(g,64,66,.43f);
                    WatchingEye(g,31,35,24,-25);WatchingEye(g,97,35,24,25);WatchingEye(g,29,91,24,25);WatchingEye(g,100,91,24,-25);
                    Stroke(g,"M 54 22 L 58 33 M 74 22 L 70 33 M 56 104 L 54 110 M 72 104 L 74 110","FFE5A1",2);
                }
            }return Resolve(hi,128);
        }
    }
    static void Verify() {
        if(files.Count!=72)throw new Exception("Expected 72 outputs, got "+files.Count);
        HashSet<string> unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])){
            if(!unique.Add(files[j]))throw new Exception("Duplicate output");
            string name=Path.GetFileNameWithoutExtension(files[j]);
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid format: "+name);
            byte[] a=Read(b);int opaque=0,visible=0;
            for(int k=0;k<a.Length/4;k++){if(a[k*4+3]==255)opaque++;if(a[k*4+3]>0)visible++;else if(a[k*4]!=0||a[k*4+1]!=0||a[k*4+2]!=0)throw new Exception("Hidden RGB: "+name);}
            bool empty=name=="AllAroundHelperAccessory_north";
            if(empty?visible!=0:(opaque==0||visible==b.Width*b.Height))throw new Exception("Invalid alpha: "+name);
            Rectangle box=Bounds(b,0);
            for(set=0;set<3;set++){
                if(name.StartsWith(N+"Suit_")){
                    string key=name.Substring((N+"Suit_").Length);BodyMask m=masks[key];
                    if(key.Contains("North"))throw new Exception("Uppercase north");
                    for(int k=0;k<m.Inside.Length;k++){
                        if(m.Inside[k]&&a[k*4+3]<128)throw new Exception("Unsealed interior: "+name);
                        if(!m.Inside[k]&&m.Original[k*4+3]==0&&a[k*4+3]!=0)throw new Exception("Mask leak: "+name);
                        if(m.Inside[k]&&m.Distance[k]<=1.42f&&(Math.Abs(a[k*4]-Ink.B)>1||Math.Abs(a[k*4+1]-Ink.G)>1||Math.Abs(a[k*4+2]-Ink.R)>1))throw new Exception("Missing inward ink: "+name);
                    }
                    using(Bitmap original=new Bitmap(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+key+".png")))if(box!=Bounds(original,0))throw new Exception("Mask bbox mismatch: "+name);
                }
                if(name.StartsWith(N+"Accessory")&&!empty){string face=name==N+"Accessory"?"item":name.Substring((N+"Accessory_").Length);CheckBox(box,AccessoryTarget(face=="item",face),name);}
                if(name=="EgoWeapon_"+N)CheckBox(box,WeaponTarget,name);
            }
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3}, visible={4}, bbox=({5},{6})-({7},{8})",name,b.Width,b.Height,opaque,visible,box.Left,box.Top,box.Right-1,box.Bottom-1);
        }
        Console.WriteLine("SUCCESS: all 72 PNGs passed format, alpha, sealed-mask, inward-ink and coordinate checks.");
    }
    static void CheckBox(Rectangle b,Rectangle t,string name){if(Math.Abs(b.Left-t.Left)>2||Math.Abs(b.Top-t.Top)>2||Math.Abs(b.Right-t.Right)>2||Math.Abs(b.Bottom-t.Bottom)>2)throw new Exception("Coordinate bbox: "+name+" "+b+" expected "+t);}
    static void Review() {
        using(Bitmap sheet=CanvasRect(2160,900))using(Graphics g=G(sheet))using(Font title=new Font(FontFamily.GenericSansSerif,15,FontStyle.Bold))using(Font label=new Font(FontFamily.GenericSansSerif,10,FontStyle.Bold))using(Brush white=new SolidBrush(C("EEE6D5"))){
            g.Clear(C("171A24"));
            string[] labels={"Female S","Female N","Female E","Male S","Weapon","Accessory","Armor Icon","Weapon Icon","Skill Icon"};
            for(set=0;set<3;set++){
                int y=set*300;g.DrawString(N+" / "+(set==0?"GRINDER MK4 / HE":set==1?"JUSTITIA / ALEPH":"LAMP / WAW"),title,white,14,y+7);
                string suit="Things/Pawn/Humanlike/Apparel/"+N+"Suit/"+N+"Suit";
                string[] rel={suit+"_Female_south.png",suit+"_Female_north.png",suit+"_Female_east.png",suit+"_Male_south.png","Things/Item/Equipment/WeaponMelee/EgoWeapon_"+N+".png","Things/Pawn/Humanlike/Apparel/Accessory/"+N+"Accessory.png","UI/Commands/ExtractEGO_"+N+"_Armor.png","UI/Commands/ExtractEGO_"+N+"_Weapon.png","UI/WeaponSkills/"+N+"/"+skills[set]+".png"};
                for(int j=0;j<9;j++){
                    int x=j*240;using(Brush bg=new SolidBrush(C("303440")))g.FillRectangle(bg,x+5,y+38,230,253);
                    for(int yy=0;yy<12;yy++)for(int xx=0;xx<11;xx++)if((xx+yy)%2==0)using(Brush br=new SolidBrush(C("383E4A")))g.FillRectangle(br,x+10+xx*20,y+66+yy*18,20,18);
                    g.DrawString(labels[j],label,white,x+14,y+43);
                    using(Bitmap b=new Bitmap(Path.Combine(root,"Cont/Textures",rel[j]))){Rectangle r=Bounds(b,0);if(j>=6)r=new Rectangle(0,0,128,128);float sc=Math.Min(204f/r.Width,208f/r.Height);int w=(int)(r.Width*sc),h=(int)(r.Height*sc);g.DrawImage(b,new Rectangle(x+120-w/2,y+176-h/2,w,h),r,GraphicsUnit.Pixel);}
                }
            }
            string path=Path.Combine(Path.GetTempPath(),"Vanilla-GroupD-review.png");sheet.Save(path,ImageFormat.Png);Console.WriteLine("REVIEW "+path);
        }
    }
    static Bitmap CanvasRect(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
    public static void Run(string modRoot){
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();masks.Clear();
        for(set=0;set<3;set++){
            using(Bitmap weapon=Weapon()){Save(weapon,"Things/Item/Equipment/WeaponMelee/EgoWeapon_"+N+".png",512);using(Bitmap icon=Icon(weapon,false))Save(icon,"UI/Commands/ExtractEGO_"+N+"_Weapon.png",128);}
            foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})using(Bitmap b=Worn(body,face))Save(b,"Things/Pawn/Humanlike/Apparel/"+N+"Suit/"+N+"Suit_"+body+"_"+face+".png",512);
            using(Bitmap suit=SuitItem()){Save(suit,"Things/Pawn/Humanlike/Apparel/"+N+"Suit/"+N+"Suit.png",512);using(Bitmap icon=Icon(suit,true))Save(icon,"UI/Commands/ExtractEGO_"+N+"_Armor.png",128);}
            foreach(string face in new string[]{"item","south","north","east"})using(Bitmap b=Accessory(face=="item",face))Save(b,"Things/Pawn/Humanlike/Apparel/Accessory/"+N+"Accessory"+(face=="item"?"":"_"+face)+".png",512);
            using(Bitmap b=Ability())Save(b,"UI/WeaponSkills/"+N+"/"+skills[set]+".png",128);
        }
        Verify();Review();
    }
}

'@
if (-not ('GroupDArt' -as [type])) { Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing }
[GroupDArt]::Run($ModRoot)

