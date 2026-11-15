using System.Net;
using System.Net.Sockets;
using System.Text;
using Sintonia.Core;

int assertions=0;
void Check(bool condition,string message){if(!condition)throw new Exception(message);assertions++;Console.WriteLine("PASS "+message);}
void Throws(Action action,string message){try{action();}catch(InvalidDataException){Check(true,message);return;}throw new Exception(message);}

var sample="""
﻿#EXTM3U
#EXTINF:-1 tvg-id="Test.es@HD" http-user-agent="Mozilla/5.0 (X11, Linux)" tvg-logo="https://example.org/logo.png" group-title="News;Public",Canal, con coma (1080p) [Geo-blocked]
#EXTVLCOPT:http-referrer=https://example.org/
https://example.org/live/master.m3u8
#EXTINF:-1 tvg-id="Test.es@SD" group-title="News",Canal, con coma (720p)
https://example.org/backup.m3u8
#EXTINF:-1 tvg-id="Unsafe",No ejecutar
file:///C:/Windows/system.ini
#EXTINF:-1 tvg-id="Relative",Relativo
relative.m3u8|User-Agent=My%20Player&Referer=https%3A%2F%2Fexample.org
""";
var parsed=M3uParser.Parse(sample,"test",new Uri("https://example.org/list/all.m3u"));
Check(parsed.Count==3,"M3U imports supported network streams and rejects local files");
Check(parsed[0].Name=="Canal, con coma","Commas and quoted metadata parse correctly");
Check(parsed[0].UserAgent=="Mozilla/5.0 (X11, Linux)"&&parsed[0].Referrer=="https://example.org/","User agent and referrer retained as typed headers");
Check(parsed[0].Group=="Noticias · Pública"&&parsed[0].Hint.Length>0,"Categories translated and restrictions visible");
Check(parsed[2].Url=="https://example.org/list/relative.m3u8"&&parsed[2].UserAgent=="My Player","Relative entries and Kodi headers resolve safely");
Check(parsed[0].Id==parsed[1].Id,"Quality variants share a stable channel identity");
var s1=new PlaylistSource{Name="One",Entries=parsed};var s2=new PlaylistSource{Name="Two",Entries=[parsed[0]]};
var merged=Catalog.Merge([s1,s2]);
Check(merged.Count==2&&merged[0].Streams.Count==2,"Sources merge duplicate channels and URLs");
s2.Enabled=false;Check(Catalog.Merge([s1,s2]).Count==2,"Disabled lists do not contribute channels");
Check(Catalog.SearchKey("Televisión Málaga")=="television malaga","Search ignores accents");
Throws(()=>M3uParser.Parse("#EXTM3U\n#EXT-X-TARGETDURATION:8\n#EXTINF:8,\npart.ts","hls"),"HLS media cannot be mistaken for a channel list");
Throws(()=>M3uParser.Parse("#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=1000000\nmain.m3u8","hls"),"HLS master directs user to direct-stream import");
Check(!M3uParser.IsStreamUrl("https://example.org/\n--option")&&!M3uParser.IsStreamUrl("javascript:alert(1)"),"Control characters and unsupported protocols rejected");
var hostile=M3uParser.Parse("#EXTINF:-1 tvg-id=\"Telemadrid.es@HD\",Fake\nhttps://live.telemadrid.cross-media.es.attacker.test/live.m3u8\n#EXTINF:-1 tvg-id=\"DAZNF1.uk@Spain\",DAZN F1\nhttps://example.org/f1.m3u8","bad");
Check(OfficialSources.Filter(hostile).Count==0,"Preloaded filter rejects spoofed hosts and subscription-labelled links");
Check(M3uParser.NormalizeListUrl("https://iptv-org.github.io/")==PlaylistSource.SpainUrl,"IPTV-org catalogue URL maps to Spain playlist");

