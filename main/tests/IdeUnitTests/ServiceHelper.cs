//
// ServiceHelper.cs
//
// Author:
//       Lluis Sanchez <llsan@microsoft.com>
//
// Copyright (c) 2019 Microsoft
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
using System.Threading.Tasks;
using MonoDevelop.Ide.Gui.Documents;
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Shell;

namespace IdeUnitTests
{
	public static class ServiceHelper
	{
		public static BasicServiceProvider SetupMockShell ()
		{
			var serviceProvider = new BasicServiceProvider ();

			serviceProvider.RegisterServiceType<IShell, MockShell> ();
			serviceProvider.RegisterServiceType<ProgressMonitorManager, MockProgressMonitorManager>();

			return serviceProvider;
		}

		public static async Task ForceShow (this Document doc)
		{
			var shell = (await Runtime.GetService<IShell> ()) as MockShell;
			if (shell != null)
				await shell.ShowDocument (doc);
		}

		public static MockShellWindow GetWindow (this Document doc)
		{
			var shell = Runtime.PeekService<IShell> () as MockShell;
			return shell?.GetWindow (doc);
		}
	}
}
