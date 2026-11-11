using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

static class StartupSetting {
 const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
 public static string Command(string path){return "\""+Path.GetFullPath(path)+"\" --tray";}
 public static bool Enabled(){using(var key=Registry.CurrentUser.OpenSubKey(Key))return key!=null && key.GetValue("PreviewBlur") is string;}
 public static void Apply(bool enabled){using(var key=Registry.CurrentUser.CreateSubKey(Key)){if(enabled)key.SetValue("PreviewBlur",Command(Application.ExecutablePath),RegistryValueKind.String);else key.DeleteValue("PreviewBlur",false);}}
}

sealed class HoverGate {
 Point cursor;Rectangle row;long since;bool active;int content;
 public void Reset(){active=false;}
 public bool Observe(Point point,Rectangle bounds,long now,int delay,int fingerprint=0){if(!active || point!=cursor || bounds!=row || content!=fingerprint){cursor=point;row=bounds;content=fingerprint;since=now;active=true;}return now-since>=delay;}
}

static class Native {
 [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h, out RECT r);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetWindowText(IntPtr h, StringBuilder s,int n);
 [DllImport("user32.dll")] internal static extern bool SetWindowDisplayAffinity(IntPtr h,uint a);
 [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint m,uint k);
 [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr h);
 [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
 internal delegate bool EnumWindowProc(IntPtr hwnd,IntPtr param);
 [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowProc callback,IntPtr param);
 [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hwnd);
 [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr hwnd,uint command);
 [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int w,int h,uint flags);
 [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out RECT value,int size);
 [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int GetCloaked(IntPtr hwnd,int attribute,out int value,int size);
 [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 internal static bool IsWhatsApp(IntPtr hwnd){if(hwnd==IntPtr.Zero || !IsWindowVisible(hwnd) || IsIconic(hwnd))return false;var title=new StringBuilder(512);GetWindowText(hwnd,title,512);return title.ToString().EndsWith("WhatsApp",StringComparison.OrdinalIgnoreCase);}
 internal static IntPtr FindWhatsApp(){IntPtr found=IntPtr.Zero;EnumWindows(delegate(IntPtr h,IntPtr p){if(IsWhatsApp(h)){found=h;return false;}return true;},IntPtr.Zero);return found;}
 internal static Region VisibleArea(IntPtr target,IntPtr overlay,Rectangle bounds){
  var covered=new List<Rectangle>();bool found=false;
  EnumWindows(delegate(IntPtr h,IntPtr p){
   if(h==target){found=true;return false;}if(h==overlay || !IsWindowVisible(h) || IsIconic(h))return true;
   int cloaked;if(GetCloaked(h,14,out cloaked,4)==0 && cloaked!=0)return true;
   RECT r;if(DwmGetWindowAttribute(h,9,out r,Marshal.SizeOf(typeof(RECT)))!=0 && !GetWindowRect(h,out r))return true;
   covered.Add(Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom));return true;
  },IntPtr.Zero);
  var region=Detection.Uncovered(bounds,covered);if(!found)region.MakeEmpty();return region;
 }
 internal delegate void WinEventProc(IntPtr hook,uint evt,IntPtr hwnd,int obj,int child,uint thread,uint time);
 [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr64(IntPtr h,int index,IntPtr value);
 [DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetWindowLong32(IntPtr h,int index,int value);
 internal static void SetOwner(IntPtr h,IntPtr owner){if(IntPtr.Size==8)SetWindowLongPtr64(h,-8,owner);else SetWindowLong32(h,-8,owner.ToInt32());}
 internal struct RECT { public int Left,Top,Right,Bottom; }
}

sealed class Veil : Form {
 public Bitmap Frame;
 public Veil() { FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; TopMost=false; BackColor=Color.Black; DoubleBuffered=true; }
 public void SetMasks(List<Rectangle> masks,Region visible){var region=new Region();region.MakeEmpty();foreach(var r in masks)region.Union(r);region.Intersect(visible);var old=Region;Region=region;if(old!=null)old.Dispose();}
 public void KeepAboveTarget(){IntPtr previous=Native.GetWindow(Target,3);if(previous==Handle)previous=Native.GetWindow(Handle,3);Native.SetWindowPos(Handle,previous,0,0,0,0,0x213);}
 public IntPtr Target;
 public void AttachTo(IntPtr target){if(Target==target)return;Hide();Native.SetOwner(Handle,target);Target=target;}
 protected override bool ShowWithoutActivation { get {return true;} }
 protected override CreateParams CreateParams { get {var p=base.CreateParams; p.ExStyle|=0x08000000|0x20|0x80; return p;} }
 protected override void WndProc(ref Message m) { if(m.Msg==0x84) {m.Result=new IntPtr(-1);return;} base.WndProc(ref m); }
 protected override void OnPaint(PaintEventArgs e) { if(Frame!=null)e.Graphics.DrawImageUnscaled(Frame,0,0); }
 public void ClearFrame() { Hide(); if(Frame!=null){Frame.Dispose();Frame=null;} }
 protected override void Dispose(bool disposing) {if(disposing && Frame!=null)Frame.Dispose();base.Dispose(disposing);}
}

// Read pixels directly while the bitmap is locked; avoid thousands of GDI calls
// on every frame, which previously delayed the overlay while scrolling.
unsafe sealed class PixelView : IDisposable {
 readonly Bitmap bitmap; readonly BitmapData data;
 public int Width {get{return bitmap.Width;}} public int Height {get{return bitmap.Height;}}
 public Size Size {get{return bitmap.Size;}}
 public PixelView(Bitmap b){bitmap=b;data=b.LockBits(new Rectangle(Point.Empty,b.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);}
 public Color GetPixel(int x,int y){byte* p=(byte*)data.Scan0+y*data.Stride+x*4;return Color.FromArgb(p[2],p[1],p[0]);}
 public void Dispose(){bitmap.UnlockBits(data);}
}

static class Detection {
 public static int Fingerprint(Bitmap source,Rectangle row){unchecked{int hash=17;using(var pixels=new PixelView(source))for(int y=row.Top;y<row.Bottom;y+=3)for(int x=row.Left;x<row.Right;x+=11)hash=hash*31+pixels.GetPixel(x,y).ToArgb();return hash;}}
 public static bool CanDisplay(IntPtr target,bool visible,bool minimized,bool switching,bool altDown){return target!=IntPtr.Zero && visible && !minimized && !switching && !altDown;}
 public static Region Uncovered(Rectangle bounds,List<Rectangle> covered){var r=new Region(bounds);foreach(var box in covered)r.Exclude(box);return r;}
 public static Rectangle VisibleWindow(Rectangle window,Rectangle desktop) {
  return Rectangle.Intersect(window,desktop);
 }
 // The chat/conversation divider is a thin, continuous vertical border.
 // Reject text, rounded cards and wallpaper by requiring consistent contrast
 // and colour over most of the window height, on both sides of the line.
 public static int SidebarEdge(Bitmap b,float scale) {using(var pixels=new PixelView(b))return SidebarEdge(pixels,scale);}
 public static bool SidebarEdgeAt(Bitmap b,int x,float scale){using(var pixels=new PixelView(b))return EdgeScore(pixels,x,scale)>0;}
 static double EdgeScore(PixelView b,int x,float scale){
  int y0=(int)(50*scale),y1=b.Height-(int)(35*scale),delta=Math.Max(2,(int)(2*scale));
  if(x<delta || x+delta>=b.Width || y0>=y1)return -1;
  Color anchor=b.GetPixel(x,y0),firstLeft=b.GetPixel(x-delta,y0),firstRight=b.GetPixel(x+delta,y0);
  // Almost all columns are flat backgrounds. Reject them before scanning height.
  if(Difference(anchor,firstLeft)<18 || Difference(anchor,firstRight)<18)return -1;
  int samples=0,hits=0;double contrast=0;
  for(int y=y0;y<y1;y+=5){
   Color c=b.GetPixel(x,y),l=b.GetPixel(x-delta,y),r=b.GetPixel(x+delta,y);
   int dl=Difference(c,l),dr=Difference(c,r);
   samples++;if(Difference(c,anchor)<=15 && dl>=18 && dr>=18){hits++;contrast+=Math.Min(dl,dr);}
  }
  return samples>=10 && hits>=samples*0.80?(double)hits/samples*100+contrast/hits:-1;
 }
 static int Difference(Color a,Color b){return Math.Abs(a.R-b.R)+Math.Abs(a.G-b.G)+Math.Abs(a.B-b.B);}
 static int SidebarEdge(PixelView b,float scale) {
  int begin=(int)(265*scale),end=b.Width-(int)(20*scale);
  int best=-1;double bestScore=0;
  int delta=Math.Max(2,(int)(2*scale));
  for(int x=begin;x<end-delta;x++) {
   double score=EdgeScore(b,x,scale);
   if(score>bestScore){bestScore=score;best=x;}
  }
  return best;
 }
 // Find text bands, then select the lower band of a name/preview pair.
 public static List<Rectangle> Find(Bitmap b,float scale){using(var pixels=new PixelView(b))return Find(pixels,scale);}
 static List<Rectangle> Find(PixelView b, float scale) {
  var bands=new List<Rectangle>(); int start=-1,last=-1;
  int limit=Math.Min(b.Width,Math.Max(60,(int)(100*scale)));
  for(int y=0;y<b.Height;y++) {
   // The far right can contain pin/unread icons. Use a local background sample.
   Color bg=b.GetPixel(Math.Max(0,b.Width-3),y); int count=0;
   for(int x=0;x<limit;x++) {Color c=b.GetPixel(x,y); if(Math.Abs(c.R-bg.R)+Math.Abs(c.G-bg.G)+Math.Abs(c.B-bg.B)>190)count++;}
   if(count>=3) {if(start<0)start=y; last=y;}
   if(start>=0 && (y-last>3*scale || y==b.Height-1)) {if(last-start>=4*scale)bands.Add(new Rectangle(0,start,b.Width,last-start+1));start=-1;}
  }
  var result=new List<Rectangle>();
  for(int i=1;i<bands.Count;i++) {
   float gap=(bands[i].Top-bands[i-1].Top)/scale;
   float prior=i>1 ? (bands[i-1].Top-bands[i-2].Top)/scale : 100;
   if(gap>=15 && gap<=33 && prior>33) {
    var r=bands[i];r.Inflate(0,(int)(3*scale));r.Intersect(new Rectangle(Point.Empty,b.Size));result.Add(r);
   }
  }
  // Preserve a preview clipped by the upper list boundary during scrolling.
  if(result.Count>=2){int step=result[1].Top-result[0].Top;if(step>45*scale && step<110*scale){var previous=result[0];previous.Y-=step;previous.Intersect(new Rectangle(Point.Empty,b.Size));if(previous.Height>0)result.Insert(0,previous);}}
  return result;
 }
 public static void Blur(Graphics g,Bitmap source,Rectangle r,int strength) {
  // Three box-filter passes approximate Gaussian blur, without a colour fill,
  // resampling fringe or chroma-key transparency bleeding into the image.
  using(var crop=source.Clone(r,PixelFormat.Format32bppArgb)) {
   int w=crop.Width,h=crop.Height;var data=crop.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
   try {
    int bytes=w*h*4;if(scratchA==null || scratchA.Length<bytes){scratchA=new byte[bytes];scratchB=new byte[bytes];}
    byte[] a=scratchA,b=scratchB;
    for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),a,y*w*4,w*4);
    int radius=Math.Max(2,strength/3);
    for(int pass=0;pass<3;pass++){BoxPass(a,b,w,h,radius,true);BoxPass(b,a,w,h,radius,false);}
    for(int y=0;y<h;y++)Marshal.Copy(a,y*w*4,IntPtr.Add(data.Scan0,y*data.Stride),w*4);
   }finally{crop.UnlockBits(data);}
   g.DrawImageUnscaled(crop,r.Location);
  }
 }
 [ThreadStatic] static byte[] scratchA,scratchB;
 static void BoxPass(byte[] input,byte[] output,int w,int h,int radius,bool horizontal){
  int length=horizontal?w:h,lines=horizontal?h:w,step=horizontal?4:w*4,divisor=radius*2+1;
  for(int line=0;line<lines;line++)for(int channel=0;channel<3;channel++){
   int origin=(horizontal?line*w*4:line*4)+channel,sum=0;
   for(int k=-radius;k<=radius;k++)sum+=input[origin+Math.Max(0,Math.Min(length-1,k))*step];
   for(int p=0;p<length;p++){
    output[origin+p*step]=(byte)(sum/divisor);output[origin-channel+p*step+3]=255;
    sum+=input[origin+Math.Min(length-1,p+radius+1)*step]-input[origin+Math.Max(0,p-radius)*step];
   }
  }
 }
}

sealed class ControlPanel : Form {
 readonly Veil veil=new Veil(); readonly Timer timer=new Timer();
 readonly Label status=new Label(); readonly CheckBox enabled=new ToggleSwitch(),hover=new ToggleSwitch(),automatic=new ToggleSwitch(),autoWidth=new ToggleSwitch();
 readonly CheckBox startWindows=new ToggleSwitch(),startHidden=new ToggleSwitch();readonly NumericUpDown hoverDelay=new NumericUpDown();
 readonly HoverGate hoverGate=new HoverGate();readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
 public bool StartHidden {get{return startHidden.Checked;}}
 bool suppressInitialShow;
 public void PrepareHiddenStart(){suppressInitialShow=true;ShowInTaskbar=false;}
 protected override void SetVisibleCore(bool value){if(value && suppressInitialShow){suppressInitialShow=false;base.SetVisibleCore(false);return;}base.SetVisibleCore(value);}
 protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);int dark=1;Native.DwmSetWindowAttribute(Handle,20,ref dark,4);}
 void OpenSettings(){suppressInitialShow=false;ShowInTaskbar=true;Show();Activate();}
 readonly NumericUpDown left=new NumericUpDown(),right=new NumericUpDown(),top=new NumericUpDown(),first=new NumericUpDown(),pitch=new NumericUpDown(),height=new NumericUpDown(),strength=new NumericUpDown();
 readonly string config=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.txt");
 readonly NotifyIcon tray=new NotifyIcon(); bool captureReady,initialized,hotkeyRegistered; string error="";
 readonly Label shortcut=new Label();
 Native.WinEventProc focusCallback; IntPtr focusHook,switchHook; bool switching;
 IntPtr trackedTarget;int cachedEdge=-1;
 readonly Color muted=Theme.Muted,card=Theme.Card,accent=Theme.Accent;
 public ControlPanel() {
  Text="Preview Blur"; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi; ClientSize=new Size(560,744);
  FormBorderStyle=FormBorderStyle.FixedSingle; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen;
  BackColor=Theme.Background; ForeColor=Theme.Text; DoubleBuffered=true;
  LabelText(this,"PREVIEW BLUR",24,24,400,22,9,accent);
  LabelText(this,"Privasi, tanpa ribet.",22,53,510,45,25,ForeColor);
  LabelText(this,"Atur sekali. Buka WhatsApp seperti biasa.",24,103,510,25,10,muted);
  var main=new Panel{Location=new Point(24,194),Size=new Size(512,422),BackColor=BackColor};Controls.Add(main);
  var advanced=new Panel{Location=main.Location,Size=main.Size,BackColor=BackColor,Visible=false,AutoScroll=true};Controls.Add(advanced);
  var mainTab=(ModernButton)ActionButton("Umum",24,146,248,false);var advancedTab=(ModernButton)ActionButton("Lanjutan",288,146,248,false);
  mainTab.Selected=true;
  mainTab.Click+=delegate{main.Visible=true;advanced.Visible=false;mainTab.Selected=true;advancedTab.Selected=false;mainTab.Invalidate();advancedTab.Invalidate();};
  advancedTab.Click+=delegate{main.Visible=false;advanced.Visible=true;advancedTab.Selected=true;mainTab.Selected=false;mainTab.Invalidate();advancedTab.Invalidate();};
  var privacy=Card(main,0,0,512,128);var behavior=Card(main,0,142,512,128);var startup=Card(main,0,284,512,138);
  ConfigureCheck(privacy,enabled,"Lindungi preview chat",12);((ToggleSwitch)enabled).Description="Samarkan pesan di bawah nama kontak.";
  NumberRow(privacy,"Kekuatan blur",strength,14,6,35,82);
  ConfigureCheck(behavior,hover,"Lihat saat kursor diam",12);((ToggleSwitch)hover).Description="Preview tertutup lagi saat kursor menjauh.";
  NumberRow(behavior,"Jeda sebelum terbuka (ms)",hoverDelay,500,200,2000,82);hoverDelay.Increment=100;
  ConfigureCheck(startup,startWindows,"Mulai bersama Windows",10);((ToggleSwitch)startWindows).Description="Siap otomatis setiap kali kamu masuk.";
  ConfigureCheck(startup,startHidden,"Mulai di latar belakang",75);((ToggleSwitch)startHidden).Description="Buka pengaturan dari ikon dekat jam.";
  try{startWindows.Checked=StartupSetting.Enabled();}catch{startWindows.Checked=false;}
  var detection=Card(advanced,0,0,490,140);var calibration=Card(advanced,0,154,490,354);
  ConfigureCheck(detection,autoWidth,"Ikuti lebar sidebar",10);((ToggleSwitch)autoWidth).Description="Sesuaikan blur saat pembatas digeser.";
  ConfigureCheck(detection,automatic,"Deteksi baris otomatis",76);((ToggleSwitch)automatic).Description="Mengikuti posisi preview saat scroll.";
  LabelText(calibration,"Kalibrasi manual",20,17,440,24,11,ForeColor);
  LabelText(calibration,"Gunakan jika letak blur perlu disesuaikan.",20,45,440,24,9,muted);
  NumberRow(calibration,"Batas kiri teks",left,151,0,2000,88);
  NumberRow(calibration,"Batas kanan teks",right,310,20,3000,130);
  NumberRow(calibration,"Awal daftar chat",top,200,0,2000,172);
  NumberRow(calibration,"Preview pertama",first,247,0,2000,214);
  NumberRow(calibration,"Jarak antar chat",pitch,76,30,200,256);
  NumberRow(calibration,"Tinggi blur",height,23,10,60,298);
  LabelText(advanced,"Kalibrasi dalam piksel pada skala layar 100%.",8,524,472,28,9,muted);
  autoWidth.CheckedChanged+=delegate{right.Enabled=!autoWidth.Checked;};
  automatic.CheckedChanged+=delegate{first.Enabled=pitch.Enabled=height.Enabled=!automatic.Checked;};
  right.Enabled=false;first.Enabled=pitch.Enabled=height.Enabled=false;
  hover.CheckedChanged+=delegate{hoverDelay.Enabled=hover.Checked;};
  var save=ActionButton("Simpan perubahan",24,636,248,true);save.Click+=delegate{Save();};
  var hide=ActionButton("Selesai",288,636,248,false);hide.Click+=delegate{Hide();};
  status.SetBounds(24,695,512,20);status.Font=new Font("Segoe UI",9);status.ForeColor=muted;status.AutoEllipsis=true;Controls.Add(status);
  shortcut.SetBounds(24,719,450,20);shortcut.Font=new Font("Segoe UI",8);shortcut.ForeColor=muted;Controls.Add(shortcut);
  LabelText(this,"v0.10",487,719,50,20,8,muted);
  LoadConfig();
  hoverDelay.Enabled=hover.Checked;
  tray.Icon=SystemIcons.Shield;tray.Text="Preview Blur";tray.Visible=true;
  var menu=new ContextMenuStrip();menu.Items.Add("Buka pengaturan",null,delegate{OpenSettings();});menu.Items.Add("Aktif / nonaktif",null,delegate{enabled.Checked=!enabled.Checked;});menu.Items.Add("Keluar",null,delegate{Close();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{OpenSettings();};
  timer.Interval=15;timer.Tick+=Tick;timer.Start();
 }
 public void StartRuntime(){
   if(initialized)return;initialized=true;
   IntPtr panelHandle=Handle;captureReady=Native.SetWindowDisplayAffinity(veil.Handle,0x11);
   focusCallback=OnWindowEvent;
   focusHook=Native.SetWinEventHook(3,3,IntPtr.Zero,focusCallback,0,0,0);
   switchHook=Native.SetWinEventHook(0x14,0x15,IntPtr.Zero,focusCallback,0,0,0);
   Native.UnregisterHotKey(Handle,1);
   hotkeyRegistered=Native.RegisterHotKey(Handle,1,0x4003,0x42);
   shortcut.Text="Ctrl + Alt + B untuk aktif / nonaktif";
   if(!hotkeyRegistered){hotkeyRegistered=Native.RegisterHotKey(Handle,1,0x4006,0x42);shortcut.Text=hotkeyRegistered?"Ctrl + Shift + B untuk aktif / nonaktif":"Pintasan tidak tersedia. Gunakan tombol Aktifkan blur.";}
 }
 void LabelText(Control parent,string text,int x,int y,int w,int h,int size,Color color){parent.Controls.Add(new Label{Text=text,Location=new Point(x,y),Size=new Size(w,h),Font=new Font("Segoe UI",size,size>20?FontStyle.Bold:FontStyle.Regular),ForeColor=color,BackColor=Color.Transparent});}
 SettingsCard Card(Control parent,int x,int y,int width,int h){var c=new SettingsCard{Location=new Point(x,y),Size=new Size(width,h)};parent.Controls.Add(c);return c;}
 Button ActionButton(string text,int x,int y,int width,bool primary){var b=new ModernButton{Text=text,Location=new Point(x,y),Size=new Size(width,42),Primary=primary,Font=new Font("Segoe UI",10,FontStyle.Bold)};Controls.Add(b);return b;}
 void ConfigureCheck(Control parent,CheckBox check,string text,int y){check.Text=text;check.AccessibleName=text;check.Checked=true;check.SetBounds(20,y,parent.Width-40,54);parent.Controls.Add(check);}
 void NumberRow(Control parent,string text,NumericUpDown n,int value,int min,int max,int y){LabelText(parent,text,20,y+4,parent.Width-155,28,10,muted);n.Minimum=min;n.Maximum=max;n.Value=value;n.SetBounds(parent.Width-118,y,98,28);n.BackColor=Color.FromArgb(36,43,52);n.ForeColor=ForeColor;n.BorderStyle=BorderStyle.FixedSingle;n.TextAlign=HorizontalAlignment.Center;n.AccessibleName=text;parent.Controls.Add(n);}
 NumericUpDown[] Numbers(){return new[]{left,right,top,first,pitch,height,strength};}
 CheckBox[] Checks(){return new[]{enabled,hover,automatic,autoWidth,startHidden};}
 void LoadConfig(){try{if(!File.Exists(config))return;var lines=File.ReadAllLines(config);var ns=Numbers();for(int i=0;i<ns.Length && i<lines.Length;i++){decimal v;if(decimal.TryParse(lines[i],out v))ns[i].Value=Math.Max(ns[i].Minimum,Math.Min(ns[i].Maximum,v));}var checks=Checks();for(int i=0;i<checks.Length && ns.Length+i<lines.Length;i++){bool v;if(bool.TryParse(lines[ns.Length+i],out v))checks[i].Checked=v;}foreach(string line in lines)if(line.StartsWith("hoverDelayMs=")){decimal v;if(decimal.TryParse(line.Substring(13),out v))hoverDelay.Value=Math.Max(hoverDelay.Minimum,Math.Min(hoverDelay.Maximum,v));}}catch{error="Pengaturan gagal dibaca; memakai nilai awal.";}}
 void Save(){try{var lines=new List<string>();foreach(var n in Numbers())lines.Add(n.Value.ToString());foreach(var c in Checks())lines.Add(c.Checked.ToString());lines.Add("hoverDelayMs="+hoverDelay.Value);File.WriteAllLines(config,lines.ToArray());error="Tersimpan.";}catch{error="Tidak bisa menyimpan di folder aplikasi.";return;}try{StartupSetting.Apply(startWindows.Checked);}catch{error="Tersimpan; pengaturan mulai bersama Windows gagal.";}}
 public void RenderPreview(string path,bool calibration){initialized=true;Opacity=0;Show();timer.Stop();status.Text="Siap melindungi preview chat.";shortcut.Text="Ctrl + Alt + B untuk aktif / nonaktif";if(calibration){foreach(Control c in Controls)if(c is Button && c.Text=="Lanjutan")((Button)c).PerformClick();}using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(path,ImageFormat.Png);}}
 protected override void WndProc(ref Message m){if(m.Msg==0x312 && m.WParam.ToInt32()==1)enabled.Checked=!enabled.Checked;base.WndProc(ref m);}
 void OnWindowEvent(IntPtr hook,uint evt,IntPtr hwnd,int obj,int child,uint thread,uint time){
  if(IsDisposed || Disposing)return;
  if(evt==0x14)switching=true;
  if(evt==0x15)switching=false;
  if(!Detection.CanDisplay(veil.Target,Native.IsWindowVisible(veil.Target),Native.IsIconic(veil.Target),switching,(Native.GetAsyncKeyState(0x12)&0x8000)!=0))veil.ClearFrame();
 }
 void Tick(object sender,EventArgs e) {
  try {
   IntPtr target=Native.GetForegroundWindow();
   if(!Native.IsWhatsApp(target))target=Native.IsWhatsApp(trackedTarget)?trackedTarget:Native.FindWhatsApp();
   if(target!=trackedTarget){trackedTarget=target;cachedEdge=-1;hoverGate.Reset();}
   if(!enabled.Checked || !Detection.CanDisplay(target,Native.IsWindowVisible(target),Native.IsIconic(target),switching,(Native.GetAsyncKeyState(0x12)&0x8000)!=0)){hoverGate.Reset();veil.ClearFrame();status.Text=enabled.Checked?"Menunggu jendela WhatsApp. "+error:"Blur dinonaktifkan.";return;}
   Native.RECT wr;if(!Native.GetWindowRect(target,out wr))return;
   float scale=Native.GetDpiForWindow(target)/96f;if(scale<=0)scale=1;
   int x=wr.Left+(int)(left.Value*(decimal)scale),y=wr.Top+(int)(top.Value*(decimal)scale);
   int edge=-1;
   if(autoWidth.Checked) {
    // Revalidate only a narrow strip at the known divider during scrolling.
    // A moved divider immediately falls back to the complete scan below.
    int radius=Math.Max(3,(int)(3*scale));
    var stripArea=new Rectangle(wr.Left+cachedEdge-radius,Math.Max(wr.Top,SystemInformation.VirtualScreen.Top),radius*2+1,Math.Min(wr.Bottom,SystemInformation.VirtualScreen.Bottom)-Math.Max(wr.Top,SystemInformation.VirtualScreen.Top));
    if(cachedEdge>0 && stripArea.Height>150 && SystemInformation.VirtualScreen.Contains(stripArea))using(var strip=new Bitmap(stripArea.Width,stripArea.Height)){
     using(var g=Graphics.FromImage(strip))g.CopyFromScreen(stripArea.Location,Point.Empty,strip.Size,CopyPixelOperation.SourceCopy);
     if(Detection.SidebarEdgeAt(strip,radius,scale))edge=cachedEdge;
    }
    // Maximized windows have invisible borders outside the monitor (often -8px).
    // Capture the visible intersection instead of skipping detection altogether.
    var windowArea=Detection.VisibleWindow(Rectangle.FromLTRB(wr.Left,wr.Top,wr.Right,wr.Bottom),SystemInformation.VirtualScreen);
    if(edge<0 && windowArea.Width>300 && windowArea.Height>200) {
     using(var layout=new Bitmap(windowArea.Width,windowArea.Height)) {
      using(var g=Graphics.FromImage(layout))g.CopyFromScreen(windowArea.Location,Point.Empty,layout.Size,CopyPixelOperation.SourceCopy);
      edge=Detection.SidebarEdge(layout,scale);
      if(edge>0){edge+=windowArea.Left-wr.Left;cachedEdge=edge;}
     }
    }
   }
   if(edge<0 && cachedEdge>0 && cachedEdge<wr.Right-wr.Left)edge=cachedEdge;
   int rightPixel=autoWidth.Checked && edge>0?edge-(int)(20*scale):(int)(right.Value*(decimal)scale);
   int w=rightPixel-(x-wr.Left),h=Math.Min(wr.Bottom-8,SystemInformation.VirtualScreen.Bottom)-y;
   var area=Rectangle.Intersect(new Rectangle(x,y,Math.Max(1,w),Math.Max(1,h)),SystemInformation.VirtualScreen);
   if(w<30 || h<30 || area.Width!=w || area.Height!=h){veil.ClearFrame();status.Text="Sesuaikan batas teks dengan ukuran jendela.";return;}
   if(!captureReady){veil.ClearFrame();status.Text="Windows tidak mendukung lapisan blur ini.";return;}
   using(var visible=Native.VisibleArea(target,veil.Handle,new Rectangle(x,y,w,h)))
   using(var source=new Bitmap(w,h)) {
    visible.Translate(-x,-y);
    using(var g=Graphics.FromImage(source))g.CopyFromScreen(x,y,0,0,source.Size,CopyPixelOperation.SourceCopy);
    // Covered pixels belong to another application. Never sample them as chat text.
    using(var covered=new Region(new Rectangle(0,0,w,h)))using(var g=Graphics.FromImage(source))using(var background=new SolidBrush(Color.FromArgb(22,23,23))){covered.Exclude(visible);g.SetClip(covered,System.Drawing.Drawing2D.CombineMode.Replace);g.FillRectangle(background,0,0,w,h);}
    List<Rectangle> rows;
    if(automatic.Checked) rows=Detection.Find(source,scale);
    else {rows=new List<Rectangle>();for(int row=(int)((first.Value-top.Value)*(decimal)scale);row<h;row+=(int)(pitch.Value*(decimal)scale)){var r=Rectangle.Intersect(new Rectangle(0,row,w,(int)(height.Value*(decimal)scale)),new Rectangle(0,0,w,h));if(r.Height>0)rows.Add(r);}}
    Rectangle? reveal=null;Point pointer=Cursor.Position;bool candidate=false;
    if(hover.Checked && Native.GetForegroundWindow()==target && visible.IsVisible(pointer.X-x,pointer.Y-y))foreach(var r in rows){var screenRow=new Rectangle(x+r.X,y+r.Y,r.Width,r.Height);if(screenRow.Contains(pointer)){candidate=true;if(hoverGate.Observe(pointer,screenRow,clock.ElapsedMilliseconds,(int)hoverDelay.Value,Detection.Fingerprint(source,r)))reveal=r;break;}}
    if(!candidate)hoverGate.Reset();
    var masks=new List<Rectangle>();var frame=new Bitmap(w,h);using(var g=Graphics.FromImage(frame)){foreach(var r in rows){if(reveal.HasValue && r==reveal.Value)continue;Detection.Blur(g,source,r,(int)strength.Value);masks.Add(r);}}
    if(!Detection.CanDisplay(target,Native.IsWindowVisible(target),Native.IsIconic(target),switching,(Native.GetAsyncKeyState(0x12)&0x8000)!=0)){frame.Dispose();veil.ClearFrame();return;}
    veil.AttachTo(target);
    using(var latestVisible=Native.VisibleArea(target,veil.Handle,new Rectangle(x,y,w,h))){latestVisible.Translate(-x,-y);visible.Intersect(latestVisible);}
    veil.SuspendLayout();var old=veil.Frame;veil.Frame=frame;veil.Bounds=new Rectangle(x,y,w,h);veil.SetMasks(masks,visible);veil.ResumeLayout();if(!veil.Visible)veil.Show();veil.KeepAboveTarget();veil.Refresh();if(old!=null)old.Dispose();
    if(!Detection.CanDisplay(target,Native.IsWindowVisible(target),Native.IsIconic(target),switching,(Native.GetAsyncKeyState(0x12)&0x8000)!=0))veil.ClearFrame();
    status.Text=rows.Count+" preview; "+(edge>0?"lebar mengikuti sidebar.":"memakai batas kanan manual.")+" "+error;
   }
  } catch(Exception ex){veil.ClearFrame();status.Text="Blur berhenti: "+ex.Message;}
 }
 protected override void OnFormClosed(FormClosedEventArgs e){timer.Stop();timer.Dispose();if(focusHook!=IntPtr.Zero)Native.UnhookWinEvent(focusHook);if(switchHook!=IntPtr.Zero)Native.UnhookWinEvent(switchHook);Native.UnregisterHotKey(Handle,1);tray.Visible=false;tray.Dispose();veil.Dispose();base.OnFormClosed(e);}
}

static class Program {
 [STAThread] static void Main(string[] args) {
  if(args.Length>0 && args[0]=="--self-test"){try{SelfTest();}catch(Exception ex){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"FAIL: "+ex.Message);Environment.ExitCode=1;}return;}
  if(args.Length>1 && args[0]=="--test-sidebar") {using(var b=new Bitmap(args[1]))File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"sidebar-test-result.txt"),Detection.SidebarEdge(b,1).ToString());return;}
  Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>0 && args[0]=="--enable-startup"){StartupSetting.Apply(true);return;}
  if(args.Length>0 && args[0]=="--refresh-startup"){if(StartupSetting.Enabled())StartupSetting.Apply(true);return;}
  if(args.Length>0 && args[0]=="--test-tray"){
   using(var panel=new ControlPanel()){
    panel.StartRuntime();panel.PrepareHiddenStart();using(var stop=new Timer()){stop.Interval=100;stop.Tick+=delegate{bool hidden=!panel.Visible;File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tray-test-result.txt"),hidden?"PASS: hidden startup; message loop running; panel handle initialized without showing a window.":"FAIL: panel became visible");stop.Stop();panel.Close();};stop.Start();Application.Run(new ApplicationContext(panel));}
   }return;
  }
  if(args.Length>1 && args[0]=="--preview"){using(var panel=new ControlPanel())panel.RenderPreview(args[1],args.Length>2);return;}
  bool created;using(var mutex=new System.Threading.Mutex(true,"Local\\WhatsAppPreviewBlurPrototype",out created)){if(!created){if(Array.IndexOf(args,"--tray")<0)MessageBox.Show("Preview Blur versi lain masih berjalan. Pilih Keluar dari ikon Preview Blur di dekat jam Windows, lalu buka versi ini lagi.","Preview Blur v10");return;}using(var panel=new ControlPanel()){panel.StartRuntime();var context=new ApplicationContext(panel);if(Array.IndexOf(args,"--settings")>=0 || (!panel.StartHidden && Array.IndexOf(args,"--tray")<0))panel.Show();else panel.PrepareHiddenStart();Application.Run(context);}}
 }
 static void SelfTest(){
  var gate=new HoverGate();var point=new Point(20,20);var rowBox=new Rectangle(0,10,100,25);
  if(gate.Observe(point,rowBox,0,500) || gate.Observe(point,rowBox,499,500) || !gate.Observe(point,rowBox,500,500))throw new Exception("Hover delay boundary failed");
  if(gate.Observe(new Point(21,20),rowBox,510,500))throw new Exception("Cursor motion did not reset delay");
  if(gate.Observe(new Point(21,20),new Rectangle(0,9,100,25),1010,500))throw new Exception("Scroll did not reset hover delay");
  gate.Reset();if(gate.Observe(point,rowBox,2000,500,1) || gate.Observe(point,rowBox,2600,500,2))throw new Exception("Changed chat did not reset delay");
  if(!StartupSetting.Command(@"D:\My Document\PreviewBlur.exe").Equals("\"D:\\My Document\\PreviewBlur.exe\" --tray"))throw new Exception("Startup command quoting failed");
  for(int offset=0;offset<152;offset++)using(var image=new Bitmap(350,400)){
   using(var g=Graphics.FromImage(image)){g.Clear(Color.FromArgb(22,22,22));for(int row=0;row<9;row++){g.FillRectangle(Brushes.White,4,15+row*76-offset,65,10);g.FillRectangle(Brushes.LightGray,4,39+row*76-offset,85,10);}}
   var detected=Detection.Find(image,1);
   for(int row=0;row<9;row++){int py=39+row*76-offset;if(py<0 || py+10>400)continue;bool covered=false;foreach(var r in detected)if(r.Top<=py && r.Bottom>=py+10)covered=true;if(!covered)throw new Exception("Scroll tracking missed preview at offset "+offset+", y="+py);}
  }
  using(var neutral=new Bitmap(100,25))using(var output=new Bitmap(100,25)){
   using(var g=Graphics.FromImage(neutral)){g.Clear(Color.FromArgb(25,25,25));g.FillRectangle(Brushes.White,40,7,10,10);}
   using(var g=Graphics.FromImage(output))Detection.Blur(g,neutral,new Rectangle(0,0,100,25),14);
   for(int y=0;y<25;y++)for(int x=0;x<100;x++){var c=output.GetPixel(x,y);if(c.R!=c.G || c.G!=c.B)throw new Exception("Blur introduced colour");}
   if(output.GetPixel(39,12).R<=25 || output.GetPixel(45,12).R>=255)throw new Exception("Blur did not smooth edges");
  }
  var target=new IntPtr(101);
  if(!Detection.CanDisplay(target,true,false,false,false))throw new Exception("Visible background WhatsApp rejected");
  if(Detection.CanDisplay(target,false,false,false,false) || Detection.CanDisplay(target,true,true,false,false) || Detection.CanDisplay(target,true,false,true,false) || Detection.CanDisplay(target,true,false,false,true) || Detection.CanDisplay(IntPtr.Zero,true,false,false,false))throw new Exception("Visibility guard failed");
  using(var visible=Detection.Uncovered(new Rectangle(0,0,500,800),new List<Rectangle>{new Rectangle(200,100,700,400)})){
   if(!visible.IsVisible(150,200) || visible.IsVisible(250,200) || !visible.IsVisible(250,600))throw new Exception("Partial occlusion clipping failed");
  }
  using(var visible=Detection.Uncovered(new Rectangle(0,0,500,800),new List<Rectangle>{new Rectangle(0,0,1920,1200)}))using(var g=Graphics.FromHwnd(IntPtr.Zero)){if(!visible.IsEmpty(g))throw new Exception("Fully covered window still has overlay");}
  var clipped=Detection.VisibleWindow(new Rectangle(-8,-8,1936,1216),new Rectangle(0,0,1920,1200));
  if(clipped!=new Rectangle(0,0,1920,1200))throw new Exception("Maximized window clipping failed");
  if(clipped.Left-(-8)+638!=646)throw new Exception("Capture coordinate offset failed");
  var secondary=Detection.VisibleWindow(new Rectangle(-1928,-8,1936,1096),new Rectangle(-1920,0,3840,1200));
  if(secondary.Left!=-1920 || secondary.Top!=0)throw new Exception("Secondary monitor clipping failed");
  foreach(int width in new[]{330,528,638})using(var b=new Bitmap(1000,650)) {
   using(var g=Graphics.FromImage(b)){g.Clear(Color.FromArgb(22,22,22));g.DrawLine(Pens.DimGray,width,40,width,649);g.DrawString("sample text",SystemFonts.DefaultFont,Brushes.White,151,220);}
   if(Detection.SidebarEdge(b,1)!=width)throw new Exception("Sidebar resize detection failed: "+width);
   using(var strip=b.Clone(new Rectangle(width-3,0,7,650),PixelFormat.Format32bppArgb)){if(!Detection.SidebarEdgeAt(strip,3,1))throw new Exception("Cached divider validation failed");}
   using(var strip=b.Clone(new Rectangle(width+10,0,7,650),PixelFormat.Format32bppArgb)){if(Detection.SidebarEdgeAt(strip,3,1))throw new Exception("Moved divider did not invalidate cache");}
  }
  using(var b=new Bitmap(1000,650)){using(var g=Graphics.FromImage(b))g.Clear(Color.FromArgb(22,22,22));if(Detection.SidebarEdge(b,1)!=-1)throw new Exception("Missing divider must use manual fallback");}
  using(var b=new Bitmap(190,180)) {using(var g=Graphics.FromImage(b)){g.Clear(Color.FromArgb(22,22,22));foreach(int y in new[]{15,39,91,115})g.FillRectangle(Brushes.White,4,y,60,10);}
   var rows=Detection.Find(b,1);if(rows.Count!=2 || rows[0].Top!=36 || rows[1].Top!=112)throw new Exception("Text pairing failed");
   using(var dest=new Bitmap(b.Width,b.Height))using(var g=Graphics.FromImage(dest))Detection.Blur(g,b,rows[0],14);
  }
  using(var b=new Bitmap(190,180)){using(var g=Graphics.FromImage(b))g.Clear(Color.White);if(Detection.Find(b,1).Count!=0)throw new Exception("Blank image produced previews");}
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-result.txt"),"PASS: 500ms hover boundary; cursor motion, scroll and changed-chat hover resets; quoted startup command; cached divider validation and resize invalidation; visible background window; partial and full occlusion clipping; hidden/minimized window; Alt-tab guards; 152 scroll offsets; no added colour; blur smooths edges; maximized and secondary monitor geometry; sidebar resize; text pairing.");
 }
}
