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
        private MainForm mainForm;
        private static FormHome instance;
        public FormHome(MainForm mainForm)
        {
            InitializeComponent();
            this.mainForm = mainForm;
            instance = this;

            this.TopLevel = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;           
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            // Ensure the PictureBox scales the received image to fit its bounds while preserving aspect ratio.
            this.pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
        }

        public static void updatePictureBox(Bitmap bmp)
        {
            if (instance != null && !instance.IsDisposed)
            {
                if (instance.pictureBox.InvokeRequired)
                {
                    instance.pictureBox.Invoke(new Action(() => updatePictureBox(bmp)));
                    return;
                }

                if (instance.pictureBox.Image != null) instance.pictureBox.Image.Dispose();
                instance.pictureBox.Image = (Bitmap)bmp.Clone();
            }
        }
    }
}
