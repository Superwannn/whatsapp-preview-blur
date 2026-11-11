using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

static class Theme {
 public static readonly Color Background=Color.FromArgb(17,20,25),Card=Color.FromArgb(27,32,39),Border=Color.FromArgb(43,50,59),Text=Color.FromArgb(238,242,246),Muted=Color.FromArgb(151,162,175),Accent=Color.FromArgb(137,235,193);
 public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}

sealed class SettingsCard : Panel {
 public SettingsCard(){DoubleBuffered=true;BackColor=Theme.Card;}
 protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Theme.Round(new RectangleF(0,0,Width-1,Height-1),14))using(var fill=new SolidBrush(BackColor))using(var border=new Pen(Theme.Border)){e.Graphics.FillPath(fill,p);e.Graphics.DrawPath(border,p);}}
}

sealed class ToggleSwitch : CheckBox {
 public string Description="";
 public ToggleSwitch(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.CheckButton;}
 protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);Invalidate();}
 protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
 protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
  float scale=g.DpiX/96f;int tw=(int)(42*scale),th=(int)(24*scale),x=Width-tw-2,y=(Height-th)/2;
  using(var track=Theme.Round(new RectangleF(x,y,tw,th),th/2f))using(var brush=new SolidBrush(!Enabled?Theme.Border:Checked?Theme.Accent:Color.FromArgb(67,77,89)))g.FillPath(brush,track);
  int diameter=th-6;using(var knob=new SolidBrush(Checked?Color.FromArgb(23,46,36):Theme.Text))g.FillEllipse(knob,Checked?x+tw-diameter-3:x+3,y+3,diameter,diameter);
  var textArea=new Rectangle(0,Description.Length==0?(Height-Font.Height)/2:5,x-16,Font.Height+4);
  TextRenderer.DrawText(g,Text,Font,textArea,Enabled?Theme.Text:Theme.Muted,TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
  if(Description.Length>0)using(var small=new Font("Segoe UI",9))TextRenderer.DrawText(g,Description,small,new Rectangle(0,27,Math.Max(1,x-12),Height-27),Theme.Muted,TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
  if(Focused && ShowFocusCues)using(var pen=new Pen(Theme.Accent))using(var path=Theme.Round(new RectangleF(x-3,y-3,tw+6,th+6),th/2f+3))g.DrawPath(pen,path);
 }
}

sealed class ModernButton : Button {
 public bool Primary,Selected;bool hovered,pressed;
 public ModernButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;}
 protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hovered=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.Clear(Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
  Color fill=Primary?(pressed?Color.FromArgb(106,204,162):hovered?Color.FromArgb(161,247,211):Theme.Accent):(hovered||Selected?Color.FromArgb(47,56,66):Theme.Card);
  using(var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),10))using(var brush=new SolidBrush(fill))using(var pen=new Pen(Focused&&ShowFocusCues?Theme.Accent:Primary?fill:Theme.Border)){g.FillPath(brush,path);g.DrawPath(pen,path);}
  TextRenderer.DrawText(g,Text,Font,ClientRectangle,Primary?Color.FromArgb(17,44,31):Selected?Theme.Text:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
 }
}
