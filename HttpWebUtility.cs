using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace JSDXTS
{
    public class HttpWebUtility : IDisposable
    {
        public HttpWebUtility()
        {
            ServicePointManager.DefaultConnectionLimit = 500;

            MyCookieContainer = new CookieContainer();

            AccpetSet(HttpAccept.All);
            UserAgentSet(HttpUserAgents.Ie11);

            MyWebProxy = null;

            ParamsEncoding = Encoding.UTF8;
            UrlReferer = string.Empty;
            JScriptEscape = false;
            TimeOut = 15000;
            KeepAlive = false;
            Expect100 = false;
            IfModifiedSince = null;
            Date = null;
            AllowAutoRedirect = false;
        }

        public static string Authorization(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("null param", nameof(username));

            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("null param", nameof(password));

            var bytes = Encoding.UTF8.GetBytes($"{username}:{password}");
            var base64 = Convert.ToBase64String(bytes);
            return $"Basic {base64}";
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

        public static Dictionary<string, string> BuildParameters(NameValueCollection p_paramList)
        {
            return p_paramList.Cast<KeyValuePair<string, string>>().ToDictionary(item => item.Key, item => item.Value);
        }

        public static string BuildQueryString(IDictionary<string, string> p_paramList, Encoding encoding = null)
        {
            var querystr = string.Empty;
            if (p_paramList != null && p_paramList.Count > 0)
                querystr = p_paramList.Aggregate(querystr,
                    (p_current, item) =>
                        p_current + UriDataEncode(item.Key, encoding) + (!string.IsNullOrEmpty(item.Value)
                            ? "=" + UriDataEncode(item.Value, encoding)
                            : "=") + "&").TrimEnd('&');

            return querystr;
        }

        public static string UriDataEncode(string p_input, Encoding encoding = null)
        {
            //http://www.w3school.com.cn/tags/html_ref_urlencode.html
            encoding ??= Encoding.UTF8;
            if (string.IsNullOrEmpty(p_input))
                return "";
            return HttpUtility.UrlEncode(p_input, encoding).Replace("+", "%20").Replace("!", "%21").Replace(".", "%2e")
                .Replace("*", "%2a").Replace("(", "%28").Replace(")", "%29").Replace("_", "%5f");
        }

        public string Response(Uri p_url, HttpMethod p_httpMethod = HttpMethod.Get,
            Dictionary<string, string> p_paramList = null, Dictionary<string, string> p_headList = null,
            Encoding p_encoding = null, UpLoadFile p_upLoadFile = null)
        {
            string resultStr;
            p_encoding ??= Encoding.UTF8;

            var myMemoryStream =
                new MemoryStream(ResponseBinary(p_url, p_httpMethod, p_paramList, p_headList, p_upLoadFile));
            using (var streamReader = new StreamReader(myMemoryStream, p_encoding))
            {
                resultStr = streamReader.ReadToEnd();
                var match = RegexCharSet.Match(resultStr);
                if (match.Success && !string.IsNullOrEmpty(match.Groups["charset"].Value))
                {
                    var tempencoding = Encoding.GetEncoding(match.Groups["charset"].Value);
                    if (p_encoding.WebName != tempencoding.WebName)
                    {
                        p_encoding = tempencoding;
                        myMemoryStream.Seek(0, SeekOrigin.Begin);
                        using (var streamReaderRe = new StreamReader(myMemoryStream, p_encoding))
                        {
                            resultStr = streamReaderRe.ReadToEnd();
                        }
                    }
                }
            }


            //if (JScriptEscape)
            //{
            //    ResultStr = Microsoft.JScript.GlobalObject.unescape(ResultStr);
            //}

            return resultStr;
        }

        public string ResponseBody(Uri p_url, HttpMethod p_httpMethod = HttpMethod.Get,
            string p_queryData = null, Dictionary<string, string> p_headList = null,
            Encoding p_encoding = null, string p_boundary = null)
        {
            p_encoding ??= Encoding.UTF8;

            return p_encoding.GetString(ResponseBodyBinary(p_url, p_httpMethod,
                p_queryData == null ? null : p_encoding.GetBytes(p_queryData),
                p_headList, p_boundary));
        }

        public byte[] ResponseBinary(Uri p_url, HttpMethod p_httpMethod = HttpMethod.Get,
            Dictionary<string, string> p_paramList = null, Dictionary<string, string> p_headList = null,
            UpLoadFile p_upLoadFile = null)
        {
            if (p_httpMethod == HttpMethod.Get || p_httpMethod == HttpMethod.Head)
                if (p_paramList != null && p_paramList.Count > 0)
                {
                    var ub = new UriBuilder(p_url);
                    if (!string.IsNullOrWhiteSpace(ub.Query))
                        ub.Query = ub.Query.Substring(1) + "&" + BuildQueryString(p_paramList, ParamsEncoding);
                    else
                        ub.Query = BuildQueryString(p_paramList, ParamsEncoding);

                    p_paramList.Clear();
                    p_url = new Uri(ub.Uri.ToString());
                }

            var queryData = new byte[0];
            string boundary = null;
            if (p_httpMethod == HttpMethod.Post || p_httpMethod == HttpMethod.Put || p_httpMethod == HttpMethod.Patch ||
                p_httpMethod == HttpMethod.Delete)
            {
                p_paramList ??= new Dictionary<string, string>();

                if (p_upLoadFile != null)
                {
                    boundary = GetBoundary();
                    queryData = BuildPostData(p_paramList, ParamsEncoding, p_upLoadFile, boundary);
                }
                else
                {
                    queryData = ParamsEncoding.GetBytes(BuildQueryString(p_paramList, ParamsEncoding));
                }
            }

            return ResponseBodyBinary(p_url, p_httpMethod, queryData, p_headList, boundary);
        }

        public byte[] ResponseBodyBinary(Uri p_url, HttpMethod p_httpMethod = HttpMethod.Get,
            byte[] p_queryData = null, Dictionary<string, string> p_headList = null, string p_boundary = null)
        {
            if (p_url == null)
                throw new Exception("必须设置Url");

            CurrentUri = p_url;

            var resultBinary = new byte[0];

            var myRequest = WebRequest.Create(p_url) as HttpWebRequest;
            if (myRequest != null)
            {
                myRequest.CookieContainer = MyCookieContainer;
                myRequest.Headers.Add("Accept-Encoding", "gzip, deflate, br");
                myRequest.Method = p_httpMethod.ToString();
                myRequest.ContentType = ContentType;
                myRequest.Accept = Accpet;
                myRequest.UserAgent = UserAgent;
                myRequest.Timeout = TimeOut;
                myRequest.KeepAlive = KeepAlive;

                if (Date != null) myRequest.Date = (DateTime) Date;

                if (IfModifiedSince != null) myRequest.IfModifiedSince = (DateTime) IfModifiedSince;

                if (!string.IsNullOrEmpty(UrlReferer)) myRequest.Referer = UrlReferer;

                if (p_headList != null && p_headList.Count > 0)
                    foreach (var item in p_headList)
                        myRequest.Headers[item.Key] = item.Value;


                myRequest.Proxy = MyWebProxy;

                myRequest.AllowAutoRedirect = AllowAutoRedirect;

                if (p_url.Scheme == "https")
                {
                    ServicePointManager.ServerCertificateValidationCallback = CheckValidationResult;
                    ServicePointManager.Expect100Continue = Expect100;
                }

                if (CertificateBuff != null)
                    myRequest.ClientCertificates.Add(CertificatePassword != null
                        ? new X509Certificate2(CertificateBuff, CertificatePassword)
                        : new X509Certificate2(CertificateBuff));

                if (p_httpMethod == HttpMethod.Post || p_httpMethod == HttpMethod.Put ||
                    p_httpMethod == HttpMethod.Patch || p_httpMethod == HttpMethod.Delete)
                {
                    if (p_boundary != null)
                    {
                        myRequest.ContentType = string.IsNullOrEmpty(myRequest.ContentType)
                            ? "multipart/form-data; charset=" + ParamsEncoding.WebName + "; boundary=" + p_boundary
                            : myRequest.ContentType;
                        myRequest.AllowWriteStreamBuffering = true;
                        if (p_queryData != null && p_queryData.Length > 0)
                        {
                            using (var myStream = myRequest.GetRequestStream())
                            {
                                myStream.Write(p_queryData, 0, p_queryData.Length);
                                myStream.Flush();
                            }
                        }
                    }
                    else
                    {
                        myRequest.ContentType = string.IsNullOrEmpty(myRequest.ContentType)
                            ? "application/x-www-form-urlencoded; charset=" + ParamsEncoding.WebName + ";"
                            : myRequest.ContentType;
                        if (p_queryData != null && p_queryData.Length > 0)
                        {
                            using (var myStream = myRequest.GetRequestStream())
                            {
                                myStream.Write(p_queryData, 0, p_queryData.Length);
                                myStream.Flush();
                            }
                        }
                    }
                }

                try
                {
                    using (var myResponse = myRequest.GetResponse() as HttpWebResponse)
                    {
                        resultBinary = ReadStream(myResponse.GetResponseStream(),
                            myResponse.ContentEncoding?.ToLower());
                        
                        CurrentUri = myResponse.ResponseUri;
                    }
                }
                catch (WebException webEx)
                {
                    if (webEx.Response != null)
                    {
                        resultBinary = ReadStream(webEx.Response.GetResponseStream(),
                            (webEx.Response as HttpWebResponse).ContentEncoding?.ToLower());

                        CurrentUri = webEx.Response.ResponseUri;
                    }
                    else
                    {
                        resultBinary = Encoding.UTF8.GetBytes(webEx.Message);
                    }
                }
                finally
                {
                    myRequest.Abort();
                }
            }

            return resultBinary;
        }

        protected Func<Stream, string, byte[]> ReadStream = (inputStream, compression) =>
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
                int bytenow;
                do
                {
                    bytenow = stream.ReadByte();
                    if (bytenow != -1)
                        buffer.Add((byte) bytenow);
                } while (bytenow != -1);

                stream.Dispose();

                return buffer.ToArray();
            }

            return new byte[0];
        };

        public class UpLoadFile
        {
            public byte[] Buffer;
            public string ContentType;
            public string Filename;
            public string Name;

            /// <summary>
            /// </summary>
            /// <param name="p_fileInfo">FileInfo</param>
            public UpLoadFile(FileInfo p_fileInfo)
            {
                Name = p_fileInfo.Name.Replace(p_fileInfo.Extension, "");
                Buffer = File.ReadAllBytes(p_fileInfo.FullName);
                Filename = p_fileInfo.Name;
                ContentType = ExtensionToContentType(p_fileInfo.Extension);
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

            public static string ExtensionToContentType(string p_extension)
            {
                switch (p_extension.TrimStart('.').ToLowerInvariant())
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
                    MyCookieContainer = null;
                    MyWebProxy = null;
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
            Xml = 5
        }

        /// <summary>
        ///     UserAgent类型枚举
        /// </summary>
        public enum HttpUserAgents
        {
            Ie6 = 0,
            Ie8 = 1,
            Ie9 = 2,
            Ie10 = 3,
            Ie11 = 4,
            Edge = 5,
            Firefox = 11,
            Chrome = 12
        }

        /// <summary>
        ///     请求类型枚举
        /// </summary>
        public enum HttpMethod
        {
            Get = 0,
            Post = 1,
            Put = 3,
            Head = 4,
            Delete = 5,
            Patch = 6
        }

        #endregion

        #region 变量

        private CookieContainer MyCookieContainer { get; set; }

        private WebProxy MyWebProxy { get; set; }

        private static readonly Regex RegexCharSet = new("charset=(?<charset>[\\w-]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public string Accpet { get; set; }
        public string ContentType { get; set; }
        public string UserAgent { get; set; }
        public string UrlReferer { get; set; }

        public Encoding ParamsEncoding { get; set; }
        public bool JScriptEscape { get; set; }
        public int TimeOut { get; set; }
        public bool KeepAlive { get; set; }
        public bool Expect100 { get; set; }
        public Uri CurrentUri { get; private set; }

        public bool AllowAutoRedirect { get; set; }

        /// <summary>
        ///     最后修改日期
        /// </summary>
        public DateTime? IfModifiedSince { get; set; }

        public DateTime? Date { get; set; }

        /// <summary>
        ///     证书字节数组
        /// </summary>
        public byte[] CertificateBuff { get; set; }

        /// <summary>
        ///     证书密码
        /// </summary>
        public string CertificatePassword { get; set; }

        #endregion

        #region 设置HttpWebUtility

        /// <summary>
        ///     设置Accpet类型
        /// </summary>
        /// <param name="p_accpetType"></param>
        public void AccpetSet(HttpAccept p_accpetType)
        {
            switch (p_accpetType)
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
        /// <param name="p_userAgent"></param>
        public void UserAgentSet(HttpUserAgents p_userAgent)
        {
            switch (p_userAgent)
            {
                case HttpUserAgents.Ie6:
                    UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1; SV1; .NET CLR 1.1.4322)";
                    break;
                case HttpUserAgents.Ie8:
                    UserAgent = "Mozilla/4.0 (compatible; MSIE 8.0; Windows NT 5.1; SV1; .NET CLR 2.0.50727;)";
                    break;
                case HttpUserAgents.Ie9:
                    UserAgent =
                        "Mozilla/4.0 (compatible; MSIE 7.0; Windows NT 6.1; WOW64; Trident/5.0; SLCC2; .NET CLR 2.0.50727; .NET CLR 3.5.30729; .NET CLR 3.0.30729; .NET4.0C; .NET4.0E; BOIE9;ZHCN)";
                    break;
                case HttpUserAgents.Ie10:
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:10.0) like Gecko";
                    break;
                case HttpUserAgents.Ie11:
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:11.0) like Gecko";
                    break;
                case HttpUserAgents.Firefox:
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; rv:57.0) Gecko/20100101 Firefox/57.0";
                    break;
                case HttpUserAgents.Chrome:
                    UserAgent =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36";
                    break;
                default:
                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64; Trident/7.0; rv:10.0) like Gecko";
                    break;
            }
        }

        /// <summary>
        ///     设置web代理
        /// </summary>
        /// <param name="p_url">代理网址</param>
        /// <param name="p_port">代理端口</param>
        /// <param name="p_username">代理帐号</param>
        /// <param name="p_password">代理密码</param>
        public void WebProxySet(string p_url, int p_port, string p_username = "", string p_password = "")
        {
            if (string.IsNullOrWhiteSpace(p_url))
            {
                MyWebProxy = null;
            }
            else
            {
                MyWebProxy = new WebProxy(p_url, p_port);
                if (!string.IsNullOrWhiteSpace(p_username))
                {
                    MyWebProxy.Credentials = new NetworkCredential(p_username, p_password);
                    MyWebProxy.UseDefaultCredentials = true;
                }
                else
                {
                    MyWebProxy.UseDefaultCredentials = false;
                }
            }
        }


        /// <summary>
        ///     设置加密证书
        /// </summary>
        /// <param name="p_certificatePath">证书文件路径</param>
        /// <param name="p_certificatePassword">证书密码</param>
        public void CertificateSet(string p_certificatePath, string p_certificatePassword = "")
        {
            if (File.Exists(p_certificatePath))
                CertificateSet(File.ReadAllBytes(p_certificatePath), p_certificatePassword);
        }

        /// <summary>
        ///     设置加密证书
        /// </summary>
        /// <param name="p_certificateBuff">证书字节流</param>
        /// <param name="p_certificatePassword">证书密码</param>
        public void CertificateSet(byte[] p_certificateBuff, string p_certificatePassword = "")
        {
            CertificateBuff = p_certificateBuff;
            CertificatePassword = p_certificatePassword;
        }

        #endregion

        #region Cookies操作

        public string GetCookie(Uri p_url, string p_name)
        {
            var cookie = MyCookieContainer.GetCookies(p_url)
                .FirstOrDefault(item => item.Name == p_name);
            if (cookie != null) return cookie.Value;

            return null;
        }

        public Dictionary<string, Cookie> GetCookiesAll(Uri p_url)
        {
            return MyCookieContainer.GetCookies(p_url).ToDictionary(item => item.Name);
        }

        public void SetCookie(Uri p_url, string p_name, string p_value)
        {
            SetCookie(p_url, p_name, p_value, TimeSpan.FromDays(1));
        }

        public void SetCookie(Uri p_url, string p_name, string p_value, TimeSpan p_exprires)
        {
            var cookie = MyCookieContainer.GetCookies(p_url)
                .FirstOrDefault(item => item.Name == p_name);
            if (cookie != null)
                cookie.Value = p_value;
            else
                MyCookieContainer.Add(new Cookie
                {
                    Name = p_name,
                    Value = p_value,
                    Expires = DateTime.Now.Add(p_exprires),
                    Domain = p_url.Host
                });
        }

        public void SetCookie(Uri p_url, Cookie p_cookie)
        {
            var cookie = MyCookieContainer.GetCookies(p_url)
                .FirstOrDefault(item => item.Name == p_cookie.Name);
            
            if (cookie != null)
            {
                cookie.Expires = p_cookie.Expires;
                cookie.Domain = p_cookie.Domain;
                cookie.Path = p_cookie.Path;
                cookie.Value = p_cookie.Value;
                cookie.HttpOnly = p_cookie.HttpOnly;
                cookie.Secure = p_cookie.Secure;
                cookie.Discard = p_cookie.Discard;
            }
            else
            {
                MyCookieContainer.Add(p_cookie);
            }
        }

        #endregion

        #region 私用方法

        private bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain,
            SslPolicyErrors errors)
        {
            //直接确认，否则打不开
            return true;
        }

        private static string GetBoundary()
        {
            return string.Format("---------------------------{0}", DateTime.Now.Ticks.ToString("x"));
        }

        private static byte[] BuildPostData(IEnumerable<KeyValuePair<string, string>> p_postParamList,
            Encoding p_encoding, UpLoadFile p_upLoadFile = null, string boundary = "")
        {
            var stream = new MemoryStream();
            var header = p_encoding.GetBytes(string.Format("--{0}", boundary) + "\r\n");
            var newline = p_encoding.GetBytes("\r\n");

            if (null != p_upLoadFile)
            {
                stream.Write(header, 0, header.Length);
                const string fileheadTemplate =
                    "Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n";
                var filehead = p_encoding.GetBytes(string.Format(fileheadTemplate, p_upLoadFile.Name,
                    p_upLoadFile.Filename, p_upLoadFile.ContentType));
                stream.Write(filehead, 0, filehead.Length);
                stream.Write(p_upLoadFile.Buffer, 0, p_upLoadFile.Buffer.Length);
                stream.Write(newline, 0, newline.Length);
            }

            foreach (var param in p_postParamList)
            {
                stream.Write(header, 0, header.Length);
                const string dataTemplate = "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}\r\n";
                var data = p_encoding.GetBytes(string.Format(dataTemplate, param.Key, param.Value));
                stream.Write(data, 0, data.Length);
            }

            var footer = p_encoding.GetBytes(string.Format("--{0}--", boundary));
            stream.Write(footer, 0, footer.Length);
            stream.Write(newline, 0, newline.Length);

            stream.Seek(0, SeekOrigin.Begin);

            var contentBuff = new byte[stream.Length];
            stream.Read(contentBuff, 0, contentBuff.Length);
            stream.Close();
            return contentBuff;
        }

        #endregion
    }
}