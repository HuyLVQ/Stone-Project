using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stone_Application.Event;
using Stone_Application.Forms;
using Stone_Application.Repository;

namespace Stone_Application.Observer
{
    public class UIInformationObserver<T> : IEventObserver<T> where T : IInformation
    {
        public UIInformationObserver()
        {
            ;
        }

        public void Update(T information)
        {
            FormLogs.updateLogs(information);
        }
    }
}
