using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stone_Application.Event;

namespace Stone_Application.Repository
{
    public class NoSQLRepository<T> : IRepository<T> where T : IInformation
    {

        private static NoSQLRepository<T> instance;
        private static readonly object _lock = new object();

        private SortedList<DateTime, IInformation> customNoSQL;

        private NoSQLRepository()
        {
            customNoSQL = new SortedList<DateTime, IInformation>();
        }

        public static NoSQLRepository<T> getIntance()
        {
            if (instance == null)
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new NoSQLRepository<T>();
                    }
                }
            }
            return instance;
        }

        void IRepository<T>.add(T entity)
        {
            lock(_lock)
            {
                if (customNoSQL.Count() > 0)
                {
                    entity.deltaPerctMiSang += customNoSQL.Values.Last().deltaPerctMiSang;
                    entity.deltaPerct1x2 += customNoSQL.Values.Last().deltaPerct1x2;
                    entity.deltaPerct2x4 += customNoSQL.Values.Last().deltaPerct2x4;
                    entity.deltaPerct4x6 += customNoSQL.Values.Last().deltaPerct4x6;
                }

                customNoSQL.Add(DateTime.Now, entity);
                Console.WriteLine("[INFO] [REPOSITORY] [ADD] New record has been added");
            }
        }

        void IRepository<T>.update(T entity)
        {
            throw new NotImplementedException();
        }

        T IRepository<T>.getTotal()
        {
            lock (_lock)
            {
                T result = Activator.CreateInstance<T>();

                if (customNoSQL.Count > 0)
                {
                    var latestRecord = customNoSQL.Values.Last();
                    result.deltaPerctMiSang = latestRecord.deltaPerctMiSang / customNoSQL.Count();
                    result.deltaPerct1x2 = latestRecord.deltaPerct1x2 / customNoSQL.Count();
                    result.deltaPerct2x4 = latestRecord.deltaPerct2x4 / customNoSQL.Count();
                    result.deltaPerct4x6 = latestRecord.deltaPerct4x6 / customNoSQL.Count();
                    Console.WriteLine("[INFO] [REPOSITORY] [TOTAL] Some records have been retrieved.");
                }
                else
                {
                    result.deltaPerctMiSang = 0.0;
                    result.deltaPerct1x2 = 0.0;
                    result.deltaPerct2x4 = 0.0;
                    result.deltaPerct4x6 = 0.0;
                    Console.WriteLine("[WARN] [REPOSITORY] Zero Count. Returning default values.");
                }
                return result;
            }
        }

        void IRepository<T>.reset()
        {
            lock (_lock)
            {
                customNoSQL.Clear();
                Console.WriteLine("[INFO] [REPOSITORY] [RESET] All records have been cleared.");
            }
        }

        T IRepository<T>.get(string start_time, string end_time)
        {
            DateTime startTime = DateTime.Parse(start_time);
            DateTime endTime = DateTime.Parse(end_time);
            List<IInformation> filteredRecords; 

            lock (_lock)
            {
                filteredRecords = customNoSQL.Where(record => record.Key >= startTime && record.Key <= endTime)
                                             .Select(record => record.Value)
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
                Console.WriteLine("[INFO] [REPOSITORY] [GET] Some records have been retrieved for the specified time range.");
            }
            else
            {
                result.deltaPerctMiSang = 0.0;
                result.deltaPerct1x2 = 0.0;
                result.deltaPerct2x4 = 0.0;
                result.deltaPerct4x6 = 0.0;
                Console.WriteLine("[WARN] [REPOSITORY] Zero Count for the specified time range. Returning default values.");
            }
            return result;
        }
    }
}
