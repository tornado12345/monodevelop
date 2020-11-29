﻿//
// GitConfigurationDialog.cs
//
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
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
using Gtk;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Components;
using LibGit2Sharp;
using MonoDevelop.Components.AutoTest;
using System.ComponentModel;
using System.Threading;

namespace MonoDevelop.VersionControl.Git
{
	partial class GitConfigurationDialog : Gtk.Dialog
	{
		GitRepository repo;
		readonly ListStore storeBranches;
		readonly ListStore storeTags;
		readonly TreeStore storeRemotes;

		public GitConfigurationDialog (VersionControlSystem vcs, string repoPath, string repoUrl)
		{
			this.Build ();
			this.repo = new GitRepository (vcs, repoPath, repoUrl, false);
			this.HasSeparator = false;

			this.UseNativeContextMenus ();

			// Branches list

			storeBranches = new ListStore (typeof(Branch), typeof(string), typeof(string), typeof(string));
			listBranches.Model = storeBranches;
			listBranches.SearchColumn = -1; // disable the interactive search
			listBranches.HeadersVisible = true;

			SemanticModelAttribute modelAttr = new SemanticModelAttribute ("storeBranches__Branch", "storeBranches__DisplayName", "storeBranches__Tracking", "storeBranches__Name");
			TypeDescriptor.AddAttributes (storeBranches, modelAttr);

			listBranches.AppendColumn (GettextCatalog.GetString ("Branch"), new CellRendererText (), "markup", 1);
			listBranches.AppendColumn (GettextCatalog.GetString ("Tracking"), new CellRendererText (), "text", 2);

			listBranches.Selection.Changed += delegate {
				TreeIter it;
				bool anythingSelected =
					buttonRemoveBranch.Sensitive = buttonEditBranch.Sensitive = buttonSetDefaultBranch.Sensitive = listBranches.Selection.GetSelected (out it);
				if (!anythingSelected)
						return;
				if (repo == null || repo.IsDisposed)
					return;
				string currentBranch = repo.GetCurrentBranch ();
				var b = (Branch) storeBranches.GetValue (it, 0);
				buttonRemoveBranch.Sensitive = b.FriendlyName != currentBranch;
				buttonSetDefaultBranch.Sensitive = !b.IsCurrentRepositoryHead;
			};
			buttonRemoveBranch.Sensitive = buttonEditBranch.Sensitive = buttonSetDefaultBranch.Sensitive = false;

			// Sources tree

			storeRemotes = new TreeStore (typeof(Remote), typeof(string), typeof(string), typeof(string), typeof(string));
			treeRemotes.Model = storeRemotes;
			treeRemotes.SearchColumn = -1; // disable the interactive search
			treeRemotes.HeadersVisible = true;

			SemanticModelAttribute remotesModelAttr = new SemanticModelAttribute ("storeRemotes__Remote", "storeRemotes__Name", "storeRemotes__Url", "storeRemotes__BranchName", "storeRemotes__FullName");
			TypeDescriptor.AddAttributes (storeRemotes, remotesModelAttr);

			treeRemotes.AppendColumn (GettextCatalog.GetString ("Remote Source / Branch"), new CellRendererText (), "markup", 1);
			treeRemotes.AppendColumn (GettextCatalog.GetString ("Url"), new CellRendererText (), "text", 2);

			treeRemotes.Selection.Changed += delegate {
				TreeIter it;
				bool anythingSelected = treeRemotes.Selection.GetSelected (out it);
				buttonTrackRemote.Sensitive = false;
				buttonFetch.Sensitive = buttonEditRemote.Sensitive = buttonRemoveRemote.Sensitive = anythingSelected;
				if (!anythingSelected)
					return;
				string branchName = (string) storeRemotes.GetValue (it, 3);
				if (branchName != null)
					buttonTrackRemote.Sensitive = true;
			};
			buttonTrackRemote.Sensitive = buttonFetch.Sensitive = buttonEditRemote.Sensitive = buttonRemoveRemote.Sensitive = false;

			// Tags list

			storeTags = new ListStore (typeof(string));
			listTags.Model = storeTags;
			listTags.SearchColumn = -1; // disable the interactive search
			listTags.HeadersVisible = true;

			SemanticModelAttribute tagsModelAttr = new SemanticModelAttribute ("storeTags__Name");
			TypeDescriptor.AddAttributes (storeTags, tagsModelAttr);

			listTags.AppendColumn (GettextCatalog.GetString ("Tag"), new CellRendererText (), "text", 0);

			listTags.Selection.Changed += delegate {
				TreeIter it;
				buttonRemoveTag.Sensitive = buttonPushTag.Sensitive = listTags.Selection.GetSelected (out it);
			};
			buttonRemoveTag.Sensitive = buttonPushTag.Sensitive = false;

			// Fill data

			FillBranches ();
			FillRemotes ();
			FillTags ();
		}

