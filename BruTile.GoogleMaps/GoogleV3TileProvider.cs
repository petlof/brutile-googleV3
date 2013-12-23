using System.Linq;
using Common.Logging;
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
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;

namespace BruTile.GoogleMaps
{
    public class GoogleV3TileProvider : ITileProvider
    {
        private static readonly ILog m_logger = LogManager.GetLogger(typeof(GoogleV3TileProvider));

        public const string UserAgent = @"Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US; rv:1.9.1.7) Gecko/20091221 Firefox/3.5.7";
        private readonly string m_referer;
        private readonly GoogleV3TileSchema m_tileSchema;

        public GoogleV3TileProvider(GoogleV3TileSchema tileSchema, string referer = "http://localhost")
        {
            m_referer = referer;
            m_tileSchema = tileSchema;
        }

        private const string Secword = "Galileo";

        static string CalculateSParam(int x, int y)
        {
            int seclen = (3 * x + y) % 8;
            string sec = Secword.Substring(0, seclen);
            return sec;
        }

        readonly Random m_r = new Random();
        public byte[] GetTile(TileInfo tileInfo)
        {
            if (m_logger.IsDebugEnabled)
            {
                m_logger.DebugFormat("Fetching tile: {0},{1},{2}", tileInfo.Index.Level, tileInfo.Index.Col, tileInfo.Index.Row);
            }
            
            string url = m_tileSchema.MapUrlTemplates[m_r.Next(0, m_tileSchema.MapUrlTemplates.Length - 1)];
            url = string.Format(url, tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level, CalculateSParam(tileInfo.Index.Col, tileInfo.Index.Row));

            if (m_logger.IsDebugEnabled)
            {
                m_logger.DebugFormat("Using URL: {0}", url);
            }
            
            byte[] data = Fetch(url);

            if (m_tileSchema.MapType == GoogleV3TileSource.MapTypeId.HYBRID)
            {
                string overlayurl = m_tileSchema.OverlayUrlTemplates[m_r.Next(0, m_tileSchema.OverlayUrlTemplates.Length - 1)];
                overlayurl = string.Format(overlayurl, tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level, CalculateSParam(tileInfo.Index.Col, tileInfo.Index.Row));
                byte[] overlayData = Fetch(overlayurl);                

                using (var ms = new MemoryStream(data))
                {
                    using (var ms2 = new MemoryStream(overlayData))
                    {
                        using (System.Drawing.Image img = new System.Drawing.Bitmap(ms))
                        {
                            using (System.Drawing.Image overlayimg = new System.Drawing.Bitmap(ms2))
                            {
                                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(img))
                                {
                                    g.DrawImage(overlayimg, 0, 0);
                                }

                                using (var newMs = new MemoryStream())
                                {

                                    ImageCodecInfo jgpEncoder = GetEncoder(ImageFormat.Jpeg);
                                    Encoder myEncoder =
                                        Encoder.Quality;
                                    var myEncoderParameters = new EncoderParameters(1);
                                    var myEncoderParameter = new EncoderParameter(myEncoder,
                                        90L);
                                    myEncoderParameters.Param[0] = myEncoderParameter;
                                    img.Save(newMs, jgpEncoder, myEncoderParameters);


                                    newMs.Seek(0, SeekOrigin.Begin);
                                    data = new byte[newMs.Length];
                                    newMs.Read(data, 0, data.Length);
                                    newMs.Close();
                                }
                            }
                        }
                        ms2.Close();
                    }
                    ms.Close();
                }
            }


            return data;
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid);
        }

        private byte[] Fetch(string url)
        {
            WebRequest wq = WebRequest.Create(url);
            var httpWebRequest = wq as HttpWebRequest;
            if (httpWebRequest != null)
            {
                httpWebRequest.UserAgent = UserAgent;

                httpWebRequest.Referer = m_referer;
                httpWebRequest.KeepAlive = false;
                httpWebRequest.AllowAutoRedirect = true;
                httpWebRequest.Timeout = 5000;
            }

            byte[] ret;
            using (WebResponse resp = wq.GetResponse())
            {
                if (resp.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream responseStream = resp.GetResponseStream())
                    {
                        ret = Utilities.ReadFully(responseStream);
                        if (responseStream != null)
                        {
                            responseStream.Close();
                        }
                    }
                }
                else
                {
                    string message = ComposeErrorMessage(resp, url);

                    if (m_logger.IsDebugEnabled)
                    {
                        m_logger.DebugFormat("Error fetching tile: {0}", message);
                    }

                    throw (new Web.WebResponseFormatException(message, null));
                }
                resp.Close();
            }
            return ret;
        }

        private static string ComposeErrorMessage(WebResponse webResponse, string uri)
        {
            string message = String.Format(
                CultureInfo.InvariantCulture,
                "Failed to retrieve tile from this uri:\n{0}\n.An image was expected but the received type was '{1}'.",
                uri,
                webResponse.ContentType
            );

            if (webResponse.ContentType.StartsWith("text", StringComparison.OrdinalIgnoreCase))
            {
                using (Stream stream = webResponse.GetResponseStream())
                {
                    message += String.Format(CultureInfo.InvariantCulture,
                      "\nThis was returned:\n{0}", ReadAllText(stream));
                }
            }
            return message;
        }

        private static string ReadAllText(Stream responseStream)
        {
            using (var streamReader = new StreamReader(responseStream, true))
            {
                using (var stringWriter = new StringWriter(CultureInfo.InvariantCulture))
                {
                    stringWriter.Write(streamReader.ReadToEnd());
                    return stringWriter.ToString();
                }
            }
        }
    }
}
