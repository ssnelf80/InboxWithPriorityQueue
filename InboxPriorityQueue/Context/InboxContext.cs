using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace InboxPriorityQueue.Context;

public class InboxContext
{
    private readonly string _connectionString;
    
    public InboxContext(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("SqlConnection")!;
        if (string.IsNullOrEmpty(_connectionString))
            throw new NullReferenceException("Connection string is null or empty");
    }
    public IDbConnection OpenConnection()
    {
        var connection =  new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}