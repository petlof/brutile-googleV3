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

namespace BruTile.GoogleMaps
{
    public class GoogleV3TileSchema : ITileSchema, IDisposable
    {
        System.Windows.Forms.WebBrowser m_WebBrowser;
        private string gmeClientID;
        private string googleChannel;
        private string referer;
        public GoogleV3TileSchema(string gmeClientID, string googleChannel, string referer)
        {
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

            m_WebBrowser = new System.Windows.Forms.WebBrowser();
            m_WebBrowser.Navigating += new WebBrowserNavigatingEventHandler(m_WebBrowser_Navigating);
            m_WebBrowser.Visible = false;
            m_WebBrowser.ScrollBarsEnabled = false;
            m_WebBrowser.Size = new System.Drawing.Size(600, 400);            
            m_WebBrowser.DocumentCompleted += new System.Windows.Forms.WebBrowserDocumentCompletedEventHandler(m_WebBrowser_DocumentCompleted);

            if (!string.IsNullOrEmpty(referer))
            {
                m_WebBrowser.Navigate(referer);
            }
            else
            {
                m_WebBrowser.DocumentText = "<!DOCTYPE html><html><body></body></html>";
            }
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
                string resp = "<!DOCTYPE html><html><body></body></html>";
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
                t.InvokeMember("text", BindingFlags.SetProperty, null, htEl.DomElement, new object[] { getOpenLayersCode() });
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
                do
                {
                    m_WebBrowser.Invoke(new MethodInvoker(delegate
                        {
                            res = m_WebBrowser.Document.InvokeScript("isLoaded");
                        }));
                    if (!(res is bool && (bool)res == true))
                        Thread.Sleep(100);
                }
                while (!(res is bool && (bool)res == true));

                m_Resolutions = getResolutions();
                haveInited = true;
            }));
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



        public new Extent GetExtentOfTilesInView(Extent extent, int level)
        {
            waitForLoad();

            /*Get mapWidth in pixels*/
            setExtent(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY, level);

            int minPxX = 0;
            int maxPxX = m_WebBrowser.Width;
            int minPxY = 0;
            int maxPxY = m_WebBrowser.Height;

            var tis = getCurrentTileURLs();
            foreach (var ti in tis)
            {
                if (ti.Left < minPxX)
                    minPxX = ti.Left;
                if (ti.Left + Width > maxPxX)
                    maxPxX = ti.Left + Width;
                if (ti.Top < minPxY)
                    minPxY = ti.Top;
                if (ti.Top + Height > maxPxY)
                    maxPxY = ti.Top + Height;
            }

            var ext = new Extent(extent.MinX - minPxX * Resolutions[level].UnitsPerPixel,
                extent.MinY - (maxPxY - m_WebBrowser.Height) * Resolutions[level].UnitsPerPixel,
                extent.MaxX + (maxPxX - m_WebBrowser.Width) * Resolutions[level].UnitsPerPixel,
                extent.MaxY - (minPxY) * Resolutions[level].UnitsPerPixel);
            return ext;
        }

        public void SetSize(int w, int h)
        {
            setSize(w, h);
        }

        Regex rex = new Regex(@"x=(?<x>\d+).*?&y=(?<y>\d+).*?&z=(?<z>\d+)", RegexOptions.IgnoreCase);
        public IEnumerable<BruTile.TileInfo> GetTilesInView(Extent extent, int level)
        {
            waitForLoad();
            extent = setExtent(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY, level);

            List<BruTile.TileInfo> tis = new List<BruTile.TileInfo>();
            foreach (var ti in getCurrentTileURLs())
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

                    tis.Add(new BruTile.GoogleMaps.GoogleV3TileInfo()
                        {
                            Url = ti.Url,
                            Index = new TileIndex(x, y, z),
                            Extent = e
                        });
                }
            }
            return tis;
        }


        /// <summary>
        /// Retreived resolutions from the GoogleMaps JS
        /// </summary>
        /// <returns></returns>
        Resolution[] getResolutions()
        {
            string result = null;
            m_WebBrowser.Invoke(new MethodInvoker(delegate
            {
                result = m_WebBrowser.Document.InvokeScript("getResolutions") as string;
            }));
            string[] parts = result.Split(',');
            Resolution[] ret = new Resolution[parts.Length];
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
        }

        private jsTileInfo[] getCurrentTileURLs()
        {
            object ret = null;

            m_WebBrowser.Invoke(new MethodInvoker(delegate
            {
                ret = m_WebBrowser.Document.InvokeScript("getTileURLs");
            }));
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
                ti[i] = new jsTileInfo()
                {
                    Url = url,
                    Left = left,
                    Top = top,
                    Index = index
                };
            }

            return ti;
        }

        /// <summary>
        /// Sets mapsize..
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        void setSize(int width, int height)
        {
            string size;
            
            m_WebBrowser.Invoke(new MethodInvoker(delegate
                {
                    m_WebBrowser.Size = new System.Drawing.Size(width+Width*2, height+Height*2);
                    size = m_WebBrowser.Document.InvokeScript("updateSize", new object[] { width+Width*2, height+Height*2 }) as string;
                }));

            //Wait for zooming to end
            //for (int i = 0; i < 100; i++)
            //{
            //    if (!zoomDone())
            //    {
            //        Thread.Sleep(100);
            //    }
            //    else
            //    {
            //        break;
            //    }
            //}

            string ext = "";
            m_WebBrowser.Invoke(new MethodInvoker(delegate
            {
                ext = m_WebBrowser.Document.InvokeScript("getExtent") as string;
            }));

            string[] parts = ext.Split(',');
        }

        /// <summary>
        /// Sets the extent
        /// </summary>
        /// <param name="xmin"></param>
        /// <param name="ymin"></param>
        /// <param name="xmax"></param>
        /// <param name="ymax"></param>
        Extent setExtent(double xmin, double ymin, double xmax, double ymax, int level)
        {
            string ext = null;
            m_WebBrowser.Invoke(new MethodInvoker(delegate
            {
                m_WebBrowser.Document.InvokeScript("setExtent", new object[] { xmin, ymin, xmax, ymax, level});
            }));

            //Wait for zooming to end
            for (int i = 0; i < 10; i++)
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

            m_WebBrowser.Invoke(new MethodInvoker(delegate
            {
                ext = m_WebBrowser.Document.InvokeScript("getExtent") as string;
            }));

            string[] parts = ext.Split(',');
            return new Extent(Convert.ToDouble(parts[0], CultureInfo.InvariantCulture),
                Convert.ToDouble(parts[1], CultureInfo.InvariantCulture),
                Convert.ToDouble(parts[2], CultureInfo.InvariantCulture),
                Convert.ToDouble(parts[3], CultureInfo.InvariantCulture));
        }
        bool zoomDone()
        {
            bool done = false;
            m_WebBrowser.Invoke(new MethodInvoker(delegate
            {
                done = (bool)m_WebBrowser.Document.InvokeScript("isZoomDone");
            }));

            return done;
        }

        bool isLoaded()
        {
            bool done = false;
            m_WebBrowser.Invoke(new MethodInvoker(delegate
            {
                done = (bool)m_WebBrowser.Document.InvokeScript("isLoaded");
            }));

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
            if (m_WebBrowser != null)
                m_WebBrowser.Dispose();
        }
    }
}
