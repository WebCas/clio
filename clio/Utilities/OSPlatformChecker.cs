using System;

namespace Clio.Utilities
{
	using System.Runtime.InteropServices;

	#region Interface: IOSPlatformChecker

	public interface IOSPlatformChecker
	{

		#region Methods: Public

		bool IsWindowsEnvironment { get; }

		#endregion

	}

	#endregion

	#region Class: OSPlatformChecker

	public class OSPlatformChecker : IOSPlatformChecker
	{


		#region Properties: Public

		public bool IsWindowsEnvironment => GetIsWindowsEnvironment();

		#endregion

		#region Methods: Public

		public static bool GetIsWindowsEnvironment() {
			switch (Environment.OSVersion.Platform) {
				case PlatformID.Win32NT:
				case PlatformID.Win32S:
				case PlatformID.Win32Windows:
				case PlatformID.WinCE:
					return true;
				default:
					return false;
			}
		}

		public static OSPlatform GetOSPlatform(){
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
				return OSPlatform.Windows;
			}
			
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)){
				return OSPlatform.Linux;
			}
			
			if(RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)){
				return OSPlatform.FreeBSD;
			}
			
			if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)){
				return OSPlatform.OSX;
			}
			
			throw new PlatformNotSupportedException();
			
		}
		
		public static string GetOSPlatformName(){
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)){
				return "win";
			}
			
			if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)){
				return "linux";
			}
			
			if(RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)){
				return "freebsd";
			}
			
			if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX)){
				return "osx";
			}
			
			throw new PlatformNotSupportedException();
			
		}
		
		#endregion

	}

	#endregion

}