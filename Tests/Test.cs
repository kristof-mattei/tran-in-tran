namespace Tests
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Transactions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using IsolationLevel = System.Transactions.IsolationLevel;

    [TestClass]
    public class Test
    {
        const string ConnectionString = "Server=.;Integrated Security=SSPI";

        [TestInitialize]
        public void Initialize()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString))
            {
                sqlConnection.Open();

                // create temp table
                using (var sqlCommand = new SqlCommand("CREATE TABLE [tempdb..#FooBar] ([Id] INT NOT NULL IDENTITY PRIMARY KEY, [Word] NVARCHAR (10) NOT NULL)", sqlConnection))
                {
                    sqlCommand.ExecuteNonQuery();
                }
            }
        }

        [TestCleanup]
        public void Cleanup_on_aisle_4()
        {
            using (var sqlConnection = new SqlConnection(ConnectionString))
            {
                sqlConnection.Open();

                // create temp table
                using (var sqlCommand = new SqlCommand("DROP TABLE [tempdb..#FooBar]", sqlConnection))
                {
                    sqlCommand.ExecuteNonQuery();
                }
            }
        }

        private static TransactionScope GetNewTransactionScope()
        {
            return new TransactionScope(TransactionScopeOption.Required,
                                        new TransactionOptions()
                                        {
                                            IsolationLevel = IsolationLevel.ReadCommitted
                                        });
        }

        [TestMethod]
        public void Test_Transaction_Scope_In_Transaction_Scope()
        {
            const string valueToInsert = "Foo";

            using (var outerTransactionScope = GetNewTransactionScope())
            {
                using (var innerTransactionScope = GetNewTransactionScope())
                {
                    using (var sqlConnection = new SqlConnection(ConnectionString))
                    {
                        sqlConnection.Open();

                        // insert into table
                        using (var sqlCommand = new SqlCommand("INSERT INTO [tempdb..#FooBar] ([Word]) VALUES (@word)", sqlConnection))
                        {
                            sqlCommand.Parameters.Add(new SqlParameter()
                                                      {
                                                          ParameterName = "word",
                                                          SqlDbType = SqlDbType.NVarChar,
                                                          Value = valueToInsert,
                                                      });

                            sqlCommand.ExecuteNonQuery();
                        }
                    }
                    innerTransactionScope.Complete();
                }
            }

            // notice the outerTransactionScope is not completed, we've ended the scope, so it should be rolled back.
            // however since the inner one is completed, it should actually have written that to the database

            using (var sqlConnection = new SqlConnection(ConnectionString))
            {
                sqlConnection.Open();

                using (var sqlCommand = new SqlCommand("SELECT [Id], [Word] FROM [tempdb..#FooBar] WHERE [Word] = @word", sqlConnection))
                {
                    sqlCommand.Parameters.Add(new SqlParameter()
                                              {
                                                  ParameterName = "Word",
                                                  SqlDbType = SqlDbType.NVarChar,
                                                  Value = valueToInsert
                                              });

                    using (var sqlReader = sqlCommand.ExecuteReader())
                    {
                        if (!sqlReader.HasRows)
                        {
                            Console.WriteLine("Unexpected: Couldn't find any rows, this is weird");
                        }
                        else
                        {
                            while (sqlReader.Read())
                            {
                                Console.WriteLine($"Found row with Id = {sqlReader.GetInt32(0)}, Word = {sqlReader.GetString(1)}");
                            }
                        }
                    }
                }
            }
        }
    }
}
