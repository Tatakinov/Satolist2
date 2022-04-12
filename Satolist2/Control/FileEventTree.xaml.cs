﻿using Satolist2.Dialog;
using Satolist2.Model;
using Satolist2.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
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
	/// FileEventTree.xaml の相互作用ロジック
	/// </summary>
	public partial class FileEventTree : UserControl
	{
		public FileEventTree()
		{
			InitializeComponent();
		}

		private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if(DataContext is FileEventTreeViewModel treeViewModel)
			{
				if(e.NewValue is FileEventTreeItemEventViewModel eventViewModel)
				{
					treeViewModel.Main.EventListViewModel.SetItems(eventViewModel.Items, eventViewModel.Label, eventViewModel.Dictionary.Dictionary);
				}
				else if(e.NewValue is FileEventTreeItemInlineEventViewModel inlineEventViewModel)
				{
					treeViewModel.Main.EventListViewModel.SetItems(inlineEventViewModel.EventList, inlineEventViewModel.Label, inlineEventViewModel.Dictionary);
				}
			}
		}

		private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if(sender is TreeViewItem item)
			{
				if (item.DataContext is FileEventTreeItemDictionaryViewModel dic)
				{
					e.Handled = dic.Open();
				}
				else if(item.DataContext is FileEventTreeItemEventViewModel ev)
				{
					if(ev.Items.Count==1)
					{
						ev.OpenFirstEventEditor();
					}
				}
			}
		}
	}

	internal class FileEventTreeViewModel : NotificationObject, IDockingWindowContent
	{
		public const string ContentId = "FileEventTree";
		private ObservableCollection<FileEventTreeItemDictionaryViewModel> dictionaries;

		public MainViewModel Main { get; }
		public GhostModel Ghost
		{
			get => Main.Ghost;
		}

		public ReadOnlyObservableCollection<FileEventTreeItemDictionaryViewModel> Dictionaries
		{
			get => new ReadOnlyObservableCollection<FileEventTreeItemDictionaryViewModel>(dictionaries);
		}

		public string DockingTitle => "ファイルイベントツリー";

		public string DockingContentId => ContentId;

		public FileEventTreeViewModel(MainViewModel main)
		{
			Main = main;
			dictionaries = new ObservableCollection<FileEventTreeItemDictionaryViewModel>();

			if (Ghost != null)
			{
				//辞書生成
				foreach (var dic in Ghost.Dictionaries)
				{
					var dictViewModel = new FileEventTreeItemDictionaryViewModel(this, dic);
					dictionaries.Add(dictViewModel);
				}
			}
		}
	}

	//辞書ファイルにあたる
	internal class FileEventTreeItemDictionaryViewModel : NotificationObject
	{
		private ObservableCollection<FileEventTreeItemEventViewModel> items;
		private Dictionary<string, FileEventTreeItemEventViewModel> itemsDictionary;
		private Dictionary<EventModel, FileEventTreeItemEventViewModel> reverseDictionary;  //逆引き

		public string NodeType => "Dictionary";
		public FileEventTreeViewModel FileEventTree { get; }
		public DictionaryModel Dictionary { get; }

		public ReadOnlyObservableCollection<FileEventTreeItemEventViewModel> ChildItems
		{
			get => new ReadOnlyObservableCollection<FileEventTreeItemEventViewModel>(items);
		}

		//ファイルイベントツリー上で表示する名前
		public string Label => Dictionary.RelativeName;

		//アイテムを追加
		public ActionCommand AddItemCommand { get; }

		public FileEventTreeItemDictionaryViewModel(FileEventTreeViewModel parent, DictionaryModel dictionary)
		{
			FileEventTree = parent;
			Dictionary = dictionary;

			items = new ObservableCollection<FileEventTreeItemEventViewModel>();
			itemsDictionary = new Dictionary<string, FileEventTreeItemEventViewModel>();
			reverseDictionary = new Dictionary<EventModel, FileEventTreeItemEventViewModel>();

			//コマンド
			AddItemCommand = new ActionCommand(
				o => FileEventTree.Main.OpenAddEventDialog(addTarget: Dictionary)
				);

			//イベントハンドラの用意
			Dictionary.PropertyChanged += OnDictionaryPropertyChanged;
			INotifyCollectionChanged collectionChanged = Dictionary.Events;
			collectionChanged.CollectionChanged += EventCollectionChanged;

			//dictionaryから同名のeventsを引っ張ってくる
			foreach (var ev in Dictionary.Events)
			{
				ev.PropertyChanged += OnEventPropertyChanged;

				if (!itemsDictionary.ContainsKey(ev.Identifier))
				{
					var newItem = new FileEventTreeItemEventViewModel(this);
					itemsDictionary.Add(ev.Identifier, newItem);
					items.Add(newItem);
				}
				itemsDictionary[ev.Identifier].AddEvent(ev);
				reverseDictionary.Add(ev, itemsDictionary[ev.Identifier]);
			}
		}

		//項目の追加
		private void AddEvent(EventModel ev)
		{
			FileEventTreeItemEventViewModel item;
			if(!itemsDictionary.TryGetValue(ev.Identifier, out item))
			{
				item = new FileEventTreeItemEventViewModel(this);
				itemsDictionary.Add(ev.Identifier, item);
				items.Add(item);
			}

			item.AddEvent(ev);
			reverseDictionary[ev] = item;
		}

		//項目の削除
		private void RemoveEvent(EventModel ev)
		{
			var removeTarget = reverseDictionary[ev];
			if (removeTarget.ItemCount == 1)
			{
				itemsDictionary.Remove(ev.Identifier);
				items.Remove(removeTarget);
			}
			removeTarget.RemoveEvent(ev);
			reverseDictionary.Remove(ev);
		}

		//全項目の削除
		private void ClearEvents()
		{
			foreach(var ev in reverseDictionary.Keys.ToArray())
			{
				RemoveEvent(ev);
			}
		}

		//項目のリネーム
		private void RenameEvent(EventModel ev)
		{
			//名前が変更になった場合
			FileEventTreeItemEventViewModel eventViewModel = reverseDictionary[ev];
			string newLabel = ev.Identifier;
			string oldLabel = eventViewModel.Label;

			//移動先があるかを探す
			FileEventTreeItemEventViewModel moveTarget = null;
			if (itemsDictionary.ContainsKey(newLabel))
			{
				moveTarget = itemsDictionary[newLabel];
			}

			if (eventViewModel.ItemCount == 1)
			{
				if (moveTarget == null)
				{
					//アイテムが１個しかなく、移動先もない場合はその項目自体をリネームする
					eventViewModel.UpdateLabel();
					itemsDictionary.Remove(oldLabel);
					itemsDictionary.Add(newLabel, eventViewModel);
				}
				else
				{
					//アイテムが１個しかなく、移動先がある場合は項目を破棄し、既存の項目に合流する
					itemsDictionary.Remove(oldLabel);
					items.Remove(eventViewModel);
					eventViewModel.RemoveEvent(ev);
					moveTarget.AddEvent(ev);
					reverseDictionary[ev] = moveTarget;
				}
			}
			else
			{
				if (moveTarget == null)
				{
					//他の項目が存在し、移動先がない場合は項目を新設する
					eventViewModel.RemoveEvent(ev);

					var newItem = new FileEventTreeItemEventViewModel(this);
					newItem.AddEvent(ev);
					itemsDictionary.Add(newLabel, newItem);
					items.Add(newItem);
					reverseDictionary[ev] = newItem;
				}
				else
				{
					//他の項目が存在し、移動先がある場合はビューモデル間を移動させる
					eventViewModel.RemoveEvent(ev);
					moveTarget.AddEvent(ev);
					reverseDictionary[ev] = moveTarget;
				}
			}
		}

		private void OnEventPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (sender is EventModel ev)
			{
				switch (e.PropertyName)
				{
					case nameof(EventModel.Identifier):
						RenameEvent(ev);
						break;
				}
			}
		}

		private void OnDictionaryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(DictionaryModel.RelativeName):
					NotifyChanged(nameof(Label));
					break;
			}
		}

		//イベントの追加削除
		private void EventCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
			{
				foreach (object item in e.OldItems)
				{
					if (item is EventModel ev)
						RemoveEvent(ev);
				}
			}

			if (e.OldItems != null)
			{
				foreach (object item in e.NewItems)
				{
					if (item is EventModel ev)
						AddEvent(ev);
				}
			}
		}

		//開く
		public bool Open()
		{
			if (Dictionary.IsSerialized)
			{
				//シリアライズされてない版をエディタで開いく
				FileEventTree.Main.MainWindow.OpenTextEditor(Dictionary);
				return true;
			}
			return false;
		}
		
	}

	//イベントにあたる
	internal class FileEventTreeItemEventViewModel : NotificationObject
	{
		//ノードに所属するイベント
		private ObservableCollection<EventModel> events;
		private Dictionary<string, ObservableCollection<InlineEventModel>> inlineEvents;
		private ObservableCollection<FileEventTreeItemInlineEventViewModel> inlineEventViewModels;
		private string identifier;

		public string NodeType => "Event";
		public FileEventTreeItemDictionaryViewModel Dictionary { get; }
		public ActionCommand AddItemCommand { get; }

		public string Label
		{
			get => identifier;
			private set
			{
				identifier = value;
				NotifyChanged();
			}
		}

		public int ItemCount
		{
			get => events.Count;
		}

		public ReadOnlyObservableCollection<EventModel> Items
		{
			get => new ReadOnlyObservableCollection<EventModel>(events);
		}

		public IList<FileEventTreeItemInlineEventViewModel> ChildItems
		{
			get => new ReadOnlyObservableCollection<FileEventTreeItemInlineEventViewModel>(inlineEventViewModels);
		}


		public FileEventTreeItemEventViewModel(FileEventTreeItemDictionaryViewModel dict)
		{
			events = new ObservableCollection<EventModel>();
			inlineEventViewModels = new ObservableCollection<FileEventTreeItemInlineEventViewModel>();
			inlineEvents = new Dictionary<string, ObservableCollection<InlineEventModel>>();
			Dictionary = dict;

			AddItemCommand = new ActionCommand(
				o => Dictionary.FileEventTree.Main.OpenAddEventDialog(addTarget: Dictionary.Dictionary)
				);
		}

		//インスタンス生成中のイベントの追加
		public void AddEvent(EventModel ev)
		{
			events.Add(ev);
			foreach (var inlineEv in ev.InlineEvents)
				AddInlineEvent(inlineEv);

			INotifyCollectionChanged inlineEvents = ev.InlineEvents;
			inlineEvents.CollectionChanged += InlineEvents_CollectionChanged;

			if (identifier == null)
			{
				identifier = ev.Identifier;
			}
		}

		//イベントを取り除く
		public void RemoveEvent(EventModel ev)
		{
			INotifyCollectionChanged inlineEvents = ev.InlineEvents;
			inlineEvents.CollectionChanged -= InlineEvents_CollectionChanged;

			foreach (var inlineEv in ev.InlineEvents)
				RemoveInlineEvent(inlineEv);
			events.Remove(ev);
		}

		//イベント名の更新
		public void UpdateLabel()
		{
			Label = events.First().Identifier;
		}

		//インラインイベント処理
		private void InlineEvents_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
			{
				foreach (var item in e.OldItems)
					RemoveInlineEvent((InlineEventModel)item);
			}
			if (e.NewItems != null)
			{
				foreach (var item in e.NewItems)
					AddInlineEvent((InlineEventModel)item);
			}
		}

		public void AddInlineEvent(InlineEventModel ev)
		{
			//追加。必要に応じてviewModelも足す
			ObservableCollection<InlineEventModel> inlineEventList;
			if (!inlineEvents.TryGetValue(ev.Identifier, out inlineEventList))
			{
				inlineEventList = new ObservableCollection<InlineEventModel>();
				inlineEvents.Add(ev.Identifier, inlineEventList);
				inlineEventList.Add(ev);
				var newViewModel = new FileEventTreeItemInlineEventViewModel(this, inlineEventList);
				inlineEventViewModels.Add(newViewModel);
			}
			else
			{
				inlineEventList.Add(ev);
			}
		}

		public void RemoveInlineEvent(InlineEventModel ev)
		{
			//Viewmodelを削除
			ObservableCollection<InlineEventModel> inlineEventList = inlineEvents[ev.Identifier];
			inlineEventList.Remove(ev); ;

			//同名のものがなくなったらViewModelを取り除く
			if(inlineEventList.Count == 0)
			{
				inlineEvents.Remove(ev.Identifier);

				//同じイベントをもつものを取り除く
				inlineEventViewModels.Remove(inlineEventViewModels.First(o => o.EventList.Count == 0));
			}
		}

		//最初の１つをイベントエディタで開く。１つしかアイテムがない場合にダブルクリック時エディタを開くためのもの
		public void OpenFirstEventEditor()
		{
			if(Items.Count > 0)
				Dictionary.FileEventTree.Main.OpenEventEditor(Items.First());
		}
	}

	//インラインイベントにあたる
	//末端なので簡易なつくり
	internal class FileEventTreeItemInlineEventViewModel : NotificationObject
	{
		private FileEventTreeItemEventViewModel parentViewModel;
		public string NodeType => "InlineEvent";
		public DictionaryModel Dictionary => parentViewModel.Dictionary.Dictionary;
		public IEnumerable<object> ChildItems => Array.Empty<object>();
		public ReadOnlyObservableCollection<InlineEventModel> EventList { get; }
		public string Label { get; }

		public FileEventTreeItemInlineEventViewModel(FileEventTreeItemEventViewModel parent, ObservableCollection<InlineEventModel> events)
		{
			parentViewModel = parent;
			EventList = new ReadOnlyObservableCollection<InlineEventModel>(events);
			Label = events.First().Identifier;
		}
	}
}