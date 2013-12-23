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

namespace BruTile.GoogleMaps
{

    /// <summary>
    /// Implementation of a GoogleMaps V3 TileSource
    /// This hosts the Google Maps Javascript API internally in the application and grabs tiles from that to render in brutile.
    /// </summary>
    public class GoogleV3TileSource : ITileSource, IDisposable
    {
        public enum MapTypeId { ROADMAP, SATELLITE, HYBRID, TERRAIN };
        private GoogleV3TileSchema m_tileSchema;
        private readonly GoogleV3TileProvider m_googleMapsTp;

        public GoogleV3TileSource(string googleClientID, string googleChannel, string baseUrl, MapTypeId mapType)
        {
            m_tileSchema = new GoogleV3TileSchema(googleClientID, googleChannel, baseUrl, mapType);
            m_googleMapsTp = !string.IsNullOrEmpty(baseUrl) ? new GoogleV3TileProvider(m_tileSchema) : new GoogleV3TileProvider(m_tileSchema, baseUrl);
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
            get { return m_googleMapsTp; }
        }

        public ITileSchema Schema
        {
            get { return m_tileSchema; }
        }

        public void Dispose()
        {
            if (m_tileSchema != null)
            {
                m_tileSchema.Dispose();
                m_tileSchema = null;
            }
        }
    }
}
