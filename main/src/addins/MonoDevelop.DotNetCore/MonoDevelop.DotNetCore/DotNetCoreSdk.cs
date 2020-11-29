//
// DotNetCoreSdk.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2017 Xamarin Inc. (http://xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Core;
using MonoDevelop.Core.Assemblies;
using MonoDevelop.Projects.MSBuild;

namespace MonoDevelop.DotNetCore
{
	public static class DotNetCoreSdk
	{
		static readonly Version DotNetCoreVersion2_1 = new Version (2, 1, 0);
		internal static readonly DotNetCoreVersion DotNetCoreUnsupportedTargetFrameworkVersion = new DotNetCoreVersion (3, 2, 0);

		static DotNetCoreSdk ()
		{
			var sdkPaths = new DotNetCoreSdkPaths ();
			sdkPaths.ResolveSDK ();
			Update (sdkPaths);
		}

		internal static void Update (DotNetCoreSdkPaths dotNetCoreSdkPaths)
		{
			RegisterProjectImportSearchPath (MSBuildSDKsPath, dotNetCoreSdkPaths.MSBuildSDKsPath);

			MSBuildSDKsPath = dotNetCoreSdkPaths.MSBuildSDKsPath;
			SdkRootPath = dotNetCoreSdkPaths.SdkRootPath;
			IsInstalled = !string.IsNullOrEmpty (MSBuildSDKsPath);
			Versions = dotNetCoreSdkPaths.SdkVersions ?? Array.Empty<DotNetCoreVersion> ();

			if (!IsInstalled)
				LoggingService.LogInfo (".NET Core SDK not found.");
		}

		static void RegisterProjectImportSearchPath (string oldPath, string newPath)
		{
			const string propertyName = "MSBuildSDKsPath";

			if (!string.IsNullOrEmpty (oldPath))
				MSBuildProjectService.UnregisterProjectImportSearchPath (propertyName, oldPath);

			if (!string.IsNullOrEmpty (newPath))
				MSBuildProjectService.RegisterProjectImportSearchPath (propertyName, newPath);
		}

		public static bool IsInstalled { get; private set; }
		public static string MSBuildSDKsPath { get; private set; }
		internal static string SdkRootPath { get; private set; }

		internal static DotNetCoreVersion[] Versions { get; private set; }

		internal static void EnsureInitialized ()
		{
		}

		internal static DotNetCoreSdkPaths FindSdkPaths (string[] sdks)
		{
			var sdkPaths = new DotNetCoreSdkPaths ();
			sdkPaths.ResolveSDK ();
			sdkPaths.FindSdkPaths (sdks);
			return sdkPaths;
		}

		/// <summary>
		/// Checks that the target framework (e.g. .NETCoreApp1.1 or .NETStandard2.0) is supported
		/// by the installed SDKs. Takes into account Mono having .NET Core v1 SDKs installed.
		/// </summary>
		internal static bool IsSupported (TargetFramework framework)
		{
			return IsSupported (framework.Id, Versions, MSBuildSdks.Installed);
		}

		/// <summary>
		/// Used by unit tests.
		/// </summary>
		internal static bool IsSupported (
			TargetFrameworkMoniker projectFramework,
			DotNetCoreVersion[] versions,
			bool msbuildSdksInstalled)
		{
			if (!projectFramework.IsNetStandardOrNetCoreApp ()) {
				// Allow other frameworks to be supported such as .NET Framework.
				return true;
			}

			var projectFrameworkVersion = Version.Parse (projectFramework.Version);

			if (versions.Any (sdkVersion => IsSupported (projectFramework, projectFrameworkVersion, sdkVersion)))
				return true;

			// .NET Core <= 2.1 is supported by the MSBuild .NET Core SDKs if they are installed with Mono.
			if (projectFrameworkVersion <= DotNetCoreVersion2_1)
				return msbuildSdksInstalled;

			return false;
		}

