
//#define USE_CefSharp
/**
 * Brutile GoogleV3
 *
 * Copyright 2012 Peter Löfås
  * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either 
 * version 2.1 of the License, or (at your option) any later version.
 * 
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 * 
 * You should have received a copy of the GNU Lesser General Public 
 * License along with this library.  If not, see <http://www.gnu.org/licenses/>.
 * 
 **/
using BruTile.PreDefined;
using Common.Logging;
using mshtml;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace BruTile.GoogleMaps
{
    public class GoogleV3TileSchema : ITileSchema, IDisposable
    {
#if USE_CefSharp
        private CefSharp.WinForms.WebView m_webView;
#else
        WebBrowser m_webBrowser;
        
#endif
        static readonly object m_locker = new object();
        private readonly string m_gmeClientID;
        private readonly string m_googleChannel;
        private readonly string m_referer;
        internal GoogleV3TileSource.MapTypeId MapType;
        Thread wbThread;
        ApplicationContext m_appContext;
        internal string[] MapUrlTemplates = null;
        internal string[] OverlayUrlTemplates = null;

        static readonly Dictionary<string, string[]> m_cachedUrLs = new Dictionary<string, string[]>();

        static readonly ILog m_logger = LogManager.GetLogger(typeof(GoogleV3TileSchema));


        public GoogleV3TileSchema(string gmeClientID, string googleChannel, string referer, GoogleV3TileSource.MapTypeId mapType)
        {
            MapType = mapType;
            Height = 256;
            Width = 256;
            Extent = new Extent(-20037508.342789, -20037508.342789, 20037508.342789, 20037508.342789);
            OriginX = -20037508.342789;
            OriginY = 20037508.342789;
            Name = "GoogleSchema";
            Format = "png";
            Axis = AxisDirection.InvertedY;
            Srs = "EPSG:3857";

            m_gmeClientID = gmeClientID;
            m_googleChannel = googleChannel;
            m_referer = referer;
            if (m_cachedUrLs.ContainsKey(mapType + "_base"))
            {
                MapUrlTemplates = m_cachedUrLs[mapType + "_base"];
            }
            if (m_cachedUrLs.ContainsKey(mapType + "_overlay"))
            {
                OverlayUrlTemplates = m_cachedUrLs[mapType + "_overlay"];
            }
            m_appContext = new ApplicationContext();


           /* var frm = new Form();
            frm.Show();
            frm.Size = new System.Drawing.Size(600, 400);
            Label l = new Label();
            l.Text = "Test";

            WebBrowser bw = new WebBrowser();
            bw.Size = new System.Drawing.Size(600, 400);
            frm.Size = new System.Drawing.Size(600, 400);

            frm.Controls.Add(l);
            frm.Controls.Add(bw);
            */

            wbThread = new Thread(() =>
            {
               try
                {
#if USE_CefSharp
                    var settings = new CefSharp.Settings
                    {
                        PackLoadingDisabled = true,
                    };

                    if (CEF.Initialize(settings))
                    {
                        m_webView = new WebView();
                        m_webView.PropertyChanged += WebViewOnPropertyChanged;
                        m_webView.Address = referer;

                    }
#else

                    m_webBrowser = new WebBrowser();
                    m_webBrowser.Navigating += m_WebBrowser_Navigating;
                    m_webBrowser.Visible = true;
                    m_webBrowser.ScrollBarsEnabled = false;
                    m_webBrowser.Size = new System.Drawing.Size(600, 400);
                    m_webBrowser.ScriptErrorsSuppressed = true;
                    m_webBrowser.DocumentCompleted += m_WebBrowser_DocumentCompleted;


                    //bw.Invoke(new MethodInvoker(delegate
                    //{
                    //    bw.Navigating += new WebBrowserNavigatingEventHandler(m_WebBrowser_Navigating);
                    //    bw.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(m_WebBrowser_DocumentCompleted);
                    //    bw.DocumentText =
                    //        "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\"><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><style>BODY { background-color: red;}</style></head><body></body></html>";
                    //}));

                    //m_webBrowser = bw;

                    if (!string.IsNullOrEmpty(referer))
                    {
                        m_webBrowser.Navigate(referer);
                    }
                    else
                    {
                        m_webBrowser.DocumentText = "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\"><html xmlns=\"http://www.w3.org/1999/xhtml\"><body></body></html>";

                    }
#endif

                    if (m_appContext != null)
                    {
                        Application.Run(m_appContext);
                    }
                }
                catch (Exception ee)
                {
                    m_logger.Error("Exception in WebBrowserThread, quitting", ee);
                }
            });
            wbThread.Name = "WebBrowser Thread";
            wbThread.SetApartmentState(ApartmentState.STA);
            wbThread.Start();
            if (m_logger.IsDebugEnabled)
                m_logger.Debug("WebBrowserThread Started");
        }
#if USE_CefSharp
        private bool m_webViewReady = false;
        private void WebViewOnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName.Equals("IsBrowserInitialized", StringComparison.OrdinalIgnoreCase))
            {
                m_webViewReady = m_webView.IsBrowserInitialized;
                if (m_webViewReady)
                {
                    m_webView.Address = referer;
                    /* string resourceName = "Yaircc.UI.default.htm";
                    using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            this.WebView.LoadHtml(reader.ReadToEnd());
                        }
                    }*/
                }
            }

            // Once the HTML has finished loading, begin loading the initial content.
            if (e.PropertyName.Equals("IsLoading", StringComparison.OrdinalIgnoreCase))
            {
                if (!m_webView.IsLoading)
                {
                    /*this.SetSplashText();
                    if (this.type == IRCTabType.Console)
                    {
                        this.SetupConsoleContent();
                    }

                    GlobalSettings settings = GlobalSettings.Instance;
                    this.LoadTheme(settings.ThemeFileName);
                    
                    if (this.webViewInitialised != null)
                    {
                        this.webViewInitialised.Invoke();
                    }*/
                }
            }
        }
