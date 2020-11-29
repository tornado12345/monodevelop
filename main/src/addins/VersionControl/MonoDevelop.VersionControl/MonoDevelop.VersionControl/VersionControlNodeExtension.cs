﻿using System;
using System.Linq;
using System.Collections.Generic;

using MonoDevelop.Core;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Ide.Gui.Pads.ProjectPad;
using MonoDevelop.Projects;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide.Gui.Components;
using MonoDevelop.VersionControl.Views;
using MonoDevelop.Ide;
using System.Threading.Tasks;
using System.Threading;

namespace MonoDevelop.VersionControl
{
	class VersionControlNodeExtension : NodeBuilderExtension
	{
		Dictionary<FilePath,object> pathToObject = new Dictionary<FilePath, object> ();
		
		public override bool CanBuildNode (Type dataType)
		{
			//Console.Error.WriteLine(dataType);
			return typeof(ProjectFile).IsAssignableFrom (dataType)
				|| typeof(SystemFile).IsAssignableFrom (dataType)
				|| typeof(ProjectFolder).IsAssignableFrom (dataType)
				|| typeof(WorkspaceObject).IsAssignableFrom (dataType);
		}
		
		protected override void Initialize ()
		{
			base.Initialize ();
			VersionControlService.FileStatusChanged += Monitor;
			IdeApp.Workspace.LastWorkspaceItemClosed += OnWorkspaceRefresh;
		}

		public override void Dispose ()
		{
			VersionControlService.FileStatusChanged -= Monitor;
			IdeApp.Workspace.LastWorkspaceItemClosed -= OnWorkspaceRefresh;
			base.Dispose ();
		}

		void OnWorkspaceRefresh (object sender, EventArgs args)
		{
			pathToObject.Clear ();
		}

		public override void BuildNode (ITreeBuilder builder, object dataObject, NodeInfo nodeInfo)
		{
			if (!builder.Options["ShowVersionControlOverlays"])
				return;
		
			// Add status overlays
			
			if (dataObject is WorkspaceObject) {
				WorkspaceObject ce = (WorkspaceObject)dataObject;
				Repository rep = VersionControlService.GetRepository (ce);
				if (rep != null) {
					rep.GetDirectoryVersionInfoAsync (ce.BaseDirectory, false, false);
					AddFolderOverlay (rep, ce.BaseDirectory, nodeInfo, false);
				}
				return;
			} else if (dataObject is ProjectFolder) {
				ProjectFolder ce = (ProjectFolder) dataObject;
				if (ce.ParentWorkspaceObject != null) {
					Repository rep = VersionControlService.GetRepository (ce.ParentWorkspaceObject);
					if (rep != null) {
						rep.GetDirectoryVersionInfoAsync (ce.Path, false, false);
						AddFolderOverlay (rep, ce.Path, nodeInfo, true);
					}
				}
				return;
			}
			
			WorkspaceObject prj;
			FilePath file;
			
			if (dataObject is ProjectFile) {
				ProjectFile pfile = (ProjectFile) dataObject;
				prj = pfile.Project;
				file = pfile.FilePath;
			} else {
				SystemFile pfile = (SystemFile) dataObject;
				prj = pfile.ParentWorkspaceObject;
				file = pfile.Path;
			}
			
			if (prj == null)
				return;
			
			Repository repo = VersionControlService.GetRepository (prj);
			if (repo == null)
				return;

			if (repo.TryGetVersionInfo (file, out var vi)) {
				var overlay = VersionControlService.LoadOverlayIconForStatus (vi.Status);
				if (overlay != null)
					nodeInfo.OverlayBottomRight = overlay;
			}
		}

/*		public override void PrepareChildNodes (object dataObject)
		{
			if (dataObject is IWorkspaceObject) {
				IWorkspaceObject ce = (IWorkspaceObject) dataObject;
				Repository rep = VersionControlService.GetRepository (ce);
				if (rep != null)
					rep.GetDirectoryVersionInfo (ce.BaseDirectory, false, false);
			} else if (dataObject is ProjectFolder) {
				ProjectFolder ce = (ProjectFolder) dataObject;
				if (ce.ParentWorkspaceObject != null) {
					Repository rep = VersionControlService.GetRepository (ce.ParentWorkspaceObject);
					if (rep != null)
						rep.GetDirectoryVersionInfo (ce.Path, false, false);
				}
			}
			base.PrepareChildNodes (dataObject);
		}
*/		
		static void AddFolderOverlay (Repository rep, string folder, NodeInfo nodeInfo, bool skipVersionedOverlay)
		{
			if (!rep.TryGetVersionInfo (folder, out var vinfo))
				return;
			Xwt.Drawing.Image overlay = null;
			if (vinfo == null || !vinfo.IsVersioned) {
				overlay = VersionControlService.LoadOverlayIconForStatus (VersionStatus.Unversioned);
			} else if (vinfo.IsVersioned && !vinfo.HasLocalChanges) {
				if (!skipVersionedOverlay)
					overlay = VersionControlService.overlay_controled;
			} else {
				overlay = VersionControlService.LoadOverlayIconForStatus (vinfo.Status);
			}
			if (overlay != null)
				nodeInfo.OverlayBottomRight = overlay;
		}
		
