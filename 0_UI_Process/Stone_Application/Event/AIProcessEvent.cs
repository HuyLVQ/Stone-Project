using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Stone_Application.IPC;
using Stone_Application.Observer;

namespace Stone_Application.Event
{
    public class AIProcessEvent
    {
        private List<IEventObserver<IInformation>> m_informationObservers = new List<IEventObserver<IInformation>>();
        private List<IEventObserver<IImage>> m_imageObservers = new List<IEventObserver<IImage>>();

        private static AIProcessEvent s_instance;
        private readonly IPCServices m_ipcService;


        private AIProcessEvent(IPCServices p_ipcServiceArg)
        {
            m_ipcService = p_ipcServiceArg;
        }

        public static AIProcessEvent getInstance(IPCServices p_ipcServiceArg)
        {
            if (s_instance == null)
            {
                s_instance = new AIProcessEvent(p_ipcServiceArg);
            }
            return s_instance;
        }

        public void attachInformationObserver(IEventObserver<IInformation> p_observer)
        {
            this.m_informationObservers.Add(p_observer);
            Console.WriteLine("Information observer attached.");
        }

        public void attachImageObserver(IEventObserver<IImage> p_observer)
        {
            this.m_imageObservers.Add(p_observer);
            Console.WriteLine("Image observer attached.");
        }

        public void detachInformationObserver(IEventObserver<IInformation> p_observer)
        {
            this.m_informationObservers.Remove(p_observer);
        }

        public void detachImageObserver(IEventObserver<IImage> p_observer)
        {
            this.m_imageObservers.Remove(p_observer);
        }


        public void notifyInformation(IInformation p_information)
        {
            foreach (var observer in this.m_informationObservers)
            {
                observer.Update(p_information);
            }
        }

        public void notifyImage(IImage p_image)
        {
            foreach (var observer in this.m_imageObservers)
            {
                observer.Update(p_image);
            }
        }



        public void notify()
        {
            try
            {
                var (information, image) = m_ipcService.readTask();

                if (information != null)
                    notifyInformation(information);

                if (image != null)
                    notifyImage(image);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERROR] [AI EVENT] " + ex.Message);
            }
        }

    }
}
