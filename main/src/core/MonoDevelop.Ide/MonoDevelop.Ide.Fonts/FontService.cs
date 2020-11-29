﻿// 
// IdeServices.FontService.cs
//  
// Author:
//       Mike Krüger <mkrueger@novell.com>
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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Mono.Addins;
using MonoDevelop.Core;
using Pango;
#if MAC
using AppKit;
#endif

namespace MonoDevelop.Ide.Fonts
{
	[DefaultServiceImplementation]
	public class FontService: Service
	{
		List<FontDescriptionCodon> fontDescriptions = new List<FontDescriptionCodon> ();
		Dictionary<string, FontDescription> loadedFonts = new Dictionary<string, FontDescription> ();
		Properties fontProperties;
		DesktopService desktopService;

		string defaultMonospaceFontName = String.Empty;
		FontDescription defaultMonospaceFont = new FontDescription ();

		void LoadDefaults ()
		{
			if (defaultMonospaceFont != null) {
				defaultMonospaceFont.Dispose ();
			}

			#pragma warning disable 618
			defaultMonospaceFontName = desktopService.DefaultMonospaceFont;
			defaultMonospaceFont = FontDescription.FromString (defaultMonospaceFontName);
			#pragma warning restore 618
		}
		
		internal IEnumerable<FontDescriptionCodon> FontDescriptions {
			get {
				return fontDescriptions;
			}
		}

		protected override async Task OnInitialize (ServiceProvider serviceProvider)
		{
			desktopService = await serviceProvider.GetService<DesktopService> ();
			fontProperties = PropertyService.Get ("FontProperties", new Properties ());
			
			AddinManager.AddExtensionNodeHandler ("/MonoDevelop/Ide/Fonts", delegate(object sender, ExtensionNodeEventArgs args) {
				var codon = (FontDescriptionCodon)args.ExtensionNode;
				switch (args.Change) {
				case ExtensionChange.Add:
					fontDescriptions.Add (codon);
					break;
				case ExtensionChange.Remove:
					fontDescriptions.Remove (codon);
					if (loadedFonts.ContainsKey (codon.Name))
						loadedFonts.Remove (codon.Name);
					break;
				}
			});

			LoadDefaults ();
		}

		public FontDescription MonospaceFont { get { return defaultMonospaceFont; } }
		public FontDescription SansFont { get { return Gui.Styles.DefaultFont; } }

		public string MonospaceFontName { get { return defaultMonospaceFontName; } }
		public string SansFontName { get { return Gui.Styles.DefaultFontName; } }

		[Obsolete ("Use MonospaceFont")]
		public FontDescription DefaultMonospaceFontDescription {
			get {
				if (defaultMonospaceFont == null)
					defaultMonospaceFont = LoadFont (desktopService.DefaultMonospaceFont);
				return defaultMonospaceFont;
			}
		}

		FontDescription LoadFont (string name)
		{
			var fontName = FilterFontName (name);
			return FontDescription.FromString (fontName);
		}
		
		public string FilterFontName (string name)
		{
			switch (name) {
			case "_DEFAULT_MONOSPACE":
				return defaultMonospaceFontName;
			case "_DEFAULT_SANS":
				return SansFontName;
			default:
				return name;
			}
		}
		
		public string GetUnderlyingFontName (string name)
		{
			var result = fontProperties.Get<string> (name);
			
			if (result == null) {
				var font = GetFontDescriptionCodon (name);
				if (font == null)
					throw new InvalidOperationException ("Font " + name + " not found.");
				return font.FontDescription;
			}
			return result;
		}

		/// <summary>
		/// Gets the font description for the provided font id
		/// </summary>
		/// <returns>
		/// The font description.
		/// </returns>
		/// <param name='name'>
		/// Identifier of the font
		/// </param>
		/// <param name='createDefaultFont'>
		/// If set to <c>false</c> and no custom font has been set, the method will return null.
		/// </param>
		public FontDescription GetFontDescription (string name, bool createDefaultFont = true)
		{
			if (loadedFonts.ContainsKey (name))
				return loadedFonts [name];
			return loadedFonts [name] = LoadFont (GetUnderlyingFontName (name));
		}

		public Xwt.Drawing.Font GetFont (string name)
		{
			var fontDescription = GetUnderlyingFontName (name);
			return Xwt.Drawing.Font.FromName (fontDescription);
		}

		internal FontDescriptionCodon GetFontDescriptionCodon (string name)
		{
			foreach (var d in fontDescriptions) {
				if (d.Name == name)
					return d;
			}
			LoggingService.LogError ("Font " + name + " not found.");
			return null;
		}
		
		public void SetFont (string name, string value)
		{
			if (loadedFonts.ContainsKey (name)) 
				loadedFonts.Remove (name);

			var font = GetFontDescriptionCodon (name);
			if (font != null && font.FontDescription == value) {
				fontProperties.Set (name, null);
			} else {
				fontProperties.Set (name, value);
			}
			List<Action> callbacks;
			if (fontChangeCallbacks.TryGetValue (name, out callbacks)) {
				callbacks.ForEach (c => c ());
			}
		}