		void Monitor (object sender, FileUpdateEventArgs args)
		{
			foreach (var uinfo in args) {
				foreach (var ob in GetObjectsForPath (uinfo.FilePath)) {
					ITreeBuilder builder = Context.GetTreeBuilder (ob);
					if (builder != null)
						builder.Update ();
				}
			}
		}

		void RegisterObjectPath (FilePath path, object ob)
		{
			path = path.CanonicalPath;
			object currentObj;
			if (pathToObject.TryGetValue (path, out currentObj)) {
				if (currentObj is List<object>) {
					var list = (List<object>) currentObj;
					list.Add (ob);
				} else {
					var list = new List<object> (2);
					list.Add (currentObj);
					list.Add (ob);
					pathToObject [path] = list;
				}
			} else
				pathToObject [path] = ob;
		}

		void UnregisterObjectPath (FilePath path, object ob)
		{
			path = path.CanonicalPath;
			object currentObj;
			if (pathToObject.TryGetValue (path, out currentObj)) {
				if (currentObj is List<object>) {
					var list = (List<object>) currentObj;
					if (list.Remove (ob)) {
						if (list.Count == 1)
							pathToObject [path] = list[0];
					}
				} else if (currentObj == ob)
					pathToObject.Remove (path);
			}
		}

		IEnumerable<object> GetObjectsForPath (FilePath path)
		{
			path = path.CanonicalPath;
			object currentObj;
			if (pathToObject.TryGetValue (path, out currentObj)) {
				if (currentObj is List<object>) {
					foreach (var ob in (List<object>) currentObj)
						yield return ob;
				} else
					yield return currentObj;
			}
		}
		
		public override void OnNodeAdded (object dataObject)
		{
			FilePath path = GetPath (dataObject);
			if (path != FilePath.Null)
				RegisterObjectPath (path, dataObject);
		}
		
		public override void OnNodeRemoved (object dataObject)
		{
			FilePath path = GetPath (dataObject);
			if (path != FilePath.Null)
				UnregisterObjectPath (path, dataObject);
		}
		
		internal static string GetPath (object dataObject)
		{
			if (dataObject is ProjectFile) {
				return ((ProjectFile) dataObject).FilePath;
			} else if (dataObject is SystemFile) {
				return ((SystemFile) dataObject).Path;
			} else if (dataObject is WorkspaceObject) {
				return ((WorkspaceObject)dataObject).BaseDirectory;
			} else if (dataObject is ProjectFolder) {
				return ((ProjectFolder)dataObject).Path;
			}
			return FilePath.Null;
		}
		
		public override Type CommandHandlerType {
			get { return typeof(AddinCommandHandler); }
		}
	}

	class AddinCommandHandler : VersionControlCommandHandler 
	{
		[AllowMultiSelection]
		[CommandHandler (Commands.Update)]
		protected Task OnUpdate() => RunCommandAsync(Commands.Update, false);

		[CommandUpdateHandler (Commands.Update)]
		protected Task UpdateUpdate (CommandInfo item, CancellationToken token) => TestCommand (Commands.Update, item, cancellationToken: token);

		[AllowMultiSelection]
		[CommandHandler (Commands.Diff)]
		protected Task OnDiff() => RunCommandAsync(Commands.Diff, false);
		
		[CommandUpdateHandler (Commands.Diff)]
		protected Task UpdateDiff(CommandInfo item, CancellationToken token) => TestCommand(Commands.Diff, item, cancellationToken: token);
		
		[AllowMultiSelection]
		[CommandHandler (Commands.Log)]
		protected Task OnLog() => RunCommandAsync(Commands.Log, false);
		
