#requires -Version 5.1
<#
Deterministic Laetitia E.G.O art. Pure procedural C# / GDI+, 3x supersampling.
Only inputs: fifteen BodyMasks PNGs. No source art, packages or fonts needed
for sprites. Exterior flood-fill closes enclosed mask holes; chamfer distance
adds a smooth inward ink contour without expanding pawn silhouettes.
Exports and verifies exactly 24 PNGs and a review sheet.
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

public static class LaetitiaArt {
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

    static void Bow(Graphics g,float x,float y,float scale,bool crimson) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(scale,scale);
        string light=crimson?"D74B60":"51404B", dark=crimson?"760E26":"181216";
        Paint(g,"M -3 0 C -26 -29 -48 -24 -40 3 C -38 19 -17 11 -3 0 Z",light,dark,2.3f);
        Paint(g,"M 3 0 C 26 -29 48 -24 40 3 C 38 19 17 11 3 0 Z",light,dark,2.3f);
        Paint(g,"M -8 3 C -10 21 -21 35 -23 44 L -9 37 L -1 44 L 5 5 Z",light,dark,2);
        Paint(g,"M 4 4 L 11 41 L 20 34 L 31 39 C 23 23 17 10 11 3 Z",light,dark,2);
        Stroke(g,"M -35 -10 C -23 -10 -14 -3 -8 -1 M 10 -2 C 19 -8 25 -11 34 -10",crimson?"F78B95":"9D8792",1.5f);
        Ellipse(g,-7,-8,15,16,crimson?"E25167":"57434E",dark,2);
        g.Restore(st);
    }
    static void Bell(Graphics g,float x,float y,float s) {
        GraphicsState st=g.Save();g.TranslateTransform(x,y);g.ScaleTransform(s,s);
        Ellipse(g,-4,-15,8,12,"F7E3A0","8A642A",1.5f);
        Paint(g,"M -11 0 C -12 -15 12 -15 11 0 L 16 10 C 8 18 -9 18 -16 10 Z","F3D77F","98712C",2);
        Stroke(g,"M -12 10 C -4 7 5 7 12 10 M 0 8 L 0 14","4C351E",2);
        Ellipse(g,-3,3,6,6,"382924","181216",0);
        Stroke(g,"M -7 -6 L -8 1","FFF0B8",2);g.Restore(st);
    }
    // Scallops are a coherent lace edge, each with its own cutwork and red stitch.
    static void Lace(Graphics g,float x,float y,float length,float depth,bool dots) {
        int n=Math.Max(2,(int)(length/13));float step=length/n;
        using(Brush b=new SolidBrush(C("EADFD0")))g.FillRectangle(b,x,y,length,depth*.55f);
        for(int j=0;j<n;j++) {
            float xx=x+j*step;
            Ellipse(g,xx,y+depth*.16f,step+1,depth*.84f,"FFF1DC","CAB6A4",1.15f);
            Ellipse(g,xx+step*.38f,y+depth*.54f,step*.24f,depth*.18f,"8E2639","8E2639",0);
            if(dots)Ellipse(g,xx+step*.42f,y+2,2.5f,2.5f,"BD3246","962133",0);
        }
        Stroke(g,"M "+x+" "+y+" L "+(x+length)+" "+y,"B5943C",1.4f);
    }
    static void Coat(Graphics g,string facing) {
        Paint(g,"M -30 -30 L 330 -30 L 330 390 L -30 390 Z","962133","5C0F1C",0);
        using(LinearGradientBrush b=new LinearGradientBrush(new Rectangle(0,0,300,360),C("5C0F1C"),C("962133"),0f)) {
            ColorBlend cb=new ColorBlend();cb.Colors=new Color[]{C("4B0D19"),C("962133"),C("AD2E43"),C("83192D"),C("430C17")};cb.Positions=new float[]{0,.22f,.43f,.72f,1};b.InterpolationColors=cb;g.FillRectangle(b,-10,-10,320,380);
        }
        // Velvet skirt gores: dark recesses, broad red nap and short rim glints.
        for(int j=0;j<7;j++) {
            int x=12+j*46;
            Paint(g,"M "+(135+(x-150)/3)+" 177 C "+x+" 239 "+(x-5)+" 294 "+(x-12)+" 352 L "+(x+14)+" 352 C "+(x+6)+" 288 "+(x+17)+" 237 "+(145+(x-150)/3)+" 177 Z","9E293E","500B19",0);
            Stroke(g,"M "+x+" 268 C "+(x-2)+" 299 "+(x-7)+" 321 "+(x-8)+" 333","BD3246",1.8f);
        }
        Lace(g,-4,333,308,23,true);
        for(int j=0;j<5;j++)Bow(g,25+j*63,329,.20f,false);
        if(facing=="north") {
            // Shoulder cape and radiating pleats lead down into the bustle.
            Paint(g,"M 50 5 C 99 -9 203 -9 254 6 L 297 131 C 241 160 196 137 150 153 C 101 135 54 162 2 131 Z","AC2F45","600F24",3.4f);
            for(int j=0;j<7;j++) {
                int x=26+j*41;
                Stroke(g,"M 150 13 C "+(x+20)+" 48 "+x+" 89 "+x+" 130","530D1E",3);
                Stroke(g,"M 150 21 C "+(x+28)+" 57 "+(x+8)+" 94 "+(x+7)+" 128","BD3246",1.6f);
            }
            Stroke(g,"M 8 130 C 65 153 104 126 150 146 C 194 126 242 151 291 130","EADFD0",5);
            Stroke(g,"M 14 123 C 67 145 109 119 150 139 C 201 119 244 144 285 123","B5943C",1.5f);
            Lace(g,100,2,100,20,false);
            Bow(g,150,170,1.05f,true);
            Stroke(g,"M 118 209 L 103 265 M 180 209 L 195 262","ED7884",2);
        } else if(facing=="east") {
            Paint(g,"M 48 8 C 91 -6 151 1 189 25 C 210 71 231 111 265 159 C 211 179 167 161 131 138 C 84 113 66 67 18 66 Z","B3354B","5C0F1C",3.6f);
            for(int j=0;j<4;j++) {
                int y=24+j*25;
                Stroke(g,"M 72 "+y+" C 121 "+(y+7)+" 146 "+(y+62)+" 217 "+(y+62),"4F0C1C",3);
                Stroke(g,"M 78 "+(y+6)+" C 124 "+(y+14)+" 155 "+(y+68)+" 224 "+(y+68),"BD3246",1.6f);
            }
            Stroke(g,"M 21 67 C 88 76 96 125 147 145 C 188 167 227 177 265 159","EADFD0",7);
            Stroke(g,"M 24 61 C 85 70 100 119 152 139 C 193 160 224 169 259 153","B5943C",2);
            Bow(g,111,154,.57f,true);
        } else {
            // Ivory doll collar and central inset contrast the velvet cape.
            Paint(g,"M 111 -8 L 190 -8 L 188 55 L 153 88 L 111 53 Z","F4EADB","CDB7A8",2.5f);
            Paint(g,"M 90 72 C 127 86 175 85 208 72 L 195 253 C 165 272 132 272 105 253 Z","641022","360A16",3);
            Paint(g,"M 58 5 L 108 -3 C 112 55 94 83 76 124 C 48 138 23 131 4 118 Z","A52B40","5C0F1C",3);
            Paint(g,"M 194 -3 L 249 5 L 296 118 C 276 135 253 137 225 124 C 203 85 188 52 194 -3 Z","A52B40","5C0F1C",3);
            Stroke(g,"M 7 115 C 34 131 54 126 76 120 L 104 51 M 197 51 L 226 120 C 253 132 274 129 292 115","B5943C",2);
            // Broad scalloped collar, arranged as two curved wings.
            Paint(g,"M 107 -1 L 149 24 L 130 61 C 108 52 88 30 87 12 Z","FFF0DD","D3BCAE",2.4f);
            Paint(g,"M 191 -1 L 151 24 L 171 61 C 194 49 211 29 212 12 Z","FFF0DD","D3BCAE",2.4f);
            for(int j=0;j<5;j++) {
                Ellipse(g,90+j*7,21+j*6,14,14,"F9EBD8","D5C1AC",1);
                Ellipse(g,197-j*7,21+j*6,14,14,"F9EBD8","D5C1AC",1);
            }
            Bow(g,150,38,.57f,false);Bell(g,151,68,.52f);
            for(int j=0;j<5;j++) {
                int y=104+j*28;float left=124-j*.5f,right=177+j*.5f;
                Ellipse(g,left-4,y-3,7,7,"D1B65F","76602D",1);
                Ellipse(g,right-4,y-3,7,7,"D1B65F","76602D",1);
                Stroke(g,"M "+left+" "+y+" L "+right+" "+(y+28)+" M "+right+" "+y+" L "+left+" "+(y+28),"30101B",5.5f);
                Stroke(g,"M "+left+" "+y+" L "+right+" "+(y+28)+" M "+right+" "+y+" L "+left+" "+(y+28),"EAE0D0",3.4f);
            }
            Bow(g,151,262,.38f,false);
            Stroke(g,"M 89 157 L 80 241 M 212 157 L 220 241","BD3246",2.2f);
        }
    }
    static Bitmap Worn(string body,string facing) {
        BodyMask mask=new BodyMask(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+body+"_"+facing+".png"));
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);g.TranslateTransform(mask.Bounds.X,mask.Bounds.Y);
                g.ScaleTransform(mask.Bounds.Width/300f,mask.Bounds.Height/360f);Coat(g,facing);
                // Cuffs track the actual anatomy of each mask rather than rectangle corners.
                foreach(bool right in new bool[]{false,true}) {
                    float x=mask.Edge(.78f,right)+(right?-32:2);
                    GraphicsState st=g.Save();g.TranslateTransform(x,273);g.RotateTransform(right?-10:10);
                    Lace(g,-2,0,34,21,true);Bow(g,16,-4,.23f,false);g.Restore(st);
                }
                if(facing=="east") {
                    float x=mask.Edge(.15f,true)-22;
                    GraphicsState st=g.Save();g.TranslateTransform(x,47);
                    Lace(g,-14,-4,36,15,false);Bow(g,5,11,.30f,false);Bell(g,5,25,.35f);g.Restore(st);
                }
            }
            Bitmap b=Resolve(hi,512);mask.Clip(b);return b;
        }
    }
    static Bitmap SuitItem() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                using(GraphicsPath p=P("M 215 66 L 255 88 L 296 66 C 317 86 341 92 365 105 L 410 266 L 382 294 L 351 270 L 371 425 C 307 452 207 452 142 425 L 161 270 L 129 294 L 102 266 L 145 105 C 172 94 193 86 215 66 Z")) {
                    GraphicsState st=g.Save();g.SetClip(p);g.TranslateTransform(102,66);g.ScaleTransform(308f/300,378f/360);Coat(g,"south");g.Restore(st);
                    using(Pen pen=new Pen(Ink,4.5f)){pen.LineJoin=LineJoin.Round;g.DrawPath(pen,p);}
                }
                Stroke(g,"M 160 148 C 180 199 165 242 162 270 M 351 148 C 331 199 347 243 350 270","430A18",4);
                GraphicsState a=g.Save();g.TranslateTransform(113,260);g.RotateTransform(-21);Lace(g,0,0,43,22,true);Bow(g,22,-4,.28f,false);g.Restore(a);
                a=g.Save();g.TranslateTransform(358,248);g.RotateTransform(21);Lace(g,0,0,43,22,true);Bow(g,22,-4,.28f,false);g.Restore(a);
            }return Resolve(hi,512);
        }
    }
    static Bitmap Weapon() {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                // Preserve the old horizontal rifle footprint, including its left-hand stock.
                Paint(g,"M 55 218 C 87 221 107 232 135 234 L 177 229 L 190 246 L 166 262 L 143 263 L 120 281 L 53 296 L 50 280 L 57 249 Z","854139","391820",3.5f);
                Paint(g,"M 61 231 C 96 234 111 246 144 246 L 154 253 L 120 270 L 63 284 Z","A55744","642631",1.7f);
                Stroke(g,"M 69 242 C 96 240 112 255 133 252 M 69 253 C 90 250 105 263 118 260 M 64 273 L 96 268","C07B59",1.4f);
                Paint(g,"M 51 217 L 62 219 L 65 292 L 53 296 Z","D2B063","77603B",2);
                Paint(g,"M 157 248 C 153 271 175 282 196 270 L 211 249 L 201 246 C 192 268 177 271 169 260 L 174 247 Z","C6A456","6E542E",2.5f);
                Stroke(g,"M 186 247 C 178 256 183 262 189 260","D2BD83",3);
                // Twin gunmetal cores and two distinct clean muzzle sockets.
                Paint(g,"M 220 226 L 459 226 L 459 239 L 220 239 Z","929099","2B2930",2.5f);
                Paint(g,"M 220 242 L 459 242 L 459 255 L 220 255 Z","5E5B65","18171E",2.5f);
                Paint(g,"M 230 223 L 416 223 L 416 258 L 231 258 Z","F5EDDF","CCB9A7",2.4f);
                // Subtle ivory winding under the contrasting black satin diagonals.
                for(int j=0;j<8;j++) {
                    int x=237+j*22;
                    Stroke(g,"M "+x+" 225 L "+(x+16)+" 256","B9A995",1.2f);
                }
                using(GraphicsPath p=P("M 235 225 L 414 225 L 414 256 L 235 256 Z")) {
                    GraphicsState st=g.Save();g.SetClip(p);
                    for(int j=0;j<6;j++) {
                        int x=223+j*37;
                        Stroke(g,"M "+x+" 223 L "+(x+40)+" 258 M "+x+" 258 L "+(x+40)+" 223","181216",6);
                        Stroke(g,"M "+x+" 221 L "+(x+40)+" 256 M "+x+" 256 L "+(x+40)+" 221","66505C",1.1f);
                    }g.Restore(st);
                }
                Paint(g,"M 414 222 L 426 222 L 426 259 L 414 259 Z","D9BC6B","8A682F",2);
                Paint(g,"M 447 224 L 459 224 L 459 240 L 447 240 Z","D7BD6C","86662C",2);
                Paint(g,"M 447 242 L 459 242 L 459 257 L 447 257 Z","CBB05B","715129",2);
                Ellipse(g,455,225,6,14,"433943","120D13",1.5f);Ellipse(g,455,243,6,13,"433943","120D13",1.5f);
                Stroke(g,"M 430 228 L 444 228 M 430 244 L 444 244","CCC3AD",1.4f);
                // Antique engraved receiver and raised flintlock hammer.
                Paint(g,"M 174 224 C 193 220 213 220 237 224 L 238 252 C 217 263 194 258 173 254 Z","D0B260","8B692E",3);
                Paint(g,"M 190 222 C 197 210 207 212 209 202 L 203 194 L 209 190 L 224 199 L 220 209 L 215 210 L 211 223 Z","D8BC76","8D6B32",2.6f);
                Paint(g,"M 230 225 L 231 211 L 237 208 L 240 212 L 237 229 Z","AFA69A","504650",2);
                Stroke(g,"M 180 236 C 192 222 210 249 227 232 M 181 247 C 194 234 208 258 229 243","66502A",1.4f);
                Stroke(g,"M 181 230 C 198 226 213 230 229 228","F3DA91",1.6f);
                for(int j=0;j<3;j++)Ellipse(g,182+j*19,239,3.5f,3.5f,"F6E3A1","795B30",.8f);
                Bow(g,291,221,.30f,true);
            }return Resolve(hi,512);
        }
    }
    static Bitmap Bonnet(string facing,bool item) {
        using(Bitmap hi=Canvas(512*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);g.TranslateTransform(item?174:183,item?163:127);
                // Entire bonnet is drawn inside the original 178 x 167 head box.
                for(int j=0;j<13;j++) {
                    double a=Math.PI+(j*Math.PI/12);float xx=89+(float)Math.Cos(a)*73, yy=91+(float)Math.Sin(a)*20;
                    Ellipse(g,xx-13,yy-12,26,43,"51404A","211B1E",2);
                }
                Paint(g,"M 16 83 C 15 40 41 14 87 11 C 131 8 158 34 161 77 L 151 102 C 112 115 57 115 22 99 Z","A5374D","5D1425",3.2f);
                using(GraphicsPath p=P("M 19 81 C 19 39 44 15 87 14 C 128 11 154 36 158 78 L 149 99 C 113 111 58 112 24 97 Z")) {
                    GraphicsState st=g.Save();g.SetClip(p);
                    // Curved-looking diamond quilting, small brass pin-tufts.
                    for(int j=-3;j<8;j++) {
                        int x=j*28;
                        Stroke(g,"M "+x+" 10 L "+(x+91)+" 111 M "+x+" 111 L "+(x+91)+" 10","4D1120",1.6f);
                        Stroke(g,"M "+(x+2)+" 10 L "+(x+93)+" 111","BD5263",.8f);
                    }
                    for(int yy=35;yy<95;yy+=28)for(int xx=45;xx<146;xx+=28)Ellipse(g,xx,yy,2.4f,2.4f,"CDA56D","916A43",0);
                    g.Restore(st);
                }
                // Black cross-straps follow the dome rather than hiding it.
                Stroke(g,"M 35 33 C 72 48 111 70 147 101 M 142 29 C 105 50 70 75 31 103","181216",7);
                Stroke(g,"M 37 32 C 73 48 109 67 144 96 M 140 29 C 105 50 72 73 34 98","68505B",1.4f);
                Paint(g,"M 17 79 C 54 96 125 99 161 76 L 164 93 C 129 120 51 123 14 98 Z","4D3945","181216",3);
                Stroke(g,"M 23 87 C 63 106 119 107 154 86","89717B",1.7f);
                for(int j=0;j<11;j++) {
                    float x=16+j*13.5f;float y=96+(float)Math.Sin(j*Math.PI/10)*14;
                    Ellipse(g,x-3,y-1,15,18,"48323E","211B1E",1.3f);
                    Stroke(g,"M "+(x+3)+" "+(y+5)+" L "+(x+5)+" "+(y+12),"7D5B6A",.8f);
                }
                if(facing=="north") {
                    Bow(g,89,103,.66f,true);Stroke(g,"M 89 116 L 89 141","B5943C",2);Bell(g,89,147,.66f);
                } else {
                    Bow(g,facing=="east"?137:138,91,.65f,true);
                    Stroke(g,"M 145 108 C 160 119 141 133 150 143","B5943C",2);
                    Bell(g,150,148,.67f);
                }
            }return (Bitmap)hi.Clone();
        }
    }
    static Bitmap WeaponIcon() {
        // Render original procedural geometry at full resolution then angle for UI.
        using(Bitmap w=Weapon())using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);g.TranslateTransform(64,64);g.RotateTransform(-34);g.ScaleTransform(.245f,.245f);g.TranslateTransform(-256,-244);
                g.DrawImage(w,0,0,512,512);
            }return Resolve(hi,128);
        }
    }
    static Bitmap Ability() {
        using(Bitmap hi=Canvas(128*S)) {
            using(Graphics g=G(hi)) {
                g.ScaleTransform(S,S);
                Paint(g,"M 17 5 L 111 5 L 123 17 L 123 111 L 111 123 L 17 123 L 5 111 L 5 17 Z","53303A","100C14",0);
                Stroke(g,"M 17 5 L 111 5 L 123 17 L 123 111 L 111 123 L 17 123 L 5 111 L 5 17 Z","B5943C",2.6f);
                Stroke(g,"M 20 10 L 108 10 L 118 20 L 118 108 L 108 118 L 20 118 L 10 108 L 10 20 Z","8F263C",1.5f);
                // Layered soft spider-heart halo behind the solid present.
                for(int j=6;j>=0;j--) {
                    using(Pen p=new Pen(Color.FromArgb(10+j*2,244,66,111),3+j*2))g.DrawEllipse(p,26-j,21-j,76+j*2,76+j*2);
                }
                for(int j=0;j<8;j++) {
                    double a=j*Math.PI/4;float x=64+(float)Math.Cos(a)*43,y=59+(float)Math.Sin(a)*40;
                    Ellipse(g,x-2,y-2,4,4,"EDB3BE","9F3653",0);
                }
                Stroke(g,"M 47 52 L 28 39 L 24 24 M 44 60 L 23 56 L 17 43 M 81 52 L 100 39 L 104 24 M 84 60 L 105 56 L 111 43 M 45 70 L 25 79 L 20 96 M 83 70 L 103 79 L 108 96","DA6B92",2);
                Paint(g,"M 34 58 L 94 58 L 91 103 L 37 103 Z","A12B46","4E1127",2.3f);
                Paint(g,"M 31 51 L 97 51 L 97 65 L 31 65 Z","B03A53","68162E",2.3f);
                Paint(g,"M 57 52 L 70 52 L 70 103 L 57 103 Z","49313F","181216",1.5f);
                Stroke(g,"M 60 66 L 60 100","8C6378",1);
                Bow(g,64,50,.60f,false);
                // Eerie tiny spider-heart seal, with lace petals and a hot pink core.
                for(int j=0;j<10;j++) {
                    double a=j*Math.PI/5;Ellipse(g,63+(float)Math.Cos(a)*13-2,82+(float)Math.Sin(a)*12-2,5,5,"E7BDC1","99475C",.5f);
                }
                Paint(g,"M 64 76 C 53 64 47 81 64 93 C 81 81 75 64 64 76 Z","F49BB2","B12D59",1.5f);
                Ellipse(g,60,76,4,4,"FFE2DC","F1A4B6",0);
                Stroke(g,"M 15 22 L 22 15 M 106 15 L 113 22 M 15 106 L 22 113 M 106 113 L 113 106","E2C77A",2.3f);
            }return Resolve(hi,128);
        }
    }
    static Rectangle Bounds(Bitmap b) {
        byte[] a=Read(b);int l=b.Width,t=b.Height,r=-1,bt=-1;
        for(int k=0;k<a.Length/4;k++)if(a[k*4+3]>0){int x=k%b.Width,y=k/b.Width;l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);bt=Math.Max(bt,y);}
        return Rectangle.FromLTRB(l,t,r+1,bt+1);
    }
    static Bitmap FitBonnet(string face,bool item) {
        using(Bitmap original=Bonnet(face,item))using(Bitmap hi=Canvas(512*S)) {
            Rectangle src=Bounds(original);Rectangle dst=new Rectangle(item?174:183,item?163:127,178,167);
            using(Graphics g=G(hi))using(ImageAttributes ia=new ImageAttributes()) {
                ia.SetWrapMode(WrapMode.TileFlipXY);g.SetClip(new Rectangle(dst.X*S,dst.Y*S,dst.Width*S,dst.Height*S));
                g.DrawImage(original,new Rectangle(dst.X*S,dst.Y*S,dst.Width*S,dst.Height*S),src.X,src.Y,src.Width,src.Height,GraphicsUnit.Pixel,ia);
            }
            Bitmap b=Resolve(hi,512);byte[] a=Read(b);
            for(int k=0;k<a.Length/4;k++)if(!dst.Contains(k%512,k/512)){int i=k*4;a[i]=a[i+1]=a[i+2]=a[i+3]=0;}Write(b,a);return b;
        }
    }
    static void Verify() {
        if(files.Count!=24||new HashSet<string>(files).Count!=24)throw new Exception("Expected exactly 24 unique PNGs");
        for(int j=0;j<files.Count;j++)using(Bitmap b=new Bitmap(files[j])) {
            if(b.Width!=sizes[j]||b.Height!=sizes[j]||b.PixelFormat!=PixelFormat.Format32bppArgb)throw new Exception("Invalid size/ARGB32: "+files[j]);
            byte[] a=Read(b);int opaque=0,visible=0;
            for(int k=0;k<a.Length/4;k++) {
                int i=k*4;if(a[i+3]==255)opaque++;if(a[i+3]>0)visible++;
                else if(a[i]!=0||a[i+1]!=0||a[i+2]!=0)throw new Exception("Hidden RGB: "+files[j]);
            }
            Rectangle box=Bounds(b);string name=Path.GetFileNameWithoutExtension(files[j]);
            if(opaque<50||visible<100||box.Left<1||box.Top<1||box.Right>=b.Width||box.Bottom>=b.Height)throw new Exception("Empty/clipped asset: "+name);
            Match m=Regex.Match(name,@"^LaetitiaSuit_(Fat|Female|Hulk|Male|Thin)_(south|north|east)$");
            if(m.Success) {
                BodyMask mask=new BodyMask(Path.Combine(root,"Tools/1.7/EgoArt/Templates/BodyMasks/Body_"+m.Groups[1].Value+"_"+m.Groups[2].Value+".png"));
                for(int k=0;k<512*512;k++) {
                    int i=k*4;byte expected=mask.Inside[k]?(mask.Distance[k]<=1.42f&&mask.Original[i+3]>=128?mask.Original[i+3]:(byte)255):mask.Original[i+3];
                    if(a[i+3]!=expected)throw new Exception("Mask alpha mismatch at "+k+": "+name);
                    if(mask.Distance[k]<=4&&a[i+3]>0&&(a[i]!=Ink.B||a[i+1]!=Ink.G||a[i+2]!=Ink.R))throw new Exception("Inward ink mismatch: "+name);
                }
                if(box!=BoundsFromMask(mask))throw new Exception("Mask bbox mismatch: "+name);
            }
            if(name.StartsWith("LaetitiaAccessory")) {
                bool item=name=="LaetitiaAccessory";Rectangle target=new Rectangle(item?174:183,item?163:127,178,167);
                if(!target.Contains(box)||Math.Abs(box.Width-178)>2||Math.Abs(box.Height-167)>2)throw new Exception("Head registration mismatch: "+name+" "+box);
            }
            if(name=="EgoWeapon_Laetitia"&&(box.Left<48||box.Left>57||box.Right<458||box.Right>465||box.Top<185||box.Bottom>301||box.Width<box.Height*3.5))throw new Exception("Rifle orientation/bbox mismatch");
            Console.WriteLine("PASS {0} | {1}x{2} ARGB32 | opaque={3}, visible={4}, bbox=({5},{6})-({7},{8})",files[j].Substring(root.Length+1),b.Width,b.Height,opaque,visible,box.Left,box.Top,box.Right-1,box.Bottom-1);
        }
        Console.WriteLine("SUCCESS: 24 PNGs; format, transparency, mask alpha, inward ink, head registration and horizontal rifle bbox verified.");
    }
    static Rectangle BoundsFromMask(BodyMask m) {
        int l=512,t=512,r=-1,b=-1;
        for(int k=0;k<m.Inside.Length;k++)if(m.Inside[k]||m.Original[k*4+3]>0){int x=k%512,y=k/512;l=Math.Min(l,x);r=Math.Max(r,x);t=Math.Min(t,y);b=Math.Max(b,y);}
        return Rectangle.FromLTRB(l,t,r+1,b+1);
    }
    static void Review() {
        string suit="Things/Pawn/Humanlike/Apparel/LaetitiaSuit/LaetitiaSuit";
        string[] names={suit+"_Female_south.png",suit+"_Female_north.png",suit+"_Female_east.png",suit+"_Male_south.png",suit+"_Hulk_south.png",suit+"_Fat_south.png",suit+"_Thin_south.png","Things/Item/Equipment/WeaponRanged/EgoWeapon_Laetitia.png","Things/Pawn/Humanlike/Apparel/Accessory/LaetitiaAccessory.png","UI/Commands/ExtractEGO_Laetitia_Armor.png","UI/Commands/ExtractEGO_Laetitia_Weapon.png","UI/Abilities/SendTheGift.png"};
        string[] labels={"FEMALE / SOUTH","FEMALE / NORTH","FEMALE / EAST","MALE / SOUTH","HULK / SOUTH","FAT / SOUTH","THIN / SOUTH","LAETITIA / FLINTLOCK RIFLE","PILLBOX BONNET / ITEM + NORTH","EXTRACT E.G.O / ARMOR","EXTRACT E.G.O / WEAPON","SEND THE GIFT"};
        using(Bitmap sheet=new Bitmap(1600,1314,PixelFormat.Format32bppArgb))using(Graphics g=G(sheet))
        using(Font title=new Font(FontFamily.GenericSansSerif,25,FontStyle.Bold))using(Font label=new Font(FontFamily.GenericSansSerif,12,FontStyle.Bold))using(Font small=new Font(FontFamily.GenericSansSerif,10))
        using(Brush cream=new SolidBrush(C("EADFD0")))using(Brush muted=new SolidBrush(C("B7A7AF"))) {
            g.Clear(C("151218"));g.DrawString("LAETITIA  /  THE LITTLE WITCH'S GIFT",title,cream,28,17);
            g.DrawString("24 procedural textures | 3x supersampling | burgundy velvet / ivory lace / antique brass | crop views for detail",small,muted,30,60);
            for(int j=0;j<12;j++) {
                int x=20+(j%4)*395,y=96+(j/4)*400;
                using(Brush panel=new SolidBrush(C("26222B")))g.FillRectangle(panel,x,y,375,380);
                using(Brush c=new SolidBrush(C("2C2731")))for(int yy=0;yy<20;yy++)for(int xx=0;xx<19;xx++)if((xx+yy)%2==0)g.FillRectangle(c,x+xx*19,y+42+yy*15,19,15);
                g.DrawString(labels[j],label,cream,x+12,y+12);
                using(Bitmap b=new Bitmap(Path.Combine(root,"Cont/Textures",names[j]))) {
                    Rectangle crop=Bounds(b);crop.Inflate(6,6);crop.Intersect(new Rectangle(0,0,b.Width,b.Height));
                    float scale=Math.Min((j==8?240f:339f)/crop.Width,(j==8?235f:287f)/crop.Height);int w=(int)(crop.Width*scale),h=(int)(crop.Height*scale);
                    g.DrawImage(b,new Rectangle(j==8?x+8:x+(375-w)/2,y+46+(290-h)/2,w,h),crop,GraphicsUnit.Pixel);
                    Rectangle box=Bounds(b);g.DrawString(String.Format("{0}px  |  bbox {1},{2} - {3},{4}",b.Width,box.Left,box.Top,box.Right-1,box.Bottom-1),small,muted,x+12,y+352);
                }
                if(j==8)using(Bitmap back=new Bitmap(Path.Combine(root,"Cont/Textures/Things/Pawn/Humanlike/Apparel/Accessory/LaetitiaAccessory_north.png"))) {
                    Rectangle crop=Bounds(back);g.DrawImage(back,new Rectangle(x+254,y+175,110,104),crop,GraphicsUnit.Pixel);g.DrawString("NORTH",small,cream,x+276,y+289);
                }
            }
            string path=Path.Combine(Path.GetTempPath(),"Vanilla-Laetitia-review.png");sheet.Save(path,ImageFormat.Png);Console.WriteLine("REVIEW: "+path);
        }
    }
    public static void Run(string modRoot) {
        root=Path.GetFullPath(modRoot).TrimEnd(Path.DirectorySeparatorChar);files.Clear();sizes.Clear();
        using(Bitmap weapon=Weapon())Save(weapon,"Things/Item/Equipment/WeaponRanged/EgoWeapon_Laetitia.png",512);
        foreach(string body in new string[]{"Fat","Female","Hulk","Male","Thin"})foreach(string face in new string[]{"south","north","east"})
            using(Bitmap suit=Worn(body,face))Save(suit,"Things/Pawn/Humanlike/Apparel/LaetitiaSuit/LaetitiaSuit_"+body+"_"+face+".png",512);
        using(Bitmap suit=SuitItem()) {
            Save(suit,"Things/Pawn/Humanlike/Apparel/LaetitiaSuit/LaetitiaSuit.png",512);
            using(Bitmap icon=Resolve(suit,128))Save(icon,"UI/Commands/ExtractEGO_Laetitia_Armor.png",128);
        }
        foreach(string face in new string[]{"item","south","north","east"})using(Bitmap b=FitBonnet(face,face=="item"))
            Save(b,"Things/Pawn/Humanlike/Apparel/Accessory/LaetitiaAccessory"+(face=="item"?"":"_"+face)+".png",512);
        using(Bitmap icon=WeaponIcon())Save(icon,"UI/Commands/ExtractEGO_Laetitia_Weapon.png",128);
        using(Bitmap icon=Ability())Save(icon,"UI/Abilities/SendTheGift.png",128);
        Verify();Review();
    }
}
'@
if (-not ('LaetitiaArt' -as [type])) {
    Add-Type -TypeDefinition $source -ReferencedAssemblies System.Drawing
}
[LaetitiaArt]::Run($ModRoot)

