using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using Clio.WebApplication;

namespace Clio.Common
{

	#region Class: LogRecord

	[DataContract]
	public class LogRecord
	{

		#region Properties: Public

		[DataMember(Name = "id")]
		public long Id { get; set; }

		[DataMember(Name = "date")]
		public DateTime Date { get; set; }

		[DataMember(Name = "host")]
		public string Host { get; set; }

		[DataMember(Name = "site")]
		public string Site { get; set; }

		[DataMember(Name = "thread")]
		public int Thread { get; set; }

		[DataMember(Name = "level")]
		public string Level { get; set; }

		[DataMember(Name = "logger")]
		public string Logger { get; set; }

		[DataMember(Name = "user")]
		public string User { get; set; }

		[DataMember(Name = "message")]
		public string Message { get; set; }

		[DataMember(Name = "exception")]
		public string Exception { get; set; }

		[DataMember(Name = "messageObject")]
		public string MessageObject { get; set; }

		#endregion

	}

	#endregion

	#region Interface: IClioLogDownloader

	public interface IClioLogDownloader
	{
		void GetLogFile(string beginDateTime, string endDateTime, string excludeLoggers,
			string folderPath);
	}

	#endregion

	#region Class: ClioLogDownloader

	public class ClioLogDownloader : IClioLogDownloader
	{

		#region Constants: Private

		private static string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

		#endregion

		#region Fields: Private

		private readonly IServiceUrlBuilder _serviceUrlBuilder;
		private readonly IDownloader _downloader;
		private readonly IFileSystem _fileSystem;

		#endregion

		#region Constructors: Public

		public ClioLogDownloader(IServiceUrlBuilder serviceUrlBuilder, IDownloader downloader, IFileSystem fileSystem) {
			serviceUrlBuilder.CheckArgumentNull(nameof(serviceUrlBuilder));
			downloader.CheckArgumentNull(nameof(downloader));
			fileSystem.CheckArgumentNull(nameof(fileSystem));
			_serviceUrlBuilder = serviceUrlBuilder;
			_downloader = downloader;
			_fileSystem = fileSystem;
		}

		#endregion

		#region Methods: Private


		private string GetUrl(string endpoint) => _serviceUrlBuilder.Build(endpoint);

		private static void CheckDateTime(string beginDateTime, string endDateTime) {
			if (!DateTime.TryParseExact(beginDateTime, DateTimeFormat, null, DateTimeStyles.None,
				    out DateTime _)) {
				throw new Exception($"Incorrect BeginDateTime format. Correct format {DateTimeFormat}");
			}
			if (!DateTime.TryParseExact(endDateTime, DateTimeFormat, null, DateTimeStyles.None,
				    out DateTime _)) {
				throw new Exception($"Incorrect EndDateTime format. Correct format {DateTimeFormat}");
			}
		}

		#endregion

		#region Methods: Public

		public void GetLogFile(string beginDateTime, string endDateTime, string excludeLoggers, string folderPath) {
			CheckDateTime(beginDateTime, endDateTime);
			folderPath = _fileSystem.GetCurrentDirectoryIfEmpty(folderPath);
			string logFolderPath = Path.Combine(folderPath, "Logs");
			string requestData = $"{{\"logrequest\":{{\"begin-date-time\":\"{beginDateTime}\","
				+ $"\"end-date-time\":\"{endDateTime}\",\"exclude-loggers\":\"{excludeLoggers}\"}}}}";
			var downloadInfo = new DownloadInfo(GetUrl("/rest/CreatioApiGateway/GetLogFile"),
				"logs", logFolderPath, requestData);
			_downloader.Download(downloadInfo);
		}

		#endregion

	}

	#endregion

}