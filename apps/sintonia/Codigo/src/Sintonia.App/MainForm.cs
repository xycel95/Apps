using LibVLCSharp.Shared;
using Sintonia.Core;
using VlcPlayer=LibVLCSharp.Shared.MediaPlayer;

namespace Sintonia.App;

internal sealed class MainForm : Form
{
    private readonly StateStore _store;
    internal AppState State {get;}
    private readonly PlaylistService _service=new();
    private readonly CancellationTokenSource _closing=new();
    private readonly LibVLC _vlc;
    private readonly VlcPlayer _player;
    private readonly ChannelList _channels=new();
    private readonly TextBox _search=Theme.Input("Buscar canal…");
    private readonly ComboBox _category=Theme.Combo(),_variants=Theme.Combo(),_audio=Theme.Combo();
    private readonly Label _title=Theme.Label("Sin canales",21,false,FontStyle.Bold),_subtitle=Theme.Label("Añade tu propia lista o una emisión directa",9.5f,true),_status=Theme.Label("Añade una lista para empezar",9.5f,true),_footer=Theme.Label("",9,true),_count=Theme.Label("CANALES",9,true,FontStyle.Bold),_volumeLabel=Theme.Label("70 %",9,true);
    private readonly VideoSurface _video=new();
    private readonly TrackBar _volume=new(){Minimum=0,Maximum=100,TickStyle=TickStyle.None,SmallChange=5,LargeChange=10,BackColor=Theme.Surface,Width=110,Height=35};
    private readonly Button _all,_favorites,_star,_play,_mute,_refresh,_retry,_official,_full;
    private readonly Panel _header=new(){Dock=DockStyle.Top,Height=84,BackColor=Theme.Surface},_bottom=new(){Dock=DockStyle.Bottom,Height=30,BackColor=Theme.Background};
    private readonly TableLayoutPanel _body=new(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new(0,1,0,0)},_main=new(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new(22,12,22,6)};
    private List<Channel> _catalog=[];
    private Channel? _current;
    private StreamEntry? _currentStream;
    private bool _favoritesOnly,_rendering,_changingVariant,_changingAudio,_refreshing,_fullscreen,_disposed,_starting,_playing,_stopped=true;
    private int _attempt,_generation;
    private DateTime _startedAt;
    private FormWindowState _previousState;
    private Rectangle _previousBounds;
    private readonly System.Windows.Forms.Timer _watchdog=new(){Interval=1000};
    private readonly HashSet<int> _tried=[];

