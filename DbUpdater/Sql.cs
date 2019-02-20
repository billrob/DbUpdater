using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbUpdater
{
	public static class Sql
	{
		public static string GET_CREATE_SCHEMA_TABLE(Options options)
		{
			return String.Format(@"
IF OBJECT_ID (N'SchemaChangeLog_{0}', N'U') IS NULL 
BEGIN
	CREATE TABLE [dbo].[SchemaChangeLog_{0}](
		[SchemaChangeLogId] [int] IDENTITY(1,1) NOT NULL,
		[Date] [datetime] NOT NULL,
		[MajorReleaseId] [int] NOT NULL,
		[MinorReleaseId] [int] NOT NULL,
		[PatchId] [int] NOT NULL,
		[RowsAffected] [int] NOT NULL,
		[FileName] [varchar](max) NOT NULL,
		[ExecutedSQL] [varchar](max) NOT NULL
	) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
END", options.TablePrefix);

		}

		public static string GET_CREATE_DATABASE(Options options)
		{
			return $@"
IF EXISTS(SELECT 1 from sys.databases WHERE Name = '{options.DbName}')
BEGIN
	ALTER DATABASE [{options.DbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE
	DROP DATABASE [{options.DbName}]
END


CREATE DATABASE [{options.DbName}]
";
		}

		public static string GET_CREATE_DATABASE_IF_NOT_EXISTS(Options options)
		{
			return $@"
IF NOT EXISTS(SELECT 1 from sys.databases WHERE Name = '{options.DbName}')
BEGIN
	CREATE DATABASE [{options.DbName}]
END


";
		}
		

		public static string GET_INSERT_PATCH(Options options)
		{
			return String.Format(@"			
INSERT INTO SchemaChangeLog_{0}
	([Date]
	,[MajorReleaseID]
	,[MinorReleaseID]
	,[PatchID]
	,[ExecutedSQL]
	,[FileName]
	,[RowsAffected])
VALUES
	(GETDATE()
	,@MajorReleaseID
	,@MinorReleaseID
	,@PatchID
	,@Script
	,@FileName
	,@RowsAffected
	)", options.TablePrefix);

		}
		public static string GET_DROP_SCHEMA_TABLE(Options options)
		{
			return String.Format(@"
IF OBJECT_ID (N'SchemaChangeLog_{0}', N'U') IS NOT NULL 
BEGIN
	DROP TABLE SchemaChangeLog_{0}
END", options.TablePrefix);

		}

		public static string GET_GET_SCHEMA_RUN_DATE(Options options)
		{
			return String.Format(@"
SELECT Date 
FROM SchemaChangeLog_{0}
WHERE 
	MajorReleaseId = @MajorReleaseId and 
	MinorReleaseId = @MinorReleaseId and 
	PatchId = @PatchId
				", options.TablePrefix);
		}

		public static string GET_BACKUP_DATABASE_COMMAND(Options options)
		{
			return String.Format(@"
EXEC xp_cmdshell 'IF NOT EXIST ""{2}"" MKDIR ""{2}""'

BACKUP DATABASE [{0}]
TO DISK = '{1}'

EXEC xp_cmdshell 'ECHO Y | DEL ""{3}""'
EXEC xp_cmdshell 'move ""{1}"" ""{3}"" '


				", options.DbName
				, options.GetFullAttemptedBackupFilePath()
				, options.GetFullBackupPath()
				, options.GetFullBackupFilePath());
		}
	}
}
