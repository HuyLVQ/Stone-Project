using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stone_Application.Repository
{
    public interface IRepository<T>
    {
        void add(T entity);
        void update(T entity);

        T getTotal();
        void reset();

        T get(string start_time, string end_time);

    }
}
