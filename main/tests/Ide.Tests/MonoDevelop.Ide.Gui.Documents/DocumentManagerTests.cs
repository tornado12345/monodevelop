//
// DocumentManagerTests.cs
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdeUnitTests;
using MonoDevelop.Components;
using MonoDevelop.Core;
using MonoDevelop.Ide.Fonts;
using MonoDevelop.Ide.Gui.Shell;
using MonoDevelop.Ide.TypeSystem;
using MonoDevelop.Projects;
using NUnit.Framework;
using UnitTests;

namespace MonoDevelop.Ide.Gui.Documents
{
	[RequireService (typeof (TypeSystemService))]
	[RequireService (typeof (FontService))]
	public class DocumentManagerTests : TestBase
	{
//		BasicServiceProvider serviceProvider;
		DocumentManager documentManager;
		DocumentManagerEventTracker eventTracker;
		DocumentControllerService documentControllerService;
		MockShell shell;

		[SetUp]
		public async Task Setup ()
		{
			Runtime.RegisterServiceType<IShell, MockShell> ();
			Runtime.RegisterServiceType<ProgressMonitorManager, MockProgressMonitorManager> ();

			//			serviceProvider = ServiceHelper.SetupMockShell ();
			documentManager = await Runtime.GetService<DocumentManager> ();
			shell = await Runtime.GetService<IShell> () as MockShell;
			documentControllerService = await Runtime.GetService<DocumentControllerService> ();

			while (documentManager.Documents.Count > 0)
				await documentManager.Documents [0].Close (true);

			eventTracker = new DocumentManagerEventTracker (documentManager);
		}

		[TearDown]
		public async Task TestTearDown ()
		{
			//await serviceProvider.Dispose ();
		}

		[Test]
		public async Task OpenCloseController ()
		{
			Assert.AreEqual (0, documentManager.Documents.Count);

			var doc = await documentManager.OpenDocument (new TestController ());

			Assert.AreEqual (1, documentManager.Documents.Count);

			Assert.AreEqual (1, eventTracker.DocumentOpenedEvents.Count);
			Assert.AreSame (doc, eventTracker.DocumentOpenedEvents [0].Document);

			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc, eventTracker.ActiveDocumentChangedEvents [0].Document);

			Assert.AreEqual (1, shell.Windows.Count);
			var window = shell.Windows [0];
			Assert.AreSame (doc, window.Document);

			eventTracker.Reset ();

			await doc.Close (true);

			Assert.AreEqual (0, shell.Windows.Count);

			Assert.AreEqual (0, eventTracker.DocumentOpenedEvents.Count);

			Assert.AreEqual (1, eventTracker.DocumentClosedEvents.Count);
			Assert.AreSame (doc, eventTracker.DocumentClosedEvents [0].Document);

			Assert.AreEqual (1, eventTracker.DocumentClosingEvents.Count);
			Assert.AreSame (doc, eventTracker.DocumentClosingEvents [0].Document);

			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.IsNull (eventTracker.ActiveDocumentChangedEvents [0].Document);
			Assert.IsNull (documentManager.ActiveDocument);
		}

		[Test]
		public async Task ActiveDocument ()
		{
			// Open one document

			Assert.AreEqual (0, documentManager.Documents.Count);

			var doc1 = await documentManager.OpenDocument (new TestController ());

			Assert.AreEqual (1, documentManager.Documents.Count);
			Assert.AreSame (doc1, documentManager.Documents [0]);
			Assert.AreSame (doc1, documentManager.ActiveDocument);

			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc1, eventTracker.ActiveDocumentChangedEvents [0].Document);

			// Open second

			eventTracker.Reset ();
			var doc2 = await documentManager.OpenDocument (new TestController (), false);

			Assert.AreEqual (2, documentManager.Documents.Count);
			Assert.AreSame (doc1, documentManager.Documents [0]);
			Assert.AreSame (doc2, documentManager.Documents [1]);
			Assert.AreEqual (0, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc1, documentManager.ActiveDocument);

			// Open third

			eventTracker.Reset ();
			var doc3 = await documentManager.OpenDocument (new TestController ());

