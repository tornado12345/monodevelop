﻿//
// DependenciesNodeBuilder.cs
//
// Author:
//       Matt Ward <matt.ward@xamarin.com>
//
// Copyright (c) 2016 Xamarin Inc. (http://xamarin.com)
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
using System.Collections;
using MonoDevelop.DotNetCore.Commands;
using MonoDevelop.Ide.Gui.Components;

namespace MonoDevelop.DotNetCore.NodeBuilders
{
	public class DependenciesNodeBuilder : TypeNodeBuilder
	{
		public override Type NodeDataType {
			get { return typeof(DependenciesNode); }
		}

		public override string GetNodeName (ITreeNavigator thisNode, object dataObject)
		{
			return DependenciesNode.NodeName;
		}

		public override Type CommandHandlerType {
			get { return typeof (DependenciesNodeCommandHandler); }
		}

		public override void BuildNode (ITreeBuilder treeBuilder, object dataObject, NodeInfo nodeInfo)
		{
			var node = (DependenciesNode)dataObject;
			nodeInfo.Label = node.GetLabel ();
			nodeInfo.SecondaryLabel = node.GetSecondaryLabel ();
			nodeInfo.Icon = Context.GetIcon (node.Icon);
			nodeInfo.ClosedIcon = Context.GetIcon (node.ClosedIcon);
		}

		public override int GetSortIndex (ITreeNavigator node)
		{
			return -600;
		}

		public override bool HasChildNodes (ITreeBuilder builder, object dataObject)
		{
			return true;
		}

		public override void BuildChildNodes (ITreeBuilder treeBuilder, object dataObject)
		{
			var node = (DependenciesNode)dataObject;
			AddChildren (treeBuilder, node.GetChildNodes ());
		}

		protected virtual void AddChildren (ITreeBuilder treeBuilder, IEnumerable dataObjects)
		{
			treeBuilder.AddChildren (dataObjects);
		}

		public override void OnNodeAdded (object dataObject)
		{
			var dependenciesNode = (DependenciesNode)dataObject;
			dependenciesNode.PackageDependencyCache.PackageDependenciesChanged += OnPackageDependenciesChanged;
			dependenciesNode.FrameworkReferencesCache.FrameworkReferencesChanged += OnFrameworkReferencesChanged;
		}

		public override void OnNodeRemoved (object dataObject)
		{
			var dependenciesNode = (DependenciesNode)dataObject;
			dependenciesNode.PackageDependencyCache.PackageDependenciesChanged -= OnPackageDependenciesChanged;
			dependenciesNode.FrameworkReferencesCache.FrameworkReferencesChanged -= OnFrameworkReferencesChanged;
		}

		void OnPackageDependenciesChanged (object sender, EventArgs e)
		{
			var cache = (PackageDependencyNodeCache)sender;
			ITreeBuilder builder = Context.GetTreeBuilder (cache.Project);
			if (builder == null)
				return;

			if (builder.MoveToChild (DependenciesNode.NodeName, typeof (DependenciesNode))) {
				builder.UpdateAll ();
			}
		}

		void OnFrameworkReferencesChanged (object sender, EventArgs e)
		{
			var cache = (FrameworkReferenceNodeCache)sender;
			ITreeBuilder builder = Context.GetTreeBuilder (cache.Project);
			if (builder == null)
				return;

			if (!builder.MoveToChild (DependenciesNode.NodeName, typeof (DependenciesNode))) {
				builder.UpdateAll ();
				return;
			}

			if (builder.MoveToChild (FrameworkReferencesNode.NodeName, typeof (FrameworkReferencesNode))) {
				builder.UpdateAll ();
			} else {
				builder.UpdateAll ();
			}
		}
	}
}
