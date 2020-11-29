// Copyright (c) Microsoft
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
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.CodeAnalysis.Text;

using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Text;

using Mono.Addins;

using MonoDevelop.Ide.Composition;

namespace MonoDevelop.Ide.TypeSystem
{
	/// <summary>
	/// Tracks open .cs documents not claimed by any primary workspace and .csx script files.
	/// Handles standalone files and open documents that haven't been added to the main workspace,
	/// yet during solution load, and all .csx script files.
	/// </summary>
	/// <remarks>
	/// Scenarios:
	///  * Document opening:
	///     - not part of any workspace yet
	///     - already part of a different (primary) workspace
	///  * Open document workspace registration changed:
	///     - another workspace opens and claims our document
	///     - another workspace closes and releases our document
	///       - we didn't know we lost the document, need to re-sync
	///       - we knew we lost the document
	///  * Document closing
	///     - it was in our workspace
	///     - it was not in our workspace
	/// </remarks>
	[Export]
	class MiscellaneousFilesWorkspace : Workspace
	{
		ConcurrentDictionary<WorkspaceRegistration, OpenDocumentInfo> openDocuments = new ConcurrentDictionary<WorkspaceRegistration, OpenDocumentInfo> ();

		readonly ImmutableArray<MetadataReference> defaultReferences = ImmutableArray.Create<MetadataReference> (
			MetadataReference.CreateFromFile (typeof (object).Assembly.Location));

		readonly ProjectId defaultProjectId;
		const string DefaultProjectName = "MiscellaneousProject";

		[ImportingConstructor]
		public MiscellaneousFilesWorkspace ()
			: base (CompositionManager.Instance.HostServices, WorkspaceKind.MiscellaneousFiles)
		{
			foreach (var factory in AddinManager.GetExtensionObjects<Microsoft.CodeAnalysis.Options.IDocumentOptionsProviderFactory> ("/MonoDevelop/Ide/TypeService/OptionProviders"))
				Services.GetRequiredService<Microsoft.CodeAnalysis.Options.IOptionService> ().RegisterDocumentOptionsProvider (factory.TryCreate (this));

			defaultProjectId = ProjectId.CreateNewId (DefaultProjectName);

			var compilationOptions = new CSharpCompilationOptions (OutputKind.ConsoleApplication);

			var projectInfo = ProjectInfo.Create (
				defaultProjectId,
				VersionStamp.Create (),
				DefaultProjectName,
				DefaultProjectName,
				LanguageNames.CSharp,
				compilationOptions: compilationOptions,
				parseOptions: CSharpParseOptions.Default.WithLanguageVersion (LanguageVersion.Latest),
				metadataReferences: defaultReferences).WithHasAllInformation (false);
			OnProjectAdded (projectInfo);
		}

		class OpenDocumentInfo
		{
			public DocumentId DocumentId;
			public ProjectId ProjectId;
			public SourceTextContainer SourceTextContainer;
			public string FilePath;
		}

		public ProjectId DefaultProjectId => defaultProjectId;

		public DocumentId GetDocumentId (string fileName)
		{
			foreach (var entry in openDocuments) {
				if (entry.Value.FilePath == fileName) {
					if (entry.Key.Workspace == this)
						return entry.Value.DocumentId;
					return null;
				}
			}
			return null;
		}

		/// <summary>
		/// This should be called when a new document is opened in the IDE.
		/// </summary>
		public void OnDocumentOpened (string filePath, ITextBuffer buffer)
		{
			var textContainer = buffer?.AsTextContainer ();
			if (!IsSupportedDocument (filePath) || textContainer == null) {
				return;
			}

			var workspaceRegistration = GetWorkspaceRegistration (textContainer);

			// Check if the document is already registered as open
			if (openDocuments.ContainsKey (workspaceRegistration))
				return;

			workspaceRegistration.WorkspaceChanged += Registration_WorkspaceChanged;

			var openDocumentInfo = new OpenDocumentInfo {
				SourceTextContainer = textContainer,
				FilePath = filePath
			};
			openDocuments[workspaceRegistration] = openDocumentInfo;

			if (workspaceRegistration.Workspace != null) {
				return;
			}

			AddDocument (workspaceRegistration, openDocumentInfo);
		}

		/// <summary>
		/// This is called by Roslyn when a SourceTextContainer's document is added or removed from a workspace
		/// </summary>
		/// <param name="sender">The associated <see cref="WorkspaceRegistration"/> object.</param>
		void Registration_WorkspaceChanged (object sender, EventArgs e)
		{
			var workspaceRegistration = (WorkspaceRegistration)sender;

			openDocuments.TryGetValue (workspaceRegistration, out var openDocumentInfo);

			if (workspaceRegistration.Workspace == null && openDocumentInfo != null) {
				// The workspace was taken from us and released and we have only asynchronously found out now.
				// We already have the file open in our workspace, but the global mapping of source text container
				// to the workspace that owns it needs to be updated once more.
				if (openDocumentInfo.DocumentId != null) {
					RegisterText (openDocumentInfo.SourceTextContainer);
				} else {
					// properly claim by adding and opening a new document
					AddDocument (workspaceRegistration, openDocumentInfo);
				}
			} else if (workspaceRegistration.Workspace != this) {
				// another workspace has claimed the document, remove it from our workspace
				RemoveDocument (openDocumentInfo);
			}
		}