		/// <summary>
		/// Project framework version is considered supported if the major version of the
		/// .NET Core SDK is greater or equal to the major version of the project framework.
		/// The fact that a .NET Core SDK is a preview version is ignored in this check.
		///
		/// .NET Core SDK 1.0.4 supports .NET Core 1.0 and 1.1
		/// .NET Core SDK 1.0.4 supports .NET Standard 1.0 to 1.6
		/// .NET Core SDK 2.0 supports .NET Core 1.0, 1.1 and 2.0
		/// .NET Core SDK 2.0 supports .NET Standard 1.0 to 1.6 and 2.0
		/// .NET Core SDK 2.1.x (x &lt; 300) supports .NET Core 1.0, 1.1 and 2.0
		/// .NET Core SDK 2.1.x (x &lt; 300) supports .NET Standard 1.0 to 1.6 and 2.0
		/// .NET Core 2.1.300 supports .NET Core 1.0, 1.1, 2.0 and 2.1
		/// .NET Core 2.1.300 supports .NET Standard 1.0 to 1.6, and 2.0, 2.1
		/// </summary>
		static bool IsSupported (TargetFrameworkMoniker projectFramework, Version projectFrameworkVersion, DotNetCoreVersion sdkVersion)
		{
			// Special case .NET Core 2.1.
			if (IsNetCore21 (projectFramework, projectFrameworkVersion) && !SupportsNetCore21 (sdkVersion))
				return false;

			return sdkVersion.Major >= projectFrameworkVersion.Major;
		}

		static bool IsNetCore21 (TargetFrameworkMoniker framework, Version version)
		{
			return framework.IsNetCoreApp () && version.Major == 2 && version.Minor == 1;
		}

		/// <summary>
		/// .NET Core 2.1.300 SDK is the lowest version that supports .NET Core App 2.1
		/// </summary>
		static bool SupportsNetCore21 (DotNetCoreVersion version)
		{
			return version.Major >= 2 && version.Minor >= 1 && version.Patch >= 300;
		}

		/// <summary>
		/// Used by unit tests to fake having different .NET Core sdks installed.
		/// </summary>
		internal static void SetVersions (IEnumerable<DotNetCoreVersion> versions)
		{
			Versions = versions.ToArray ();
		}

		/// <summary>
		/// Used by unit tests to fake having the sdk installed.
		/// </summary>
		internal static void SetInstalled (bool installed)
		{
			IsInstalled = installed;
		}

		/// <summary>
		/// Used by unit tests to fake having the sdk installed.
		/// </summary>
		internal static void SetSdkRootPath (string path)
		{
			SdkRootPath = path;
		}

		internal static string GetNotSupportedVersionMessage (string version = "")
		{
			string GetMessage (DotNetCoreVersion currentVersion)
			{
				return GettextCatalog.GetString (".NET Core {0}.{1} SDK version {2} is not compatible with this version of Visual Studio for Mac. Install the latest update to the .NET Core {0}.{1} SDK by visiting {3}", currentVersion.Major, currentVersion.Minor, currentVersion.ToString (), DotNetCoreDownloadUrl.GetDotNetCoreDownloadUrl (currentVersion));
			}

			var installedVersion = Versions?.OrderByDescending (x => x).FirstOrDefault ();
			if (installedVersion != null) {
				if (installedVersion < DotNetCoreVersion.MinimumSupportedSdkVersion) {
					return GetMessage (installedVersion);
				} else if (installedVersion.Major == 2 && installedVersion.Minor == 2 && installedVersion < DotNetCoreVersion.MinimumSupportedSdkVersion22) {
					return GetMessage (installedVersion);
				} else if (installedVersion.Major == 3 && installedVersion < DotNetCoreVersion.MinimumSupportedSdkVersion30) {
					return GetMessage (installedVersion);
				}
			}

			return GettextCatalog.GetString (".NET Core {0} SDK is required to build this application, and is not installed. Install the latest update to the .NET Core {0} SDK by visiting {1}", version, DotNetCoreDownloadUrl.GetDotNetCoreDownloadUrl (version));
		}
	}
}
