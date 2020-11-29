﻿// DebugCommands.cs
//
// Author:
//   Lluis Sanchez Gual <lluis@novell.com>
//
// Copyright (c) 2005 Novell, Inc (http://www.novell.com)
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
//
//

using System;
using System.Collections.Generic;
using MonoDevelop.Core;
using Mono.Debugging.Client;
using MonoDevelop.Components;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Components.Commands;
using MonoDevelop.Projects;
using MonoDevelop.Ide;
using System.Linq;
using System.IO;
using MonoDevelop.Core.Execution;
using MonoDevelop.Ide.Commands;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text;
using MonoDevelop.Components.AtkCocoaHelper;

namespace MonoDevelop.Debugger
{
	public enum DebugCommands
	{
		Debug,
		DebugEntry,
		DebugApplication,
		ToggleBreakpoint,
		StepOver,
		StepInto,
		StepOut,
		Pause,
		Continue,
		ClearAllBreakpoints,
		AttachToProcess,
		Detach,
		EnableDisableBreakpoint,
		DisableAllBreakpoints,
		ShowDisassembly,
		NewBreakpoint,
		RemoveBreakpoint,
		ShowBreakpointProperties,
		ExpressionEvaluator,
		ShowCurrentExecutionLine,
		AddWatch,
		StopEvaluation,
		RunToCursor,
		SetNextStatement,
		ShowNextStatement,
		NewCatchpoint,
		NewFunctionBreakpoint,
	}

	class DebugHandler: CommandHandler
	{
		internal static IBuildTarget GetRunTarget ()
		{
			return IdeApp.ProjectOperations.CurrentSelectedSolution ?? IdeApp.ProjectOperations.CurrentSelectedBuildTarget;
		}

		protected async override void Run ()
		{
			if (DebuggingService.IsPaused) {
				DebuggingService.Resume ();
				return;
			}

			if (!IdeApp.ProjectOperations.CurrentRunOperation.IsCompleted) {
				if (!MessageService.Confirm (GettextCatalog.GetString ("An application is already running. Do you want to stop it?"), AlertButton.Stop))
					return;
				StopHandler.StopBuildOperations ();
				await IdeApp.ProjectOperations.CurrentRunOperation.Task;
			}

			if (IdeApp.Workspace.IsOpen) {
				var target = GetRunTarget ();
				if (target != null)
					IdeApp.ProjectOperations.Debug (target);
			}
		}

		protected override void Update (CommandInfo info)
		{
			if (!IdeApp.Workspace.IsOpen || !DebuggingService.IsDebuggingSupported) {
				info.Enabled = false;
				return;
			}

			if (DebuggingService.IsPaused) {
				info.Enabled = true;
				info.Text = GettextCatalog.GetString ("_Continue Debugging");
				info.Description = GettextCatalog.GetString ("Continue the execution of the application");
				return;
			}

			if (DebuggingService.IsDebugging) {
				info.Enabled = false;
				return;
			}

			if ((IdeApp.Workspace.IsOpen) && (!IdeApp.ProjectOperations.CurrentRunOperation.IsCompleted))
				info.Text = GettextCatalog.GetString ("Restart With Debugging");
			else
				info.Text = GettextCatalog.GetString ("Start Debugging");

			var target = GetRunTarget ();
			info.Enabled = target != null && IdeApp.ProjectOperations.CanDebug (target);
		}
	}
	
	class DebugEntryHandler: CommandHandler
	{
		protected async override void Run ()
		{
			IBuildTarget entry = IdeApp.ProjectOperations.CurrentSelectedBuildTarget;

			await IdeApp.ProjectOperations.Debug (entry).Task;
		}
		
		protected override void Update (CommandInfo info)
		{
			IBuildTarget target = IdeApp.ProjectOperations.CurrentSelectedBuildTarget;
			info.Enabled = target != null && !(target is Workspace) && IdeApp.ProjectOperations.CanDebug (target);

			if (target is Solution)
				info.Text = GettextCatalog.GetString ("Start Debugging Solution");
			else if (target is Project)
				info.Text = GettextCatalog.GetString ("Start Debugging Project");
		}
	}
	
