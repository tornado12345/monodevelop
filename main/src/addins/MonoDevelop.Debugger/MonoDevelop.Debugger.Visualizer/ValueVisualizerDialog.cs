﻿// 
// ValueViewerDialog.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
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
using System.Collections.Generic;
using Mono.Debugging.Client;
using Gtk;
using Gdk;

namespace MonoDevelop.Debugger.Viewers
{
	public partial class ValueVisualizerDialog : Gtk.Dialog
	{
		List<ValueVisualizer> visualizers;
		List<ToggleButton> buttons;
		Gtk.Widget currentWidget;
		ValueVisualizer currentVisualizer;
		ObjectValue value;

		public ValueVisualizerDialog ()
		{
			this.Build ();
			this.Modal = true;
		}

		public void Show (ObjectValue val)
		{
			value = val;
			visualizers = new List<ValueVisualizer> (DebuggingService.GetValueVisualizers (val));
			visualizers.Sort ((v1, v2) => string.Compare (v1.Name, v2.Name, StringComparison.CurrentCultureIgnoreCase));
			buttons = new List<ToggleButton> ();

			ToggleButton defaultVis = null;

			for (int i = 0; i < visualizers.Count; i++) {
				var button = new ToggleButton ();
				button.Label = visualizers [i].Name;
				button.Toggled += OnComboVisualizersChanged;
				if (visualizers [i].IsDefaultVisualizer (val))
					defaultVis = button;
				hbox1.PackStart (button, false, false, 0);
				buttons.Add (button);
				button.Show ();
			}

			if (defaultVis == null)
				defaultVis = buttons [0];

			defaultVis.GrabFocus ();
			SetToggleState (defaultVis, true);
			UpdateVisualizer (defaultVis);

			if (val.IsReadOnly || !visualizers.Any (v => v.CanEdit (val))) {
				buttonCancel.Label = Gtk.Stock.Close;
				buttonSave.Hide ();
			}
		}

		protected override bool OnKeyPressEvent (EventKey evnt)
		{
			if (evnt.Key == Gdk.Key.Escape) {
				Respond (Gtk.ResponseType.Cancel);
				// Prevent the escape key from propagating down to the ExceptionCaughtDialog
				return true;
			}
			return base.OnKeyPressEvent (evnt);
		}

		void SetToggleState (ToggleButton button, bool value)
		{
			button.Toggled -= OnComboVisualizersChanged;
			button.Active = value;
			button.Toggled += OnComboVisualizersChanged;
		}

		void UpdateVisualizer (ToggleButton button)
		{
			if (currentWidget != null)
				mainBox.Remove (currentWidget);

			foreach (var b in buttons) {
				if (b != button && b.Active)
					SetToggleState (b, false);
			}

			currentVisualizer = visualizers [buttons.IndexOf (button)];
			currentWidget = currentVisualizer.GetVisualizerWidget (value);
			buttonSave.Sensitive = currentVisualizer.CanEdit (value);
			mainBox.PackStart (currentWidget, true, true, 0);
			currentWidget.Show ();
		}

		protected virtual void OnComboVisualizersChanged (object sender, EventArgs e)
		{
			var button = (ToggleButton) sender;

			if (!button.Active) {//Prevent un-toggling
				SetToggleState (button, true);
				return;
			}

			UpdateVisualizer (button);
		}

		protected virtual void OnSaveClicked (object sender, EventArgs e)
		{
			bool saved = false;

			if (currentVisualizer == null || (saved = currentVisualizer.StoreValue (value))) {
				Respond (Gtk.ResponseType.Ok);

				if (saved)
					DebuggingService.NotifyVariableChanged ();
			}
		}
	}
}

