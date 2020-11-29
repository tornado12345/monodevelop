using System.Collections.Generic;
using System.Linq;

using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.VersionControl.Dialogs;
using MonoDevelop.Ide;
using MonoDevelop.Components.Commands;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace MonoDevelop.VersionControl
{
	internal class PublishCommand : CommandHandler
	{
		protected override void Update (CommandInfo info)
		{
			info.Enabled = info.Visible = false;
			if (!VersionControlService.IsGloballyDisabled) {
				var solution = IdeApp.ProjectOperations.CurrentSelectedSolution;
				if (solution == null)
					return;
				info.Enabled = info.Visible = Publish (solution, solution.BaseDirectory, true);
			}
		}

		protected override void Run ()
		{
			var solution = IdeApp.ProjectOperations.CurrentSelectedSolution;
			if (solution == null)
				return;
			Publish (solution, solution.BaseDirectory, false);
		}

		public static bool Publish (WorkspaceObject entry, FilePath localPath, bool test)
		{
			if (test)
				return VersionControlService.CheckVersionControlInstalled () && VersionControlService.GetRepository (entry) == null;

			List<FilePath> files = new List<FilePath> ();

			// Build the list of files to be checked in
			string moduleName = entry.Name;
			if (localPath == entry.BaseDirectory) {
				GetFiles (files, entry);
			} else if (entry is Project) {
				foreach (ProjectFile file in ((Project)entry).Files.GetFilesInPath (localPath))
					if (file.Subtype != Subtype.Directory)
						files.Add (file.FilePath);
			} else
				return false;

			if (files.Count == 0)
				return false;

			SelectRepositoryDialog dlg = new SelectRepositoryDialog (SelectRepositoryMode.Publish);
			try {
				dlg.ModuleName = moduleName;
				dlg.Message = GettextCatalog.GetString ("Initial check-in of module {0}", moduleName);
				do {
					if (MessageService.RunCustomDialog (dlg) == (int) Gtk.ResponseType.Ok && dlg.Repository != null) {
						AlertButton publishButton = new AlertButton (GettextCatalog.GetString ("_Publish"));
						if (MessageService.AskQuestion (GettextCatalog.GetString ("Are you sure you want to publish the project?"), GettextCatalog.GetString ("The project will be published to the repository '{0}', module '{1}'.", dlg.Repository.Name, dlg.ModuleName), AlertButton.Cancel, publishButton) == publishButton) {
							PublishWorker w = new PublishWorker (dlg.Repository, dlg.ModuleName, localPath, files.ToArray (), dlg.Message);
							w.StartAsync ();
							break;
						}
					} else
						break;
				} while (true);
			} finally {
				dlg.Destroy ();
				dlg.Dispose ();
			}
			return true;
		}

		static void GetFiles (List<FilePath> files, WorkspaceObject entry)
		{
			// Ensure that we strip out all linked files from outside of the solution/projects path.
			if (entry is IWorkspaceFileObject)
				files.AddRange (((IWorkspaceFileObject)entry).GetItemFiles (true).Where (file => file.CanonicalPath.IsChildPathOf (entry.BaseDirectory)));
		}

		public static async Task<bool> CanPublishAsync (Repository vc, string path, bool isDir) {
			if (!VersionControlService.CheckVersionControlInstalled ())
				return false;
			
			return !(vc.TryGetVersionInfo (path, out var info) && info.IsVersioned && isDir);
		}

		class PublishWorker : VersionControlTask
		{
			Repository vc;
			FilePath path;
			string moduleName;
			FilePath [] files;
			string message;

			public PublishWorker (Repository vc, string moduleName, FilePath localPath, FilePath [] files, string message)
			{
				this.vc = vc;
				this.path = localPath;
				this.moduleName = moduleName;
				this.files = files;
				this.message = message;
				OperationType = VersionControlOperationType.Push;
			}

			protected override string GetDescription ()
			{
				return GettextCatalog.GetString ("Publishing \"{0}\" Project...", moduleName);
			}

			protected override async Task RunAsync ()
			{
				try {
					await vc.PublishAsync (moduleName, path, files, message, Monitor);
				} catch (OperationCanceledException) {
					return;
				} catch (VersionControlException e) {
					LoggingService.LogError ("Publish operation failed", e);
					Monitor.ReportError (e.Message, null);
					return;
				}

				Gtk.Application.Invoke ((o, args) => {
					VersionControlService.NotifyFileStatusChanged (new FileUpdateEventArgs (vc, path, true));
				});
				Monitor.ReportSuccess (GettextCatalog.GetString ("Publish operation completed."));
			}
		}
	}
}
