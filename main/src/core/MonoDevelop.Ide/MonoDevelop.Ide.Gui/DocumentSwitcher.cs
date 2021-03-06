//
// WindowSwitcher.cs
//
// Author:
//   Mike Krüger <mkrueger@novell.com>
//
// Copyright (C) 2008 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Linq;
using Gdk;
using Gtk;
using MonoDevelop.Ide.Gui;
using MonoDevelop.Components.Commands;
using MonoDevelop.Ide.Commands;
using MonoDevelop.Core;
using Mono.TextEditor;


namespace MonoDevelop.Ide
{
	class DocumentList : Gtk.DrawingArea
	{ 
		List<Category> categories = new List<Category> ();
		int maxLength = 15;
		const int maxItems = 20;
		const int padding = 6;
		const int headerDistance = 4;
		const int maxRows = 2;
		const int itemPadding = 3;
		
		Item activeItem;
		public Item ActiveItem {
			get { return activeItem; }
			set {
				activeItem = value;
				OnActiveItemChanged (EventArgs.Empty);
				QueueDraw ();
			}
		}
		
		Item hoverItem;

		protected virtual void OnActiveItemChanged (EventArgs e)
		{
			EventHandler handler = this.ActiveItemChanged;
			if (handler != null)
				handler (this, e);
		}

		public event EventHandler ActiveItemChanged;
		
		public DocumentList ()
		{
			this.CanFocus = true;
			Events |= EventMask.KeyPressMask | EventMask.KeyReleaseMask | EventMask.PointerMotionMask | EventMask.ButtonPressMask | EventMask.LeaveNotifyMask;
		}
		
		public void AddCategory (Category category)
		{
			categories.Add (category);
		}
		
		Item GetItemAt (double x, double y)
		{
			double xPos = padding, yPos = padding;
			using (var layout = PangoUtil.CreateLayout (this)) {
				int w, h;
					
				foreach (Category cat in categories) {
					if (cat.Items.Count == 0)
						continue;
					yPos = padding;
					layout.SetMarkup ("<b>" + cat.Title + "</b>");
					layout.SetMarkup ("");
					layout.GetPixelSize (out w, out h);
					yPos += h;
					yPos += headerDistance;
					var startY = yPos;
					int curItem = 0;
					layout.SetText (new string ('X', maxLength));
					layout.GetPixelSize (out w, out h);
					var iconHeight = Math.Max (h, cat.Items[0].Icon.Height + 2) + itemPadding * 2;
					if (cat.FirstVisibleItem > 0) {
						yPos += iconHeight;
						curItem++;
					}
					for (int i = cat.FirstVisibleItem; i < cat.Items.Count; i++) {
						var item = cat.Items[i];
						int itemWidth = w + item.Icon.Width + 2 + itemPadding * 2;
						if (xPos <= x && yPos <= y && x < xPos + itemWidth && y < yPos + iconHeight)
							return item;
						yPos += iconHeight;
						if (++curItem >= maxItems) {
							curItem = 0;
							yPos = startY;
							xPos += w + cat.Items[0].Icon.Width + 2 + padding + itemPadding * 2;
						}
					}
					xPos += w + cat.Items[0].Icon.Width + 2 + padding + itemPadding * 2;
				}
			}
			return null;
		}
		
		protected override bool OnButtonPressEvent (EventButton evnt)
		{
			if (evnt.Button == 1 && hoverItem != null)
				ActiveItem = hoverItem;
			return base.OnButtonPressEvent (evnt);
		}
		
		protected override bool OnMotionNotifyEvent (EventMotion evnt)
		{
			var item = GetItemAt (evnt.X, evnt.Y);
			if (item != hoverItem) {
				hoverItem = item;
				QueueDraw ();
			}
			return base.OnMotionNotifyEvent (evnt);
		}
		
		protected override bool OnLeaveNotifyEvent (EventCrossing evnt)
		{
			if (hoverItem != null) {
				hoverItem = null;
				QueueDraw ();
			}
			return base.OnLeaveNotifyEvent (evnt);
		}

		
		static string Ellipsize (string str, int maxLength)
		{
			if (str != null && str.Length > maxLength)
				return str.Substring (0, maxLength - 3) + "...";
			return str;
		}
		
