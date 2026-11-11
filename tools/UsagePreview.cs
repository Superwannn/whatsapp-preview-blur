using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Render fictional chat rows, then use the application's real blur algorithm.
static class UsagePreview {
 static readonly Color Bg=Color.FromArgb(17,20,25),Panel=Color.FromArgb(22,23,23),Text=Color.FromArgb(238,242,246),Muted=Color.FromArgb(151,162,175),Accent=Color.FromArgb(137,235,193);
 static void Main(string[] args){
  using(var canvas=new Bitmap(1200,840))using(var g=Graphics.FromImage(canvas)){
   g.Clear(Bg);g.SmoothingMode=SmoothingMode.AntiAlias;
   Label(g,"PREVIEW BLUR",48,30,11,Accent,true);
   Label(g,"Nama tetap terlihat. Pesan tetap pribadi.",46,62,25,Text,true);
   Label(g,"Contoh ilustrasi dengan kontak dan pesan fiktif.",48,111,12,Muted,false);
   Card(g,48,162,false);Card(g,624,162,true);
   Label(g,"Sebelum",64,728,14,Text,true);Label(g,"Preview pesan dapat terbaca.",64,758,12,Muted,false);
   Label(g,"Sesudah",640,728,14,Accent,true);Label(g,"Hanya cuplikan pesan yang diburamkan.",640,758,12,Muted,false);
   canvas.Save(args[0],ImageFormat.Png);
  }
 }
 static void Card(Graphics canvas,int x,int y,bool blurred){
  using(var image=new Bitmap(528,534))using(var g=Graphics.FromImage(image)){
   g.Clear(Panel);g.SmoothingMode=SmoothingMode.AntiAlias;
   Label(g,"WhatsApp",22,16,10,Muted,false);Label(g,"Chats",22,53,22,Text,true);
   using(var p=Theme.Round(new RectangleF(20,107,488,40),20))using(var b=new SolidBrush(Color.FromArgb(44,45,45)))g.FillPath(b,p);
   Label(g,"Cari atau mulai chat baru",40,117,11,Muted,false);
   Label(g,"Semua      Belum dibaca      Favorit      Grup",24,166,10,Accent,false);
   string[] names={"Alya","Tim Desain","Keluarga","Raka","Komunitas Buku"};
   string[] messages={"Besok ketemu jam berapa?","Nadia: File desainnya sudah siap.","Ibu: Jangan lupa makan, ya.","Terima kasih, sampai jumpa!","Dita: Diskusi bukunya hari Sabtu."};
   string[] times={"19:42","19:30","18:56","18:20","Kemarin"};
   for(int i=0;i<names.Length;i++){
    int ry=208+i*64;
    using(var b=new SolidBrush(Color.FromArgb(48+i*3,62+i*2,59+i*2)))g.FillEllipse(b,22,ry+3,44,44);
    Label(g,names[i].Substring(0,1),36,ry+13,14,Text,true);
    Label(g,names[i],82,ry+3,12,Text,true);Label(g,messages[i],82,ry+27,11,Muted,false);Label(g,times[i],446,ry+5,9,Muted,false);
   }
   if(blurred){
    using(var source=(Bitmap)image.Clone()){
     for(int i=0;i<5;i++)Detection.Blur(g,source,new Rectangle(80,208+i*64+24,355,25),23);
    }
   }
   using(var border=new Pen(Theme.Border))g.DrawRectangle(border,0,0,image.Width-1,image.Height-1);
   canvas.DrawImageUnscaled(image,x,y);
  }
 }
 static void Label(Graphics g,string text,int x,int y,int size,Color color,bool bold){using(var font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular))using(var brush=new SolidBrush(color))g.DrawString(text,font,brush,x,y);}
}
