using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Transactions;
using System.Collections.Generic;
using DbUpdater;
using Newtonsoft.Json;
using System.Xml;

namespace DbUpdater
{
	public class Program
	{

		//eg: DbUpdater /p:\sql\patches /env:DEV /dbName:devdb
		static void Main(string[] args)
		{
			var options = new Options();

			if (!CommandLine.Parser.Default.ParseArgumentsStrict(args, options))
			{
				return;
			}

			if (options.Debugger)
			{
				if (!System.Diagnostics.Debugger.IsAttached)
					System.Diagnostics.Debugger.Break();
			}

			AssertSqlInjection(options);

			CheckForCreateDatabase(options);
			CheckForCreateDatabaseIfNotExists(options);
			AssertChangeLogTableExists(options);
			CheckForBackup(options);
			CheckForLogFile(options);

			try
			{
				ProcessFiles(options);

				if (!String.IsNullOrEmpty(options.GetFullLogFilePath()))
				{
					Console.WriteLine("Log file saved at: " + options.GetFullLogFilePath());
				}

				Environment.Exit(0);
			}
			catch (Exception ex)
			{
				WriteLine(options, ex.ToString());
				Environment.Exit(1);
			}
		}

		private static void AssertSqlInjection(Options options)
		{
			if (options.DbName.Contains("'"))
				throw new NotSupportedException("Can not have ' in dbname");

			if (options.TablePrefix.Contains("'"))
				throw new NotSupportedException("Can not have ' in TablePrefix");
		}

		private static void CheckForCreateDatabaseIfNotExists(Options options)
		{
			if (!options.CreateDatabaseIfNotExist)
				return;

			using (var connection = new SqlConnection(options.GetConnectionString(forMaster: true)))
			{
				connection.Open();

				ExecuteQuery(connection, null, Sql.GET_CREATE_DATABASE_IF_NOT_EXISTS(options));
			}
		}

		private static void CheckForCreateDatabase(Options options)
		{
			if (!options.CreateDatabase)
				return;

			using (var connection = new SqlConnection(options.GetConnectionString(forMaster: true)))
			{
				connection.Open();

				ExecuteQuery(connection, null, Sql.GET_CREATE_DATABASE(options));
			}
		}

		private static void CheckForLogFile(Options options)
		{
			var logFile = options.GetFullLogFilePath();
			if (String.IsNullOrEmpty(logFile))
				return;

			if (File.Exists(logFile))
				File.Delete(logFile);

			WriteLine(options, "Log file ready to receive at location: " + logFile);
		}

		private static void AssertChangeLogTableExists(Options options)
		{
			using (var connection = new SqlConnection(options.GetConnectionString()))
			{
				connection.Open();
				
				using (var transaction = connection.BeginTransaction())
				{
					if (options.ResetSchemaTable)
					{
						ExecuteQuery(connection, transaction, Sql.GET_DROP_SCHEMA_TABLE(options));
					}

					ExecuteQuery(connection, transaction, Sql.GET_CREATE_SCHEMA_TABLE(options));
					transaction.Commit();
				}
			}
		}

		private static void CheckForBackup(Options options)
		{
			if (String.IsNullOrEmpty(options.BackupPath))
				return;

			WriteLine(options, "Backing up database: " + options.DbName + " to path: " + options.GetFullAttemptedBackupFilePath());

			using (var connection = new SqlConnection(options.GetConnectionString()))
			{
				connection.Open();

				ExecuteQuery(connection, null, Sql.GET_BACKUP_DATABASE_COMMAND(options));
			}

			WriteLine(options, "Backup successful at : " + options.GetFullBackupFilePath() + " or here: " + options.GetFullAttemptedBackupFilePath() + " can't really tell from the server executing dos commands.");
		}