		async void FillBranches ()
		{
			var state = new TreeViewState (listBranches, 3);
			state.Save ();
			storeBranches.Clear ();
			var token = destroyTokenSource.Token;
			string currentBranch = await repo.GetCurrentBranchAsync (token);
			if (token.IsCancellationRequested)
				return;
			foreach (var branch in await repo.GetBranchesAsync (token)) {
				if (token.IsCancellationRequested)
					return;
				string text = branch.FriendlyName == currentBranch ? "<b>" + branch.FriendlyName + "</b>" : branch.FriendlyName;
				storeBranches.AppendValues (branch, text, branch.IsTracking ? branch.TrackedBranch.FriendlyName : String.Empty, branch.FriendlyName);
			}
			state.Load ();
		}

		async void FillRemotes ()
		{
			try {
				var state = new TreeViewState (treeRemotes, 4);
				state.Save ();
				storeRemotes.Clear ();
				var token = destroyTokenSource.Token;
				string currentRemote = await repo.GetCurrentRemoteAsync (token);
				if (token.IsCancellationRequested)
					return;
				foreach (Remote remote in await repo.GetRemotesAsync (token)) {
					if (token.IsCancellationRequested)
						return;
					// Take into account fetch/push ref specs.
					string text = remote.Name == currentRemote ? "<b>" + remote.Name + "</b>" : remote.Name;
					string url = remote.Url;
					TreeIter it = storeRemotes.AppendValues (remote, text, url, null, remote.Name);

					var remoteBranches = await repo.GetRemoteBranchesAsync (remote.Name, token);
					if (token.IsCancellationRequested)
						return;
					foreach (string branch in remoteBranches)
						storeRemotes.AppendValues (it, null, branch, null, branch, remote.Name + "/" + branch);
				}
				state.Load ();
			} catch (Exception e) {
				LoggingService.LogInternalError (e);
			}
		}

		async void FillTags ()
		{
			storeTags.Clear ();
			var token = destroyTokenSource.Token;
			var tags = await repo.GetTagsAsync (token);
			if (token.IsCancellationRequested)
				return;
			foreach (string tag in tags) {
				storeTags.AppendValues (tag);
			}
		}

		CancellationTokenSource destroyTokenSource = new CancellationTokenSource ();

		protected override void OnDestroyed ()
		{
			destroyTokenSource.Cancel ();

			base.OnDestroyed ();
			if (this.repo != null) {
				this.repo.Dispose ();
				this.repo = null;
			}
		}

		protected virtual async void OnButtonAddBranchClicked (object sender, EventArgs e)
		{
			var dlg = new EditBranchDialog (repo);
			try {
				if (MessageService.RunCustomDialog (dlg) == (int)ResponseType.Ok) {
					var token = destroyTokenSource.Token;
					await repo.CreateBranchAsync (dlg.BranchName, dlg.TrackSource, dlg.TrackRef);
					if (!token.IsCancellationRequested)
						FillBranches ();
				}
			} catch (Exception ex) {
				MessageService.ShowError (GettextCatalog.GetString ("The branch could not be created"), ex);
			} finally {
				dlg.Destroy ();
				dlg.Dispose ();
			}
		}

		protected virtual async void OnButtonEditBranchClicked (object sender, EventArgs e)
		{
			TreeIter it;
			if (!listBranches.Selection.GetSelected (out it))
				return;
			var b = (Branch) storeBranches.GetValue (it, 0);
			var dlg = new EditBranchDialog (repo, b.FriendlyName, b.IsTracking ? b.TrackedBranch.FriendlyName : String.Empty);
			try {
				var token = destroyTokenSource.Token;
				if (MessageService.RunCustomDialog (dlg) == (int) ResponseType.Ok) {
					if (dlg.BranchName != b.FriendlyName) {
						try {
							await repo.RenameBranchAsync (b.FriendlyName, dlg.BranchName);
						} catch (Exception ex) {
							MessageService.ShowError (GettextCatalog.GetString ("The branch could not be renamed"), ex);
						}
					}
					await repo.SetBranchTrackRefAsync (dlg.BranchName, dlg.TrackSource, dlg.TrackRef);
					if (!token.IsCancellationRequested)
						FillBranches ();
				}
			} finally {
				dlg.Destroy ();
				dlg.Dispose ();
			}
		}

