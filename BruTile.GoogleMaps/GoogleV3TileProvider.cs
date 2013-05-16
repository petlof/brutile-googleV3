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
using System.Net;
using System.IO;
using System.Drawing.Imaging;
using System.Globalization;

namespace BruTile.GoogleMaps
{
    public class GoogleV3TileProvider : BruTile.ITileProvider
    {
        public const string UserAgent = @"Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US; rv:1.9.1.7) Gecko/20091221 Firefox/3.5.7";
        private string Referer;
        private GoogleV3TileSchema _tileSchema;

        public GoogleV3TileProvider(GoogleV3TileSchema tileSchema, string referer = "http://localhost")
        {
            this.Referer = referer;
            _tileSchema = tileSchema;
        }

        string secword = "Galileo";
        string calculateSParam(int x, int y)
        {
            int seclen = (3 * x + y) % 8;
            string sec = secword.Substring(0, seclen);
            return sec;
        }

        Random r = new Random();
        public byte[] GetTile(BruTile.TileInfo tileInfo)
        {
            string url = _tileSchema.mapUrlTemplates[r.Next(0, _tileSchema.mapUrlTemplates.Length - 1)];
            url = string.Format(url, tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level, calculateSParam(tileInfo.Index.Col, tileInfo.Index.Row));

            
            byte[] data = Fetch(url);

            if (_tileSchema._mapType == GoogleV3TileSource.MapTypeId.HYBRID)
            {
                string overlayurl = _tileSchema.overlayUrlTemplates[r.Next(0, _tileSchema.overlayUrlTemplates.Length - 1)];
                overlayurl = string.Format(overlayurl, tileInfo.Index.Col, tileInfo.Index.Row, tileInfo.Index.Level, calculateSParam(tileInfo.Index.Col, tileInfo.Index.Row));
                byte[] overlayData = Fetch(overlayurl);                

                using (MemoryStream ms = new MemoryStream(data))
                {
                    using (MemoryStream ms2 = new MemoryStream(overlayData))
                    {
                        System.Drawing.Image img = new System.Drawing.Bitmap(ms);
                        System.Drawing.Image overlayimg = new System.Drawing.Bitmap(ms2);
                        using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(img))
                        {
                            g.DrawImage(overlayimg, 0, 0);
                        }

                        using (MemoryStream newMs = new MemoryStream())
                        {

                            ImageCodecInfo jgpEncoder = GetEncoder(ImageFormat.Jpeg);
                            System.Drawing.Imaging.Encoder myEncoder =
                                System.Drawing.Imaging.Encoder.Quality;
                            EncoderParameters myEncoderParameters = new EncoderParameters(1);
                            EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder,
                                90L);
                            myEncoderParameters.Param[0] = myEncoderParameter;
                            img.Save(newMs, jgpEncoder, myEncoderParameters);


                            newMs.Seek(0, SeekOrigin.Begin);
                            data = new byte[newMs.Length];
                            newMs.Read(data, 0, data.Length);
                            newMs.Close();
                        }
                        ms2.Close();
                    }
                    ms.Close();
                }
            }


            return data;
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private byte[] Fetch(string url)
        {
            WebRequest wq = HttpWebRequest.Create(url);

            (wq as HttpWebRequest).UserAgent = UserAgent;
            (wq as HttpWebRequest).Referer = Referer;

            (wq as HttpWebRequest).KeepAlive = false;
            (wq as HttpWebRequest).AllowAutoRedirect = true;
            (wq as HttpWebRequest).Timeout = 5000;

            WebResponse resp = wq.GetResponse();

            if (resp.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {
                using (Stream responseStream = resp.GetResponseStream())
                {
                    return Utilities.ReadFully(responseStream);
                }
            }
            else
            {
                string message = ComposeErrorMessage(resp, url);
                throw (new BruTile.Web.WebResponseFormatException(message, null));
            }

            /*Stream s = resp.GetResponseStream();

            int toRead = (int)resp.ContentLength;
            int numRead = 0;
            byte[] data = new byte[toRead];
            do
            {
                int nr = s.Read(data, numRead, toRead - numRead);
                numRead += nr;
            } while (numRead < toRead);

            resp.Close();
            return data;*/
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