		private static void ProcessFiles(Options options)
		{
			WriteLine(options, "Starting to process path: " + options.GetSqlPath() + ".");
			var files = Directory.GetFiles(options.GetSqlPath(), "*.sql")
				.Select(x => new FileInfo(x))
				.OrderBy(x=> GetValue(x, 0, "Major"))
				.ThenBy(x=> GetValue(x, 1, "Minor"))
				.ThenBy(x=> GetValue(x, 2, "Minor"));

			if (!files.Any())
			{
				WriteLine(options, "No files found so doing nothing.");
				return;
			}

			foreach (var file in files)
			{
				WriteLine(options, "Checking patch: " + file.Name);

				using (var connection = new SqlConnection(options.GetConnectionString()))
				{
					connection.Open();

					var dateApplied = IsAlreadyUpdated(connection, file, options);
					if (dateApplied.HasValue)
					{
						WriteLine(options, "  Patch already applied on: " + dateApplied.Value.ToString());
						continue;
					}

					

					using (var transaction = connection.BeginTransaction())
					{
						WriteLine(options, "Executing Patch: " + file.Name + " on " + DateTime.Now.ToString());
						var sqlExecuted = new StringBuilder();
						var rowsAffected = 0;
						var max = (int?)null;
						var min = (int?)null;

						foreach (var sqlPart in SplitSqlByGo(file, options, out max, out min))
						{
							if (String.IsNullOrEmpty(sqlPart))
								continue;
							var rows = ExecuteQuery(connection, transaction, sqlPart);
							if (rows != -1)
								rowsAffected += rows;
							sqlExecuted.AppendLine(sqlPart);
						}

						if(max != null)
						{
							if (rowsAffected > max) throw new ValidationException(string.Format("Number of records affected ({0}) cannot be more than Max Records({1}) defined.", rowsAffected, max));
						}
						if (min != null)
						{
							if (rowsAffected < min) throw new ValidationException(string.Format("Number of records affected ({0}) cannot be less than Min Records({1}) defined.", rowsAffected, min));
						}

						var sqlParams = new SqlParameter[] { 
								 new SqlParameter("@MajorReleaseID", GetMajor(file))
								,new SqlParameter("@MinorReleaseID", GetMinor(file))
								,new SqlParameter("@PatchID", GetPatch(file))
								,new SqlParameter("@RowsAffected", rowsAffected)
								,new SqlParameter("@FileName", file.Name)
								,new SqlParameter("@Script", sqlExecuted.ToString())
						};

						ExecuteQuery(connection, transaction, Sql.GET_INSERT_PATCH(options), sqlParams);

						transaction.Commit();
						WriteLine(options, "  " + rowsAffected.ToString("g") + " rows affected on " + DateTime.Now.ToString());
					};
				}
			}
			WriteLine(options, "Process finished.");
		}

		private static List<string> SplitSqlByGo(FileInfo fileInfo, Options options, out int? max, out int? min)
		{
			max = null;
			min = null;
			var list = new List<string>();
			var part = new StringBuilder();

			var lines = File.ReadAllLines(fileInfo.FullName);
			if (lines.Any())
			{
				var line = lines[0];
				if ((line.StartsWith("{") || line.StartsWith("--{") || line.StartsWith("-- {")) && line.EndsWith("}"))
				{
					if (line.StartsWith("--"))
						line = line.Substring(2).Trim();
					var maxMin = JsonConvert.DeserializeObject<MaxMin>(line);
					if (!maxMin.MaxRecords.HasValue && maxMin.MinRecords == null)
						throw new ValidationException("MinRecords and/or MaxRecords not specified.");

					max = maxMin.MaxRecords;
					min = maxMin.MinRecords;
					lines = lines.Skip(1).ToArray();
				}
			}

			lines = FilterForEnvironmentFlags(lines, options);

			foreach (var line in lines)
			{
				if (line.StartsWith("GO", StringComparison.OrdinalIgnoreCase))
				{
					list.Add(part.ToString());
					part = new StringBuilder();
					continue;
				}
				part.AppendLine(line);
			}
			list.Add(part.ToString());
			
			return list;
		}

