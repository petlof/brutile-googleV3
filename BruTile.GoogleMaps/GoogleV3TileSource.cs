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
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace BruTile.GoogleMaps
{

    /// <summary>
    /// Implementation of a GoogleMaps V3 TileSource
    /// This hosts the Google Maps Javascript API internally in the application and grabs tiles from that to render in brutile.
    /// </summary>
    public class GoogleV3TileSource : BruTile.ITileSource, IDisposable
    {
        public enum MapTypeId { ROADMAP, SATELLITE, HYBRID, TERRAIN };
        private GoogleV3TileSchema _tileSchema;
        private GoogleV3TileProvider _googleMapsTP;

        public GoogleV3TileSource(string googleClientID, string googleChannel, string baseUrl, MapTypeId mapType)
        {
            _tileSchema = new GoogleMaps.GoogleV3TileSchema(googleClientID, googleChannel, baseUrl, mapType);
            if (!string.IsNullOrEmpty(baseUrl))
                _googleMapsTP = new GoogleMaps.GoogleV3TileProvider(_tileSchema);
            else
                _googleMapsTP = new GoogleMaps.GoogleV3TileProvider(_tileSchema, baseUrl);

        }

        public GoogleV3TileSource(MapTypeId mapType)
            : this(null, null, null, mapType)
        {

        }

        public GoogleV3TileSource()
            : this(null, null, null, MapTypeId.ROADMAP)
        {

        }
        public ITileProvider Provider
        {
            get { return _googleMapsTP; }
        }

        public ITileSchema Schema
        {
            get { return _tileSchema; }
        }

        public void Dispose()
        {
            if (_tileSchema != null)
            {
                _tileSchema.Dispose();
                _tileSchema = null;
            }
        }
    }
}
