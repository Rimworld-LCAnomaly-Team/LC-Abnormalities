#requires -Version 5.1
# Deterministic Group A painter: C# GDI+, 3x supersampling, mask-only inputs.

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

public static class GroupAArt {
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
    static Rectangle Bounds(Bitmap b,int cutoff) {
        byte[] a=Read(b);int l=b.Width,t=b.Height,r=-1,bot=-1;
        for(int k=0;k<a.Length/4;k++)if(a[k*4+3]>cutoff){int x=k%b.Width,y=k/b.Width;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);bot=Math.Max(bot,y);}
        return r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,bot+1);
    }
    static readonly string[] Sets={"BurrowingHeaven","FairyFestival","WellCheers"};
    static readonly string[] Skills={"Heaven_RootImpale","Wingbeat_FairyFeast","Soda_RefreshingSplash"};
    static int theme;
    static string Set {get{return Sets[theme];}}
    static string Light {get{return theme==0?"DF5460":theme==1?"D6FFE9":"70D9F3";}}
    static string Dark {get{return theme==0?"460F25":theme==1?"16826B":"13304F";}}
    static string Accent {get{return theme==0?"FFC85A":theme==1?"E6C879":"EF485C";}}
    static string F(float v){return v.ToString("0.###",CultureInfo.InvariantCulture);}
    static void Vine(Graphics g,float x,float y,float length,float angle) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);
        Stroke(g,"M 0 0 C 18 -18 -18 -38 0 -60 C 19 -81 -12 -102 0 -120","140A0C",9);
        Stroke(g,"M 0 0 C 18 -18 -18 -38 0 -60 C 19 -81 -12 -102 0 -120","9E233F",5);
        for(int i=0;i<5;i++) {float yy=-12-i*23;int s=i%2==0?1:-1;
            Paint(g,"M 0 "+F(yy)+" L "+F(s*23)+" "+F(yy-18)+" L "+F(s*6)+" "+F(yy-1)+" Z","CC4250","580E27",1.5f);}
        g.Restore(st);
    }
    static void AmberEye(Graphics g,float x,float y,float scale) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        Paint(g,"M -48 0 C -22 -32 21 -34 49 0 C 20 29 -21 30 -48 0 Z","B73845","4A1226",3.5f);
        Paint(g,"M -39 0 C -15 -21 15 -22 40 0 C 15 19 -14 20 -39 0 Z","FFE19A","BA6C26",2);
        Ellipse(g,-15,-19,31,38,"FFE66C","C17B20",2);
        Paint(g,"M 0 -17 C 8 -4 7 7 0 18 C -6 5 -6 -6 0 -17 Z","27101A","140A0C",1);
        Ellipse(g,-9,-12,7,8,"FFFFFF","FFF6CE",0);g.Restore(st);
    }
    // Each wing has a scalloped trailing edge, gold veins and a pale leading rim.
    static void Wing(Graphics g,float x,float y,float scale,float angle,bool flip) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);g.ScaleTransform(flip?-scale:scale,scale);
        Paint(g,"M 0 0 C 10 -48 32 -92 84 -110 C 92 -69 79 -45 63 -33 C 93 -43 109 -30 100 -9 C 83 25 41 39 0 0 Z","D2FFE9","149C7C",3);
        Paint(g,"M 4 -3 C 36 -46 59 -76 79 -100 C 65 -58 54 -24 4 -3 Z","F0FFF0","62D7AB",1);
        Stroke(g,"M 0 0 C 33 -18 59 -62 84 -110 M 8 2 C 37 -2 75 -12 100 -9 M 25 -28 L 42 -65 M 42 -45 L 69 -50 M 30 2 L 60 16 M 59 -5 L 82 2","D9C17B",1.9f);
        Stroke(g,"M 5 -17 C 21 -60 45 -87 72 -99","F1FFF2",2);
        g.Restore(st);
    }
    static void Spark(Graphics g,float x,float y,float r) {
        Paint(g,"M "+F(x)+" "+F(y-r)+" L "+F(x+r*.22f)+" "+F(y-r*.22f)+" L "+F(x+r)+" "+F(y)+" L "+F(x+r*.22f)+" "+F(y+r*.22f)+" L "+F(x)+" "+F(y+r)+" L "+F(x-r*.22f)+" "+F(y+r*.22f)+" L "+F(x-r)+" "+F(y)+" L "+F(x-r*.22f)+" "+F(y-r*.22f)+" Z","FFFADF","85DCC9",.8f);
    }
    static void Anchor(Graphics g,float x,float y,float s) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
        Ellipse(g,-6,-29,12,12,"F6EACB","C59D59",2);
        Stroke(g,"M 0 -17 L 0 28 M -14 -8 L 14 -8 M -25 5 C -25 31 25 39 25 5 M -25 5 L -30 16 M -25 5 L -15 12 M 25 5 L 30 16 M 25 5 L 15 12","E9DFB7",4);g.Restore(st);
    }
    static void Coat(Graphics g,string facing) {
        bool north=facing=="north",east=facing=="east";
        Paint(g,"M -40 -30 L 340 -30 L 340 390 L -40 390 Z",theme==0?"842A40":theme==1?"D1F5DD":"285C85",Dark,0);
        // Structured side panels, cuffs, waist and tailored skirt seams.
        Paint(g,"M -6 18 L 55 3 L 89 48 L 69 135 L 16 153 L -12 96 Z",Light,Dark,3);
        Paint(g,"M 245 3 L 307 18 L 312 96 L 284 153 L 231 135 L 211 48 Z",Light,Dark,3);
        Paint(g,"M 89 71 L 150 92 L 212 71 L 202 220 L 150 244 L 98 220 Z",theme==0?"772237":theme==1?"AFEBCC":"24496C",Dark,2.8f);
        Stroke(g,"M 71 145 C 87 219 62 296 42 356 M 229 145 C 213 219 238 296 258 356","140A0C",3);
        Stroke(g,"M 82 244 L 65 339 M 218 244 L 235 339",Light,2);
        Paint(g,"M 38 319 C 105 345 199 345 264 319 L 277 359 C 207 382 90 382 25 359 Z",Light,Dark,3);
        Stroke(g,"M 38 350 C 112 373 195 373 264 350",Accent,2.5f);
        if(theme==0) {
            for(int i=0;i<7;i++) {float x=24+i*42;
                Stroke(g,"M "+F(x)+" 84 C "+F(x+19)+" 152 "+F(x-16)+" 235 "+F(x+9)+" 325","300F20",5);
                Stroke(g,"M "+F(x+4)+" 96 C "+F(x+23)+" 174 "+F(x-9)+" 244 "+F(x+14)+" 310","B94C55",1.8f);}
            for(int i=0;i<4;i++) {int y=87+i*28;
                Paint(g,"M 83 "+y+" L 139 "+(y+16)+" L 139 "+(y+28)+" L 91 "+(y+15)+" Z","C24A55","591329",2);
                Paint(g,"M 217 "+y+" L 161 "+(y+16)+" L 161 "+(y+28)+" L 209 "+(y+15)+" Z","A43749","410F23",2);}
            if(north){for(int i=0;i<7;i++)Vine(g,150,122,120,-110+i*36);AmberEye(g,150,106,.77f);}
            else if(east){Vine(g,111,120,120,-22);Vine(g,178,107,120,36);AmberEye(g,151,114,.45f);}
            else {Vine(g,47,122,120,-22);Vine(g,253,122,120,22);AmberEye(g,150,132,.62f);}
        } else if(theme==1) {
            if(north){Wing(g,150,100,1.26f,-15,true);Wing(g,150,100,1.26f,15,false);Wing(g,148,251,.88f,148,false);Wing(g,152,251,.88f,-148,true);}
            else if(east){Wing(g,144,114,.85f,12,false);Wing(g,173,260,.82f,155,false);}
            else {Wing(g,147,88,.8f,-13,true);Wing(g,153,88,.8f,13,false);Wing(g,80,269,.58f,154,false);Wing(g,220,269,.58f,-154,true);}
            Stroke(g,"M 150 140 L 150 315","609B82",2);
            for(int i=0;i<4;i++)Ellipse(g,146,168+i*29,8,8,"FFF0AD","A37F3D",1);
        } else {
            // Nautical yoke: broad pale collar and parallel navy/white braid.
            Paint(g,north?"M 68 -9 L 231 -9 L 234 97 L 67 97 Z":east?"M 165 -12 L 252 7 L 234 99 L 174 65 L 129 15 Z":"M 66 -9 L 234 -9 L 217 54 L 150 113 L 83 54 Z","F7F6E6","B6D4DC",3);
            Stroke(g,north?"M 78 0 L 78 86 L 222 86 L 222 0":east?"M 178 -2 L 241 13 L 224 84 L 169 51":"M 78 1 L 92 47 L 150 99 L 208 47 L 222 1","214767",3);
            Stroke(g,north?"M 87 0 L 87 76 L 212 76 L 212 0":east?"M 187 1 L 231 20 L 215 69":"M 88 0 L 101 42 L 150 86 L 199 42 L 212 0","214767",2);
            if(!north){Paint(g,east?"M 217 52 L 235 63 L 216 109 L 241 150 L 213 140 L 201 106 Z":"M 135 67 L 164 67 L 169 88 L 153 103 L 133 87 Z","FF6671","B51F40",2.5f);
                if(!east)Paint(g,"M 144 93 L 158 94 L 176 153 L 151 142 L 128 156 Z","ED4A61","9C1636",2.5f);}
            Anchor(g,east?149:103,north?146:145,north?.9f:.5f);
            Paint(g,"M 30 251 L 112 251 L 108 299 L 37 299 Z","2B668F","163650",2);
            Paint(g,"M 188 251 L 270 251 L 263 299 L 192 299 Z","2B668F","163650",2);
            Stroke(g,"M 35 259 L 109 259 M 191 259 L 265 259","C1EBEE",2);
            for(int i=0;i<3;i++)Stroke(g,"M 30 "+(330+i*9)+" C 97 "+(351+i*9)+" 206 "+(351+i*9)+" 270 "+(330+i*9),i==1?"F4F5E7":"77C9DF",2);
        }
        if(theme!=2) {
            Paint(g,north?"M 105 -13 L 195 -13 L 190 31 L 110 31 Z":east?"M 174 -10 L 245 -2 L 239 42 L 209 57 L 178 26 Z":"M 110 -13 L 190 -13 L 198 31 L 150 66 L 102 31 Z",theme==0?"29232D":"FBF8E8",theme==0?"100E19":"AED9C2",3);
            if(!north&&!east){
                Paint(g,"M 117 0 L 148 19 L 136 44 L 107 22 Z",theme==0?"48414A":"FFFFFF",Dark,2);
                Paint(g,"M 183 0 L 152 19 L 164 44 L 193 22 Z",theme==0?"48414A":"FFFFFF",Dark,2);
                if(theme==0){Paint(g,"M 144 17 L 156 17 L 161 30 L 150 42 L 139 30 Z","FFFFFF","D8D1CD",1.5f);Paint(g,"M 146 40 L 154 40 L 166 82 L 150 97 L 135 82 Z","FFFFFF","D8D1CD",2);}
                else {Paint(g,"M 150 34 C 124 6 102 21 119 47 L 147 45 C 169 62 191 38 178 21 C 169 20 161 28 150 34 Z","FFE9E9","C89DC5",2);Ellipse(g,143,31,15,15,"FFF5D4","D1A0B5",1.5f);}
            }
        }
        Paint(g,"M 46 219 C 112 232 189 232 254 219 L 257 236 C 184 252 111 252 43 236 Z",Dark,"161722",2);
        Paint(g,"M 138 226 L 163 226 L 163 245 L 138 245 Z",Accent,"82683F",1.5f);
    }
    static Bitmap Worn(string body,string facing) {
        string key=body+"_"+facing;
        if(!masks.ContainsKey(key))masks[key]=new BodyMask(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+key+".png"));
        BodyMask mask=masks[key];
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);g.TranslateTransform(mask.Bounds.X,mask.Bounds.Y);g.ScaleTransform(mask.Bounds.Width/300f,mask.Bounds.Height/360f);Coat(g,facing);}
            Bitmap b=Resolve(hi,512);mask.Clip(b);return b;
        }
    }
    static Bitmap SuitItem() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);
                using(GraphicsPath p=P("M 212 62 L 250 80 L 300 62 L 346 85 L 388 108 L 424 253 L 402 289 L 365 273 L 387 434 C 307 464 203 464 125 434 L 146 273 L 110 289 L 88 253 L 124 108 L 166 85 Z")) {
                    GraphicsState st=g.Save();g.SetClip(p);g.TranslateTransform(88,61);g.ScaleTransform(336f/300,390f/360);Coat(g,"south");g.Restore(st);
                    using(Pen pen=new Pen(Ink,3.8f)){pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);}
                }
                Stroke(g,"M 142 128 L 153 205 L 146 273 M 370 128 L 359 205 L 365 273",Dark,3);
            }return Resolve(hi,512);
        }
    }
    static Bitmap Fit(Bitmap src,Rectangle target,int n) {
        using(Bitmap hi=Canvas(n*S)) {
            Rectangle box=Bounds(src,0);
            using(Graphics g=G(hi))using(ImageAttributes ia=new ImageAttributes()) {ia.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(src,new Rectangle(target.X*S,target.Y*S,target.Width*S,target.Height*S),box.X,box.Y,box.Width,box.Height,GraphicsUnit.Pixel,ia);}
            return Resolve(hi,n);
        }
    }
    static Rectangle WeaponTarget() {return theme==0?Rectangle.FromLTRB(18,204,498,308):theme==1?Rectangle.FromLTRB(20,128,490,378):Rectangle.FromLTRB(30,94,482,418);}
    static Bitmap Weapon() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);
                if(theme==0){
                    Paint(g,"M 16 243 L 333 236 L 349 261 L 324 279 L 16 273 Z","493140","1D1523",4);
                    for(int i=0;i<15;i++){int x=24+i*18;Stroke(g,"M "+x+" 244 L "+(x+11)+" 271","9A3449",3);Stroke(g,"M "+x+" 247 L "+(x+10)+" 274","D06463",1);}
                    Paint(g,"M 289 238 L 321 207 L 334 226 L 371 201 L 386 221 L 426 209 L 495 251 L 436 280 L 406 277 L 374 302 L 360 282 L 329 302 L 314 279 L 289 276 Z","A83246","341024",4);
                    Stroke(g,"M 327 242 L 371 220 L 405 236 L 474 250 M 335 276 L 370 261 L 407 269 L 460 257","DD5A5E",2);
                    AmberEye(g,389,250,.59f);AmberEye(g,329,258,.28f);
                    for(int i=0;i<7;i++) {int x=53+i*35;Stroke(g,"M "+x+" 274 C "+(x+24)+" 256 "+(x-2)+" 239 "+(x+23)+" 233","751B36",6);Paint(g,"M "+(x+12)+" 239 L "+(x+29)+" 222 L "+(x+24)+" 244 Z","C14352","4A142A",1.5f);}
                } else if(theme==1){
                    Paint(g,"M 20 246 L 198 246 L 212 270 L 192 285 L 20 281 Z","E9D8A2","756148",4);
                    Paint(g,"M 37 250 L 164 250 L 164 278 L 37 278 Z","75CEAC","267961",3);
                    for(int i=0;i<9;i++)Stroke(g,"M "+(40+i*13)+" 252 L "+(51+i*13)+" 276","DCF8CE",2);
                    Paint(g,"M 183 210 L 206 210 L 219 302 L 194 307 Z","F9E8AC","AE8A45",3);
                    Paint(g,"M 212 242 C 277 140 390 114 481 137 C 487 213 464 307 409 367 C 362 392 279 363 224 288 Z","CFFFF1","159371",4);
                    Wing(g,226,279,2.37f,21,false);
                    Stroke(g,"M 233 289 C 326 359 410 358 462 255","E2FFE6",5);
                    Spark(g,420,166,13);Spark(g,337,207,10);
                } else {
                    Paint(g,"M 115 244 L 219 252 L 200 410 L 118 398 L 92 374 Z","3D7C9F","163049",4);
                    Paint(g,"M 126 281 L 192 292 L 178 392 L 128 382 Z","E84D62","8A1938",3);
                    for(int i=0;i<5;i++)Stroke(g,"M 133 "+(303+i*15)+" L 184 "+(311+i*15),"F88A87",2);
                    Paint(g,"M 198 259 C 253 263 273 316 220 330 L 200 328 L 202 310 C 231 318 240 285 205 280 Z","B7E7E8","477C94",3);
                    Paint(g,"M 45 149 L 92 113 L 325 113 L 360 145 L 398 149 L 398 244 L 354 252 L 319 285 L 89 285 L 45 253 Z","5ED5E9","1A6898",4);
                    Paint(g,"M 111 122 L 298 122 L 298 276 L 111 276 Z","36ADC9","19618A",3);
                    Paint(g,"M 162 125 L 203 125 L 203 275 L 162 275 Z","F7606E","BA2748",2);
                    Ellipse(g,48,122,63,150,"E5F3E6","538BA2",3);
                    Paint(g,"M 314 147 L 414 147 L 414 245 L 314 245 Z","F1F4DD","608697",3);
                    Paint(g,"M 365 161 L 463 161 L 477 177 L 477 221 L 462 235 L 365 235 Z","DBF1E9","668B9D",4);
                    Ellipse(g,444,169,27,59,"4C7083","172637",3);
                    Stroke(g,"M 326 156 L 326 237 M 341 156 L 341 237 M 376 173 L 434 173","FFFFFF",3);
                    Ellipse(g,244,91,68,68,"DCEBE0","748F9E",3);Ellipse(g,252,99,52,52,"FFF9DB","B6DCD8",2);
                    for(int i=0;i<7;i++){double a=(i*25+180)*Math.PI/180;using(Pen p=new Pen(Ink,2))g.DrawLine(p,278+(float)Math.Cos(a)*18,125+(float)Math.Sin(a)*18,278+(float)Math.Cos(a)*23,125+(float)Math.Sin(a)*23);}
                    Stroke(g,"M 278 125 L 293 111","C63750",3);Ellipse(g,274,121,8,8,"E74954","B92643",1);
                    Anchor(g,232,209,.7f);Stroke(g,"M 69 141 L 69 248 M 120 140 L 150 140","F3FEED",3);
                }
            }using(Bitmap raw=Resolve(hi,512))return Fit(raw,WeaponTarget(),512);
        }
    }
    static Rectangle AccessoryTarget(string face) {
        if(theme==0)return face=="item"?Rectangle.FromLTRB(94,151,418,351):face=="south"?Rectangle.FromLTRB(167,129,355,245):face=="east"?Rectangle.FromLTRB(175,135,347,237):Rectangle.FromLTRB(168,130,352,241);
        if(theme==1)return face=="item"?Rectangle.FromLTRB(77,74,434,436):Rectangle.FromLTRB(177,326,226,377);
        return face=="item"?Rectangle.FromLTRB(72,147,439,365):face=="south"?Rectangle.FromLTRB(236,257,277,276):Rectangle.FromLTRB(269,252,304,271);
    }
    static Bitmap Accessory(string face) {
        if(face=="north"&&theme!=0)return Canvas(512);
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);
                if(theme==0){
                    for(int side=-1;side<=1;side+=2){GraphicsState st=g.Save();g.TranslateTransform(250,300);g.ScaleTransform(side,1);
                        Paint(g,"M 0 0 C 45 -60 82 -33 143 -111 L 127 -42 L 162 -60 L 137 -17 L 174 -22 L 144 18 L 97 34 L 44 20 Z","B5374B","4A1029",3);
                        Stroke(g,"M 19 0 C 69 -10 109 -31 143 -111","ED6D6B",2);
                        AmberEye(g,92,-8,.57f);g.Restore(st);}
                    for(int i=0;i<5;i++)Vine(g,170+i*40,298,120,-35+i*17);
                    Stroke(g,"M 156 299 C 195 323 300 324 346 296","140A0C",13);Stroke(g,"M 156 297 C 195 320 300 321 346 294","A32E49",7);
                    if(face!="north")AmberEye(g,250,294,.62f);
                } else if(theme==1){Wing(g,210,309,1.6f,-38,true);Wing(g,219,303,1.7f,23,false);Wing(g,215,302,.95f,150,false);Ellipse(g,204,285,25,48,"F8E8B4","9B8550",2);Spark(g,231,254,13);}
                else {
                    Paint(g,"M 99 246 L 114 211 L 195 211 L 209 246 L 207 314 C 181 334 126 334 102 314 Z","8FE6EA","1C87B3",4);
                    Paint(g,"M 109 256 L 203 256 L 203 300 L 109 300 Z","FA6976","B42A4A",3);
                    Paint(g,"M 114 209 L 194 209 L 194 230 L 114 230 Z","E7F6E8","7598A6",3);
                    Stroke(g,"M 157 211 L 157 174 L 230 174","140A0C",12);Stroke(g,"M 157 211 L 157 174 L 230 174","EE4F66",7);
                    Stroke(g,"M 119 243 L 119 308","E6FFF3",4);Spark(g,156,278,12);
                    Ellipse(g,240,230,19,19,"F5FFF1","59C6DD",2);
                }
            }using(Bitmap raw=Resolve(hi,512))return Fit(raw,AccessoryTarget(face),512);
        }
    }
    static void Frame(Graphics g) {
        Paint(g,"M 17 5 L 111 5 L 123 17 L 123 111 L 111 123 L 17 123 L 5 111 L 5 17 Z","30303E","10121D",3);
        Stroke(g,"M 19 7 L 109 7 L 121 19 L 121 109 L 109 121 L 19 121 L 7 109 L 7 19 Z","C7AC70",2);
        Stroke(g,"M 22 12 L 106 12 L 116 22 L 116 106 L 106 116 L 22 116 L 12 106 L 12 22 Z",theme==0?"AB3549":theme==1?"43886E":"367C9F",2);
        Paint(g,"M 59 16 L 64 11 L 69 16 L 64 21 Z","F9E2A0","8F7146",1);
        Paint(g,"M 59 112 L 64 107 L 69 112 L 64 117 Z",Accent,"72503A",1);
    }
    static Bitmap Icon(Bitmap source,int kind) {
        using(Bitmap hi=Canvas(128*S)){
            using(Graphics g=G(hi)){g.ScaleTransform(S,S);Frame(g);g.SetClip(new Rectangle(16,22,96,84));
                if(kind==0) {Rectangle box=Bounds(source,0);g.DrawImage(source,new Rectangle(29,23,70,81),box,GraphicsUnit.Pixel);}
                else if(kind==1) {GraphicsState st=g.Save();g.TranslateTransform(64,64);g.RotateTransform(-33);Rectangle box=Bounds(source,0);float h=theme==0?24:theme==1?49:64;g.DrawImage(source,new RectangleF(-48,-h/2,96,h),box,GraphicsUnit.Pixel);g.Restore(st);}
                else if(theme==0){for(int i=0;i<4;i++){GraphicsState st=g.Save();g.TranslateTransform(40+i*16,106);g.ScaleTransform(.48f,.63f);Vine(g,0,0,120,-30+i*17);g.Restore(st);}AmberEye(g,65,44,.64f);Paint(g,"M 62 60 L 68 60 L 71 93 L 65 103 L 58 92 Z","FFE9AA","BE7B37",2);}
                else if(theme==1){Wing(g,62,66,.42f,-10,true);Wing(g,66,66,.42f,10,false);Stroke(g,"M 29 93 C 54 96 78 63 104 32","D5FFCE",5);Spark(g,82,55,10);Spark(g,34,77,6);}
                else {Paint(g,"M 21 82 C 45 101 40 49 73 55 C 104 58 92 83 75 77 C 105 108 119 55 95 41 C 75 25 42 49 36 66 Z","AAF9EE","2589BD",2);Stroke(g,"M 26 88 C 56 100 59 63 81 66","F65F70",6);for(int i=0;i<6;i++)Ellipse(g,29+i*12,30+(i%3)*13,6+i%2*3,6+i%2*3,"F1FFF3","43B9CD",1);Spark(g,91,37,8);}
            }return Resolve(hi,128);
        }
    }
    static string SuitPath(string suffix){return "Things/Pawn/Humanlike/Apparel/"+Set+"Suit/"+Set+"Suit"+suffix+".png";}
    static string WeaponPath(){return "Things/Item/Equipment/"+(theme==2?"WeaponRanged":"WeaponMelee")+"/EgoWeapon_"+Set+".png";}
    static string AccessoryPath(string f){return "Things/Pawn/Humanlike/Apparel/Accessory/"+Set+"Accessory"+(f=="item"?"":"_"+f)+".png";}
    static void CheckBox(Rectangle actual,Rectangle target,string name) {
        if(Math.Abs(actual.Left-target.Left)>2||Math.Abs(actual.Top-target.Top)>2||Math.Abs(actual.Right-target.Right)>2||Math.Abs(actual.Bottom-target.Bottom)>2)throw new Exception("Registration mismatch: "+name+" "+actual+" expected "+target);
    }
    static void Verify() {
        if(files.Count!=72||new HashSet<string>(files,StringComparer.OrdinalIgnoreCase).Count!=72)throw new Exception("Expected 72 distinct PNGs");
        int[] counts=new int[3];
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])) {
            string name=Path.GetFileNameWithoutExtension(files[j]);theme=Array.FindIndex(Sets,delegate(string s){return files[j].Contains(s);});if(theme<0)throw new Exception(name);counts[theme]++;
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid format: "+name);
            byte[] a=Read(b);int opaque=0,visible=0;
            for(int k=0;k<a.Length/4;k++){if(a[k*4+3]==255)opaque++;if(a[k*4+3]>0)visible++;else if(a[k*4]!=0||a[k*4+1]!=0||a[k*4+2]!=0)throw new Exception("Hidden RGB: "+name);}
            bool empty=name==Set+"Accessory_north"&&theme!=0;
            Rectangle box=Bounds(b,0);
            if(empty){if(visible!=0)throw new Exception("North accessory must be transparent: "+name);}
            else if(opaque==0||visible==b.Width*b.Height)throw new Exception("Invalid alpha: "+name);
            if(name.StartsWith(Set+"Suit_")) {
                string key=name.Substring((Set+"Suit_").Length).Replace("North","north");BodyMask m=masks[key];
                for(int k=0;k<m.Inside.Length;k++){
                    if(m.Inside[k]&&a[k*4+3]<128)throw new Exception("Unsealed interior: "+name);
                    if(!m.Inside[k]&&m.Original[k*4+3]==0&&a[k*4+3]!=0)throw new Exception("Mask leak: "+name);
                    if(m.Inside[k]&&m.Distance[k]<=1.42f&&(Math.Abs(a[k*4]-Ink.B)>1||Math.Abs(a[k*4+1]-Ink.G)>1||Math.Abs(a[k*4+2]-Ink.R)>1))throw new Exception("Missing inward ink: "+name);
                }
                using(Bitmap original=new Bitmap(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+key+".png")))if(box!=Bounds(original,0))throw new Exception("Mask bbox mismatch: "+name);
            }
            if(name.StartsWith(Set+"Accessory")&&!empty){string face=name==Set+"Accessory"?"item":name.Substring((Set+"Accessory_").Length);CheckBox(box,AccessoryTarget(face),name);}
            if(name=="EgoWeapon_"+Set) {CheckBox(box,WeaponTarget(),name);if(box.Width<box.Height)throw new Exception("Nonhorizontal weapon");}
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3}, visible={4}, bbox={5}",name,b.Width,b.Height,opaque,visible,box);
        }
        for(theme=0;theme<3;theme++) {
            if(counts[theme]!=24)throw new Exception("Expected 24 files for "+Set);
            string expected=Set+"Suit_Fat_"+(theme==0?"north":"North")+".png";bool found=false;
            foreach(string f in Directory.GetFiles(Path.Combine(root,"Cont/Textures/Things/Pawn/Humanlike/Apparel/"+Set+"Suit")))if(Path.GetFileName(f)==expected)found=true;
            if(!found)throw new Exception("Incorrect exact filename casing: "+expected);
        }
        Console.WriteLine("SUCCESS: all 72 PNGs passed dimensions, ARGB32, alpha, zero hidden RGB, sealed-mask, inward-ink, exact filename and registration checks.");
    }
    static void Review() {
        string[] labels={"Female S","Female N","Female E","Male S","Weapon","Accessory","Armor Icon","Weapon Icon","Skill Icon"};
        using(Bitmap sheet= new Bitmap(2160,920,PixelFormat.Format32bppArgb))using(Graphics g=G(sheet))using(Font title=new Font(FontFamily.GenericSansSerif,16,FontStyle.Bold))using(Font label=new Font(FontFamily.GenericSansSerif,11,FontStyle.Bold))using(Brush white=new SolidBrush(C("F0E8D7"))) {
            g.Clear(C("171B25"));
            for(theme=0;theme<3;theme++) {
                int y=theme*306;g.DrawString(Set+" / "+(theme==0?"WAW - CRIMSON BRIAR":theme==1?"ZAYIN - EMERALD WINGS":"ZAYIN - SODA SAILOR"),title,white,15,y+5);
                string[] paths={SuitPath("_Female_south"),SuitPath("_Female_north"),SuitPath("_Female_east"),SuitPath("_Male_south"),WeaponPath(),AccessoryPath("item"),"UI/Commands/ExtractEGO_"+Set+"_Armor.png","UI/Commands/ExtractEGO_"+Set+"_Weapon.png","UI/WeaponSkills/"+Set+"/"+Skills[theme]+".png"};
                for(int col=0;col<9;col++) {
                    int x=col*240;using(Brush bg=new SolidBrush(C("303743")))g.FillRectangle(bg,x+5,y+35,230,264);
                    for(int yy=0;yy<11;yy++)for(int xx=0;xx<10;xx++)if((xx+yy)%2==0)using(Brush br=new SolidBrush(C("3A414D")))g.FillRectangle(br,x+10+xx*22,y+59+yy*21,22,21);
                    g.DrawString(labels[col],label,white,x+13,y+39);
                    using(Bitmap b=new Bitmap(Path.Combine(root,"Cont/Textures",paths[col]))) {
                        Rectangle src=col<6?Rectangle.Inflate(Bounds(b,0),8,8):new Rectangle(0,0,128,128);
                        float scale=Math.Min(212f/src.Width,218f/src.Height);int w=(int)(src.Width*scale),h=(int)(src.Height*scale);
                        g.DrawImage(b,new Rectangle(x+120-w/2,y+176-h/2,w,h),src,GraphicsUnit.Pixel);
                    }
                }
            }
            string output=Path.Combine(Path.GetTempPath(),"Vanilla-GroupA-review.png");sheet.Save(output,ImageFormat.Png);Console.WriteLine("REVIEW "+output);
        }
    }
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();masks.Clear();
        for(theme=0;theme<3;theme++) {
            using(Bitmap weapon=Weapon()){Save(weapon,WeaponPath(),512);using(Bitmap icon=Icon(weapon,1))Save(icon,"UI/Commands/ExtractEGO_"+Set+"_Weapon.png",128);}
            foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})using(Bitmap b=Worn(body,face))Save(b,SuitPath("_"+body+"_"+(theme!=0&&body=="Fat"&&face=="north"?"North":face)),512);
            using(Bitmap suit=SuitItem()){Save(suit,SuitPath(""),512);using(Bitmap icon=Icon(suit,0))Save(icon,"UI/Commands/ExtractEGO_"+Set+"_Armor.png",128);}
            foreach(string face in new string[]{"item","south","north","east"})using(Bitmap b=Accessory(face))Save(b,AccessoryPath(face),512);
            using(Bitmap icon=Icon(null,2))Save(icon,"UI/WeaponSkills/"+Set+"/"+Skills[theme]+".png",128);
        }
        Verify();Review();
    }

}
'@
if (-not ('GroupAArt' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[GroupAArt]::Run($ModRoot)
