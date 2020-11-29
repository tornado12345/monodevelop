
using System;
using System.Collections.Generic;
using System.Linq;
using MonoDevelop.Components;
using MonoDevelop.Components.AtkCocoaHelper;
using MonoDevelop.Core;

namespace MonoDevelop.VersionControl
{
	public partial class UrlBasedRepositoryEditor : Gtk.Bin, IRepositoryEditor
	{
		UrlBasedRepository repo;
		public event EventHandler<EventArgs> PathChanged;
		public event EventHandler<EventArgs> UrlChanged;
		bool updating;
		List<string> protocols = new List<string> ();

		public UrlBasedRepositoryEditor (UrlBasedRepository repo)
		{
			Build ();

			labelError.Markup = "<small><span color='" + Ide.Gui.Styles.ErrorForegroundColor.ToHexString (false) + "'>"
				+ GettextCatalog.GetString ("Invalid URL") + "</span></small>";

			protocols = new List<string> (repo.SupportedProtocols);
			protocols.AddRange (repo.SupportedNonUrlProtocols);

			this.repo = repo;
			foreach (string p in protocols)
				comboProtocol.AppendText (p);

			updating = true;
			repositoryUrlEntry.Text = repo.Url;
			repositoryPortSpin.Adjustment.Lower = -1.0;
			Fill ();
			UpdateControls ();
			updating = false;
			SetupAccessibility ();
		}

		Control IRepositoryEditor.Widget {
			get { return this; }
		}

		void SetupAccessibility ()
		{
			repositoryUrlEntry.SetCommonAccessibilityAttributes ("UrlBasedRepositoryEditor.repositoryUrlEntry", "",
											GettextCatalog.GetString ("Enter the repository url"));
			repositoryUrlEntry.SetAccessibilityLabelRelationship (label4);

			repositoryUrlEntry.SetCommonAccessibilityAttributes ("UrlBasedRepositoryEditor.comboProtocol", "",
											GettextCatalog.GetString ("Enter the protocol to use"));
			comboProtocol.SetAccessibilityLabelRelationship (label5);

			repositoryServerEntry.SetCommonAccessibilityAttributes ("UrlBasedRepositoryEditor.repositoryServerEntry", "",
								GettextCatalog.GetString ("Enter the server"));
			repositoryServerEntry.SetAccessibilityLabelRelationship (label11);

			repositoryPortSpin.SetCommonAccessibilityAttributes ("UrlBasedRepositoryEditor.repositoryPortSpin", "",
					GettextCatalog.GetString ("Enter the port"));
			repositoryPortSpin.SetAccessibilityLabelRelationship (label6);

			repositoryPathEntry.SetCommonAccessibilityAttributes ("UrlBasedRepositoryEditor.repositoryPathEntry", "",
					GettextCatalog.GetString ("Enter the path"));
			repositoryPathEntry.SetAccessibilityLabelRelationship (label7);

			repositoryUserEntry.SetCommonAccessibilityAttributes ("UrlBasedRepositoryEditor.repositoryUserEntry", "",
					GettextCatalog.GetString ("Enter the user"));
			repositoryUserEntry.SetAccessibilityLabelRelationship (label8);
		}


		public bool Validate ()
		{
			if (!repo.IsUrlValid (repositoryUrlEntry.Text) || !CanCreateUri ()) {
				labelError.Show ();
				return false;
			}
			return true;
		}

		public bool CanCreateUri()
		{
			if (string.IsNullOrEmpty (repositoryUrlEntry.Text))
				return false;

			return Uri.TryCreate (repositoryUrlEntry.Text, UriKind.RelativeOrAbsolute, out Uri serverUri);
		}

		public string RelativePath {
			get { return repositoryPathEntry.Text; }
		}

		public string RepositoryServer {
			get { return repositoryServerEntry.Text; }
		}

		bool ParseSSHUrl (string url)
		{
			if (!url.Contains (':'))
				return false;
			
			var tokens = url.Split (new [] { ':' }, 2);
			if (tokens.Length < 2)
				return false;
			
			if (!Uri.IsWellFormedUriString (tokens [0], UriKind.RelativeOrAbsolute) ||
				!Uri.IsWellFormedUriString (tokens [1], UriKind.RelativeOrAbsolute))
				return false;

			var userAndHost = tokens [0].Split (new [] { '@' }, 2);
			if (userAndHost.Length < 2)
				return false;
			
			repositoryUserEntry.Text = userAndHost [0];
			repositoryServerEntry.Text = userAndHost [1];
			repositoryPortSpin.Value = 22;
			string path = tokens [1];
			if (!path.StartsWith ("/", StringComparison.Ordinal)) {
				path = "/" + path;
			}
			repositoryPathEntry.Text = path;
			comboProtocol.Active = protocols.IndexOf ("ssh");
			comboProtocol.Sensitive = false;
			PathChanged?.Invoke (this, EventArgs.Empty);
			UrlChanged?.Invoke (this, EventArgs.Empty);
			return true;
		}
		