		const int upperGradientHeight = 16;
		protected override bool OnExposeEvent (Gdk.EventExpose e)
		{
			
			using (Cairo.Context cr = Gdk.CairoHelper.Create (e.Window)) {
				double xPos = padding, yPos = padding;
				var layout = PangoUtil.CreateLayout (this);
				int w, h;
				layout.SetText (new string ('X', maxLength));
				layout.GetPixelSize (out w, out h);
				
				foreach (Category cat in categories) {
					yPos = padding;
					cr.MoveTo (xPos, yPos);
					layout.SetMarkup ("<b>" + cat.Title + "</b>");
					cr.Color = (HslColor)Style.Text (StateType.Normal);
					cr.ShowLayout (layout);
					
					if (cat.Items.Count == 0)
						continue;
					
					layout.SetMarkup ("");
					int w2, h2;
					layout.GetPixelSize (out w2, out h2);
					yPos += h2;
					yPos += headerDistance;
					var startY = yPos;
					int curItem = 0;
					int row = 0;
					var iconHeight = Math.Max (h, cat.Items [0].Icon.Height + 2) + itemPadding * 2;
					if (cat.FirstVisibleItem > 0) {
						Gtk.Style.PaintArrow (Style, e.Window, State, ShadowType.None, 
								new Rectangle ((int)xPos, (int)yPos, w, h), 
								this, 
								"", 
								ArrowType.Up, 
								true, 
								(int)xPos, 
								(int)yPos, 
								w, 
								h);
						yPos += iconHeight;
						curItem++;
					}
					
					for (int i = cat.FirstVisibleItem; i < cat.Items.Count; i++) {
						var item = cat.Items [i];
						
						if (curItem + 1 >= maxItems && row + 1 >= maxRows && i + 1 < cat.Items.Count) {
							Gtk.Style.PaintArrow (Style, e.Window, State, ShadowType.None, 
								new Rectangle ((int)xPos, (int)yPos, w, h), 
								this, 
								"", 
								ArrowType.Down, 
								true, 
								(int)xPos, 
								(int)yPos, 
								w, 
								h);
							break;
						}
						
						if (item == ActiveItem) {
							int itemWidth = w + item.Icon.Width + 2 + itemPadding * 2;
							cr.Rectangle (xPos, yPos, itemWidth, iconHeight);
							cr.LineWidth = 1;
							cr.Color = (HslColor)Style.Base (StateType.Selected);
							cr.Fill ();
						} else if (item == hoverItem) {
							int itemWidth = w + item.Icon.Width + 2 + itemPadding * 2;
							cr.Rectangle (xPos + 0.5, yPos + 0.5, itemWidth - 1, iconHeight);
							cr.LineWidth = 1;
							cr.Color = (HslColor)Style.Base (StateType.Selected);
							cr.Stroke ();
						}
						cr.Color = (HslColor)Style.Text (item == ActiveItem? StateType.Selected : StateType.Normal);
						cr.MoveTo (xPos + item.Icon.Width + 2 + itemPadding, yPos + (iconHeight - h) / 2);
						layout.SetText (Ellipsize (item.ListTitle ?? item.Title, maxLength));
						cr.ShowLayout (layout);
						e.Window.DrawPixbuf (Style.BaseGC (StateType.Normal), item.Icon, 0, 0, (int)xPos + itemPadding,
							(int)(yPos + (iconHeight - item.Icon.Height) / 2), item.Icon.Width, item.Icon.Height, RgbDither.None, 0, 0);
						
						yPos += iconHeight;
						if (++curItem >= maxItems) {
							curItem = 0;
							yPos = startY;
							xPos += w + cat.Items [0].Icon.Width + 2 + padding + itemPadding * 2;
							row++;
						}
					}
					
				
					xPos += w + cat.Items [0].Icon.Width + 2 + padding + itemPadding * 2;
				}
				layout.Dispose ();
			}
			return true;
		}
		
		protected override bool OnKeyPressEvent (EventKey evnt)
		{
			Gdk.Key key;
			Gdk.ModifierType mod;
			Mono.TextEditor.KeyboardShortcut[] accels;
			Mono.TextEditor.GtkWorkarounds.MapKeys (evnt, out key, out mod, out accels);
			
			switch (accels[0].Key) {
			case Gdk.Key.Left:
				LeftItem ();
				break;
			case Gdk.Key.Right:
				RightItem ();
				break;
			case Gdk.Key.Up:
				PrevItem (false);
				break;
			case Gdk.Key.Down:
				NextItem (false);
				break;
			case Gdk.Key.Tab:
				if ((accels[0].Modifier & ModifierType.ShiftMask) == 0)
					NextItem (true);
				else
					PrevItem (true);
				break;
			}
			return base.OnKeyPressEvent (evnt);
		}
		
