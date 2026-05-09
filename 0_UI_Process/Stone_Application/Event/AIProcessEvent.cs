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
        private List<IEventObserver<IInformation>> informationObservers = new List<IEventObserver<IInformation>>();
        private List<IEventObserver<IImage>> imageObservers = new List<IEventObserver<IImage>>();

        private static AIProcessEvent instance;
        private readonly IPCServices ipcService;


        private AIProcessEvent(IPCServices ipcServiceArg)
        {
            ipcService = ipcServiceArg;
        }

        public static AIProcessEvent getInstance(IPCServices ipcServiceArg)
        {
            if (instance == null)
            {
                instance = new AIProcessEvent(ipcServiceArg);
            }
            return instance;
        }

        public void attachInformationObserver(IEventObserver<IInformation> observer)
        {
            this.informationObservers.Add(observer);
            Console.WriteLine("Information observer attached.");
        }

        public void attachImageObserver(IEventObserver<IImage> observer)
        {
            this.imageObservers.Add(observer);
            Console.WriteLine("Image observer attached.");
        }

        public void detachInformationObserver(IEventObserver<IInformation> observer)
        {
            this.informationObservers.Remove(observer);
        }

        public void detachImageObserver(IEventObserver<IImage> observer)
        {
            this.imageObservers.Remove(observer);
        }


        public void notifyInformation(IInformation information)
        {
            foreach (var observer in this.informationObservers)
            {
                observer.Update(information);
            }
        }

        public void notifyImage(IImage image)
        {
            foreach (var observer in this.imageObservers)
            {
                observer.Update(image);
            }
        }



        public void notify()
        {
            try
            {
                var (information, image) = ipcService.readTask();

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
