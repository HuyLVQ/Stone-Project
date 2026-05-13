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

    public class SQLServerRepository<T> : IRepository<T> where T : IInformation
    {
        /// ----- Declare members ----- ///
        private static SQLServerRepository<T> instance;
        private string connectionString { get; set; }
        private readonly object _lock = new object();



        /// ----- Declare methods ----- ///
        /// 

        private SQLServerRepository()
        {
            var configBuilder = new ConfigurationBuilder()
                       .SetBasePath(Directory.GetCurrentDirectory())
                       .AddJsonFile("appconfig.json");
            var configurationRoot = configBuilder.Build();
            connectionString = configurationRoot["db:base_connection"];
        }

        private SqlConnection connect()
        {
            var connection = new SqlConnection(this.connectionString);

            connection.StatisticsEnabled = true;
            connection.FireInfoMessageEventOnUserErrors = true; 

            connection.Open();
            return connection;
        }

        private SqlCommand openCommand(SqlConnection connection)
        {
            var command = new SqlCommand();
            command.Connection = connection;
            return command;
        }

        public static SQLServerRepository<T> getIntance()
        {
            if (instance == null)
            {
                instance = new SQLServerRepository<T>();
            }
            return instance;
        }

        void IRepository<T>.add (T entity)
        {
            lock(_lock) 
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
                        command.Parameters.AddWithValue("@delta_misang", entity.deltaPerctMiSang);
                        command.Parameters.AddWithValue("@delta_1_2", entity.deltaPerct1x2);
                        command.Parameters.AddWithValue("@delta_2_4", entity.deltaPerct2x4);
                        command.Parameters.AddWithValue("@delta_4_6", entity.deltaPerct4x6);
                        command.Parameters.AddWithValue("@total_weight", entity.measuredWeight);

                        Console.WriteLine("[INFO] [REPOSITORY] [ADD] New record has been added");
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        void IRepository<T>.update(T entity)
        {

        }


        T IRepository<T>.getTotal()
        {
            lock(_lock)
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

                        T result = Activator.CreateInstance<T>();
                        using (var read = command.ExecuteReader())
                        {
                            if (count == 0)
                            {
                                result.deltaPerctMiSang = 0.0f;
                                result.deltaPerct1x2 = 0.0f;
                                result.deltaPerct2x4 = 0.0f;
                                result.deltaPerct4x6 = 0.0f;
                                result.measuredWeight = 0.0f;

                                Console.WriteLine("[WARN] [REPOSITORY] Zero Count. Returning default values.");
                            } else
                            {
                                if (read.Read())
                                {
                                    result.deltaPerctMiSang = read.GetFloat(read.GetOrdinal("perct_misang")) / count;
                                    result.deltaPerct1x2 = read.GetFloat(read.GetOrdinal("perct_1_2")) / count;
                                    result.deltaPerct2x4 = read.GetFloat(read.GetOrdinal("perct_2_4")) / count;
                                    result.deltaPerct4x6 = read.GetFloat(read.GetOrdinal("perct_4_6")) / count;
                                    result.measuredWeight = read.GetFloat(read.GetOrdinal("total_weight")) / count;
                                    Console.WriteLine("[INFO] [REPOSITORY] [TOTAL] Some records have been retrieved.");
                                }
                                else
                                {
                                    result.deltaPerctMiSang = 0.0f;
                                    result.deltaPerct1x2 = 0.0f;
                                    result.deltaPerct2x4 = 0.0f;
                                    result.deltaPerct4x6 = 0.0f;
                                    result.measuredWeight = 0.0f;

                                    Console.WriteLine("[WARN] [REPOSITORY] No records found in the database. Returning default values.");
                                }
                            }
                        }

                        return result;
                    }
                }
            }
            
        }

        void IRepository<T>.reset()
        {
            lock(_lock)
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


        T IRepository<T>.get(string start_time, string end_time)
        {
            return null;
        }
    }
}
