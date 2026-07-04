using Stone_Application.Event;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stone_Application.Repository
{
    public class NoSQLRepository<T> : IRepository<T> where T : IInformation
    {

        private static NoSQLRepository<T> s_instance;
        private static readonly object s_lock = new object();

        private SortedList<DateTime, IInformation> m_customNoSQL;

        private NoSQLRepository()
        {
            m_customNoSQL = new SortedList<DateTime, IInformation>();
        }

        public static NoSQLRepository<T> getIntance()
        {
            if (s_instance == null)
            {
                lock (s_lock)
                {
                    if (s_instance == null)
                    {
                        s_instance = new NoSQLRepository<T>();
                    }
                }
            }
            return s_instance;
        }

        void IRepository<T>.add(T p_entity)
        {
            lock (s_lock)
            {
                if (m_customNoSQL.Count() > 0)
                {
                    p_entity.deltaPerctMiSang += m_customNoSQL.Values.Last().deltaPerctMiSang;
                    p_entity.deltaPerct1x2 += m_customNoSQL.Values.Last().deltaPerct1x2;
                    p_entity.deltaPerct2x4 += m_customNoSQL.Values.Last().deltaPerct2x4;
                    p_entity.deltaPerct4x6 += m_customNoSQL.Values.Last().deltaPerct4x6;
                    p_entity.measuredWeight += m_customNoSQL.Values.Last().measuredWeight;
                }

                m_customNoSQL.Add(DateTime.Now, p_entity);
                Console.WriteLine("[INFO] [REPOSITORY] [ADD] New record has been added");
            }
        }

        void IRepository<T>.update(T p_entity)
        {
            throw new NotImplementedException();
        }

        T IRepository<T>.getTotal()
        {
            lock (s_lock)
            {
                T result = Activator.CreateInstance<T>();

                if (m_customNoSQL.Count > 0)
                {
                    var latestRecord = m_customNoSQL.Values.Last();
                    result.deltaPerctMiSang = latestRecord.deltaPerctMiSang / m_customNoSQL.Count();
                    result.deltaPerct1x2 = latestRecord.deltaPerct1x2 / m_customNoSQL.Count();
                    result.deltaPerct2x4 = latestRecord.deltaPerct2x4 / m_customNoSQL.Count();
                    result.deltaPerct4x6 = latestRecord.deltaPerct4x6 / m_customNoSQL.Count();
                    result.measuredWeight = latestRecord.measuredWeight / m_customNoSQL.Count();
                    Console.WriteLine("[INFO] [REPOSITORY] [TOTAL] Some records have been retrieved.");
                }
                else
                {
                    result.deltaPerctMiSang = 0.0f;
                    result.deltaPerct1x2 = 0.0f;
                    result.deltaPerct2x4 = 0.0f;
                    result.deltaPerct4x6 = 0.0f;
                    result.measuredWeight = 0.0f;
                    Console.WriteLine("[WARN] [REPOSITORY] Zero Count. Returning default values.");
                }
                return result;
            }
        }

        void IRepository<T>.reset()
        {
            lock (s_lock)
            {
                m_customNoSQL.Clear();
                Console.WriteLine("[INFO] [REPOSITORY] [RESET] All records have been cleared.");
            }
        }

        T IRepository<T>.get(string p_startTime, string p_endTime)
        {
            DateTime startTime = DateTime.Parse(p_startTime);
            DateTime endTime = DateTime.Parse(p_endTime);
            List<IInformation> filteredRecords;

            lock (s_lock)
            {
                filteredRecords = m_customNoSQL.Where(p_record => p_record.Key >= startTime && p_record.Key <= endTime)
                                             .Select(p_record => p_record.Value)
                                             .ToList();
            }

            T result = Activator.CreateInstance<T>();
            if (filteredRecords.Count > 0)
            {
                var latestRecord = filteredRecords.Last();
                var oldestRecord = filteredRecords.First();
                result.deltaPerctMiSang = latestRecord.deltaPerctMiSang - oldestRecord.deltaPerctMiSang;
                result.deltaPerct1x2 = latestRecord.deltaPerct1x2 - oldestRecord.deltaPerct1x2;
                result.deltaPerct2x4 = latestRecord.deltaPerct2x4 - oldestRecord.deltaPerct2x4;
                result.deltaPerct4x6 = latestRecord.deltaPerct4x6 - oldestRecord.deltaPerct4x6;
                result.measuredWeight = latestRecord.measuredWeight - oldestRecord.measuredWeight;
                Console.WriteLine("[INFO] [REPOSITORY] [GET] Some records have been retrieved for the specified time range.");
            }
            else
            {
                result.deltaPerctMiSang = 0.0f;
                result.deltaPerct1x2 = 0.0f;
                result.deltaPerct2x4 = 0.0f;
                result.deltaPerct4x6 = 0.0f;
                result.measuredWeight = 0.0f;
                Console.WriteLine("[WARN] [REPOSITORY] Zero Count for the specified time range. Returning default values.");
            }
            return result;
        }


        string IRepository<T>.getStartTime()
        {
            lock (s_lock)
            {
                if (m_customNoSQL.Count > 0)
                {
                    return m_customNoSQL.Keys.First().ToString("yyyy-MM-dd__hh-mm-ss", CultureInfo.InvariantCulture);
                }
                else
                {
                    Console.WriteLine("[WARN] [REPOSITORY] No records available. Returning empty string.");
                    return string.Empty;
                }
            }
        }


        string IRepository<T>.getLatestTime()
        {
            lock (s_lock)
            {
                if (m_customNoSQL.Count > 0)
                {
                    return m_customNoSQL.Keys.Last().ToString("yyyy-MM-dd__hh-mm-ss", CultureInfo.InvariantCulture);
                }
                else
                {
                    Console.WriteLine("[WARN] [REPOSITORY] No records available. Returning empty string.");
                    return string.Empty;
                }
            }
        }
    }
}
