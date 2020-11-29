﻿//
// HPanedThin.cs
//
// Author:
//       Lluis Sanchez <lluis@xamarin.com>
//
// Copyright (c) 2012 Xamarin Inc
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
using MonoDevelop.Ide.Gui;
using System.Collections.Generic;
using System;

#if MAC
using AppKit;
using MonoDevelop.Components.Mac;
#endif

namespace MonoDevelop.Components
{
	public class HPanedThin: Gtk.HPaned
	{
		static HashSet<int> stylesParsed = new HashSet<int> ();

		CustomPanedHandle handle;

		public HPanedThin ()
		{
			GtkWorkarounds.FixContainerLeak (this);
#if MAC
			if (Core.Platform.IsMac)
				handle = new CustomMacPanedHandle (this);
			else
#endif
				handle = new CustomGtkPanedHandle (this);
		}

		public int GrabAreaSize {
			get { return handle.GrabAreaSize; }
			set {
				handle.GrabAreaSize = value;
				QueueResize ();
			}
		}

		public Gtk.Widget HandleWidget {
			get { return handle.HandleWidget; }
			set { handle.HandleWidget = value; }
		}

		internal static void InitStyle (Gtk.Paned paned, int size)
		{
			string id = "MonoDevelop.ThinPanedHandle.s" + size;
			if (stylesParsed.Add (size)) {
				Gtk.Rc.ParseString ("style \"" + id + "\" {\n GtkPaned::handle-size = " + size + "\n }\n");
				Gtk.Rc.ParseString ("widget \"*." + id + "\" style  \"" + id + "\"\n");
			}
			paned.Name = id;
		}

		protected override void ForAll (bool include_internals, Gtk.Callback callback)
		{
			base.ForAll (include_internals, callback);
			if (handle != null)
				callback (handle);
		}

		protected override bool OnExposeEvent (Gdk.EventExpose evnt)
		{
			base.OnExposeEvent (evnt);

			if (Child1 != null && Child1.Visible && Child2 != null && Child2.Visible) {
				var gc = new Gdk.GC (evnt.Window);
				gc.RgbFgColor = Styles.ThinSplitterColor.ToGdkColor ();
				var x = Child1.Allocation.X + Child1.Allocation.Width;
				evnt.Window.DrawLine (gc, x, Allocation.Y, x, Allocation.Y + Allocation.Height);
				gc.Dispose ();
			}

			return true;
		}
	}

	abstract class CustomPanedHandle : Gtk.EventBox
	{
		internal const int HandleGrabWidth = 4;

		public abstract int GrabAreaSize { get; set; }

		public virtual Gtk.Widget HandleWidget { get; set; }

		protected Gtk.Paned ParentPaned { get; private set; }

		protected CustomPanedHandle (Gtk.Paned parent)
		{
			ParentPaned = parent;
			ParentPaned.SizeRequested += HandleSizeRequested;
			ParentPaned.SizeAllocated += HandleSizeAllocated;
			Parent = parent;
		}

		protected virtual void OnParentSizeRequested (Gtk.SizeRequestedArgs args)
		{
			SizeRequest ();
		}

		protected virtual void OnParentSizeAllocated (Gtk.SizeAllocatedArgs args)
		{
		}

		void HandleSizeRequested (object o, Gtk.SizeRequestedArgs args)
		{
			OnParentSizeRequested (args);
		}

		void HandleSizeAllocated (object o, Gtk.SizeAllocatedArgs args)
		{
			OnParentSizeAllocated (args);
		}

		protected override void OnDestroyed ()
		{
            if (ParentPaned != null) {
				ParentPaned.SizeRequested -= HandleSizeRequested;
				ParentPaned.SizeAllocated -= HandleSizeAllocated;
				ParentPaned = null;
			}
			base.OnDestroyed ();
		}
	}

#if MAC

	sealed class CustomMacPanedHandle : CustomPanedHandle
	{
		Gtk.GtkNSViewHost host;
		MacPanedHandleView handle;
		readonly bool horizontal;

