﻿using ICSharpCode.AvalonEdit.Document;
using Satolist2.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Satolist2.Control
{
	/// <summary>
	/// TemporaryTextEditor.xaml の相互作用ロジック
	/// </summary>
	public partial class TemporaryTextEditor : UserControl
	{
		public TemporaryTextEditor()
		{
			InitializeComponent();

			var hilighter = new SatoriSyntaxHilighter();
			MainTextEditor.SyntaxHighlighting = hilighter;
			MainTextEditor.Foreground = hilighter.MainForegroundColor;
		}

		public void RequestFocus()
		{
			Dispatcher.BeginInvoke(new Action(() => { MainTextEditor.Focus(); }), System.Windows.Threading.DispatcherPriority.Render);
		}
	}

	internal class TemporaryTextEditorViewModel : TextEditorViewModelBase, IControlBindedReceiver
	{
		private TemporaryTextEditor control;
		private TextDocument document;
		private string title;

		public TextDocument Document
		{
			get => document;
			set
			{
				Document = value;
				NotifyChanged();
				NotifyChanged(nameof(Text));
			}
		}

		public string Text
		{
			get => document.Text;
			set
			{
				document.Text = value;
				NotifyChanged();
				NotifyChanged(nameof(Document));
			}
		}

		public string Title
		{
			get => title;
			set
			{
				title = value;
				NotifyDocumentTitleChanged();
				NotifyChanged();
			}
		}

		public override string DocumentTitle => Title;

		public override string DockingContentId { get; } = Guid.NewGuid().ToString();

		public override ICSharpCode.AvalonEdit.TextEditor MainTextEditor => control.MainTextEditor;

		public TemporaryTextEditorViewModel(MainViewModel main):base(main)
		{
			document = new TextDocument();
			title = "無題";
			document.Text = string.Empty;
		}

		public void ControlBind(System.Windows.Controls.Control control)
		{
			this.control = (TemporaryTextEditor)control;
		}
	}
}
