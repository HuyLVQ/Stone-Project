using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stone_Application.Observer
{
    public interface IEventObserver<T>
    {
        void Update(T data);
    }
}
