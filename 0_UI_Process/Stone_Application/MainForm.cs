using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Stone_Application.Forms;

namespace Stone_Application
{
    public partial class MainForm : Form
    {
        private FormHome m_formHomeInstance;
        private FormSettings m_formSettingsInstance;
        private FormLogs m_formLogsInstance;
        private bool m_isDraggingWindow;
        private Point m_dragCursorPoint;
        private Point m_dragFormPoint;



        public MainForm()
        {
            InitializeComponent();
            ConfigureWindow();

            this.m_panelLoad.Controls.Clear();

            this.m_formHomeInstance = new FormHome(this) { Dock = DockStyle.Fill };
            this.m_formSettingsInstance = new FormSettings(this) { Dock = DockStyle.Fill };
            this.m_formLogsInstance = new FormLogs(this) { Dock = DockStyle.Fill };

            this.m_panelHighlight.Height = this.m_buttonHome.Height;
            this.m_panelHighlight.Top = this.m_buttonHome.Top;
            this.m_panelHighlight.Left = this.m_buttonHome.Left;
            this.m_buttonHome.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.m_buttonLog.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonSettings.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonExit.BackColor = ColorTranslator.FromHtml("#4B164C");

            this.m_panelLoad.Controls.Add(this.m_formHomeInstance);
            this.m_formHomeInstance.Show();
        }

        private void ConfigureWindow()
        {
            this.KeyPreview = true;
            this.WindowState = FormWindowState.Maximized;
            UpdateExpandButtonText();
        }

        private void ToggleWindowState()
        {
            this.WindowState = this.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;

            UpdateExpandButtonText();
        }

        private void UpdateExpandButtonText()
        {
            this.m_buttonWindowToggle.Text = this.WindowState == FormWindowState.Maximized
                ? "Restore"
                : "Expand";
        }

        private void buttonHomeClick(object p_sender, EventArgs p_e)
        {
            this.m_panelHighlight.Height = this.m_buttonHome.Height;
            this.m_panelHighlight.Top = this.m_buttonHome.Top;
            this.m_panelHighlight.Left = this.m_buttonHome.Left;
            this.m_buttonHome.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.m_buttonLog.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonSettings.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonExit.BackColor = ColorTranslator.FromHtml("#4B164C");

            this.m_panelLoad.Controls.Clear();
            this.m_panelLoad.Controls.Add(this.m_formHomeInstance);
            this.m_formHomeInstance.Show();
        }

        private void buttonLogClick(object p_sender, EventArgs p_e)
        {
            this.m_panelHighlight.Height = this.m_buttonLog.Height;
            this.m_panelHighlight.Top = this.m_buttonLog.Top;
            this.m_panelHighlight.Left = this.m_buttonLog.Left;
            this.m_buttonLog.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.m_buttonHome.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonSettings.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonExit.BackColor = ColorTranslator.FromHtml("#4B164C");

            this.m_panelLoad.Controls.Clear();
            this.m_panelLoad.Controls.Add(this.m_formLogsInstance);
            this.m_formLogsInstance.Show();
        }

        private void buttonSettingsClick(object p_sender, EventArgs p_e)
        {
            this.m_panelHighlight.Height = this.m_buttonSettings.Height;
            this.m_panelHighlight.Top = this.m_buttonSettings.Top;
            this.m_panelHighlight.Left = this.m_buttonSettings.Left;
            this.m_buttonSettings.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.m_buttonHome.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonLog.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonExit.BackColor = ColorTranslator.FromHtml("#4B164C");

            this.m_panelLoad.Controls.Clear();
            this.m_panelLoad.Controls.Add(this.m_formSettingsInstance);
            this.m_formSettingsInstance.Show();
        }

        private void buttonExitClick(object p_sender, EventArgs p_e)
        {
            this.m_panelHighlight.Height = this.m_buttonExit.Height;
            this.m_panelHighlight.Top = this.m_buttonExit.Top;
            this.m_panelHighlight.Left = this.m_buttonExit.Left;
            this.m_buttonExit.BackColor = ColorTranslator.FromHtml("#7e467d");

            this.m_buttonHome.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonLog.BackColor = ColorTranslator.FromHtml("#4B164C");
            this.m_buttonSettings.BackColor = ColorTranslator.FromHtml("#4B164C");

            Application.Exit();
        }

        private void buttonWindowMinimizeClick(object p_sender, EventArgs p_e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void buttonWindowToggleClick(object p_sender, EventArgs p_e)
        {
            ToggleWindowState();
        }

        private void panelTopBarMouseDown(object p_sender, MouseEventArgs p_e)
        {
            if (p_e.Button != MouseButtons.Left || this.WindowState == FormWindowState.Maximized)
            {
                return;
            }

            this.m_isDraggingWindow = true;
            this.m_dragCursorPoint = Cursor.Position;
            this.m_dragFormPoint = this.Location;
        }

        private void panelTopBarMouseMove(object p_sender, MouseEventArgs p_e)
        {
            if (!this.m_isDraggingWindow)
            {
                return;
            }

            Point dragOffset = Point.Subtract(Cursor.Position, new Size(this.m_dragCursorPoint));
            this.Location = Point.Add(this.m_dragFormPoint, new Size(dragOffset));
        }

        private void panelTopBarMouseUp(object p_sender, MouseEventArgs p_e)
        {
            this.m_isDraggingWindow = false;
        }

        private void panelTopBarDoubleClick(object p_sender, EventArgs p_e)
        {
            ToggleWindowState();
        }

        protected override void OnResize(EventArgs p_e)
        {
            base.OnResize(p_e);
            UpdateExpandButtonText();
        }

        protected override void OnShown(EventArgs p_e)
        {
            base.OnShown(p_e);
            this.m_panelHighlight.Height = this.m_buttonHome.Height;
            this.m_panelHighlight.Top = this.m_buttonHome.Top;
            this.m_panelHighlight.Left = this.m_buttonHome.Left;
        }

        protected override bool ProcessCmdKey(ref Message p_msg, Keys p_keyData)
        {
            if (p_keyData == Keys.F11)
            {
                ToggleWindowState();
                return true;
            }

            return base.ProcessCmdKey(ref p_msg, p_keyData);
        }
    }
}
