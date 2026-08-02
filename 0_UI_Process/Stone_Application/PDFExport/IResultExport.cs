using System.Collections.Generic;
using Stone_Application.Event;

namespace Stone_Application.PDFExport
{
    public interface IResultExport
    {
        bool ExportFile(string p_outputFilePath, IEnumerable<IInformation> p_measurements);
    }
}