		/// <summary>
		/// Method that attempts an xml thing to only return the lines based on the environment.
		/// </summary>
		public static string[] FilterForEnvironmentFlags(string[] lines, Options options)
		{
			const string newline = "\r\n";
			//blank
			if (!lines.Any())
				return lines;

			if (String.IsNullOrEmpty(options.Environment))
				return lines;

			if (!lines.Any(x => String.Equals(x, "<environment>", StringComparison.OrdinalIgnoreCase)))
				return lines;
			
			var fullBody = String.Join(newline, lines);

			var document = (XmlDocument)null;
			try
			{
				document = new XmlDocument();
				document.LoadXml(fullBody);
			}
			catch
			{
				Console.WriteLine("You are using the magic string '<environment>'.  This can only be used with xml parsing and environment checks");
				throw;
			}

			if (document.ChildNodes.Count != 1)
				throw new NotImplementedException(); //i think this is impossible when LoadXml is called, but just for sanity

			if (!String.Equals(document.ChildNodes[0].Name, "environment", StringComparison.OrdinalIgnoreCase))
				throw new EnvironmentRootNodeMissingException();

			var filteredLines = new List<string>();

			foreach (XmlNode childNode in document.ChildNodes[0].ChildNodes)
			{
				if (childNode.InnerText != childNode.InnerXml)
					throw new OnlyOneLevelOfXmlNodeException(childNode.Name);

				if (String.Equals(options.Environment, childNode.Name, StringComparison.OrdinalIgnoreCase))
				{
					lines = childNode.InnerText.Split(new[] { newline }, StringSplitOptions.None);
					if (!lines.Any())
						return lines;
					if (lines[0] == String.Empty)
						lines = lines.Skip(1).ToArray();
					if (!lines.Any())
						return lines;
					if (lines[lines.Length - 1] == String.Empty)
						lines = lines.Take(lines.Length - 1).ToArray();

					filteredLines.AddRange(lines);
				}
					
			}

			return filteredLines.ToArray();
		}

		private static DateTime? IsAlreadyUpdated(SqlConnection connection, FileInfo file, Options options)
		{
			using (var cmd = new SqlCommand(Sql.GET_GET_SCHEMA_RUN_DATE(options), connection))
			{
				var sqlParams = new SqlParameter[] { 
					new SqlParameter("@MajorReleaseId", GetMajor(file)),
					new SqlParameter("@MinorReleaseId", GetMinor(file)),
					new SqlParameter("@PatchID", GetPatch(file))
					};

				cmd.Parameters.AddRange(sqlParams);
				return (DateTime?)cmd.ExecuteScalar();
			}
		}

		private static int ExecuteQuery(SqlConnection connection, SqlTransaction transaction, string sql, SqlParameter[] sqlParams = null)
		{
			using (var cmd = new SqlCommand(sql, connection, transaction))
			{
				cmd.CommandTimeout = 60 * 20;
				if (sqlParams != null) cmd.Parameters.AddRange(sqlParams);
				return cmd.ExecuteNonQuery();
			}
		}

		public static int GetMajor(FileInfo fileInfo)
		{
			return GetValue(fileInfo, 0, "Major");
		}

		public static int GetMinor(FileInfo fileInfo)
		{
			return GetValue(fileInfo, 1, "Minor");
		}

		public static int GetPatch(FileInfo fileInfo)
		{
			return GetValue(fileInfo, 2, "Patch");
		}

		private static int GetValue(FileInfo fileInfo, int position, string partName)
		{
			var array = fileInfo.Name.Split('.');

			var value = 0;

			if (array.Length < position + 1 || !Int32.TryParse(array[position], out value))
				throw new ApplicationException("File name: " + fileInfo.Name + " Could not determine the " + partName + " number.  .sql files must be in the format X.Y.Z.{maybe more stuff}.sql");

			return value;

		}

		private static void WriteLine(Options options, string data)
		{
			var logFile = options.GetFullLogFilePath();
			if (String.IsNullOrEmpty(logFile))
			{
				Console.WriteLine(data);
				return;
			}

			File.AppendAllText(logFile, data + "\r\n");
			if (options.Tee)
				Console.WriteLine(data);
		}
	}
}