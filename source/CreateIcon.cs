using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
class CreateIcon {
 static GraphicsPath Round(float x,float y,float w,float h,float r){var p=new GraphicsPath();float d=r*2;p.AddArc(x,y,d,d,180,90);p.AddArc(x+w-d,y,d,d,270,90);p.AddArc(x+w-d,y+h-d,d,d,0,90);p.AddArc(x,y+h-d,d,d,90,90);p.CloseFigure();return p;}
 static void Fill(Graphics g,Color c,float x,float y,float w,float h,float r){using(var p=Round(x,y,w,h,r))using(var b=new SolidBrush(c))g.FillPath(b,p);}
 static Bitmap Draw(int size){var b=new Bitmap(size,size,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b)){g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.ScaleTransform(size/512f,size/512f);
  Fill(g,Color.FromArgb(24,31,47),16,16,480,480,112);
  // Two complementary keycaps, staggered like a simultaneous chord.
  Fill(g,Color.FromArgb(11,18,31),86,207,212,212,48);
  Fill(g,Color.FromArgb(74,159,149),86,191,212,212,48);
  Fill(g,Color.FromArgb(155,236,211),86,179,212,202,48);
  using(var pen=new Pen(Color.FromArgb(34,81,77),14)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,143,288,224,288);}
  Fill(g,Color.FromArgb(11,18,31),219,124,212,212,48);
  Fill(g,Color.FromArgb(119,104,182),219,108,212,212,48);
  Fill(g,Color.FromArgb(205,194,255),219,96,212,202,48);
  // Filled Shift arrow: robust at taskbar and title-bar sizes.
  using(var p=new GraphicsPath()){p.AddPolygon(new PointF[]{new PointF(325,147),new PointF(374,197),new PointF(345,197),new PointF(345,246),new PointF(305,246),new PointF(305,197),new PointF(276,197)});using(var brush=new SolidBrush(Color.FromArgb(59,48,99)))g.FillPath(brush,p);}
 }return b;}
 static void Main(string[] args){string dir=args[0];Directory.CreateDirectory(dir);int[] sizes={16,20,24,32,40,48,64,128,256};var frames=new List<byte[]>();foreach(int s in sizes)using(var b=Draw(s))using(var ms=new MemoryStream()){b.Save(ms,ImageFormat.Png);frames.Add(ms.ToArray());}using(var w=new BinaryWriter(File.Create(Path.Combine(dir,"app.ico")))){w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(frames[i].Length);w.Write(offset);offset+=frames[i].Length;}foreach(var f in frames)w.Write(f);}using(var b=Draw(1024))b.Save(Path.Combine(dir,"app-icon.png"),ImageFormat.Png);
 using(var preview=new Bitmap(720,360))using(var g=Graphics.FromImage(preview)){g.Clear(Color.FromArgb(242,244,248));using(var b=Draw(256))g.DrawImage(b,36,40,256,256);using(var b=Draw(64))g.DrawImage(b,350,140,64,64);using(var b=Draw(32))g.DrawImage(b,466,156,32,32);using(var b=Draw(16))g.DrawImage(b,552,164,16,16);using(var f=new Font("Segoe UI",10))using(var brush=new SolidBrush(Color.FromArgb(70,78,93))){g.DrawString("256",f,brush,148,315);g.DrawString("64",f,brush,370,225);g.DrawString("32",f,brush,472,225);g.DrawString("16",f,brush,550,225);}preview.Save(Path.Combine(dir,"icon-preview.png"),ImageFormat.Png);}
 }
}
