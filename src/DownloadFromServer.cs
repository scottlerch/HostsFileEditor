﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HostsFileEditor.Properties;

namespace HostsFileEditor
{
    public partial class DownloadFromServer : Form
    {
        public DownloadFromServer()
        {
            InitializeComponent();
            LoadSettings();
            if (!string.IsNullOrWhiteSpace(textBoxURL.Text) &&
                !string.IsNullOrWhiteSpace(textBoxUserName.Text))
            {
                this.ActiveControl = textBoxPassword;
            }
        }

        private void LoadSettings()
        {
            Settings settings = new Settings();
            textBoxURL.Text = settings.DownloadServerURL;
            textBoxUserName.Text = settings.DownloadUsername;
        }

        private void SaveSettings()
        {
            Settings settings = new Settings();
            if (!string.IsNullOrWhiteSpace(textBoxURL.Text))
            {
                settings.DownloadServerURL = textBoxURL.Text;
            }
            if (!string.IsNullOrWhiteSpace(textBoxUserName.Text))
            {
                settings.DownloadUsername = textBoxUserName.Text;
            }

            settings.Save();
        }

        private void buttonDownload_Click(object sender, EventArgs e)
        {
            SaveSettings();
            if (string.IsNullOrWhiteSpace(textBoxURL.Text) ||
                string.IsNullOrWhiteSpace(textBoxUserName.Text) ||
                string.IsNullOrWhiteSpace(textBoxPassword.Text))
            {
                MessageBox.Show("All three fields must have information.", this.Text);
            }
            else
            {
                Dictionary<string, bool> downloadResults = GetHostsFileFromServer.DownloadFiles(textBoxURL.Text, textBoxUserName.Text, textBoxPassword.Text);
                MessageBox.Show("Completed. " + downloadResults.Count + " mod files downloaded.", this.Text);
            }
        }
    }
}