		protected override bool OnKeyReleaseEvent (Gdk.EventKey evnt)
		{
			if (evnt.Key == Gdk.Key.Control_L || evnt.Key == Gdk.Key.Control_R) {
				OnRequestClose (EventArgs.Empty);
			}
			return base.OnKeyReleaseEvent (evnt);
		}
		
		protected virtual void OnRequestClose (EventArgs e)
		{
			EventHandler handler = this.RequestClose;
			if (handler != null)
				handler (this, e);
		}

		public event EventHandler RequestClose;
		
		void LeftItem ()
		{
			for (int i = 0; i < categories.Count; i++) {
				var cat = categories[i];
				int idx = cat.Items.IndexOf (ActiveItem);
				if (idx < 0)
					continue;
				int relIndex = idx - cat.FirstVisibleItem;
				if (relIndex / maxItems == 0) {
					Category prevCat = GetPrevCat (i);
					if (prevCat == cat)
						return;
					int newIndex = Math.Min (prevCat.Items.Count - 1, prevCat.FirstVisibleItem + relIndex);
					ActiveItem = prevCat.Items [newIndex];
				} else {
					ActiveItem = cat.Items [relIndex - maxItems];
				}
				
			}
		}
		
		void RightItem ()
		{
			for (int i = 0; i < categories.Count; i++) {
				var cat = categories[i];
				int idx = cat.Items.IndexOf (ActiveItem);
				if (idx < 0)
					continue;
				int relIndex = idx - cat.FirstVisibleItem;
				if (relIndex / maxItems == maxRows - 1 || relIndex + maxItems >= cat.Items.Count) {
					Category nextCat = GetNextCat (i);
					if (nextCat == cat)
						return;
					if (i + 1 < categories.Count) {
						int newIndex = Math.Min (nextCat.Items.Count - 1, nextCat.FirstVisibleItem + relIndex);
						ActiveItem = nextCat.Items [newIndex];
					}
				} else {
					ActiveItem = cat.Items [relIndex + maxItems];
				}
			}
		}
		
		public void NextItem (bool stayInCategory)
		{
			for (int i = 0; i < categories.Count; i++) {
				var cat = categories[i];
				int idx = cat.Items.IndexOf (ActiveItem);
				if (idx < 0)
					continue;
				if (idx + 1 < cat.Items.Count) {
					if (idx + 1 > cat.FirstVisibleItem + maxItems * maxRows - 3)
						cat.FirstVisibleItem++;
					ActiveItem = cat.Items[idx + 1];
				} else {
					if (!stayInCategory) {
						cat = GetNextCat (i);
					}
					cat.FirstVisibleItem = 0;
					ActiveItem = cat.Items[0];
				}
				break;
			}
		}
		
		public void PrevItem (bool stayInCategory)
		{
			for (int i = 0; i < categories.Count; i++) {
				var cat = categories[i];
				int idx = cat.Items.IndexOf (ActiveItem);
				if (idx < 0)
					continue;
				if (idx - 1 >= 0) {
					if (idx - 1 < cat.FirstVisibleItem)
						cat.FirstVisibleItem--;
					ActiveItem = cat.Items[idx - 1];
				} else {
					if (!stayInCategory) {
						cat = GetPrevCat (i);
					}
					if (cat.Items.Count - 1 > cat.FirstVisibleItem + maxItems * maxRows)
						cat.FirstVisibleItem = cat.Items.Count - maxItems * maxRows + 1;
					ActiveItem = cat.Items[cat.Items.Count - 1];
				}
				break;
			}
		}
		
		Category GetNextCat (int i)
		{
			Category cat;
			do {
				i = (i + 1) % (categories.Count);
				cat = categories[i];
			} while (cat.Items.Count == 0);
			return cat;
		}
		
		Category GetPrevCat (int i)
		{
			Category cat;
			do {
				i = (categories.Count + i - 1) % (categories.Count);
				cat = categories[i];
			} while (cat.Items.Count == 0);
			return cat;
		}
		