			Assert.AreEqual (3, documentManager.Documents.Count);
			Assert.AreSame (doc1, documentManager.Documents [0]);
			Assert.AreSame (doc2, documentManager.Documents [1]);
			Assert.AreSame (doc3, documentManager.Documents [2]);
			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc3, eventTracker.ActiveDocumentChangedEvents [0].Document);
			Assert.AreSame (doc3, documentManager.ActiveDocument);

			// Select third again

			eventTracker.Reset ();
			doc3.Select ();

			Assert.AreEqual (0, eventTracker.ActiveDocumentChangedEvents.Count);

			// Select second

			eventTracker.Reset ();
			doc2.Select ();
			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc2, eventTracker.ActiveDocumentChangedEvents [0].Document);
			Assert.AreSame (doc2, documentManager.ActiveDocument);

			// Select first through underlying window

			eventTracker.Reset ();
			shell.Windows [0].SelectWindow ();
			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc1, eventTracker.ActiveDocumentChangedEvents [0].Document);
			Assert.AreSame (doc1, documentManager.ActiveDocument);

			// Select again is no-op

			eventTracker.Reset ();
			shell.Windows [0].SelectWindow ();
			Assert.AreEqual (0, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc1, documentManager.ActiveDocument);

			// Close unselects

			eventTracker.Reset ();
			await doc1.Close (true);
			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc2, eventTracker.ActiveDocumentChangedEvents [0].Document);
			Assert.AreSame (doc2, documentManager.ActiveDocument);

			eventTracker.Reset ();
			await doc2.Close (true);
			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.AreSame (doc3, eventTracker.ActiveDocumentChangedEvents [0].Document);
			Assert.AreSame (doc3, documentManager.ActiveDocument);

			eventTracker.Reset ();
			await doc3.Close (true);
			Assert.AreEqual (1, eventTracker.ActiveDocumentChangedEvents.Count);
			Assert.IsNull (documentManager.ActiveDocument);
		}

		[Test]
		public async Task PreventDocumentClose ()
		{
			var doc = await documentManager.OpenDocument (new TestController ());

			documentManager.DocumentClosing += DontClose;

			try {

				eventTracker.Reset ();

				bool result = await doc.Close ();
				Assert.IsFalse (result);

				Assert.AreEqual (1, shell.Windows.Count);
				Assert.AreEqual (1, documentManager.Documents.Count);
				Assert.AreEqual (0, eventTracker.DocumentClosedEvents.Count);
				Assert.AreEqual (0, eventTracker.ActiveDocumentChangedEvents.Count);
			} finally {
				documentManager.DocumentClosing -= DontClose;
			}
		}

		async Task DontClose (object s, DocumentCloseEventArgs e)
		{
			await Task.Delay (100);
			e.Cancel = true;
		}

		[Test]
		public async Task NewDocumentFileNameMayNotChange ()
		{
			const string newName = "SomeFileName.txt";
			var doc = await documentManager.NewDocument (newName, "text/plain", "");
			//Since this is just "unsaved/new" file, its expected that Name and FileName are matching
			Assert.AreEqual (doc.Name, doc.FileName.ToString ());
		}

		[Test]
		public async Task GetDocument ()
		{
			documentControllerService.RegisterFactory (new TestFileControllerFactory ());

			FilePath tempDir = FileService.CreateTempDirectory ();
			try {
				var file1 = tempDir.Combine ("aa", "bb", "foo1.test");
				var file2 = tempDir.Combine ("aa", "bb", "cc", "foo1.test");
				var file3 = tempDir.Combine ("aa", "bb", "cc", "..", "foo3.test");

				Directory.CreateDirectory (file1.ParentDirectory);
				Directory.CreateDirectory (file2.ParentDirectory);
				Directory.CreateDirectory (file3.ParentDirectory);

				File.WriteAllText (file1, "");
				File.WriteAllText (file2, "");
				File.WriteAllText (file3, "");

				var doc1 = await documentManager.OpenDocument (new FileOpenInformation (file1));
				var doc2 = await documentManager.OpenDocument (new FileOpenInformation (file2));
				var doc3 = await documentManager.OpenDocument (new FileOpenInformation (file3));

				Assert.AreEqual (3, documentManager.Documents.Count);
				Assert.AreSame (doc1, documentManager.Documents [0]);
				Assert.AreSame (doc2, documentManager.Documents [1]);
				Assert.AreSame (doc3, documentManager.Documents [2]);

				var sel = documentManager.GetDocument ("foo1.test");
				Assert.IsNull (sel);

				sel = documentManager.GetDocument ("a.test");
				Assert.IsNull (sel);

				sel = documentManager.GetDocument (file2);
				Assert.AreSame (doc2, sel);

				sel = documentManager.GetDocument (tempDir.Combine ("aa", "bb", "cc", "dd", "..", "foo1.test"));
				Assert.AreSame (doc2, sel);

				sel = documentManager.GetDocument (tempDir.Combine ("aa", "bb", "foo3.test"));
				Assert.AreSame (doc3, sel);
			} finally {
				Directory.Delete (tempDir, true);
			}
		}

		[Test]
		public async Task ActiveViewInHierarchy ()
		{
			var controller = new TestController ();
			await controller.Initialize (null, null);
			var view = await controller.GetDocumentView ();

			Assert.AreEqual (view, view.ActiveViewInHierarchy);

			var attached1 = new DocumentViewContent (c => Task.FromResult<Control> (null));
			var attached2 = new DocumentViewContent (c => Task.FromResult<Control> (null));
			view.AttachedViews.Add (attached1);
			view.AttachedViews.Add (attached2);

			Assert.AreEqual (view, view.ActiveViewInHierarchy);

			attached1.SetActive ();
			Assert.AreEqual (attached1, view.ActiveViewInHierarchy);
			Assert.AreEqual (attached1, attached1.ActiveViewInHierarchy);
			Assert.AreEqual (attached2, attached2.ActiveViewInHierarchy);

			attached2.SetActive ();
			Assert.AreEqual (attached2, view.ActiveViewInHierarchy);
			Assert.AreEqual (attached1, attached1.ActiveViewInHierarchy);
			Assert.AreEqual (attached2, attached2.ActiveViewInHierarchy);
		}

		[Test]
		public void ActiveViewInHierarchy2 ()
		{
			var root = new DocumentViewContainer () { Title = "root" };
			root.IsRoot = true;
			Assert.IsNull (root.ActiveViewInHierarchy);
			Assert.IsNull (root.ActiveView);

			var attached1 = new DocumentViewContent (c => Task.FromResult<Control> (null)) { Title = "attached1" };
			root.AttachedViews.Add (attached1);
			Assert.IsNull (root.ActiveView);
			Assert.IsNull (root.ActiveViewInHierarchy);

			var view1 = new DocumentViewContent (c => Task.FromResult<Control> (null)) { Title = "view1" };
			root.Views.Add (view1);
			Assert.AreEqual (view1, root.ActiveView);
			Assert.AreEqual (view1, root.ActiveViewInHierarchy);
			Assert.AreEqual (view1, view1.ActiveViewInHierarchy);

			attached1.SetActive ();
			Assert.AreEqual (view1, root.ActiveView);
			Assert.AreEqual (attached1, root.ActiveViewInHierarchy);

			root.SetActive ();
			Assert.AreEqual (view1, root.ActiveView);
			Assert.AreEqual (view1, root.ActiveViewInHierarchy);

			var view2 = new DocumentViewContent (c => Task.FromResult<Control> (null)) { Title = "view2" };
			root.Views.Add (view2);
			Assert.AreEqual (view1, root.ActiveView);
			Assert.AreEqual (view1, root.ActiveViewInHierarchy);
			Assert.AreEqual (view2, view2.ActiveViewInHierarchy);

			var container = new DocumentViewContainer ();
			root.Views.Add (container);
			Assert.AreEqual (view1, root.ActiveView);
			Assert.AreEqual (view1, root.ActiveViewInHierarchy);
			Assert.IsNull (container.ActiveViewInHierarchy);

			var subView1 = new DocumentViewContent (c => Task.FromResult<Control> (null)) { Title = "subView1" };
			container.Views.Add (subView1);
			Assert.AreEqual (view1, root.ActiveView);
			Assert.AreEqual (view1, root.ActiveViewInHierarchy);
			Assert.AreEqual (subView1, container.ActiveView);
			Assert.AreEqual (subView1, container.ActiveViewInHierarchy);
			Assert.AreEqual (subView1, subView1.ActiveViewInHierarchy);

			var subView2 = new DocumentViewContent (c => Task.FromResult<Control> (null)) { Title = "subView2" };
			container.Views.Add (subView2);
			Assert.AreEqual (view1, root.ActiveView);
			Assert.AreEqual (view1, root.ActiveViewInHierarchy);
			Assert.AreEqual (subView1, container.ActiveView);
			Assert.AreEqual (subView1, container.ActiveViewInHierarchy);
			Assert.AreEqual (subView2, subView2.ActiveViewInHierarchy);

			container.SetActive ();
			Assert.AreEqual (container, root.ActiveView);
			Assert.AreEqual (subView1, root.ActiveViewInHierarchy);
			Assert.AreEqual (subView1, container.ActiveViewInHierarchy);

			subView2.SetActive ();
			Assert.AreEqual (subView2, root.ActiveViewInHierarchy);
			Assert.AreEqual (subView2, container.ActiveViewInHierarchy);

			view2.SetActive ();
			Assert.AreEqual (view2, root.ActiveViewInHierarchy);
			Assert.AreEqual (subView2, container.ActiveViewInHierarchy);

			subView1.SetActive ();
			Assert.AreEqual (view2, root.ActiveViewInHierarchy);
			Assert.AreEqual (subView1, container.ActiveViewInHierarchy);

			container.SetActive ();
			Assert.AreEqual (subView1, root.ActiveViewInHierarchy);
			Assert.AreEqual (subView1, container.ActiveViewInHierarchy);
		}

		[Test]
		public async Task ReuseFileDocument ()
		{
			var f1 = new ReusableControllerFactory ();
			var f2 = new ReusableFileControllerFactory ();
			documentControllerService.RegisterFactory (f1);
			documentControllerService.RegisterFactory (f2);

			var foo_dll_test = GetTempFile (".dll_test");
			var bar_dll_test = GetTempFile (".dll_test");
			var bar_exe_test = GetTempFile (".exe_test");
			var foo_txt = GetTempFile (".txt");

			try {
				var controller = new ReusableFileController ();
				var descriptor = new FileDescriptor (foo_dll_test, null, null);
				await controller.Initialize (descriptor);
				var doc = await documentManager.OpenDocument (controller);
				Assert.NotNull (doc);

				var doc2 = await documentManager.OpenDocument (new FileDescriptor (foo_dll_test, null, null));
				Assert.AreSame (doc, doc2);

				doc2 = await documentManager.OpenDocument (new FileDescriptor (bar_dll_test, null, null));
				Assert.AreSame (doc, doc2);

				doc2 = await documentManager.OpenDocument (new FileDescriptor (bar_exe_test, null, null));
				Assert.AreSame (doc, doc2);

				doc2 = await documentManager.OpenDocument (new FileDescriptor (foo_txt, null, null));
				Assert.AreNotSame (doc, doc2);
				await doc2.Close ();

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (foo_dll_test));
				Assert.AreSame (doc, doc2);

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (bar_dll_test));
				Assert.AreSame (doc, doc2);

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (bar_exe_test));
				Assert.AreSame (doc, doc2);

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (foo_txt));
				Assert.AreNotSame (doc, doc2);
				await doc2.Close ();

				await documentManager.CloseAllDocuments (false);

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (foo_dll_test) { Options = OpenDocumentOptions.None });
				Assert.AreNotSame (doc, doc2);
				await doc2.Close ();

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (bar_dll_test) { Options = OpenDocumentOptions.None });
				Assert.AreNotSame (doc, doc2);
				await doc2.Close ();

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (bar_exe_test) { Options = OpenDocumentOptions.None });
				Assert.AreNotSame (doc, doc2);
				await doc2.Close ();

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (foo_txt) { Options = OpenDocumentOptions.None });
				Assert.AreNotSame (doc, doc2);
				await doc2.Close ();
			} finally {
				documentControllerService.UnregisterFactory (f1);
				documentControllerService.UnregisterFactory (f2);
				File.Delete (foo_dll_test);
				File.Delete (bar_dll_test);
				File.Delete (bar_exe_test);
				File.Delete (foo_txt);
			}
		}

		string GetTempFile (string extension)
		{
			var tempFile = Path.GetTempFileName ();
			var finalFile = tempFile + extension;
			File.Copy (GetType ().Assembly.Location, finalFile, true); // We need a binary file to avoid the file being opened in the text editor by default
			return finalFile;
		}

		[Test]
		public async Task ReuseFileDocumentChangingOwner()
		{
			var f2 = new ReusableFileControllerFactory ();
			documentControllerService.RegisterFactory (f2);
			var project = Services.ProjectService.CreateDotNetProject ("C#");
			var project2 = Services.ProjectService.CreateDotNetProject ("C#");

			var file = GetTempFile (".dll_test");

			try {
				var doc = await documentManager.OpenDocument (new FileOpenInformation (file, project));
				Assert.NotNull (doc);
				Assert.AreSame (project, doc.DocumentController.Owner);

				// Reuse

				var doc2 = await documentManager.OpenDocument (new FileOpenInformation (file, project));
				Assert.AreSame (doc, doc2);
				Assert.AreSame (project, doc.DocumentController.Owner);

				// Reuse, changing owner

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (file, project2));
				Assert.AreSame (doc, doc2);
				Assert.AreSame (project2, doc.DocumentController.Owner);

			} finally {
				documentControllerService.UnregisterFactory (f2);
				project.Dispose ();
				project2.Dispose ();
				File.Delete (file);
			}
		}

		[Test]
		public async Task ReuseFileDocumentWithNoReuseFlag ()
		{
			var f1 = new ReusableFileControllerFactory ();
			var f2 = new ReusableFileControllerFactory ();

			documentControllerService.RegisterFactory (f1);
			documentControllerService.RegisterFactory (f2);

			f1.Enabled = true;
			f2.Enabled = false;

			var project = Services.ProjectService.CreateDotNetProject ("C#");

			var file = GetTempFile (".dll_test");

			try {
				var doc = await documentManager.OpenDocument (new FileOpenInformation (file, project));
				Assert.NotNull (doc);
				var controller = (ReusableFileController)doc.DocumentController;
				controller.AllowReuse = false;

				// If the reusable flag is set, reuse the doc since even if TryReuseDocument returns false, the file names match.

				var doc2 = await documentManager.OpenDocument (new FileOpenInformation (file, project) { Options = OpenDocumentOptions.TryToReuseViewer });
				Assert.AreSame (doc, doc2);

				// If the reusable flag is not set, the document can't be reused, even if names match

				doc2 = await documentManager.OpenDocument (new FileOpenInformation (file, project) { Options = OpenDocumentOptions.None });
				Assert.AreNotSame (doc, doc2);

				// The old non-reusable document must have been closed
				Assert.AreEqual (1, documentManager.Documents.Count);

			} finally {
				documentControllerService.UnregisterFactory (f1);
				documentControllerService.UnregisterFactory (f2);
				project.Dispose ();
				File.Delete (file);	
			}
		}

		[Test]
		public async Task ReuseDocumentIdentifiedByDescriptor ()
		{
			var f1 = new ReusableControllerFactory ();
			documentControllerService.RegisterFactory (f1);
			try {
				var controller = new ReusableController ();
				var descriptor = new ReusableDescriptor ();
				await controller.Initialize (descriptor);
				var doc = await documentManager.OpenDocument (controller);

				Assert.NotNull (doc);

				var doc2 = await documentManager.OpenDocument (descriptor);
				Assert.AreSame (doc, doc2);

				var doc3 = await documentManager.OpenDocument (new ReusableDescriptor ());
				Assert.AreNotSame (doc, doc3);
			} finally {
				documentControllerService.UnregisterFactory (f1);
			}
		}

		[Test]
		public async Task RunWhenContentAdded ()
		{
			var controller = new ContentTestController ();
			await controller.Initialize (new ModelDescriptor ());
			var doc = await documentManager.OpenDocument (controller);

			var theContent = new SomeContent ();

			int totalEvents = 0;
			int contentAddedEvents = 0;
			int additionalContentAddedEvents = 0;

			var r1 = doc.RunWhenContentAdded<SomeContent> (c => {
				totalEvents++;
				if (c == theContent)
					contentAddedEvents++;
			});

			Assert.AreEqual (0, totalEvents);
			Assert.AreEqual (0, contentAddedEvents);
			Assert.AreEqual (0, additionalContentAddedEvents);

			controller.AddContent (theContent);

			Assert.AreEqual (1, totalEvents);
			Assert.AreEqual (1, contentAddedEvents);
			Assert.AreEqual (0, additionalContentAddedEvents);

			doc.RunWhenContentAdded<SomeContent> (c => {
				totalEvents++;
				if (c == theContent)
					additionalContentAddedEvents++;
			});

			Assert.AreEqual (2, totalEvents);
			Assert.AreEqual (1, contentAddedEvents);
			Assert.AreEqual (1, additionalContentAddedEvents);

			controller.RemoveContent (theContent);

			Assert.AreEqual (2, totalEvents);
			Assert.AreEqual (1, contentAddedEvents);
			Assert.AreEqual (1, additionalContentAddedEvents);

			controller.AddContent (theContent);

			Assert.AreEqual (4, totalEvents);
			Assert.AreEqual (2, contentAddedEvents);
			Assert.AreEqual (2, additionalContentAddedEvents);

			var oldContent = theContent;
			theContent = new SomeContent ();
			controller.ReplaceContent (oldContent, theContent);

			Assert.AreEqual (6, totalEvents);
			Assert.AreEqual (3, contentAddedEvents);
			Assert.AreEqual (3, additionalContentAddedEvents);

			r1.Dispose ();

			controller.RemoveContent (theContent);
			theContent = new SomeContent ();
			controller.AddContent (theContent);

			Assert.AreEqual (7, totalEvents);
			Assert.AreEqual (3, contentAddedEvents);
			Assert.AreEqual (4, additionalContentAddedEvents);
		}

		[Test]
		public async Task RunWhenContentRemoved ()
		{
			var controller = new ContentTestController ();
			await controller.Initialize (new ModelDescriptor ());
			var doc = await documentManager.OpenDocument (controller);

			var theContent = new SomeContent ();

			int totalEvents = 0;
			int contentEvents = 0;
			int additionalContentEvents = 0;

			var r1 = doc.RunWhenContentRemoved<SomeContent> (c => {
				totalEvents++;
				if (c == theContent)
					contentEvents++;
			});

			Assert.AreEqual (0, totalEvents);
			Assert.AreEqual (0, contentEvents);
			Assert.AreEqual (0, additionalContentEvents);

			controller.AddContent (theContent);

			Assert.AreEqual (0, totalEvents);
			Assert.AreEqual (0, contentEvents);
			Assert.AreEqual (0, additionalContentEvents);

			doc.RunWhenContentRemoved<SomeContent> (c => {
				totalEvents++;
				if (c == theContent)
					additionalContentEvents++;
			});

			Assert.AreEqual (0, totalEvents);
			Assert.AreEqual (0, contentEvents);
			Assert.AreEqual (0, additionalContentEvents);

			controller.RemoveContent (theContent);

			Assert.AreEqual (2, totalEvents);
			Assert.AreEqual (1, contentEvents);
			Assert.AreEqual (1, additionalContentEvents);

			controller.AddContent (theContent);

			Assert.AreEqual (2, totalEvents);
			Assert.AreEqual (1, contentEvents);
			Assert.AreEqual (1, additionalContentEvents);

			var newContent = new SomeContent ();
			controller.ReplaceContent (theContent, newContent);
			theContent = newContent;

			Assert.AreEqual (4, totalEvents);
			Assert.AreEqual (2, contentEvents);
			Assert.AreEqual (2, additionalContentEvents);

			r1.Dispose ();

			controller.RemoveContent (theContent);

			Assert.AreEqual (5, totalEvents);
			Assert.AreEqual (2, contentEvents);
			Assert.AreEqual (3, additionalContentEvents);
		}

		[Test]
		public async Task RunWhenContentAddedOrRemoved ()
		{
			var controller = new ContentTestController ();
			await controller.Initialize (new ModelDescriptor ());
			var doc = await documentManager.OpenDocument (controller);

			var addedContent = new SomeContent ();
			var removedContent = addedContent;

			int totalEvents = 0;
			int contentAddedEvents = 0;
			int contentRemovedEvents = 0;
			int additionalContentAddedEvents = 0;
			int additionalContentRemovedEvents = 0;

			var r1 = doc.RunWhenContentAddedOrRemoved<SomeContent> (
				added => {
					totalEvents++;
					if (added == addedContent)
						contentAddedEvents++;
				},
				removed => {
					totalEvents++;
					if (removed == removedContent)
						contentRemovedEvents++;
				}
			);

			Assert.AreEqual (0, totalEvents);
			Assert.AreEqual (0, contentAddedEvents);
			Assert.AreEqual (0, contentRemovedEvents);
			Assert.AreEqual (0, additionalContentAddedEvents);
			Assert.AreEqual (0, additionalContentRemovedEvents);

			controller.AddContent (addedContent);

			Assert.AreEqual (1, totalEvents);
			Assert.AreEqual (1, contentAddedEvents);
			Assert.AreEqual (0, contentRemovedEvents);
			Assert.AreEqual (0, additionalContentAddedEvents);
			Assert.AreEqual (0, additionalContentRemovedEvents);

			doc.RunWhenContentAddedOrRemoved<SomeContent> (
				added => {
					totalEvents++;
					if (added == addedContent)
						additionalContentAddedEvents++;
				},
				removed => {
					totalEvents++;
					if (removed == removedContent)
						additionalContentRemovedEvents++;
				}
			);

			Assert.AreEqual (2, totalEvents);
			Assert.AreEqual (1, contentAddedEvents);
			Assert.AreEqual (0, contentRemovedEvents);
			Assert.AreEqual (1, additionalContentAddedEvents);
			Assert.AreEqual (0, additionalContentRemovedEvents);

			controller.RemoveContent (addedContent);

			Assert.AreEqual (1, contentAddedEvents);
			Assert.AreEqual (1, contentRemovedEvents);
			Assert.AreEqual (1, additionalContentAddedEvents);
			Assert.AreEqual (1, additionalContentRemovedEvents);
			Assert.AreEqual (4, totalEvents);

			controller.AddContent (addedContent);

			Assert.AreEqual (6, totalEvents);
			Assert.AreEqual (2, contentAddedEvents);
			Assert.AreEqual (1, contentRemovedEvents);
			Assert.AreEqual (2, additionalContentAddedEvents);
			Assert.AreEqual (1, additionalContentRemovedEvents);

			addedContent = new SomeContent ();
			controller.ReplaceContent (removedContent, addedContent);
			removedContent = addedContent;

			Assert.AreEqual (10, totalEvents);
			Assert.AreEqual (3, contentAddedEvents);
			Assert.AreEqual (2, contentRemovedEvents);
			Assert.AreEqual (3, additionalContentAddedEvents);
			Assert.AreEqual (2, additionalContentRemovedEvents);

			r1.Dispose ();

			controller.RemoveContent (addedContent);
			controller.AddContent (addedContent);

			Assert.AreEqual (12, totalEvents);
			Assert.AreEqual (3, contentAddedEvents);
			Assert.AreEqual (2, contentRemovedEvents);
			Assert.AreEqual (4, additionalContentAddedEvents);
			Assert.AreEqual (3, additionalContentRemovedEvents);
		}

		[Test]
		public async Task RunWhenContentAddedForSlowView()
		{
			// In this test the SomeContent instance is provided by the view,
			// and the view is slow to load, so it won't be available just
			// after the document is returned.

			var controller = new SlowlyLoadedController ();
			var doc = await documentManager.OpenDocument (controller);

			int contentAddedEvents = 0;

			// View not yet loaded
			Assert.IsNull (doc.GetContent<SomeContent> ());

			var r1 = doc.RunWhenContentAdded<SomeContent> (_ => {
				contentAddedEvents++;
			});

			Assert.AreEqual (0, contentAddedEvents);

			// Simulate slow load done
			controller.GoAhead ();

			var c = await doc.GetContentWhenAvailable<SomeContent> ();
			Assert.IsNotNull (c);
			Assert.AreEqual (1, contentAddedEvents);
			Assert.IsNotNull (doc.GetContent<SomeContent> ());
		}

		[Test]
		public async Task NewDocumentUniqueFileName()
		{
			const string newName = "Untitled";
			var doc = await documentManager.NewDocument (newName, "text/plain", "");
			var doc2 = await documentManager.NewDocument (newName, "text/plain", "");
			Assert.AreNotEqual (doc.FileName, doc2.FileName);
		}
	}

	class TestController: DocumentController
	{
	}

	class TestFileController : FileDocumentController
	{
	}

	class TestFileControllerFactory : FileDocumentControllerFactory
	{
		public override Task<DocumentController> CreateController (FileDescriptor modelDescriptor, DocumentControllerDescription controllerDescription)
		{
			return Task.FromResult<DocumentController> (new TestFileController ());
		}

		protected override IEnumerable<DocumentControllerDescription> GetSupportedControllers (FileDescriptor modelDescriptor)
		{
			if (modelDescriptor.FilePath.Extension == ".test") {
				yield return new DocumentControllerDescription ("Test Source View", true, DocumentControllerRole.Source);
				yield return new DocumentControllerDescription ("Test Design View", false, DocumentControllerRole.VisualDesign);
			}
		}
	}

	class DocumentManagerEventTracker
	{
		private readonly DocumentManager documentManager;

		public List<DocumentEventArgs> DocumentOpenedEvents = new List<DocumentEventArgs> ();
		public List<DocumentEventArgs> DocumentClosedEvents = new List<DocumentEventArgs> ();
		public List<DocumentCloseEventArgs> DocumentClosingEvents = new List<DocumentCloseEventArgs> ();
		public List<DocumentEventArgs> ActiveDocumentChangedEvents = new List<DocumentEventArgs> ();

		public DocumentManagerEventTracker (DocumentManager documentManager)
		{
			this.documentManager = documentManager;

			documentManager.DocumentOpened += (sender, e) => DocumentOpenedEvents.Add (e);
			documentManager.DocumentClosed += (sender, e) => DocumentClosedEvents.Add (e);
			documentManager.DocumentClosing += (sender, e) => { DocumentClosingEvents.Add (e); return Task.CompletedTask; };
			documentManager.ActiveDocumentChanged += (sender, e) => ActiveDocumentChangedEvents.Add (e);
		}

		public void Reset ()
		{
			DocumentOpenedEvents.Clear ();
			DocumentClosedEvents.Clear ();
			DocumentClosingEvents.Clear ();
			ActiveDocumentChangedEvents.Clear ();
		}
	}

	class SlowlyLoadedController: DocumentController
	{
		TaskCompletionSource<bool> slowLoad = new TaskCompletionSource<bool> ();

		public void GoAhead ()
		{
			slowLoad.SetResult (true);
		}

		protected override async Task<DocumentView> OnInitializeView ()
		{
			// Simulate slow load
			await slowLoad.Task;

			var inner = new ContentTestController ();
			await inner.Initialize (null);
			inner.AddContent (new SomeContent ());
			return await inner.GetDocumentView ();
		}
	}
  
	class ReusableController : DocumentController
	{
		ModelDescriptor modelDescriptor;

		protected override Task OnInitialize (ModelDescriptor modelDescriptor, Properties status)
		{
			this.modelDescriptor = modelDescriptor;
			return base.OnInitialize (modelDescriptor, status);
		}

		protected override bool OnTryReuseDocument (ModelDescriptor modelDescriptor)
		{
			if (this.modelDescriptor == modelDescriptor)
				return true;
			return base.OnTryReuseDocument (modelDescriptor);
		}
	}

	class ReusableFileController : FileDocumentController
	{
		public bool AllowReuse { get; set; } = true;
		
		protected override bool OnTryReuseDocument (ModelDescriptor modelDescriptor)
		{
			var fileDesc = modelDescriptor as FileDescriptor;
			var ext = fileDesc.FilePath.Extension;
			return AllowReuse && (ext == ".dll_test" || ext == ".exe_test");
		}
	}

	class ReusableFileControllerFactory : FileDocumentControllerFactory
	{
		public bool Enabled { get; set; } = true;

		public override Task<DocumentController> CreateController (FileDescriptor modelDescriptor, DocumentControllerDescription controllerDescription)
		{
			return Task.FromResult<DocumentController> (new ReusableFileController ());
		}

		protected override IEnumerable<DocumentControllerDescription> GetSupportedControllers (FileDescriptor modelDescriptor)
		{
			if (Enabled && (modelDescriptor.FilePath.Extension == ".dll_test" || modelDescriptor.FilePath.Extension == ".exe_test"))
				yield return new DocumentControllerDescription ("Assembly_Test", true, DocumentControllerRole.Tool);
		}
	}

	class ReusableControllerFactory : DocumentControllerFactory
	{
		public override Task<DocumentController> CreateController (ModelDescriptor modelDescriptor, DocumentControllerDescription controllerDescription)
		{
			return Task.FromResult<DocumentController> (new ReusableController ());
		}

		protected override IEnumerable<DocumentControllerDescription> GetSupportedControllers (ModelDescriptor modelDescriptor)
		{
			if (modelDescriptor is ReusableDescriptor)
				yield return new DocumentControllerDescription ("Reusable", true, DocumentControllerRole.Tool);
		}
	}


	class ReusableDescriptor : ModelDescriptor
	{
	}
}
