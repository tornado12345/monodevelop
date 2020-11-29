//
// IObjectValueTreeView.cs
//
// Author:
//       Jeffrey Stedfast <jestedfa@microsoft.com>
//
// Copyright (c) 2019 Microsoft Corp.
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

namespace MonoDevelop.Debugger
{
	/// <summary>
	/// Defines the interface to the view that ObjectValueTreeViewController can interact with
	/// </summary>
	public interface IObjectValueTreeView
	{
		/// <summary>
		/// Gets or sets a value indicating whether the user should be able to edit values in the tree
		/// </summary>
		bool AllowEditing { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether or not the user should be able to expand nodes in the tree
		/// </summary>
		bool AllowExpanding { get; set; }

		/// <summary>
		/// Gets or sets the pinned watch for the view. When a watch is pinned, the view should display only this value
		/// </summary>
		PinnedWatch PinnedWatch { get; set; }

		/// <summary>
		/// Gets a value indicating the offset required for pinned watches
		/// </summary>
		int PinnedWatchOffset { get; }

		/// <summary>
		/// Notifies the treeview that the tree has been cleared
		/// </summary>
		void Cleared ();

		/// <summary>
		/// Notifies the treeview that the specified node has been appended
		/// </summary>
		/// <param name="node">The appended node.</param>
		void Appended (ObjectValueNode node);

		/// <summary>
		/// Notifies the treeview that the specified nodes have been appended
		/// </summary>
		/// <param name="nodes">The appended nodes.</param>
		void Appended (IList<ObjectValueNode> nodes);

		/// <summary>
		/// Informs the view to load the children of the given node. startIndex and count may specify a range of
		/// the children of the node to load (for when children are being paged in from an enumerable for example).
		/// </summary>
		void LoadNodeChildren (ObjectValueNode node, int startIndex, int count);

		/// <summary>
		/// Informs the view to load the new values into the given node, optionally replacing that node with
		/// the set of replacement nodes. Handles the case where, for example, the "locals" is replaced
		/// with the set of local values
		/// </summary>
		void LoadEvaluatedNode (ObjectValueNode node, ObjectValueNode[] replacementNodes);

		/// <summary>
		/// Triggered when the view tries to expand a node. This may trigger a load of
		/// the node's children
		/// </summary>
		event EventHandler<ObjectValueNodeEventArgs> NodeExpand;

		/// <summary>
		/// Triggered when the view tries to collapse a node.
		/// </summary>
		event EventHandler<ObjectValueNodeEventArgs> NodeCollapse;

		/// <summary>
		/// Triggered when the view requests a node to fetch more of it's children
		/// </summary>
		event EventHandler<ObjectValueNodeEventArgs> NodeLoadMoreChildren;

		/// <summary>
		/// Triggered when the view needs the node to be refreshed
		/// </summary>
		event EventHandler<ObjectValueNodeEventArgs> NodeRefresh;

		/// <summary>
		/// Triggered when the view needs to know if the node can be edited
		/// </summary>
		event EventHandler<ObjectValueNodeEventArgs> NodeGetCanEdit;

		/// <summary>
		/// Triggered when the node's value has been edited by the user
		/// </summary>
		event EventHandler<ObjectValueEditEventArgs> NodeEditValue;

		/// <summary>
		/// Triggered when the user removes a node (an expression)
		/// </summary>
		event EventHandler<ObjectValueNodeEventArgs> NodeRemoved;

		/// <summary>
		/// Triggered when the user pins the node
		/// </summary>
		event EventHandler<ObjectValueNodeEventArgs> NodePinned;

		/// <summary>
		/// Triggered when the pinned watch is removed by the user
		/// </summary>
		event EventHandler<EventArgs> NodeUnpinned;

		/// <summary>
		/// Triggered when the visualiser for the node should be shown
		/// </summary>
		event EventHandler<ObjectValueNodeEventArgs> NodeShowVisualiser;

		//event EventHandler<ObjectValueDisplayEventArgs> NodeGetDisplayText;

		/// <summary>
		/// Triggered when an expression is added to the tree by the user
		/// </summary>
		event EventHandler<ObjectValueExpressionEventArgs> ExpressionAdded;

		/// <summary>
		/// Triggered when an expression is edited by the user
		/// </summary>
		event EventHandler<ObjectValueExpressionEventArgs> ExpressionEdited;

		/// <summary>
		/// Informs the view that the node was expanded and children have been loaded.
		/// </summary>
		void OnNodeExpanded (ObjectValueNode node);

		/// <summary>
		/// Triggered when the user starts editing a node
		/// </summary>
		event EventHandler StartEditing;

		/// <summary>
		/// Triggered when the user stops editing a node
		/// </summary>
		event EventHandler EndEditing;
	}
}
