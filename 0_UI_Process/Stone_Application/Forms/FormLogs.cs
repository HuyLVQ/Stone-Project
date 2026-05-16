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
using Stone_Application.PDFExport;
using Stone_Application;

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
            if (instance == null ||
                instance.IsDisposed ||
                !instance.IsHandleCreated)
                return;

            instance.BeginInvoke(new Action(() =>
            {
                string time = DateTime.Now.ToString(
                    "MM/dd/yyyy HH:mm:ss",
                    new CultureInfo("en-US"));

                string log =
                    "============================================================\n" +
                    $"{"\u001b[33m"}[{time}]{"\u001b[0m"}\n\n" +
                    $"{"\u001b[32m"}Mi sàng :{"\u001b[0m"} {information.deltaPerctMiSang,8:F2}     %    " +
                    $"{"\u001b[32m"}1x2     :{"\u001b[0m"} {information.deltaPerct1x2,8:F2}\n" +
                    $"{"\u001b[32m"}2x4     :{"\u001b[0m"} {information.deltaPerct2x4,8:F2}    %    " +
                    $"{"\u001b[32m"}4x6     :{"\u001b[0m"} {information.deltaPerct4x6,8:F2}\n" +
                    $"{"\u001b[36m"}Total weight:{"\u001b[0m"} {information.measuredWeight}\n" +
                    "============================================================\n\n";

                instance.textBoxLogging.AppendText(log);

                instance.textBoxLogging.SelectionStart =
                    instance.textBoxLogging.TextLength;

                instance.textBoxLogging.ScrollToCaret();
            }));
        }

        private void buttonExportPDFClick(object sender, EventArgs e)
        {
            string startTime = Common.repositoryInstance.getStartTime();
            string currentTime = Common.repositoryInstance.getLatestTime();

            if (startTime == null || currentTime == null)
            {
                MessageBox.Show("No data to export.", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string outputFile = Config.outputPath + currentTime + ".docx";

            PDFExportcs.ExportFile(
                outputFilePath: outputFile,
                startTime: startTime,
                totalTime: currentTime,
                loadcellRecord: Common.repositoryInstance.getTotal().measuredWeight,
                realRecord: 0,
                deviation: 0,
                isOk: false,
                perctMisang: Common.repositoryInstance.getTotal().deltaPerctMiSang,
                perct1x2: Common.repositoryInstance.getTotal().deltaPerct1x2,
                perct2x4: Common.repositoryInstance.getTotal().deltaPerct2x4,
                perct4x6: Common.repositoryInstance.getTotal().deltaPerct4x6
            );

            this.Invoke(new Action(() =>
            {
                MessageBox.Show("Export successful to" + outputFile, "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));
        }
    }
}
