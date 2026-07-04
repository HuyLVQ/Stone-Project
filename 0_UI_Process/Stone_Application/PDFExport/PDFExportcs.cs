using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Drawing.Charts;
using System.IO;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Globalization;

using Stone_Application;

namespace Stone_Application.PDFExport
{
    public class PDFExportcs
    {

        public static bool ExportFile(string p_outputFilePath,
                                      string p_startTime,
                                      string p_totalTime,
                                      double p_loadcellRecord,
                                      double p_realRecord,
                                      double p_perctMisang,
                                      double p_perct1x2,
                                      double p_perct2x4,
                                      double p_perct4x6)
        {
            File.Copy(Config.s_templatePath, p_outputFilePath, true);

            try
            {
                using (WordprocessingDocument doc = WordprocessingDocument.Open(p_outputFilePath, true))
                {
                    foreach (var text in doc.MainDocumentPart.Document.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                    {
                        if (text.Text.Contains("{{Start_time}}"))
                            text.Text = text.Text.Replace("{{Start_time}}", p_startTime);

                        if (text.Text.Contains("{{Total_time}}"))
                            text.Text = text.Text.Replace("{{Total_time}}", p_totalTime);

                        if (text.Text.Contains("{{Loadcell_record}}"))
                            text.Text = text.Text.Replace("{{Loadcell_record}}", p_loadcellRecord.ToString());

                        if (text.Text.Contains("AIdudoan"))
                            text.Text = text.Text.Replace("AIdudoan", p_realRecord.ToString());

                        if (text.Text.Contains("{{Deviation}}"))
                            text.Text = text.Text.Replace("{{Deviation}}", Math.Abs(p_realRecord - p_loadcellRecord).ToString());

                        if (text.Text.Contains("Dathaykhongdat"))
                            text.Text = text.Text.Replace("Dathaykhongdat", (Math.Abs(p_realRecord - p_loadcellRecord)/p_loadcellRecord <= 0.1) ? "Đạt" : "Không đạt");

                        if (text.Text.Contains("{{Sign}}"))
                            text.Text = text.Text.Replace("{{Sign}}", (Math.Abs(p_realRecord - p_loadcellRecord) / p_loadcellRecord <= 0.1) ? $"<" : $">");

                        if (text.Text.Contains("{{LVQHMisang}}"))
                            text.Text = text.Text.Replace("{{LVQHMisang}}", p_perctMisang.ToString(CultureInfo.InvariantCulture));

                        if (text.Text.Contains("{{LVQH1x2}}"))
                            text.Text = text.Text.Replace("{{LVQH1x2}}", p_perct1x2.ToString(CultureInfo.InvariantCulture));

                        if (text.Text.Contains("{{LVQH2x4}}"))
                            text.Text = text.Text.Replace("{{LVQH2x4}}", p_perct2x4.ToString(CultureInfo.InvariantCulture));

                        if (text.Text.Contains("{{LVQH4x6}}"))
                            text.Text = text.Text.Replace("{{LVQH4x6}}", p_perct4x6.ToString(CultureInfo.InvariantCulture));
                    }


                    var chartPart = doc.MainDocumentPart
                                       .ChartParts
                                       .First();

                    var chart = chartPart.ChartSpace;
                    var pieSeries = chart.Descendants<PieChartSeries>().First();

                    var values = pieSeries.Descendants<NumberingCache>().First();
                    var points = values.Elements<NumericPoint>().ToList();

                    points[0].NumericValue.Text = p_perctMisang.ToString(CultureInfo.InvariantCulture);
                    points[1].NumericValue.Text = p_perct1x2.ToString(CultureInfo.InvariantCulture);
                    points[2].NumericValue.Text = p_perct2x4.ToString(CultureInfo.InvariantCulture);
                    points[3].NumericValue.Text = p_perct4x6.ToString(CultureInfo.InvariantCulture);

                    values.PointCount.Val = (uint)points.Count;

                    var chartLabels = new[] { "Mi sàng", "1x2", "2x4", "4x6" };
                    var strCache = pieSeries.Descendants<CategoryAxisData>()
                                            .First()
                                            .Descendants<StringCache>()
                                            .First();
                    var strPoints = strCache.Elements<StringPoint>().ToList();

                    for (int i = 0; i < chartLabels.Length && i < strPoints.Count; i++)
                    {
                        strPoints[i].NumericValue.Text = chartLabels[i];
                    }

                    chart.Save();

                    var embeddedPart = chartPart.EmbeddedPackagePart;

                    using (var stream = embeddedPart.GetStream())
                    {
                        using (var spreadsheet = SpreadsheetDocument.Open(stream, true))
                        {
                            var sheet = spreadsheet.WorkbookPart.WorksheetParts.First().Worksheet;

                            var cells = sheet.Descendants<Cell>().ToList();

                            cells.First(p_c => p_c.CellReference == "B2").CellValue = new CellValue(p_perctMisang.ToString(CultureInfo.InvariantCulture));
                            cells.First(p_c => p_c.CellReference == "B3").CellValue = new CellValue(p_perct1x2.ToString(CultureInfo.InvariantCulture));
                            cells.First(p_c => p_c.CellReference == "B4").CellValue = new CellValue(p_perct2x4.ToString(CultureInfo.InvariantCulture));
                            cells.First(p_c => p_c.CellReference == "B5").CellValue = new CellValue(p_perct4x6.ToString(CultureInfo.InvariantCulture));

                            var sharedStrings = spreadsheet.WorkbookPart.SharedStringTablePart.SharedStringTable;
                            var labelCells = new[] { "A2", "A3", "A4", "A5" };

                            for (int i = 0; i < chartLabels.Length; i++)
                            {
                                var cell = cells.First(p_c => p_c.CellReference == labelCells[i]);
                                var sharedStringIndex = int.Parse(cell.CellValue.Text, CultureInfo.InvariantCulture);
                                sharedStrings.Elements<SharedStringItem>().ElementAt(sharedStringIndex).Text.Text = chartLabels[i];
                            }

                            sheet.Save();
                            sharedStrings.Save();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return false;
            }
            
            return true;
        }       

    }
}