    internal MainForm(StateStore store,AppState state)
    {
        _store=store;State=state;
        Text="Sintonía · Televisión en directo";BackColor=Theme.Background;ForeColor=Theme.Text;Font=Theme.Font();ClientSize=new(1320,840);MinimumSize=new(1080,680);StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.Dpi;KeyPreview=true;
        Icon=System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        _all=Theme.Button("Todos",(_,_)=>SetFavorites(false),true,130);_favorites=Theme.Button("☆ Favoritos",(_,_)=>SetFavorites(true),false,130);
        _star=Theme.Button("☆ Favorito",(_,_)=>{if(_current is not null)ToggleFavorite(_current);},false,126);
        _play=Theme.Button("Reproducir",(_,_)=>TogglePlay(),true,100);_mute=Theme.Button("Sonido",(_,_)=>ToggleMute(),false,68);
        _refresh=Theme.Button("Actualizar",async(_,_)=>await RefreshSourcesAsync(),false,112);
        _retry=Theme.Button("Reintentar",(_,_)=>{if(_current is not null)StartChannel(_current);},false,108);_retry.Enabled=false;
        _official=Theme.Button("Web del emisor",(_,_)=>{if(_currentStream?.OfficialWebsite.Length>0)AboutDialog.Open(_currentStream.OfficialWebsite);},false,136);_official.Enabled=false;
        _full=Theme.Button("Pantalla completa",(_,_)=>ToggleFullscreen(),false,139);_volume.Width=90;
        BuildUi();
        _vlc=new LibVLC("--no-video-title-show","--no-snapshot-preview","--no-osd","--no-lua","--no-stats","--quiet");
        _player=new VlcPlayer(_vlc){Volume=state.Volume,Mute=state.Muted,EnableKeyInput=false,EnableMouseInput=false};
        _player.Playing+=PlayerPlaying;_player.Paused+=PlayerPaused;_player.EncounteredError+=PlayerError;_player.EndReached+=PlayerEnded;
        _volume.Value=state.Volume;UpdateVolume();_watchdog.Tick+=(_,_)=>WatchPlayback();_watchdog.Start();
        _search.TextChanged+=(_,_)=>RenderChannels();_category.SelectedIndexChanged+=(_,_)=>{if(!_rendering)RenderChannels();};
        _channels.SelectedIndexChanged+=(_,_)=>{if(!_rendering&&_channels.SelectedItem is Channel c)StartChannel(c);};
        _channels.FavoriteClicked+=ToggleFavorite;
        _channels.MouseClick+=(_,e)=>{if(e.X<_channels.ClientSize.Width-38&&_stopped&&!_rendering&&_channels.SelectedItem is Channel c)StartChannel(c);};
        _variants.SelectedIndexChanged+=(_,_)=>{if(!_changingVariant&&_current is not null&&_variants.SelectedIndex>=0){_tried.Clear();_attempt=0;StartVariant(_variants.SelectedIndex);}};
        _audio.SelectedIndexChanged+=(_,_)=>{if(!_changingAudio&&_audio.SelectedItem is AudioChoice choice)_player.SetAudioTrack(choice.Id);};
        _volume.ValueChanged+=(_,_)=>{if(_player is null)return;State.Volume=_volume.Value;_player.Volume=State.Volume;UpdateVolume();};_volume.MouseUp+=(_,_)=>Save();_volume.KeyUp+=(_,_)=>Save();
        _video.DoubleClick+=(_,_)=>ToggleFullscreen();
        Shown+=async(_,_)=>{SourcesChanged();if(_store.RecoveryNotice is not null)MessageBox.Show(this,_store.RecoveryNotice,"Preferencias recuperadas");await RefreshSourcesAsync();};
        FormClosing+=(_,_)=>{_closing.Cancel();State.Volume=_volume.Value;Save();};
    }

