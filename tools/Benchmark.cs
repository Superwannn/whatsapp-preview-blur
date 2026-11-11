using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
static class Benchmark {
 static void Main(string[] args){
  using(var layout=new Bitmap(1920,1200))using(var source=new Bitmap(468,990))using(var output=new Bitmap(468,990)){
   using(var g=Graphics.FromImage(layout)){g.Clear(Color.FromArgb(22,23,23));g.DrawLine(Pens.DimGray,638,40,638,1199);}
   using(var g=Graphics.FromImage(source)){g.Clear(Color.FromArgb(22,23,23));for(int row=0;row<13;row++){g.DrawString("Nama kontak",SystemFonts.DefaultFont,Brushes.White,4,15+row*76);g.DrawString("Cuplikan pesan untuk pengukuran",SystemFonts.DefaultFont,Brushes.LightGray,4,39+row*76);}}
   var rows=Detection.Find(source,1);
   Action edge=delegate{Detection.SidebarEdge(layout,1);};Action find=delegate{Detection.Find(source,1);};
   Action blur=delegate{using(var g=Graphics.FromImage(output))foreach(var r in rows)Detection.Blur(g,source,r,23);};
   string result="Rows: "+rows.Count+"\nSidebar scan ms: "+Measure(edge)+"\nRow detection ms: "+Measure(find)+"\nBlur ms: "+Measure(blur);
   File.WriteAllText(args[0],result);
  }
 }
 static string Measure(Action action){for(int i=0;i<3;i++)action();var sw=Stopwatch.StartNew();for(int i=0;i<30;i++)action();return (sw.Elapsed.TotalMilliseconds/30).ToString("F2",System.Globalization.CultureInfo.InvariantCulture);}
}
