﻿//
// MonoDevelopDocumentSupportsFeatureService.cs
//
// Author:
//       iain <iaholmes@microsoft.com>
//
// Copyright (c) 2018 Microsoft Corp
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
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.Shared;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.VisualStudio.Text;
using MonoDevelop.Ide;
using MonoDevelop.Ide.TypeSystem;

namespace MonoDevelop.Ide.RoslynServices
{
	[ExportWorkspaceService (typeof (ITextBufferSupportsFeatureService), ServiceLayer.Host), Shared]
	internal sealed class MonoDevelopDocumentSupportsFeatureService : ITextBufferSupportsFeatureService
	{
		public bool SupportsCodeFixes (ITextBuffer textBuffer) => !IsContainedBuffer (textBuffer);
		public bool SupportsNavigationToAnyPosition (ITextBuffer textBuffer) => !IsContainedBuffer (textBuffer);
		public bool SupportsRefactorings (ITextBuffer textBuffer) => !IsContainedBuffer (textBuffer);
		public bool SupportsRename (ITextBuffer textBuffer) => !IsContainedBuffer (textBuffer);

		static bool IsContainedBuffer (ITextBuffer textBuffer)
		{
			if (textBuffer == null) return false;
			return textBuffer.Properties.ContainsProperty (typeof (IMonoDevelopHostDocument));
		}
	}
}
