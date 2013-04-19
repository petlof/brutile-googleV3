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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BruTile.PreDefined;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Net;
using Common.Logging;

namespace BruTile.GoogleMaps
{
    public class GoogleV3TileSchema : ITileSchema, IDisposable
    {
        System.Windows.Forms.WebBrowser m_WebBrowser;
        static object locker = new object();
        private string gmeClientID;
        private string googleChannel;
        private string referer;
        internal GoogleV3TileSource.MapTypeId _mapType;
        Thread wbThread = null;
        ApplicationContext appContext = null;
        internal string[] mapUrlTemplates = null;
        internal string[] overlayUrlTemplates = null;

        static Dictionary<string, string[]> _cachedURLs = new Dictionary<string, string[]>();

        static readonly ILog logger = LogManager.GetLogger(typeof(GoogleV3TileSchema));

        public GoogleV3TileSchema(string gmeClientID, string googleChannel, string referer, GoogleV3TileSource.MapTypeId mapType)
        {
            _mapType = mapType;
            Height = 256;
            Width = 256;
            Extent = new Extent(-20037508.342789, -20037508.342789, 20037508.342789, 20037508.342789);
            OriginX = -20037508.342789;
            OriginY = 20037508.342789;
            Name = "GoogleSchema";
            Format = "png";
            Axis = AxisDirection.InvertedY;
            Srs = "EPSG:3857";

            this.gmeClientID = gmeClientID;
            this.googleChannel = googleChannel;
            this.referer = referer;
            if (_cachedURLs.ContainsKey(mapType.ToString() + "_base"))
            {
                mapUrlTemplates = _cachedURLs[mapType.ToString() + "_base"];
            }
            if (_cachedURLs.ContainsKey(mapType.ToString() + "_overlay"))
            {
                overlayUrlTemplates = _cachedURLs[mapType.ToString() + "_overlay"];
            }
            appContext = new ApplicationContext();
            
            wbThread = new Thread(() =>
            {
                //Form f = new Form();
                //f.Size = new System.Drawing.Size(600, 400);
                try
                {
                    m_WebBrowser = new System.Windows.Forms.WebBrowser();
                    m_WebBrowser.Navigating += new WebBrowserNavigatingEventHandler(m_WebBrowser_Navigating);
                    m_WebBrowser.Visible = false;
                    m_WebBrowser.ScrollBarsEnabled = false;
                    m_WebBrowser.Size = new System.Drawing.Size(600, 400);
                    m_WebBrowser.ScriptErrorsSuppressed = true;
                    m_WebBrowser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(m_WebBrowser_DocumentCompleted);

                    if (!string.IsNullOrEmpty(referer))
                    {
                        m_WebBrowser.Navigate(referer);
                    }
                    else
                    {
                        m_WebBrowser.DocumentText = "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\"><html xmlns=\"http://www.w3.org/1999/xhtml\"><body></body></html>";
                    }

                    if (appContext != null)
                    {
                        Application.Run(appContext);
                    }
                }
                catch (Exception ee)
                {
                    logger.Error("Exception in WebBrowserThread, quitting", ee);
                }
            });
            wbThread.Name = "WebBrowser Thread";
            wbThread.SetApartmentState(ApartmentState.STA);
            wbThread.Start();
            if (logger.IsDebugEnabled)
                logger.Debug("WebBrowserThread Started");
        }

        

        void m_WebBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (!string.IsNullOrEmpty(referer) && e.Url.Host == new Uri(referer).Host)
            {
                MemoryStream ms = new MemoryStream();
                var sw = new StreamWriter(ms);
                sw.WriteLine("HTTP/1.1 200 OK");
                sw.WriteLine("Server: Brutile");
                sw.WriteLine("Content-Type: text/html");
                sw.WriteLine("Connection: close");
                string resp = "<!DOCTYPE html PUBLIC \"-//W3C//DTD HTML 4.01//EN\" \"http://www.w3.org/TR/html4/strict.dtd\"><html xmlns=\"http://www.w3.org/1999/xhtml\"><body></body></html>";
                sw.WriteLine("Content-Length: " + resp.Length);
                sw.WriteLine();
                sw.Write(resp);
                sw.Flush();
                ms.Seek(0,SeekOrigin.Begin);
                m_WebBrowser.DocumentStream = ms;
            }
        }

