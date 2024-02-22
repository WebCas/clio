using System;
using Npgsql;

namespace Clio.Common.db;

using Terrasoft.Core.Tasks;

public class Postgres
{

	private readonly string _connectionString;

	public Postgres(int port, string username, string password): this("127.0.0.1", port,username, password) { }
	public Postgres(string host, int port, string username, string password) {
		_connectionString = $"Host={host};Port={port};Username={username};Password={password};Database=postgres";
	}
	
	public bool CreateDbFromTemplate (string templateName, string dbName) {
		try {
			using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(_connectionString);
			using NpgsqlConnection cnn = dataSource.OpenConnection();
			
			string killSqlConnections = @$"
			SELECT pg_terminate_backend(pg_stat_activity.pid)
			FROM pg_stat_activity
			WHERE pg_stat_activity.datname = '{templateName}'
			";
			using NpgsqlCommand killConnectionCmd = dataSource.CreateCommand(killSqlConnections);
			killConnectionCmd.ExecuteNonQuery();
			
			using NpgsqlCommand cmd = dataSource.CreateCommand($"CREATE DATABASE \"{dbName}\" TEMPLATE=\"{templateName}\" ENCODING UTF8 CONNECTION LIMIT -1");
			cmd.ExecuteNonQuery();
			cnn.Close();
			return true;
		} catch (Exception e)  when (e is PostgresException pe){
			Console.WriteLine($"[{pe.Severity}] - {pe.MessageText}");
			return false;
		}
		catch(Exception e) when (e is NpgsqlException ne) {
			Console.WriteLine(ne.Message);
			return false;
		}
		catch(Exception e) {
			Console.WriteLine(e.Message);
			return false;
		}
	}
	
	public bool CreateDb (string dbName) {
		
		try {
			//using NpgsqlConnection cnn = dataSource.OpenConnection();

			System.Threading.Tasks.Task.Run(async ()=> {
				using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(_connectionString);
				var cnn = await dataSource.OpenConnectionAsync();
				using NpgsqlCommand cmd = dataSource.CreateCommand($"CREATE DATABASE \"{dbName}\" ENCODING UTF8 CONNECTION LIMIT -1");
				//cmd.ExecuteNonQuery();
				await cmd.ExecuteNonQueryAsync();
				await cnn.CloseAsync();
			}).Wait();
			
			return true;
		} catch (Exception e)  when (e is PostgresException pe){
			Console.WriteLine($"[{pe.Severity}] - {pe.MessageText}");
			return false;
		}
		catch(Exception e) when (e is NpgsqlException ne) {
			Console.WriteLine(ne.Message);
			return false;
		}
		catch(Exception e) {
			Console.WriteLine(e.Message);
			return false;
		}
	}
	
	public bool SetDatabaseAsTemplate( string dbName) {
		try {
			
			using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(_connectionString);
			using NpgsqlConnection cnn = dataSource.OpenConnection();
			using NpgsqlCommand cmd = dataSource.CreateCommand($"UPDATE pg_database SET datistemplate='true' WHERE datname='{dbName}'");
			cmd.ExecuteNonQuery();
			cnn.Close();
			return true;
		} catch (Exception e)  when (e is PostgresException pe){
			Console.WriteLine($"[{pe.Severity}] - {pe.MessageText}");
			return false;
		}
		catch(Exception e) when (e is NpgsqlException ne) {
			Console.WriteLine(ne.Message);
			return false;
		}
		catch(Exception e) {
			Console.WriteLine(e.Message);
			return false;
		}
	}
	
	public bool CheckTemplateExists (string templateName) {
		try {
			string sqlText = @$"
				SELECT COUNT(datname) 
				FROM pg_catalog.pg_database d 
				WHERE datistemplate = true AND datName = '{templateName}';
			";
			
			using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(_connectionString);
			using NpgsqlConnection cnn = dataSource.OpenConnection();
			using NpgsqlCommand cmd = dataSource.CreateCommand(sqlText);
			var result = cmd.ExecuteScalar();
			cnn.Close();
			
			return result is long r && r == 1;
			
			return true;
		} catch (Exception e)  when (e is PostgresException pe){
			Console.WriteLine($"[{pe.Severity}] - {pe.MessageText}");
			return false;
		}
		catch(Exception e) when (e is NpgsqlException ne) {
			Console.WriteLine(ne.Message);
			return false;
		}
		catch(Exception e) {
			Console.WriteLine(e.Message);
			return false;
		}
	}
}