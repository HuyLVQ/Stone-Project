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

        // Returns the latest accumulated measurement for every sub-session.
        // Exporters use this to produce one row per session.
        List<TInformation> getSessionMeasurements();

        string getStartTime();
        string getLatestTime();

    }
}
