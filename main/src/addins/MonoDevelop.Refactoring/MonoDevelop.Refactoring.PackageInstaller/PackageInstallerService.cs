﻿//
// PackageInstallerService.cs
//
// Author:
//       Mike Krüger <mikkrg@microsoft.com>
//
// Copyright (c) 2017 Microsoft Corporation
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Packaging;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;

namespace MonoDevelop.Refactoring.PackageInstaller
{
	[ExportWorkspaceServiceFactory (typeof (IPackageInstallerService)), Shared]
	class PackageInstallerServiceFactory : IWorkspaceServiceFactory
	{
		public static IPackageServicesProxy PackageServices;

		static Lazy<IPackageInstallerService> service = new Lazy<IPackageInstallerService> (() => new PackageInstallerService ());

		public IWorkspaceService CreateService (HostWorkspaceServices workspaceServices)
		{
			if (PackageServices == null)
				return null;
			return service.Value;
		}

		// Wrapper types to ensure we delay load the nuget libraries.
		internal interface IPackageServicesProxy
		{
			event EventHandler SourcesChanged;

			IEnumerable<KeyValuePair<string, string>> GetSources (bool includeUnOfficial, bool includeDisabled);

			IEnumerable<PackageMetadata> GetInstalledPackages (MonoDevelop.Projects.Project project);

			bool IsPackageInstalled (MonoDevelop.Projects.Project project, string id);

			void InstallPackage (string source, MonoDevelop.Projects.Project project, string packageId, string version, bool ignoreDependencies);
			void InstallLatestPackage (string source, MonoDevelop.Projects.Project project, string packageId, bool includePrerelease, bool ignoreDependencies);

			void UninstallPackage (MonoDevelop.Projects.Project project, string packageId, bool removeDependencies);

			Task<IEnumerable<(string PackageName, string Version, int Rank)>> FindPackagesWithAssemblyAsync (string source, string assemblyName, CancellationToken cancellationToken);
			Task<IEnumerable<(string PackageName, string TypeName, string Version, int Rank, ImmutableArray<string> ContainingNamespaceNames)>> FindPackagesWithTypeAsync (string source, string name, int arity, CancellationToken cancellationToken);
			Task<IEnumerable<(string AssemblyName, string TypeName, ImmutableArray<string> ContainingNamespaceNames)>> FindReferenceAssembliesWithTypeAsync (string name, int arity, CancellationToken cancellationToken);
			ImmutableArray<string> GetInstalledVersions (string packageName);
			IEnumerable<MonoDevelop.Projects.Project> GetProjectsWithInstalledPackage (MonoDevelop.Projects.Solution solution, string packageName, string version);
			void ShowManagePackagesDialog (string packageName);
		}

		internal class PackageMetadata
		{
			public readonly string Id;
			public readonly string VersionString;

			public PackageMetadata (string id, string versionString)
			{
				Id = id;
				VersionString = versionString;
			}
		}

		class PackageInstallerService : IPackageInstallerService
		{
			readonly ConcurrentDictionary<ProjectId, Dictionary<string, string>> _projectToInstalledPackageAndVersion = new ConcurrentDictionary<ProjectId, Dictionary<string, string>> ();

			/// <summary>
			/// Get package sources.
			///
			/// NOTE: This method is known to be called from the threadpool, while the UI thread is blocking.
			/// Therefore, it must be thread-safe and not defer to and then block other threads.
			/// </summary>
			public ImmutableArray<PackageSource> GetPackageSources ()
			{
				return PackageServices.GetSources (false, false).Select (kv => new PackageSource (kv.Key, kv.Value)) .ToImmutableArray ();
			}

			public event EventHandler PackageSourcesChanged {
				add => PackageServices.SourcesChanged += value;
				remove => PackageServices.SourcesChanged -= value;
			}

			public ImmutableArray<string> GetInstalledVersions (string packageName)
			{
				return PackageServices.GetInstalledVersions (packageName);
			}

			public IEnumerable<Project> GetProjectsWithInstalledPackage (Solution solution, string packageName, string version)
			{
				return PackageServices.GetProjectsWithInstalledPackage (IdeApp.ProjectOperations.CurrentSelectedSolution, packageName, version).Select (p => IdeApp.TypeSystemService.GetCodeAnalysisProject (p));
			}

			public bool IsInstalled (Workspace workspace, ProjectId projectId, string packageName)
			{
				return _projectToInstalledPackageAndVersion.TryGetValue (projectId, out var installedPackages) &&
					installedPackages.ContainsKey (packageName);
			}

			public bool CanShowManagePackagesDialog ()
				=> true;

			public void ShowManagePackagesDialog (string packageName)
			{
				PackageServices.ShowManagePackagesDialog (packageName);
			}

			public bool TryInstallPackage (Workspace workspace, DocumentId documentId, string source, string packageName, string versionOpt, bool includePrerelease, CancellationToken cancellationToken)
			{
				try {
					var monoProject = ((MonoDevelopWorkspace)workspace).GetMonoProject (documentId.ProjectId);

					if (!PackageServices.IsPackageInstalled (monoProject, packageName)) {

						if (versionOpt == null) {
							PackageServices.InstallLatestPackage (
								source, monoProject, packageName, includePrerelease, ignoreDependencies: false);
						} else {
							PackageServices.InstallPackage (
								source, monoProject, packageName, versionOpt, ignoreDependencies: false);
						}
						return true;
					}
				} catch (Exception e) {
					LoggingService.LogError ("Error while installing nuget package.", e);
				}

				return false;
			}

			public bool IsEnabled (ProjectId projectId)
			{
				return true;
			}
		}
	}
}
