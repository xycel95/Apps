using System.Drawing.Drawing2D;
using System.ComponentModel;
using Sintonia.Core;

namespace Sintonia.App;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(15,19,26), Surface = Color.FromArgb(23,29,39), Raised = Color.FromArgb(33,42,55), Border = Color.FromArgb(46,58,73), Text = Color.FromArgb(237,242,247), Muted = Color.FromArgb(153,169,188), Accent = Color.FromArgb(95,226,195);
    public static Font Font(float size=10, FontStyle style=FontStyle.Regular) => new("Segoe UI",size,style);
    public static Button Button(string text, EventHandler? action=null, bool accent=false, int width=110)
    {
        var b = new Button { Text=text, AutoSize=false, Size=new(width,36), FlatStyle=FlatStyle.Flat, BackColor=accent?Accent:Raised, ForeColor=accent?Background:Text, Font=Font(9.5f,FontStyle.Bold), Cursor=Cursors.Hand, Margin=new(4), TabStop=true };
        b.FlatAppearance.BorderSize=0; b.FlatAppearance.MouseOverBackColor=accent?Color.FromArgb(130,240,213):Color.FromArgb(46,60,77);
        if(action is not null)b.Click+=action;
        return b;
    }
    public static Label Label(string text, float size=10, bool muted=false, FontStyle style=FontStyle.Regular) => new(){Text=text,ForeColor=muted?Muted:Text,Font=Font(size,style),AutoSize=true,BackColor=Color.Transparent,Margin=new(0,4,0,4)};
    public static TextBox Input(string placeholder="") => new(){BackColor=Raised,ForeColor=Text,Font=Font(11),BorderStyle=BorderStyle.FixedSingle,PlaceholderText=placeholder};
    public static ComboBox Combo() => new(){DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Raised,ForeColor=Text,FlatStyle=FlatStyle.Flat,Font=Font(9.5f),IntegralHeight=false,DropDownHeight=300};
    public static void Dialog(Form f,string title,int width,int height)
    {
        f.Text=title; f.ClientSize=new(width,height);f.MinimumSize=f.Size;f.MaximumSize=f.Size;f.StartPosition=FormStartPosition.CenterParent; f.FormBorderStyle=FormBorderStyle.FixedDialog;f.MaximizeBox=false;f.MinimizeBox=false;f.BackColor=Surface;f.ForeColor=Text;f.Font=Font();f.ShowInTaskbar=false;f.AutoScaleMode=AutoScaleMode.Dpi;
    }
}

internal sealed class ChannelList : ListBox
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal HashSet<string> Favorites { get; set; } = [];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string PlayingId { get; set; } = "";
    internal event Action<Channel>? FavoriteClicked;
    internal ChannelList()
    {
        DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=66;BorderStyle=BorderStyle.None;BackColor=Theme.Surface;ForeColor=Theme.Text;IntegralHeight=false;Font=Theme.Font();
        SetStyle(ControlStyles.OptimizedDoubleBuffer,true);
        AccessibleName="Canales disponibles";
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if(e.Index<0 || e.Index>=Items.Count)return;
        var channel=(Channel)Items[e.Index];var r=e.Bounds;
        using var titleFont=Theme.Font(10,FontStyle.Bold);
        using var smallFont=Theme.Font(8.5f);
        using var starFont=Theme.Font(15);
        bool selected=(e.State&DrawItemState.Selected)!=0;
        using(var brush=new SolidBrush(selected?Theme.Raised:Theme.Surface))e.Graphics.FillRectangle(brush,r);
        if(channel.Id==PlayingId) using(var mark=new SolidBrush(Theme.Accent))e.Graphics.FillRectangle(mark,r.X,r.Y+10,3,r.Height-20);
        var badge=new Rectangle(r.X+14,r.Y+13,39,39);
        using(var brush=new SolidBrush(Color.FromArgb(44,62,73)))e.Graphics.FillRectangle(brush,badge);
        var initials=string.Concat(channel.Name.Split(' ',StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p=>p[0])).ToUpperInvariant();
        TextRenderer.DrawText(e.Graphics,initials,titleFont,badge,Theme.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(e.Graphics,channel.Name,titleFont,new Rectangle(r.X+65,r.Y+12,r.Width-102,24),Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(e.Graphics,channel.Group,smallFont,new Rectangle(r.X+65,r.Y+37,r.Width-102,20),Theme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(e.Graphics,Favorites.Contains(channel.Id)?"★":"☆",starFont,new Rectangle(r.Right-33,r.Y+19,27,29),Favorites.Contains(channel.Id)?Theme.Accent:Theme.Muted,TextFormatFlags.HorizontalCenter);
        if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        var i=IndexFromPoint(e.Location);
        if(i>=0 && e.X>ClientSize.Width-38) { FavoriteClicked?.Invoke((Channel)Items[i]);return; }
        base.OnMouseDown(e);
    }
}

internal sealed class VideoSurface : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool ShowWelcome { get; set; } = true;
    internal VideoSurface(){DoubleBuffered=true;BackColor=Color.FromArgb(7,10,15);TabStop=true;AccessibleName="Vídeo en directo";}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);if(!ShowWelcome)return;
        using var titleFont=Theme.Font(36,FontStyle.Bold);using var subFont=Theme.Font(15);using var detailFont=Theme.Font(10);
        var mid=ClientSize.Height/2;
        TextRenderer.DrawText(e.Graphics,"Sintonía",titleFont,new Rectangle(10,mid-77,Width-20,68),Theme.Accent,TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(e.Graphics,"Tu televisión, a un clic.",subFont,new Rectangle(10,mid,Width-20,40),Theme.Text,TextFormatFlags.HorizontalCenter);
        TextRenderer.DrawText(e.Graphics,"Elige un canal en la lista para empezar",detailFont,new Rectangle(10,mid+43,Width-20,28),Theme.Muted,TextFormatFlags.HorizontalCenter);
    }
}
