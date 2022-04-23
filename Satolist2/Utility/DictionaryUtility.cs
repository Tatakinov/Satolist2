﻿using Satolist2.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Satolist2.Utility
{
	//里々辞書ユーティリティ
	internal static class DictionaryUtility
	{
		public static bool SplitEventHeader(string line, out EventType eventType, out string eventName, out string eventCondition)
		{
			if (line.IndexOf(Constants.SentenceHead) == 0)
			{
				eventType = EventType.Sentence;
				var sp = line.Substring(1).Split(Constants.TabSeparator, 2, StringSplitOptions.RemoveEmptyEntries);

				if (sp.Length == 0)
				{
					eventName = string.Empty;
					eventCondition = null;
				}
				else if (sp.Length == 1)
				{
					eventName = sp[0];
					eventCondition = null;
				}
				else
				{
					eventName = sp[0];
					eventCondition = sp[1];
				}
				return true;
			}
			else if (line.IndexOf(Constants.WordHead) == 0)
			{
				eventType = EventType.Word;
				var sp = line.Substring(1).Split(Constants.TabSeparator, 2, StringSplitOptions.RemoveEmptyEntries);

				if (sp.Length == 0)
				{
					eventName = string.Empty;
					eventCondition = null;
				}
				else if (sp.Length == 1)
				{
					eventName = sp[0];
					eventCondition = null;
				}
				else
				{
					eventName = sp[0];
					eventCondition = sp[1];
				}
				return true;
			}

			//文または単語群ヘッダではない
			eventType = EventType.Sentence;
			eventName = null;
			eventCondition = null;
			return false;
		}

		public static string SerializeEventHeader(EventType eventType, string eventName, string eventCondition)
		{
			string nameAndCondition = eventName;
			if (!string.IsNullOrEmpty(eventCondition))
				nameAndCondition += Constants.Tab + eventCondition;

			if (eventType == EventType.Sentence)
				return Constants.SentenceHead + nameAndCondition;
			else if (eventType == EventType.Word)
				return Constants.WordHead + nameAndCondition;
			else
				return null;
		}

		public static string[] SplitLines(string text)
		{
			//TODO: CRLFじゃなかったときも考える？　ここではとりあえず処理を統一化するためにsplitをラップしてみる
			return text.Split(Constants.NewLineSeparator, StringSplitOptions.None);
		}

		public static string[] SplitCSVLine(string line)
		{
			return line.Split(new char[] { ',' }, 2, StringSplitOptions.None);
		}

		public static string JoinLines(IEnumerable<string> lines)
		{
			return string.Join(Constants.NewLine, lines);
		}

		//スラッシュ区切りのconbine
		public static string ConbinePath(params string[] path)
		{
			return Path.Combine(path).Replace("\\", "/");
		}

		//絶対パスに整形し、区切り文字を '/' に統一する
		public static string NormalizeFullPath(string path)
		{
			return Path.GetFullPath(path).Replace('\\', '/');
		}

		//パス区切りを統一
		public static string NormalizePath(string path)
		{
			return path.Replace('\\', '/');
		}

		//パス栗切りを\\に変更
		public static string NormalizeWindowsPath(string path)
		{
			return path.Replace('/', '\\');
		}

		//パス区切り文字を delete.exe 準拠の \ に統一
		public static string NormalizeDeletePath(string path)
		{
			return path.Replace('/', '\\');
		}

		//フルパス同士から相対パスの作成
		public static string MakeRelativePath(string baseFullPath, string targetFullPath)
		{
			if (baseFullPath.Last() != '/')
				baseFullPath = baseFullPath + "/";

			var baseUri = new Uri(baseFullPath);
			var targetUri = new Uri(targetFullPath);
			return baseUri.MakeRelativeUri(targetUri).ToString();
		}

		//baseFullpathの配下にあるかどうかチェックする
		public static bool IsChildPath(string baseFullPath, string targetFullPath)
		{
			//もう少しちゃんとした検出方法があるかも？
			//ドライブレターが異なる場合は間違ってる
			if (baseFullPath[0] != targetFullPath[0])
				return false;

			var relativePath = MakeRelativePath(baseFullPath, targetFullPath);

			//遡ろうとしてたら違う
			if(relativePath.IndexOf("..") == 0)
			{
				return false;
			}
			return true;
		}
	}

	internal static class Constants
	{
		//文の先頭
		public const char SentenceHead = '＊';
		//単語群の先頭
		public const char WordHead = '＠';
		//変数
		public const char VariableHead = '＄';
		//エスケープ
		public const char Escape = 'φ';
		//タブ
		public const char Tab = '\t';
		public static readonly char[] TabSeparator = { Tab };
		//改行
		public const string NewLine = "\r\n";
		public static readonly string[] NewLineSeparator = { NewLine };

		//カンマ
		public static readonly string[] CommaSeparator = { "," };

		public static readonly Encoding EncodingShiftJis = Encoding.GetEncoding("Shift_JIS");

		public const string SaveDataFileName = "satori_savedata.txt";
		public const string SaveBackupFileName = "satori_savebackup.txt";

		//変数
		public const string VariableDictionaryDirectory = "辞書フォルダ";

		//変数初期化
		public const string VariableInitializeEvent = "＊初期化";
		public const string SaoriListEvent = "＠SAORI";

		//インラインイベント
		public const string InlineEventSeparator = "＃＃＃インラインイベント";

		//EventTypeに対応する文字を取得
		public static string GetEventHead(EventType type)
		{
			switch(type)
			{
				case EventType.Sentence:
					return SentenceHead.ToString();
				case EventType.Word:
					return WordHead.ToString();
			}
			return string.Empty;
		}
	}

	public enum EventType
	{
		Sentence,       //文
		Word,           //単語群
		Header          //辞書ヘッダ
	}

	//セーブデータ解析
	public class SaveDataBuilder
	{
		public bool Loaded { get; private set; }

		public Dictionary<string, string> SaveData { get; }

		//Ghostのセーブデータをロード
		public SaveDataBuilder(GhostModel ghost)
		{
			SaveData = new Dictionary<string, string>();

			//規定のファイルからロードを試みる
			if(!Deserialize(ghost.FullDictionaryPath + "/" + Constants.SaveDataFileName))
			{
				Deserialize(ghost.FullDictionaryPath + "/" + Constants.SaveBackupFileName);
			}
		}

		public SaveDataBuilder()
		{
			SaveData = new Dictionary<string, string>();
		}

		public bool Deserialize(string path)
		{
			try
			{
				string[] lines = File.ReadAllLines(path);
				foreach (var line in lines)
				{
					if (line.IndexOf(Constants.VariableHead) == 0)
					{
						var sp = line.Substring(1).Split(Constants.TabSeparator, 2, StringSplitOptions.RemoveEmptyEntries);
						if (sp.Length == 2)
						{
							SaveData[sp[0]] = sp[1];
						}
					}
				}
				Loaded = true;
			}
			catch
			{
				Loaded = false;
				SaveData.Clear();
			}
			return Loaded;
		}
	}

	//SHIORI等のプロトコルビルダ
	public class ProtocolBuilder
	{
		public const string CommandGetShiori = "GET SHIORI/3.0";

		public string Command { get; set; }
		public Dictionary<string, string> Parameters{get;}

		public ProtocolBuilder()
		{
			Parameters = new Dictionary<string, string>();
			Command = string.Empty;
		}

		public void Deserialize(string data)
		{
			Command = string.Empty;
			Parameters.Clear();

			var lines = data.Split(Constants.NewLineSeparator, StringSplitOptions.RemoveEmptyEntries);
			Command = lines[0];

			foreach(var line in lines.Skip(1))
			{
				var sp = line.Split(new string[] { ": " }, 2, StringSplitOptions.None);
				if(sp.Length == 2)
					Parameters[sp[0]] = sp[1];
			}
		}

		public string Serialize()
		{
			var param = string.Join(Constants.NewLine, Parameters.Select(o => string.Format("{0}: {1}", o.Key, o.Value)));
			return string.Format("{1}{0}{2}{0}{0}", Constants.NewLine, Command, param);
		}
	}

	//descript.txt 等CSV解析
	public class CsvBuilder
	{
		//中身のデータ
		public List<KeyValuePair<string, string>> Records { get; }
		public bool Deserialized { get; private set; }
		
		public CsvBuilder()
		{
			Records = new List<KeyValuePair<string, string>>();
		}

		public void Deserialize(string data)
		{
			var lines = DictionaryUtility.SplitLines(data);
			foreach(var line in lines)
			{
				var sp = line.Split(new char[] { ',' }, 2, StringSplitOptions.None);
				if(sp.Length == 1)
				{
					Records.Add(new KeyValuePair<string, string>(null, sp[0]));	//カンマ区切りされてにない行を示す
				}
				else
				{
					Records.Add(new KeyValuePair<string, string>(sp[0], sp[1]));
				}
			}
			Deserialized = true;
		}

		public void DeserializeFromFile(string path)
		{
			Deserialized = false;
			try
			{
				var text = File.ReadAllText(path, Constants.EncodingShiftJis);
				Deserialize(text);
			}
			catch { }	//Deserialized=falseなら失敗
		}

		public string Serialize()
		{
			var result = new List<string>();
			foreach(var record in Records)
			{
				if(record.Key == null)
				{
					result.Add(record.Value);
				}
				else
				{
					result.Add(string.Format("{0},{1}", record.Key, record.Value));
				}
			}
			return string.Join(Constants.NewLine, result);
		}

		//キーが存在するか
		public bool ContaintsKey(string key)
		{
			return Records.FirstOrDefault(o => o.Key == key).Value != null;
		}

		public string GetValue(string key)
		{
			return Records.FirstOrDefault(o => o.Key == key).Value;
		}
	}
	

}
