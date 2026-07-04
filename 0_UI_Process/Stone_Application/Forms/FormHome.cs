using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Stone_Application.Forms
{
    public partial class FormHome : Form
    {
        private MainForm m_mainForm;
        private static FormHome s_instance;
        public FormHome(MainForm p_mainForm)
        {
            InitializeComponent();
            this.m_mainForm = p_mainForm;
            s_instance = this;

            this.TopLevel = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;           
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            // Ensure the PictureBox scales the received image to fit its bounds while preserving aspect ratio.
            this.pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        }

        public static void updatePictureBox(Bitmap p_bmp)
        {
            if (s_instance != null && !s_instance.IsDisposed)
            {
                if (s_instance.pictureBox.InvokeRequired)
                {
                    s_instance.pictureBox.Invoke(new Action(() => updatePictureBox(p_bmp)));
                    return;
                }

                if (s_instance.pictureBox.Image != null) s_instance.pictureBox.Image.Dispose();
                s_instance.pictureBox.Image = (Bitmap)p_bmp.Clone();
            }
        }
    }
}
