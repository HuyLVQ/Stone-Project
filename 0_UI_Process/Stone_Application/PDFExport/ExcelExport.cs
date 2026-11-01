using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Stone_Application.Event;

namespace Stone_Application.PDFExport
{
    public sealed class ExcelExport : IResultExport
    {
        private const uint FirstDataRow = 4;

        public bool ExportFile(string p_outputFilePath, IEnumerable<IInformation> p_measurements)
        {
            if (p_measurements == null)
                throw new ArgumentNullException(nameof(p_measurements));

            try
            {
                File.Copy(Config.s_excelTemplatePath, p_outputFilePath, true);

                using (SpreadsheetDocument document = SpreadsheetDocument.Open(p_outputFilePath, true))
                {
                    Worksheet worksheet = document.WorkbookPart.WorksheetParts.First().Worksheet;
                    SheetData sheetData = worksheet.GetFirstChild<SheetData>();
                    Row qualityHeader = GetOrCreateRow(sheetData, FirstDataRow - 1);
                    SetTextCell(qualityHeader, "J", FirstDataRow - 1, "Analysis status");
                    SetTextCell(qualityHeader, "K", FirstDataRow - 1, "Expected frames");
                    SetTextCell(qualityHeader, "L", FirstDataRow - 1, "Processed frames");
                    SetTextCell(qualityHeader, "M", FirstDataRow - 1, "Skipped frames");
                    SetTextCell(qualityHeader, "N", FirstDataRow - 1, "Recording losses");

                    uint rowIndex = FirstDataRow;
                    foreach (IInformation measurement in p_measurements)
                    {
                        Row row = GetOrCreateRow(sheetData, rowIndex);
                        double totalCount = measurement.countMiSang + measurement.count1x2 +
                                            measurement.count2x4 + measurement.count4x6;

                        SetNumericCell(row, "A", rowIndex, measurement.sessionId);
                        SetNumericCell(row, "B", rowIndex, CalculatePercentage(measurement.countMiSang, totalCount));
                        SetNumericCell(row, "C", rowIndex, CalculatePercentage(measurement.count1x2, totalCount));
                        SetNumericCell(row, "D", rowIndex, CalculatePercentage(measurement.count2x4, totalCount));
                        SetNumericCell(row, "E", rowIndex, CalculatePercentage(measurement.count4x6, totalCount));
                        SetNumericCell(row, "F", rowIndex, measurement.measuredWeight1);
                        SetTextCell(row, "J", rowIndex, measurement.analysisStatus);
                        SetNumericCell(row, "K", rowIndex, measurement.expectedFrames);
                        SetNumericCell(row, "L", rowIndex, measurement.processedFrames);
                        SetNumericCell(row, "M", rowIndex, measurement.skippedFrames);
                        SetNumericCell(row, "N", rowIndex, measurement.droppedFrames);
                        if (Config.s_isDebugMode == true) {
                            SetNumericCell(row, "G", rowIndex, measurement.measuredWeight2);
                            SetNumericCell(row, "H", rowIndex, measurement.measuredWeight3);
                            SetNumericCell(row, "I", rowIndex, measurement.measuredWeight4);
                        }
                        rowIndex++;
                    }

                    worksheet.Save();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error exporting Excel file: " + ex.Message);
                return false;
            }
        }

        private static double CalculatePercentage(long p_count, double p_totalCount)
        {
            return p_totalCount > 0 ? p_count / p_totalCount * 100.0 : 0.0;
        }

        private static Row GetOrCreateRow(SheetData p_sheetData, uint p_rowIndex)
        {
            Row row = p_sheetData.Elements<Row>().FirstOrDefault(p_row => p_row.RowIndex != null && p_row.RowIndex.Value == p_rowIndex);
            if (row != null)
                return row;

            row = new Row { RowIndex = p_rowIndex };
            var next = p_sheetData.Elements<Row>().FirstOrDefault(item => item.RowIndex != null && item.RowIndex.Value > p_rowIndex);
            if (next == null) p_sheetData.Append(row); else p_sheetData.InsertBefore(row, next);
            return row;
        }

        private static void SetNumericCell(Row p_row, string p_columnName, uint p_rowIndex, double p_value)
        {
            string reference = p_columnName + p_rowIndex;
            Cell cell = p_row.Elements<Cell>().FirstOrDefault(p_cell => p_cell.CellReference != null && p_cell.CellReference.Value == reference);
            if (cell == null)
            {
                cell = new Cell { CellReference = reference, StyleIndex = 4U };
                InsertCell(p_row, cell);
            }

            cell.DataType = CellValues.Number;
            cell.CellValue = new CellValue(p_value.ToString("0.##", CultureInfo.InvariantCulture));
        }

        private static void SetTextCell(Row row, string column, uint index, string value)
        {
            string reference = column + index;
            Cell cell = row.Elements<Cell>().FirstOrDefault(item => item.CellReference?.Value == reference);
            if (cell == null) { cell = new Cell { CellReference = reference }; InsertCell(row, cell); }
            cell.DataType = CellValues.InlineString;
            cell.CellValue = null;
            cell.InlineString = new InlineString(new Text(value ?? "Unknown"));
        }

        private static void InsertCell(Row row, Cell cell)
        {
            Func<string, int> column = reference => reference.TakeWhile(Char.IsLetter).Aggregate(0, (number, letter) => number * 26 + Char.ToUpperInvariant(letter) - 'A' + 1);
            int target = column(cell.CellReference.Value);
            Cell next = row.Elements<Cell>().FirstOrDefault(item => item.CellReference != null && column(item.CellReference.Value) > target);
            if (next == null) row.Append(cell); else row.InsertBefore(cell, next);
        }
    }
}
