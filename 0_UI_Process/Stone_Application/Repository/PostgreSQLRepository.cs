using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Npgsql;
using Microsoft.EntityFrameworkCore.Design;
using Stone_Application.Event;
using System.Globalization;

namespace Stone_Application.Repository
{
    public class PostgreSqlRepository : IRepository<IInformation, IResultInformation>
    {
        private readonly string m_connectionString;
        private const string m_TABLE_NAME = "measurement_information";

        public PostgreSqlRepository(
            string p_host = "localhost",
            int p_port = 5432,
            string p_database = "measurements_db",
            string p_username = "postgres",
            string p_password = "postgres")
        {
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = p_host,
                Port = p_port,
                Database = p_database,
                Username = p_username,
                Password = p_password
            };

            m_connectionString = builder.ConnectionString;

            EnsureTableExists();
        }

        private void EnsureTableExists()
        {
            string sql =
                "CREATE TABLE IF NOT EXISTS " + m_TABLE_NAME + " (" +
                "    id SERIAL PRIMARY KEY," +
                "    countMiSang BIGINT NOT NULL," +
                "    count1x2 BIGINT NOT NULL," +
                "    count2x4 BIGINT NOT NULL," +
                "    count4x6 BIGINT NOT NULL," +
                "    measured_weight REAL NOT NULL," +
                "    recorded_at TIMESTAMP NOT NULL DEFAULT NOW()" +
                ");";

            using (var conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private const long AddLockKey = 875001; 

        public void add(IInformation p_entity)
        {
            using (var conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@lockKey);", conn, transaction))
                    {
                        lockCmd.Parameters.AddWithValue("lockKey", AddLockKey);
                        lockCmd.ExecuteNonQuery();
                    }

                    string selectSql =
                        "SELECT countMiSang, count1x2, count2x4, count4x6, measured_weight" +
                        " FROM " + m_TABLE_NAME +
                        " ORDER BY recorded_at DESC LIMIT 1;";

                    using (var selectCmd = new NpgsqlCommand(selectSql, conn, transaction))
                    using (var reader = selectCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            p_entity.countMiSang += reader.GetInt64(0);
                            p_entity.count1x2 += reader.GetInt64(1);
                            p_entity.count2x4 += reader.GetInt64(2);
                            p_entity.count4x6 += reader.GetInt64(3);
                            p_entity.measuredWeight += reader.GetFloat(4);
                        }
                    }

                    string insertSql =
                        "INSERT INTO " + m_TABLE_NAME +
                        "    (countMiSang, count1x2, count2x4, count4x6, measured_weight, recorded_at)" +
                        " VALUES" +
                        "    (@miSang, @p1x2, @p2x4, @p4x6, @weight, NOW());";

                    using (var insertCmd = new NpgsqlCommand(insertSql, conn, transaction))
                    {
                        insertCmd.Parameters.AddWithValue("miSang", p_entity.countMiSang);
                        insertCmd.Parameters.AddWithValue("p1x2", p_entity.count1x2);
                        insertCmd.Parameters.AddWithValue("p2x4", p_entity.count2x4);
                        insertCmd.Parameters.AddWithValue("p4x6", p_entity.count4x6);
                        insertCmd.Parameters.AddWithValue("weight", p_entity.measuredWeight);
                        insertCmd.ExecuteNonQuery();
                    }

                    transaction.Commit();
                }
            }

