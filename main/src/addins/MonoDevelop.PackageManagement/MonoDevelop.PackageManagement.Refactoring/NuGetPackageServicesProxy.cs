﻿//
// NuGetPackageServicesProxy.cs
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
using System.Collections.Generic;
using MonoDevelop.Projects;
using MonoDevelop.Refactoring.PackageInstaller;
using System.Threading;
using MonoDevelop.Ide;
using System.Linq;
using MonoDevelop.Core;
using System.Threading.Tasks;
using System.Collections.Immutable;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace MonoDevelop.PackageManagement.Refactoring
{
	class NuGetPackageServicesProxy : PackageInstallerServiceFactory.IPackageServicesProxy
	{
		#pragma warning disable 67
		public event EventHandler SourcesChanged;
		#pragma warning restore 67

		public IEnumerable<PackageInstallerServiceFactory.PackageMetadata> GetInstalledPackages (Project project)
		{
			var solutionManager = PackageManagementServices.Workspace.GetSolutionManager (project.ParentSolution);
			var proxy = new DotNetProjectProxy ((DotNetProject)project);
			var np = solutionManager.GetNuGetProject (proxy);
			if (np == null)
				yield break;
			var packages = np.GetInstalledPackagesAsync (default (CancellationToken)).WaitAndGetResult (default (CancellationToken));
			if (packages == null)
				yield break;
			foreach (var p in packages) {
				yield return new PackageInstallerServiceFactory.PackageMetadata (p.PackageIdentity.Id, p.PackageIdentity.Version.ToFullString ());
			}
		}

		/// <summary>
		/// Get package sources.
		/// 
		/// NOTE: This method is known to be called from the threadpool, while the UI thread is blocking.
		/// Therefore, it must be thread-safe and not defer to and then block other threads.
		/// </summary>
		public IEnumerable<KeyValuePair<string, string>> GetSources (bool includeUnOfficial, bool includeDisabled)
		{
			var result = new List<KeyValuePair<string, string>> ();

			foreach (var repository in GetSourceRepositories ().ToList ()) {
				result.Add (new KeyValuePair<string, string> (
					repository.PackageSource.Name,
					repository.PackageSource.Source
				));
			}
			return result;
		}

		public void InstallLatestPackage (string source, Project project, string packageId, bool includePrerelease, bool ignoreDependencies)
		{
			InstallPackage (source, project, packageId, null, includePrerelease, false);
		}

		public void InstallPackage (string source, Project project, string packageId, string version, bool ignoreDependencies)
		{
			InstallPackage (source, project, packageId, version, false, ignoreDependencies);
		}

		void InstallPackage (string source, Project project, string packageId, string version, bool includePrerelease, bool ignoreDependencies)
		{
			Runtime.RunInMainThread (delegate {
				var repositoryProvider = SourceRepositoryProviderFactory.CreateSourceRepositoryProvider ();
				var repository = repositoryProvider.CreateRepository (new PackageSource (source));
				var solutionManager = PackageManagementServices.Workspace.GetSolutionManager (project.ParentSolution);

				var action = new InstallNuGetPackageAction (
					new [] { repository },
					solutionManager,
					new DotNetProjectProxy ((DotNetProject)project),
					new NuGetProjectContext (solutionManager.Settings)) {
					PackageId = packageId,
					Version = string.IsNullOrEmpty (version) ? null : new NuGetVersion (version),
					IncludePrerelease = includePrerelease,
					IgnoreDependencies = ignoreDependencies,
				};

				var message = ProgressMonitorStatusMessageFactory.CreateInstallingSinglePackageMessage (packageId);
				PackageManagementServices.BackgroundPackageActionRunner.Run (message, action);
			});
		}

		public bool IsPackageInstalled (Project project, string id)
		{
			return GetInstalledPackages (project).Any (p => StringComparer.OrdinalIgnoreCase.Equals (p.Id, id));
		}

		public void UninstallPackage (Project project, string packageId, bool removeDependencies)
		{
			Runtime.RunInMainThread (delegate {
				var action = new UninstallNuGetPackageAction (
					PackageManagementServices.Workspace.GetSolutionManager (project.ParentSolution),
					new DotNetProjectProxy ((DotNetProject)project)) {
					PackageId = packageId,
					RemoveDependencies = removeDependencies
				};

				var message = ProgressMonitorStatusMessageFactory.CreateRemoveSinglePackageMessage (packageId);
				PackageManagementServices.BackgroundPackageActionRunner.Run (message, action);
			});
		}

		/// <summary>
		/// Find packages for a given assembly name.
		/// 
		/// NOTE: This method is known to be called from the threadpool, while the UI thread is blocking.
		/// Therefore, it must be thread-safe and not defer to and then block other threads.
		/// </summary>
		public Task<IEnumerable<(string PackageName, string Version, int Rank)>> FindPackagesWithAssemblyAsync (string source, string assemblyName, CancellationToken cancellationToken)
		{
			var result = new List<(string PackageName, string Version, int Rank)> ();
			if (assemblyName == "System.ValueTuple" && IsOfficialNuGetPackageSource (source)) {
				result.Add (("System.ValueTuple", "4.3.0", 1)); 
			}
			return Task.FromResult<IEnumerable<(string PackageName, string Version, int Rank)>> (result);
		}

		public Task<IEnumerable<(string PackageName, string TypeName, string Version, int Rank, ImmutableArray<string> ContainingNamespaceNames)>> FindPackagesWithTypeAsync (string source, string name, int arity, CancellationToken cancellationToken)
		{
			var result = new List<(string PackageName, string TypeName, string Version, int Rank, ImmutableArray<string> ContainingNamespaceNames)> ();
			return Task.FromResult ((IEnumerable<(string PackageName, string TypeName, string Version, int Rank, ImmutableArray<string> ContainingNamespaceNames)>)result);
		}

		public Task<IEnumerable<(string AssemblyName, string TypeName, ImmutableArray<string> ContainingNamespaceNames)>> FindReferenceAssembliesWithTypeAsync (string name, int arity, CancellationToken cancellationToken)
		{
			var result = new List<(string AssemblyName, string TypeName, ImmutableArray<string> ContainingNamespaceNames)> ();
			return Task.FromResult ((IEnumerable<(string AssemblyName, string TypeName, ImmutableArray<string> ContainingNamespaceNames)>)result);
		}


		/// <summary>
		/// Get the installed versions of a given package.
		/// 
		/// NOTE: This method is known to be called from the threadpool, while the UI thread is blocking.
		/// Therefore, it must be thread-safe and not defer to and then block other threads.
		/// </summary>
		public ImmutableArray<string> GetInstalledVersions (string packageName)
		{
			// The UI thread may already be blocking when this is called, so defer to threadpool instead of UI thread.
			return Task.Run (async () => {
				var solutionManager = PackageManagementServices.Workspace.GetSolutionManager (IdeApp.ProjectOperations.CurrentSelectedSolution);
				var versions = await solutionManager.GetInstalledVersions (packageName).ConfigureAwait (false);
				var versionStrings = versions.Select (version => version.ToFullString ()).ToArray ();
				return ImmutableArray.Create (versionStrings);
			}).WaitAndGetResult ();
		}

		public IEnumerable<Project> GetProjectsWithInstalledPackage (Solution solution, string packageName, string version)
		{
			return Runtime.RunInMainThread (async delegate {
				var solutionManager = PackageManagementServices.Workspace.GetSolutionManager (solution);
				var projects = await solutionManager.GetProjectsWithInstalledPackage (packageName, version).ConfigureAwait (false);
				return projects.Select (project => project.DotNetProject);
			}).WaitAndGetResult (default (CancellationToken));
		}

		public void ShowManagePackagesDialog (string packageName)
		{
 			Runtime.RunInMainThread (delegate {
				var project = IdeApp.Workbench.ActiveDocument?.Owner as DotNetProject;
				if (project != null) {
					var runner = new ManagePackagesDialogRunner ();
					runner.Run (project, packageName);
				}
			});
		}

		/// <summary>
		/// Get package source repositories.
		/// 
		/// NOTE: This method is known to be called from the threadpool, while the UI thread is blocking.
		/// Therefore, it must be thread-safe and not defer to and then block other threads.
		/// </summary>
		IEnumerable<SourceRepository> GetSourceRepositories ()
		{
			var solutionManager = PackageManagementServices.Workspace.GetSolutionManager (IdeApp.ProjectOperations.CurrentSelectedSolution);

			var provider = solutionManager.CreateSourceRepositoryProvider ();
			var packageSourceProvider = provider.PackageSourceProvider;
			return provider.GetRepositories ();
		}

		/// <summary>
		/// Determine if a given source is the official nuget.org package source.
		/// 
		/// NOTE: This method is known to be called from the threadpool, while the UI thread is blocking.
		/// Therefore, it must be thread-safe and not defer to and then block other threads.
		/// </summary>
		bool IsOfficialNuGetPackageSource (string source)
		{
			var matchedRepository = GetSourceRepositories ()
				.FirstOrDefault (repository => repository.PackageSource.Name == source);

			if (matchedRepository != null) {
				string host = matchedRepository.PackageSource.SourceUri.Host;
				return host.EndsWith ("nuget.org", StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}
	}
}