		public CustomMacPanedHandle (Gtk.Paned parent) : base (parent)
		{
			VisibleWindow = false;
			HPanedThin.InitStyle (parent, 1);
			horizontal = parent is HPanedThin;

			handle = new MacPanedHandleView (parent);
			host = new Gtk.GtkNSViewHost (handle);

			GrabAreaSize = HandleGrabWidth;

			Add (host);
			host.Show ();
		}

		public override int GrabAreaSize {
			get {
				if (horizontal)
					return SizeRequest ().Width;
				else
					return SizeRequest ().Height;
			}
			set {
				if (horizontal)
					WidthRequest = value;
				else
					HeightRequest = value;
			}
		}

		protected override void OnParentSizeAllocated (Gtk.SizeAllocatedArgs args)
		{
			if (ParentPaned.Child1 != null && ParentPaned.Child1.Visible && ParentPaned.Child2 != null && ParentPaned.Child2.Visible) {
				Show ();
				int centerSize = Child == null ? GrabAreaSize / 2 : 0;
				if (horizontal)
					SizeAllocate (new Gdk.Rectangle (ParentPaned.Child1.Allocation.X + ParentPaned.Child1.Allocation.Width - centerSize, args.Allocation.Y, GrabAreaSize, args.Allocation.Height));
				else
					SizeAllocate (new Gdk.Rectangle (args.Allocation.X, ParentPaned.Child1.Allocation.Y + ParentPaned.Child1.Allocation.Height - centerSize, args.Allocation.Width, GrabAreaSize));
			} else
				Hide ();
			base.OnParentSizeAllocated (args);
		}

		protected override void OnDestroyed ()
		{
			host = null;
			handle = null;
			base.OnDestroyed ();
		}
	}

	sealed class MacPanedHandleView: DragEventTrapView
	{
		readonly WeakReference<Gtk.Paned> owner;
		readonly bool horizontal;
		int initialPos;
		int initialPanedPos;

		public MacPanedHandleView (Gtk.Paned owner)
		{
			horizontal = owner is HPanedThin;
			this.owner = new WeakReference<Gtk.Paned>(owner);
		}

		protected override NSCursor GetDragCursor ()
		{
			if (horizontal) {
				return NSCursor.ResizeLeftRightCursor;
			} else {
				return NSCursor.ResizeUpDownCursor;
			}
		}

		public override void MouseDown (NSEvent theEvent)
		{
			if (owner.TryGetTarget (out var paned)) {
				var point = NSEvent.CurrentMouseLocation;

				if (horizontal)
					initialPos = (int)point.X;
				else
					initialPos = (int)point.Y;
				initialPanedPos = paned.Position;
			}

			base.MouseDown (theEvent);
		}

		public override void MouseDragged (NSEvent theEvent)
		{
			base.MouseDragged (theEvent);

			if (owner.TryGetTarget (out var paned)) {
				var point = NSEvent.CurrentMouseLocation;
				int relativeTo = horizontal ? (int)point.X : (int)point.Y;
				int newpos = initialPanedPos + relativeTo - initialPos;
				paned.Position = newpos >= 10 ? newpos : 10;
			}
		}

		public override void UpdateTrackingAreas ()
		{
			base.UpdateTrackingAreas ();
			// this will be called after layout changes, let's make sure that we're still the topmost view
			EnsureViewIsTopmost ();
		}

		void EnsureViewIsTopmost ()
		{
			if (Superview?.Subviews?.Length > 1 && Superview.Subviews [Superview.Subviews.Length - 1] != this) {
				var superview = Superview;
				RemoveFromSuperview ();
				superview.AddSubview (this, NSWindowOrderingMode.Above, null);
			}
		}
	}

#endif

	sealed class CustomGtkPanedHandle: CustomPanedHandle
	{
		static Gdk.Cursor resizeCursorW = new Gdk.Cursor (Gdk.CursorType.SbHDoubleArrow);
		static Gdk.Cursor resizeCursorH = new Gdk.Cursor (Gdk.CursorType.SbVDoubleArrow);