var folder=Path.Combine(Path.GetTempPath(),"SintoniaChecks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
try
{
    var store=new StateStore(folder);var state=new AppState{Sources=[s1],Favorites=[parsed[0].Id],Volume=42};store.Save(state);var restored=store.Load();
    Check(restored.Favorites.Contains(parsed[0].Id)&&restored.Volume==42&&restored.Sources[0].Entries.Count==3,"Preferences, cache and favorites survive restart");
    Check(!File.Exists(Path.Combine(folder,"preferences.json.tmp")),"Preferences are committed with atomic replacement");
    await File.WriteAllTextAsync(Path.Combine(folder,"preferences.json"),"{broken");var recovered=store.Load();
    Check(recovered.Sources.Count==1&&store.RecoveryNotice is not null&&Directory.GetFiles(folder,"*.recovered-*").Length==1,"Corrupt state is backed up before defaults are restored");
    using var service=new PlaylistService();
    var file=Path.Combine(folder,"channels.m3u");await File.WriteAllTextAsync(file,sample);var local=new PlaylistSource{Kind="file",Location=file,Name="Local"};
    Check(await service.RefreshAsync(local)==2,"Local playlists import without fetching external streams");
    var old=local.Entries;await File.WriteAllTextAsync(file,"<html>not a playlist</html>");
    try{await service.RefreshAsync(local);}catch(InvalidDataException){}
    Check(ReferenceEquals(local.Entries,old)&&local.LastError.Length>0,"Invalid updates preserve the last good catalogue and explain the failure");
    using var canceled=new CancellationTokenSource();canceled.Cancel();
    try{await service.RefreshAsync(local,canceled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){Check(true,"Cancellation stops playlist work");}
    var direct=new PlaylistSource{Name="My authorized channel",Kind="stream",Location="https://example.org/direct.m3u8"};
    Check(await service.RefreshAsync(direct)==1&&direct.Entries[0].Url==direct.Location,"Direct HLS streams import as a single channel");

    // Exercise real HTTP client and failure handling against a deterministic server.
    var server=new TcpListener(IPAddress.Loopback,0);server.Start();var port=((IPEndPoint)server.LocalEndpoint).Port;
    async Task Reply(string body,int status=200)
    {
        using var client=await server.AcceptTcpClientAsync();await using var stream=client.GetStream();
        var header=new List<byte>();var octet=new byte[1];
        while(header.Count<8192&&await stream.ReadAsync(octet)==1){header.Add(octet[0]);if(header.Count>=4&&header.TakeLast(4).SequenceEqual(new byte[]{13,10,13,10}))break;}
        var bytes=Encoding.UTF8.GetBytes(body);var headers=Encoding.ASCII.GetBytes($"HTTP/1.1 {status} Test\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers);await stream.WriteAsync(bytes);
    }
    var remote=new PlaylistSource{Location=$"http://127.0.0.1:{port}/list.m3u",Name="HTTP"};
    var reply=Reply(sample);var amount=await service.RefreshAsync(remote);await reply;
    Check(amount==3&&remote.UpdatedAt is not null,"HTTP playlists load with relative URL resolution");
    var prior=remote.Entries;reply=Reply("Unavailable",503);
    try{await service.RefreshAsync(remote);}catch(InvalidDataException){}await reply;
    Check(ReferenceEquals(remote.Entries,prior)&&remote.LastError.Length>0,"HTTP failure retains cache and records its status");server.Stop();
}
finally{Directory.Delete(folder,true);}

var fixture=args.Length>0?args[0]:"src/Sintonia.App/Assets/spain.m3u";
var actual=M3uParser.Parse(await File.ReadAllTextAsync(fixture),"spain");var selected=OfficialSources.Filter(actual);
Check(actual.Count>100&&selected.Count>15,"Real Spain catalogue and preloaded origin filter parse successfully");
Check(!selected.Any(e=>e.Name.Contains("DAZN",StringComparison.OrdinalIgnoreCase)||e.Name.Contains("Eurosport",StringComparison.OrdinalIgnoreCase)),"Subscription channels are not included in the initial selection");
Check(selected.All(e=>e.OfficialWebsite.StartsWith("https://")),"Every preloaded signal has an emitter website");
Console.WriteLine($"\n{assertions} assertions passed. Spain input: {actual.Count} signals. Preloaded: {Catalog.Merge([new PlaylistSource{Entries=selected}]).Count} channels / {selected.Count} signals.");
