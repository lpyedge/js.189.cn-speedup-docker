using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace JSDXTS;

public class HttpWebUtility : IDisposable
{
    public HttpWebUtility()
    {
        ServicePointManager.DefaultConnectionLimit = 500;
        ServicePointManager.Expect100Continue = false;

        AccpetSet(HttpAccept.All);
        UserAgentSet(HttpUserAgents.IE11_Win);

        ParamsEncoding = Encoding.UTF8;
        UrlReferer = string.Empty;
        //JScriptEscape = false;
        TimeOut = 15000;
        KeepAlive = false;

        IfModifiedSince = null;
        Date = null;

        
        MyHttpClientHandler = new HttpClientHandler
        {
            Proxy = null,
            AllowAutoRedirect = false,
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, x509Certificate2, x509Chain, sslPolicyErrors) => true
        };

        //初始化 HttpClient
        MyHttpClient = new HttpClient(MyHttpClientHandler);
        
        MyHttpClient.DefaultRequestHeaders.AcceptEncoding.TryParseAdd("gzip");
    }

    public async Task<string> ResponseAsync(Uri url, HttpMethod httpMethod = HttpMethod.GET,
        Dictionary<string, dynamic> paramList = null, Dictionary<string, string> headList = null)
    {
        string resultStr;

        using (var myMemoryStream =
               new MemoryStream(await ResponseBinaryAsync(url, httpMethod, paramList, headList)))
        {
            var chatSet = !Equals(CurrentCharset, Encoding.UTF8) ? CurrentCharset : Encoding.UTF8;

            using (var streamReader = new StreamReader(myMemoryStream, chatSet))
            {
                resultStr = streamReader.ReadToEnd();
            }
        }

        // if (JScriptEscape)
        // {
        //     ResultStr = Microsoft.JScript.GlobalObject.unescape(ResultStr);
        // }

        return resultStr;
    }

    public async Task<string> ResponseBodyAsync(Uri url, HttpMethod httpMethod = HttpMethod.GET,
        string paramsData = null, Dictionary<string, string> headList = null)
    {
        return CurrentCharset.GetString(await ResponseBodyBinaryAsync(url, httpMethod,
            paramsData != null ? ParamsEncoding.GetBytes(paramsData) : Array.Empty<byte>(),
            headList));
    }

    public async Task<byte[]> ResponseBinaryAsync(Uri url, HttpMethod httpMethod = HttpMethod.GET,
        Dictionary<string, dynamic> paramList = null, Dictionary<string, string> headList = null)
    {
        if (httpMethod == HttpMethod.GET || httpMethod == HttpMethod.HEAD)
            if (paramList != null && paramList.Count > 0)
            {
                var ub = new UriBuilder(url);
                if (!string.IsNullOrWhiteSpace(ub.Query))
                    ub.Query = ub.Query.Substring(1) + "&" +
                               BuildQueryString(paramList.ToDictionary(p => p.Key, p => (string) p.Value),
                                   ParamsEncoding);
                else
                    ub.Query = BuildQueryString(paramList.ToDictionary(p => p.Key, p => (string) p.Value),
                        ParamsEncoding);

                paramList.Clear();
                url = new Uri(ub.Uri.ToString());
            }

        var paramsData = Array.Empty<byte>();
        if (httpMethod == HttpMethod.POST || httpMethod == HttpMethod.PUT || httpMethod == HttpMethod.PATCH ||
            httpMethod == HttpMethod.DELETE)
        {
            paramList = paramList ?? new Dictionary<string, dynamic>();

            var uploadFiles = paramList.Values.OfType<UpLoadFile>().ToList();
            var postParams = paramList.Where(p => p.Value is string)
                .ToDictionary(p => p.Key, p => (string) p.Value);

            if (uploadFiles.Count > 0)
            {
                var boundary = GetBoundary;
                paramsData = BuildPostData(postParams, uploadFiles, ParamsEncoding, boundary);
                headList = headList ?? new Dictionary<string, string>();
                ContentType = $"multipart/form-data; boundary={boundary}";
            }
            else
            {
                paramsData = ParamsEncoding.GetBytes(BuildQueryString(postParams, ParamsEncoding));
            }
        }

        return await ResponseBodyBinaryAsync(url, httpMethod, paramsData, headList);
    }


    public async Task<byte[]> ResponseBodyBinaryAsync(Uri url, HttpMethod httpMethod = HttpMethod.GET,
        byte[] paramsData = null, IDictionary<string, string> headList = null)
    {
        if (url == null)
            throw new Exception("必须设置Url");

        CurrentUri = url;

        MyHttpClient.Timeout = TimeSpan.FromMilliseconds(TimeOut);


        using (var myRequest = new HttpRequestMessage(new System.Net.Http.HttpMethod(httpMethod.ToString()), url))
        {
            myRequest.Headers.AcceptEncoding.TryParseAdd("gzip");
            myRequest.Headers.AcceptEncoding.TryParseAdd("deflate");
            myRequest.Headers.AcceptEncoding.TryParseAdd("br");
            myRequest.Headers.Accept.TryParseAdd(Accpet);
            myRequest.Headers.UserAgent.TryParseAdd(UserAgent);
            
            if (KeepAlive)
                myRequest.Headers.Connection.TryParseAdd("Keep-Alive");
            
            if (!string.IsNullOrEmpty(UrlReferer))
                myRequest.Headers.Referrer = new Uri(UrlReferer); //MyRequest.Referer = UrlReferer;

            if (Date != null)
                myRequest.Headers.Date = Date; //MyRequest.Date = (DateTime) Date;

            if (IfModifiedSince != null)
                //MyRequest.Headers.Add("If-Modified-Since",((DateTime) IfModifiedSince).GetDateTimeFormats('r')[0]);
                myRequest.Headers.IfModifiedSince =
                    IfModifiedSince; //MyRequest.IfModifiedSince = (DateTime) IfModifiedSince;

            if (AuthenticationHeader != null)
                myRequest.Headers.Authorization = AuthenticationHeader;

            if (headList is {Count: > 0})
                foreach (var item in headList)
                    myRequest.Headers.Add(item.Key, item.Value);

            //ServicePoint.Expect100Continue
            //myRequest.Headers.Expect.Add();

            if (httpMethod is HttpMethod.POST or HttpMethod.PUT or HttpMethod.PATCH or HttpMethod.DELETE)
            {
                myRequest.Content = new ByteArrayContent(paramsData);
                if (string.IsNullOrWhiteSpace(ContentType))
                {
                    myRequest.Content.Headers.ContentType =
                        new MediaTypeHeaderValue("application/x-www-form-urlencoded");
                }
                else
                {
                    var contentTypes = ContentType.Split(';');
                    myRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(contentTypes.FirstOrDefault(p=>!p.Contains("="))?.Trim());
                    for (int i = 1; i < contentTypes.Length; i++)
                    {
                        myRequest.Content.Headers.ContentType.Parameters.Add(
                            contentTypes[i].Contains("=")
                                ? new NameValueHeaderValue(contentTypes[i].Split('=')[0].Trim(), contentTypes[i].Split('=')[1].Trim())
                                : new NameValueHeaderValue(contentTypes[i].Trim()));
                    }
                }
                myRequest.Content.Headers.ContentType.Parameters.Add(
                    new NameValueHeaderValue("charset", ParamsEncoding.WebName));
            }

            try
            {
                using (var myResponse = await MyHttpClient.SendAsync(myRequest))
                {
                    CurrentHeaders =
                        myResponse.Headers.ToDictionary(p => p.Key, p => p.Value);

                    using (var myStream = await myResponse.Content.ReadAsStreamAsync())
                    {
                        var responseEncoding = myResponse.Content.Headers.ContentEncoding.ToArray().FirstOrDefault();
                        var headerCharSet = myResponse.Content.Headers.ContentType?.CharSet.Trim('"','\'');

                        CurrentCharset = !string.IsNullOrWhiteSpace(headerCharSet)
                            ? Encoding.GetEncoding(headerCharSet)
                            : Encoding.UTF8;

                        CurrentUri = myResponse.RequestMessage.RequestUri;

                        return ReadCompressionStream(myStream, responseEncoding);
                    }
                }
            }
            catch (WebException webEx)
            {
                if (webEx.Response != null)
                    using (var myStream = webEx.Response.GetResponseStream())
                    {
                        var responseEncoding = (webEx.Response as HttpWebResponse).ContentEncoding?.ToLower();
                        var match = Regex.Match((webEx.Response as HttpWebResponse).ContentType,
                            "charset=(?<charset>[\\w-]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                        if (match.Success && !string.IsNullOrEmpty(match.Groups["charset"].Value))
                            CurrentCharset = !string.IsNullOrWhiteSpace(match.Groups["charset"].Value)
                                ? Encoding.GetEncoding(match.Groups["charset"].Value)
                                : Encoding.UTF8;
                        else
                            CurrentCharset = Encoding.UTF8;

                        CurrentUri = webEx.Response.ResponseUri;

                        return ReadCompressionStream(myStream, responseEncoding);
                    }

                return Encoding.UTF8.GetBytes(webEx.Message);
            }
        }
    }

    
    public async Task<HttpResponseMessage> SendSync(HttpRequestMessage httpRequestMessage)
    {
        MyHttpClient.Timeout = TimeSpan.FromMilliseconds(TimeOut);
        return await MyHttpClient.SendAsync(httpRequestMessage);
    }
    
    public async Task<HttpResponseMessage> PostAsync(Uri requestUri,HttpContent content)
    {
        MyHttpClient.Timeout = TimeSpan.FromMilliseconds(TimeOut);
        return await MyHttpClient.PostAsync(requestUri, content);
    }
    
    public async Task<HttpResponseMessage> GetAsync(Uri requestUri)
    {
        MyHttpClient.Timeout = TimeSpan.FromMilliseconds(TimeOut);
        return await MyHttpClient.GetAsync( requestUri);
    }
    
    public async Task<Stream> GetStreamAsync(Uri requestUri)
    {
        MyHttpClient.Timeout = TimeSpan.FromMilliseconds(TimeOut);
        return await MyHttpClient.GetStreamAsync( requestUri);
    }
    
    public async Task<byte[]> GetByteArrayAsync(Uri requestUri)
    {
        MyHttpClient.Timeout = TimeSpan.FromMilliseconds(TimeOut);
        return await MyHttpClient.GetByteArrayAsync( requestUri);
    }
    
    public async Task<string> GetStringAsync(Uri requestUri)
    {
        MyHttpClient.Timeout = TimeSpan.FromMilliseconds(TimeOut);
        return await MyHttpClient.GetStringAsync( requestUri);
    }


    /// <summary>
    ///     设置Accpet类型
    /// </summary>
    /// <param name="accpetType"></param>
    public void AccpetSet(HttpAccept accpetType)
    {
        switch (accpetType)
        {
            case HttpAccept.All:
                Accpet = "*/*";
                break;
            case HttpAccept.Html:
                Accpet = "text/html, application/xhtml+xml";
                break;
            case HttpAccept.Css:
                Accpet = "text/css";
                break;
            case HttpAccept.Js:
                Accpet = "application/x-javascript";
                break;
            case HttpAccept.Image:
                Accpet = "image/*";
                break;
            case HttpAccept.Text:
                Accpet = "text/*";
                break;
            default:
                Accpet = "*/*";
                break;
        }
    }

    /// <summary>
    ///     设置UserAgent
    /// </summary>
    /// <param name="userAgent"></param>
    public void UserAgentSet(HttpUserAgents userAgent)
    {
        switch (userAgent)
        {
            case HttpUserAgents.IE6_Win:
                UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322)";
                break;
            case HttpUserAgents.IE8_Win:
                UserAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 5.1; SV1; .NET CLR 2.0.50727;)";
                break;
            case HttpUserAgents.IE9_Win:
                UserAgent =
                    "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1; WOW64; Trident/5.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; .NET4.0C; .NET4.0E; BOIE9;ZHCN)";
                break;
            case HttpUserAgents.IE10_Win:
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:10.0) like Gecko";
                break;
            case HttpUserAgents.IE11_Win:
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
                break;
            case HttpUserAgents.Firefox_Win:
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:57.0) Gecko/20100101 Firefox/57.0";
                break;
            case HttpUserAgents.Chrome_Win:
                UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36";
                break;
            case HttpUserAgents.Edge_Mac:
                UserAgent =
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/95.0.4638.54 Safari/537.36 Edg/95.0.1020.40";
                break;
            case HttpUserAgents.Edge_Win:
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:10.0) like Gecko";
                break;
            default:
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:10.0) like Gecko";
                break;
        }
    }


    /// <summary>
    ///     web验证头信息
    /// </summary>
    /// <param name="scheme">"Basic" ...</param>
    /// <param name="value"></param>
    public void AuthorizationSet(string scheme, string value)
    {
        AuthenticationHeader = new AuthenticationHeaderValue(scheme, value);
    }

    #region web代理

    /// <summary>
    ///     设置web代理
    /// </summary>
    /// <param name="url">代理网址</param>
    /// <param name="port">代理端口</param>
    /// <param name="username">代理帐号</param>
    /// <param name="password">代理密码</param>
    public void WebProxySet(string url, int port, string username = "", string password = "")
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            MyHttpClientHandler.Proxy = null;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(username))
                MyHttpClientHandler.Proxy = new WebProxy(url, port)
                {
                    UseDefaultCredentials = true,
                    Credentials = new NetworkCredential(username, password)
                };
            else
                MyHttpClientHandler.Proxy = new WebProxy(url, port)
                {
                    UseDefaultCredentials = false
                };
        }
    }

    #endregion

    public sealed class UpLoadFile
    {
        public byte[] Buffer;
        public string ContentType;
        public string Filename;
        public string Name;

        /// <summary>
        /// </summary>
        /// <param name="fileInfo">FileInfo</param>
        public UpLoadFile(FileInfo fileInfo)
        {
            Name = fileInfo.Name.Replace(fileInfo.Extension, "");
            Buffer = File.ReadAllBytes(fileInfo.FullName);
            Filename = fileInfo.Name;
            ContentType = ExtensionToContentType(fileInfo.Extension);
        }

        /// <summary>
        /// </summary>
        /// <param name="name">Form name for file</param>
        /// <param name="buffer">File Stream</param>
        /// <param name="filename">Name of file</param>
        /// <param name="contentType">Content type of file</param>
        public UpLoadFile(string name, byte[] buffer, string filename = "", string contentType = null)
        {
            Name = name;
            Buffer = buffer;
            Filename = filename;
            ContentType = contentType ?? "application/octet-stream";
        }

        /// <summary>
        /// </summary>
        /// <param name="name">Form name for file</param>
        /// <param name="stream">File Stream</param>
        /// <param name="filename">Name of file</param>
        /// <param name="contentType">Content type of file</param>
        public UpLoadFile(string name, Stream stream, string filename = "", string contentType = null)
        {
            Name = name;
            Buffer = new byte[stream.Length];
            stream.Read(Buffer, 0, Buffer.Length);
            stream.Seek(0, SeekOrigin.Begin);
            Filename = filename;
            ContentType = contentType ?? "application/octet-stream";
        }

        public static string FileExtension(byte[] bytes)
        {
            var filecode = "";
            if (bytes.Length > 2) filecode = bytes[0] + "|" + bytes[1];
            switch (filecode)
            {
                case "71|73":
                    return "gif";
                case "66|77":
                    return "bmp";
                case "255|216":
                    return "jpg";
                case "137|80":
                    return "png";
                case "0|0":
                    return "ico";
                case "56|66":
                    return "psd";
                case "37|80":
                    return "pdf";
                case "208|207":
                    return "doc";
                case "70|114":
                    return "mht";
                case "73|84":
                    return "chm";
                case "255|254":
                    return "txt(Unicode)";
                case "254|255":
                    return "txt(Unicode Big)";
                case "239|187":
                    return "txt(UTF-8)";
                case "60|63":
                    return "xml";
                case "60|33":
                    return "html";
                case "0|1":
                    return "mdb";
                case "100|56":
                    return "torrent";
                case "83|81":
                    return "sqlitedb";
                case "80|75":
                    return "zip";
                case "82|97":
                    return "rar";
                case "55|122":
                    return "7z";
                case "48|130":
                    return "pfm";
                case "45|45":
                    return "cer";
                case "67|87":
                    return "swf";
                case "77|90|":
                    return "exe";
                default:
                    return "";
            }
        }

        public static string ExtensionToContentType(string extension)
        {
            switch (extension.TrimStart('.').ToLowerInvariant())
            {
                case "gif":
                    return "image/gif";
                case "jpg":
                    return "image/jpeg";
                case "png":
                    return "image/png";
                case "bmp":
                    return "image/bmp";
                case "ico":
                    return "image/x-icon";
                case "zip":
                    return "application/x-zip-compressed"; // "application/zip";
                case "avi":
                    return "video/avi";
                case "rmvb":
                    return "application/vnd.rn-realmedia-vbr";
                case "doc":
                    return "application/mswordc";
                case "xls":
                    return "application/vnd.ms-excel";
                case "ppt":
                    return "application/vnd.ms-powerpoint";
                case "pdf":
                    return "application/pdf";
                case "mdb":
                    return "application/msaccess";
                case "txt":
                    return "text/plain";
                case "sql":
                    return "text/plain";
                case "log":
                    return "text/plain";
                case "mp3":
                    return "audio/mpeg";
                case "wav":
                    return "audio/wav";
                case "mp4":
                    return "video/mp4";
                case "html":
                    return "text/html";
                case "htm":
                    return "text/html";
                case "xml":
                    return "text/xml";
                case "js":
                    return "application/x-javascript";
                case "json":
                    return "application/json";
                case "css":
                    return "text/css";
                default:
                    return "application/octet-stream";
            }
        }
    }


    #region Dispose

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
        if (!_mDisposed)
        {
            if (disposing)
            {
                MyHttpClient.Dispose();
                MyHttpClientHandler.Dispose();
                // Release managed resources
            }

            // Release unmanaged resources

            _mDisposed = true;
        }
    }

    ~HttpWebUtility()
    {
        Dispose(false);
    }

    private bool _mDisposed;

    #endregion

    #region 枚举

    /// <summary>
    ///     接受文件类型枚举
    /// </summary>
    public enum HttpAccept
    {
        All = 0,
        Html = 1,
        Css = 2,
        Js = 3,
        Image = 4,
        Text = 5,
        Xml = 6
    }

    /// <summary>
    ///     UserAgent类型枚举
    /// </summary>
    public enum HttpUserAgents
    {
        IE6_Win = 0,
        IE8_Win = 1,
        IE9_Win = 2,
        IE10_Win = 3,
        IE11_Win = 4,
        Edge_Win = 5,
        Firefox_Win = 11,
        Chrome_Win = 12,

        Edge_Mac = 50
    }

    /// <summary>
    ///     请求类型枚举
    /// </summary>
    public enum HttpMethod
    {
        GET = 0,
        POST = 1,
        PUT = 3,
        HEAD = 4,
        DELETE = 5,
        PATCH = 6
    }

    #endregion

    #region 变量

    protected HttpClient MyHttpClient { get; set; }

    public HttpClientHandler MyHttpClientHandler { get; protected set; }
    public Uri CurrentUri { get; protected set; }
    public Encoding CurrentCharset { get; protected set; } = Encoding.UTF8;
    public Dictionary<string, IEnumerable<string>> CurrentHeaders { get; protected set; }

    public string Accpet { get; set; }
    public string ContentType { get; set; }
    public string UserAgent { get; set; }
    public string UrlReferer { get; set; }

    public AuthenticationHeaderValue AuthenticationHeader { get; protected set; }
    public Encoding ParamsEncoding { get; set; }

    // public bool JScriptEscape { get; set; }

    /// <summary>
    ///     超时时长 毫秒
    /// </summary>
    public int TimeOut { get; set; }

    public bool KeepAlive { get; set; }


    /// <summary>
    ///     最后修改日期
    /// </summary>
    public DateTime? IfModifiedSince { get; set; }

    public DateTime? Date { get; set; }

    #endregion


    #region 静态全局方法

    public static string AuthorizationBasic(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("null param", nameof(username));

        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("null param", nameof(password));

        var bytes = Encoding.UTF8.GetBytes($"{username}:{password}");
        return Convert.ToBase64String(bytes);
    }

    public static string[] DomainSplit(string domain)
    {
        const string pattern =
            "([a-z0-9--]{1,200})\\.(ac.cn|bj.cn|sh.cn|tj.cn|cq.cn|he.cn|sn.cn|sx.cn|nm.cn|ln.cn|jl.cn|hl.cn|js.cn|zj.cn|ah.cn|fj.cn|jx.cn|sd.cn|ha.cn|hb.cn|hn.cn|gd.cn|gx.cn|hi.cn|sc.cn|gz.cn|yn.cn|gs.cn|qh.cn|nx.cn|xj.cn|tw.cn|hk.cn|mo.cn|xz.cn" +
            "|com.cn|net.cn|org.cn|gov.cn|.com.hk|我爱你|在线|中国|网址|网店|中文网|公司|网络|集团" +
            "|com|cn|cc|org|net|xin|xyz|vip|shop|top|club|wang|fun|info|online|tech|store|site|ltd|ink|biz|group|link|work|pro|mobi|ren|kim|name|tv|red" +
            "|cool|team|live|pub|company|zone|today|video|art|chat|gold|guru|show|life|love|email|fund|city|plus|design|social|center|world|auto|.rip|.ceo|.sale|.hk|.io|.gg|.tm|.gs|.us)$";

        var regexMainDoamin =
            new Regex(pattern,
                RegexOptions.IgnoreCase);

        var m = regexMainDoamin.Match(domain);
        if (m.Success)
        {
            var mainDomain = m.Value;
            var subDomain = domain.Replace("." + mainDomain, "");

            return new[] {subDomain, mainDomain};
        }

        return null;
    }

    public static Dictionary<string, string> BuildParameters(NameValueCollection paramList)
    {
        return paramList.Cast<KeyValuePair<string, string>>()
            .ToDictionary(item => item.Key, item => item.Value);
    }

    public static string BuildQueryString(IDictionary<string, string> paramList, Encoding encoding = null)
    {
        var querystr = string.Empty;
        if (paramList != null && paramList.Count > 0)
            querystr = paramList.Aggregate(querystr,
                (current, item) =>
                    current + UriDataEncode(item.Key, encoding) + (!string.IsNullOrEmpty(item.Value)
                        ? "=" + UriDataEncode(item.Value, encoding)
                        : "=") + "&").TrimEnd('&');

        return querystr;
    }

    public static string UriDataEncode(string input, Encoding encoding = null)
    {
        //http://www.w3school.com.cn/tags/html_ref_urlencode.html
        encoding = encoding ?? Encoding.UTF8;
        if (string.IsNullOrEmpty(input))
            return "";
        return HttpUtility.UrlEncode(input, encoding).Replace("+", "%20").Replace("!", "%21").Replace(".", "%2e")
            .Replace("*", "%2a").Replace("(", "%28").Replace(")", "%29").Replace("_", "%5f");
    }

    #endregion

    #region Cookies

    public string GetCookie(Uri url, string name)
    {
        var cookie = MyHttpClientHandler.CookieContainer.GetCookies(url).Cast<Cookie>()
            .FirstOrDefault(item => item.Name == name);
        if (cookie != null) return cookie.Value;

        return null;
    }

    public Dictionary<string, Cookie> GetCookiesAll(Uri url)
    {
        return MyHttpClientHandler.CookieContainer.GetCookies(url).Cast<Cookie>().ToDictionary(item => item.Name);
    }

    public void SetCookie(Uri url, string name, string value)
    {
        SetCookie(url, name, value, TimeSpan.FromDays(1));
    }

    public void SetCookie(Uri url, string name, string value, TimeSpan exprires)
    {
        var cookie = MyHttpClientHandler.CookieContainer.GetCookies(url).Cast<Cookie>()
            .FirstOrDefault(item => item.Name == name);
        if (cookie != null)
            cookie.Value = value;
        else
            MyHttpClientHandler.CookieContainer.Add(new Cookie
            {
                Name = name,
                Value = value,
                Expires = DateTime.Now.Add(exprires),
                Domain = url.Host
            });
    }

    public void SetCookie(Uri url, Cookie cookie)
    {
        var data = MyHttpClientHandler.CookieContainer.GetCookies(url).Cast<Cookie>()
            .FirstOrDefault(item => item.Name == cookie.Name);

        if (data != null)
        {
            data.Expires = cookie.Expires;
            data.Domain = cookie.Domain;
            data.Path = cookie.Path;
            data.Value = cookie.Value;
            data.HttpOnly = cookie.HttpOnly;
            data.Secure = cookie.Secure;
            data.Discard = cookie.Discard;
        }
        else
        {
            MyHttpClientHandler.CookieContainer.Add(cookie);
        }
    }

    #endregion

    #region 客户端证书

    /// <summary>
    ///     设置客户端证书
    /// </summary>
    /// <param name="certificateBuff">证书字节流</param>
    /// <param name="certificatePassword">证书密码(可为空)</param>
    public void ClientCertificateSet(byte[] certificateBuff, string certificatePassword = "")
    {
        var certificate = new X509Certificate2(certificatePassword != null
            ? new X509Certificate2(certificateBuff, certificatePassword)
            : new X509Certificate2(certificateBuff));

        ClientCertificateSet(certificate);
    }

    /// <summary>
    ///     设置客户端证书
    /// </summary>
    /// <param name="certificate">X509证书</param>
    /// <returns></returns>
    public void ClientCertificateSet(X509Certificate2 certificate)
    {
        //         new X509Certificate2(CertificatePassword != null
        //             ? new X509Certificate2(CertificateBuff, CertificatePassword)
        //             : new X509Certificate2(CertificateBuff))
        if (!MyHttpClientHandler.ClientCertificates.Contains(certificate))
            MyHttpClientHandler.ClientCertificates.Add(certificate);
    }

    #endregion

    #region 私用方法

    protected readonly Func<Stream, string, byte[]> ReadCompressionStream = (inputStream, compression) =>
    {
        if (inputStream != null)
        {
            Stream stream;
            switch (compression?.ToLower())
            {
                case "gzip":
                    stream = new GZipStream(inputStream, CompressionMode.Decompress);
                    break;
                case "deflate":
                    stream = new DeflateStream(inputStream, CompressionMode.Decompress);
                    break;
                case "br":
                    stream = new BrotliStream(inputStream, CompressionMode.Decompress);
                    break;
                default:
                    stream = inputStream;
                    break;
            }

            var buffer = new List<byte>();
            int byteCurrent;
            do
            {
                byteCurrent = stream.ReadByte();
                if (byteCurrent != -1)
                    buffer.Add((byte) byteCurrent);
            } while (byteCurrent != -1);

            stream.Dispose();

            return buffer.ToArray();
        }

        return Array.Empty<byte>();
    };

    protected static string GetBoundary =>  $"-----{Convert.ToInt64(DateTime.Now.Ticks):x}-----";

    private static byte[] BuildPostData(IDictionary<string, string> postParams, IList<UpLoadFile> uploadFiles,
        Encoding encoding, string boundary = "")
    {
        using (var stream = new MemoryStream())
        {
            var header = encoding.GetBytes($"--{boundary}" + "\r\n");
            var newline = encoding.GetBytes("\r\n");

            stream.Write(header, 0, header.Length);
            const string fileheadTemplate =
                "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";

            foreach (var uploadFile in uploadFiles)
            {
                var filehead = encoding.GetBytes(string.Format(fileheadTemplate, uploadFile.Name,
                    uploadFile.Filename, uploadFile.ContentType));
                stream.Write(filehead, 0, filehead.Length);
                stream.Write(uploadFile.Buffer, 0, uploadFile.Buffer.Length);
                stream.Write(newline, 0, newline.Length);
            }


            foreach (var param in postParams)
            {
                stream.Write(header, 0, header.Length);
                const string dataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}\r\n";
                var data = encoding.GetBytes(string.Format(dataTemplate, param.Key, param.Value));
                stream.Write(data, 0, data.Length);
            }

            var footer = encoding.GetBytes($"--{boundary}--");
            stream.Write(footer, 0, footer.Length);
            stream.Write(newline, 0, newline.Length);

            stream.Seek(0, SeekOrigin.Begin);

            var contentBuff = new byte[stream.Length];
            stream.Read(contentBuff, 0, contentBuff.Length);
            stream.Close();
            return contentBuff;
        }
    }

    #endregion
}
