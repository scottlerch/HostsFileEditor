using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HostsFileEditor
{
    public partial class DownloadFromServer : Form
    {
        public DownloadFromServer()
        {
            InitializeComponent();
        }

        private void buttonDownload_Click(object sender, EventArgs e)
        {

            GetHostsFileFromServer.do(textBoxURL.Text, textBoxUserName.Text, textBoxPassword.Text);
        }
    }
}
