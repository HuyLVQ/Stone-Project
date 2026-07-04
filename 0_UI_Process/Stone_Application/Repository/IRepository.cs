using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stone_Application.Repository
{
    public interface IRepository<TInformation, TResultInformation>
    {
        void add(TInformation p_entity);
        void update(TInformation p_entity);

        TResultInformation getTotal();
        void reset();

        TResultInformation get(string p_startTime, string p_endTime);

        string getStartTime();
        string getLatestTime();

    }
}
