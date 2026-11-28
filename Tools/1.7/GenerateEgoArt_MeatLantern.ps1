#requires -Version 5.1
<#
Deterministic Meat Lantern E.G.O sprite painter. No external packages, downloaded art or fonts.
All geometry is painted at 3x resolution and resolved into straight-alpha ARGB.
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

public static class MeatLanternArt {
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
                float ink=Inside[k]?Math.Max(0,Math.Min(1,5.0f-Distance[k])):1;
                a[i]=(byte)(a[i]*(1-ink)+Ink.B*ink);a[i+1]=(byte)(a[i+1]*(1-ink)+Ink.G*ink);a[i+2]=(byte)(a[i+2]*(1-ink)+Ink.R*ink);
            }
            Write(b,a);
        }
    }

    // Each tooth has a red gum socket, ivory blade and a fine specular edge.
    static void Fang(Graphics g,float x,float y,float width,float length,float angle,bool redTip) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.RotateTransform(angle);
        g.ScaleTransform(width/16f,length/32f);
        Paint(g,"M -8 0 C -7 -4 6 -4 8 0 C 7 12 4 24 -1 32 C 0 18 -7 10 -8 0 Z","FFF3EA","D4C2B1",1.5f);
        Paint(g,"M -8 0 C -6 -5 5 -5 8 0 L 6 7 L 2 4 L -2 8 L -7 5 Z","E3523B","A72D2A",.8f);
        if(redTip)Paint(g,"M 0 24 L -1 32 L 4 22 L 1 25 Z","E3523B","BD3735",0);
        Stroke(g,"M -3 8 C 1 14 1 19 0 23","FFFFFF",1);
        g.Restore(st);
    }
    static void Halo(Graphics g,float x,float y,float radius) {
        using(GraphicsPath p=new GraphicsPath()) {
            p.AddEllipse(x-radius,y-radius,radius*2,radius*2);
            using(PathGradientBrush b=new PathGradientBrush(p)) {
                b.CenterColor=Color.FromArgb(155,C("D3FF93"));
                b.SurroundColors=new Color[]{Color.FromArgb(0,C("C5FF8A"))};
                g.FillPath(b,p);
            }
        }
    }
    static void Flower(Graphics g,float x,float y,float scale) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        Halo(g,0,0,66);
        // Narrow pointed petals bloom from a dark chitin calyx, never a flat star.
        Ellipse(g,-26,-25,52,50,"835A38","34271D",2.6f);
        for(int j=0;j<9;j++) {
            GraphicsState petal=g.Save();g.RotateTransform(j*40+8);
            Paint(g,"M -6 -7 C -23 -20 -16 -39 -7 -48 C -6 -35 10 -32 10 -20 C 10 -14 4 -9 -6 -7 Z","F6FF4D","B7CB32",1.6f);
            Stroke(g,"M -7 -36 C -6 -24 0 -22 0 -13","FFFFB4",1.8f);g.Restore(petal);
        }
        Ellipse(g,-15,-15,30,30,"DAFF8A","94F536",2.2f);
        Ellipse(g,-7,-9,13,15,"FAFFD1","D2FF58",0);
        for(int j=0;j<7;j++){double a=j*Math.PI*2/7;Ellipse(g,(float)Math.Cos(a)*10-1,(float)Math.Sin(a)*10-1,2.5f,2.5f,"5E8A20","5E8A20",0);}
        g.Restore(st);
    }
    static Bitmap Weapon() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                // Horizontal registration matches the original left-to-right polearm.
                Paint(g,"M 17 227 L 302 225 L 331 238 L 331 255 L 301 266 L 17 261 L 11 248 Z","51545C","16171E",3.4f);
                Paint(g,"M 26 233 L 296 232 L 310 239 L 26 241 Z","87929A","353B43",0);
                Stroke(g,"M 37 255 L 287 258","070D13",3);
                Paint(g,"M 15 226 L 29 227 L 29 263 L 16 263 L 10 251 L 10 238 Z","6D737A","22242C",3);
                for(int j=0;j<13;j++) {
                    int x=48+j*9;
                    Stroke(g,"M "+x+" 234 L "+(x+5)+" 255","17171F",3.2f);
                    Stroke(g,"M "+(x+2)+" 234 L "+(x+6)+" 249","60626C",1);
                }
                Paint(g,"M 285 220 L 310 218 L 325 230 L 325 263 L 308 275 L 285 268 Z","7D513E","34251F",3.4f);
                Stroke(g,"M 295 225 L 295 262 M 304 227 L 304 266","C49B74",1.6f);
                // Hollow throat is enclosed by two separately sculpted fleshy jaws.
                Paint(g,"M 317 188 C 356 169 440 173 481 208 C 501 230 492 284 465 310 C 424 339 343 324 318 292 C 301 262 300 218 317 188 Z","754435","2A2020",4);
                Paint(g,"M 337 212 C 374 189 434 195 471 221 C 483 238 473 275 454 290 C 414 312 363 296 333 278 Z","203F23","11171B",3);
                Halo(g,410,247,67);
                Paint(g,"M 342 248 C 371 228 391 252 419 235 C 445 219 464 239 464 253 C 443 238 433 273 407 268 C 382 259 366 278 342 265 Z","B8F846","507A28",0);
                Stroke(g,"M 359 253 C 388 245 402 261 423 248 C 440 236 447 244 451 249","E4FF9E",3);
                // Ivory skin, pink recessed gum rim and chestnut root folds.
                Paint(g,"M 307 224 C 298 202 302 180 328 166 C 365 145 409 151 438 163 C 464 173 490 190 498 212 C 479 200 465 211 454 218 C 421 194 382 194 354 211 L 330 236 Z","EDE2DA","B6A298",4.2f);
                Paint(g,"M 320 274 C 344 284 362 304 402 302 C 442 301 460 279 483 273 C 479 304 456 330 420 338 C 382 348 334 329 317 310 C 304 296 306 283 320 274 Z","EDE2DA","BFA99D",4.2f);
                Stroke(g,"M 331 221 C 372 188 424 194 463 216 M 330 283 C 372 320 438 311 475 281","AE4C44",8);
                Stroke(g,"M 323 187 C 357 164 406 166 432 177 M 339 318 C 370 336 413 336 441 322","FFF3EA",3);
                // Clustered unequal fangs, facing into the mouth; red-tinted tips.
                for(int j=0;j<10;j++) {
                    float x=335+j*14, y=214-14*(float)Math.Sin(j*Math.PI/10);
                    Fang(g,x,y,13+(j%3)*2,27+(j%3)*6,-15+j*3,true);
                }
                for(int j=0;j<9;j++) {
                    float x=341+j*15, y=291+12*(float)Math.Sin(j*Math.PI/9);
                    Fang(g,x,y,14+(j%2)*3,29+(j%3)*5,180-18+j*4,true);
                }
                Fang(g,326,233,17,36,-52,true);Fang(g,325,277,17,37,231,true);
                // Skin/chitin pleats remain readable at inventory scale.
                for(int j=0;j<4;j++) {
                    int x=312+j*12;
                    Stroke(g,"M "+x+" 187 C "+(x-5)+" 200 "+(x-2)+" 207 "+(x+4)+" 215","897265",2);
                    Stroke(g,"M "+(x+2)+" 294 L "+(x+9)+" 315","9C7B6A",2);
                }
                // Small dorsal angler filament, firmly attached to the maw.
                Stroke(g,"M 376 160 C 375 135 408 134 411 158","140A0C",5);
                Stroke(g,"M 376 160 C 375 135 408 134 411 158","8B7257",2.5f);
                Halo(g,411,161,20);Ellipse(g,403,153,16,16,"F6FFB0","B8F846",1.6f);
            }
            return Resolve(hi,512);
        }
    }
    static void Pauldron(Graphics g,float x,float y,bool mirror) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);if(mirror)g.ScaleTransform(-1,1);
        Paint(g,"M -48 17 C -38 -15 1 -21 32 -2 L 43 34 C 16 28 -3 44 -37 43 Z","94664D","4F3124",3.5f);
        Paint(g,"M -41 11 C -23 -7 5 -8 28 3 L 31 14 C 3 6 -15 13 -38 25 Z","B38A68","7D513E",2);
        Stroke(g,"M -31 30 C -7 18 12 20 31 25","C69B74",1.5f);
        g.Restore(st);
    }
    static void Coat(Graphics g,string facing) {
        Paint(g,"M -30 -25 L 330 -25 L 330 390 L -30 390 Z","E4DBD3","BFB1A7",0);
        // Broad clean value grouping: cream sleeves and skirt, sculpted chestnut shell.
        for(int j=0;j<6;j++) {
            int x=24+j*50;
            Paint(g,"M "+x+" 177 C "+(x+17)+" 230 "+(x-5)+" 294 "+(x-12)+" 370 L "+(x+10)+" 370 C "+(x+15)+" 284 "+(x+27)+" 230 "+(x+17)+" 180 Z","D0C3B8","AA998E",0);
            Stroke(g,"M "+(x+19)+" 224 C "+(x+24)+" 278 "+(x+12)+" 315 "+(x+11)+" 340","F2EAE0",1.8f);
        }
        Stroke(g,"M 15 327 C 80 349 220 349 289 327","675046",3);
        Stroke(g,"M 16 323 C 97 342 211 342 287 323","F5EEE3",2);
        // Teeth stay subtle and INSIDE the body masks along the coat hem.
        for(int j=0;j<12;j++)Fang(g,24+j*23,337+(float)Math.Sin(j*.65)*5,8+j%2*3,15+j%3*3,(j%3-1)*12,false);
        if(facing=="north") {
            Paint(g,"M 63 -4 C 100 10 201 10 239 -4 L 266 78 C 239 133 204 147 155 166 C 111 150 67 129 39 83 Z","7D513E","4F3124",4);
            for(int j=0;j<3;j++) {
                int y=32+j*30;
                Stroke(g,"M 65 "+y+" C 101 "+(y+5)+" 115 "+(y+33)+" 150 "+(y+36)+" C 185 "+(y+33)+" 205 "+(y+5)+" 238 "+y,"34221E",4);
                Stroke(g,"M 70 "+(y-5)+" C 103 "+y+" 118 "+(y+26)+" 150 "+(y+29)+" C 183 "+(y+26)+" 201 "+y+" 232 "+(y-5),"B48A62",2);
            }
            Pauldron(g,52,25,false);Pauldron(g,248,25,true);
            Paint(g,"M 135 5 L 165 5 L 169 278 L 150 320 L 132 278 Z","5A4131","322722",2.5f);
            Stroke(g,"M 150 16 C 144 102 159 160 150 225","99C942",3);
            Halo(g,150,187,57);
            Paint(g,"M 150 147 C 175 163 178 193 165 214 L 150 232 L 135 214 C 123 191 126 163 150 147 Z","835A3C","34261E",3);
            Ellipse(g,135,162,30,53,"F6FFAF","94F536",2.5f);
            Stroke(g,"M 144 171 C 139 181 140 193 143 200","FFFFCD",3);
            Stroke(g,"M 151 239 L 151 327","6E594D",2);
        } else if(facing=="east") {
            Paint(g,"M 83 -8 L 256 -8 L 278 94 L 254 192 C 211 177 179 167 153 130 L 89 87 Z","7D513E","4F3124",3.5f);
            Pauldron(g,124,47,false);
            for(int j=0;j<3;j++)Stroke(g,"M 151 "+(76+j*25)+" C 191 "+(91+j*24)+" 217 "+(80+j*24)+" 265 "+(52+j*26),"BA9067",2.2f);
            Paint(g,"M 259 4 L 301 3 L 303 55 L 271 63 Z","48414A","2E2A2E",3);
            Paint(g,"M 279 25 L 290 21 L 298 33 L 291 43 L 298 95 L 284 88 L 279 45 Z","FFF9EF","C9C7BC",2);
            Flower(g,264,126,.54f);
            Stroke(g,"M 246 188 C 256 246 251 286 250 328","5F4538",3);
            for(int j=0;j<5;j++)Fang(g,247-j*3,191+j*21,9,15,65,false);
        } else {
            Paint(g,"M 96 -12 L 203 -12 L 211 85 L 149 126 L 89 85 Z","48414A","2E2A2E",3);
            Paint(g,"M 110 0 L 147 18 L 129 42 L 104 22 Z","3A343C","1D1920",2);
            Paint(g,"M 192 0 L 153 18 L 175 42 L 198 22 Z","3A343C","1D1920",2);
            Paint(g,"M 144 13 L 158 13 L 164 25 L 153 38 L 140 26 Z","FFFFFF","D8D5CC",2);
            Paint(g,"M 149 36 L 157 36 L 167 100 L 153 117 L 139 100 Z","FFFDF4","C5C3BC",2.5f);
            Paint(g,"M 80 28 C 95 56 121 76 151 80 C 178 75 203 58 222 28 L 250 121 C 223 154 188 177 151 187 C 113 175 76 155 52 121 Z","7D513E","4F3124",4);
            Paint(g,"M 67 75 C 96 89 116 109 145 112 L 143 160 C 111 150 88 139 69 121 Z","9B6C4F","64412F",2.5f);
            Paint(g,"M 232 75 C 208 90 182 110 158 112 L 159 160 C 190 150 213 137 231 121 Z","946548","533326",2.5f);
            Stroke(g,"M 75 91 C 98 100 119 121 140 124 M 225 91 C 204 102 182 122 163 124","C99C70",2);
            Pauldron(g,48,27,false);Pauldron(g,252,27,true);
            // The coat opens in two long pale pointed lapels beneath the cuirass.
            Paint(g,"M 72 132 L 121 171 L 103 260 L 67 231 L 83 185 L 56 178 Z","ECE3D9","BCAB9B",3);
            Paint(g,"M 229 132 L 180 171 L 197 260 L 233 231 L 216 185 L 244 178 Z","E4DBD3","AD9A8C",3);
            for(int j=0;j<5;j++) {
                Fang(g,83+j*6,143+j*10,8,14,-43,false);
                Fang(g,218-j*6,143+j*10,8,14,43,false);
            }
            Stroke(g,"M 148 190 L 149 332","5F493C",2.5f);
            Stroke(g,"M 155 199 L 157 329","F7F0E6",2);
            for(int j=0;j<4;j++)Ellipse(g,159,219+j*27,5,6,"BB9E6B","564336",1);
            Flower(g,151,112,.88f);
        }
    }
    static Bitmap Worn(string body,string facing) {
        BodyMask mask=new BodyMask(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+body+"_"+facing+".png"));
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);g.TranslateTransform(mask.Bounds.X,mask.Bounds.Y);
                g.ScaleTransform(mask.Bounds.Width/300f,mask.Bounds.Height/360f);Coat(g,facing);
                if(facing=="east") {
                    // Body-specific front edge registration, as in NothingThere.
                    GraphicsState st=g.Save();g.TranslateTransform(mask.Edge(.19f,true)-12,65);
                    Paint(g,"M -16 -10 L 9 -5 L 11 18 L -15 20 Z","48414A","2E2A2E",2);
                    Paint(g,"M -1 0 L 6 3 L 4 13 L 10 38 L 0 34 L -4 14 Z","FFFDF4","C5C3BC",1.8f);g.Restore(st);
                }
            }
            Bitmap b=Resolve(hi,512);mask.Clip(b);return b;
        }
    }
    static Bitmap SuitItem() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                using(GraphicsPath shape=P("M 220 67 C 202 82 179 94 149 104 L 106 242 L 126 278 L 163 259 L 147 430 C 211 447 305 447 369 430 L 352 259 L 388 278 L 408 242 L 365 104 C 334 94 313 82 294 67 L 256 87 Z")) {
                    GraphicsState st=g.Save();g.SetClip(shape);g.TranslateTransform(106,66);g.ScaleTransform(302f/300,377f/360);Coat(g,"south");g.Restore(st);
                    using(Pen p=new Pen(Ink,4.5f)){p.LineJoin=LineJoin.Round;g.DrawPath(p,shape);}
                }
                Stroke(g,"M 153 127 C 175 164 168 221 164 259 M 361 127 C 341 163 346 220 352 259","685144",3);
                Stroke(g,"M 138 178 L 120 241 L 132 260 M 377 177 L 393 240 L 384 257","FFF3E4",2.3f);
            }
            return Resolve(hi,512);
        }
    }
    static Bitmap Accessory(bool east) {
        // Draw within an exact integer destination box AFTER supersampling.
        // This preserves the tiny original world-space registration without overshoot.
        Rectangle box=east?new Rectangle(148,150,19,17):new Rectangle(104,154,49,13);
        using(Bitmap hi=Canvas(256*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);g.TranslateTransform(box.X,box.Y);g.ScaleTransform(box.Width/100f,box.Height/32f);
                Stroke(g,"M 3 5 C 28 1 69 1 97 5","140A0C",5);
                int count=east?3:6;
                for(int j=0;j<count;j++) {
                    float x=9+j*(82f/(count-1));
                    Fang(g,x,5,15,j==0||j==count-1?25:18+j%2*4,(j-(count-1)/2f)*-3,false);
                }
            }
            Bitmap b=Resolve(hi,256);byte[] a=Read(b);
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)if(!box.Contains(x,y)){int i=(y*256+x)*4;a[i]=a[i+1]=a[i+2]=a[i+3]=0;}
            Write(b,a);return b;
        }
    }
    static Bitmap Miniature(Bitmap source,bool weapon) {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                // Tightly crop the master: preserve orientation and avoid tiny UI subjects.
                Rectangle crop=weapon?new Rectangle(7,133,495,213):new Rectangle(102,62,310,388);
                Rectangle dest=weapon?new Rectangle(3,38,122,53):new Rectangle(19,6,90,113);
                using(ImageAttributes ia=new ImageAttributes()) {ia.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(source,dest,crop.X,crop.Y,crop.Width,crop.Height,GraphicsUnit.Pixel,ia);}
            }
            return Resolve(hi,128);
        }
    }
    static Bitmap Ability() {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                string border="M 16 4 L 112 4 L 124 16 L 124 112 L 112 124 L 16 124 L 4 112 L 4 16 Z";
                Paint(g,border,"3B2424","100C12",3);
                Stroke(g,border,"A38550",2.5f);
                Stroke(g,"M 17 9 L 110 9 L 119 18 L 119 110 L 110 119 L 18 119 L 9 110 L 9 18 Z","7C292E",2);
                for(int j=0;j<4;j++) {
                    GraphicsState st=g.Save();g.TranslateTransform(64,64);g.RotateTransform(j*90);
                    Paint(g,"M -53 -48 L -43 -53 L -34 -53 L -45 -45 L -53 -34 Z","E3C17A","6C492F",.7f);g.Restore(st);
                }
                Halo(g,64,64,49);
                using(Pen p=new Pen(C("344C24"),13))g.DrawArc(p,24,23,79,79,12,295);
                using(Pen p=new Pen(C("94F536"),5)){p.StartCap=p.EndCap=LineCap.Round;g.DrawArc(p,24,23,79,79,12,295);}
                using(Pen p=new Pen(C("F3FF9C"),1.5f))g.DrawArc(p,21,20,85,85,30,240);
                // Opposing curved snapping jaws, rotating around a bright lure.
                for(int j=0;j<2;j++) {
                    GraphicsState st=g.Save();g.TranslateTransform(64,64);g.RotateTransform(-28+j*180);
                    Paint(g,"M -40 -9 C -43 -47 29 -57 44 -15 L 33 -17 C 13 -36 -18 -31 -28 -9 Z","EDE2DA","B9A296",2);
                    Stroke(g,"M -29 -13 C -12 -32 18 -30 33 -17","AC3935",4);
                    for(int k=0;k<5;k++)Fang(g,-24+k*13,-17-(float)Math.Sin(k*Math.PI/4)*9,8,13+k%2*5,-22+k*11,true);
                    g.Restore(st);
                }
                Flower(g,64,64,.28f);
                Paint(g,"M 93 85 L 105 84 L 100 95 Z","F6FF4D","94F536",1);
            }
            return Resolve(hi,128);
        }
    }
    static Rectangle Bounds(Bitmap b) {
        byte[] a=Read(b);int l=b.Width,t=b.Height,r=-1,d=-1;
        for(int k=0;k<a.Length/4;k++)if(a[k*4+3]>0){int x=k%b.Width,y=k/b.Width;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);d=Math.Max(d,y);}
        return r<0?Rectangle.Empty:Rectangle.FromLTRB(l,t,r+1,d+1);
    }
    static void Verify() {
        if(files.Count!=24||new HashSet<string>(files).Count!=24)throw new Exception("Expected 24 unique deliverables");
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])) {
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid size/ARGB32: "+files[j]);
            byte[] a=Read(b);int opaque=0,visible=0;
            for(int k=0;k<a.Length/4;k++) {
                int i=k*4;if(a[i+3]>0)visible++;if(a[i+3]==255)opaque++;
                if(a[i+3]==0&&(a[i]!=0||a[i+1]!=0||a[i+2]!=0))throw new Exception("Hidden RGB: "+files[j]);
            }
            bool empty=files[j].EndsWith("Accessory_north.png",StringComparison.OrdinalIgnoreCase);
            if(empty?visible!=0:opaque==0)throw new Exception("Invalid alpha population: "+files[j]);
            Rectangle bounds=Bounds(b);
            if(!empty&&(bounds.Left==0||bounds.Top==0||bounds.Right==b.Width||bounds.Bottom==b.Height))throw new Exception("Clipped canvas edge: "+files[j]);
            string name=Path.GetFileNameWithoutExtension(files[j]);
            Match worn=Regex.Match(name,@"^MeatLanternSuit_(Fat|Female|Hulk|Male|Thin)_(south|north|east)$");
            if(worn.Success) {
                BodyMask mask=new BodyMask(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+worn.Groups[1].Value+"_"+worn.Groups[2].Value+".png"));
                for(int k=0;k<mask.Inside.Length;k++) {
                    int i=k*4;byte expected=0;
                    if(mask.Inside[k])expected=mask.Distance[k]<=1.42f&&mask.Original[i+3]>=128?mask.Original[i+3]:(byte)255;
                    else if(mask.Original[i+3]>0)expected=mask.Original[i+3];
                    if(a[i+3]!=expected)throw new Exception("Body mask alpha mismatch at "+k+": "+name);
                    if(expected>0&&mask.Distance[k]<=4&& (a[i]!=Ink.B||a[i+1]!=Ink.G||a[i+2]!=Ink.R))throw new Exception("Inward ink contour mismatch: "+name);
                }
                if(bounds!=MaskVisibleBounds(mask))throw new Exception("Body silhouette bbox mismatch: "+name);
            }
            if(name=="MeatLanternAccessory"||name=="MeatLanternAccessory_south") {
                Rectangle target=Rectangle.FromLTRB(104,154,153,167);
                if(bounds!=target)throw new Exception("South/item fang registration mismatch: "+bounds);
            }
            if(name=="MeatLanternAccessory_east"&&bounds!=Rectangle.FromLTRB(148,150,167,167))throw new Exception("East fang registration mismatch: "+bounds);
            if(name=="EgoWeapon_Lantern"&&(bounds.Left<7||bounds.Left>15||bounds.Right<497||bounds.Right>502||bounds.Top<130||bounds.Bottom>350||bounds.Width<480||bounds.Width<bounds.Height*2))throw new Exception("Horizontal weapon registration mismatch: "+bounds);
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3}, visible={4}, bbox=({5},{6})-({7},{8})",files[j].Substring(root.Length+1),b.Width,b.Height,opaque,visible,empty?-1:bounds.Left,empty?-1:bounds.Top,empty?-1:bounds.Right-1,empty?-1:bounds.Bottom-1);
        }
        Console.WriteLine("SUCCESS: 24 PNGs; exact body-mask alpha + inward ink, weapon orientation, accessory registration, empty north and clean hidden RGB verified.");
    }
    static Rectangle MaskVisibleBounds(BodyMask m) {
        int l=512,t=512,r=-1,b=-1;
        for(int k=0;k<m.Inside.Length;k++)if(m.Inside[k]||m.Original[k*4+3]>0){int x=k%512,y=k/512;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}
        return Rectangle.FromLTRB(l,t,r+1,b+1);
    }
    static void Review() {
        // Review labels use the OS font only; no font/image input files are loaded.
        string suit="Things/Pawn/Humanlike/Apparel/MeatLanternSuit/MeatLanternSuit";
        string[] paths={suit+"_Female_south.png",suit+"_Female_north.png",suit+"_Female_east.png",suit+"_Male_south.png",suit+"_Hulk_south.png",suit+"_Fat_south.png",suit+"_Thin_south.png","Things/Item/Equipment/WeaponMelee/EgoWeapon_Lantern.png","Things/Pawn/Humanlike/Apparel/Accessory/MeatLanternAccessory.png","UI/Commands/ExtractEGO_MeatLantern_Armor.png","UI/Commands/ExtractEGO_MeatLantern_Weapon.png","UI/WeaponSkills/MeatLantern/Whirlwind_OmNomNom.png"};
        string[] labels={"Female / SOUTH","Female / NORTH","Female / EAST","Male / SOUTH","Hulk / SOUTH","Fat / SOUTH","Thin / SOUTH","LANTERN / HORIZONTAL MAW","FANGS / EXACT PLACEMENT","EXTRACT / ARMOR","EXTRACT / WEAPON","OM NOM NOM / SKILL"};
        using(Bitmap sheet=new Bitmap(1600,1330,PixelFormat.Format32bppArgb))using(Graphics g=G(sheet))using(Font title=new Font(FontFamily.GenericSansSerif,23,FontStyle.Bold))using(Font font=new Font(FontFamily.GenericSansSerif,12,FontStyle.Bold))using(Font small=new Font(FontFamily.GenericSansSerif,10)) {
            g.Clear(C("14191D"));using(Brush b=new SolidBrush(C("E5E0D2")))g.DrawString("MEAT LANTERN  /  E.G.O ART REVIEW",title,b,22,15);
            using(Brush b=new SolidBrush(C("B8F846")))g.DrawString("3x procedural GDI+  |  15 vanilla-mask silhouettes  |  24 transparent PNG assets",small,b,25,57);
            for(int j=0;j<12;j++) {
                int x=16+j%4*396,y=90+j/4*410;
                using(Brush b=new SolidBrush(C("242C32")))g.FillRectangle(b,x,y,384,396);
                for(int cy=0;cy<22;cy++)for(int cx=0;cx<23;cx++)using(Brush b=new SolidBrush(C((cx+cy)%2==0?"30393F":"343E44")))g.FillRectangle(b,x+8+cx*16,y+34+cy*16,16,16);
                using(Brush b=new SolidBrush(C("E9E3D5")))g.DrawString(labels[j],font,b,x+10,y+8);
                using(Bitmap image=new Bitmap(Path.Combine(root,"Cont/Textures",paths[j]))) {
                    if(j<7)g.DrawImage(image,new Rectangle(x+9,y+29,366,366));
                    else if(j==7)g.DrawImage(image,new Rectangle(x+9,y+33,366,366));
                    else if(j==8) {
                        g.DrawImage(image,new Rectangle(x+10,y+35,256,256));
                        Rectangle box=Bounds(image);
                        using(Pen p=new Pen(C("C0A66A"),1))g.DrawRectangle(p,x+10+box.X-3,y+35+box.Y-3,box.Width+6,box.Height+6);
                        g.DrawImage(image,new Rectangle(x+57,y+299,270,72),box,GraphicsUnit.Pixel);
                        using(Brush b=new SolidBrush(C("CEC6B8")))g.DrawString("256px registration above / enlarged below",small,b,x+12,y+373);
                    } else {
                        // Enlarged main view plus a separate native-size preview.
                        g.DrawImage(image,new Rectangle(x+80,y+35,224,224));
                        g.DrawImageUnscaled(image,x+128,y+264);
                    }
                }
            }
            string path=Path.Combine(Path.GetTempPath(),"Vanilla-MeatLantern-review.png");sheet.Save(path,ImageFormat.Png);Console.WriteLine("REVIEW "+path);
        }
    }
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();
        using(Bitmap weapon=Weapon()) {
            Save(weapon,"Things/Item/Equipment/WeaponMelee/EgoWeapon_Lantern.png",512);
            using(Bitmap icon=Miniature(weapon,true))Save(icon,"UI/Commands/ExtractEGO_MeatLantern_Weapon.png",128);
        }
        using(Bitmap icon=Ability())Save(icon,"UI/WeaponSkills/MeatLantern/Whirlwind_OmNomNom.png",128);
        foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})
            using(Bitmap b=Worn(body,face))Save(b,"Things/Pawn/Humanlike/Apparel/MeatLanternSuit/MeatLanternSuit_"+body+"_"+face+".png",512);
        using(Bitmap suit=SuitItem()) {
            Save(suit,"Things/Pawn/Humanlike/Apparel/MeatLanternSuit/MeatLanternSuit.png",512);
            using(Bitmap icon=Miniature(suit,false))Save(icon,"UI/Commands/ExtractEGO_MeatLantern_Armor.png",128);
        }
        string dir="Things/Pawn/Humanlike/Apparel/Accessory/MeatLanternAccessory";
        using(Bitmap b=Accessory(false)){Save(b,dir+".png",256);Save(b,dir+"_south.png",256);}
        using(Bitmap b=Accessory(true))Save(b,dir+"_east.png",256);
        using(Bitmap b=Canvas(256))Save(b,dir+"_north.png",256);
        Verify();Review();
    }
}
'@
if (-not ('MeatLanternArt' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[MeatLanternArt]::Run($ModRoot)

