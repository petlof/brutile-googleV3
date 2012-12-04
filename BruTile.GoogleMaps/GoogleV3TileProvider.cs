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

namespace BruTile.GoogleMaps
{
    public class GoogleV3TileProvider : BruTile.ITileProvider
    {
        public const string UserAgent = @"Mozilla/5.0 (Windows; U; Windows NT 6.0; en-US; rv:1.9.1.7) Gecko/20091221 Firefox/3.5.7";
        private string Referer;

        public GoogleV3TileProvider(string referer = "http://localhost")
        {
            this.Referer = referer;
        }

        public byte[] GetTile(BruTile.TileInfo tileInfo)
        {
            if (!(tileInfo is GoogleV3TileInfo))
                throw new ApplicationException("Need to get GoogleV3TileInfo as param");

            var gti = tileInfo as GoogleV3TileInfo;

            var cacheEntry = WebCacheTool.WinInetAPI.GetUrlCacheEntryInfo(gti.Url);
            byte[] data = null;
            if (!string.IsNullOrEmpty(cacheEntry.lpszLocalFileName))
            {
                data = System.IO.File.ReadAllBytes(cacheEntry.lpszLocalFileName);
            }
            else
            {
                WebRequest wq = HttpWebRequest.Create(gti.Url);
                (wq as HttpWebRequest).UserAgent = UserAgent;
                (wq as HttpWebRequest).Referer = Referer;
                WebResponse resp = wq.GetResponse();

                Stream s = resp.GetResponseStream();

                int toRead = (int)resp.ContentLength;
                int numRead = 0;
                data = new byte[toRead];
                do
                {
                    int nr = s.Read(data, numRead, toRead - numRead);
                    numRead += nr;
                } while (numRead < toRead);

                resp.Close();
            }
            return data;
        }
    }
}
