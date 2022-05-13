﻿using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Media;

namespace Satolist2.Utility
{
	enum ScriptSyntax
	{
		Escape,
		ScriptTag,
		ChangeSurface,
		Jump,
		Choice,
		Anchor,
		Word,
		Variable,
		Sentence,
		Getter,
		Random,
		Argument,
		Comment,
		Tab,
		Error,
		Function,
		Saori,
		Replace,
		ReplaceAfter
	}

	class SatoriSyntaxRuleSet : HighlightingRuleSet
	{
		private readonly List<SyntaxDefinition> Definitions = new List<SyntaxDefinition>()
		{
			new SyntaxDefinition("\\\\s\\[.+?\\]", ScriptSyntax.ChangeSurface),
			new SyntaxDefinition("\\\\s[0-9]", ScriptSyntax.ChangeSurface),
			new SyntaxDefinition("（[1234567890１２３４５６７８９０\\-－]+）", ScriptSyntax.ChangeSurface),

			new SyntaxDefinition("φ.", ScriptSyntax.Escape),

			new SyntaxDefinition("\\\\!\\[(raise|embed|get,property|timerraise|notify),.+?\\]", ScriptSyntax.ScriptTag),
			new SyntaxDefinition("\\\\[!pibncjmfx\\&]\\[.+?\\]", ScriptSyntax.ScriptTag),
			new SyntaxDefinition("\\\\_[wblvums]\\[.+?\\]", ScriptSyntax.ScriptTag),
			new SyntaxDefinition("\\\\__w\\[\\d+?\\]", ScriptSyntax.ScriptTag),
			new SyntaxDefinition("\\\\&\\[.+?\\]", ScriptSyntax.ScriptTag),
			new SyntaxDefinition("\\\\[01456huncvCxt\\*e\\-\\+]", ScriptSyntax.ScriptTag),
			new SyntaxDefinition("\\\\_[nqs\\?\\+V]", ScriptSyntax.ScriptTag),
			new SyntaxDefinition("\\\\w[0-9]", ScriptSyntax.ScriptTag),

			new SyntaxDefinition("^[＿].+", ScriptSyntax.Choice),
			new SyntaxDefinition("\\\\q\\[.+?\\]", ScriptSyntax.Choice),
			new SyntaxDefinition("\\\\__q\\[.+?\\].+?\\\\__q", ScriptSyntax.Choice),

			new SyntaxDefinition("^[＞].+", ScriptSyntax.Jump),

			//new SyntaxDefinition("\\\\_a\\[.+?\\].+?\\\\_a", ScriptSyntax.Anchor),	//アンカー分ける意味無いか
			new SyntaxDefinition("\\\\_a\\[.+?\\].+?\\\\_a", ScriptSyntax.Choice),

			new SyntaxDefinition("^＊[^\t]*", ScriptSyntax.Sentence),

			new SyntaxDefinition("^＠[^\t]+", ScriptSyntax.Word),

			new SyntaxDefinition("^＄.+[\t＝\\=]", ScriptSyntax.Variable),

			new SyntaxDefinition("\\b(単語の追加|追加単語の削除|追加単語の全削除|合成単語群|バイト値)\\b", ScriptSyntax.Function),
			new SyntaxDefinition("\\b(call|nop|remember|set|sync|when|whenlist|loop|times|while|for|vncall|equal)\\b", ScriptSyntax.Function),

			new SyntaxDefinition("\\b(Value\\d+|教わること|引数区切り追加|引数区切り削除|文「.+」の重複回避|ウィンドウハンドル\\d+|スクリプトの一番頭|呼び出し回数制限|ジャンプ回数制限|デバッグ|SAORI引数の計算|BalloonOffset\\d+|Log|RequestLog|OperationLog|ResposeLog)\\b", ScriptSyntax.Getter),
			new SyntaxDefinition("\\b(スコープ切り替え時|さくらスクリプトによるスコープ切り替え時|.+タイマ|全タイマ解除|次から[0123456789０１２３４５６７８９]+回目のトーク|次のトーク|トーク予約のキャンセル|デフォルトサーフェス\\d+|会話時サーフェス戻し|サーフェス加算値\\d+|辞書フォルダ|辞書リロード|＄自動セーブ間隔|手動セーブ|セーブデータ暗号化|自動挿入ウェイトタイプ|自動挿入ウェイトの倍率|喋り間隔|喋り間隔誤差|今回は喋らない|見切れてても喋る|自動アンカー|今回は自動アンカー|トーク中のなでられ反応|なでられ持続秒数|なでられ反応回数)\\b", ScriptSyntax.Getter),
			new SyntaxDefinition("（(現在[時分秒]|起動回数|起動[時分秒]|累計[時分秒]|ＯＳ起動[時分秒]|単純累計[時分秒]|単純起動[時分秒]|単純ＯＳ起動[時分秒]|最終トークからの経過秒|ゴースト起動時間累計ミリ秒|サーフェス\\d+?|前回終了時サーフェス\\d+?|文「.+?」の数|文「.+?」の存在|単語群「.+?」の数|単語群「.+?」の存在|変数「.+?」の存在)）", ScriptSyntax.Getter),
			new SyntaxDefinition("（(里々のバージョン|本体の所在|Sender|Status|Charset|[ARSＡＲＳ]の数|隣で起動しているゴースト|起動しているゴースト数|.+?の存在|.+?のサーフェス|予約トーク|次から.+回目の予約トーク|トーク「.+?」の予約有無)）", ScriptSyntax.Getter),

			new SyntaxDefinition("（Reference(\\d+?|（/+?）)）", ScriptSyntax.Argument),

			new SyntaxDefinition("（乱数([－\\-]?[01234567890０１２３４５６７８９０]+?|（.+?）)～([－\\-]?[01234567890０１２３４５６７８９０]+?|（.+?）)）", ScriptSyntax.Random),
			new SyntaxDefinition("（乱数（.+?））", ScriptSyntax.Random),

			new SyntaxDefinition("＃.*", ScriptSyntax.Comment),

			new SyntaxDefinition("\t", ScriptSyntax.Tab)
		};


