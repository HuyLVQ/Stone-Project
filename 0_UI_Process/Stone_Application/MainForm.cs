using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using Stone_Application.Forms;

namespace Stone_Application
{
    public partial class MainForm : Form
    {
        private FormHome formHomeInstance;
        private FormSettings formSettingsInstance;
        private FormLogs formLogsInstance;



        public MainForm()
        {
            InitializeComponent();

            this.panelLoad.Controls.Clear();

            this.formHomeInstance = new FormHome(this) { Dock = DockStyle.Fill };
            this.formSettingsInstance = new FormSettings(this) { Dock = DockStyle.Fill };
            this.formLogsInstance = new FormLogs(this) { Dock = DockStyle.Fill };

            this.panelHighlight.Height = this.buttonHome.Height;
            this.panelHighlight.Top = this.buttonHome.Top;
            this.panelHighlight.Left = this.buttonHome.Left;
            this.buttonHome.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.buttonLog.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonSettings.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonExit.BackColor = ColorTranslator.FromHtml("#4B164C");

            this.panelLoad.Controls.Add(this.formHomeInstance);
            this.formHomeInstance.Show();
        }

        private void buttonHomeClick(object sender, EventArgs e)
        {
            this.panelHighlight.Height = this.buttonHome.Height;
            this.panelHighlight.Top = this.buttonHome.Top;
            this.panelHighlight.Left = this.buttonHome.Left;
            this.buttonHome.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.buttonLog.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonSettings.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonExit.BackColor = ColorTranslator.FromHtml("#4B164C");

            this.panelLoad.Controls.Clear();
            this.panelLoad.Controls.Add(this.formHomeInstance);
            this.formHomeInstance.Show();
        }

        private void buttonLogClick(object sender, EventArgs e)
        {
            this.panelHighlight.Height = this.buttonLog.Height;
            this.panelHighlight.Top = this.buttonLog.Top;
            this.panelHighlight.Left = this.buttonLog.Left;
            this.buttonLog.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.buttonHome.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonSettings.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonExit.BackColor = ColorTranslator.FromHtml("#4B164C");

            this.panelLoad.Controls.Clear();
            this.panelLoad.Controls.Add(this.formLogsInstance);
            this.formLogsInstance.Show();
        }

        private void buttonSettingsClick(object sender, EventArgs e)
        {
            this.panelHighlight.Height = this.buttonSettings.Height;
            this.panelHighlight.Top = this.buttonSettings.Top;
            this.panelHighlight.Left = this.buttonSettings.Left;
            this.buttonSettings.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.buttonHome.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonLog.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonExit.BackColor = ColorTranslator.FromHtml("#4B164C");

            this.panelLoad.Controls.Clear();
            this.panelLoad.Controls.Add(this.formSettingsInstance);
            this.formSettingsInstance.Show();
        }

        private void buttonExitClick(object sender, EventArgs e)
        {
            this.panelHighlight.Height = this.buttonExit.Height;
            this.panelHighlight.Top = this.buttonExit.Top;
            this.panelHighlight.Left = this.buttonExit.Left;
            this.buttonExit.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.buttonHome.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonLog.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.buttonSettings.BackColor = ColorTranslator.FromHtml("#4B164C");

            //exit function
            Thread.Sleep(2000);
            Application.Exit();
        }
    }
}
