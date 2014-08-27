using System;
using System.Threading;
using System.Windows.Forms;

namespace GoogleMapsTest
{
    public partial class Form1 : Form
    {
        BruTile.GoogleMaps.GoogleV3TileSource m_ts;
        public Form1()
        {
            m_ts = new BruTile.GoogleMaps.GoogleV3TileSource(BruTile.GoogleMaps.GoogleV3TileSource.MapTypeId.HYBRID);
            InitializeComponent();
            var tl = new SharpMap.Layers.TileAsyncLayer(m_ts, "Google");
            tl.OnlyRedrawWhenComplete = true;

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
        int m_nextMap = 1;
        private void button2_Click(object sender, EventArgs e)
        {
            mapBox1.Map.BackgroundLayer.Clear();
            if (m_ts != null)
                m_ts.Dispose();
            m_ts = new BruTile.GoogleMaps.GoogleV3TileSource((BruTile.GoogleMaps.GoogleV3TileSource.MapTypeId)(m_nextMap++ % 4));
            var tl = new SharpMap.Layers.TileAsyncLayer(m_ts, "Google");
            mapBox1.Map.BackgroundLayer.Add(tl);

            mapBox1.Refresh();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_ts.Dispose();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                var th = new Thread(RunTest)
                {
                    Name = "T" + i
                };
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