	class DebugApplicationHandler: CommandHandler
	{
		protected override void Run ()
		{
			var dlg = new DebugApplicationDialog ();

			try {
				bool isOK;

				while ((isOK = (MessageService.RunCustomDialog (dlg) == (int)Gtk.ResponseType.Ok)) 
					&& !Validate (dlg));

				if (isOK)
					IdeApp.ProjectOperations.DebugApplication (dlg.SelectedFile, dlg.Arguments, dlg.WorkingDirectory, dlg.EnvironmentVariables);

			} finally {
				dlg.Destroy ();
				dlg.Dispose ();
			}


		}

		bool Validate (DebugApplicationDialog dlg)
		{
			if (String.IsNullOrEmpty (dlg.SelectedFile)) {
				MessageService.ShowError (GettextCatalog.GetString ("Please select the application to debug"));
				return false;
			}

			if (!File.Exists (dlg.SelectedFile)) {
				MessageService.ShowError (GettextCatalog.GetString ("The file '{0}' does not exist", dlg.SelectedFile));
				return false;
			}

			if (!IdeApp.ProjectOperations.CanDebugFile (dlg.SelectedFile)) {
				MessageService.ShowError (GettextCatalog.GetString ("The file '{0}' can't be debugged", dlg.SelectedFile));
				return false;
			}

			return true;
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Enabled = info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.DebugFile);
		}
	}
	
	class AttachToProcessHandler: CommandHandler
	{
		protected override void Run ()
		{
			var dlg = new AttachToProcessDialog ();
			try {
				if (MessageService.RunCustomDialog (dlg) == (int) Gtk.ResponseType.Ok)
					IdeApp.ProjectOperations.AttachToProcess (dlg.SelectedDebugger, dlg.SelectedProcess);
			}
			finally {
				dlg.Destroy ();
				dlg.Dispose ();
			}
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Enabled = info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Attaching);
		}
	}
	
	class DetachFromProcessHandler: CommandHandler
	{
		protected override void Run ()
		{
			if (MessageService.Confirm (GettextCatalog.GetString ("Do you want to detach from the process being debugged?"), new AlertButton (GettextCatalog.GetString ("Detach")), true)) {
				DebuggingService.DebuggerSession.Detach ();
			}
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Enabled = DebuggingService.IsDebugging && DebuggingService.DebuggerSession.AttachedToProcess;
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Attaching);
		}
	}
	
	class StepOverHandler : CommandHandler
	{
		protected override void Run ()
		{
			DebuggingService.StepOver();
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Enabled = DebuggingService.IsPaused;
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Stepping);
		}
	}

	class StepIntoHandler : CommandHandler
	{
		protected override void Run ()
		{
			DebuggingService.StepInto();
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Enabled = DebuggingService.IsPaused;
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Stepping);
		}
	}
	
	class StepOutHandler : CommandHandler
	{
		protected override void Run ()
		{
			DebuggingService.StepOut ();
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Enabled = DebuggingService.IsPaused;
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Stepping);
		}
	}
	
	class PauseDebugHandler : CommandHandler
	{
		protected override void Run ()
		{
			DebuggingService.Pause ();
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsRunning;
			info.Enabled = DebuggingService.IsFeatureSupported (DebuggerFeatures.Pause) && DebuggingService.IsConnected;
		}
	}
	
	class ContinueDebugHandler : CommandHandler
	{
		protected override void Run ()
		{
			DebuggingService.Resume ();
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsPaused;
			info.Enabled = DebuggingService.IsConnected && DebuggingService.IsPaused;
		}
	}
	
	class ClearAllBreakpointsHandler: CommandHandler
	{
		protected override void Run ()
		{
			var breakpoints = DebuggingService.Breakpoints;

			lock (breakpoints)
				breakpoints.Clear (false);
		}
		
		protected override void Update (CommandInfo info)
		{
			var breakpoints = DebuggingService.Breakpoints;

			if (!breakpoints.IsReadOnly) {
				foreach (var be in breakpoints) {
					if (be is Breakpoint bp) {
						if (!bp.NonUserBreakpoint) {
							info.Enabled = true;
							break;
						}
					} else {
						info.Enabled = true;
						break;
					}
				}
			} else {
				info.Enabled = false;
			}

			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints);
		}
	}
	
	class ToggleBreakpointHandler: CommandHandler
	{
		protected override void Run ()
		{
			var breakpoints = DebuggingService.Breakpoints;
			Breakpoint bp;

			var textView = IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true);
			var (caretLine, caretColumn) = textView.MDCaretLineAndColumn ();
			var point = textView.Caret.Position.BufferPosition;

			bp = breakpoints.Toggle (IdeApp.Workbench.ActiveDocument.FileName, caretLine, caretColumn);

			var msg = bp == null
				? GettextCatalog.GetString ("Removed breakpoint, line {0}, file {1}", caretLine, IdeApp.Workbench.ActiveDocument.FileName)
				: GettextCatalog.GetString ("Added breakpoint, line {0}, file {1}", caretLine, IdeApp.Workbench.ActiveDocument.FileName);
			IdeApp.Workbench.RootWindow.Accessible.MakeAccessibilityAnnouncement (msg);
			// If the breakpoint could not be inserted in the caret location, move the caret
			// to the real line of the breakpoint, so that if the Toggle command is run again,
			// this breakpoint will be removed
			if (bp != null && bp.Line != caretLine)
				textView.Caret.MoveTo (point.Snapshot.GetLineFromLineNumber (bp.Line).Start);
		}

		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints);
			info.Enabled = IdeApp.Workbench.ActiveDocument != null &&
					IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true) != null &&
					IdeApp.Workbench.ActiveDocument.FileName != FilePath.Null &&
					!DebuggingService.Breakpoints.IsReadOnly;
		}
	}

	class EnableDisableBreakpointHandler: CommandHandler
	{
		protected override void Run ()
		{
			var breakpoints = DebuggingService.Breakpoints;

			foreach (var bp in breakpoints.GetBreakpointsAtFileLine (IdeApp.Workbench.ActiveDocument.FileName, IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true).MDCaretLine ()))
				bp.Enabled = !bp.Enabled;
		}
		
		protected override void Update (CommandInfo info)
		{
			var breakpoints = DebuggingService.Breakpoints;

			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints);
			if (IdeApp.Workbench.ActiveDocument != null && 
			    IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true) != null &&
			    IdeApp.Workbench.ActiveDocument.FileName != FilePath.Null &&
			    !breakpoints.IsReadOnly) {
				var bpInLine = breakpoints.GetBreakpointsAtFileLine (IdeApp.Workbench.ActiveDocument.FileName, IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true).MDCaretLine());
				info.Enabled = bpInLine.Count > 0;
				info.Text = GettextCatalog.GetString ("Disable Breakpoint");
				foreach (var bp in bpInLine) {
					if (!bp.Enabled)
						info.Text = GettextCatalog.GetString ("Enable Breakpoint");
					break;
				}
			} else {
				info.Enabled = false;
			}
		}
	}
	
	class DisableAllBreakpointsHandler: CommandHandler
	{
		protected override void Run ()
		{
			var breakpoints = DebuggingService.Breakpoints.ToList();
			bool enable = false;

			foreach (BreakEvent bp in breakpoints) {
				if (!bp.Enabled) {
					enable = true;
					break;
				}
			}

			foreach (BreakEvent bp in breakpoints) {
				bp.Enabled = enable;
			}
		}
		
		protected override void Update (CommandInfo info)
		{
			var breakpoints = DebuggingService.Breakpoints;

			info.Enabled = !breakpoints.IsReadOnly && breakpoints.Count > 0;
			bool enable = false;
			foreach (BreakEvent bp in breakpoints) {
				if (!bp.Enabled) {
					enable = true;
					break;
				}
			}
			if (enable)
				info.Text = GettextCatalog.GetString ("Enable All Breakpoints");
			else
				info.Text = GettextCatalog.GetString ("Disable All Breakpoints");

			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints);
		}
	}
	
	class ShowDisassemblyHandler: CommandHandler
	{
		protected override void Run ()
		{
			DebuggingService.ShowDisassembly ();
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Disassembly);
			info.Enabled = IdeApp.Workspace.IsOpen;
		}
	}
	
	class RemoveBreakpointHandler: CommandHandler
	{
		protected override void Run ()
		{
			var breakpoints = DebuggingService.Breakpoints;

			IEnumerable<Breakpoint> brs = breakpoints.GetBreakpointsAtFileLine (
				IdeApp.Workbench.ActiveDocument.FileName,
				IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true).MDCaretLine ());

			List<Breakpoint> list = new List<Breakpoint> (brs);
			foreach (Breakpoint bp in list)
				breakpoints.Remove (bp);
		}
		
		protected override void Update (CommandInfo info)
		{
			var breakpoints = DebuggingService.Breakpoints;

			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints);
			if (IdeApp.Workbench.ActiveDocument != null && 
			    IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true) != null &&
			    IdeApp.Workbench.ActiveDocument.FileName != FilePath.Null &&
			    !breakpoints.IsReadOnly) {
				info.Enabled = breakpoints.GetBreakpointsAtFileLine (IdeApp.Workbench.ActiveDocument.FileName, IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true).MDCaretLine ()).Count > 0;
			} else {
				info.Enabled = false;
			}
		}
	}

	class NewBreakpointHandler: CommandHandler
	{
		protected override void Run ()
		{
			BreakEvent bp = null;
			if (DebuggingService.ShowBreakpointProperties (ref bp)) {
				var breakpoints = DebuggingService.Breakpoints;

				breakpoints.Add (bp);
			}
		}

		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints);
			info.Enabled = !DebuggingService.Breakpoints.IsReadOnly;
		}
	}

	class NewFunctionBreakpointHandler: CommandHandler
	{
		protected override void Run ()
		{
			BreakEvent bp = null;
			if (DebuggingService.ShowBreakpointProperties (ref bp, BreakpointType.Function)) {
				var breakpoints = DebuggingService.Breakpoints;

				breakpoints.Add (bp);
			}
		}

		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints);
			info.Enabled = !DebuggingService.Breakpoints.IsReadOnly;
		}
	}

	class NewCatchpointHandler: CommandHandler
	{
		protected override void Run ()
		{
			BreakEvent bp = null;
			if (DebuggingService.ShowBreakpointProperties (ref bp, BreakpointType.Catchpoint)) {
				var breakpoints = DebuggingService.Breakpoints;

				breakpoints.Add (bp);
			}
		}

		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Catchpoints);
			info.Enabled = !DebuggingService.Breakpoints.IsReadOnly;
		}
	}

	class ShowBreakpointsHandler: CommandHandler
	{
		protected override void Run ()
		{
			if (!IdeApp.Workbench.Visible) {
				IdeApp.Workbench.Present ();
			}
			var breakpointsPad = IdeApp.Workbench.Pads.FirstOrDefault (p => p.Id == "MonoDevelop.Debugger.BreakpointPad");
			if (breakpointsPad != null) {
				breakpointsPad.BringToFront ();
			}
		}

		protected override void Update (CommandInfo info)
		{
			info.Enabled = true;
		}
	}

	class RunToCursorHandler : CommandHandler
	{
		protected override void Run ()
		{
			var doc = IdeApp.Workbench.ActiveDocument;
			var textView = doc.GetContent<ITextView> (true);
			var (caretLine, caretColumn) = textView.MDCaretLineAndColumn ();
			if (DebuggingService.IsPaused) {
				DebuggingService.RunToCursor (doc.FileName, caretLine, caretColumn);
				return;
			}

			if (IdeApp.Workspace.IsOpen) {
				var bp = new RunToCursorBreakpoint (doc.FileName, caretLine, caretColumn);
				DebuggingService.Breakpoints.Add (bp);
				var target = DebugHandler.GetRunTarget ();
				if (target != null)
					IdeApp.ProjectOperations.Debug (target);
			}
		}

		protected override void Update (CommandInfo info)
		{
			info.Visible = true;

			if (!IdeApp.Workspace.IsOpen || !DebuggingService.IsDebuggingSupported || !DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints) || DebuggingService.Breakpoints.IsReadOnly) {
				info.Enabled = false;
				return;
			}

			var doc = IdeApp.Workbench.ActiveDocument;

			if (doc?.GetContent<ITextView> (true) != null && doc.FileName != FilePath.Null) {
				var target = DebugHandler.GetRunTarget ();
				if (target != null && IdeApp.ProjectOperations.CanDebug (target)) {
					info.Enabled = true;
					return;
				}
			}
			info.Enabled = false;
		}
	}
	
	class ShowBreakpointPropertiesHandler: CommandHandler
	{
		protected override void Run ()
		{
			var breakpoints = DebuggingService.Breakpoints;
			IList<Breakpoint> brs;

			brs = breakpoints.GetBreakpointsAtFileLine (
				IdeApp.Workbench.ActiveDocument.FileName,
				IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true).MDCaretLine ());

			if (brs.Count > 0) {
				BreakEvent be = brs [0];
				DebuggingService.ShowBreakpointProperties (ref be);
			}
		}
		
		protected override void Update (CommandInfo info)
		{
			var breakpoints = DebuggingService.Breakpoints;

			info.Visible = DebuggingService.IsFeatureSupported (DebuggerFeatures.Breakpoints);
			if (IdeApp.Workbench.ActiveDocument != null && 
			    IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true) != null &&
			    IdeApp.Workbench.ActiveDocument.FileName != FilePath.Null &&
			    !breakpoints.IsReadOnly) {
				info.Enabled = breakpoints.GetBreakpointsAtFileLine (IdeApp.Workbench.ActiveDocument.FileName, IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true).MDCaretLine ()).Count > 0;
			} else {
				info.Enabled = false;
			}
		}
	}

	class ExpressionEvaluatorCommand: CommandHandler
	{
		protected override void Run ()
		{
			var textView = IdeApp.Workbench.ActiveDocument.GetContent<ITextView> (true);
			if (textView != null) {
				var viewPrimitives = MonoDevelop.Ide.Composition.CompositionManager.GetExport<IEditorPrimitivesFactoryService> ().Value.GetViewPrimitives (textView);
				var selectedText = viewPrimitives.Selection.GetText ();
				if (!string.IsNullOrWhiteSpace (selectedText)) {
					DebuggingService.ShowExpressionEvaluator (selectedText);
					return;
				}

				// GetCurrentWord() works correctly only in new editor
				if (IdeApp.Workbench.ActiveDocument.Editor == null) {
					var currentWordText = viewPrimitives.Caret.GetCurrentWord ().GetText ();
					if (!string.IsNullOrWhiteSpace (currentWordText)) {
						DebuggingService.ShowExpressionEvaluator (currentWordText);
						return;
					}
				}
			}
			DebuggingService.ShowExpressionEvaluator (null);
		}

		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsDebuggingSupported && DebuggingService.IsDebugging;
			info.Enabled = DebuggingService.CurrentFrame != null;
		}
	}
	
	class ShowCurrentExecutionLineCommand : CommandHandler
	{
		protected override void Run ()
		{
			DebuggingService.ShowCurrentExecutionLine ();
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Enabled = DebuggingService.IsPaused;
			info.Visible = DebuggingService.IsDebuggingSupported;
		}
	}
	
	class StopEvaluationHandler : CommandHandler
	{
		protected override void Run ()
		{
			DebuggingService.DebuggerSession.CancelAsyncEvaluations ();
		}
		
		protected override void Update (CommandInfo info)
		{
			info.Visible = DebuggingService.IsDebugging && DebuggingService.IsPaused && DebuggingService.DebuggerSession.CanCancelAsyncEvaluations;
		}
	}

	class SetNextStatementHandler : CommandHandler
	{
		protected override void Update (CommandInfo info)
		{
			var doc = IdeApp.Workbench.ActiveDocument;

			if (doc != null && doc.FileName != FilePath.Null && doc.GetContent<ITextView> (true) != null && DebuggingService.IsDebuggingSupported) {
				info.Enabled = DebuggingService.IsPaused && DebuggingService.DebuggerSession.CanSetNextStatement;
				info.Visible = DebuggingService.IsPaused;
			} else {
				info.Visible = false;
				info.Enabled = false;
			}
		}

		protected override void Run ()
		{
			var doc = IdeApp.Workbench.ActiveDocument;

			try {
				var (caretLine, caretColumn) = doc.GetContent<ITextView> (true).MDCaretLineAndColumn ();
				DebuggingService.SetNextStatement (doc.FileName, caretLine, caretColumn);
			} catch (Exception e) {
				if (e is NotSupportedException || e.InnerException is NotSupportedException) {
					string message;
					if (e is NotSupportedException)
						message = e.Message;
					else
						message = e.InnerException.Message;
					if (message == "Unable to set the next statement. The next statement cannot be set to another function.")
						MessageService.ShowError (GettextCatalog.GetString ("Unable to set the next statement. The next statement cannot be set to another function."));
					else
						MessageService.ShowError (GettextCatalog.GetString ("Unable to set the next statement to this location."));
				} else {
					throw;
				}
			}
		}
	}

	class ShowNextStatementHandler : CommandHandler
	{
		protected override void Update (CommandInfo info)
		{
			info.Enabled = DebuggingService.IsPaused && DebuggingService.DebuggerSession.CanSetNextStatement;
			info.Visible = DebuggingService.IsPaused;
		}

		protected override void Run ()
		{
			DebuggingService.ShowNextStatement ();
		}
	}
}