		internal ConfigurationProperty<FontDescription> GetFontProperty (string name)
		{
			return new FontConfigurationProperty (name);
		}
		
		Dictionary<string, List<Action>> fontChangeCallbacks = new Dictionary<string, List<Action>> ();
		public void RegisterFontChangedCallback (string fontName, Action callback)
		{
			if (!fontChangeCallbacks.ContainsKey (fontName))
				fontChangeCallbacks [fontName] = new List<Action> ();
			fontChangeCallbacks [fontName].Add (callback);
		}
		
		public void RemoveCallback (Action callback)
		{
			foreach (var list in fontChangeCallbacks.Values.ToList ())
				list.Remove (callback);
		}
	}

	class FontConfigurationProperty: ConfigurationProperty<FontDescription>
	{
		string name;

		public FontConfigurationProperty (string name)
		{
			this.name = name;
			IdeServices.FontService.RegisterFontChangedCallback (name, OnChanged);
		}

		protected override FontDescription OnGetValue ()
		{
			return IdeServices.FontService.GetFontDescription (name);
		}

		protected override bool OnSetValue (FontDescription value)
		{
			IdeServices.FontService.SetFont (name, value.ToString ());
			return true;
		}
	}

	public static class FontExtensions
	{
		public static FontDescription CopyModified (this FontDescription font, double? scale = null, Pango.Weight? weight = null)
		{
			font = font.Copy ();

			if (scale.HasValue)
				Scale (font, scale.Value);

			if (weight.HasValue)
				font.Weight = weight.Value;

			return font;
		}
		public static FontDescription CopyModified (this FontDescription font, int absoluteResize, Pango.Weight? weight = null)
		{
			font = font.Copy ();

			ResizeAbsolute (font, absoluteResize);

			if (weight.HasValue)
				font.Weight = weight.Value;

			return font;
		}

		static void Scale (FontDescription font, double scale)
		{
			if (font.SizeIsAbsolute) {
				font.AbsoluteSize = scale * font.Size;
			} else {
				var size = font.Size;
				if (size == 0)
					size = (int)(10 * Pango.Scale.PangoScale); 
				font.Size = (int)(Pango.Scale.PangoScale * (int)(scale * size / Pango.Scale.PangoScale));
			}
		}

		static void ResizeAbsolute (FontDescription font, int pt)
		{
			if (font.SizeIsAbsolute) {
				font.AbsoluteSize = font.Size + pt;
			} else {
				var size = font.Size;
				if (size == 0)
					size = (int)((10 + pt) * Pango.Scale.PangoScale);
				font.Size = (int)(Pango.Scale.PangoScale * (int)(pt + size / Pango.Scale.PangoScale));
			}
		}

		public static FontDescription ToPangoFont (this Xwt.Drawing.Font font)
		{
			var backend = Xwt.Toolkit.GetBackend (font) as FontDescription;
			if (backend != null)
				return backend.Copy ();
			var description = FontDescription.FromString (font.Family + " " + font.Size);
			description.Weight = (Pango.Weight)font.Weight;
			description.Style = (Pango.Style)font.Style;
			description.Stretch = (Pango.Stretch)font.Stretch;
			return description;
		}
#if MAC
		public static NSFont ToNSFont (this Xwt.Drawing.Font font)
		{
			if (Xwt.Toolkit.GetBackend (font) is Xwt.Mac.FontData fontData)
				return fontData.Font;
			NSFont result = null;
			Xwt.Toolkit.NativeEngine.Invoke (() => {
				var nativeXwtFont = Xwt.Drawing.Font.FromName (font.ToString ());
				if (Xwt.Toolkit.GetBackend (nativeXwtFont) is Xwt.Mac.FontData fontData)
					result = fontData.Font;
			});
			return result;
		}
#endif

		public static Xwt.Drawing.Font ToXwtFont (this FontDescription font)
		{
			return font.ToXwtFont (null);
		}

		public static Xwt.Drawing.Font ToXwtFont (this FontDescription font, Xwt.Toolkit withToolkit)
		{
			var toolkit = withToolkit ?? Xwt.Toolkit.CurrentEngine;

			Xwt.Drawing.Font xwtFont = null;
			toolkit.Invoke (() => {
				xwtFont = Xwt.Drawing.Font.FromName (font.Family + " " + (int)(font.Size / Pango.Scale.PangoScale))
					.WithWeight ((Xwt.Drawing.FontWeight)font.Weight)
					.WithStyle ((Xwt.Drawing.FontStyle)font.Style)
					.WithStretch ((Xwt.Drawing.FontStretch)font.Stretch);
		});
			return xwtFont;
		}
	}
}
