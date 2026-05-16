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

        public static bool ExportFile(string outputFilePath,
                                      string startTime,
                                      string totalTime,
                                      double loadcellRecord,
                                      double realRecord,
                                      double deviation,
                                      bool isOk,
                                      double perctMisang,
                                      double perct1x2,
                                      double perct2x4,
                                      double perct4x6)
        {
            File.Copy(Config.templatePath, outputFilePath, true);

            try
            {
                using (WordprocessingDocument doc = WordprocessingDocument.Open(outputFilePath, true))
                {
                    foreach (var text in doc.MainDocumentPart.Document.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>())
                    {
                        if (text.Text.Contains("{{Start_time}}"))
                            text.Text = text.Text.Replace("{{Start_time}}", startTime);

                        if (text.Text.Contains("{{Total_time}}"))
                            text.Text = text.Text.Replace("{{Total_time}}", totalTime);

                        if (text.Text.Contains("{{Loadcell_record}}"))
                            text.Text = text.Text.Replace("{{Loadcell_record}}", loadcellRecord.ToString());

                        if (text.Text.Contains("AIdudoan"))
                            text.Text = text.Text.Replace("AIdudoan", realRecord.ToString());

                        if (text.Text.Contains("{{Deviation}}"))
                            text.Text = text.Text.Replace("{{Deviation}}", deviation.ToString());

                        if (text.Text.Contains("Dathaykhongdat"))
                            text.Text = text.Text.Replace("Dathaykhongdat", isOk ? "Đạt" : "Không đạt");

                        if (text.Text.Contains("{{Sign}}"))
                            text.Text = text.Text.Replace("{{Sign}}", isOk ? $"<" : $">");
                    }


                    var chartPart = doc.MainDocumentPart
                                       .ChartParts
                                       .First();

                    var chart = chartPart.ChartSpace;
                    var pieSeries = chart.Descendants<PieChartSeries>().First();

                    var values = pieSeries.Descendants<NumberingCache>().First();
                    var points = values.Elements<NumericPoint>().ToList();

                    points[0].NumericValue.Text = perctMisang.ToString(CultureInfo.InvariantCulture);
                    points[1].NumericValue.Text = perct1x2.ToString(CultureInfo.InvariantCulture);
                    points[2].NumericValue.Text = perct2x4.ToString(CultureInfo.InvariantCulture);
                    points[3].NumericValue.Text = perct4x6.ToString(CultureInfo.InvariantCulture);

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

                            cells.First(c => c.CellReference == "B2").CellValue = new CellValue(perctMisang.ToString(CultureInfo.InvariantCulture));
                            cells.First(c => c.CellReference == "B3").CellValue = new CellValue(perct1x2.ToString(CultureInfo.InvariantCulture));
                            cells.First(c => c.CellReference == "B4").CellValue = new CellValue(perct2x4.ToString(CultureInfo.InvariantCulture));
                            cells.First(c => c.CellReference == "B5").CellValue = new CellValue(perct4x6.ToString(CultureInfo.InvariantCulture));

                            var sharedStrings = spreadsheet.WorkbookPart.SharedStringTablePart.SharedStringTable;
                            var labelCells = new[] { "A2", "A3", "A4", "A5" };

                            for (int i = 0; i < chartLabels.Length; i++)
                            {
                                var cell = cells.First(c => c.CellReference == labelCells[i]);
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
