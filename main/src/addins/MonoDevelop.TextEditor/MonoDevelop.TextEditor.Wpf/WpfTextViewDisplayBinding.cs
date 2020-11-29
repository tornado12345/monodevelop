//
// Copyright (c) Microsoft Corp. (https://www.microsoft.com)
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
using System.Reflection;
using System.Windows;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Classification;

using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Documents;
using MonoDevelop.Projects;

namespace MonoDevelop.TextEditor
{
	[ExportDocumentControllerFactory (FileExtension = "*", InsertBefore = "TextEditor")]
	class WpfTextViewDisplayBinding : TextViewDisplayBinding<WpfTextViewImports>
	{
		static WpfTextViewDisplayBinding ()
		{
			AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
		}

		private static Assembly OnAssemblyResolve (object sender, ResolveEventArgs args)
		{
			if (args.Name == "MonoDevelop.TextEditor.Wpf, Version=2.6.0, Culture=neutral") {
				return typeof (WpfTextViewDisplayBinding).Assembly;
			}

			return null;
		}

		protected override DocumentController CreateContent (WpfTextViewImports imports)
		{
			return new WpfTextViewContent (imports);
		}

		protected override ThemeToClassification CreateThemeToClassification (IEditorFormatMapService editorFormatMapService)
			=> new WpfThemeToClassification (editorFormatMapService);
	}

	class WpfThemeToClassification : ThemeToClassification
	{
		public WpfThemeToClassification (IEditorFormatMapService editorFormatMapService) : base (editorFormatMapService) { }

		protected override void AddFontToDictionary (ResourceDictionary resourceDictionary, string appearanceCategory, string fontName, double fontSize)
		{
			resourceDictionary[ClassificationFormatDefinition.TypefaceId] = new Typeface (fontName);
			resourceDictionary[ClassificationFormatDefinition.FontRenderingSizeId] = fontSize * 96 / 72;
		}
	}
}