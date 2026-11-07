using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace App.XmlParser;

public class Word
{
	private const string NumberPattern = "\\d+(\\.\\d+)?";

	private const string SCPattern = "\\d+(\\.\\d+)?(E|e)(\\+|-)\\d+";

	protected string _block;

	private static readonly string[] RegexEscapecharacters = new string[13]
	{
		"^", "$", ".", "(", ")", "*", "?", "+", "[", "]",
		"{", "}", "|"
	};

	public string Type { get; internal set; }

	public int StartIndex { get; internal set; }

	public int Length => (!string.IsNullOrEmpty(_block)) ? _block.Length : 0;

	public bool IsFunGroup { get; set; }

	internal Word(string block)
	{
		_block = block;
	}

	public override string ToString()
	{
		return _block;
	}

	private static void GetWord(string exp, string pattern, List<Word> blocks, string type)
	{
		MatchCollection matchCollection = Regex.Matches(exp, pattern);
		foreach (object item in matchCollection)
		{
			Match match = item as Match;
			if (match != null && !IsContainedWord(match, blocks))
			{
				Word word = blocks.SingleOrDefault((Word p) => p.StartIndex == match.Index);
				if (word == null)
				{
					blocks.Add(new Word(match.Value)
					{
						StartIndex = match.Index,
						Type = type
					});
				}
				else if (word.Length < match.Length)
				{
					word._block = match.Value;
					word.Type = type;
				}
			}
		}
	}

	private static bool IsContainedWord(Match match, List<Word> blocks)
	{
		Word word = blocks.FirstOrDefault((Word p) => p.StartIndex < match.Index && match.Index < p.StartIndex + p.Length);
		return word != null;
	}

	public static List<Word> ExpressionToWords(string exp)
	{
		List<Word> list = new List<Word>();
		GetWord(exp, "\\d+(\\.\\d+)?(E|e)(\\+|-)\\d+", list, "operand");
		IOrderedEnumerable<KeyValuePair<string, IOperator>> orderedEnumerable = DefaultOperators.Operators.OrderByDescending((KeyValuePair<string, IOperator> p) => p.Key.Length);
		foreach (KeyValuePair<string, IOperator> item in orderedEnumerable)
		{
			string type = (item.Value.IsFun ? "fun" : "operator");
			GetWord(exp, GetEscapeString(item.Key), list, type);
		}
		string[] array = new string[3] { "(", ")", "," };
		string[] array2 = array;
		foreach (string input in array2)
		{
			GetWord(exp, GetEscapeString(input), list, "groupoperator");
		}
		GetWord(exp, "\\d+(\\.\\d+)?", list, "operand");
		return list.OrderBy((Word p) => p.StartIndex).ToList();
	}

	public static string GetEscapeString(string input)
	{
		string[] regexEscapecharacters = RegexEscapecharacters;
		foreach (string text in regexEscapecharacters)
		{
			if (input.Contains(text))
			{
				input = Regex.Replace(input, "\\" + text, (Match m) => m.Value.Replace(m.Value, $"\\{m.Value}"));
			}
		}
		return input;
	}
}