            Console.WriteLine("[INFO] [REPOSITORY] [ADD] New record has been added");
        }

        public void update(IInformation p_entity)
        {
            //string sql =
            //    "UPDATE " + m_TABLE_NAME +
            //    " SET countMiSang = @miSang," +
            //    "     count1x2 = @p1x2," +
            //    "     count2x4 = @p2x4," +
            //    "     count4x6 = @p4x6," +
            //    "     measured_weight = @weight" +
            //    " WHERE id = (SELECT id FROM " + m_TABLE_NAME + " ORDER BY recorded_at DESC LIMIT 1);";

            //using (var conn = new NpgsqlConnection(_connectionString))
            //{
            //    conn.Open();
            //    using (var cmd = new NpgsqlCommand(sql, conn))
            //    {
            //        cmd.Parameters.AddWithValue("miSang", p_entity.countMiSang);
            //        cmd.Parameters.AddWithValue("p1x2", p_entity.count1x2);
            //        cmd.Parameters.AddWithValue("p2x4", p_entity.count2x4);
            //        cmd.Parameters.AddWithValue("p4x6", p_entity.count4x6);
            //        cmd.Parameters.AddWithValue("weight", p_entity.measuredWeight);
            //        cmd.ExecuteNonQuery();
            //    }
            //}
        }

        public IResultInformation getTotal()
        {
            string sql =
                "SELECT countMiSang, count1x2, count2x4, count4x6, measured_weight" +
                " FROM " + m_TABLE_NAME +
                " ORDER BY recorded_at DESC LIMIT 1;";

            using (var conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        Console.WriteLine("[WARN] [REPOSITORY] Zero Count. Returning default values.");
                        return CreateResult(0, 0, 0, 0, 0.0f);
                    }

                    Console.WriteLine("[INFO] [REPOSITORY] [TOTAL] Some records have been retrieved.");
                    return CreateResult(
                        reader.GetFloat(0),
                        reader.GetFloat(1),
                        reader.GetFloat(2),
                        reader.GetFloat(3),
                        reader.GetFloat(4));
                }
            }
        }

        public void reset()
        {
            string sql = "TRUNCATE TABLE " + m_TABLE_NAME + " RESTART IDENTITY;";

            using (var conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // p_startTime / p_endTime expected as "yyyy-MM-dd HH:mm:ss"
        public IResultInformation get(string p_startTime, string p_endTime)
        {
            DateTime startTime = DateTime.Parse(p_startTime, CultureInfo.InvariantCulture);
            DateTime endTime = DateTime.Parse(p_endTime, CultureInfo.InvariantCulture);

            string sql =
                "SELECT countMiSang, count1x2, count2x4, count4x6, measured_weight" +
                " FROM " + m_TABLE_NAME +
                " WHERE recorded_at >= @startTime AND recorded_at <= @endTime" +
                " ORDER BY recorded_at ASC;";

            var filteredRecords = new List<IInformation>();

            using (var conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("startTime", startTime);
                    cmd.Parameters.AddWithValue("endTime", endTime);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            filteredRecords.Add(new IInformation
                            {
                                countMiSang = reader.GetInt64(0),
                                count1x2 = reader.GetInt64(1),
                                count2x4 = reader.GetInt64(2),
                                count4x6 = reader.GetInt64(3),
                                measuredWeight = reader.GetFloat(4)
                            });
                        }
                    }
                }
            }

            if (filteredRecords.Count == 0)
            {
                Console.WriteLine("[WARN] [REPOSITORY] Zero Count for the specified time range. Returning default values.");
                return CreateResult(0, 0, 0, 0, 0.0f);
            }

            IInformation latestRecord = filteredRecords[filteredRecords.Count - 1];
            IInformation oldestRecord = filteredRecords[0];

            Console.WriteLine("[INFO] [REPOSITORY] [GET] Some records have been retrieved for the specified time range.");

            return CreateResult(
                latestRecord.countMiSang - oldestRecord.countMiSang,
                latestRecord.count1x2 - oldestRecord.count1x2,
                latestRecord.count2x4 - oldestRecord.count2x4,
                latestRecord.count4x6 - oldestRecord.count4x6,
                latestRecord.measuredWeight - oldestRecord.measuredWeight);
        }

        private static IResultInformation CreateResult(
            float p_miSang,
            float p_1x2,
            float p_2x4,
            float p_4x6,
            float p_weight)
        {
            var result = new IResultInformation();
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

        public string getStartTime()
        {
            return GetSingleTimeValue("SELECT MIN(recorded_at) FROM " + m_TABLE_NAME + ";");
        }

        public string getLatestTime()
        {
            return GetSingleTimeValue("SELECT MAX(recorded_at) FROM " + m_TABLE_NAME + ";");
        }

        private string GetSingleTimeValue(string p_sql)
        {
            using (var conn = new NpgsqlConnection(m_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand(p_sql, conn))
                {
                    object value = cmd.ExecuteScalar();
                    if (value == null || value == DBNull.Value)
                        return string.Empty;

                    return ((DateTime)value).ToString("yyyy-MM-dd__hh-mm-ss");
                }
            }
        }

    }
}