#endif

        bool m_mapsAdded;
#if USE_CefSharp
#else

        void m_WebBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (!string.IsNullOrEmpty(m_referer) && e.Url.Host == new Uri(m_referer).Host)
            {
                var ms = new MemoryStream();
                var sw = new StreamWriter(ms);
                sw.WriteLine("HTTP/1.1 200 OK");
                sw.WriteLine("Server: Brutile");
                sw.WriteLine("Content-Type: text/html");
                sw.WriteLine("Connection: close");
                const string resp = "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\"><html xmlns=\"http://www.w3.org/1999/xhtml\"><head></head><body></body></html>";
                sw.WriteLine("Content-Length: " + resp.Length);
                sw.WriteLine();
                sw.Write(resp);
                sw.Flush();
                ms.Seek(0,SeekOrigin.Begin);

                m_webBrowser.DocumentStream = ms;
            }
        }


        private void AddMaps(WebBrowser browser)
        {
            string googleURL = "http://maps.googleapis.com/maps/api/js?libraries=&sensor=false&callback=init";
            if (!string.IsNullOrEmpty(m_gmeClientID))
                googleURL += "&client=" + m_gmeClientID;
            if (!string.IsNullOrEmpty(m_googleChannel))
                googleURL += "&channel=" + m_googleChannel;

            //Clear body
            if (browser.Document != null)
            {
                if (browser.Document.Body != null)
                {
                    HtmlElement htEl = browser.Document.CreateElement("script");
                    if (htEl != null)
                    {
                        htEl.SetAttribute("type", "text/javascript");
                        Type t = htEl.DomElement.GetType();
                        t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[]
                        {
                            @"function modContent() 
{
var head = document.getElementsByTagName(""head"")[0];
/*while (head.getElementsByTagName(""META"").length > 0) 
{
   head.removeChild(head.getElementsByTagName(""META"")[0]);
}*/
while (head.getElementsByTagName(""link"").length > 0) 
{
   head.removeChild(document.getElementsByTagName(""link"")[0]);
}
while (head.getElementsByTagName(""script"").length > 0) 
{
   head.removeChild(document.getElementsByTagName(""script"")[0]);
}  
while (head.getElementsByTagName(""style"").length > 0) 
{
   head.removeChild(document.getElementsByTagName(""style"")[0]);
}  
if (document.body) 
{document.body.innerHTML = """"
;}}
function getContent()
{
    return document.getElementsByTagName(""head"")[0].innerHTML;
}
"
                        });


                        if (browser.Document.Body != null)
                        {
                            browser.Document.Body.AppendChild(htEl);
                        }
                        browser.Document.InvokeScript("modContent");
                    }


                    htEl = browser.Document.CreateElement("script");
                    if (htEl != null)
                    {
                        htEl.SetAttribute("type", "text/javascript");
                        Type t = htEl.DomElement.GetType();
                        t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[]
                        {
                            "function addGoogle() { var sc = document.createElement(\"SCRIPT\"); sc.type=\"text/javascript\"; sc.src=\"" + googleURL +
                            "\";document.body.appendChild(sc);}"
                        });
                        if (browser.Document.Body != null)
                        {
                            browser.Document.Body.AppendChild(htEl);
                        }
                    }



                    htEl = browser.Document.CreateElement("script");
                    if (htEl != null)
                    {
                        htEl.SetAttribute("type", "text/javascript");
                        Type t = htEl.DomElement.GetType();
                        t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[]
                        {
                            "baseLayer = \"google.maps.MapTypeId." + MapType + "\";" + getOpenLayersCode()
                        });
                        if (browser.Document.Body != null)
                        {
                            browser.Document.Body.AppendChild(htEl);
                        }
                    }



                    htEl = browser.Document.CreateElement("script");
                    if (htEl != null)
                    {
                        htEl.SetAttribute("type", "text/javascript");
                        Type t = htEl.DomElement.GetType();
                        t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[]
                        {
                            getWrapperCode()
                        });

                        if (browser.Document.Body != null)
                        {
                            browser.Document.Body.AppendChild(htEl);
                        }
                    }


                    htEl = browser.Document.CreateElement("style");
                    if (htEl != null)
                    {
                        var htmlDocument2 = browser.Document.DomDocument as IHTMLDocument2;
                        if (htmlDocument2 != null)
                        {
                            IHTMLStyleSheet styleSheet = htmlDocument2.createStyleSheet("", 0);
                            styleSheet.cssText = "BODY { margin: 0px; padding: 0px;} #map { width: 600px; height: 400px; border: 0px;}";
                        }
                        browser.Document.GetElementsByTagName("head")[0].AppendChild(htEl);
                    }

                    htEl = browser.Document.CreateElement("div");
                    if (htEl != null)
                    {
                        htEl.Id = "map";
                        if (browser.Document.Body != null)
                        {
                            browser.Document.Body.AppendChild(htEl);
                        }
                    }
                }


                browser.Document.InvokeScript("addGoogle");


                ThreadPool.QueueUserWorkItem(delegate
                {
                    object res = null;
                    if (m_logger.IsDebugEnabled)
                        m_logger.Debug("Starting detection of initcomplete");
                    do
                    {
                        try
                        {
                            browser.Invoke(new MethodInvoker(delegate
                            {
                                res = browser.Document.InvokeScript("isLoaded");
                            }));
                            if (!(res is bool && (bool) res))
                                Thread.Sleep(100);
                        }
                        catch
                        {
                            Thread.Sleep(100);
                        }
                    } while (m_appContext != null && !(res is bool && (bool) res));

                    if (m_appContext != null)
                    {
                        m_haveInited = true;
                        UpdateURLTemplates();

                        if (m_logger.IsDebugEnabled)
                            m_logger.Debug("init is complete");
                    }
                    else
                    {
                        if (m_logger.IsDebugEnabled)
                            m_logger.Debug("AppContext Destroyed before init");
                    }

                });
            }
        }
#endif
        private void UpdateURLTemplates()
        {
            if (MapUrlTemplates == null || MapUrlTemplates.Length == 0)
            {
                for (int i = 0; i < 50; i++)
                {
                    if (m_appContext == null)
                    {
                        return;
                    }

                    if (!ZoomDone())
                    {
                        Thread.Sleep(100);
                    }
                    else
                    {
                        break;
                    }
                }

                for (int i = 0; i < 50; i++)
                {
                    if (m_appContext == null)
                        return;
                    var jstiles = GetCurrentTileUrLs();
                    GetTemplateUrls(jstiles, out MapUrlTemplates, out OverlayUrlTemplates);
                    if (MapUrlTemplates == null || MapUrlTemplates.Length == 0)
                    {
                        Thread.Sleep(100);
                    }
                    else
                    {
                        lock (m_cachedUrLs)
                        {
                            if (!m_cachedUrLs.ContainsKey(MapType.ToString() + "_base"))
                            {
                                m_cachedUrLs.Add(MapType.ToString() + "_base", MapUrlTemplates);
                            }
                            if (OverlayUrlTemplates != null && OverlayUrlTemplates.Length > 0 && !m_cachedUrLs.ContainsKey(MapType.ToString() + "_overlay"))
                            {
                                m_cachedUrLs.Add(MapType.ToString() + "_overlay", OverlayUrlTemplates);
                            }
                        }
                        break;
                        }
                }
            }
        }


        bool m_haveInited;
#if USE_CefSharp
#else
        void m_WebBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            m_webBrowser.DocumentCompleted -= m_WebBrowser_DocumentCompleted;
            if (!m_mapsAdded)
            {                
                AddMaps(sender as WebBrowser);
                m_mapsAdded = true;
            }
        }
#endif



        public Extent GetExtentOfTilesInView(Extent extent, int level)
        {
            SetExtent(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY, level);
            return m_baseSchema.GetExtentOfTilesInView(extent, level);
        }

        public void SetSize(int w, int h)
        {

            if (m_logger.IsDebugEnabled)
                m_logger.Debug("Setting size to: " + w + " , " + h);

            setSize(w, h);
        }

        public IEnumerable<TileInfo> GetTilesInView(Extent extent, double resolution)
        {
            

            int level = Utilities.GetNearestLevel(Resolutions, resolution);
            return GetTilesInView(extent, level);
        }

        readonly Regex m_rex = new Regex(@"!1i(?<z>\d+).*?!2i(?<x>\d+).*?!3i(?<y>\d+)", RegexOptions.IgnoreCase);
        readonly SphericalMercatorInvertedWorldSchema m_baseSchema = new SphericalMercatorInvertedWorldSchema();
        public IEnumerable<TileInfo> GetTilesInView(Extent extent, int level)
        {
            SetExtent(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY, level);

            if (MapUrlTemplates == null || MapUrlTemplates.Length == 0)
                UpdateURLTemplates();

            return m_baseSchema.GetTilesInView(extent, level);
        }

        readonly Regex m_sMatch = new Regex("&s=.*?&");
        readonly Regex m_tokenMatch = new Regex("&token=\\d*?&");
        readonly Regex m_scaleMath = new Regex("&scale=\\d");
        private void GetTemplateUrls(IEnumerable<JsTileInfo> tiles, out string[] mapUrlTemplates, out string[] overlayUrlTemplates)
        {
            var baseUrls = new List<string>();
            var overlayUrls = new List<string>();
            foreach (var ti in tiles)
            {
                Match m = m_rex.Match(ti.Url);
                if (m.Success)
                {
                    string url = ti.Url;
                    url = url.Replace("!1i" + m.Groups["z"].Value, "!1i{2}");
                    url = url.Replace("!2i" + m.Groups["x"].Value, "!2i{0}");
                    url = url.Replace("!3i" + m.Groups["y"].Value, "!3i{1}");
                    //if (url.Contains("&s="))
                    //    url = m_sMatch.Replace(url, "&s={3}&");
                    if (url.Contains("&token="))
                        url = m_tokenMatch.Replace(url, "&token={4}&");
                    if (url.Contains("&scale="))
                        url = m_scaleMath.Replace(url, "");

                    if (MapType == GoogleV3TileSource.MapTypeId.HYBRID && url.StartsWith("http://mt", StringComparison.OrdinalIgnoreCase))
                    {
                        overlayUrls.Add(url);
                    }
                    else
                    {
                        if (!baseUrls.Contains(url))
                            baseUrls.Add(url);
                    }
                }
            }
            mapUrlTemplates = baseUrls.ToArray();
            overlayUrlTemplates = overlayUrls.ToArray();
        }


        /// <summary>
        /// Retreived resolutions from the GoogleMaps JS
        /// </summary>
        /// <returns></returns>
        Resolution[] GetResolutions()
        {
            string result = null;
            lock (m_locker)
            {
#if USE_CefSharp
#else
                if (m_webBrowser != null)
                {
                    try
                    {
                        m_webBrowser.Invoke(new MethodInvoker(delegate
                        {
                            if (m_webBrowser.Document != null)
                            {
                                result = m_webBrowser.Document.InvokeScript("getResolutions") as string;
                            }
                        }));
                    }
                    catch (Exception ee)
                    {
                        m_logger.Warn(ee.Message, ee);
                        try
                        {
                            m_webBrowser.Invoke(new MethodInvoker(delegate
                            {
                                if (m_webBrowser.Document != null)
                                {
                                    result = m_webBrowser.Document.InvokeScript("getResolutions") as string;
                                }
                            }));
                        }
                        catch (Exception ee2)
                        {
                            m_logger.Warn("Again: " + ee2.Message, ee2);
                        }
                    }
                }
#endif
            }
            if (result != null)
            {
                string[] parts = result.Split(',');
                int numResolutions;
                numResolutions = MapType == GoogleV3TileSource.MapTypeId.TERRAIN ? 15 : 19;
                var ret = new Resolution[numResolutions];
                for (int i = 0; i < ret.Length; i++)
                {
                    var res = Convert.ToDouble(parts[i], CultureInfo.InvariantCulture);
                    ret[i] = new Resolution { UnitsPerPixel = res, Id = i.ToString(CultureInfo.InvariantCulture) };
                }
                return ret;
            }
            return null;
        }

        class JsTileInfo
        {
            public string Url { get; set; }
            public int Left { get; set; }
            public int Top { get; set; }
            public int Index { get; set; }
            public int ZIndex { get; set; }
        }

        private IEnumerable<JsTileInfo> GetCurrentTileUrLs()
        {
            object ret = null;
            lock (m_locker)
            {
#if USE_CefSharp
#else
                if (m_webBrowser != null)
                {
                    try
                    {
                        m_webBrowser.Invoke(new MethodInvoker(delegate
                        {
                            if (m_webBrowser.Document != null)
                            {
                                ret = m_webBrowser.Document.InvokeScript("getTileURLs");
                            }
                        }));
                    }
                    catch (Exception ee)
                    {
                        m_logger.Warn(ee.Message, ee);
                        try
                        {
                            m_webBrowser.Invoke(new MethodInvoker(delegate
                            {
                                if (m_webBrowser.Document != null)
                                {
                                    ret = m_webBrowser.Document.InvokeScript("getTileURLs");
                                }
                            }));
                        }
                        catch (Exception ee2)
                        {
                            m_logger.Warn("Exception again: " + ee2.Message, ee2);
                        }
                    }
                }
#endif
                    
            }
            if (ret != null)
            {
                Type t = ret.GetType();
                int len = Convert.ToInt32(t.InvokeMember("length", BindingFlags.GetProperty, null, ret, null));
                var ti = new JsTileInfo[len];
                for (int i = 0; i < len; i++)
                {
                    object item = t.InvokeMember("item_" + i, BindingFlags.GetProperty, null, ret, null);
                    var url = t.InvokeMember("url", BindingFlags.GetProperty, null, item, null) as string;
                    var left = (int)t.InvokeMember("left", BindingFlags.GetProperty, null, item, null);
                    var top = (int)t.InvokeMember("top", BindingFlags.GetProperty, null, item, null);
                    var index = (int)t.InvokeMember("index", BindingFlags.GetProperty, null, item, null);
                    var zIndex = (int)t.InvokeMember("zIndex", BindingFlags.GetProperty, null, item, null);
                    ti[i] = new JsTileInfo
                    {
                        Url = url,
                        Left = left,
                        Top = top,
                        Index = index,
                        ZIndex = zIndex
                    };
                }

                return ti;
            }
            return null;
        }

        int m_curWidth;
        int m_curHeight;
        /// <summary>
        /// Sets mapsize..
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        void setSize(int width, int height)
        {
            if (m_curWidth != width || m_curHeight != height)
            {
                if (m_logger.IsDebugEnabled)
                    m_logger.DebugFormat("Into setSize {0} {1}", width, height);

                lock (m_locker)
                {
#if USE_CefSharp
#else

                    try
                    {
                        m_webBrowser.Invoke(new MethodInvoker(delegate
                        {
                            m_webBrowser.Size = new System.Drawing.Size(width, height);
                            if (m_webBrowser.Document != null)
                            {
                                m_webBrowser.Document.InvokeScript("updateSize", new object[] { width, height });
                            }
                        }));
                    }
                    catch (Exception ee)
                    {
                        m_logger.Warn(ee.Message, ee);
                        try
                        {
                            m_webBrowser.Invoke(new MethodInvoker(delegate
                            {
                                m_webBrowser.Size = new System.Drawing.Size(width, height);
                                if (m_webBrowser.Document != null)
                                {
                                    m_webBrowser.Document.InvokeScript("updateSize", new object[] { width, height });
                                }
                            }));
                        }
                        catch (Exception ee2)
                        {
                            m_logger.Warn("Exception again: " + ee2.Message, ee2);
                        }
                    }
#endif
                }
                m_curWidth = width;
                m_curHeight = height;

            }
        }

        /// <summary>
        /// Sets the extent
        /// </summary>
        /// <param name="xmin"></param>
        /// <param name="ymin"></param>
        /// <param name="xmax"></param>
        /// <param name="ymax"></param>
        /// <param name="level"></param>
        void SetExtent(double xmin, double ymin, double xmax, double ymax, int level)
        {
            if (m_logger.IsDebugEnabled)
                m_logger.DebugFormat("setExtent {0},{1},{2},{3},{4}", xmin, ymin, xmax, ymax, level);

            lock (m_locker)
            {
                if (m_logger.IsDebugEnabled)
                    m_logger.Debug("Into lock");
#if USE_CefSharp
#else

                try
                {

                    m_webBrowser.Invoke(new MethodInvoker(delegate
                    {
                        if (m_webBrowser.Document != null)
                        {
                            m_webBrowser.Document.InvokeScript("setExtent", new object[] { xmin, ymin, xmax, ymax, level });
                        }
                    }));
                }
                catch (Exception ee)
                {
                    m_logger.Warn(ee.Message, ee);
                    //Try again, there are some things with the webbrowsercontrol that throws exceptions sometimes..
                    try
                    {
                        m_webBrowser.Invoke(new MethodInvoker(delegate
                        {
                            if (m_webBrowser.Document != null)
                            {
                                m_webBrowser.Document.InvokeScript("setExtent", new object[] { xmin, ymin, xmax, ymax, level });
                            }
                        }));
                    }
                    catch (Exception ee2)
                    {
                        m_logger.Warn("Exception again: " + ee2.Message, ee2);
                    }
                }
#endif
            }
        }

        bool ZoomDone()
        {
            bool done = false;
            //Do Not LOCK here
            #if USE_CefSharp
#else

            try
            {
                if (m_appContext != null)
                {
                    m_webBrowser.Invoke(new MethodInvoker(delegate
                    {
                        if (m_webBrowser.Document != null)
                        {
                            done = (bool)m_webBrowser.Document.InvokeScript("isZoomDone");
                        }
                    }));
                }
            }
            catch (Exception ee)
            {
                m_logger.Warn(ee.Message, ee);
                try
                {
                    m_webBrowser.Invoke(new MethodInvoker(delegate
                    {
                        if (m_webBrowser.Document != null)
                        {
                            done = (bool)m_webBrowser.Document.InvokeScript("isZoomDone");
                        }
                    }));
                }
                catch (Exception ee2)
                {
                    m_logger.Warn("Exception again " + ee2.Message, ee);
                }
            }
#endif
            System.Diagnostics.Debug.WriteLine("ZoomDone: " + done);

            if (!done)
            {
                bool idle;
                bool tilesLoaded;
#if USE_CefSharp
#else

                try
                {
                    m_webBrowser.Invoke(new MethodInvoker(delegate
                    {
                        if (m_webBrowser.Document != null)
                        {
                            idle = (bool)m_webBrowser.Document.InvokeScript("isIdle");
                            tilesLoaded = (bool)m_webBrowser.Document.InvokeScript("isTilesLoaded");
                        }
                    }));
                }
                catch
                {
                    try
                    {
                        m_webBrowser.Invoke(new MethodInvoker(delegate
                        {
                            if (m_webBrowser.Document != null)
                            {
                                idle = (bool)m_webBrowser.Document.InvokeScript("isIdle");
                                tilesLoaded = (bool)m_webBrowser.Document.InvokeScript("isTilesLoaded");
                            }
                        }));
                    }
                    catch
                    { }
                }
#endif
                System.Diagnostics.Debug.WriteLine("Idle: " + idle + ", TilesLoaded: " + tilesLoaded);
            }

            return done;
        }

        bool m_isLoaded;
        bool IsLoaded()
        {
            if (!m_mapsAdded)
                return false;
            if (m_isLoaded)
                return true;

            bool done = false;
            lock (m_locker)
            {
#if USE_CefSharp
#else

                try
                {
                    m_webBrowser.Invoke(new MethodInvoker(delegate
                    {
                        if (m_webBrowser.Document != null)
                        {
                            done = (bool)m_webBrowser.Document.InvokeScript("isLoaded");
                        }
                    }));
                }
                catch
                {
                    try
                    {
                        m_webBrowser.Invoke(new MethodInvoker(delegate
                        {
                            if (m_webBrowser.Document != null)
                            {
                                done = (bool)m_webBrowser.Document.InvokeScript("isLoaded");
                            }
                        }));
                    }
                    catch
                    { }
                }
#endif
            }
            if (done)
                m_isLoaded = true;

            return done;
        }

        string getOpenLayersCode()
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                               .GetManifestResourceStream("BruTile.GoogleMaps.OpenLayers.light.js"))
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }

            return null;
        }

        string getWrapperCode()
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                               .GetManifestResourceStream("BruTile.GoogleMaps.Wrapper.js"))
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            return null;
        }


        public string Name { get; set; }
        public string Srs { get; set; }
        public Extent Extent { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; }
        IList<Resolution> m_resolutions;
        public IList<Resolution> Resolutions
        {
            get
            {
                WaitForLoad();
                return m_resolutions ?? (m_resolutions = GetResolutions());
            }
            private set
            {
                m_resolutions = value;
            }
        }

        private void WaitForLoad()
        {
            for (int i = 0; i < 100; i++)
            {
                if (!m_haveInited && !IsLoaded())
                    Thread.Sleep(100);
                else
                    break;
            }
        }
        public AxisDirection Axis { get; set; }


        public void Dispose()
        {
            if (m_appContext != null)
            {
                m_appContext.ExitThread();
                m_appContext = null;
#if USE_CefSharp
#else

                if (m_webBrowser != null)
                    m_webBrowser = null;
#endif
            }
        }
    }
}