    private void BuildUi()
    {
        var brand=Theme.Label("Sintonía",25,false,FontStyle.Bold);brand.ForeColor=Theme.Accent;brand.SetBounds(24,12,260,40);
        var tag=Theme.Label("TELEVISIÓN EN DIRECTO",8.5f,true,FontStyle.Bold);tag.Location=new(26,56);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Right,Width=520,Padding=new(0,20,12,0),WrapContents=false,FlowDirection=FlowDirection.RightToLeft};
        actions.Controls.Add(Theme.Button("Acerca de",(_,_)=>AboutDialog.Show(this),false,100));
        actions.Controls.Add(Theme.Button("Añadir lista",async(_,_)=>await AddSourceAsync(this),true,122));
        actions.Controls.Add(Theme.Button("Tus listas",(_,_)=>{using var d=new SourcesDialog(this);d.ShowDialog(this);},false,106));actions.Controls.Add(_refresh);
        _header.Controls.AddRange([brand,tag,actions]);
        _footer.Dock=DockStyle.Fill;_footer.Padding=new(22,3,10,0);_footer.AutoSize=false;_bottom.Controls.Add(_footer);
        _body.ColumnStyles.Add(new(SizeType.Absolute,328));_body.ColumnStyles.Add(new(SizeType.Percent,100));_body.RowStyles.Add(new(SizeType.Percent,100));
        var left=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,BackColor=Theme.Surface,Padding=new(18,15,12,10)};
        left.ColumnStyles.Add(new(SizeType.Percent,100));left.RowStyles.Add(new(SizeType.Absolute,29));left.RowStyles.Add(new(SizeType.Absolute,45));left.RowStyles.Add(new(SizeType.Absolute,50));left.RowStyles.Add(new(SizeType.Absolute,43));left.RowStyles.Add(new(SizeType.Percent,100));
        _count.Dock=DockStyle.Fill;left.Controls.Add(_count,0,0);_search.Dock=DockStyle.Top;_search.Height=30;_search.AccessibleName="Buscar canal";left.Controls.Add(_search,0,1);
        var tabs=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=new(0)};tabs.Controls.AddRange([_all,_favorites]);left.Controls.Add(tabs,0,2);
        _category.Dock=DockStyle.Top;_category.AccessibleName="Filtrar por categoría";left.Controls.Add(_category,0,3);_channels.Dock=DockStyle.Fill;left.Controls.Add(_channels,0,4);
        _main.ColumnStyles.Add(new(SizeType.Percent,100));_main.RowStyles.Add(new(SizeType.Absolute,80));_main.RowStyles.Add(new(SizeType.Percent,100));_main.RowStyles.Add(new(SizeType.Absolute,48));_main.RowStyles.Add(new(SizeType.Absolute,55));_main.RowStyles.Add(new(SizeType.Absolute,40));_main.RowStyles.Add(new(SizeType.Absolute,33));
        var now=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2};now.ColumnStyles.Add(new(SizeType.Percent,100));now.ColumnStyles.Add(new(SizeType.Absolute,138));now.RowStyles.Add(new(SizeType.Absolute,40));now.RowStyles.Add(new(SizeType.Absolute,29));_title.AutoSize=false;_title.AutoEllipsis=true;_title.Dock=DockStyle.Fill;_subtitle.AutoSize=false;_subtitle.AutoEllipsis=true;_subtitle.Dock=DockStyle.Fill;now.Controls.Add(_title,0,0);now.Controls.Add(_subtitle,0,1);now.Controls.Add(_star,1,0);_main.Controls.Add(now,0,0);
        _video.Dock=DockStyle.Fill;_main.Controls.Add(_video,0,1);
        var statusRow=new Panel{Dock=DockStyle.Fill};_status.AutoSize=false;_status.Dock=DockStyle.Fill;_status.TextAlign=ContentAlignment.MiddleLeft;var statusButtons=new FlowLayoutPanel{Dock=DockStyle.Right,Width=260,WrapContents=false,FlowDirection=FlowDirection.RightToLeft};statusButtons.Controls.AddRange([_retry,_official]);statusRow.Controls.Add(_status);statusRow.Controls.Add(statusButtons);_main.Controls.Add(statusRow,0,2);
        var controls=new FlowLayoutPanel{Dock=DockStyle.Fill,BackColor=Theme.Surface,Padding=new(8,5,0,0),WrapContents=false};
        controls.Controls.AddRange([Theme.Button("‹",(_,_)=>MoveChannel(-1),false,28),_play,Theme.Button("■",(_,_)=>StopPlayback(),false,28),Theme.Button("›",(_,_)=>MoveChannel(1),false,28),_mute,_volume,_volumeLabel,_full]);_volume.Margin=new(8,5,0,0);_volumeLabel.Margin=new(0,10,6,0);_main.Controls.Add(controls,0,3);
        var options=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Padding=new(0,6,0,0)};options.ColumnStyles.Add(new(SizeType.Absolute,45));options.ColumnStyles.Add(new(SizeType.Percent,65));options.ColumnStyles.Add(new(SizeType.Absolute,56));options.ColumnStyles.Add(new(SizeType.Percent,35));
        options.Controls.Add(Theme.Label("Señal",9,true),0,0);_variants.Dock=DockStyle.Top;_variants.AccessibleName="Señal de vídeo";options.Controls.Add(_variants,1,0);options.Controls.Add(Theme.Label(" Audio",9,true),2,0);_audio.Dock=DockStyle.Top;_audio.AccessibleName="Pista de audio";options.Controls.Add(_audio,3,0);_main.Controls.Add(options,0,4);
        var tips=Theme.Label("F11  Pantalla completa     ·     Espacio  Pausa     ·     Ctrl+F  Buscar",9,true);tips.Dock=DockStyle.Fill;tips.TextAlign=ContentAlignment.MiddleLeft;tips.AutoSize=false;_main.Controls.Add(tips,0,5);
        _body.Controls.Add(left,0,0);_body.Controls.Add(_main,1,0);Controls.Add(_body);Controls.Add(_bottom);Controls.Add(_header);
    }

    internal void SourcesChanged()
    {
        _catalog=Catalog.Merge(State.Sources);_channels.Favorites=State.Favorites;
        _video.HasChannels=_catalog.Count>0;_video.Invalidate();
        var selected=_category.SelectedItem as string;
        _rendering=true;_category.Items.Clear();_category.Items.Add("Todas las categorías");
        _category.Items.AddRange(_catalog.SelectMany(c=>c.Group.Split(" · ")).Distinct().Order().Cast<object>().ToArray());
        _category.SelectedIndex=selected is not null&&_category.Items.Contains(selected)?_category.Items.IndexOf(selected):0;_rendering=false;
        if(_current is not null)
        {
            var replacement=_catalog.FirstOrDefault(c=>c.Id==_current.Id);
            if(replacement is null) {StopPlayback();_current=null;_title.Text="Elige un canal";_subtitle.Text="La lista del canal está desactivada o se ha quitado.";}
            else { _current=replacement;UpdateVariants(_currentStream?.Url); }
        }
        if(_catalog.Count==0&&_current is null){_title.Text="Sin canales";_subtitle.Text="Añade tu propia lista o una emisión directa";_status.Text="Añade una lista para empezar";_variants.Items.Clear();_audio.Items.Clear();_retry.Enabled=false;_official.Enabled=false;}
        RenderChannels();Save();
    }
    private void RenderChannels()
    {
        _rendering=true;var q=Catalog.SearchKey(_search.Text.Trim());var group=_category.SelectedIndex>0?_category.SelectedItem as string:null;
        var visible=_catalog.Where(c=>(!_favoritesOnly||State.Favorites.Contains(c.Id))&&(group is null||c.Group.Split(" · ").Contains(group))&&(q.Length==0||Catalog.SearchKey(c.Name+" "+c.Group).Contains(q))).ToList();
        var top=_channels.TopIndex;_channels.BeginUpdate();_channels.Items.Clear();_channels.Items.AddRange(visible.Cast<object>().ToArray());
        if(_current is not null)_channels.SelectedIndex=visible.FindIndex(c=>c.Id==_current.Id);
        if(top>=0&&top<visible.Count)_channels.TopIndex=top;_channels.EndUpdate();_rendering=false;
        _all.Text=$"Todos ({_catalog.Count})";_favorites.Text=$"★ Favoritos ({_catalog.Count(c=>State.Favorites.Contains(c.Id))})";_count.Text=$"{visible.Count} CANALES";
        if(visible.Count==0)_count.Text=_catalog.Count==0?"SIN CANALES":_favoritesOnly?"AÚN NO HAY FAVORITOS":"SIN RESULTADOS";
        _channels.AccessibleDescription=visible.Count==0?"No hay canales con este filtro":"Selecciona un canal para reproducirlo. La estrella lo guarda en favoritos.";
    }
    private void SetFavorites(bool value){_favoritesOnly=value;_all.BackColor=value?Theme.Raised:Theme.Accent;_all.ForeColor=value?Theme.Text:Theme.Background;_favorites.BackColor=value?Theme.Accent:Theme.Raised;_favorites.ForeColor=value?Theme.Background:Theme.Text;RenderChannels();}
    private void ToggleFavorite(Channel channel){if(!State.Favorites.Add(channel.Id))State.Favorites.Remove(channel.Id);UpdateStar();RenderChannels();Save();}
    private void UpdateStar(){_star.Text=_current is not null&&State.Favorites.Contains(_current.Id)?"★ Guardado":"☆ Favorito";_channels.Invalidate();}
    private void UpdateVariants(string? url=null)
    {
        _changingVariant=true;_variants.Items.Clear();if(_current is not null){_variants.Items.AddRange(_current.Streams.Cast<object>().ToArray());_variants.SelectedIndex=Math.Max(0,_current.Streams.FindIndex(s=>s.Url==url));}_changingVariant=false;
    }
    private void StartChannel(Channel channel)
    {
        _current=channel;_tried.Clear();_attempt=0;State.LastChannelId=channel.Id;_channels.PlayingId=channel.Id;
        _title.Text=channel.Name;Text=channel.Name+" · Sintonía";_subtitle.Text=channel.Group+" · "+channel.Streams.Count+" señal"+(channel.Streams.Count==1?"":"es");UpdateStar();UpdateVariants();Save();StartVariant(0);
    }
    private void StartVariant(int index)
    {
        if(_current is null||index<0||index>=_current.Streams.Count||_disposed)return;
        _generation++;_starting=true;_playing=false;_stopped=false;_startedAt=DateTime.UtcNow;_tried.Add(index);_currentStream=_current.Streams[index];
        _changingVariant=true;_variants.SelectedIndex=index;_changingVariant=false;_audio.Items.Clear();
        _official.Enabled=_currentStream.OfficialWebsite.Length>0;_retry.Enabled=true;_video.ShowWelcome=false;_video.Invalidate();
        _status.ForeColor=Theme.Muted;_status.Text="Conectando con el canal…";_play.Text="Pausa";
        _player.Stop();_player.Hwnd=_video.Handle;
        using var media=new Media(_vlc,new Uri(_currentStream.Url));
        media.AddOption(":network-caching=1500");media.AddOption(":http-reconnect");media.AddOption(":no-video-title-show");
        if(_currentStream.UserAgent.Length>0)media.AddOption(":http-user-agent="+_currentStream.UserAgent);
        if(_currentStream.Referrer.Length>0)media.AddOption(":http-referrer="+_currentStream.Referrer);
        _player.Mute=State.Muted;_player.Volume=State.Volume;
        if(!_player.Play(media))Ui(()=>PlaybackFailed());
    }
    private void PlayerPlaying(object? sender,EventArgs e)=>Ui(()=>{if(_stopped||_player.State!=VLCState.Playing)return;_starting=false;_playing=true;_play.Text="Pausa";_status.ForeColor=Theme.Accent;_status.Text="● En directo"+(_currentStream?.Hint.Length>0?" · "+_currentStream.Hint:"");UpdateAudio();});
    private void PlayerPaused(object? sender,EventArgs e)=>Ui(()=>{if(_stopped||_player.State!=VLCState.Paused)return;_playing=false;_play.Text="Reproducir";_status.ForeColor=Theme.Muted;_status.Text="En pausa · Pulsa Reproducir para continuar";});
    private void PlayerError(object? sender,EventArgs e)=>Ui(()=>{if(!_stopped&&_player.State==VLCState.Error)PlaybackFailed();});
    private void PlayerEnded(object? sender,EventArgs e)=>Ui(()=>{if(!_stopped&&_player.State==VLCState.Ended)PlaybackFailed();});
    private async void PlaybackFailed()
    {
        if(_disposed||_stopped||_current is null)return;
        // Multiple VLC events can describe the same failure. Retire this session.
        _stopped=true;_starting=false;_playing=false;var gen=++_generation;
        _status.ForeColor=Color.FromArgb(243,190,113);_status.Text="La señal no responde. Probando de nuevo…";
        int next=_current.Streams.FindIndex(s=>!_tried.Contains(_current.Streams.IndexOf(s)));
        if(next<0&&_attempt>=1){_play.Text="Reproducir";_status.Text="Señal no disponible · Reintenta o abre la web del emisor";return;}
        if(next<0){next=Math.Max(0,_variants.SelectedIndex);_attempt++;}
        try { await Task.Delay(1800,_closing.Token); } catch(OperationCanceledException){return;}
        if(gen==_generation&&!_disposed)StartVariant(next);
    }
    private void WatchPlayback()
    {
        if(_starting&&!_stopped&&(DateTime.UtcNow-_startedAt).TotalSeconds>25)PlaybackFailed();
        if(_playing&&_audio.Items.Count==0)UpdateAudio();
    }
    private void UpdateAudio()
    {
        _changingAudio=true;
        var track=_player.AudioTrack;var tracks=_player.AudioTrackDescription;
        _audio.Items.Clear();
        foreach(var t in tracks.Where(t=>t.Id>=0))_audio.Items.Add(new AudioChoice(t.Id,t.Name));
        for(int i=0;i<_audio.Items.Count;i++)if(_audio.Items[i] is AudioChoice choice&&choice.Id==track)_audio.SelectedIndex=i;
        _changingAudio=false;
    }
    private void TogglePlay()
    {
        if(_current is null){if(_channels.SelectedItem is Channel c)StartChannel(c);else if(_channels.Items.Count>0)_channels.SelectedIndex=0;return;}
        if(_stopped){StartChannel(_current);return;}
        if(_player.IsPlaying)_player.SetPause(true);else _player.SetPause(false);
    }
    private void StopPlayback()
    {
        _generation++;_stopped=true;_starting=_playing=false;_player.Stop();_play.Text="Reproducir";_status.Text="Reproducción detenida";_status.ForeColor=Theme.Muted;_channels.PlayingId="";_channels.Invalidate();_video.ShowWelcome=true;_video.Invalidate();
    }
    private void MoveChannel(int direction)
    {
        if(_channels.Items.Count==0)return;var index=_channels.SelectedIndex;if(index<0)index=direction>0?-1:0;
        _channels.SelectedIndex=(index+direction+_channels.Items.Count)%_channels.Items.Count;
    }
    private void ToggleMute(){State.Muted=!State.Muted;_player.Mute=State.Muted;UpdateVolume();Save();}
    private void UpdateVolume(){_mute.Text=State.Muted?"Silencio":"Sonido";_volumeLabel.Text=State.Volume+" %";}

    internal async Task AddSourceAsync(IWin32Window owner)
    {
        using var dialog=new AddSourceDialog();if(dialog.ShowDialog(owner)!=DialogResult.OK||dialog.Source is not PlaylistSource source)return;
        if(State.Sources.Any(s=>s.Kind==source.Kind&&s.Location==source.Location&&s.OfficialOnly==source.OfficialOnly)) {MessageBox.Show(owner,"Esa lista ya está añadida. Puedes activarla o actualizarla desde «Tus listas».","Lista existente");return;}
        _footer.Text="Comprobando la nueva fuente…";
        try {await _service.RefreshAsync(source,_closing.Token);if(_disposed)return;State.Sources.Add(source);SourcesChanged();_footer.Text=$"Lista añadida: {source.Name} · {source.Entries.Count} señales";}
        catch(OperationCanceledException) { }
        catch(InvalidDataException ex){MessageBox.Show(owner,ex.Message,"No se ha podido añadir la fuente",MessageBoxButtons.OK,MessageBoxIcon.Information);_footer.Text="No se ha añadido la fuente. Revisa la dirección e inténtalo de nuevo.";}
    }
    internal async Task RefreshSourcesAsync()
    {
        if(_refreshing||_disposed)return;
        if(!State.Sources.Any(s=>s.Enabled)){_footer.Text=State.Sources.Count==0?"Sin listas · Pulsa Añadir lista para empezar":"No hay listas activas · Revisa Tus listas";return;}
        _refreshing=true;_refresh.Enabled=false;_refresh.Text="Cargando…";_footer.Text="Actualizando tus listas…";
        var sources=State.Sources.Where(s=>s.Enabled).ToArray();int errors=0;using var semaphore=new SemaphoreSlim(3);
        try
        {
            await Task.WhenAll(sources.Select(async source=>{await semaphore.WaitAsync(_closing.Token);try{await _service.RefreshAsync(source,_closing.Token);}catch(InvalidDataException){Interlocked.Increment(ref errors);}finally{semaphore.Release();}}));
            if(_disposed)return;SourcesChanged();_footer.Text=errors>0?$"{errors} lista(s) sin actualizar. Se mantiene el último catálogo. Consulta «Tus listas». ":$"{_catalog.Count} canales · Listas actualizadas a las {DateTime.Now:HH:mm}";
        }
        catch(OperationCanceledException) { }
        finally{if(!_disposed){_refreshing=false;_refresh.Enabled=true;_refresh.Text="Actualizar";}}
    }
    private void Save(){if(_disposed)return;try{_store.Save(State);}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){_footer.Text="No se han guardado las preferencias: "+ex.Message;}}
    private void Ui(Action action){if(_disposed||IsDisposed||!IsHandleCreated)return;try{BeginInvoke(()=>{if(!_disposed&&!IsDisposed)action();});}catch(InvalidOperationException){}}
    private void ToggleFullscreen()
    {
        _fullscreen=!_fullscreen;
        if(_fullscreen){_previousState=WindowState;_previousBounds=Bounds;WindowState=FormWindowState.Normal;FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;_header.Hide();_bottom.Hide();_body.ColumnStyles[0].Width=0;foreach(var i in new[]{0,2,4,5})_main.RowStyles[i].Height=0;_main.Padding=new(0);_full.Text="Salir de pantalla";}
        else{WindowState=FormWindowState.Normal;FormBorderStyle=FormBorderStyle.Sizable;Bounds=_previousBounds;WindowState=_previousState;_header.Show();_bottom.Show();_body.ColumnStyles[0].Width=328;_main.RowStyles[0].Height=80;_main.RowStyles[2].Height=48;_main.RowStyles[4].Height=40;_main.RowStyles[5].Height=33;_main.Padding=new(22,12,22,6);_full.Text="Pantalla completa";}
    }
    protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
    {
        if(keyData==Keys.F11){ToggleFullscreen();return true;}
        if(keyData==Keys.Escape&&_fullscreen){ToggleFullscreen();return true;}
        if(keyData==(Keys.Control|Keys.F)){if(_fullscreen)ToggleFullscreen();_search.Focus();_search.SelectAll();return true;}
        if(keyData==(Keys.Control|Keys.R)){_=RefreshSourcesAsync();return true;}
        if(keyData==(Keys.Control|Keys.Left)){MoveChannel(-1);return true;}
        if(keyData==(Keys.Control|Keys.Right)){MoveChannel(1);return true;}
        if(ActiveControl is not TextBox){if(keyData==Keys.Space){TogglePlay();return true;}if(keyData==Keys.M){ToggleMute();return true;}}
        return base.ProcessCmdKey(ref msg,keyData);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing&&!_disposed){_disposed=true;_closing.Cancel();_watchdog.Stop();_watchdog.Dispose();_player.Playing-=PlayerPlaying;_player.Paused-=PlayerPaused;_player.EncounteredError-=PlayerError;_player.EndReached-=PlayerEnded;_player.Stop();_player.Dispose();_vlc.Dispose();_service.Dispose();_closing.Dispose();}
        base.Dispose(disposing);
    }
    private sealed record AudioChoice(int Id,string Name){public override string ToString()=>Name;}
}