		protected override void OnSizeRequested (ref Requisition req)
		{
			maxLength = 15;
			foreach (var cat in categories) {
				foreach (var item in cat.Items) {
					maxLength = Math.Min (30, Math.Max (maxLength, (item.ListTitle ?? item.Title).Length));
				}
			}
			
			var layout = PangoUtil.CreateLayout (this);
			int w, h;
			layout.SetText (new string ('X', maxLength));
			layout.GetPixelSize (out w, out h);
			layout.Dispose ();
			int totalWidth = 0;
			int totalHeight = 0;
			
			var firstNonEmptyCat = categories.FirstOrDefault (c => c.Items.Count > 0);
			if (firstNonEmptyCat == null)
				return;
			
			var icon = firstNonEmptyCat.Items[0].Icon;
			var iconHeight = Math.Max (h, icon.Height + 2) + itemPadding * 2;
			var iconWidth = icon.Width + 2 + w  + itemPadding * 2;
				
			foreach (var cat in categories) {
				var headerHeight = h + headerDistance;
				totalHeight = Math.Max (totalHeight, headerHeight + (Math.Min (cat.Items.Count, maxItems)) * iconHeight);
				totalWidth += (1 + Math.Min (maxRows - 1, cat.Items.Count / maxItems)) * iconWidth;
			}
			req.Width = totalWidth + padding * 2 + (categories.Count - 1) * padding;
			req.Height = totalHeight + padding * 2;
		}
		
		public class Item
		{
			public string Title {
				get;
				set;
			}
			
			public string ListTitle {
				get;
				set;
			}
			
			public Pixbuf Icon {
				get;
				set;
			}
			
			public string Description {
				get;
				set;
			}
			
			public string Path {
				get;
				set;
			}
			
			public object Tag {
				get;
				set;
			}
			
			public override string ToString ()
			{
				return string.Format ("[Item: Title={0}]", Title);
			}

		}
		
		public class Category
		{
			public string Title {
				get;
				set;
			}
			
			List<Item> items = new List<Item> ();
			public List<Item> Items {
				get {
					return items;
				}
			}
			
			public int FirstVisibleItem {
				get;
				set;
			}
			
			public Category (string title)
			{
				this.Title = title;
			}
			
			public void AddItem (Item item)
			{
				items.Add (item);
			}
			
			public override string ToString ()
			{
				return string.Format ("[Category: Title={0}]", Title);
			}
		}
	}
	
	internal class DocumentSwitcher : Gtk.Window
	{
		List<MonoDevelop.Ide.Gui.Document> documents;
		Gtk.Image imageTitle = new Gtk.Image ();
		Label labelFileName = new Label ();
		Label labelType     = new Label ();
		Label labelTitle    = new Label ();
		DocumentList documentList = new DocumentList ();
		
