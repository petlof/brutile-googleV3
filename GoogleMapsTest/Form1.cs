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
            //ThreadPool.SetMinThreads(20, 6);
            //ThreadPool.SetMaxThreads(50,30);
            
            ts = new BruTile.GoogleMaps.GoogleV3TileSource(BruTile.GoogleMaps.GoogleV3TileSource.MapTypeId.ROADMAP);
            InitializeComponent();
            SharpMap.Layers.TileAsyncLayer tl = new SharpMap.Layers.TileAsyncLayer(ts, "Google");

            mapBox1.Map.BackgroundLayer.Add(tl);
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

        //Random r = new Random();
        int nextMap = 1;
        private void button2_Click(object sender, EventArgs e)
        {
            mapBox1.Map.BackgroundLayer.Clear();
            if (ts != null)
                ts.Dispose();
            ts = new BruTile.GoogleMaps.GoogleV3TileSource((BruTile.GoogleMaps.GoogleV3TileSource.MapTypeId)(nextMap++ % 4));
            SharpMap.Layers.TileAsyncLayer tl = new SharpMap.Layers.TileAsyncLayer(ts, "Google");
            mapBox1.Map.BackgroundLayer.Add(tl);

            mapBox1.Refresh();

            
            //for (int i = 0; i < 10; i++)
            {
               // FrmMapFrm frm = new FrmMapFrm();
               // frm.Show();
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

        private void button3_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                Thread th = new Thread(new ThreadStart(delegate
                {
                    RunTest();
                }
                ));
                th.Name = "T" + i;
                th.Start();
            }
        }

        private void RunTest()
        {
            for (int i = 0; i < 1000; i++)
            {
                mapBox1.Refresh();
                Application.DoEvents();
                Thread.Sleep(1000);
                System.Diagnostics.Debug.WriteLine("Thread " + Thread.CurrentThread.Name + " , " + i);
            }
        }

    }
}
