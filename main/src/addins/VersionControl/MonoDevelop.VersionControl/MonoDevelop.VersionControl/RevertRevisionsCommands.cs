// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
// Copyright (c) 2008 Novell, Inc. (http://www.novell.com)
//
// Authors:
//      Andres G. Aragoneses <aaragoneses@novell.com>
//

using System;
using System.IO;

using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide;
using System.Threading.Tasks;
using System.Threading;

namespace MonoDevelop.VersionControl
{
	internal class RevertRevisionsCommands
	{
		public static Task<bool> RevertRevisionAsync (Repository vc, string path, Revision revision, bool test, CancellationToken cancellationToken = default)
		{
			return RevertRevisionsAsync (vc, path, revision, test, false, cancellationToken);
		}

		public static Task<bool> RevertToRevisionAsync (Repository vc, string path, Revision revision, bool test, CancellationToken cancellationToken = default)
		{
			return RevertRevisionsAsync (vc, path, revision, test, true, cancellationToken);
		}

		private static async Task<bool> RevertRevisionsAsync (Repository vc, string path, Revision revision, bool test, bool toRevision, CancellationToken cancellationToken)
		{
			try {
				if (test) {
					if (vc.TryGetVersionInfo (path, out var info))
						return info.CanRevert;
					return false;
				}

				string question = GettextCatalog.GetString (
				  "Are you sure you want to revert the selected resources to the revision specified (all local changes will be discarded)?");

				if (!toRevision)
					question = GettextCatalog.GetString (
					  "Are you sure you want to revert the changes from the revision selected on these resources?");

				if (MessageService.AskQuestion (question,
												GettextCatalog.GetString ("Note: The reversion will occur in your working copy, so you will still need to perform a commit to complete it."),
												AlertButton.Cancel, AlertButton.Revert) != AlertButton.Revert)
					return false;

				await new RevertWorker (vc, path, revision, toRevision).StartAsync (cancellationToken);
				return true;
			} catch (OperationCanceledException) {
				return false;
			} catch (Exception ex) {
				if (test)
					LoggingService.LogError (ex.ToString ());
				else
					MessageService.ShowError (GettextCatalog.GetString ("Version control command failed."), ex);
				return false;
			}
		}

		private class RevertWorker : VersionControlTask {
			Repository vc;
			string path;
			Revision revision;
			bool toRevision;

			public RevertWorker(Repository vc, string path, Revision revision, bool toRevision) {
				this.vc = vc;
				this.path = path;
				this.revision = revision;
				this.toRevision = toRevision;
			}

			protected override string GetDescription() {
				if (toRevision)
					return GettextCatalog.GetString ("Reverting to revision {0}...", revision);
				else
					return GettextCatalog.GetString ("Reverting revision {0}...", revision);
			}
			protected override async Task RunAsync ()
			{
				try {
					// A revert operation can create or remove a directory, so the directory
					// check must be done before and after the revert.

					bool isDir = Directory.Exists (path);

					if (toRevision) {
						//we discard working changes (we are warning the user), it's the more intuitive action
						await vc.RevertAsync (path, true, Monitor);

						await vc.RevertToRevisionAsync (path, revision, Monitor);
					} else {
						await vc.RevertRevisionAsync (path, revision, Monitor);
					}

					if (!(isDir || Directory.Exists (path)))
						isDir = false;

					Gtk.Application.Invoke ((o, args) => {
						if (!isDir) {
							// Reload reverted files
							Document doc = IdeApp.Workbench.GetDocument (path);
							if (doc != null)
								doc.Reload ();
							VersionControlService.NotifyFileStatusChanged (new FileUpdateEventArgs (vc, path, false));
						} else {
							VersionControlService.NotifyFileStatusChanged (new FileUpdateEventArgs (vc, path, true));
						}
					});
					Monitor.ReportSuccess (GettextCatalog.GetString ("Revert operation completed."));
				} catch (OperationCanceledException) {
					return;
				} catch (Exception ex) {
					LoggingService.LogError ("Revert operation failed", ex);
					Monitor.ReportError (ex.Message, null);
				}
			}
		}

	}
}