		/// <summary>
		/// This should be called when an open document in the IDE is closed.
		/// </summary>
		public void OnDocumentClosed (string filePath, ITextBuffer buffer)
		{
			var textContainer = buffer?.AsTextContainer ();
			if (!IsSupportedDocument (filePath) || textContainer == null) {
				return;
			}

			var workspaceRegistration = GetWorkspaceRegistration (textContainer);
			workspaceRegistration.WorkspaceChanged -= Registration_WorkspaceChanged;

			if (openDocuments.TryRemove (workspaceRegistration, out var openDocumentInfo)) {
				RemoveDocument (openDocumentInfo);
			}
		}

		/// <summary>
		/// Create a new DocumentId and a new Document, add that to the workspace and open in the text container
		/// </summary>
		/// <param name="registration"></param>
		/// <param name="openDocumentInfo"></param>
		void AddDocument (WorkspaceRegistration registration, OpenDocumentInfo openDocumentInfo)
		{
			var filePath = openDocumentInfo.FilePath;
			var sourceTextContainer = openDocumentInfo.SourceTextContainer;

			var sourceCodeKind = GetSourceCodeKind (filePath);
			openDocumentInfo.ProjectId = sourceCodeKind == SourceCodeKind.Script
				? CreateScriptProject ()
				: defaultProjectId;

			var documentId = DocumentId.CreateNewId (openDocumentInfo.ProjectId, filePath);
			openDocumentInfo.DocumentId = documentId;

			var documentInfo = DocumentInfo.Create (
				documentId,
				Path.GetFileName (filePath),
				sourceCodeKind: sourceCodeKind,
				filePath: filePath,
				loader: TextLoader.From (sourceTextContainer, VersionStamp.Create ()));

			OnDocumentAdded (documentInfo);
			OnDocumentOpened (documentId, sourceTextContainer);

			ProjectId CreateScriptProject ()
			{
				var projectId = ProjectId.CreateNewId (filePath);

				var compilationOptions = new CSharpCompilationOptions (
					outputKind: OutputKind.ConsoleApplication,
					sourceReferenceResolver: ScriptSourceResolver.Default,
					metadataReferenceResolver: ScriptMetadataResolver.Default);

				var projectInfo = ProjectInfo.Create (
					id: projectId,
					version: VersionStamp.Create (),
					name: filePath,
					assemblyName: Path.GetFileNameWithoutExtension (filePath),
					language: LanguageNames.CSharp,
					metadataReferences: defaultReferences,
					compilationOptions: compilationOptions,
					parseOptions: CSharpParseOptions
						.Default
						.WithLanguageVersion (LanguageVersion.Latest));

				OnProjectAdded (projectInfo);

				return projectInfo.Id;
			}
		}

		/// <summary>
		/// If the DocumentId is currently part of our workspace, remove this document
		/// from our workspace. If the document is a script, remove its project as well.
		/// </summary>
		void RemoveDocument (OpenDocumentInfo openDocumentInfo)
		{
			var documentId = openDocumentInfo.DocumentId;
			if (documentId != null) {
				OnDocumentClosed (documentId, EmptyTextLoader.Instance);
				OnDocumentRemoved (documentId);
				openDocumentInfo.DocumentId = null;

				if (openDocumentInfo.ProjectId != null &&
					openDocumentInfo.ProjectId != defaultProjectId) {
					OnProjectRemoved (openDocumentInfo.ProjectId);
					openDocumentInfo.ProjectId = null;
				}
			}
		}

		static SourceCodeKind GetSourceCodeKind (string filePath)
		{
			var sourceCodeKind = SourceCodeKind.Regular;
			if (filePath.EndsWith (".csx", StringComparison.OrdinalIgnoreCase)) {
				sourceCodeKind = SourceCodeKind.Script;
			}

			return sourceCodeKind;
		}

		bool IsSupportedDocument (string filePath)
		{
			return filePath != null && (
				filePath.EndsWith (".cs", StringComparison.OrdinalIgnoreCase) ||
				filePath.EndsWith (".csx", StringComparison.OrdinalIgnoreCase));
		}

		public override bool CanApplyChange (ApplyChangesKind feature)
		{
			if (feature == ApplyChangesKind.ChangeDocument) {
				return true;
			}

			return base.CanApplyChange (feature);
		}

		protected override void ApplyDocumentTextChanged (DocumentId id, SourceText text)
		{
			var openDocument = openDocuments.FirstOrDefault (doc => doc.Value.DocumentId == id);
			if (openDocument.Value != null) {
				var textBuffer = openDocument.Value.SourceTextContainer.GetTextBuffer ();
				SetText (textBuffer, text.ToString ());
			}
		}

		static void SetText (ITextBuffer textBuffer, string content)
		{
			content = content ?? "";

			using (var edit = textBuffer.CreateEdit (EditOptions.DefaultMinimalChange, reiteratedVersionNumber: null, editTag: 1)) {
				edit.Replace (0, textBuffer.CurrentSnapshot.Length, content);
				edit.Apply ();
			}
		}

		class EmptyTextLoader : TextLoader
		{
			public static EmptyTextLoader Instance = new EmptyTextLoader ();

			SourceText emptySourceText = SourceText.From (string.Empty);

			private EmptyTextLoader ()
			{
			}

			public override Task<TextAndVersion> LoadTextAndVersionAsync (Workspace workspace, DocumentId documentId, CancellationToken cancellationToken)
			{
				return Task.FromResult (TextAndVersion.Create (emptySourceText, VersionStamp.Default));
			}
		}
	}
}
