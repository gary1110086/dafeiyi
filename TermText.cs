using System.Text.RegularExpressions;
namespace LightTranslate {
    internal static class TermText {
        internal static string Preview(string raw) { string text=(raw??"").Length>1000?(raw??"").Substring(0,1000):raw??""; text=Regex.Replace(text,@"[\r\n\t]+"," "); text=Regex.Replace(text,@"[\*`#>|]",""); text=Regex.Replace(text,@"\s+"," ").Trim(); return text.Length>80?text.Substring(0,80)+"…":text; }
    }
}
