// 
// AssemblyReferenceFolder.cs
//  
// Author:
//       Mike Krüger <mkrueger@xamarin.com>
// 
// Copyright (c) 2012 Xamarin <http://xamarin.com>
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
using System.Linq;
using System.Text;
	
using MonoDevelop.Core;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Ide.Gui.Pads;
using MonoDevelop.Ide.Gui.Components;
using System.Collections.Generic;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace MonoDevelop.AssemblyBrowser
{
	class AssemblyReferenceFolder
	{
		PEFile definition;
		
		public IEnumerable<AssemblyReference> AssemblyReferences {
			get {
				return definition.AssemblyReferences;
			}
		}

		// Ass
		
		public IEnumerable<IModule> ModuleReferences {
			get {
				// TODO:
				yield break;
				//foreach (var reference in definition.Metadata.GetModuleReferences ())
				//yield return definition.Metadata.GetModuleReference (reference);
			}
		}
		
		public AssemblyReferenceFolder (PEFile definition)
		{
			this.definition = definition ?? throw new ArgumentNullException (nameof (definition));
		}
	}
	
}
