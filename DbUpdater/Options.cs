using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using System.IO;

namespace DbUpdater
{
	public class Options
	{
		[Option("tablePrefix", Required = true, HelpText = "The table prefix to be applied to the schema change log table.")]
		public string TablePrefix { get; set; }

		[Option('p', "path", Required = true, HelpText = "The path relative to the execute of DbUpdator of the sql scripts to run.")]
		public string SqlPath { get; set; }

		[Option("dbname", Required = true, HelpText = "The database the connection string should be opened too.")]
		public string DbName { get; set; }

		[Option("server", Required = true, HelpText = "The servername or alias.")]
		public string Server { get; set; }

		[Option("username", HelpText = "The username for sql.  If omited will use windows credentials")]
		public string Username { get; set; }

		[Option("password", HelpText = "The password for sql.  If omited will use windows credentials")]
		public string Password { get; set; }

		[Option("reset", Required = false, DefaultValue = false, HelpText = "Will delete the schema table and start over again.  The same as 'execute everything in the folder.'")]
		public bool ResetSchemaTable { get; set; }

		[Option("tee", Required = false, DefaultValue = false, HelpText = "Whether the output will be sent to the console as well as any files.")]
		public bool Tee { get; set; }

		[Option("logFile", Required = false, HelpText = "If provided, the output will be recorded to this text file.")]
		public string LogFile { get; set; }

		[Option("backupPath", Required = false, DefaultValue = null, HelpText = "Will back up the database to the path specified.")]
		public string BackupPath { get; set; }

		[Option("environment", Required = false, DefaultValue = null, HelpText = "The environment that should be filtered.  Use xml nodes inside the .sql files to selectively run.")]
		public string Environment { get; set; }

		[Option("debugger", Required = false, DefaultValue = false, HelpText = "If you would like the application to pause to give chance to debugger to attach.")]
		public bool Debugger { get; set; }

		public string GetSqlPath()
		{
			if (SqlPath.Contains(":"))
				return SqlPath;

			var info = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SqlPath));

			return info.FullName;
		}

		/// <summary>
		/// Gets the root path for the backup operations.  Will ensure the path ends with a \.
		/// </summary>
		public string GetFullBackupPath()
		{
			var path = BackupPath;

			if (!BackupPath.EndsWith("\\"))
				path += "\\";

			return path;
		}

		/// <summary>
		/// The name of the finalized back up file.  Will be used to delete on next backup run.
		/// </summary>
		public string GetFullBackupFilePath()
		{
			 return GetFullBackupPath() + DbName + "_dbUpdater.bak";
		}

		/// <summary>
		/// The name of any current backup attempts.
		/// </summary>
		public string GetFullAttemptedBackupFilePath()
		{
			return GetFullBackupPath() + DbName + "_dbUpdater_attempt.bak";
		}

		/// <summary>
		/// The connection string based on the command line arguments.
		/// </summary>
		public string GetConnectionString()
		{

			if (String.IsNullOrEmpty(Password) && String.IsNullOrEmpty(Username))
				return String.Format("Integrated Security=SSPI;Initial Catalog={0};Data Source={1}", DbName, Server);
			else
				return String.Format("Initial Catalog={0};Data Source={1};User Id={2};Password={3}", DbName, Server, Username, Password);
		}

		public string GetFullLogFilePath()
		{
			if (String.IsNullOrEmpty(LogFile))
				return null;

			var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LogFile);
			var fullDirectory = Path.GetDirectoryName(fullPath);

			if (!Directory.Exists(fullDirectory))
				Directory.CreateDirectory(fullDirectory);

			var info = new DirectoryInfo(fullPath);

			return info.FullName;
		}
	}
}