		protected virtual async void OnButtonRemoveBranchClicked (object sender, EventArgs e)
		{
			TreeIter it;
			if (!listBranches.Selection.GetSelected (out it))
				return;
			var b = (Branch) storeBranches.GetValue (it, 0);
			string txt = null;
			if (!await repo.IsBranchMergedAsync (b.FriendlyName))
				txt = GettextCatalog.GetString ("WARNING: The branch has not yet been merged to HEAD");
			if (MessageService.Confirm (GettextCatalog.GetString ("Are you sure you want to delete the branch '{0}'?", b.FriendlyName), txt, AlertButton.Delete)) {
				try {
					var token = destroyTokenSource.Token;
					await repo.RemoveBranchAsync (b.FriendlyName);
					if (!token.IsCancellationRequested)
						FillBranches ();
				} catch (Exception ex) {
					MessageService.ShowError (GettextCatalog.GetString ("The branch could not be deleted"), ex);
				}
			}
		}

		protected virtual async void OnButtonSetDefaultBranchClicked (object sender, EventArgs e)
		{
			TreeIter it;
			if (!listBranches.Selection.GetSelected (out it))
				return;
			var b = (Branch) storeBranches.GetValue (it, 0);
			var token = destroyTokenSource.Token;
			if (await GitService.SwitchToBranchAsync (repo, b.FriendlyName) && !token.IsCancellationRequested)
				FillBranches ();
		}

		protected virtual async void OnButtonAddRemoteClicked (object sender, EventArgs e)
		{
			var dlg = new EditRemoteDialog (repo, null);
			try {
				if (MessageService.RunCustomDialog (dlg) == (int) ResponseType.Ok) {
					var token = destroyTokenSource.Token;
					await repo.AddRemoteAsync (dlg.RemoteName, dlg.RemoteUrl, dlg.ImportTags);
					if (!token.IsCancellationRequested)
						FillRemotes ();
				}
			} finally {
				dlg.Destroy ();
				dlg.Dispose ();
			}
		}

		protected virtual async void OnButtonEditRemoteClicked (object sender, EventArgs e)
		{
			TreeIter it;
			if (!treeRemotes.Selection.GetSelected (out it))
				return;

			var remote = (Remote) storeRemotes.GetValue (it, 0);
			if (remote == null)
				return;

			var dlg = new EditRemoteDialog (repo, remote);
			try {
				if (MessageService.RunCustomDialog (dlg) == (int) ResponseType.Ok) {
					var token = destroyTokenSource.Token;
					if (remote.Url != dlg.RemoteUrl)
						await repo.ChangeRemoteUrlAsync (remote.Name, dlg.RemoteUrl);
					if (remote.PushUrl != dlg.RemotePushUrl)
						await repo.ChangeRemotePushUrlAsync (remote.Name, dlg.RemotePushUrl);

					// Only do rename after we've done previous changes.
					if (remote.Name != dlg.RemoteName)
						await repo.RenameRemoteAsync (remote.Name, dlg.RemoteName);
					if (!token.IsCancellationRequested)
						FillRemotes ();
				}
			} finally {
				dlg.Destroy ();
				dlg.Dispose ();
			}
		}

		protected virtual async void OnButtonRemoveRemoteClicked (object sender, EventArgs e)
		{
			TreeIter it;
			if (!treeRemotes.Selection.GetSelected (out it))
				return;

			var remote = (Remote) storeRemotes.GetValue (it, 0);
			if (remote == null)
				return;

			if (MessageService.Confirm (GettextCatalog.GetString ("Are you sure you want to delete the remote '{0}'?", remote.Name), AlertButton.Delete)) {
				var token = destroyTokenSource.Token;
				await repo.RemoveRemoteAsync (remote.Name);
				if (!token.IsCancellationRequested)
					FillRemotes ();
			}
		}

