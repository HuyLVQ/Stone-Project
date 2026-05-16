using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocumentFormat.OpenXml.Drawing;
using Stone_Application.Event;

namespace Stone_Application.Forms
{
    public partial class FormLogs : Form
    {
        private MainForm mainForm;
        private static FormLogs instance;

        public FormLogs(MainForm mainForm)
        {
            InitializeComponent();
            this.mainForm = mainForm;
            instance = this;

            this.TopLevel = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ControlBox = false;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;

            this.textBoxLogging.Clear();
            this.textBoxLogging.Font = new Font("Courier New", 11f);
            textBoxLogging.WordWrap = false;
            textBoxLogging.ScrollBars = RichTextBoxScrollBars.Vertical;
            
        }

        private static void AppendColoredText(  RichTextBox box,
                                                string text,
                                                Color color, 
                                                bool isBold = false)
        {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = color;

            if (isBold)
            {
                box.SelectionFont = new Font(box.Font, FontStyle.Bold);
            }
            else
            {
                box.SelectionFont = new Font(box.Font, FontStyle.Regular);
            }

            box.AppendText(text);

            box.SelectionColor = box.ForeColor;
        }

        public static void updateLogs(IInformation information)
        {
            if (instance == null ||
                instance.IsDisposed ||
                !instance.IsHandleCreated)
                return;

            instance.BeginInvoke(new Action(() =>
            {
                string time = DateTime.Now.ToString(    "MM/dd/yyyy HH:mm:ss",
                                                        new CultureInfo("en-US"));

                instance.textBoxLogging.AppendText(
                    "============================================================\n");

                AppendColoredText(
                    instance.textBoxLogging,
                    $"[{time}]\n\n",
                    Color.Goldenrod,
                    true);

                AppendColoredText(
                    instance.textBoxLogging,
                    "Mi sàng : ",
                    Color.Green,
                    true);

                instance.textBoxLogging.AppendText(
                    $"{information.deltaPerctMiSang,8:F2}%\n");

                AppendColoredText(
                    instance.textBoxLogging,
                    "1x2 : ",
                    Color.Green,
                    true);

                instance.textBoxLogging.AppendText(
                    $"{information.deltaPerct1x2,8:F2}%\n");

                AppendColoredText(
                    instance.textBoxLogging,
                    "2x4 : ",
                    Color.Green,
                    true);

                instance.textBoxLogging.AppendText(
                    $"{information.deltaPerct2x4,8:F2}%\n");

                AppendColoredText(
                    instance.textBoxLogging,
                    "4x6 : ",
                    Color.Green,
                    true);

                instance.textBoxLogging.AppendText(
                    $"{information.deltaPerct4x6,8:F2}%\n");

                AppendColoredText(
                    instance.textBoxLogging,
                    "Total weight: ",
                    Color.Cyan,
                    true);

                instance.textBoxLogging.AppendText(
                    $"{information.measuredWeight} g\n");

                instance.textBoxLogging.AppendText(
                    "============================================================\n\n");

                //instance.textBoxLogging.ScrollToCaret();

                //instance.textBoxLogging.AppendText(log);

                instance.textBoxLogging.SelectionStart =
                    instance.textBoxLogging.TextLength;

                instance.textBoxLogging.ScrollToCaret();
            }));
        }
    }
}
