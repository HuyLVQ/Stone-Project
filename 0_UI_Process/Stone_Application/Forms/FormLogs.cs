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
        private const string LOG_COMPONENT_NAME = "AI_PROCESS";
        private MainForm m_mainForm;
        private static FormLogs s_instance;
        private static long s_logSequence;

        public FormLogs(MainForm p_mainForm)
        {
            InitializeComponent();
            this.m_mainForm = p_mainForm;
            s_instance = this;

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

        private static void AppendColoredText(RichTextBox p_box,
                                              string p_text,
                                              Color p_color,
                                              bool p_isBold = false)
        {
            p_box.SelectionStart = p_box.TextLength;
            p_box.SelectionLength = 0;

            p_box.SelectionColor = p_color;

            if (p_isBold)
            {
                p_box.SelectionFont = new Font(p_box.Font, FontStyle.Bold);
            }
            else
            {
                p_box.SelectionFont = new Font(p_box.Font, FontStyle.Regular);
            }

            p_box.AppendText(p_text);

            p_box.SelectionColor = p_box.ForeColor;
        }

        private static void AppendMetric(RichTextBox p_box, string p_metricName, string p_metricValue)
        {
            AppendColoredText(
                p_box,
                $"    {p_metricName,-24}",
                Color.SteelBlue,
                true);

            p_box.AppendText($": {p_metricValue}\n");
        }

        private static void AppendLogEntry(RichTextBox p_box, IInformation p_information)
        {
            long eventId = Interlocked.Increment(ref s_logSequence);
            string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

            AppendColoredText(p_box, timestamp, Color.Goldenrod, true);
            p_box.AppendText(" | ");
            AppendColoredText(p_box, "INFO ", Color.ForestGreen, true);
            p_box.AppendText(" | ");
            AppendColoredText(p_box, $"{LOG_COMPONENT_NAME,-10}", Color.MediumPurple, true);
            p_box.AppendText($" | Measurement snapshot received | event_id={eventId:D6}\n");

            Int64 totalCount = p_information.countMiSang + p_information.count1x2 + p_information.count2x4 + p_information.count4x6;

            AppendMetric(p_box, "sieve.misang_pct", $"{p_information.countMiSang / totalCount,8:F2} %");
            AppendMetric(p_box, "sieve.1x2_pct", $"{p_information.count1x2 / totalCount,8:F2} %");
            AppendMetric(p_box, "sieve.2x4_pct", $"{p_information.count2x4 / totalCount,8:F2} %");
            AppendMetric(p_box, "sieve.4x6_pct", $"{p_information.count4x6 / totalCount,8:F2} %");
            AppendMetric(p_box, "weight.total_g", $"{p_information.measuredWeight,8:F2}");

            p_box.AppendText("--------------------------------------------------------------------------------\n");
        }

        public static void updateLogs(IInformation p_information)
        {
            if (s_instance == null ||
                s_instance.IsDisposed ||
                !s_instance.IsHandleCreated)
                return;

            s_instance.BeginInvoke(new Action(() =>
            {
                if (s_instance.textBoxLogging.Text.Length == 0)
                {
                    s_instance.m_buttonExportPDF.Enabled = true;
                    s_instance.m_buttonExportPDF.ForeColor = System.Drawing.SystemColors.ControlText;
                }

                AppendLogEntry(s_instance.textBoxLogging, p_information);

                s_instance.textBoxLogging.SelectionStart =
                    s_instance.textBoxLogging.TextLength;

                s_instance.textBoxLogging.ScrollToCaret();
            }));
        }

        private void buttonExportPDFClick(object p_sender, EventArgs p_e)
        {
            string startTime = Common.s_repositoryInstance.getStartTime();
            string currentTime = Common.s_repositoryInstance.getLatestTime();
            IResultInformation totalResult = Common.s_repositoryInstance.getTotal();

            if (startTime == null || currentTime == null)
            {
                MessageBox.Show("No data to export.", "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string outputFile = Config.s_outputPath + currentTime + ".docx";

            PDFExportcs.ExportFile(
                p_outputFilePath: outputFile,
                p_startTime: startTime,
                p_totalTime: currentTime,
                p_loadcellRecord: float.Parse(s_instance.userInputTextBox.Text.Length > 0 ? s_instance.userInputTextBox.Text : "0.0", CultureInfo.InvariantCulture),
                p_realRecord: totalResult.resultWeight,
                p_perctMisang: totalResult.resultPerctMiSang,
                p_perct1x2: totalResult.resultPerct1x2,
                p_perct2x4: totalResult.resultPerct2x4,
                p_perct4x6: totalResult.resultPerct4x6
            );

            this.Invoke(new Action(() =>
            {
                MessageBox.Show("Export successful to" + outputFile, "Export PDF", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));
        }
    }
}
