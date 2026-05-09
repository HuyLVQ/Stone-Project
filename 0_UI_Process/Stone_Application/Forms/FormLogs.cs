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

        public static void updateLogs(IInformation information)
        {
            if (instance == null)
                return;

            instance.Invoke(new Action(() =>
            {
                string time = DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss",
                    new CultureInfo("en-US"));

                string log =
                    "============================================================\n" +
                    $"[{time}]\n\n" +
                    $"Mi sàng : {information.deltaPerctMiSang,8:F2}     %    " +
                    $"1x2     : {information.deltaPerct1x2,8:F2}\n" +
                    $"2x4     : {information.deltaPerct2x4,8:F2}    %    " +
                    $"4x6     : {information.deltaPerct4x6,8:F2}\n" +
                    //$"Total weight: {information.weight}\n" + 
                    "============================================================\n\n";

                instance.textBoxLogging.AppendText(log);
                instance.textBoxLogging.SelectionStart =
                    instance.textBoxLogging.TextLength;
                instance.textBoxLogging.ScrollToCaret();
            }));
        }
    }
}
