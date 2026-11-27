namespace Stone_Client.Observer
{
    public interface IEventObserver<T>
    {
        void Update(T p_data);
    }
}
