using Stone_Application.Event;
using Stone_Application.Networking.Iot;

namespace Stone_Application.Observer
{
    public sealed class IotImageObserver<T> : IEventObserver<T> where T : IImage
    {
        public static readonly IotImageObserver<T> Instance = new IotImageObserver<T>();

        private IotImageObserver() { }

        public void Update(T image)
        {
            IotServer.Instance.PublishImage(image);
        }
    }
}
