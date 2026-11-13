using System.Net;
using System.Text.RegularExpressions;
namespace AldaJoyeros.Helpers;
public static class EmailPlainText {
 public static string FromHtml(string html){
  var timeout=TimeSpan.FromSeconds(1);
  var text=Regex.Replace(html,@"<(head|style|script)\b[^>]*>.*?</\1>","",RegexOptions.IgnoreCase|RegexOptions.Singleline,timeout);
  text=Regex.Replace(text,@"<a\b[^>]*href=['"" ]?([^'"" >]+)[^>]*>(.*?)</a>","$2 ($1)",RegexOptions.IgnoreCase|RegexOptions.Singleline,timeout);
  text=Regex.Replace(text,@"</(?:p|div|h[1-6]|tr|li)>|<br\s*/?>","\n",RegexOptions.IgnoreCase,timeout);
  text=WebUtility.HtmlDecode(Regex.Replace(text,"<[^>]+>"," ",RegexOptions.None,timeout));
  return Regex.Replace(text,@"[ \t]+"," ",RegexOptions.None,timeout).Trim();
 }
}
