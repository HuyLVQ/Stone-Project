using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stone_Application.Repository
{
    public interface IRepository<T>
    {
        void add(T p_entity);
        void update(T p_entity);

        T getTotal();
        void reset();

        T get(string p_startTime, string p_endTime);

        string getStartTime();
        string getLatestTime();

    }
}
