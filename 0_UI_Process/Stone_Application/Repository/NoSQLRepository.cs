using Stone_Application.Event;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Stone_Application.Repository
{
    public class NoSQLRepository<TInformation, TResultInformation> : IRepository<TInformation, TResultInformation>
        where TInformation : IInformation
        where TResultInformation : IResultInformation, new()
    {
        private static NoSQLRepository<TInformation, TResultInformation> s_instance;
        private static readonly object s_lock = new object();

        private readonly SortedList<DateTime, IInformation> m_customNoSQL;

        private NoSQLRepository()
        {
            m_customNoSQL = new SortedList<DateTime, IInformation>();
        }

        public static NoSQLRepository<TInformation, TResultInformation> getIntance()
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null)
                    {
                        s_instance = new NoSQLRepository<TInformation, TResultInformation>();
                    }
                }
            }

            return s_instance;
        }

        void IRepository<TInformation, TResultInformation>.add(TInformation p_entity)
        {
            lock (s_lock)
            {
                if (m_customNoSQL.Count > 0)
                {
                    IInformation latestRecord = m_customNoSQL.Values.Last();
                    p_entity.deltaPerctMiSang += latestRecord.deltaPerctMiSang;
                    p_entity.deltaPerct1x2 += latestRecord.deltaPerct1x2;
                    p_entity.deltaPerct2x4 += latestRecord.deltaPerct2x4;
                    p_entity.deltaPerct4x6 += latestRecord.deltaPerct4x6;
                    p_entity.measuredWeight += latestRecord.measuredWeight;
                }

                m_customNoSQL.Add(DateTime.Now, p_entity);
                Console.WriteLine("[INFO] [REPOSITORY] [ADD] New record has been added");
            }
        }

        void IRepository<TInformation, TResultInformation>.update(TInformation p_entity)
        {
            throw new NotImplementedException();
        }

        TResultInformation IRepository<TInformation, TResultInformation>.getTotal()
        {
            lock (s_lock)
            {
                if (m_customNoSQL.Count == 0)
                {
                    Console.WriteLine("[WARN] [REPOSITORY] Zero Count. Returning default values.");
                    return CreateResult(0, 0, 0, 0, 0.0f);
                }

                IInformation latestRecord = m_customNoSQL.Values.Last();
                Console.WriteLine("[INFO] [REPOSITORY] [TOTAL] Some records have been retrieved.");
                return CreateResult(
                    latestRecord.deltaPerctMiSang,
                    latestRecord.deltaPerct1x2,
                    latestRecord.deltaPerct2x4,
                    latestRecord.deltaPerct4x6,
                    latestRecord.measuredWeight);
            }
        }

        void IRepository<TInformation, TResultInformation>.reset()
        {
            lock (s_lock)
            {
                m_customNoSQL.Clear();
                Console.WriteLine("[INFO] [REPOSITORY] [RESET] All records have been cleared.");
            }
        }

        TResultInformation IRepository<TInformation, TResultInformation>.get(string p_startTime, string p_endTime)
        {
            DateTime startTime = DateTime.Parse(p_startTime, CultureInfo.InvariantCulture);
            DateTime endTime = DateTime.Parse(p_endTime, CultureInfo.InvariantCulture);
            List<IInformation> filteredRecords;

            lock (s_lock)
            {
                filteredRecords = m_customNoSQL
                    .Where(p_record => p_record.Key >= startTime && p_record.Key <= endTime)
                    .Select(p_record => p_record.Value)
                    .ToList();
            }

            if (filteredRecords.Count == 0)
            {
                Console.WriteLine("[WARN] [REPOSITORY] Zero Count for the specified time range. Returning default values.");
                return CreateResult(0, 0, 0, 0, 0.0f);
            }

            IInformation latestRecord = filteredRecords.Last();
            IInformation oldestRecord = filteredRecords.First();
            Console.WriteLine("[INFO] [REPOSITORY] [GET] Some records have been retrieved for the specified time range.");
            return CreateResult(
                latestRecord.deltaPerctMiSang - oldestRecord.deltaPerctMiSang,
                latestRecord.deltaPerct1x2 - oldestRecord.deltaPerct1x2,
                latestRecord.deltaPerct2x4 - oldestRecord.deltaPerct2x4,
                latestRecord.deltaPerct4x6 - oldestRecord.deltaPerct4x6,
                latestRecord.measuredWeight - oldestRecord.measuredWeight);
        }

        string IRepository<TInformation, TResultInformation>.getStartTime()
        {
            lock (s_lock)
            {
                if (m_customNoSQL.Count > 0)
                {
                    return m_customNoSQL.Keys.First().ToString("yyyy-MM-dd__hh-mm-ss", CultureInfo.InvariantCulture);
                }

                Console.WriteLine("[WARN] [REPOSITORY] No records available. Returning empty string.");
                return string.Empty;
            }
        }

        string IRepository<TInformation, TResultInformation>.getLatestTime()
        {
            lock (s_lock)
            {
                if (m_customNoSQL.Count > 0)
                {
                    return m_customNoSQL.Keys.Last().ToString("yyyy-MM-dd__hh-mm-ss", CultureInfo.InvariantCulture);
                }

                Console.WriteLine("[WARN] [REPOSITORY] No records available. Returning empty string.");
                return string.Empty;
            }
        }

        private static TResultInformation CreateResult(
            float p_miSang,
            float p_1x2,
            float p_2x4,
            float p_4x6,
            float p_weight)
        {
            TResultInformation result = new TResultInformation();
            float total = p_miSang + p_1x2 + p_2x4 + p_4x6;

            if (total > 0)
            {
                result.resultPerctMiSang = p_miSang / total;
                result.resultPerct1x2 = p_1x2 / total;
                result.resultPerct2x4 = p_2x4 / total;
                result.resultPerct4x6 = p_4x6 / total;
            }

            result.resultWeight = p_weight;
            return result;
        }
    }
}