		public HighlightingRule SearchHilightRule { get; }

		public SatoriSyntaxRuleSet()
		{
			Name = "Satori";
			SearchHilightRule = new HighlightingRule();
			SearchHilightRule.Color = new HighlightingColor();
			SearchHilightRule.Color.Background = new SimpleHighlightingBrush(System.Windows.Media.Color.FromArgb(255,255,255,0));
			SearchHilightRule.Regex = new System.Text.RegularExpressions.Regex(string.Empty);


			foreach (var def in Definitions)
			{
				var rule = new HighlightingRule();
				var col = GetHilightColor(def.syntaxType);
				var brush = new SimpleHighlightingBrush(col);

				rule.Color = new HighlightingColor();
				if (def.syntaxType != ScriptSyntax.Tab)
					rule.Color.Foreground = brush;
				else
					rule.Color.Background = brush;
				rule.Regex = new System.Text.RegularExpressions.Regex(def.pattern);
				Rules.Add(rule);
			}
		}

		public void UpdateSearchHilighter()
		{
			if (!string.IsNullOrEmpty(SearchHilightRule.Regex.ToString()))
			{
				if (!Rules.Contains(SearchHilightRule))
				{
					//上から順に照合する様子なので最優先(と思ったけど実際はそうでもなかった…正規表現の複雑さか何かで見られてそう)
					Rules.Insert(0, SearchHilightRule);
				}
			}
			else
			{
				if (Rules.Contains(SearchHilightRule))
				{
					Rules.Remove(SearchHilightRule);
				}
			}
		}

		private System.Windows.Media.Color GetHilightColor(ScriptSyntax def)
		{
			//設定を問い合わせる
			if(MainViewModel.EditorSettings.GeneralSettings.TextEditorColors.ContainsKey(Enum.GetName(typeof(ScriptSyntax), def)))
			{
				var col = Themes.ApplicationTheme.UintToColorRGB(
					MainViewModel.EditorSettings.GeneralSettings.TextEditorColors[Enum.GetName(typeof(ScriptSyntax), def)]
					);
				return col;
			}
			return Themes.ApplicationTheme.UintToColorRGB(Themes.ApplicationTheme.GetEditorHilight(def));
		}
	}

	class SatoriSyntaxHilighter : IHighlightingDefinition
	{
		public string Name => "Satori";
		private SatoriSyntaxRuleSet mainRuleSet;

		public HighlightingRule SearchHilightRule => mainRuleSet.SearchHilightRule;
		public HighlightingRuleSet MainRuleSet => mainRuleSet;

		public IEnumerable<HighlightingColor> NamedHighlightingColors => throw new NotImplementedException();

		public IDictionary<string, string> Properties => throw new NotImplementedException();

		public HighlightingColor GetNamedColor(string name)
		{
			throw new NotImplementedException();
		}

		public HighlightingRuleSet GetNamedRuleSet(string name)
		{
			return MainRuleSet;
		}

		public SatoriSyntaxHilighter()
		{
			mainRuleSet = new SatoriSyntaxRuleSet();
		}

		public void UpdateSearchHilighter()
		{
			mainRuleSet.UpdateSearchHilighter();
		}
	}

	struct SyntaxDefinition
	{
		public string pattern;
		public ScriptSyntax syntaxType;

		public SyntaxDefinition(string pattern, ScriptSyntax type)
		{
			this.pattern = pattern;
			this.syntaxType = type;
		}
	}

}
