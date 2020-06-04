﻿using System;
using System.Collections.Generic;

namespace Clio
{
	public interface IPackageArchiver
	{
		string GetPackedPackageFileName(string packageName);
		string GetPackedGroupPackagesFileName(string groupPackagesName);
		void CheckPackedPackageExistsAndNotEmpty(string packedPackagePath);
		IEnumerable<string> FindGzipPackedPackagesFiles(string searchDirectory);
		void Pack(string packagePath, string packedPackagePath, bool skipPdb, bool overwrite);
		void Pack(string sourcePath, string destinationPath, IEnumerable<string> names, bool skipPdb, bool overwrite);
		void Unpack(string packedPackagePath, bool overwrite, string destinationPath = null);
		void Unpack(IEnumerable<string> packedPackagesPaths, bool overwrite, string destinationPath = null);
		void ZipPackages(string sourceGzipFilesFolderPaths, string destinationArchiveFileName, bool overwrite);
		void UnZipPackages(string zipFilePath, bool overwrite, bool deleteGzFiles = true, 
			string destinationPath = null);
	}
}