		bool horizontal;
		bool dragging;
		int initialPos;
		int initialPanedPos;

		public CustomGtkPanedHandle (Gtk.Paned parent) : base (parent)
		{
			horizontal = parent is HPanedThin;
			GrabAreaSize = HandleGrabWidth;
			Events |= Gdk.EventMask.EnterNotifyMask | Gdk.EventMask.LeaveNotifyMask | Gdk.EventMask.PointerMotionMask | Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;

			HandleWidget = null;
		}

		void HandleSizeRequested (object o, Gtk.SizeRequestedArgs args)
		{
			SizeRequest ();
		}

		protected override void OnParentSizeAllocated (Gtk.SizeAllocatedArgs args)
		{
			if (ParentPaned.Child1 != null && ParentPaned.Child1.Visible && ParentPaned.Child2 != null && ParentPaned.Child2.Visible) {
				Show ();
				int centerSize = Child == null ? GrabAreaSize / 2 : 0;
				if (horizontal)
					SizeAllocate (new Gdk.Rectangle (ParentPaned.Child1.Allocation.X + ParentPaned.Child1.Allocation.Width - centerSize, args.Allocation.Y, GrabAreaSize, args.Allocation.Height));
				else
					SizeAllocate (new Gdk.Rectangle (args.Allocation.X, ParentPaned.Child1.Allocation.Y + ParentPaned.Child1.Allocation.Height - centerSize, args.Allocation.Width, GrabAreaSize));
			} else
				Hide ();
			base.OnParentSizeAllocated (args);
		}

		public override int GrabAreaSize {
			get {
				if (horizontal)
					return SizeRequest ().Width;
				else
					return SizeRequest ().Height;
			}
			set {
				if (horizontal)
					WidthRequest = value;
				else
					HeightRequest = value;
			}
		}

		public override Gtk.Widget HandleWidget {
			get { return Child; }
			set {
				if (Child != null) {
					Remove (Child);
				}
				if (value != null) {
					Add (value);
					value.Show ();
					VisibleWindow = true;
					WidthRequest = HeightRequest = -1;
					HPanedThin.InitStyle (ParentPaned, GrabAreaSize);
				} else {
					VisibleWindow = false;
					if (horizontal)
						WidthRequest = 1;
					else
						HeightRequest = 1;
					HPanedThin.InitStyle (ParentPaned, 1);
				}
			}
		}

		protected override bool OnEnterNotifyEvent (Gdk.EventCrossing evnt)
		{
			if (horizontal)
				GdkWindow.Cursor = resizeCursorW;
			else
				GdkWindow.Cursor = resizeCursorH;
			return base.OnEnterNotifyEvent (evnt);
		}

		protected override bool OnLeaveNotifyEvent (Gdk.EventCrossing evnt)
		{
			GdkWindow.Cursor = null;
			return base.OnLeaveNotifyEvent (evnt);
		}

		protected override bool OnButtonPressEvent (Gdk.EventButton evnt)
		{
			if (horizontal)
				initialPos = (int) evnt.XRoot;
			else
				initialPos = (int) evnt.YRoot;
			initialPanedPos = ParentPaned.Position;
			dragging = true;
			return true;
		}

		protected override bool OnButtonReleaseEvent (Gdk.EventButton evnt)
		{
			dragging = false;
			return true;
		}

		protected override bool OnMotionNotifyEvent (Gdk.EventMotion evnt)
		{
			if (dragging) {
				if (horizontal) {
					int newpos = initialPanedPos + ((int) evnt.XRoot - initialPos);
					ParentPaned.Position = newpos >= 10 ? newpos : 10;
				}
				else {
					int newpos = initialPanedPos + ((int) evnt.YRoot - initialPos);
					ParentPaned.Position = newpos >= 10 ? newpos : 10;
				}
			}
			return base.OnMotionNotifyEvent (evnt);
		}
	}
}