		public DocumentSwitcher (Gtk.Window parent, bool startWithNext) : base(Gtk.WindowType.Toplevel)
		{
			IdeApp.CommandService.IsEnabled = false;
			this.documents = new List<MonoDevelop.Ide.Gui.Document> (IdeApp.Workbench.Documents.OrderByDescending (d => d.LastTimeActive));
			this.TransientFor = parent;
			
			this.Decorated = false;
			this.DestroyWithParent = true;
			this.CanDefault = true;
			
			this.Modal = true;
			this.WindowPosition = Gtk.WindowPosition.CenterOnParent;
			this.TypeHint = WindowTypeHint.Dialog;
			
			this.ModifyBg (StateType.Normal, this.Style.Base (StateType.Normal));
			
			VBox vBox = new VBox ();
			HBox hBox = new HBox ();
			
			var hBox2 = new HBox ();
			hBox2.PackStart (hBox, false, false, 8);
			
			hBox.PackStart (imageTitle, true, false, 2);
			labelTitle.Xalign = 0;
			labelTitle.HeightRequest = 24;
			hBox.PackStart (labelTitle, true, true, 2);
			vBox.PackStart (hBox2, false, false, 6);
			
			labelType.Xalign = 0;
			labelType.HeightRequest = 16;
			hBox = new HBox ();
			hBox.PackStart (labelType, false, false, 8);
			vBox.PackStart (hBox, false, false, 2);
			
			hBox = new HBox ();
			hBox.PackStart (documentList, true, true, 1);
			vBox.PackStart (hBox, false, false, 0);
			
			labelFileName.Xalign = 0;
			labelFileName.Ellipsize = Pango.EllipsizeMode.Start;
			hBox = new HBox ();
			hBox.PackStart (labelFileName, true, true, 8);
			vBox.PackEnd (hBox, false, false, 6);
			
			Add (vBox);
			
			var padCategory = new  DocumentList.Category (GettextCatalog.GetString ("Pads"));
			DocumentList.Item activeItem = null;
			
			foreach (Pad pad in IdeApp.Workbench.Pads) {
				if (!pad.Visible)
					continue;
				var item = new DocumentList.Item () {
					Icon = ImageService.GetPixbuf (pad.Icon.Name ?? MonoDevelop.Ide.Gui.Stock.MiscFiles, IconSize.Menu),
					Title = pad.Title,
					Tag = pad
				};
				if (pad.Window.Content.Control.HasFocus)
					activeItem = item;
				padCategory.AddItem (item);
			}
			documentList.AddCategory (padCategory);
			
			var documentCategory = new  DocumentList.Category (GettextCatalog.GetString ("Documents"));
			foreach (var doc in documents) {
				var item = new DocumentList.Item () {
					Icon = GetIconForDocument (doc, IconSize.Menu),
					Title = System.IO.Path.GetFileName (doc.Name),
					ListTitle = doc.Window.Title,
					Description = doc.Window.DocumentType,
					Path = doc.Name,
					Tag = doc
				};
				if (doc.Window.ActiveViewContent.Control.HasFocus)
					activeItem = item;
				documentCategory.AddItem (item);
			}
			documentList.AddCategory (documentCategory);
		
			documentList.ActiveItemChanged += delegate {
				if (documentList.ActiveItem == null) {
					labelFileName.Text = labelType.Text = labelTitle.Text = "";
					return;
				}
				imageTitle.Pixbuf = documentList.ActiveItem.Icon;
				labelFileName.Text = documentList.ActiveItem.Path;
				labelType.Markup = "<span size=\"small\">" + documentList.ActiveItem.Description + "</span>";
				labelTitle.Markup =  "<span size=\"xx-large\" weight=\"bold\">" + documentList.ActiveItem.Title + "</span>";
			};
			
			if (activeItem == null) {
				if (documentCategory.Items.Count > 0) {
					activeItem = documentCategory.Items[0];
				} else if (padCategory.Items.Count > 0) {
					activeItem = padCategory.Items[0];
				} else {
					Destroy ();
					return;
				}
			}
			
			documentList.ActiveItem = activeItem;
			documentList.NextItem (true);
			documentList.RequestClose += delegate {
				if (documentList.ActiveItem.Tag is Pad) {
					((Pad)documentList.ActiveItem.Tag).BringToFront (true);
				} else {
					((MonoDevelop.Ide.Gui.Document)documentList.ActiveItem.Tag).Select ();
				}
				Destroy ();
			};
			
			this.ShowAll ();
			documentList.GrabFocus ();
			this.GrabDefault ();
		}
		
		Pixbuf GetIconForDocument (MonoDevelop.Ide.Gui.Document document, Gtk.IconSize iconSize)
		{
			if (!string.IsNullOrEmpty (document.Window.ViewContent.StockIconId))
				return ImageService.GetPixbuf (document.Window.ViewContent.StockIconId, iconSize);
			if (string.IsNullOrEmpty (document.FileName)) 
				return ImageService.GetPixbuf (MonoDevelop.Ide.Gui.Stock.MiscFiles, iconSize);
			
			return DesktopService.GetPixbufForFile (document.FileName, iconSize);
		}
		
		
		protected override bool OnFocusOutEvent (EventFocus evnt)
		{
			Destroy ();
			return base.OnFocusOutEvent (evnt);
		}
		
		protected override void OnDestroyed ()
		{
			base.OnDestroyed ();
			IdeApp.CommandService.IsEnabled = true;
		}
		
		protected override bool OnExposeEvent (EventExpose evnt)
		{
			base.OnExposeEvent (evnt);
			
			int winWidth, winHeight;
			this.GetSize (out winWidth, out winHeight);
			
			this.GdkWindow.DrawRectangle (this.Style.ForegroundGC (StateType.Insensitive), false, 0, 0, winWidth - 1, winHeight - 1);
			return false;
		}
	}
}