		void Fill ()
		{
			comboProtocol.Sensitive = true;
			if (repo.Uri != null && repo.Uri.IsAbsoluteUri) {
				if (repo.Name == repositoryServerEntry.Text)
					repo.Name = repo.Uri.Host;
				repositoryServerEntry.Text = repo.Uri.Host;
				repositoryPortSpin.Value = repo.Uri.Port;
				repositoryPathEntry.Text = repo.Uri.PathAndQuery;
				repositoryUserEntry.Text = repo.Uri.UserInfo;
				comboProtocol.Active = protocols.IndexOf (repo.Uri.Scheme);
				PathChanged?.Invoke (this, EventArgs.Empty);
			} else if (!ParseSSHUrl (repo.Url)) {
				// The url may have a scheme, but it may be an incomplete or incorrect url. Do the best to select
				// the correct value in the protocol combo
				string prot = repo.SupportedProtocols.FirstOrDefault (p => repo.Url.StartsWith (p + "://", StringComparison.Ordinal));
				if (prot != null) {
					repositoryServerEntry.Text = string.Empty;
					repositoryPortSpin.Value = 0;
					repositoryPathEntry.Text = string.Empty;
					repositoryUserEntry.Text = string.Empty;
					comboProtocol.Active = protocols.IndexOf (prot);
				}
				else
					comboProtocol.Active = protocols.IndexOf (repo.Protocol);
			}
		}

		protected virtual void OnRepositoryUrlEntryChanged(object sender, System.EventArgs e)
		{
			if (!updating) {
				updating = true;
				repo.Url = repositoryUrlEntry.Text;
				Fill ();
				UpdateControls ();
				labelError.Hide ();
				updating = false;
			}
		}
		
		void UpdateUrl ()
		{
			updating = true;
			repositoryUrlEntry.Text = repo.Url;
			if (repo.Uri != null && repo.Name == repositoryServerEntry.Text) {
				if (repo.Uri.IsAbsoluteUri)
					repo.Name = repo.Uri.Host;
			}
			updating = false;
			UrlChanged?.Invoke (this, EventArgs.Empty);
		}
		
		void UpdateControls ()
		{
			if (repo.Uri != null || repo.SupportedProtocols.Any (p => repositoryUrlEntry.Text.StartsWith (p + "://", StringComparison.Ordinal))) {
				repositoryPathEntry.Sensitive = true;
				bool isUrl = IsUrl;
				repositoryServerEntry.Sensitive = isUrl;
				repositoryUserEntry.Sensitive = isUrl;
				repositoryPortSpin.Sensitive = isUrl;
			} else {
				repositoryPathEntry.Sensitive = false;
				repositoryServerEntry.Sensitive = false;
				repositoryUserEntry.Sensitive = false;
				repositoryPortSpin.Sensitive = false;
			}
		}

		const string FileProtocol = "file";
		bool IsUrl => Protocol != FileProtocol;

		void SetRepoUrl ()
		{
			if (!repo.SupportedProtocols.Contains (Protocol)) {
				repo.Url = string.Empty;
				return;
			}

			if (IsUrl) {
				var ub = new UriBuilder ();
				ub.Scheme = Protocol;
				ub.Host = repositoryServerEntry.Text;
				ub.UserName = repositoryUserEntry.Text;
				ub.Port = (int)repositoryPortSpin.Value;
				ub.Path = repositoryPathEntry.Text;

				if (string.IsNullOrEmpty (ub.Host)) {
					repo.Url = string.Format ("{0}://", Protocol);
				} else {
					repo.Url = ub.ToString ();
				}
			} else {
				repo.Url = string.Format ("{0}://{1}", Protocol, repositoryPathEntry.Text);
			}
		}

		protected virtual void OnRepositoryServerEntryChanged(object sender, System.EventArgs e)
		{
			if (updating) return;
			SetRepoUrl ();
			UpdateUrl ();
		}

		protected virtual void OnRepositoryPathEntryChanged(object sender, System.EventArgs e)
		{
			if (updating) return;
			SetRepoUrl ();
			UpdateUrl ();
		}

		protected virtual void OnRepositoryUserEntryChanged(object sender, System.EventArgs e)
		{
			if (updating) return;
			SetRepoUrl ();
			UpdateUrl ();
		}

		protected virtual void OnComboProtocolChanged(object sender, System.EventArgs e)
		{
			if (updating) return;
			SetRepoUrl ();
			UpdateUrl ();
			UpdateControls ();
		}

		protected virtual void OnRepositoryPortSpinValueChanged (object sender, System.EventArgs e)
		{
			if (updating) return;
			SetRepoUrl ();
			UpdateUrl ();
		}
		
		string Protocol {
			get {
				return comboProtocol.Active != -1 ? protocols [comboProtocol.Active] : null;
			}
		}

		protected void OnRepositoryUrlEntryClipboardPasted (object sender, EventArgs e)
		{
			var clip = GetClipboard (Gdk.Atom.Intern ("CLIPBOARD", false));
			clip.RequestText (delegate (Gtk.Clipboard clp, string text) {
				if (string.IsNullOrEmpty (text))
					return;
				if (repo.IsUrlValid (text))
					repositoryUrlEntry.Text = text;
			});
		}
	}
}
