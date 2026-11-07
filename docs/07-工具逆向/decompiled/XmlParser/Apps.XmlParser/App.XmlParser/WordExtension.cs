using System.Collections.Generic;
using System.Linq;

namespace App.XmlParser;

public static class WordExtension
{
	public static Word GetPreWord(this IEnumerable<Word> words, Word current)
	{
		if (words == null)
		{
			return null;
		}
		if (current == null)
		{
			return null;
		}
		return (current.StartIndex == 0) ? null : words.LastOrDefault((Word p) => p.StartIndex < current.StartIndex);
	}

	public static Word GetNextWord(this IEnumerable<Word> words, Word current)
	{
		if (words == null)
		{
			return null;
		}
		return (current == null) ? null : words.FirstOrDefault((Word p) => p.StartIndex > current.StartIndex);
	}

	public static void InsertWord(this List<Word> words, int index, Word word)
	{
		if (index > words.Count - 1)
		{
			word.StartIndex = words.Sum((Word p) => p.Length);
			words.Add(word);
			return;
		}
		if (index < 0)
		{
			index = 0;
		}
		Word word2 = words[index];
		word.StartIndex = word2.StartIndex;
		words.Insert(index, word);
		for (int num = index + 1; num < words.Count; num++)
		{
			words[num].StartIndex += word.Length;
		}
	}
}
