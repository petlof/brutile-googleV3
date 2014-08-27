using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace GoogleMapsTest
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
             using (Stream stream = Assembly.GetExecutingAssembly()
                               .GetManifestResourceStream("GoogleMapsTest.TextFile1.txt"))
            using (StreamReader reader = new StreamReader(stream))
            {
                webBrowser1.DocumentText =  reader.ReadToEnd();
            }
            


        }

        

        private void asdToolStripMenuItem_Click(object sender, EventArgs e)
        {
            object ret = null;
            webBrowser1.Invoke(new MethodInvoker(delegate
            {
                ret = webBrowser1.Document.InvokeScript("getTileURLs");
            }));

            Type t = ret.GetType();
            int len = Convert.ToInt32(t.InvokeMember("length", BindingFlags.GetProperty, null, ret, null));
            //jsTileInfo[] ti = new jsTileInfo[len];
            for (int i = 0; i < len; i++)
            {
                object item = t.InvokeMember("item_" + i, BindingFlags.GetProperty, null, ret, null);
                string url = t.InvokeMember("url", BindingFlags.GetProperty, null, item, null) as string;
                int left = (int)t.InvokeMember("left", BindingFlags.GetProperty, null, item, null);
                int top = (int)t.InvokeMember("top", BindingFlags.GetProperty, null, item, null);
                int index = (int)t.InvokeMember("index", BindingFlags.GetProperty, null, item, null);
                int zIndex = (int)t.InvokeMember("zIndex", BindingFlags.GetProperty, null, item, null);
               /* ti[i] = new jsTileInfo()
                {
                    Url = url,
                    Left = left,
                    Top = top,
                    Index = index,
                    zIndex = zIndex
                };*/
            }
        } 

    }

}
