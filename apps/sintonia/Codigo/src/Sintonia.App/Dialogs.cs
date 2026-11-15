using System.Diagnostics;
using Sintonia.Core;

namespace Sintonia.App;

internal sealed class AddSourceDialog : Form
{
    private readonly ComboBox _kind=Theme.Combo();
    private readonly TextBox _name=Theme.Input("Por ejemplo, mis canales"), _location=Theme.Input("https://…/lista.m3u");
    private readonly Button _browse;
    private readonly CheckBox _official=new(){Text="Aplicar el filtro de emisores identificados de España (opcional)",Checked=false,ForeColor=Theme.Text,AutoSize=true};
    internal PlaylistSource? Source { get; private set; }
    internal AddSourceDialog()
    {
        Theme.Dialog(this,"Añadir lista o canal",630,430);
        var title=Theme.Label("Más canales, a tu manera",19,false,FontStyle.Bold);title.SetBounds(24,20,580,36);
        var help=Theme.Label("Añade listas o emisiones oficiales que tengas autorización para utilizar.",9.5f,true);help.SetBounds(24,64,585,40);help.AutoSize=false;
        var typeLabel=Theme.Label("Tipo de fuente");typeLabel.Location=new(24,105);
        _kind.SetBounds(24,132,580,30);_kind.Items.AddRange(["Lista M3U por enlace","Archivo M3U del equipo","Emisión directa (M3U8, TS…)" ]);_kind.SelectedIndex=0;
        var nameLabel=Theme.Label("Nombre");nameLabel.Location=new(24,173);_name.SetBounds(24,199,580,30);
        var locLabel=Theme.Label("Dirección o archivo");locLabel.Location=new(24,240);_location.SetBounds(24,267,490,30);
        _browse=Theme.Button("Buscar…",(_,_)=>Browse(),false,82);_browse.SetBounds(522,263,82,36);_browse.Enabled=false;
        _official.SetBounds(24,312,580,27);
        var note=Theme.Label("Añade tu propia lista M3U/M3U8 o el enlace directo de un canal.",9,true);note.SetBounds(24,339,580,25);
        var cancel=Theme.Button("Cancelar",(_,_)=>Close(),false,100);cancel.SetBounds(382,380,100,34);
        var add=Theme.Button("Añadir",(_,_)=>Submit(),true,110);add.SetBounds(494,380,110,34);
        Controls.AddRange([title,help,typeLabel,_kind,nameLabel,_name,locLabel,_location,_browse,_official,note,cancel,add]);AcceptButton=add;CancelButton=cancel;
        _kind.SelectedIndexChanged+=(_,_)=> { _browse.Enabled=_kind.SelectedIndex==1;_official.Visible=_kind.SelectedIndex!=2;_location.PlaceholderText=_kind.SelectedIndex==2?"https://…/directo.m3u8":"https://…/lista.m3u"; };
    }
    private void Browse()
    {
        using var picker=new OpenFileDialog { Filter="Listas IPTV (*.m3u;*.m3u8)|*.m3u;*.m3u8|Todos los archivos|*.*",CheckFileExists=true };
        if(picker.ShowDialog(this)==DialogResult.OK){_location.Text=picker.FileName;if(_name.Text.Trim().Length==0)_name.Text=Path.GetFileNameWithoutExtension(picker.FileName);_official.Checked=false;}
    }
    private void Submit()
    {
        var location=_location.Text.Trim();var name=_name.Text.Trim();
        if(_kind.SelectedIndex==0)location=M3uParser.NormalizeListUrl(location);
        if(name.Length==0)name=_kind.SelectedIndex==1?Path.GetFileNameWithoutExtension(location):"Mis canales";
        string? error=null;
        if(location.Length==0)error="Introduce una dirección o selecciona un archivo.";
        else if(_kind.SelectedIndex==0&&!M3uParser.IsListUrl(location))error="La lista debe tener un enlace HTTP o HTTPS.";
        else if(_kind.SelectedIndex==1&&!File.Exists(location))error="No se encuentra ese archivo.";
        else if(_kind.SelectedIndex==2&&!M3uParser.IsStreamUrl(location))error="La dirección de la emisión no es compatible.";
        if(error is not null){MessageBox.Show(this,error,"Revisa la fuente",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        Source=new(){Name=name,Location=location,Kind=_kind.SelectedIndex==1?"file":_kind.SelectedIndex==2?"stream":"url",OfficialOnly=_kind.SelectedIndex!=2&&_official.Checked};
        DialogResult=DialogResult.OK;Close();
    }
}

internal sealed class SourcesDialog : Form
{
    private readonly ListBox _list=new(){BackColor=Theme.Raised,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=Theme.Font(10),ItemHeight=28};
    private readonly Label _detail=Theme.Label("",9,true);
    private readonly MainForm _main;
    internal SourcesDialog(MainForm main)
    {
        _main=main;Theme.Dialog(this,"Tus listas",760,470);
        var title=Theme.Label("Tus listas",20,false,FontStyle.Bold);title.SetBounds(24,20,680,38);
        var help=Theme.Label("El catálogo se actualiza al abrir Sintonía. Los favoritos se conservan.",10,true);help.SetBounds(24,66,700,28);
        _list.SetBounds(24,112,710,196);_detail.SetBounds(24,320,710,64);_detail.AutoSize=false;
        var add=Theme.Button("Añadir",async(_,_)=>{await main.AddSourceAsync(this);Reload();},true,92);add.SetBounds(24,405,92,36);
        var toggle=Theme.Button("Activar / desactivar",(_,_)=>{if(_list.SelectedItem is PlaylistSource s){s.Enabled=!s.Enabled;main.SourcesChanged();Reload();}},false,160);toggle.SetBounds(126,405,160,36);
        var remove=Theme.Button("Quitar",(_,_)=>Remove(),false,90);remove.SetBounds(296,405,90,36);
        var refresh=Theme.Button("Actualizar",async(_,_)=>{await main.RefreshSourcesAsync();Reload();},false,110);refresh.SetBounds(396,405,110,36);
        var close=Theme.Button("Cerrar",(_,_)=>Close(),false,110);close.SetBounds(624,405,110,36);
        Controls.AddRange([title,help,_list,_detail,add,toggle,remove,refresh,close]);_list.SelectedIndexChanged+=(_,_)=>Details();CancelButton=close;
        Reload();
    }
    private void Reload(){var i=_list.SelectedIndex;_list.Items.Clear();_list.Items.AddRange(_main.State.Sources.Cast<object>().ToArray());if(_list.Items.Count>0)_list.SelectedIndex=Math.Clamp(i,0,_list.Items.Count-1);}
    private void Details()
    {
        if(_list.SelectedItem is not PlaylistSource s){_detail.Text="";return;}
        _detail.Text=$"{(s.OfficialOnly?"Filtro de emisores identificados":"Lista añadida por ti")} · {(s.UpdatedAt is null?"Sin actualizar":s.UpdatedAt.Value.LocalDateTime.ToString("dd/MM/yyyy HH:mm"))}\n" + (s.LastError.Length>0?"Última actualización: "+s.LastError:s.Kind=="file"?"Archivo local. Se conserva una copia del catálogo.":s.Kind=="stream"?"Emisión directa.":"Actualización mediante enlace M3U.");
    }
    private void Remove()
    {
        if(_list.SelectedItem is not PlaylistSource s)return;
        if(MessageBox.Show(this,"¿Quitar «"+s.Name+"»? Sus favoritos se conservarán por si vuelves a añadirla.","Quitar lista",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        _main.State.Sources.Remove(s);_main.SourcesChanged();Reload();
    }
}

internal static class AboutDialog
{
    internal static void Show(Form owner)
    {
        using var f=new Form();Theme.Dialog(f,"Acerca de Sintonía",720,560);
        var title=Theme.Label("Sintonía 1.0.1",23,false,FontStyle.Bold);title.SetBounds(24,20,660,42);
        var content=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Theme.Raised,ForeColor=Theme.Text,BorderStyle=BorderStyle.None,Font=Theme.Font(10)};content.SetBounds(24,82,672,350);
        content.Text="TELEVISIÓN EN DIRECTO\r\n\r\nSintonía no incluye listas ni canales precargados. Añade tus propias fuentes mediante «Añadir lista». El filtro de emisores identificados es opcional y no se activa automáticamente.\r\n\r\nPuedes importar listas M3U, M3U8 y emisiones directas autorizadas. Al desmarcar el filtro, se conserva el contenido que hayas importado. No se eluden DRM, suscripciones ni restricciones geográficas.\r\n\r\nAlgunos programas pueden no estar disponibles por derechos, ubicación o por el horario del canal. Usa «Web del emisor» cuando el directo no se pueda reproducir.\r\n\r\nLos favoritos, listas y volumen se guardan solo en tu usuario de Windows. No hay cuentas ni analítica. Las listas y los emisores reciben las conexiones necesarias para descargar el catálogo y reproducir vídeo.\r\n\r\nATAJOS\r\nEspacio: reproducir / pausa · F11: pantalla completa · Esc: salir de pantalla completa · Ctrl+F: buscar · Ctrl+R: actualizar · M: silencio · Ctrl+← / →: canal anterior / siguiente.\r\n\r\n"+Program.ResourceText("THIRD-PARTY-NOTICES.txt").Replace("\n","\r\n");
        var sources=Theme.Button("IPTV-org",(_,_)=>Open("https://github.com/iptv-org/iptv"),false,120);sources.SetBounds(24,458,120,36);
        var licenses=Theme.Button("Guardar licencias",(_,_)=>{using var dialog=new SaveFileDialog{FileName="Sintonia_Licencias.txt",Filter="Texto|*.txt"};if(dialog.ShowDialog(f)==DialogResult.OK)File.WriteAllText(dialog.FileName,Program.ResourceText("THIRD-PARTY-NOTICES.txt"));},false,160);licenses.SetBounds(154,458,160,36);
        var close=Theme.Button("Cerrar",(_,_)=>f.Close(),true,100);close.SetBounds(596,458,100,36);
        f.Controls.AddRange([title,content,sources,licenses,close]);f.CancelButton=close;f.ShowDialog(owner);
    }
    internal static void Open(string url){if(M3uParser.IsListUrl(url))Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
}