        bool mapsAdded = false;

        void addMaps()
        {
            string googleURL = "http://maps.googleapis.com/maps/api/js?libraries=&sensor=false&callback=init";
            if (!string.IsNullOrEmpty(gmeClientID))
                googleURL += "&client=" + gmeClientID;
            if (!string.IsNullOrEmpty(googleChannel))
                googleURL += "&channel=" + googleChannel;

            string page = "<html><head><style type=\"text/css\">BODY { margin: 0px; padding: 0px;}</style></head><body><div id=\"map\" style=\"width:600px; height: 400px; border: 0px\"></div><script type=\"text/javascript\" src=\"" + googleURL + "\"></script>";
            page += "<script type=\"text/javascript\">" + getOpenLayersCode() + "</script>";
            page += "<script type=\"text/javascript\">" + getWrapperCode() + "</script>";
            page += "</body></html>";

            //Clear body
            HtmlElement htEl = m_WebBrowser.Document.CreateElement("script");
            htEl.SetAttribute("type", "text/javascript");
            Type t = htEl.DomElement.GetType();
            t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[] { "function modContent() {while (document.getElementsByTagName(\"META\").length > 0) {document.getElementsByTagName(\"head\")[0].removeChild(document.getElementsByTagName(\"META\")[0]);}  if (document.body) {document.body.innerHTML = \"\";}} " });
            m_WebBrowser.Document.Body.AppendChild(htEl);
            m_WebBrowser.Document.InvokeScript("modContent");


