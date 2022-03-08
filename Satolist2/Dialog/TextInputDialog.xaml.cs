﻿using System;
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
using System.Windows.Shapes;

namespace Satolist2.Dialog
{
	/// <summary>
	/// TextInputDialog.xaml の相互作用ロジック
	/// </summary>
	public partial class TextInputDialog : Window
	{
		public TextInputDialog()
		{
			InitializeComponent();
		}

		private void CancelButtonClick(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
