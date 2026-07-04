using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data;
using System.Data.SqlClient;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;  
using System.IO;
using System.Runtime.CompilerServices;
using Stone_Application.Event;
using DocumentFormat.OpenXml.EMMA;

namespace Stone_Application.Repository
{

    public class SQLServerRepository<TInformation, TResultInformation> : IRepository<TInformation, TResultInformation>
        where TInformation : IInformation
        where TResultInformation : IResultInformation, new()
    {
        /// ----- Declare members ----- ///
        private static SQLServerRepository<TInformation, TResultInformation> s_instance;
        private string m_connectionString;
        private readonly object m_lock = new object();



        /// ----- Declare methods ----- ///
        /// 

        private SQLServerRepository()
        {
            var configBuilder = new ConfigurationBuilder()
                       .SetBasePath(Directory.GetCurrentDirectory())
                       .AddJsonFile("appconfig.json");
            var configurationRoot = configBuilder.Build();
            m_connectionString = configurationRoot["db:base_connection"];
        }

        private SqlConnection connect()
        {
            var connection = new SqlConnection(this.m_connectionString);

            connection.StatisticsEnabled = true;
            connection.FireInfoMessageEventOnUserErrors = true; 

            connection.Open();
            return connection;
        }

        private SqlCommand openCommand(SqlConnection p_connection)
        {
            var command = new SqlCommand();
            command.Connection = p_connection;
            return command;
        }

        public static SQLServerRepository<TInformation, TResultInformation> getIntance()
        {
            if (s_instance == null)
            {
                s_instance = new SQLServerRepository<TInformation, TResultInformation>();
            }
            return s_instance;
        }

        void IRepository<TInformation, TResultInformation>.add (TInformation p_entity)
        {
            lock (m_lock)
            {
                using (var connection = this.connect())
                {
                    using (var command = this.openCommand(connection))
                    {
                        string query = @"
                                        INSERT INTO RECORD_STORAGE (current_time, perct_misang, perct_1_2, perct_2_4, perct_4_6, total_weight)
                                        SELECT TOP 1 
                                            @current_time, 
                                            perct_misang + @delta_misang, 
                                            perct_1_2 + @delta_1_2, 
                                            perct_2_4 + @delta_2_4, 
                                            perct_4_6 + @delta_4_6,
                                            total_weight + @total_weight
                                        FROM RECORD_STORAGE 
                                        ORDER BY current_time DESC;

                                        IF @@ROWCOUNT = 0
                                        BEGIN
                                            INSERT INTO RECORD_STORAGE (current_time, perct_misang, perct_1_2, perct_2_4, perct_4_6, total_weight)
                                            VALUES (@current_time, @delta_misang, @delta_1_2, @delta_2_4, @delta_4_6, @total_weight);
                                        END";

                        command.CommandText = query;
                        command.Parameters.AddWithValue("@current_time", DateTime.Now);
                        command.Parameters.AddWithValue("@delta_misang", p_entity.deltaPerctMiSang);
                        command.Parameters.AddWithValue("@delta_1_2", p_entity.deltaPerct1x2);
                        command.Parameters.AddWithValue("@delta_2_4", p_entity.deltaPerct2x4);
                        command.Parameters.AddWithValue("@delta_4_6", p_entity.deltaPerct4x6);
                        command.Parameters.AddWithValue("@total_weight", p_entity.measuredWeight);

                        Console.WriteLine("[INFO] [REPOSITORY] [ADD] New record has been added");
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        void IRepository<TInformation, TResultInformation>.update(TInformation p_entity)
        {

        }


        TResultInformation IRepository<TInformation, TResultInformation>.getTotal()
        {
            lock (m_lock)
            {
                using (var connection = this.connect())
                {
                    using (var command = this.openCommand(connection))
                    {

                        string getCountQuery = @"SELECT COUNT(*) 
                                                   FROM RECORD_STORAGE";

                        command.CommandText = getCountQuery;
                        int count = Convert.ToInt32(command.ExecuteScalar());



                        string getRecordQuery = @"SELECT TOP 1 * 
                                                    FROM RECORD_STORAGE 
                                                    ORDER BY current_time DESC";
                        command.CommandText = getRecordQuery;

                        TResultInformation result = new TResultInformation();
                        using (var read = command.ExecuteReader())
                        {
                            if (count == 0)
                            {
                                result.resultPerctMiSang = 0.0f;
                                result.resultPerct1x2 = 0.0f;
                                result.resultPerct2x4 = 0.0f;
                                result.resultPerct4x6 = 0.0f;
                                result.resultWeight = 0.0f;

                                Console.WriteLine("[WARN] [REPOSITORY] Zero Count. Returning default values.");
                            } else
                            {
                                if (read.Read())
                                {
                                    result.resultPerctMiSang = read.GetFloat(read.GetOrdinal("perct_misang")) / count;
                                    result.resultPerct1x2 = read.GetFloat(read.GetOrdinal("perct_1_2")) / count;
                                    result.resultPerct2x4 = read.GetFloat(read.GetOrdinal("perct_2_4")) / count;
                                    result.resultPerct4x6 = read.GetFloat(read.GetOrdinal("perct_4_6")) / count;
                                    result.resultWeight = read.GetFloat(read.GetOrdinal("total_weight")) / count;
                                    Console.WriteLine("[INFO] [REPOSITORY] [TOTAL] Some records have been retrieved.");
                                }
                                else
                                {
                                    result.resultPerctMiSang = 0.0f;
                                    result.resultPerct1x2 = 0.0f;
                                    result.resultPerct2x4 = 0.0f;
                                    result.resultPerct4x6 = 0.0f;
                                    result.resultWeight = 0.0f;

                                    Console.WriteLine("[WARN] [REPOSITORY] No records found in the database. Returning default values.");
                                }
                            }
                        }

                        return result;
                    }
                }
            }
            
        }

        void IRepository<TInformation, TResultInformation>.reset()
        {
            lock (m_lock)
            {
                using (var connection = this.connect())
                {
                    using (var command = this.openCommand(connection))
                    {
                        string query = @"DELETE FROM RECORD_STORAGE";
                        command.CommandText = query;
                        command.ExecuteNonQuery();
                    }
                }
            }
        }


        TResultInformation IRepository<TInformation, TResultInformation>.get(string p_startTime, string p_endTime)
        {
            return null;
        }

        string IRepository<TInformation, TResultInformation>.getStartTime()
        {
            return null;
        }

        string IRepository<TInformation, TResultInformation>.getLatestTime()
        {
            return null;
        }
    }
}