            htEl = m_WebBrowser.Document.CreateElement("script");
            htEl.SetAttribute("type", "text/javascript");
            t = htEl.DomElement.GetType();
            t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[] { "function addGoogle() { var sc = document.createElement(\"SCRIPT\"); sc.type=\"text/javascript\"; sc.src=\"" + googleURL + "\";document.body.appendChild(sc);}" });
            m_WebBrowser.Document.Body.AppendChild(htEl);

            m_WebBrowser.Document.InvokeScript("addGoogle");

            htEl = m_WebBrowser.Document.CreateElement("script");
            htEl.SetAttribute("type", "text/javascript");
            t = htEl.DomElement.GetType();
            t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[] { "baseLayer = \"google.maps.MapTypeId." + _mapType.ToString() + "\";" + getOpenLayersCode() });
            m_WebBrowser.Document.Body.AppendChild(htEl);

            htEl = m_WebBrowser.Document.CreateElement("script");
            htEl.SetAttribute("type", "text/javascript");
            t = htEl.DomElement.GetType();
            t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[] { getWrapperCode() });
            m_WebBrowser.Document.Body.AppendChild(htEl);

            htEl = m_WebBrowser.Document.CreateElement("style");
            htEl.SetAttribute("type", "text/css");
            t = htEl.DomElement.GetType();
            var mi = t.GetMethods().Where(x => x.Name.Contains("style")).FirstOrDefault();
            object it = t.InvokeMember("styleSheet", BindingFlags.GetProperty, null, htEl.DomElement, null);
            t = it.GetType();
            t.InvokeMember("cssText", BindingFlags.SetProperty, null, it, new object[] { "BODY { margin: 0px; padding: 0px;} #map { width: 600px; height: 400px; border: 0px;}" });
            m_WebBrowser.Document.GetElementsByTagName("head")[0].AppendChild(htEl);

            htEl = m_WebBrowser.Document.CreateElement("div");
            htEl.Id = "map";
            m_WebBrowser.Document.Body.AppendChild(htEl);

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate
            {
                object res = null;
                if (logger.IsDebugEnabled)
                    logger.Debug("Starting detection of initcomplete");
                do
                {
                    try
                    {
                        m_WebBrowser.Invoke(new MethodInvoker(delegate
                            {
                                res = m_WebBrowser.Document.InvokeScript("isLoaded");
                            }));
                        if (!(res is bool && (bool)res == true))
                            Thread.Sleep(100);
                    }
                    catch
                    {
                        Thread.Sleep(100);
                    }
                }
                while (!(res is bool && (bool)res == true));
                haveInited = true;

                updateURLTemplates();

                if (logger.IsDebugEnabled)
                    logger.Debug("init is complete");
            }));
        }

        private void updateURLTemplates()
        {
            if (mapUrlTemplates == null || mapUrlTemplates.Length == 0)
            {
                for (int i = 0; i < 50; i++)
                {
                    if (!zoomDone())
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
                    var jstiles = getCurrentTileURLs();
                    getTemplateUrls(jstiles, out mapUrlTemplates, out overlayUrlTemplates);
                    if (mapUrlTemplates == null || mapUrlTemplates.Length == 0)
                    {
                        Thread.Sleep(100);
                    }
                    else
                    {
                        lock (_cachedURLs)
                        {
                            if (!_cachedURLs.ContainsKey(_mapType.ToString() + "_base"))
                            {
                                _cachedURLs.Add(_mapType.ToString() + "_base", mapUrlTemplates);
                            }
                            if (overlayUrlTemplates != null && overlayUrlTemplates.Length > 0 && !_cachedURLs.ContainsKey(_mapType.ToString() + "_overlay"))
                            {
                                _cachedURLs.Add(_mapType.ToString() + "_overlay", overlayUrlTemplates);
                            }
                        }
                        break;
                        }
                }
            }
        }


        bool haveInited = false;
        void m_WebBrowser_DocumentCompleted(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e)
        {
            m_WebBrowser.DocumentCompleted -= new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(m_WebBrowser_DocumentCompleted);
            if (!mapsAdded)
            {                
                addMaps();
                mapsAdded = true;
            }
        }



        public Extent GetExtentOfTilesInView(Extent extent, int level)
        {
            setExtent(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY, level);
            return m_baseSchema.GetExtentOfTilesInView(extent, level);
        }

        public void SetSize(int w, int h)
        {

            if (logger.IsDebugEnabled)
                logger.Debug("Setting size to: " + w + " , " + h);

            setSize(w, h);
        }

        public IEnumerable<BruTile.TileInfo> GetTilesInView(Extent extent, double resolution)
        {
            

            int level = Utilities.GetNearestLevel(Resolutions, resolution);
            return GetTilesInView(extent, level);
        }

        Regex rex = new Regex(@"x=(?<x>\d+).*?&y=(?<y>\d+).*?&z=(?<z>\d+)", RegexOptions.IgnoreCase);
        BruTile.PreDefined.SphericalMercatorInvertedWorldSchema m_baseSchema = new SphericalMercatorInvertedWorldSchema();
        public IEnumerable<BruTile.TileInfo> GetTilesInView(Extent extent, int level)
        {
            setExtent(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY, level);

            if (mapUrlTemplates == null || mapUrlTemplates.Length == 0)
                updateURLTemplates();

            return m_baseSchema.GetTilesInView(extent, level);
        }

        Regex sMatch = new Regex("&s=.*?&");
        Regex tokenMatch = new Regex("&token=\\d*?&");
        private void getTemplateUrls(jsTileInfo[] tiles, out string[] mapUrlTemplates, out string[] overlayUrlTemplates)
        {
            List<string> baseUrls = new List<string>();
            List<string> overlayUrls = new List<string>();
            foreach (var ti in tiles)
            {
                Match m = rex.Match(ti.Url);
                if (m.Success)
                {
                    string url = ti.Url;
                    url = url.Replace("x=" + m.Groups["x"].Value, "x={0}");
                    url = url.Replace("y=" + m.Groups["y"].Value, "y={1}");
                    url = url.Replace("z=" + m.Groups["z"].Value, "z={2}");
                    if (url.Contains("&s="))
                        url = sMatch.Replace(url, "&s={3}&");
                    if (url.Contains("&token="))
                        url = tokenMatch.Replace(url, "&token={4}&");

                    if (_mapType == GoogleV3TileSource.MapTypeId.HYBRID && url.StartsWith("http://mt"))
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

        private Extent parseTiles(Extent extent, int level, List<BruTile.TileInfo> tis, jsTileInfo[] tiles)
        {
            foreach (var ti in tiles)
            {
                Match m = rex.Match(ti.Url);
                if (m.Success)
                {
                    int x = Convert.ToInt32(m.Groups["x"].Value);
                    int y = Convert.ToInt32(m.Groups["y"].Value);
                    int z = Convert.ToInt32(m.Groups["z"].Value);

                    Extent e = new BruTile.Extent(
                        extent.MinX + ti.Left * Resolutions[level].UnitsPerPixel,
                        extent.MaxY - (ti.Top + 256) * Resolutions[level].UnitsPerPixel,
                        extent.MinX + (ti.Left + Width) * Resolutions[level].UnitsPerPixel,
                        extent.MaxY - (ti.Top) * Resolutions[level].UnitsPerPixel
                        );

                    if (e.Intersects(extent))
                    {

                        tis.Add(new BruTile.GoogleMaps.GoogleV3TileInfo()
                        {
                            Url = ti.Url,
                            Index = new TileIndex(x, y, ti.Url.GetHashCode()),
                            Extent = e,
                            ZIndex = ti.zIndex
                        });
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Could not match " + ti.Url);
                }
            }
            System.Diagnostics.Debug.WriteLine("got " + tis.Count + " tiles");
            return extent;
        }


        /// <summary>
        /// Retreived resolutions from the GoogleMaps JS
        /// </summary>
        /// <returns></returns>
        Resolution[] getResolutions()
        {
            string result = null;
            lock (locker)
            {
                try
                {
                    m_WebBrowser.Invoke(new MethodInvoker(delegate
                    {
                        result = m_WebBrowser.Document.InvokeScript("getResolutions") as string;
                    }));
                }
                catch (Exception ee)
                {
                    logger.Warn(ee.Message, ee);
                    try
                    {
                        m_WebBrowser.Invoke(new MethodInvoker(delegate
                        {
                            result = m_WebBrowser.Document.InvokeScript("getResolutions") as string;
                        }));
                    }
                    catch (Exception ee2)
                    {
                        logger.Warn("Again: " + ee2.Message, ee2);
                    }
                }
            }
            string[] parts = result.Split(',');
            int numResolutions = parts.Length;
            if (_mapType == GoogleV3TileSource.MapTypeId.TERRAIN)
                numResolutions = 15;
            else
                numResolutions = 19;
            Resolution[] ret = new Resolution[numResolutions];
            for (int i = 0; i < ret.Length; i++)
            {
                var res = Convert.ToDouble(parts[i], CultureInfo.InvariantCulture);
                ret[i] = new Resolution() { UnitsPerPixel = res, Id = i.ToString() };
            }
            return ret;
        }

        class jsTileInfo
        {
            public string Url { get; set; }
            public int Left { get; set; }
            public int Top { get; set; }
            public int Index { get; set; }
            public int zIndex { get; set; }
        }

        private jsTileInfo[] getCurrentTileURLs()
        {
            object ret = null;
            lock (locker)
            {
                try
                {
                    m_WebBrowser.Invoke(new MethodInvoker(delegate
                    {
                        ret = m_WebBrowser.Document.InvokeScript("getTileURLs");
                    }));
                }
                catch (Exception ee)
                {
                    logger.Warn(ee.Message, ee);
                    try
                    {
                        m_WebBrowser.Invoke(new MethodInvoker(delegate
                        {
                            ret = m_WebBrowser.Document.InvokeScript("getTileURLs");
                        }));
                    }
                    catch (Exception ee2)
                    {
                        logger.Warn("Exception again: " + ee2.Message, ee2);
                    }
                }
                    
            }
            Type t = ret.GetType();
            int len = Convert.ToInt32(t.InvokeMember("length", BindingFlags.GetProperty, null, ret, null));
            jsTileInfo[] ti = new jsTileInfo[len];
            for (int i = 0; i < len; i++)
            {
                object item = t.InvokeMember("item_" + i, BindingFlags.GetProperty, null, ret, null);
                string url = t.InvokeMember("url", BindingFlags.GetProperty, null, item, null) as string;
                int left = (int)t.InvokeMember("left", BindingFlags.GetProperty, null, item, null);
                int top = (int)t.InvokeMember("top", BindingFlags.GetProperty, null, item, null);
                int index = (int)t.InvokeMember("index", BindingFlags.GetProperty, null, item, null);
                int zIndex = (int)t.InvokeMember("zIndex", BindingFlags.GetProperty, null, item, null);
                ti[i] = new jsTileInfo()
                {
                    Url = url,
                    Left = left,
                    Top = top,
                    Index = index,
                    zIndex = zIndex
                };
            }

            return ti;
        }

        int curWidth = 0;
        int curHeight = 0;
        /// <summary>
        /// Sets mapsize..
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        void setSize(int width, int height)
        {
            if (curWidth != width || curHeight != height)
            {
                if (logger.IsDebugEnabled)
                    logger.DebugFormat("Into setSize {0} {1}", width, height);

                string size;
                lock (locker)
                {
                    try
                    {
                        m_WebBrowser.Invoke(new MethodInvoker(delegate
                            {
                                m_WebBrowser.Size = new System.Drawing.Size(width, height);
                                size = m_WebBrowser.Document.InvokeScript("updateSize", new object[] { width, height }) as string;
                            }));
                    }
                    catch (Exception ee)
                    {
                        logger.Warn(ee.Message, ee);
                        try
                        {
                            m_WebBrowser.Invoke(new MethodInvoker(delegate
                            {
                                m_WebBrowser.Size = new System.Drawing.Size(width, height);
                                size = m_WebBrowser.Document.InvokeScript("updateSize", new object[] { width, height }) as string;
                            }));
                        }
                        catch (Exception ee2)
                        {
                            logger.Warn("Exception again: " + ee2.Message, ee2);
                        }
                    }
                }
                curWidth = width;
                curHeight = height;

            }
        }

        /// <summary>
        /// Sets the extent
        /// </summary>
        /// <param name="xmin"></param>
        /// <param name="ymin"></param>
        /// <param name="xmax"></param>
        /// <param name="ymax"></param>
        void setExtent(double xmin, double ymin, double xmax, double ymax, int level)
        {
            if (logger.IsDebugEnabled)
                logger.DebugFormat("setExtent {0},{1},{2},{3},{4}", xmin, ymin, xmax, ymax, level);

            lock (locker)
            {
                if (logger.IsDebugEnabled)
                    logger.Debug("Into lock");
                try
                {
                    m_WebBrowser.Invoke(new MethodInvoker(delegate
                    {
                        m_WebBrowser.Document.InvokeScript("setExtent", new object[] { xmin, ymin, xmax, ymax, level });
                    }));
                }
                catch (Exception ee)
                {
                    logger.Warn(ee.Message, ee);
                    //Try again, there are some things with the webbrowsercontrol that throws exceptions sometimes..
                    try
                    {
                        m_WebBrowser.Invoke(new MethodInvoker(delegate
                        {
                            m_WebBrowser.Document.InvokeScript("setExtent", new object[] { xmin, ymin, xmax, ymax, level });
                        }));
                    }
                    catch (Exception ee2)
                    {
                        logger.Warn("Exception again: " + ee2.Message, ee2);
                    }
                }
            }

                /*Application.DoEvents();
                //Wait for zooming to end
                for (int i = 0; i < 50; i++)
                {
                    if (!zoomDone())
                    {
                        Application.DoEvents();
                        Thread.Sleep(100);
                    }
                    else
                    {
                        break;
                    }
                }

                try
                {
                    m_WebBrowser.Invoke(new MethodInvoker(delegate
                    {
                        ext = m_WebBrowser.Document.InvokeScript("getExtent") as string;
                    }));
                }
                catch (Exception ee)
                {
                    logger.Warn(ee.Message, ee);
                    try
                    {
                        m_WebBrowser.Invoke(new MethodInvoker(delegate
                        {
                            ext = m_WebBrowser.Document.InvokeScript("getExtent") as string;
                        }));
                    }
                    catch (Exception ee2)
                    {
                        logger.Warn("Exception again " + ee2.Message, ee2); 
                    }
                }
            }

            if (logger.IsDebugEnabled)
                logger.Debug("outof lock");

            string[] parts = ext.Split(',');
            return new Extent(Convert.ToDouble(parts[0], CultureInfo.InvariantCulture),
                Convert.ToDouble(parts[1], CultureInfo.InvariantCulture),
                Convert.ToDouble(parts[2], CultureInfo.InvariantCulture),
                Convert.ToDouble(parts[3], CultureInfo.InvariantCulture));*/
        }

        bool zoomDone()
        {
            bool done = false;
            //Do Not LOCK here
            try
            {
                m_WebBrowser.Invoke(new MethodInvoker(delegate
                {
                    done = (bool)m_WebBrowser.Document.InvokeScript("isZoomDone");
                }));
            }
            catch (Exception ee)
            {
                logger.Warn(ee.Message, ee);
                try
                {
                    m_WebBrowser.Invoke(new MethodInvoker(delegate
                    {
                        done = (bool)m_WebBrowser.Document.InvokeScript("isZoomDone");
                    }));
                }
                catch (Exception ee2)
                {
                    logger.Warn("Exception again " + ee2.Message, ee);
                }
            }

            System.Diagnostics.Debug.WriteLine("ZoomDone: " + done);

            if (!done)
            {
                bool idle = false;
                bool tilesLoaded = false;
                try
                {
                    m_WebBrowser.Invoke(new MethodInvoker(delegate
                    {
                        idle = (bool)m_WebBrowser.Document.InvokeScript("isIdle");
                        tilesLoaded = (bool)m_WebBrowser.Document.InvokeScript("isTilesLoaded");
                    }));
                }
                catch
                {
                    try
                    {
                        m_WebBrowser.Invoke(new MethodInvoker(delegate
                        {
                            idle = (bool)m_WebBrowser.Document.InvokeScript("isIdle");
                            tilesLoaded = (bool)m_WebBrowser.Document.InvokeScript("isTilesLoaded");
                        }));
                    }
                    catch
                    { }
                }
                System.Diagnostics.Debug.WriteLine("Idle: " + idle + ", TilesLoaded: " + tilesLoaded);
            }

            return done;
        }

        bool m_IsLoaded = false;
        bool isLoaded()
        {
            if (!mapsAdded)
                return false;
            if (m_IsLoaded)
                return true;

            bool done = false;
            lock (locker)
            {
                try
                {
                    m_WebBrowser.Invoke(new MethodInvoker(delegate
                    {
                        done = (bool)m_WebBrowser.Document.InvokeScript("isLoaded");
                    }));
                }
                catch
                {
                    try
                    {
                        m_WebBrowser.Invoke(new MethodInvoker(delegate
                        {
                            done = (bool)m_WebBrowser.Document.InvokeScript("isLoaded");
                        }));
                    }
                    catch
                    { }
                }
            }
            if (done)
                m_IsLoaded = true;

            return done;
        }

        string getOpenLayersCode()
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                               .GetManifestResourceStream("BruTile.GoogleMaps.OpenLayers.light.js"))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        string getWrapperCode()
        {
            using (Stream stream = Assembly.GetExecutingAssembly()
                               .GetManifestResourceStream("BruTile.GoogleMaps.Wrapper.js"))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }


        public string Name { get; set; }
        public string Srs { get; set; }
        public Extent Extent { get; set; }
        public double OriginX { get; set; }
        public double OriginY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Format { get; set; }
        IList<Resolution> m_Resolutions;
        public IList<Resolution> Resolutions
        {
            get
            {
                waitForLoad();
                if (m_Resolutions == null)
                {
                    m_Resolutions = getResolutions();
                }
                return m_Resolutions;

            }
            private set
            {
                m_Resolutions = value;
            }
        }

        private void waitForLoad()
        {
            for (int i = 0; i < 100; i++)
            {
                if (!haveInited && !isLoaded())
                    Thread.Sleep(100);
                else
                    break;
            }
        }
        public AxisDirection Axis { get; set; }


        public void Dispose()
        {
            if (appContext != null)
            {
                appContext.ExitThread();
                appContext = null;
            }

            
            GC.SuppressFinalize(this);
        }
    }
}
