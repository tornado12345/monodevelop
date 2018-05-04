﻿//
// DotNetCoreSdkLocationWidget.cs
//
// Author:
//       Matt Ward <matt.ward@microsoft.com>
//
// Copyright (c) 2018 Microsoft
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
using System.Text;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using Xwt.Drawing;

namespace MonoDevelop.DotNetCore.Gui
{
	partial class DotNetCoreSdkLocationWidget
	{
		DotNetCoreSdkLocationPanel panel;

		public DotNetCoreSdkLocationWidget (DotNetCoreSdkLocationPanel panel)
		{
			this.panel = panel;

			Build ();

			string location = panel.LoadSdkLocationSetting ();
			locationFileSelector.FileName = location ?? string.Empty;

			locationFileSelector.FileChanged += LocationChanged;

			LocationChanged (null, null);
		}

		void LocationChanged (object sender, EventArgs e)
		{
			Validate ();
			UpdateFileSelectorDirectory ();
		}

		void Validate ()
		{
			FilePath location = CleanPath (locationFileSelector.FileName);
			panel.ValidateSdkLocation (location);

			ShowDotNetCoreInformation ();
		}

		void UpdateFileSelectorDirectory ()
		{
			FilePath location = CleanPath (locationFileSelector.FileName);
			if (location.IsNull) {
				locationFileSelector.CurrentFolder = null;
			} else {
				locationFileSelector.CurrentFolder = location.ParentDirectory;
			}
		}

		FilePath CleanPath (FilePath path)
		{
			if (path.IsNullOrEmpty) {
				return null;
			}

			try {
				return path.FullPath;
			} catch {
				return null;
			}
		}

		public void ApplyChanges ()
		{
			panel.SaveSdkLocationSetting (CleanPath (locationFileSelector.FileName));
		}

		void ShowDotNetCoreInformation ()
		{
			if (panel.DotNetCorePath?.Exists == true && panel.RuntimeVersions.Any ()) {
				commandLineFoundLabel.Text = GettextCatalog.GetString ("Found");
				commandLineFoundIcon.Image = GetIcon (Gtk.Stock.Apply);
				UpdateCommandLineIconAccessibility (true);
			} else {
				commandLineFoundLabel.Text = GettextCatalog.GetString ("Not found");
				commandLineFoundIcon.Image = GetIcon (Gtk.Stock.Cancel);
				UpdateCommandLineIconAccessibility (false);
			}

			if (panel.SdkPaths.Exist) {
				ShowSdkInformation (panel.SdkPaths.SdkVersions);
			} else {
				ShowNoSdkFound ();
			}

			if (panel.RuntimeVersions.Any ()) {
				ShowRuntimes (panel.RuntimeVersions);
			} else {
				ShowNoRuntimesFound ();
			}
		}

		void ShowSdkInformation (DotNetCoreVersion[] versions)
		{
			sdkFoundLabel.Text = GettextCatalog.GetPluralString ("SDK found", "SDKs found", versions.Length);
			sdkFoundIcon.Image = GetIcon (Gtk.Stock.Apply);
			UpdateSdkIconAccessibility (true);

			sdkVersionsListBox.Items.Clear ();

			foreach (var version in versions) {
				sdkVersionsListBox.Items.Add (version.OriginalString);
			}
		}

		static Image GetIcon (string name)
		{
			return ImageService.GetIcon (name, Gtk.IconSize.Menu);
		}

		void ShowNoSdkFound ()
		{
			sdkFoundLabel.Text = GettextCatalog.GetString ("Not found");
			sdkFoundIcon.Image = GetIcon (Gtk.Stock.Cancel);
			sdkVersionsListBox.Items.Clear ();
			UpdateSdkIconAccessibility (false);
		}

		void ShowRuntimes (DotNetCoreVersion[] versions)
		{
			runtimeFoundLabel.Text = GettextCatalog.GetPluralString ("Runtime found", "Runtimes found", versions.Length);
			runtimeFoundIcon.Image = GetIcon (Gtk.Stock.Apply);
			UpdateRuntimeIconAccessibility (true);

			runtimeVersionsListBox.Items.Clear ();

			foreach (var version in versions) {
				runtimeVersionsListBox.Items.Add (version.OriginalString);
			}
		}

		void ShowNoRuntimesFound ()
		{
			runtimeFoundLabel.Text = GettextCatalog.GetString ("Not found");
			runtimeFoundIcon.Image = GetIcon (Gtk.Stock.Cancel);
			runtimeVersionsListBox.Items.Clear ();
			UpdateRuntimeIconAccessibility (false);
		}
	}
}
