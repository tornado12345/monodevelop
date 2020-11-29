//
// BlameCommand.cs
//
// Author:
//       Alan McGovern <alan@xamarin.com>
//
// Copyright 2011, Xamarin Inc.
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

using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Editor;
using Mono.Addins;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using MonoDevelop.VersionControl.Views;

namespace MonoDevelop.VersionControl
{
	public class BlameCommand
	{
		internal static readonly string BlameViewHandlers = "/MonoDevelop/VersionControl/BlameViewHandler";
		
		static async Task<bool> CanShow (VersionControlItem item)
		{
			var controller = IdeApp.Workbench.GetDocument (item.Path)?.DocumentController;

			return !item.IsDirectory
				// FIXME: Review appending of Annotate support and use it.
				&& (await item.GetVersionInfoAsync ()).IsVersioned
				&& AddinManager.GetExtensionObjects<IVersionControlViewHandler> (BlameViewHandlers).Any (h => h.CanHandle (item, controller));
		}

		public static async Task<bool> Show (VersionControlItemList items, bool test)
		{
			if (test) {
				foreach (var item in items) {
					if (!await CanShow (item))
						return false;
				}
				return true;
			}

			foreach (var item in items) {
				var document = await IdeApp.Workbench.OpenDocument (item.Path, item.ContainerProject, OpenDocumentOptions.Default | OpenDocumentOptions.OnlyInternalViewer);
				if (document == null)
					continue;
				document.RunWhenContentAdded<ITextView> (tv => {
					document.GetContent<VersionControlDocumentController> ()?.ShowBlameView ();
				});
			}

			return true;
		}
	}
}
