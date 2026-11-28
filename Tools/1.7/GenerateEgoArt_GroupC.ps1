#requires -Version 5.1
# Deterministic Group C painter. OneSin's exact mask sealing and 3x GDI+ pipeline.
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

public static class GroupCArt {
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

    static int theme;
    static readonly string[] Names={"DerFreischutz","FuneralOfDeadButterflies","SnowQueen"};
    static readonly string[] Skills={"MagicBullet_DimensionalBarrage","SolemnLament_ButterflyRequiem","FrostSplinter_GlacialDuel"};
    static string Name {get{return Names[theme];}}
    static string F(float v){return v.ToString("0.###",CultureInfo.InvariantCulture);}
    static void Line(Graphics g,float x,float y,float xx,float yy,string col,float w) {
        using(Pen p=new Pen(C(col),w)){p.StartCap=p.EndCap=LineCap.Round;g.DrawLine(p,x,y,xx,yy);}
    }
    static void Glow(Graphics g,float x,float y,float r,string col,int alpha) {
        using(GraphicsPath p=new GraphicsPath()) {p.AddEllipse(x-r,y-r,r*2,r*2);
            using(PathGradientBrush b=new PathGradientBrush(p)){b.CenterColor=Color.FromArgb(alpha,C(col));b.SurroundColors=new Color[]{Color.FromArgb(0,C(col))};g.FillPath(b,p);}}
    }
    static void Ring(Graphics g,float x,float y,float r,string col,float width) {
        using(Pen p=new Pen(C(col),width))g.DrawEllipse(p,x-r,y-r,r*2,r*2);
    }
    static void Star(Graphics g,float x,float y,float r,string col) {
        Paint(g,"M "+F(x)+" "+F(y-r)+" L "+F(x+r*.22f)+" "+F(y-r*.22f)+" L "+F(x+r)+" "+F(y)+" L "+F(x+r*.22f)+" "+F(y+r*.22f)+" L "+F(x)+" "+F(y+r)+" L "+F(x-r*.22f)+" "+F(y+r*.22f)+" L "+F(x-r)+" "+F(y)+" L "+F(x-r*.22f)+" "+F(y-r*.22f)+" Z",col,"89CDE4",.7f);
    }
    static void Snowflake(Graphics g,float x,float y,float radius,string color) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);
        for(int i=0;i<6;i++){g.RotateTransform(60);Line(g,0,0,0,-radius,color,1.8f);Line(g,0,-radius*.6f,-radius*.21f,-radius*.79f,color,1.3f);Line(g,0,-radius*.6f,radius*.21f,-radius*.79f,color,1.3f);}
        g.Restore(st);
    }
    static void Seal(Graphics g,float x,float y,float r) {
        Glow(g,x,y,r*1.7f,"3AE0FF",95);Ring(g,x,y,r,"3AE0FF",2.2f);Ring(g,x,y,r*.77f,"86F3FF",1);
        for(int k=0;k<7;k++) {
            double a=k*Math.PI*2/7-Math.PI*.5,aa=(k+3)*Math.PI*2/7-Math.PI*.5;
            float xx=x+(float)Math.Cos(a)*r*.67f,yy=y+(float)Math.Sin(a)*r*.67f;
            Line(g,xx,yy,x+(float)Math.Cos(aa)*r*.67f,y+(float)Math.Sin(aa)*r*.67f,"3AE0FF",1);
            Ellipse(g,x+(float)Math.Cos(a)*r-1.7f,y+(float)Math.Sin(a)*r-1.7f,3.4f,3.4f,"E3FFFF","3AE0FF",0);
        }
        Ellipse(g,x-3,y-3,6,6,"F0FFFF","3AE0FF",0);
    }
    static void Butterfly(Graphics g,float x,float y,float scale,float angle,bool pale,int alpha=255) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(scale,scale);
        string a=pale?"FFFFF1":"566070",b=pale?"B9C8CF":"181C2A";
        using(GraphicsPath p=P("M -1 0 C -10 -27 -31 -45 -40 -35 C -45 -29 -37 -25 -39 -19 C -44 -12 -34 -3 -26 0 L -8 7 C -25 5 -34 17 -27 25 C -22 34 -7 24 0 6 C 7 24 22 34 27 25 C 34 17 25 5 8 7 L 26 0 C 34 -3 44 -12 39 -19 C 37 -25 45 -29 40 -35 C 31 -45 10 -27 1 0 Z")) {
            using(LinearGradientBrush br=new LinearGradientBrush(p.GetBounds(),Color.FromArgb(alpha,C(a)),Color.FromArgb(alpha,C(b)),65))g.FillPath(br,p);
            using(Pen pen=new Pen(Color.FromArgb(alpha,Ink),1.9f)){pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);}
        }
        Stroke(g,"M -3 4 L -33 -28 M -5 7 L -23 22 M 3 4 L 33 -28 M 5 7 L 23 22 M -14 -6 L -33 -11 M 14 -6 L 33 -11",pale?"7F939E":"B8C9D0",.9f);
        Stroke(g,"M 0 -5 L 0 13 M 0 -5 L -5 -12 M 0 -5 L 5 -12","140A0C",2);
        g.Restore(st);
    }
    static void Crystal(Graphics g,float x,float y,float scale,float angle) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(scale,scale);
        Paint(g,"M 0 -46 L 20 -12 L 13 24 L 0 42 L -17 20 L -21 -11 Z","F4FFFF","4C9EBF",2.4f);
        Paint(g,"M 0 -46 L 2 -9 L -17 20 L -21 -11 Z","B5F7FF","64B8D8",0);
        Paint(g,"M 2 -9 L 20 -12 L 13 24 L 0 42 Z","8FDBEF","3E83B4",0);
        Stroke(g,"M 0 -46 L 2 -9 L 0 42 M -21 -11 L 2 -9 L 20 -12 M 2 -9 L -17 20","E9FFFF",1.4f);
        g.Restore(st);
    }
    static void Coat(Graphics g,string facing) {
        bool back=facing=="north",side=facing=="east";
        float cx=side?208:150;
        string light=theme==0?"2F3C69":theme==1?"363B46":"F3FFFF";
        string dark=theme==0?"10172F":theme==1?"0D1019":"80B7D3";
        string trim=theme==0?"BE9B54":theme==1?"CAD3D6":"E2FCFF";
        Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z",light,dark,0);
        // Long tailored panels, hand-painted fold shadows and reflected edge lighting.
        for(int k=0;k<7;k++) {
            float x=15+k*46;
            Stroke(g,"M "+F(x)+" 87 C "+F(x-18)+" 180 "+F(x+8)+" 267 "+F(x-10)+" 367",theme==2?"6597B5":"0A1022",5);
            Stroke(g,"M "+F(x+8)+" 104 C "+F(x-1)+" 195 "+F(x+21)+" 279 "+F(x+4)+" 356",theme==2?"E1F7FF":theme==0?"435582":"454C5B",2);
        }
        if(theme==0) {
            // Separate capelet over a double-breasted military greatcoat.
            Paint(g,"M 5 13 L 100 -12 L 150 30 L 201 -12 L 296 13 L 323 101 L 255 125 L 204 88 L 150 108 L 96 88 L 45 125 L -23 101 Z","222D50","0B1024",4);
            Stroke(g,"M -10 93 L 44 113 L 97 77 L 150 94 L 203 77 L 255 113 L 310 93",trim,2.4f);
            Paint(g,"M 106 -12 L 148 11 L 194 -12 L 189 37 L 153 66 L 111 34 Z","34466B","141A30",3);
            Stroke(g,"M 116 1 L 151 28 L 184 1",trim,2);
            if(back) {
                Paint(g,"M 89 105 L 150 126 L 211 105 L 227 346 L 152 329 L 74 346 Z","2C3A63","121B34",2.6f);
                Stroke(g,"M 91 108 L 79 339 L 145 324 M 209 108 L 223 339 L 157 324",trim,2);
                Seal(g,150,159,28);Line(g,150,210,150,355,"0A1022",4);
            } else {
                Paint(g,"M "+F(cx-45)+" 40 L "+F(cx+36)+" 40 L "+F(cx+46)+" 231 L "+F(cx-50)+" 231 Z","28365D","111A33",2.8f);
                for(int k=0;k<5;k++){float y=92+k*26;Ellipse(g,cx-30,y,7,7,"E7D296","8B6934",1.2f);if(!side)Ellipse(g,cx+23,y,7,7,"E7D296","8B6934",1.2f);}
                Seal(g,cx,118,23);
                Stroke(g,"M "+F(cx-43)+" 54 L "+F(cx-47)+" 220 M "+F(cx+37)+" 54 L "+F(cx+43)+" 220",trim,1.7f);
            }
            Paint(g,"M 18 224 C 95 239 213 239 284 224 L 285 243 C 210 259 92 259 16 243 Z","342E30","171A27",2.6f);
            Paint(g,"M 137 234 L 164 234 L 164 255 L 137 255 Z","E2CA83","80612E",2);
            Paint(g,"M 143 239 L 158 239 L 158 249 L 143 249 Z","1B233C","10172A",1);
            Stroke(g,"M 7 338 C 100 360 206 360 293 338",trim,3);
            for(int k=0;k<6;k++){float x=40+k*43;Stroke(g,"M "+F(x)+" 338 L "+F(x+5)+" 329 L "+F(x+10)+" 341","806C46",1.4f);}
        } else if(theme==1) {
            // Funeral tails, deep waist, silver coffin clasps; no colored highlights.
            Paint(g,"M 17 18 L 105 -10 L 150 24 L 196 -10 L 283 18 L 278 98 L 226 118 L 201 216 L 151 246 L 97 216 L 73 118 L 21 98 Z","282C36","0B0E16",3.6f);
            if(back) {
                Stroke(g,"M 80 69 L 150 99 L 220 69 M 150 100 L 150 216","68717B",2);
                Paint(g,"M 125 104 L 174 104 L 185 164 L 150 189 L 114 164 Z","555E68","171C28",2.8f);
                Butterfly(g,150,143,.63f,0,true);
                Paint(g,"M 39 222 L 144 225 L 125 375 L -7 359 Z","252B37","0A0D16",3);
                Paint(g,"M 156 225 L 261 222 L 307 359 L 175 375 Z","303744","0A0D16",3);
            } else {
                Paint(g,"M "+F(cx-39)+" -13 L "+F(cx+36)+" -13 L "+F(cx+25)+" 136 L "+F(cx)+" 170 L "+F(cx-28)+" 136 Z","FFFFF3","B3C1C8",2.4f);
                Paint(g,"M "+F(cx-8)+" 24 L "+F(cx+9)+" 24 L "+F(cx+13)+" 39 L "+F(cx)+" 52 L "+F(cx-14)+" 39 Z","333843","090D14",1.6f);
                Paint(g,"M "+F(cx)+" 49 L "+F(cx+12)+" 113 L "+F(cx)+" 131 L "+F(cx-13)+" 113 Z","353944","090D14",2);
                Paint(g,"M "+F(cx-45)+" -4 L "+F(cx-12)+" 92 L "+F(cx-35)+" 76 L "+F(cx-23)+" 106 L "+F(cx-61)+" 163 L "+F(cx-84)+" 42 Z","434955","171C27",3);
                Paint(g,"M "+F(cx+44)+" -4 L "+F(cx+12)+" 92 L "+F(cx+35)+" 76 L "+F(cx+23)+" 106 L "+F(cx+61)+" 163 L "+F(cx+81)+" 42 Z","3B414E","111620",3);
                // Narrow six-sided coffin-lid lapel pins.
                foreach(int s in new int[]{-1,1}) {
                    GraphicsState st=g.Save();g.TranslateTransform(cx+s*40,68);g.RotateTransform(s*22);
                    Paint(g,"M -4 -13 L 4 -13 L 7 -6 L 5 15 L -5 15 L -7 -6 Z","F1F7F4","73858E",1.4f);
                    Line(g,-3,-7,3,-7,"4C5B65",1);Line(g,0,-10,0,10,"4C5B65",1);g.Restore(st);
                }
                Butterfly(g,cx-54,110,.40f,-24,true);
                Stroke(g,"M 84 225 L 70 353 M 216 225 L 230 353","6D7886",2);
            }
            Stroke(g,"M 27 216 C 105 234 195 234 273 216","727E89",2);
            for(int k=0;k<3;k++)Ellipse(g,cx-3,164+k*21,6,6,"C5D0D3","515D68",1);
            // Ethereal smaller butterflies have translucent wings and opaque dark veins.
            GraphicsState bs=g.Save();g.TranslateTransform(0,0);
            Butterfly(g,65,280,.39f,-27,true,210);Butterfly(g,232,316,.32f,24,true,185);Butterfly(g,250,170,.23f,19,true,180);g.Restore(bs);
            Stroke(g,"M 28 340 C 60 328 72 304 83 292 M 249 344 C 228 326 230 305 211 287","667782",1.2f);
        } else {
            // Royal ice mantle, fur, silver embroidery, brooch and asymmetric side view.
            Paint(g,"M -10 25 L 100 -17 L 150 12 L 200 -17 L 310 25 L 305 159 L 259 189 L 213 139 L 150 156 L 87 139 L 41 189 L -5 159 Z","F7FFFF","A3C8DD",3.4f);
            Stroke(g,"M 3 150 L 43 172 L 88 122 L 150 139 L 212 122 L 257 172 L 297 150","E5FCFF",5);
            Stroke(g,"M 3 158 L 43 180 L 88 130 L 150 147 L 212 130 L 257 180 L 297 158","739DBA",1.5f);
            Paint(g,"M 108 -12 L 191 -12 L 187 33 L 150 72 L 113 33 Z","D3F5FF","6D9CBF",3);
            // Overlapping individually shaded fur tufts read as a fluffy stole.
            for(int k=0;k<17;k++) {
                float x=3+k*18.4f,y=44+(float)Math.Sin(k*Math.PI/16)*33;
                GraphicsState fur=g.Save();g.TranslateTransform(x,y);
                Paint(g,"M -14 -6 C -14 -18 5 -19 13 -10 L 16 0 L 11 -1 L 12 9 L 7 6 L 4 14 L 0 10 L -5 14 L -8 8 L -13 8 L -12 1 L -17 0 Z","FFFFFF","C2D9E4",1.0f);g.Restore(fur);
                Stroke(g,"M "+F(x-7)+" "+F(y+2)+" L "+F(x-1)+" "+F(y+8)+" L "+F(x+5)+" "+F(y+1),"FFFFFF",2);
            }
            if(back) {
                Paint(g,"M 72 93 L 150 112 L 228 93 L 256 333 L 150 362 L 44 333 Z","E9FAFF","94BED9",2.8f);
                Stroke(g,"M 78 101 L 52 326 L 150 353 L 248 326 L 222 101","F7FFFF",3);
                Snowflake(g,150,185,49,"649AB8");Snowflake(g,150,185,45,"E8FDFF");
                Crystal(g,150,185,.48f,0);
            } else {
                Stroke(g,"M "+F(cx-38)+" 88 L "+F(cx-56)+" 316 M "+F(cx+38)+" 88 L "+F(cx+56)+" 316","719BB8",3);
                Stroke(g,"M "+F(cx-32)+" 88 L "+F(cx-50)+" 316 M "+F(cx+32)+" 88 L "+F(cx+50)+" 316","FFFFFF",2);
                Crystal(g,cx,102,.58f,0);Star(g,cx-8,86,7,"FFFFFF");
                Snowflake(g,cx,190,22,"6495B2");Snowflake(g,cx,190,20,"F5FFFF");
                for(int i=0;i<4;i++)Ellipse(g,cx-3,145+i*28,6,6,"FFFFFF","699AB7",1);
            }
            // Frost filigree follows the hem and remains legible on all body shapes.
            Stroke(g,"M 0 329 C 100 350 200 350 300 329","7BAAC4",3);
            Stroke(g,"M 0 335 C 100 358 200 358 300 335","FCFFFF",3);
            for(int k=0;k<5;k++) {float x=38+k*55;Snowflake(g,x,322,12,"F7FFFF");Crystal(g,x,286,.21f,0);}
            for(int k=0;k<12;k++)Ellipse(g,k*27-6,348,32,23,"FFFFFF","B5CEDC",1.2f);
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

    static Bitmap Fit(Bitmap source,Rectangle target) {
        using(Bitmap hi=Canvas(512*S)) {
            Rectangle src=Bounds(source,0);
            using(Graphics g=G(hi))using(ImageAttributes ia=new ImageAttributes()) {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(source,new Rectangle(target.X*S,target.Y*S,target.Width*S,target.Height*S),src.X,src.Y,src.Width,src.Height,GraphicsUnit.Pixel,ia);
            }
            Bitmap result=Resolve(hi,512);byte[] pixels=Read(result);
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)if(!target.Contains(x,y)){int k=(y*512+x)*4;pixels[k]=pixels[k+1]=pixels[k+2]=pixels[k+3]=0;}
            Write(result,pixels);return result;
        }
    }
    static void Pistol(Graphics g,float x,float y,float scale,bool white) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        string a=white?"FFFFF0":"4E5662",b=white?"C2CCD0":"0C111B";
        Paint(g,"M -11 -17 L 30 -17 L 44 -25 L 182 -25 L 196 -16 L 196 8 L 80 8 L 59 26 L 39 26 L 33 69 L 12 78 L -15 60 L 8 14 L -14 9 Z",a,b,3.6f);
        Paint(g,"M 24 15 L 49 19 L 34 64 L 16 67 L -1 56 Z",white?"363D49":"B9C9D0",white?"121823":"5E6B78",2.4f);
        Paint(g,"M 65 9 L 91 9 L 84 29 L 55 29 L 55 21 Z","8C9DA5","1D2631",2);
        Paint(g,"M 64 12 L 80 12 L 77 23 L 62 23 Z","151B25","0D1119",1);
        Paint(g,"M 46 -22 L 187 -22 L 187 -7 L 46 -7 Z",white?"FFFFFF":"58636C",b,1.4f);
        Stroke(g,"M 53 -17 L 176 -17",white?"84959D":"BDC8C9",1.2f);
        Line(g,184,-22,184,5,"140A0C",4);Line(g,175,-29,175,-25,"140A0C",3);
        for(int k=0;k<4;k++)Line(g,35+k*5,-21,30+k*5,-7,white?"80939D":"8D9CA2",1.3f);
        Butterfly(g,114,-5,.36f,0,!white);Butterfly(g,158,-5,.20f,18,!white);
        Ellipse(g,22,26,6,6,"D7E0DA","66717C",1);
        Stroke(g,"M 12 38 L 28 49 M 9 46 L 25 56",white?"809099":"D5DEDA",1);
        g.Restore(st);
    }
    static Bitmap Weapon() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                if(theme==0) {
                    // Barrel and stock remain on the same very narrow horizontal axis.
                    Paint(g,"M 16 240 L 66 240 L 88 247 L 120 247 L 127 258 L 113 269 L 77 263 L 62 283 L 16 281 Z","3A467A","11182E",3.4f);
                    Stroke(g,"M 22 246 L 64 246 L 84 253 L 108 253 M 23 274 L 57 275 L 75 256","C7A15A",1.8f);
                    Paint(g,"M 107 245 L 241 245 L 250 254 L 238 266 L 108 266 Z","323D60","111829",3);
                    Paint(g,"M 236 249 L 484 249 L 484 258 L 236 258 Z","667088","17213B",2.3f);
                    Line(g,253,251,475,251,"A3B8CA",1);Line(g,255,256,475,256,"080F20",1.4f);
                    Paint(g,"M 480 246 L 495 246 L 497 250 L 497 258 L 481 261 Z","303B52","12192A",2);
                    Paint(g,"M 129 260 L 146 264 L 137 283 L 122 283 Z","342F38","111624",2);
                    Stroke(g,"M 146 265 C 177 265 177 282 148 279","B89A59",2.8f);
                    Paint(g,"M 165 235 L 173 235 L 176 248 L 163 248 Z","C8AF70","675334",1.5f);
                    Paint(g,"M 215 235 L 223 235 L 226 248 L 213 248 Z","C8AF70","675334",1.5f);
                    Paint(g,"M 149 227 L 227 227 L 235 231 L 235 239 L 149 239 Z","485775","151C31",2);
                    Ellipse(g,225,227,11,12,"3AE0FF","153F66",1.4f);
                    Paint(g,"M 145 226 L 155 226 L 155 241 L 145 241 Z","C7AC68","604D32",1.5f);
                    for(int k=0;k<3;k++)Paint(g,"M "+(183+k*80)+" 246 L "+(192+k*80)+" 246 L "+(192+k*80)+" 261 L "+(183+k*80)+" 261 Z","D2B46E","6D542E",1.2f);
                    for(int k=0;k<13;k++){float x=245+k*17;Stroke(g,"M "+F(x)+" 251 L "+F(x+3)+" 256 L "+F(x+6)+" 251","3AE0FF",.9f);}
                    Seal(g,120,255,8);
                } else if(theme==1) {
                    Pistol(g,99,157,1.12f,false);Pistol(g,203,285,1.12f,true);
                } else {
                    // Frozen blade points toward bottom-left; long staff rises to top-right.
                    Line(g,165,325,433,126,"140A0C",12);Line(g,165,325,433,126,"183555",7);
                    Line(g,165,323,433,124,"477E9E",2);
                    for(int k=0;k<8;k++){float x=232+k*17,y=275-k*12.7f;Line(g,x-3,y-4,x+4,y+4,"92CDE1",1.5f);}
                    Crystal(g,431,126,.25f,35);
                    Paint(g,"M 74 382 L 175 270 L 207 267 L 197 295 Z","EFFFFF","55BBD9",3);
                    Paint(g,"M 74 382 L 184 285 L 175 270 Z","FFFFFF","C0FCFF",0);
                    Paint(g,"M 74 382 L 184 285 L 197 295 Z","78E1F1","3487AD",0);
                    Stroke(g,"M 74 382 L 184 285 L 207 267","F1FFFF",1.8f);
                    Paint(g,"M 159 278 L 150 251 L 167 258 L 178 274 L 207 250 L 215 227 L 226 235 L 217 261 L 198 284 L 208 302 L 197 313 L 181 292 L 158 308 L 140 307 Z","527F9F","142C4B",3);
                    Crystal(g,184,279,.30f,48);
                    Stroke(g,"M 155 278 C 146 264 158 262 166 274 M 204 270 C 225 264 225 246 216 248","C4F7FF",1.5f);
                    Star(g,178,287,7,"F2FFFF");
                }
            }
            Rectangle target=theme==0?Rectangle.FromLTRB(12,226,499,288):theme==1?Rectangle.FromLTRB(54,130,458,382):Rectangle.FromLTRB(72,124,444,386);
            return Fit(hi,target);
        }
    }
    static Rectangle AccessoryTarget(string face) {
        if(theme==0)return face=="item"?Rectangle.FromLTRB(72,125,440,387):face=="south"?Rectangle.FromLTRB(236,255,277,278):Rectangle.FromLTRB(269,250,304,273);
        if(theme==1)return face=="item"?Rectangle.FromLTRB(85,72,427,440):face=="south"?Rectangle.FromLTRB(197,266,315,370):Rectangle.FromLTRB(200,260,313,361);
        return face=="item"?Rectangle.FromLTRB(72,119,440,393):face=="south"?Rectangle.FromLTRB(203,237,244,267):Rectangle.FromLTRB(260,237,301,267);
    }
    static Bitmap Accessory(string face) {
        if(face=="north")return Canvas(512);
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                if(theme==0) {
                    // A long bent cherrywood pipe; smoke is painted into the registration box.
                    Stroke(g,"M 70 286 C 123 284 172 301 221 289 C 241 283 246 261 246 244","140A0C",16);
                    Stroke(g,"M 70 286 C 123 284 172 301 221 289 C 241 283 246 261 246 244","77503C",9);
                    Stroke(g,"M 78 283 C 140 285 180 298 218 285","B18B60",2);
                    Paint(g,"M 225 226 L 280 226 L 272 283 C 265 301 237 298 232 283 Z","AE7046","402B2A",3.4f);
                    Paint(g,"M 224 235 L 279 235 L 277 248 L 226 248 Z","E7C885","88683E",2);
                    Paint(g,"M 231 274 L 274 274 L 271 285 L 234 285 Z","CCA75D","6E5030",1.7f);
                    Ellipse(g,224,218,57,20,"E9D493","755431",2);
                    Ellipse(g,230,221,45,12,"76F7FF","12465C",1);
                    Glow(g,252,220,23,"3AE0FF",110);
                    Stroke(g,"M 251 215 C 223 195 291 185 267 163 C 244 142 276 129 280 110","207CBA",12);
                    Stroke(g,"M 251 215 C 223 195 291 185 267 163 C 244 142 276 129 280 110","3AE0FF",5);
                    Stroke(g,"M 251 212 C 235 196 281 184 269 169","CCFFFF",1.8f);
                } else if(theme==1) {
                    // Wing veil with slender trailing ribbons and a coffin-shaped brooch.
                    Stroke(g,"M 187 202 C 144 261 218 268 163 337 M 205 202 C 248 262 174 286 236 337","140A0C",7);
                    Stroke(g,"M 187 202 C 144 261 218 268 163 337 M 205 202 C 248 262 174 286 236 337","DAE7E7",3);
                    Butterfly(g,196,185,2.25f,-11,true);Butterfly(g,252,113,.58f,22,true);
                    Paint(g,"M 191 174 L 203 174 L 208 184 L 204 218 L 190 218 L 186 184 Z","E6F5F1","748893",2);
                    Line(g,197,181,197,209,"374B58",1.6f);Line(g,192,190,202,190,"374B58",1.6f);
                } else {
                    // Faceted rose petals, silver setting and tiny hanging ice shard.
                    Paint(g,"M 190 248 C 156 254 139 236 133 210 C 168 209 188 222 190 248 Z","A8ECF4","408CB5",3);
                    Paint(g,"M 200 250 C 231 255 255 234 259 212 C 225 208 205 223 200 250 Z","DAFFFF","5AA9C8",3);
                    Stroke(g,"M 143 218 L 188 242 M 248 220 L 205 243","F4FFFF",1.8f);
                    for(int k=0;k<7;k++) {
                        GraphicsState st=g.Save();g.TranslateTransform(197,183);g.RotateTransform(k*360f/7);
                        Paint(g,"M -8 7 L -34 -1 L -45 -24 L -34 -43 L -8 -48 L 19 -28 L 21 -6 L 5 13 Z","E2FFFF","4C9BC8",2.5f);
                        Stroke(g,"M -34 -43 L -17 -22 L -34 -1 M -17 -22 L 19 -28","FFFFFF",1.4f);g.Restore(st);
                    }
                    for(int k=0;k<5;k++) {
                        GraphicsState st=g.Save();g.TranslateTransform(197,183);g.RotateTransform(k*72+20);
                        Paint(g,"M -3 6 L -21 -3 L -23 -22 L -8 -29 L 12 -20 L 17 -4 L 6 10 Z","E9FFFF","5BAACD",1.8f);
                        Stroke(g,"M -23 -22 L -5 -11 L 12 -20","FFFFFF",1);g.Restore(st);
                    }
                    Paint(g,"M 189 174 L 202 170 L 210 180 L 205 192 L 191 194 L 184 184 Z","CAFAFF","3C8CB7",1.8f);
                    Stroke(g,"M 189 178 L 200 176 L 204 183 L 200 189 L 193 187 L 193 182 L 199 182","F6FFFF",1.3f);
                    Crystal(g,197,277,.35f,0);
                    Star(g,153,133,12,"FFFFFF");Star(g,232,202,9,"FFFFFF");
                }
            }
            return Fit(hi,AccessoryTarget(face));
        }
    }
    static void Frame(Graphics g) {
        Paint(g,"M 17 5 L 111 5 L 123 17 L 123 111 L 111 123 L 17 123 L 5 111 L 5 17 Z","39202B","100F1A",3);
        Stroke(g,"M 19 7 L 109 7 L 121 19 L 121 109 L 109 121 L 19 121 L 7 109 L 7 19 Z","B79B61",2);
        Stroke(g,"M 22 12 L 106 12 L 116 22 L 116 106 L 106 116 L 22 116 L 12 106 L 12 22 Z","8D2834",2);
        Paint(g,"M 59 17 L 64 12 L 69 17 L 64 22 Z","E8CB8C","8D653E",1);
        Paint(g,"M 59 111 L 64 106 L 69 111 L 64 116 Z","C3313D","671722",1);
        for(int k=0;k<4;k++){GraphicsState st=g.Save();g.TranslateTransform(64,64);g.RotateTransform(k*90);Stroke(g,"M -49 -32 L -49 -42 L -39 -49","DCC182",1.3f);g.Restore(st);}
    }
    static Bitmap Icon(Bitmap source,bool armor) {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);Frame(g);Glow(g,64,65,46,theme==0?"3AE0FF":theme==1?"D7E0E3":"9EECFF",75);
                Rectangle src=Bounds(source,0);
                if(armor)g.DrawImage(source,new Rectangle(30,24,68,81),src,GraphicsUnit.Pixel);
                else {
                    GraphicsState st=g.Save();g.TranslateTransform(64,65);
                    if(theme==0)g.RotateTransform(-32);
                    float scale=Math.Min(93f/src.Width,83f/src.Height);g.ScaleTransform(scale,scale);
                    g.DrawImage(source,new RectangleF(-src.Width/2f,-src.Height/2f,src.Width,src.Height),src,GraphicsUnit.Pixel);g.Restore(st);
                }
            }
            return Resolve(hi,128);
        }
    }
    static Bitmap Ability() {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);Frame(g);
                if(theme==0) {
                    Seal(g,64,63,34);
                    GraphicsState st=g.Save();g.TranslateTransform(64,64);g.RotateTransform(35);
                    Glow(g,0,0,29,"3AE0FF",115);
                    Paint(g,"M -8 -20 C -8 -29 -3 -36 0 -41 C 3 -36 8 -29 8 -20 L 8 31 L -8 31 Z","D9FFFF","2787B4",2);
                    Paint(g,"M -8 14 L 8 14 L 8 31 L -8 31 Z","E6CB86","8C642C",1.6f);
                    Line(g,-3,-24,-3,9,"FFFFFF",1.5f);
                    Stroke(g,"M -3 -5 L 4 -5 L -1 7","103C66",1.5f);g.Restore(st);
                    for(int k=0;k<6;k++){double a=k*Math.PI/3;Ellipse(g,62+(float)Math.Cos(a)*42,62+(float)Math.Sin(a)*42,4,4,"CEFFFF","3AE0FF",.6f);}
                } else if(theme==1) {
                    Glow(g,64,64,45,"E1E9EA",80);
                    Stroke(g,"M 22 93 C 52 88 48 48 94 29","74838D",9);
                    Stroke(g,"M 22 93 C 52 88 48 48 94 29","FCFFF4",3);
                    Stroke(g,"M 35 103 C 82 88 66 50 105 42","090F1B",10);
                    Stroke(g,"M 35 103 C 82 88 66 50 105 42","ABBBC5",2);
                    Butterfly(g,53,58,.73f,-24,true);Butterfly(g,85,82,.52f,26,false);Butterfly(g,90,34,.30f,12,true);
                    Star(g,31,86,6,"F4FFFF");
                } else {
                    Glow(g,64,64,44,"8EEBFF",150);Snowflake(g,64,64,39,"D1FBFF");
                    for(int k=0;k<6;k++){double a=k*Math.PI/3;Crystal(g,64+(float)Math.Cos(a)*30,64+(float)Math.Sin(a)*30,.22f,k*60);}
                    Crystal(g,65,64,.96f,36);Star(g,54,49,13,"FFFFFF");
                    Stroke(g,"M 26 99 C 39 86 46 90 53 78 M 80 48 C 86 35 91 37 101 23","7AC7ED",2);
                }
            }
            return Resolve(hi,128);
        }
    }
    static void Verify() {
        if(files.Count!=72||new HashSet<string>(files,StringComparer.OrdinalIgnoreCase).Count!=72)throw new Exception("Expected 72 unique outputs");
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])) {
            string name=Path.GetFileNameWithoutExtension(files[j]);int set=-1;
            for(int t=0;t<3;t++)if(name.Contains(Names[t])||name==Skills[t])set=t;
            if(set<0)throw new Exception("Unexpected output "+name);theme=set;
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid format: "+name);
            byte[] a=Read(b);int opaque=0,visible=0;
            for(int k=0;k<a.Length/4;k++){if(a[k*4+3]==255)opaque++;if(a[k*4+3]>0)visible++;else if(a[k*4]!=0||a[k*4+1]!=0||a[k*4+2]!=0)throw new Exception("Hidden RGB: "+name);}
            Rectangle box=Bounds(b,0);
            if(name==Name+"Accessory_north") {if(visible!=0)throw new Exception("North accessory must be transparent: "+name);}
            else if(opaque==0||visible==b.Width*b.Height)throw new Exception("Invalid alpha: "+name);
            if(name.StartsWith(Name+"Suit_")) {
                string key=name.Substring((Name+"Suit_").Length).Replace("North","north");BodyMask m=masks[key];
                for(int k=0;k<m.Inside.Length;k++) {
                    if(m.Inside[k]&&a[k*4+3]<128)throw new Exception("Unsealed interior: "+name);
                    if(!m.Inside[k]&&m.Original[k*4+3]==0&&a[k*4+3]!=0)throw new Exception("Mask leak: "+name);
                    if(m.Inside[k]&&m.Distance[k]<=1.42f&&(Math.Abs(a[k*4]-Ink.B)>1||Math.Abs(a[k*4+1]-Ink.G)>1||Math.Abs(a[k*4+2]-Ink.R)>1))throw new Exception("Missing inward ink: "+name);
                }
                using(Bitmap original=new Bitmap(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+key+".png")))if(box!=Bounds(original,0))throw new Exception("Mask bbox mismatch: "+name);
            }
            if(name.StartsWith(Name+"Accessory")&&!name.EndsWith("_north")) {
                string face=name==Name+"Accessory"?"item":name.Substring((Name+"Accessory_").Length);CheckBox(box,AccessoryTarget(face),2,name);
            }
            if(name=="EgoWeapon_"+Name)CheckBox(box,theme==0?Rectangle.FromLTRB(12,226,499,288):theme==1?Rectangle.FromLTRB(54,130,458,382):Rectangle.FromLTRB(72,124,444,386),2,name);
            if(sizes[j]==128)CheckBox(box,Rectangle.FromLTRB(3,3,125,125),3,name);
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3}, visible={4}, bbox=({5},{6})-({7},{8})",name,b.Width,b.Height,opaque,visible,box.Left,box.Top,box.Right-1,box.Bottom-1);
        }
        foreach(string n in Names) {
            int count=0;foreach(string f in files)if(f.Contains(n))count++;
            // Skill basenames differ; their enclosing folder carries the set name.
            if(count!=24)throw new Exception("Set count: "+n+" = "+count);
            bool exact=false;foreach(string f in Directory.GetFiles(Path.Combine(root,"Cont/Textures/Things/Pawn/Humanlike/Apparel/"+n+"Suit")))if(Path.GetFileName(f)==n+"Suit_Fat_North.png")exact=true;
            if(!exact)throw new Exception("Missing case-sensitive Fat_North filename: "+n);
        }
        Console.WriteLine("SUCCESS: all 72 PNGs passed format, alpha, hidden-RGB, sealed-mask, inward-ink, exact-case and coordinate checks.");
    }
    static void CheckBox(Rectangle b,Rectangle target,int tolerance,string name) {
        if(Math.Abs(b.Left-target.Left)>tolerance||Math.Abs(b.Top-target.Top)>tolerance||Math.Abs(b.Right-target.Right)>tolerance||Math.Abs(b.Bottom-target.Bottom)>tolerance)throw new Exception("Coordinate bbox: "+name+" actual="+b+" target="+target);
    }
    static void Review() {
        string[] labels={"Female S","Female N","Female E","Male S","Weapon","Accessory","Armor Icon","Weapon Icon","Skill Icon"};
        const int cell=220,row=335;
        using(Bitmap sheet=new Bitmap(cell*9,row*3+55,PixelFormat.Format32bppArgb))using(Graphics g=G(sheet))using(Font title=new Font(FontFamily.GenericSansSerif,18,FontStyle.Bold))using(Font label=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold))using(Brush white=new SolidBrush(C("EEE6D5"))) {
            g.Clear(C("171A24"));g.DrawString("GROUP C  /  3x GDI+  /  SEALED VANILLA MASKS",title,white,15,12);
            for(int t=0;t<3;t++) {
                theme=t;int top=55+t*row;g.DrawString(Name+" / HE",title,white,15,top+4);
                string suit="Things/Pawn/Humanlike/Apparel/"+Name+"Suit/"+Name+"Suit";
                string[] paths={suit+"_Female_south.png",suit+"_Female_north.png",suit+"_Female_east.png",suit+"_Male_south.png","Things/Item/Equipment/"+(t==2?"WeaponMelee":"WeaponRanged")+"/EgoWeapon_"+Name+".png","Things/Pawn/Humanlike/Apparel/Accessory/"+Name+"Accessory.png","UI/Commands/ExtractEGO_"+Name+"_Armor.png","UI/Commands/ExtractEGO_"+Name+"_Weapon.png","UI/WeaponSkills/"+Name+"/"+Skills[t]+".png"};
                for(int j=0;j<9;j++) {
                    int x=j*cell,y=top+38;
                    for(int yy=0;yy<13;yy++)for(int xx=0;xx<10;xx++)using(Brush bg=new SolidBrush(C((xx+yy)%2==0?"343B48":"2B323E")))g.FillRectangle(bg,x+8+xx*20,y+28+yy*20,20,20);
                    g.DrawString(labels[j],label,white,x+12,y+5);
                    using(Bitmap b=new Bitmap(Path.Combine(root,"Cont/Textures",paths[j]))) {
                        Rectangle src=Bounds(b,0);if(j>=6)src=new Rectangle(0,0,128,128);
                        float scale=Math.Min(184f/src.Width,240f/src.Height);int w=(int)(src.Width*scale),h=(int)(src.Height*scale);
                        g.DrawImage(b,new Rectangle(x+108-w/2,y+157-h/2,w,h),src,GraphicsUnit.Pixel);
                    }
                }
            }
            string output=Path.Combine(Path.GetTempPath(),"Vanilla-GroupC-review.png");sheet.Save(output,ImageFormat.Png);Console.WriteLine("REVIEW "+output);
        }
    }
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();masks.Clear();
        for(theme=0;theme<3;theme++) {
            using(Bitmap weapon=Weapon()) {
                Save(weapon,"Things/Item/Equipment/"+(theme==2?"WeaponMelee":"WeaponRanged")+"/EgoWeapon_"+Name+".png",512);
                using(Bitmap icon=Icon(weapon,false))Save(icon,"UI/Commands/ExtractEGO_"+Name+"_Weapon.png",128);
            }
            foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})
                using(Bitmap b=Worn(body,face))Save(b,"Things/Pawn/Humanlike/Apparel/"+Name+"Suit/"+Name+"Suit_"+body+"_"+(body=="Fat"&&face=="north"?"North":face)+".png",512);
            using(Bitmap suit=SuitItem()) {
                Save(suit,"Things/Pawn/Humanlike/Apparel/"+Name+"Suit/"+Name+"Suit.png",512);
                using(Bitmap icon=Icon(suit,true))Save(icon,"UI/Commands/ExtractEGO_"+Name+"_Armor.png",128);
            }
            foreach(string face in new string[]{"item","south","north","east"})using(Bitmap b=Accessory(face))Save(b,"Things/Pawn/Humanlike/Apparel/Accessory/"+Name+"Accessory"+(face=="item"?"":"_"+face)+".png",512);
            using(Bitmap icon=Ability())Save(icon,"UI/WeaponSkills/"+Name+"/"+Skills[theme]+".png",128);
        }
        Verify();Review();
    }
}
'@
if (-not ('GroupCArt' -as [type])) { Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing }
[GroupCArt]::Run($ModRoot)