		[CommandUpdateHandler (Commands.Log)]
		protected Task UpdateLog(CommandInfo item, CancellationToken token) => TestCommand(Commands.Log, item, cancellationToken: token);

		[AllowMultiSelection]
		[CommandHandler (Commands.Status)]
		protected Task OnStatus() => RunCommandAsync(Commands.Status, false);
		
		[CommandUpdateHandler (Commands.Status)]
		protected Task UpdateStatus(CommandInfo item, CancellationToken token) => TestCommand(Commands.Status, item, cancellationToken: token);

		[AllowMultiSelection]
		[CommandHandler (Commands.Add)]
		protected Task OnAdd() => RunCommandAsync(Commands.Add, false);

		[CommandUpdateHandler (Commands.Add)]
		protected Task UpdateAdd(CommandInfo item, CancellationToken token) => TestCommand(Commands.Add, item, cancellationToken: token);
		
		[AllowMultiSelection]
		[CommandHandler (Commands.Remove)]
		protected Task OnRemove()  => RunCommandAsync(Commands.Remove, false);

		[CommandUpdateHandler (Commands.Remove)]
		protected Task UpdateRemove(CommandInfo item, CancellationToken token) => TestCommand(Commands.Remove, item, cancellationToken: token);

		[CommandHandler (Commands.Publish)]
		protected Task OnPublish() => RunCommandAsync(Commands.Publish, false);

		[CommandUpdateHandler (Commands.Publish)]
		protected Task UpdatePublish(CommandInfo item, CancellationToken token) => TestCommand(Commands.Publish, item, cancellationToken: token);

		[AllowMultiSelection]
		[CommandHandler (Commands.Revert)]
		protected Task OnRevert() => RunCommandAsync(Commands.Revert, false, false);

		[CommandUpdateHandler (Commands.Revert)]
		protected  Task UpdateRevert(CommandInfo item, CancellationToken token) => TestCommand(Commands.Revert, item, false, token);

		[AllowMultiSelection]
		[CommandHandler (Commands.Lock)]
		protected Task OnLock() => RunCommandAsync(Commands.Lock, false);
		
		[CommandUpdateHandler (Commands.Lock)]
		protected Task UpdateLock(CommandInfo item, CancellationToken token) => TestCommand(Commands.Lock, item, cancellationToken: token);
		
		[AllowMultiSelection]
		[CommandHandler (Commands.Unlock)]
		protected Task OnUnlock() => RunCommandAsync(Commands.Unlock, false);
		
		[CommandUpdateHandler (Commands.Unlock)]
		protected Task UpdateUnlock(CommandInfo item, CancellationToken token) => TestCommand(Commands.Unlock, item, cancellationToken: token);
		
		[AllowMultiSelection]
		[CommandHandler (Commands.Annotate)]
		protected Task OnAnnotate() => RunCommandAsync(Commands.Annotate, false);
		
		[CommandUpdateHandler (Commands.Annotate)]
		protected Task UpdateAnnotate(CommandInfo item, CancellationToken token) => TestCommand(Commands.Annotate, item, cancellationToken: token);
		
		[AllowMultiSelection]
		[CommandHandler (Commands.CreatePatch)]
		protected Task OnCreatePatch() => RunCommandAsync(Commands.CreatePatch, false);
		
		[CommandUpdateHandler (Commands.CreatePatch)]
		protected Task UpdateCreatePatch(CommandInfo item, CancellationToken token) => TestCommand(Commands.CreatePatch, item, cancellationToken: token);
		
		[AllowMultiSelection]
		[CommandHandler (Commands.Ignore)]
		protected Task OnIgnore () => RunCommandAsync(Commands.Ignore, false);

		[CommandUpdateHandler (Commands.Ignore)]
		protected Task UpdateIgnore (CommandInfo item, CancellationToken token) => TestCommand(Commands.Ignore, item, cancellationToken: token);

		[AllowMultiSelection]
		[CommandHandler (Commands.Unignore)]
		protected Task OnUnignore () => RunCommandAsync(Commands.Unignore, false);

		[CommandUpdateHandler (Commands.Unignore)]
		protected Task UpdateUnignore (CommandInfo item, CancellationToken token) => TestCommand (Commands.Unignore, item, cancellationToken: token);

		[CommandHandler (Commands.ResolveConflicts)]
		protected Task OnResolveConflicts () => RunCommandAsync (Commands.ResolveConflicts, false, false);

