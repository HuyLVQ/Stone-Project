using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
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
        private const string LogComponentName = "AI_PROCESS";
        private MainForm mainForm;
        private static FormLogs instance;
        private static long logSequence;

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
            textBoxLogging.ScrollBars = RichTextBoxScrollBars.Both;
        }

        private static void AppendColoredText(RichTextBox box,
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

        private static void AppendMetric(RichTextBox box, string metricName, string metricValue)
        {
            AppendColoredText(
                box,
                $"    {metricName,-24}",
                Color.SteelBlue,
                true);

            box.AppendText($": {metricValue}\n");
        }

        private static void AppendLogEntry(RichTextBox box, IInformation information)
        {
            long eventId = Interlocked.Increment(ref logSequence);
            string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

            AppendColoredText(box, timestamp, Color.Goldenrod, true);
            box.AppendText(" | ");
            AppendColoredText(box, "INFO ", Color.ForestGreen, true);
            box.AppendText(" | ");
            AppendColoredText(box, $"{LogComponentName,-10}", Color.MediumPurple, true);
            box.AppendText($" | Measurement snapshot received | event_id={eventId:D6}\n");

            AppendMetric(box, "sieve.misang_pct", $"{information.deltaPerctMiSang,8:F2} %");
            AppendMetric(box, "sieve.1x2_pct", $"{information.deltaPerct1x2,8:F2} %");
            AppendMetric(box, "sieve.2x4_pct", $"{information.deltaPerct2x4,8:F2} %");
            AppendMetric(box, "sieve.4x6_pct", $"{information.deltaPerct4x6,8:F2} %");
            AppendMetric(box, "weight.total_g", $"{information.measuredWeight,8:F2}");

            box.AppendText("--------------------------------------------------------------------------------\n");
        }

        public static void updateLogs(IInformation information)
        {
            if (instance == null ||
                instance.IsDisposed ||
                !instance.IsHandleCreated)
                return;

            instance.BeginInvoke(new Action(() =>
            {
                AppendLogEntry(instance.textBoxLogging, information);

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
