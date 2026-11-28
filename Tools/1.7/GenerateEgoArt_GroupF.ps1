#requires -Version 5.1
# Deterministic Group F painter: C# GDI+, 3x supersampling, mask-only inputs.

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

public static class GroupFArt {
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

    static int set;
    static readonly string[] names={"WarmHeartedWoodsman","FragmentOfTheUniverse","ScarecrowSearchingForWisdom"};
    static readonly string[] skills={"Logging_HeartCleave","CosmosSpear_StarlightEcho","Harvest_WisdomDrain"};
    static string F(float v){return v.ToString("0.###",CultureInfo.InvariantCulture);}
    static void Line(Graphics g,float x,float y,float xx,float yy,string c,float w){Stroke(g,"M "+F(x)+" "+F(y)+" L "+F(xx)+" "+F(yy),c,w);}
    static void Rivet(Graphics g,float x,float y,float r) {
        Ellipse(g,x-r,y-r,r*2,r*2,"D2CCAD","565D56",1.2f);
        Line(g,x-r*.4f,y-r*.3f,x+r*.4f,y-r*.3f,"EBE2BC",.8f);
    }
    static void Heart(Graphics g,float x,float y,float s) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
        Ellipse(g,-36,-37,72,74,"8A252F","26161C",3);
        using(GraphicsPath glow=new GraphicsPath()) {
            glow.AddEllipse(-33,-33,66,68);
            using(PathGradientBrush light=new PathGradientBrush(glow)) {
                light.CenterColor=Color.FromArgb(185,255,48,66);
                light.SurroundColors=new Color[]{Color.FromArgb(0,255,48,66)};
                g.FillPath(light,glow);
            }
        }
        Paint(g,"M 0 -17 C -27 -48 -49 -14 -27 10 L 0 38 L 27 10 C 49 -14 27 -48 0 -17 Z","FF5C61","8A102E",3);
        Stroke(g,"M -21 -20 C -31 -23 -32 -7 -25 -2 M 2 24 L 17 8","FFABA1",2.5f);
        Stroke(g,"M -4 -13 L 5 -2 L -3 7 L 7 15","BD2242",1.5f);
        g.Restore(st);
    }
    static void Crystal(Graphics g,float x,float y,float s,float angle) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(s,s);
        Paint(g,"M 0 -24 L 14 -7 L 11 15 L -1 28 L -13 9 L -11 -10 Z","D4BAFF","6351A1",2);
        Paint(g,"M 0 -24 L 2 -3 L -1 28 L -13 9 Z","B8F8F6","6F68C6",.9f);
        Paint(g,"M 2 -3 L 14 -7 L 11 15 L -1 28 Z","DF96E1","5855AD",.8f);
        Line(g,0,-20,2,-3,"FFF5FF",1.5f);g.Restore(st);
    }
    static void Star(Graphics g,float x,float y,float r) {
        Paint(g,"M "+F(x)+" "+F(y-r)+" L "+F(x+r*.22f)+" "+F(y-r*.2f)+" L "+F(x+r)+" "+F(y)+" L "+F(x+r*.22f)+" "+F(y+r*.22f)+" L "+F(x)+" "+F(y+r)+" L "+F(x-r*.22f)+" "+F(y+r*.22f)+" L "+F(x-r)+" "+F(y)+" L "+F(x-r*.22f)+" "+F(y-r*.2f)+" Z","F6EDFF","7CD6F5",.7f);
    }
    static void Moss(Graphics g,float x,float y,float w,float h) {
        for(int i=0;i<9;i++) {
            float xx=x+w*i/8f,hh=h*(.45f+(i*7%9)/15f);
            Stroke(g,"M "+F(xx)+" "+F(y)+" C "+F(xx-9)+" "+F(y+hh*.4f)+" "+F(xx+9)+" "+F(y+hh*.6f)+" "+F(xx-2)+" "+F(y+hh),"14382B",8);
            Stroke(g,"M "+F(xx-1)+" "+F(y)+" C "+F(xx-7)+" "+F(y+hh*.4f)+" "+F(xx+6)+" "+F(y+hh*.6f)+" "+F(xx-3)+" "+F(y+hh-3),i%2==0?"4A9961":"287B52",3);
            for(int j=0;j<3;j++)Ellipse(g,xx-5+(j%2)*6,y+j*hh/3,5,4,"73A65D","326646",0);
        }
    }
    static void Straw(Graphics g,float x,float y,float w,float h) {
        for(int i=0;i<13;i++) {
            float dx=(i-6)*w/13f,dy=h*(.6f+(i*3%7)*.065f);
            Line(g,x+dx*.22f,y,x+dx,y+dy,"3F2B1D",3.8f);
            Line(g,x+dx*.22f,y,x+dx,y+dy,i%3==0?"ECD18A":"B9914D",1.8f);
            Line(g,x+dx*.7f,y+dy*.65f,x+dx+3,y+dy*.67f,"F3DDA0",.8f);
        }
    }
    static void Patch(Graphics g,float x,float y,float w,float h) {
        Paint(g,"M "+F(x)+" "+F(y)+" L "+F(x+w)+" "+F(y+3)+" L "+F(x+w-3)+" "+F(y+h)+" L "+F(x+2)+" "+F(y+h-2)+" Z","BEA477","786143",2);
        for(float xx=x+5;xx<x+w-3;xx+=7){Line(g,xx,y-2,xx+1,y+6,"DFC18C",1.3f);Line(g,xx,y+h-5,xx+1,y+h+2,"DFC18C",1.3f);}
        for(float yy=y+8;yy<y+h-5;yy+=6){Line(g,x-2,yy,x+6,yy+1,"D8BE8A",1.2f);Line(g,x+w-6,yy,x+w+2,yy+1,"D8BE8A",1.2f);}
        for(float xx=x+9;xx<x+w-4;xx+=5)Line(g,xx,y+9,xx-2,y+h-8,"90774F",.55f);
    }
    static void Coat(Graphics g,string facing) {
        bool front=facing=="south",side=facing=="east";
        if(set==0) {
            Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z","839082","37423E",0);
            for(int k=0;k<6;k++) {
                int x=k*55-8;
                Paint(g,"M "+x+" 55 L "+(x+52)+" 55 L "+(x+43)+" 347 L "+(x-8)+" 347 Z","737D72","3D4943",2.6f);
                Line(g,x+6,80,x-1,327,"A4AD96",1.5f);
                for(int j=0;j<4;j++) {float yy=100+j*65;Rivet(g,x+40,yy,3.2f);Line(g,x+15,yy+11,x+22,yy+5,"986649",1.6f);}
            }
            Paint(g,"M 60 14 L 104 0 L 196 0 L 242 16 L 224 181 L 150 202 L 76 181 Z","8E988A","49554E",4);
            Paint(g,"M 84 46 L 215 46 L 206 162 L 151 180 L 93 162 Z","6C776E","323D38",2.8f);
            for(int k=0;k<6;k++){Rivet(g,92+k*23,48,3);Rivet(g,101+k*19,161,3);}
            if(front){Ellipse(g,102,66,96,112,"636D62","202B28",4);Ellipse(g,112,76,76,92,"331A1E","10191A",3);Heart(g,150,120,.75f);}
            else {Paint(g,"M 112 62 L 188 62 L 195 144 L 150 166 L 105 144 Z","8B9486","4B5850",3);Line(g,150,70,150,151,"26332F",3);for(int k=0;k<3;k++){Rivet(g,122,80+k*27,3);Rivet(g,178,80+k*27,3);}}
            // Wide brown suspenders, brass buckles, layered iron shoulder shells.
            Stroke(g,"M 64 27 L 96 212 L 106 326 M 236 27 L 204 212 L 194 326","231B18",16);
            Stroke(g,"M 64 27 L 96 212 L 106 326 M 236 27 L 204 212 L 194 326","795139",10);
            Paint(g,"M 1 31 L 55 6 L 81 41 L 68 96 L 8 103 L -9 74 Z","A0A892","4A5650",4);
            Paint(g,"M 245 6 L 299 31 L 309 74 L 292 103 L 232 96 L 219 41 Z","8C9786","39463F",4);
            for(int k=0;k<4;k++){Rivet(g,9+k*16,72,3.2f);Rivet(g,242+k*16,72,3.2f);}
            Moss(g,5,37,58,93);Moss(g,235,28,58,102);
            Moss(g,18,300,52,53);Moss(g,226,312,47,49);
            for(int k=0;k<2;k++) {float x=k==0?87:201;Paint(g,"M "+F(x)+" 181 L "+F(x+14)+" 181 L "+F(x+17)+" 202 L "+F(x+3)+" 202 Z","C4AE70","665437",2);}
            Paint(g,"M 106 -9 L 194 -9 L 185 27 L 115 27 Z","A5AC98","4B574D",3);
        } else if(set==1) {
            Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z","514A86","191B3D",0);
            for(int k=0;k<8;k++){int x=k*47-12;Stroke(g,"M "+x+" 40 C "+(x-20)+" 153 "+(x+22)+" 248 "+(x-5)+" 380","29254D",8);Stroke(g,"M "+(x+8)+" 99 C "+(x-1)+" 199 "+(x+27)+" 267 "+(x+10)+" 355","766298",1.5f);}
            Paint(g,"M 10 25 L 67 3 L 104 42 L 73 100 L 8 88 Z","6C5C9F","302650",3.5f);
            Paint(g,"M 233 3 L 290 25 L 292 88 L 227 100 L 196 42 Z","5A4D8C","252345",3.5f);
            Stroke(g,"M 7 57 L 60 24 L 85 45 M 215 45 L 240 24 L 293 57","B6A16B",4);
            Paint(g,"M 112 -8 L 188 -8 L 207 42 L 151 138 L 93 42 Z","242344","151A31",3);
            if(front) {
                Paint(g,"M 106 2 L 144 26 L 135 70 L 86 45 Z","A78AC7","534277",3);
                Paint(g,"M 194 2 L 156 26 L 165 70 L 214 45 Z","8C78B4","403566",3);
                Stroke(g,"M 106 5 L 143 27 L 136 66 M 194 5 L 157 27 L 164 66","D1BC80",2);
                Crystal(g,150,48,.55f,0);
                Paint(g,"M 146 84 L 155 84 L 166 173 L 150 190 L 134 173 Z","B49BCB","57437C",2);
            } else {
                Paint(g,"M 119 0 L 181 0 L 195 34 L 151 61 L 105 34 Z","79649D","393155",3);
                Stroke(g,"M 82 95 C 111 125 197 125 224 95 M 150 65 L 150 298","B5A170",2);
                Crystal(g,150,114,.7f,0);
            }
            Paint(g,"M 13 250 L 126 240 L 145 286 L 112 361 L 3 350 Z","51457E","232343",3);
            Paint(g,"M 174 240 L 287 250 L 297 350 L 188 361 L 155 286 Z","4E427E","20213F",3);
            Stroke(g,"M 14 337 L 103 347 L 130 288 M 170 288 L 197 347 L 286 337","BDA872",3);
            Stroke(g,"M 28 216 C 98 232 202 232 272 216","191B32",12);
            Stroke(g,"M 28 214 C 98 230 202 230 272 214","A28D60",2);
            for(int k=0;k<29;k++) {
                float x=14+(k*73%271),y=79+(k*47%245);
                if(Math.Abs(x-150)<38&&y<200)continue;
                if(k%3==0)Crystal(g,x,y,.22f+(k%4)*.035f,k*19%70-35);else Star(g,x,y,2.3f+k%3);
            }
            for(int k=0;k<4;k++)Rivet(g,160,146+k*17,2.1f);
        } else {
            Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z","9C4245","49252D",0);
            for(int x=-25;x<340;x+=42){Line(g,x,-25,x,390,"43252E",13);Line(g,x+9,-25,x+9,390,"CF7461",2);}
            for(int y=-5;y<390;y+=43){Line(g,-30,y,330,y,"4B2830",14);Line(g,-30,y+9,330,y+9,"B56755",2);}
            for(int x=0;x<300;x+=42)for(int y=10;y<370;y+=43)Line(g,x,y,x+17,y+17,"A85A4D",.65f);
            Straw(g,63,21,74,-40);Straw(g,236,21,71,-38);Straw(g,56,318,74,47);Straw(g,248,315,69,47);
            Paint(g,"M 109 -12 L 192 -12 L 198 32 L 150 67 L 102 32 Z","BCA476","756043",3);
            Straw(g,101,46,38,-33);Straw(g,199,46,38,-33);
            if(front){Paint(g,"M 114 11 L 144 32 L 134 64 L 102 33 Z","DBC392","9D8055",2);Paint(g,"M 186 11 L 156 32 L 166 64 L 198 33 Z","C4AC7E","8B704C",2);}
            Stroke(g,"M 65 16 L 90 145 L 92 264 M 235 16 L 210 145 L 208 264","201B1B",16);
            Stroke(g,"M 65 16 L 90 145 L 92 264 M 235 16 L 210 145 L 208 264","554037",10);
            Paint(g,"M 91 118 L 209 118 L 211 228 L 88 228 Z","9B4545","602B34",3);
            for(int x=100;x<210;x+=26)Line(g,x,124,x,222,"4B2830",6);
            for(int y=134;y<225;y+=26)Line(g,94,y,206,y,"4B2830",6);
            Stroke(g,"M 97 126 L 203 126 M 97 218 L 203 218","CE9B70",1.5f);
            if(front){Patch(g,116,161,66,43);Rivet(g,99,128,4);Rivet(g,201,128,4);}
            else {Stroke(g,"M 79 65 L 216 239 M 221 65 L 84 239","2A2121",13);Stroke(g,"M 79 65 L 216 239 M 221 65 L 84 239","73513B",7);Patch(g,118,91,66,51);}
            Patch(g,22,213,47,53);Patch(g,228,264,45,55);Patch(g,106,291,60,50);
            Stroke(g,"M 10 345 C 89 361 211 361 291 345","362625",7);
            Stroke(g,"M 14 344 C 89 358 211 358 287 344","D2AC72",2);
            for(int k=0;k<16;k++)Line(g,20+k*17,343+(k%3),22+k*17,353+(k%3),"D6B57F",1.5f);
        }
        // Directional tailoring remains visible after clipping to the exact vanilla silhouettes.
        if(side) {
            Stroke(g,"M 197 55 C 178 116 187 207 203 299",set==1?"B5A16D":set==0?"263A30":"39242A",4);
            if(set==0)Moss(g,206,80,40,72);
            if(set==1)Crystal(g,229,166,.6f,17);
            if(set==2)Patch(g,192,159,51,65);
        }
    }
    static Rectangle WeaponTarget(){return set==0?Rectangle.FromLTRB(91,64,408,427):set==1?Rectangle.FromLTRB(95,105,408,423):Rectangle.FromLTRB(99,68,433,417);}
    static Rectangle AccessoryTarget(bool item,string face) {
        if(item)return set==0?Rectangle.FromLTRB(86,102,426,411):set==1?Rectangle.FromLTRB(86,93,426,420):Rectangle.FromLTRB(86,157,426,355);
        if(set==2)return face=="east"?Rectangle.FromLTRB(248,286,294,316):Rectangle.FromLTRB(228,286,284,320);
        return face=="east"?Rectangle.FromLTRB(256,300,294,338):Rectangle.FromLTRB(236,300,278,342);
    }
    // Normalize only the finished silhouette, preserving the diagonal shaft and head pose.
    static Bitmap Fit(Bitmap hi,Rectangle target) {
        Rectangle src=Bounds(hi,0);using(Bitmap outHi=Canvas(512*S)) {
            using(Graphics g=G(outHi))using(ImageAttributes ia=new ImageAttributes()) {
                ia.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(hi,new Rectangle(target.X*S,target.Y*S,target.Width*S,target.Height*S),src.X,src.Y,src.Width,src.Height,GraphicsUnit.Pixel,ia);
            }
            Bitmap result=Resolve(outHi,512);
            // Resampling may spread alpha by one pixel. Keep the requested registrations exact.
            byte[] pixels=Read(result);
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)if(!target.Contains(x,y)) {
                int i=(y*512+x)*4;pixels[i]=pixels[i+1]=pixels[i+2]=pixels[i+3]=0;
            }
            Write(result,pixels);return result;
        }
    }
    static void WeaponDrawing(Graphics g) {
        // Local vertical instrument, rotated +45 degrees: butt lower left, blade upper right.
        GraphicsState st=g.Save();g.TranslateTransform(256,256);g.RotateTransform(45);g.TranslateTransform(-256,-256);
        if(set==0) {
            Paint(g,"M 244 140 L 266 140 L 269 442 L 248 450 L 240 438 Z","8B7450","423E30",4);
            Line(g,251,196,255,427,"B4A273",2);
            Paint(g,"M 239 338 L 271 338 L 272 423 L 242 429 Z","594032","2C2422",3);
            for(int k=0;k<11;k++)Line(g,242,343+k*7,270,350+k*7,"9B7550",2);
            Paint(g,"M 240 425 L 274 420 L 274 442 L 253 452 L 241 442 Z","9B9F89","45534B",3);
            Paint(g,"M 245 77 C 211 49 172 61 138 46 C 152 93 147 159 127 204 C 171 192 208 209 248 177 L 275 176 L 275 88 Z","8D998C","4C5851",4.5f);
            Paint(g,"M 138 46 C 152 93 147 159 127 204 L 151 187 C 168 145 165 100 158 61 Z","CAD2BD","747D6D",2.5f);
            Paint(g,"M 178 77 L 231 87 L 238 165 L 177 180 L 162 140 Z","986F50","46554A",2);
            for(int k=0;k<5;k++) {Rivet(g,174+k*13,81+k*3,3.8f);Rivet(g,169+k*15,174-k*3,3.8f);}
            Paint(g,"M 241 73 L 275 76 L 285 183 L 247 190 Z","A8AE99","4A574E",3);
            Line(g,250,79,260,177,"D2CDB0",2);
            Heart(g,204,128,.72f);
            Moss(g,150,57,77,40);Moss(g,226,165,37,48);
            for(int k=0;k<7;k++)Line(g,157+k*3,110+k*9,169+k*3,104+k*9,"AA7A58",1.1f);
        } else if(set==1) {
            Paint(g,"M 250 157 L 263 157 L 266 442 L 255 455 L 245 442 Z","566B9B","29224B",3.5f);
            Line(g,255,195,257,431,"A9E7EB",2);
            Paint(g,"M 244 327 L 267 327 L 269 411 L 245 411 Z","514572","241E42",3);
            for(int k=0;k<10;k++)Line(g,246,334+k*7,267,340+k*7,"A299BD",1.5f);
            Crystal(g,256,443,.4f,0);
            Paint(g,"M 256 46 L 282 104 L 273 166 L 256 201 L 234 168 L 227 104 Z","B9ECF3","4C499B",3.5f);
            Paint(g,"M 256 46 L 254 122 L 234 168 L 227 104 Z","E7C9F8","8A69B2",1.5f);
            Paint(g,"M 256 46 L 282 104 L 254 122 Z","C7FFFC","78A6D4",1.5f);
            Paint(g,"M 254 122 L 273 166 L 256 201 Z","C893DA","57469B",1.5f);
            Line(g,255,66,254,177,"F4F8FF",2);
            Paint(g,"M 225 169 L 256 180 L 286 169 L 281 185 L 256 197 L 231 185 Z","C8B47C","746089",2.5f);
            Crystal(g,212,95,.65f,-26);Crystal(g,297,124,.55f,29);Crystal(g,289,57,.35f,22);Crystal(g,226,39,.3f,-22);
            Star(g,304,86,7);Star(g,205,141,5);Star(g,268,23,5);
            Stroke(g,"M 220 166 C 193 128 212 67 242 51 M 279 161 C 311 131 309 82 291 67","A4DCE9",1.5f);
        } else {
            Paint(g,"M 246 153 L 265 153 L 273 441 L 256 450 L 248 440 Z","A38356","4D3B2C",4);
            Stroke(g,"M 252 192 C 260 268 252 334 262 431","D1AD72",2);
            Stroke(g,"M 260 216 C 253 274 267 320 261 368","6A4C32",1.3f);
            Paint(g,"M 244 126 L 270 126 L 278 205 L 242 205 Z","756B51","403D33",3);
            Paint(g,"M 168 126 L 342 126 L 347 163 L 166 163 Z","9A8D6F","4F5448",4);
            Line(g,174,156,339,156,"D0C2A0",3);
            for(int k=0;k<4;k++) {
                int x=173+k*49;
                Paint(g,"M "+x+" 131 C "+(x-12)+" 94 "+(x-5)+" 54 "+(x+15)+" 28 C "+(x+9)+" 76 "+(x+16)+" 100 "+(x+18)+" 131 Z","9DA08B","51594E",3.5f);
                Stroke(g,"M "+(x+3)+" 115 C "+(x-3)+" 83 "+(x+4)+" 58 "+(x+12)+" 41","E2D9B4",1.8f);
                Rivet(g,x+7,143,4);
            }
            Straw(g,254,162,64,76);
            for(int k=0;k<12;k++){Line(g,237,173+k*4,278,180+k*4,"30271E",4);Line(g,237,172+k*4,278,179+k*4,k%2==0?"D0B379":"A58B58",2);}
            for(int k=0;k<5;k++)Line(g,177+k*32,135,190+k*32,146,"A76B49",2);
            for(int k=0;k<6;k++)Line(g,249,351+k*7,269,356+k*7,"B99762",3);
        }
        g.Restore(st);
    }
    static Bitmap Weapon() {
        using(Bitmap hi=Canvas(512*S)) {using(Graphics g=G(hi)){g.ScaleTransform(S,S);WeaponDrawing(g);}return Fit(hi,WeaponTarget());}
    }
    static Bitmap Accessory(bool item,string face) {
        if(face=="north")return Canvas(512);
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                if(set==0) {
                    Paint(g,"M 165 179 L 256 145 L 347 179 L 335 299 L 256 360 L 177 299 Z","9AA592","3B4B43",6);
                    Paint(g,"M 190 196 L 256 170 L 322 196 L 312 286 L 256 333 L 200 286 Z","596B5A","25372D",4);
                    Heart(g,256,247,1.65f);
                    Rivet(g,182,192,8);Rivet(g,330,192,8);Rivet(g,197,291,7);Rivet(g,315,291,7);Rivet(g,256,343,7);
                } else if(set==1) {
                    Crystal(g,252,245,3.4f,5);Crystal(g,196,202,.62f,-23);Crystal(g,318,276,.55f,28);Star(g,313,176,13);Star(g,205,316,10);
                    Stroke(g,"M 197 285 C 159 217 224 161 286 179 M 311 203 C 348 259 287 324 232 313","C3A4E4",2.8f);
                } else {
                    Straw(g,249,273,214,-109);Straw(g,282,267,133,-78);
                    Paint(g,"M 127 255 C 203 232 302 232 382 255 L 373 291 C 297 273 205 273 139 295 Z","C2A06A","78603F",5);
                    Stroke(g,"M 140 264 C 218 243 301 246 369 263 M 145 283 C 224 264 300 267 368 281","E2C48B",3);
                    for(int k=0;k<20;k++)Line(g,145+k*11,256+(k%2)*4,149+k*11,283+(k%2)*3,"4A3526",2.3f);
                    Stroke(g,"M 262 263 C 214 226 215 295 261 267 C 302 230 312 298 263 269 L 246 312 M 263 271 L 291 310","463223",8);
                    Stroke(g,"M 262 263 C 214 226 215 295 261 267 C 302 230 312 298 263 269 L 246 312 M 263 271 L 291 310","E4C48B",3);
                }
            }
            return Fit(hi,AccessoryTarget(item,face));
        }
    }
    static void Frame(Graphics g) {
        Paint(g,"M 17 5 L 111 5 L 123 17 L 123 111 L 111 123 L 17 123 L 5 111 L 5 17 Z",set==1?"302A50":"38252B","100F1A",3);
        Stroke(g,"M 19 8 L 109 8 L 120 19 L 120 109 L 109 120 L 19 120 L 8 109 L 8 19 Z",set==1?"B19BCB":"BEA16A",2);
        Stroke(g,"M 22 13 L 106 13 L 115 22 L 115 106 L 106 115 L 22 115 L 13 106 L 13 22 Z",set==0?"365D48":set==1?"547789":"753E3E",1);
        for(int k=0;k<4;k++)Rivet(g,k%2==0?17:111,k<2?17:111,2);
    }
    static Bitmap Icon(Bitmap art,bool armor) {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {g.ScaleTransform(S,S);Frame(g);Rectangle src=Bounds(art,0);float s=Math.Min(88f/src.Width,94f/src.Height);int w=(int)(src.Width*s),h=(int)(src.Height*s);g.DrawImage(art,new Rectangle(64-w/2,65-h/2,w,h),src,GraphicsUnit.Pixel);}
            return Resolve(hi,128);
        }
    }
    static Bitmap Ability() {
        using(Bitmap hi=Canvas(128*S))using(Bitmap weapon=Weapon()) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);Frame(g);
                Ellipse(g,19,19,90,90,set==1?"655889":"772D39",set==1?"20223E":"21141D",0);
                if(set==0) {
                    Stroke(g,"M 20 88 C 35 110 102 99 109 39","411E2A",14);
                    Stroke(g,"M 20 88 C 49 114 107 87 109 39","D5454C",7);
                    Stroke(g,"M 25 91 C 61 105 102 81 108 44","FFAF8B",2);
                    Moss(g,21,20,23,33);
                } else if(set==1) {
                    for(int k=0;k<10;k++) {double a=k*Math.PI/5;float x=64+(float)Math.Cos(a)*42,y=64+(float)Math.Sin(a)*42;Line(g,64,64,x,y,k%2==0?"9CDAED":"CE89CF",1.6f);if(k%2==0)Crystal(g,x,y,.24f,k*36);else Star(g,x,y,4);}
                } else {
                    Straw(g,29,87,27,-39);Straw(g,98,88,29,-48);
                    Stroke(g,"M 22 36 C 14 76 32 99 58 101 M 109 31 C 116 75 96 103 67 104","AC7546",4);
                    Stroke(g,"M 25 40 C 24 76 39 89 58 92 M 105 38 C 103 72 91 91 70 94","E3BB70",2);
                }
                Rectangle src=Bounds(weapon,0);g.DrawImage(weapon,new Rectangle(27,23,75,82),src,GraphicsUnit.Pixel);
                if(set==0)Heart(g,62,78,.36f);
                if(set==1)Star(g,64,61,12);
                if(set==2){Stroke(g,"M 54 70 L 62 77 L 58 86 M 76 70 L 67 78 L 72 88","EEC88B",2);}
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
            Rectangle box=Bounds(b,0);bool blank=name==names[set]+"Accessory_north";
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
            string dir=Path.Combine(root,"Cont/Textures/Things/Pawn/Humanlike/Apparel/"+names[i]+"Suit");
            HashSet<string> exact=new HashSet<string>(StringComparer.Ordinal);
            foreach(string f in Directory.GetFiles(dir))exact.Add(Path.GetFileName(f));
            foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})
                if(!exact.Contains(names[i]+"Suit_"+body+"_"+face+".png"))throw new Exception("Missing lowercase direction filename");        }
        Console.WriteLine("SUCCESS: all 72 PNGs passed format, alpha, hidden RGB, sealed-mask, inward-ink, bbox, head registration and filename checks.");
    }
    static void Review() {
        string[] labels={"Female S","Female N","Female E","Male S","Weapon","Accessory","Armor Icon","Weapon Icon","Skill Icon"};
        using(Bitmap sheet=CanvasRect(2160,900))using(Graphics g=G(sheet))using(Font font=new Font(FontFamily.GenericSansSerif,12,FontStyle.Bold))using(Brush text=new SolidBrush(C("F4E7D2"))) {
            g.Clear(C("181922"));
            for(int row=0;row<3;row++) {
                string n=names[row];int top=row*300;g.DrawString(n+(row==1?" / TETH":" / HE"),font,text,12,top+5);
                string apparel="Things/Pawn/Humanlike/Apparel/"+n+"Suit/"+n+"Suit";
                string[] paths={apparel+"_Female_south.png",apparel+"_Female_north.png",apparel+"_Female_east.png",apparel+"_Male_south.png","Things/Item/Equipment/WeaponMelee/EgoWeapon_"+n+".png","Things/Pawn/Humanlike/Apparel/Accessory/"+n+"Accessory.png","UI/Commands/ExtractEGO_"+n+"_Armor.png","UI/Commands/ExtractEGO_"+n+"_Weapon.png","UI/WeaponSkills/"+n+"/"+skills[row]+".png"};
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
            string output=Path.Combine(Path.GetTempPath(),"Vanilla-GroupF-review.png");sheet.Save(output,ImageFormat.Png);Console.WriteLine("REVIEW "+output);
        }
    }
    static Bitmap CanvasRect(int w,int h){return new Bitmap(w,h,PixelFormat.Format32bppArgb);}
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();masks.Clear();
        for(set=0;set<3;set++) {
            string n=names[set];
            using(Bitmap weapon=Weapon()) {Save(weapon,"Things/Item/Equipment/WeaponMelee/EgoWeapon_"+n+".png",512);using(Bitmap icon=Icon(weapon,false))Save(icon,"UI/Commands/ExtractEGO_"+n+"_Weapon.png",128);}
            foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})
                using(Bitmap b=Worn(body,face))Save(b,"Things/Pawn/Humanlike/Apparel/"+n+"Suit/"+n+"Suit_"+body+"_"+face+".png",512);
            using(Bitmap suit=SuitItem()) {Save(suit,"Things/Pawn/Humanlike/Apparel/"+n+"Suit/"+n+"Suit.png",512);using(Bitmap icon=Icon(suit,true))Save(icon,"UI/Commands/ExtractEGO_"+n+"_Armor.png",128);}
            foreach(string face in new string[]{"item","south","north","east"})using(Bitmap b=Accessory(face=="item",face))Save(b,"Things/Pawn/Humanlike/Apparel/Accessory/"+n+"Accessory"+(face=="item"?"":"_"+face)+".png",512);
            using(Bitmap icon=Ability())Save(icon,"UI/WeaponSkills/"+n+"/"+skills[set]+".png",128);
        }
        Verify();Review();
    }

}
'@
if (-not ('GroupFArt' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[GroupFArt]::Run($ModRoot)

