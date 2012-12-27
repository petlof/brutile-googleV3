using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GoogleMapsTest
{
    public partial class FrmMapFrm : Form
    {
        BruTile.GoogleMaps.GoogleV3TileSource ts;
        public FrmMapFrm()
        {
            ts = new BruTile.GoogleMaps.GoogleV3TileSource();
            InitializeComponent();
            SharpMap.Layers.TileLayer tl = new SharpMap.Layers.TileLayer(ts, "Google");
            mapBox1.Map.Layers.Add(tl);
            mapBox1.Map.ZoomToBox(new GeoAPI.Geometries.Envelope(-1500000, 4250000, 4500000, 12500000));
            mapBox1.EnableShiftButtonDragRectangleZoom = true;
            mapBox1.PanOnClick = false;
            mapBox1.SetToolsNoneWhileRedrawing = false;
            mapBox1.Refresh();
            this.FormClosing += new FormClosingEventHandler(FrmMapFrm_FormClosing);
        }

        void FrmMapFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (ts != null)
            {
                ts.Dispose();
                ts = null;
            }
            
        }

    }
}