		[CommandUpdateHandler (Commands.ResolveConflicts)]
		protected Task UpdateResolveConflicts (CommandInfo item, CancellationToken token) => TestCommand (Commands.ResolveConflicts, item, false, token);

		private async Task<TestResult> TestCommand(Commands cmd, CommandInfo item, bool projRecurse = true, CancellationToken cancellationToken = default)
		{
			TestResult res = await RunCommandAsync(cmd, true, projRecurse, cancellationToken);
			if (res == TestResult.NoVersionControl && cmd == Commands.Log) {
				// Use the update command to show the "not available" message
				item.Icon = null;
				item.Enabled = false;
				if (VersionControlService.IsGloballyDisabled)
					item.Text = GettextCatalog.GetString ("Version Control support is disabled");
				else
					item.Text = GettextCatalog.GetString ("This project or folder is not under version control");
			} else
				item.Visible = res == TestResult.Enable;

			return res;
		}
		
		private async Task<TestResult> RunCommandAsync (Commands cmd, bool test, bool projRecurse = true, CancellationToken cancellationToken = default)
		{
			VersionControlItemList items = GetItems (projRecurse);

			foreach (VersionControlItem it in items) {
				if (it.Repository == null) {
					if (cmd != Commands.Publish)
						return TestResult.NoVersionControl;
				} else if (it.Repository.VersionControlSystem != null && !it.Repository.VersionControlSystem.IsInstalled) {
					return TestResult.Disable;
				}
			}

			bool res = false;

			try {
				switch (cmd) {
				case Commands.Update:
					res = await UpdateCommand.UpdateAsync (items, test, cancellationToken);
					break;
				case Commands.Diff:
					res = await DiffCommand.Show (items, test);
					break;
				case Commands.Log:
					res = await LogCommand.Show (items, test);
					break;
				case Commands.Status:
					res = await StatusView.ShowAsync (items, test, false);
					break;
				case Commands.Add:
					res = await AddCommand.AddAsync (items, test, cancellationToken);
					break;
				case Commands.Remove:
					res = await RemoveCommand.RemoveAsync (items, test, cancellationToken);
					break;
				case Commands.Revert:
					res = await RevertCommand.RevertAsync (items, test, cancellationToken);
					break;
				case Commands.Lock:
					res = await LockCommand.LockAsync (items, test, cancellationToken);
					break;
				case Commands.Unlock:
					res = await UnlockCommand.UnlockAsync (items, test, cancellationToken);
					break;
				case Commands.Publish:
					VersionControlItem it = items [0];
					if (items.Count == 1 && it.IsDirectory && it.WorkspaceObject != null)
						res = PublishCommand.Publish (it.WorkspaceObject, it.Path, test);
					break;
				case Commands.Annotate:
					res = await BlameCommand.Show (items, test);
					break;
				case Commands.CreatePatch:
					res = await CreatePatchCommand.CreatePatchAsync (items, test, cancellationToken);
					break;
				case Commands.Ignore:
					res = await IgnoreCommand.IgnoreAsync (items, test, cancellationToken);
					break;
				case Commands.Unignore:
					res = await UnignoreCommand.UnignoreAsync (items, test, cancellationToken);
					break;
				case Commands.ResolveConflicts:
					res = await ResolveConflictsCommand.ResolveConflicts (items, test);
					break;
				}
			} catch (OperationCanceledException) {
				return TestResult.Disable;
			} catch (Exception ex) {
				if (test)
					LoggingService.LogError (ex.ToString ());
				else
					MessageService.ShowError (GettextCatalog.GetString ("Version control command failed."), ex);
				return TestResult.Disable;
			}
			
			return res ? TestResult.Enable : TestResult.Disable;
		}

		public override void RefreshItem ()
		{
			foreach (VersionControlItem it in GetItems ()) {
				if (it.Repository != null)
					it.Repository.ClearCachedVersionInfo (it.Path);
			}
			base.RefreshItem ();
		}
	}

	class OpenCommandHandler : VersionControlCommandHandler 
	{
		[AllowMultiSelection]
		[CommandHandler (ViewCommands.Open)]
		protected void OnOpen ()
		{
			foreach (VersionControlItem it in GetItems ()) {
				if (!it.IsDirectory)
					IdeApp.Workbench.OpenDocument (it.Path, it.ContainerProject);
			}
		}
	}		
	
	
	enum TestResult
	{
		Enable,
		Disable,
		NoVersionControl
	}
}