		void UpdateRemoteButtons ()
		{
			TreeIter it;
			if (!treeRemotes.Selection.GetSelected (out it)) {
				buttonAddRemote.Sensitive = buttonEditRemote.Sensitive = buttonRemoveRemote.Sensitive = buttonTrackRemote.Sensitive = false;
				return;
			}
			var remote = (Remote) storeRemotes.GetValue (it, 0);
			buttonTrackRemote.Sensitive = remote == null;
			buttonAddRemote.Sensitive = buttonEditRemote.Sensitive = buttonRemoveRemote.Sensitive = remote != null;
		}

		protected virtual async void OnButtonTrackRemoteClicked (object sender, EventArgs e)
		{
			TreeIter it;
			if (!treeRemotes.Selection.GetSelected (out it))
				return;
			string branchName = (string) storeRemotes.GetValue (it, 3);
			if (branchName == null)
				return;

			storeRemotes.IterParent (out it, it);
			var remote = (Remote) storeRemotes.GetValue (it, 0);

			var dlg = new EditBranchDialog (repo, branchName, remote.Name + "/" + branchName);
			try {
				if (MessageService.RunCustomDialog (dlg) == (int) ResponseType.Ok) {
					var token = destroyTokenSource.Token;
					await repo.CreateBranchAsync (dlg.BranchName, dlg.TrackSource, dlg.TrackRef);
					if (!token.IsCancellationRequested)
						FillBranches ();
				}
			} finally {
				dlg.Destroy ();
				dlg.Dispose ();
			}
		}

		protected async void OnButtonNewTagClicked (object sender, EventArgs e)
		{
			using (var dlg = new GitSelectRevisionDialog (repo)) {
				Xwt.WindowFrame parent = Xwt.Toolkit.CurrentEngine.WrapWindow (this);
				if (dlg.Run (parent) != Xwt.Command.Ok)
					return;

				var token = destroyTokenSource.Token;
				await repo.AddTagAsync (dlg.TagName, dlg.SelectedRevision, dlg.TagMessage, token);
				if (!token.IsCancellationRequested)
					FillTags ();
			}
		}

		protected async void OnButtonRemoveTagClicked (object sender, EventArgs e)
		{
			TreeIter it;
			if (!listTags.Selection.GetSelected (out it))
				return;

			string tagName = (string) storeTags.GetValue (it, 0);
			var token = destroyTokenSource.Token;
			await repo.RemoveTagAsync (tagName, token);
			if (!token.IsCancellationRequested)
				FillTags ();
		}

		protected async void OnButtonPushTagClicked (object sender, EventArgs e)
		{
			TreeIter it;
			if (!listTags.Selection.GetSelected (out it))
				return;

			string tagName = (string)storeTags.GetValue (it, 0);
			var monitor = new Ide.ProgressMonitoring.MessageDialogProgressMonitor (true, false, false, true);
			try {
				monitor.BeginTask (GettextCatalog.GetString ("Pushing Tag"), 1);
				monitor.Log.WriteLine (GettextCatalog.GetString ("Pushing Tag '{0}' to '{1}'", tagName, repo.Url));
				await repo.PushTagAsync (tagName, destroyTokenSource.Token);
				monitor.Step (1);
				monitor.EndTask ();
			} catch (Exception ex) {
				monitor.ReportError (GettextCatalog.GetString ("Pushing tag failed"), ex);
			} finally {
				monitor.Dispose ();
			}
		}

		protected async void OnButtonFetchClicked (object sender, EventArgs e)
		{
			if (!treeRemotes.Selection.GetSelected (out var it))
				return;

			bool toplevel = !storeRemotes.IterParent (out var parent, it);

			string remoteName = string.Empty;

			if (toplevel) {
				remoteName = (string)storeRemotes.GetValue (it, 4);
			} else {
				remoteName = (string)storeRemotes.GetValue (parent, 4);
			}

			if (string.IsNullOrEmpty(remoteName))
				return;

			var monitor = VersionControlService.GetProgressMonitor (GettextCatalog.GetString ("Fetching remote...")).WithCancellationSource (destroyTokenSource);
			try {
				await repo.FetchAsync (monitor, remoteName);
			} catch (Exception ex) {
				monitor.ReportError (GettextCatalog.GetString ("Fetching remote failed"), ex);
			} finally {
				monitor.Dispose ();
			}
			FillRemotes ();
		}
	}
}
