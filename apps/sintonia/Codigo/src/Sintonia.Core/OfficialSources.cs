namespace Sintonia.Core;

// A conservative origin filter for user-imported playlists (optional). This is
// provenance selection, not a legal certification. Unknown third-party relays,
// IP-address streams and subscription-labelled channels are excluded only when the optional filter is selected.
public static class OfficialSources
{
    public static string Website(StreamEntry entry)
    {
        if (!Uri.TryCreate(entry.Url, UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https")) return "";
        var h = u.IdnHost.ToLowerInvariant(); var id = entry.Id;
        bool Id(params string[] names) => names.Any(n => id.StartsWith("tvg:" + n.ToLowerInvariant(), StringComparison.Ordinal));
        if ((h == "ztnr.rtve.es" || h == "rtvelivestream.rtve.es") && Id("24Horas", "La1", "La2", "Radio3", "RNE")) return "https://www.rtve.es/play/videos/directo/";
        if ((h == "3catdirectes.cat" || h.EndsWith(".3catdirectes.cat")) && Id("3Cat", "TV3", "El33", "Esport3", "SX3", "BonDiaTV")) return "https://www.3cat.cat/3cat/directes/";
        if (h == "live.telemadrid.cross-media.es" && Id("Telemadrid", "LaOtra")) return "https://www.telemadrid.es/emision-en-directo/";
        if (h == "cdn1.etbon.eus" && Id("ETB")) return "https://www.eitb.eus/es/television/";
        if (h == "cdnlive.codev8.net" && Id("CanalSur")) return "https://www.canalsur.es/portada-2808.html";
        if (h == "dfk2a268yviz9.cloudfront.net" && Id("CanalSurAndalucia")) return "https://www.canalsur.es/portada-2808.html";
        if (h == "cdnlive.codev8.net" && Id("CanalExtremadura")) return "https://www.canalextremadura.es/";
        if (h is "crtvg-europa.flumotion.cloud" or "crtvg-tvg2.flumotion.cloud" && Id("GaliciaTV", "TVG")) return "https://www.g24.gal/directos";
        if (h == "rtvclive.flumotion.cloud" && Id("TelevisionCanaria")) return "https://rtvc.es/en-directo/";
        if (h == "congresodirecto.akamaized.net" && Id("CanalParlamento", "Congreso")) return "https://www.congreso.es/";
        if (h == "broadcasting.parlamentodenavarra.es" && Id("ParlamentodeNavarra")) return "https://www.parlamentodenavarra.es/";
        if (h == "ondacadiztv.es" && Id("OndaCadiz")) return "https://ondacadiz.es/";
        if (h == "interalmeria.tv" && Id("InteralmeriaTV")) return "https://www.interalmeria.tv/";
        if (h == "streaming.rtvmarbella.tv" && Id("RadioTelevisionMarbella")) return "https://rtvmarbella.tv/";
        if (h == "streaming-1.eltorotv.com" && Id("ElToroTV")) return "https://eltorotv.com/";
        if (h == "tvdirecto.teleelx.es" && Id("TeleElx")) return "https://teleelx.es/";
        if (h == "streaming.28kanala.eus" && Id("28kanala")) return "https://28kanala.eus/";
        if (h == "zuzenean.goienamedia.eus" && Id("Goiena")) return "https://goiena.eus/";
        return "";
    }
    public static List<StreamEntry> Filter(IEnumerable<StreamEntry> entries) => entries.Where(e => { e.OfficialWebsite = Website(e); return e.OfficialWebsite.Length > 0; }).ToList();
}
