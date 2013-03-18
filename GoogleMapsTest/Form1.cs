using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Threading;
using System.IO;

namespace GoogleMapsTest
{
    public partial class Form1 : Form
    {
        BruTile.GoogleMaps.GoogleV3TileSource ts;
        public Form1()
        {
            ts = new BruTile.GoogleMaps.GoogleV3TileSource( BruTile.GoogleMaps.GoogleV3TileSource.MapTypeId.HYBRID);
            InitializeComponent();
            SharpMap.Layers.TileLayer tl = new SharpMap.Layers.TileLayer(ts, "Google");

            mapBox1.Map.Layers.Add(tl);
            mapBox1.Map.ZoomToBox(new GeoAPI.Geometries.Envelope(-1500000, 4250000, 4500000, 12500000));
            mapBox1.EnableShiftButtonDragRectangleZoom = true;
            mapBox1.PanOnClick = false;
            mapBox1.SetToolsNoneWhileRedrawing = false;
            mapBox1.Refresh();
        }


        private void button1_Click(object sender, EventArgs e)
        {
            mapBox1.Refresh();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //for (int i = 0; i < 10; i++)
            {
                FrmMapFrm frm = new FrmMapFrm();
                frm.Show();
                //while (!frm.Visible)
                //{
                //    Thread.Sleep(100);
                //}
                
                //frm.Close();
                //frm.Dispose();
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            ts.Dispose();
        }
    }
}
