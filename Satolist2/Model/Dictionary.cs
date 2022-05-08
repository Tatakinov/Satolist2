﻿using Satolist2.Dialog;
using Satolist2.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace Satolist2.Model
{
	//ゴーストデータのモデル
	//編集データのルートとなる
	public class GhostModel
	{
		private string fullPath;
		private ObservableCollection<DictionaryModel> dictionaries;

		//ゴーストのフルパス（readme.txt 等のフル階層）
		public string FullPath
		{
			get => fullPath;
		}

		public string FullDictionaryPath
		{
			get => DictionaryUtility.ConbinePath(FullPath, "ghost/master");
		}

		public string FullGhostDescriptPath
		{
			get => DictionaryUtility.ConbinePath(FullDictionaryPath, "descript.txt");
		}

		public string GhostDescriptSakuraName
		{
			get; private set;
		}

		public string GhostDescriptName
		{
			get; private set;
		}

		//ゴーストが持っている辞書データ
		public ReadOnlyObservableCollection<DictionaryModel> Dictionaries
		{
			get => new ReadOnlyObservableCollection<DictionaryModel>(dictionaries);
		}

		public GhostModel(string path)
		{
			fullPath = DictionaryUtility.NormalizeFullPath(Path.GetFullPath(path));
			dictionaries = new ObservableCollection<DictionaryModel>();

			//ファイル巡回
			var files = Directory.GetFiles(FullDictionaryPath, "*.*", SearchOption.AllDirectories);
			foreach(var f in files)
			{
				if(Regex.IsMatch(Path.GetFileName(f), "^dic.*\\.txt$"))
				{
					var dict = new DictionaryModel(this, Path.GetFullPath(f));
					dict.LoadDictionary();
					dictionaries.Add(dict);
				}
			}

			//descriptを探す
			//TODO: descript editor ができたら一時的でなくてもよさそう
			var tempDescript = new CsvBuilder();
			tempDescript.DeserializeFromFile(FullGhostDescriptPath);
			GhostDescriptSakuraName = tempDescript.GetValue("sakura.name");
			GhostDescriptName = tempDescript.GetValue("name");
		}

		public DictionaryModel AddNewDictionary(string filename)
		{
			var newDictionary = new Model.DictionaryModel(this, DictionaryUtility.NormalizePath(filename));
			newDictionary.IsSerialized = MainViewModel.EditorSettings.GeneralSettings.IsTextModeDefault;    //デフォルト設定のモードにする
			newDictionary.Changed();
			dictionaries.Add(newDictionary);
			return newDictionary;
		}
	}

	//テキストファイルデータ
	public class TextFileModel : NotificationObject, ISaveFileObject
	{
		private string body;
		private bool bodyAvailable; //false ならbodyは無効。里々辞書形式でデシリアライズされているなど直接テキストファイルとして編集できない
		private bool isChanged;     //変更検出
		private bool isDeleted;		//削除扱いにされているか(保存操作時に消す予定か）


		public GhostModel Ghost { get; }

		//削除通知
		public event Action<TextFileModel> OnDelete;

		//フルパス
		public string FullPath
		{
			get; set;
		}

		//純粋なファイル名
		public string Name
		{
			get => System.IO.Path.GetFileName(FullPath);
		}

		//辞書の相対パス
		public string RelativeName
		{
			get => DictionaryUtility.MakeRelativePath(Ghost.FullDictionaryPath, FullPath);
		}

		//ファイルの中身
		public string Body
		{
			get => bodyAvailable ? body : null;
			set
			{
				bodyAvailable = true;
				body = value;
				Changed();
				NotifyChanged();
				NotifyChanged(nameof(BodyAvailable));
			}
		}

		//ファイルの中身が有効か
		public bool BodyAvailable
		{
			get => bodyAvailable;
			set
			{
				bodyAvailable = value;
				NotifyChanged();
				NotifyChanged(nameof(Body));
			}
		}

		public bool IsChanged
		{
			get => isChanged;
			protected set
			{
				isChanged = value;
				NotifyChanged();
			}
		}

		public bool IsDeleted
		{
			get => isDeleted;
			set
			{
				if (isDeleted != value)
				{
					if (!value)
						throw new InvalidOperationException();	//復活は対応してない
					isDeleted = value;
					Changed();
					NotifyChanged();

					if(isDeleted)
					{
						//削除になった場合削除イベントを発生する
						OnDelete?.Invoke(this);
					}
				}
			}
		}

		public string SaveFilePath => RelativeName;

		public EditorLoadState LoadState { get; set; }

		public TextFileModel(GhostModel ghost, string fullPath)
		{
			LoadState = EditorLoadState.Initialized;
			Ghost = ghost;
			FullPath = fullPath;
			bodyAvailable = true;
			body = string.Empty;
			if(!string.IsNullOrEmpty(fullPath))
				LoadFile();
		}

		protected virtual void LoadFile()
		{
			try
			{
				body = File.ReadAllText(FullPath, Constants.EncodingShiftJis);
				bodyAvailable = true;
				LoadState = EditorLoadState.Loaded;
			}
			catch
			{
				LoadState = EditorLoadState.LoadFailed;
			}
		}

		public virtual bool Save()
		{
			throw new NotImplementedException();
		}

		public void Changed()
		{
			IsChanged = true;
		}
	}

	//里々の辞書ファイル
	public class DictionaryModel : TextFileModel
	{
		private ObservableCollection<EventModel> events;
		private bool isSerialized;                          //リスト化解除されたか

		public bool IsInlineEventAnalyze { get; set; }

		public ReadOnlyObservableCollection<EventModel> Events
		{
			get => new ReadOnlyObservableCollection<EventModel>(events);
		}

		//リスト化解除されているか
		public bool IsSerialized
		{
			get => isSerialized;
			set
			{
				if(isSerialized != value)
				{
					bool changed = IsChanged;
					isSerialized = value;
					if(value)
					{
						Body = Serialize();
						BodyAvailable = true;

						//イベントを消す
						ClearEvent();
					}
					else
					{
						Deserialize(Body);
						BodyAvailable = false;
					}
					IsChanged = changed;    //この操作だけでは編集ステータスは変わらない
				}
				NotifyChanged();
			}
		}

		//ゴーストの辞書をロードする用
		public DictionaryModel(GhostModel ghost, string fullPath): base(ghost, fullPath)
		{
			events = new ObservableCollection<EventModel>();
			OnDelete += DictionaryModel_OnDelete;
		}

		public DictionaryModel() : base(null, null)
		{
			events = new ObservableCollection<EventModel>();
			OnDelete += DictionaryModel_OnDelete;
		}

		private void DictionaryModel_OnDelete(TextFileModel obj)
		{
			//ファイルが削除扱いにされた。
			//リストモードであればイベントの除去をまとめて行い、リスト系から表示を消す。
			if(!IsSerialized)
				ClearEvent();
		}

		protected override void LoadFile()
		{
			//nop
		}
		public void LoadDictionary()
		{
			//TODO: 辞書単体の設定を考慮する必要
			var text = File.ReadAllText(FullPath, Constants.EncodingShiftJis);
			if (!MainViewModel.EditorSettings.GeneralSettings.IsTextModeDefault)
			{
				//リストモード
				Deserialize(text);
				isSerialized = false;
				BodyAvailable = false;
			}
			else
			{
				//テキストモード
				Body = text;
				IsChanged = false;
				BodyAvailable = true;
				isSerialized = true;
			}
		}

		//イベントを追加
		//すでにどこかに所属している場合は所属が外れる
		public void AddEvent(EventModel ev)
		{
			if(ev.Dictionary != null)
			{
				//削除イベントは発生しない
				//ObservableCollectionからは外れる
				DetachEvent(ev);
			}
			events.Add(ev);
			ev.Dictionary = this;
			Changed();
		}

		//イベントの削除
		public void RemoveEvent(EventModel ev)
		{
			Debug.Assert(events.Contains(ev));
			if (events.Contains(ev))
			{
				ev.RaiseRemoveEvent();
				ev.Dictionary = null;
				RemoveEventInternal(ev);
			}
		}

		//イベントのデタッチ(削除イベントを発しないが、dictionaryからイベントを切り離す)
		public void DetachEvent(EventModel ev)
		{
			Debug.Assert(events.Contains(ev));
			if(events.Contains(ev))
			{
				ev.Dictionary = null;
				RemoveEventInternal(ev);
			}
		}

		//イベントの全削除。削除イベントを使うのでevent.Clear直接触らない
		public void ClearEvent()
		{
			foreach(var ev in events)
			{
				ev.RaiseRemoveEvent();
				ev.Dictionary = null;
			}
			events.Clear();
		}

		//内部的なイベントの削除
		private void RemoveEventInternal(EventModel ev)
		{
			events.Remove(ev);
			Changed();
		}


		//辞書のシリアライズ
		public string Serialize()
		{
			bool isChanged = IsChanged;
			var serializedEvents = new List<string>();
			
			foreach(var ev in events)
			{
				//辞書ヘッダがカラの場合は無視
				if (ev.Type == EventType.Header && string.IsNullOrEmpty(ev.Body))
					continue;

				//イベント本体
				serializedEvents.Add(ev.Serialize());

				//区切りの空行追加
				for (int i = 0; i < MainViewModel.EditorSettings.GeneralSettings.ListedDictionaryInsertEmptyLineCount; i++)
					serializedEvents.Add(string.Empty);
			}

			//isChangedの状態を復元
			IsChanged = isChanged;

			//TODO: 1行あけたい（古いさとりすとのほうはデフォルト０で指定可能だった）
			return string.Join(Constants.NewLine, serializedEvents);
		}

		private string TruncateDisableMark(string name, out bool isDisabled)
		{
			if (string.IsNullOrEmpty(name))
			{
				//名前がない。多分辞書ヘッダ
				isDisabled = false;
				return name;
			}

			var effectiveName = name;
			if (effectiveName.IndexOf(Constants.DisabledEventMark) == 0)
			{
				effectiveName = effectiveName.Substring(Constants.DisabledEventMark.Length);
				isDisabled = true;
				return effectiveName;
			}
			isDisabled = false;
			return effectiveName;
		}

		public void Deserialize(string text)
		{
			bool isChanged = IsChanged;
			var lines = text.Split(Constants.NewLineSeparator, StringSplitOptions.None);
			var eventLines = new List<string>();
			string eventName = null;
			string eventCondition = null;
			int eventLineIndex = 0;
			int eventLastEmptyLineCount = 0;			//末尾の空白行。保存設定で自動挿入する文、読み込み時は捨てるためのもの
			EventType eventType = EventType.Header;
			bool eventLineEscape = false;

			bool inlineEvent = false;
			for(var index = 0; index < lines.Length; index++)
			{
				var line = lines[index];

				//NOTE: エスケープ文字を解釈するかどうか悩ましいところ。１行に解釈まとめられないのでそのまま読んでしまう。
				EventType nextType;
				string nextName, nextCondition;
				if (DictionaryUtility.SplitEventHeader(line, out nextType, out nextName, out nextCondition) && !inlineEvent && !eventLineEscape)
				{
					//設定値だけ末尾の空白行をとりのぞく
					{
						int removeCount = Math.Min(eventLastEmptyLineCount, MainViewModel.EditorSettings.GeneralSettings.ListedDictionaryInsertEmptyLineCount);
						if (removeCount > 0)
							eventLines.RemoveRange(eventLines.Count - removeCount, removeCount);
					}

					//フラッシュ
					bool isDisabled;
					eventName = TruncateDisableMark(eventName, out isDisabled);
					var ev = new EventModel(eventType, eventName, eventCondition, string.Join(Constants.NewLine, eventLines), IsInlineEventAnalyze, eventLineIndex);
					ev.Disabled = isDisabled;
					AddEvent(ev);

					eventLastEmptyLineCount = 0;
					eventLineIndex = index + 1;
					eventType = nextType;
					eventName = nextName;
					eventCondition = nextCondition;
					eventLines.Clear();
				}
				else
				{
					eventLineEscape = false;
					inlineEvent = false;
					if (line == Constants.InlineEventSeparator) //インラインイベントを検出したら次の行はフラッシュせず同一の中に続ける
						inlineEvent = true;
					else
					{
						if (string.IsNullOrEmpty(line))
						{
							//末尾の空白行画の数を数える
							eventLastEmptyLineCount++;
						}
						else
						{
							//末尾ではなかったのでカウンタをリセット
							eventLastEmptyLineCount = 0;

							//行末エスケープか確認
							var match = Constants.LineEndEscapePattern.Match(line);
							if(match.Success)
							{
								//エスケープ文字が奇数個ならば行末エスケープされている
								if (match.Groups[1].Length % 2 == 1)
									eventLineEscape = true;
							}
						}

						eventLines.Add(line);
					}
				}
			}

			//設定値だけ末尾の空白行をとりのぞく
			{
				int removeCount = Math.Min(eventLastEmptyLineCount, MainViewModel.EditorSettings.GeneralSettings.ListedDictionaryInsertEmptyLineCount);
				if (removeCount > 0)
					eventLines.RemoveRange(eventLines.Count - removeCount, removeCount);
			}

			//最後のイベントをフラッシュ
			{
				bool isDisabled;
				eventName = TruncateDisableMark(eventName, out isDisabled);
				var lastEvent = new EventModel(eventType, eventName, eventCondition, string.Join(Constants.NewLine, eventLines), IsInlineEventAnalyze, eventLineIndex);
				lastEvent.Disabled = isDisabled;
				AddEvent(lastEvent);
			}

			//isChangedの状態を復元
			IsChanged = isChanged;
		}

		//変更済みとしてマーク
		public void MarkChanged()
		{
			IsChanged = true;
		}

		public override bool Save()
		{
			try
			{
				if (!IsDeleted)
				{
					string saveText;
					if (IsSerialized)
					{
						//保存
						saveText = Body;
					}
					else
					{
						//シリアライズして保存
						saveText = Serialize();
					}
					File.WriteAllText(FullPath, saveText, Constants.EncodingShiftJis);
				}
				else
				{
					//削除済みなので、ゴミ箱いき
					DictionaryUtility.RecycleFile(FullPath);
				}
				IsChanged = false;
				return true;
			}
			catch
			{
				return false;
			}
		}

	}

	//里々の項目１つ分
	public class EventModel : NotificationObject
	{
		private bool initialized;
		private DictionaryModel dictionary;
		private string name;
		private string condition;
		private string body;
		private EventType type;
		private bool disabled;
		private bool isInlineEvent;
		private int analyzeLineIndex;

		private ObservableCollection<InlineEventModel> inlineEvents = new ObservableCollection<InlineEventModel>();

		//項目名
		public string Name
		{
			get => name ?? string.Empty;
			set
			{
				if (name != value)
					MarkChanged();
				name = value;
				NotifyChanged();
				NotifyChanged(nameof(Identifier));
			}
		}

		//実行条件
		public string Condition
		{
			get => condition ?? string.Empty;
			set
			{
				if (condition != value)
					MarkChanged();
				condition = value;
				NotifyChanged();
			}
		}

		//インラインかどうか
		public bool IsInlineEvent
		{
			get => isInlineEvent;
		}

		//解析時の行番号（編集中にずれるので、あくまでも解析時の位置）
		public int AnalyzeLineIndex
		{
			get => analyzeLineIndex;
		}
		//本文
		public string Body
		{
			get => body ?? string.Empty;
			set
			{
				if (body != value)
					MarkChanged();
				body = value;

				//インラインイベントはインラインイベントをネストしない
				if (!isInlineEvent)
				{
					//Bodyが変わったタイミングでインラインイベントの解析を走らせる
					DictionaryModel tempDict = new DictionaryModel();
					tempDict.IsInlineEventAnalyze = true;
					tempDict.Deserialize(Body);

					List<InlineEventModel> newInlineEvents = new List<InlineEventModel>();
					foreach (var ev in tempDict.Events)
					{
						if(ev.type != EventType.Header)
							newInlineEvents.Add(new InlineEventModel(this, ev));
					}

					//差分を作る
					List<InlineEventModel> removedEvents = new List<InlineEventModel>();

					//一致するものを除去して余ったものが差分
					foreach (var item in inlineEvents)
					{
						var same = newInlineEvents.FirstOrDefault(o => o.Equals(item));
						if (same != null)
						{
							//内容を更新
							item.InlineEvent.Body = same.InlineEvent.Body;
							item.InlineEvent.Condition = same.InlineEvent.Condition;

							//重複を取り除く
							newInlineEvents.Remove(same);
						}
						else
						{
							removedEvents.Add(item);
						}
					}

					//removedを取り除く
					foreach (var r in removedEvents)
						inlineEvents.Remove(r);

					//新規を追加
					foreach (var r in newInlineEvents)
						inlineEvents.Add(r);
				}

				NotifyChanged();
				NotifyChanged(nameof(BodyPreview));
			}
		}

		//さとりすとによって無効化されているか
		public bool Disabled
		{
			get => disabled;
			set
			{
				if (disabled != value)
					MarkChanged();
				disabled = value;
				NotifyChanged();
			}
		}

		public DictionaryModel Dictionary
		{
			get => dictionary;
			internal set
			{
				dictionary = value;
				NotifyChanged();
			}
		}

		//プレビュー表示用の改行をとりのぞいた本文
		public string BodyPreview
		{
			get => Body.Replace(Constants.NewLine, " ");
		}

		public EventType Type
		{
			get => type;
			set
			{
				if (type != value)
					MarkChanged();
				type = value;
				NotifyChanged();
				NotifyChanged(nameof(Identifier));
			}
		}

		//識別子を取得、表示名とも兼ねる？
		public string Identifier
		{
			get
			{
				switch (Type)
				{
					case EventType.Sentence:
						return Constants.SentenceHead + Name;
					case EventType.Word:
						return Constants.WordHead + Name;
					case EventType.Header:
						return "<辞書ヘッダ>";
				}
				throw new NotImplementedException();
			}
		}

		public ReadOnlyObservableCollection<InlineEventModel> InlineEvents
		{
			get => new ReadOnlyObservableCollection<InlineEventModel>(inlineEvents);
		}

		public event Action<EventModel> OnRemove;

		public EventModel(EventType type, string name, string condition, string body, bool isInline = false, int analyzeLineIndex = 0)
		{
			this.analyzeLineIndex = analyzeLineIndex;
			isInlineEvent = isInline;
			Type = type;
			Name = name;
			Condition = condition;
			Body = body;
			initialized = true;
		}

		//イベントのシリアライズ
		public string Serialize()
		{
			var header = DictionaryUtility.SerializeEventHeader(type, name, condition, disabled);

			//インラインイベントを検出する
			var lines = DictionaryUtility.SplitLines(body);
			var serializeLines = new List<string>();

			foreach(var line in lines)
			{
				if (line.IndexOf(Constants.SentenceHead) == 0 || line.IndexOf(Constants.WordHead) == 0)
					serializeLines.Add(Constants.InlineEventSeparator);
				serializeLines.Add(line);
			}

			var serializedBody = DictionaryUtility.JoinLines(serializeLines);
			if (header == null)
				return serializedBody;
			else
				return header + Constants.NewLine + serializedBody;

		}

		//イベントを削除
		public void RaiseRemoveEvent()
		{
			OnRemove?.Invoke(this);
		}

		//変更済みとしてマーク
		private void MarkChanged()
		{
			if(initialized)	//初期化ステップでは無視
			{
				Dictionary?.MarkChanged();
			}
		}

		//イベントを削除
		public void Remove()
		{
			dictionary.RemoveEvent(this);
		}

		//イベントを別の辞書に移動
		public void MoveTo(DictionaryModel target)
		{
			dictionary.DetachEvent(this);
			target.AddEvent(this);
		}
	}

	//インラインイベント(イベントエディタ内に記述するイベント)のモデル
	public class InlineEventModel
	{
		public EventModel ParentEvent { get; }
		public EventModel InlineEvent { get; }

		public string Identifier
		{
			get => InlineEvent.Identifier;
		}
		
		public InlineEventModel(EventModel parentEvent, EventModel inlineEvent)
		{
			ParentEvent = parentEvent;
			InlineEvent = inlineEvent;
		}

		public bool Equals(InlineEventModel ev)
		{
			return ReferenceEquals(ParentEvent, ev.ParentEvent) && ev.Identifier == Identifier;
		}
	}

	

	
}
