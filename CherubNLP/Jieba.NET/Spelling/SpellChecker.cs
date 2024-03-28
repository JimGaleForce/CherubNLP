using System.Collections.Generic;
using System.Linq;
using JiebaNet.Segmenter.Common;

namespace JiebaNet.Segmenter.Spelling
{
    public interface ISpellChecker
    {
        IEnumerable<string> Suggests(string word);
    }

    public class SpellChecker : ISpellChecker
    {
        internal static readonly WordDictionary WordDict = WordDictionary.Instance;

        internal readonly Trie WordTrie;
        internal readonly Dictionary<char, HashSet<char>> FirstChars;

        public SpellChecker()
        {
            var wordDict = WordDictionary.Instance;
            this.WordTrie = new Trie();
            this.FirstChars = new Dictionary<char, HashSet<char>>();

            foreach (var wd in wordDict.Trie)
            {
                if (wd.Value > 0)
                {
                    this.WordTrie.Insert(wd.Key, wd.Value);

                    if (wd.Key.Length >= 2)
                    {
                        var second = wd.Key[1];
                        var first = wd.Key[0];
                        if (!this.FirstChars.ContainsKey(second))
                        {
                            this.FirstChars[second] = new HashSet<char>();
                        }
                        this.FirstChars[second].Add(first);
                    }
                }
            }
        }

        internal ISet<string> GetEdits1(string word)
        {
            var splits = new List<WordSplit>();
            for (var i = 0; i <= word.Length; i++)
            {
                splits.Add(new WordSplit() { Left = word.Substring(0, i), Right = word.Substring(i) });
            }

            var deletes = splits
                .Where(s => !string.IsNullOrEmpty(s.Right))
                .Select(s => s.Left + s.Right.Substring(1));

            var transposes = splits
                .Where(s => s.Right.Length > 1)
                .Select(s => s.Left + s.Right[1] + s.Right[0] + s.Right.Substring(2));

            var replaces = new HashSet<string>();
            if (word.Length > 1)
            {
                var firsts = this.FirstChars[word[1]];
                foreach (var first in firsts)
                {
                    if (first != word[0])
                    {
                        replaces.Add(first + word.Substring(1));
                    }
                }

                var node = this.WordTrie.Root.Children[word[0]];
                for (int i = 1; node.IsNotNull() && node.Children.IsNotEmpty() && i < word.Length; i++)
                {
                    foreach (var c in node.Children.Keys)
                    {
                        replaces.Add(word.Substring(0, i) + c + word.Substring(i + 1));
                    }
                    node = node.Children.GetValueOrDefault(word[i], null);
                }
            }

            var inserts = new HashSet<string>();
            if (word.Length > 1)
            {
                if (this.FirstChars.ContainsKey(word[0]))
                {
                    var firsts = this.FirstChars[word[0]];
                    foreach (var first in firsts)
                    {
                        inserts.Add(first + word);
                    }
                }

                var node = this.WordTrie.Root.Children.GetValueOrDefault(word[0], null);
                for (int i = 0; node.IsNotNull() && node.Children.IsNotEmpty() && i < word.Length; i++)
                {
                    foreach (var c in node.Children.Keys)
                    {
                        inserts.Add(word.Substring(0, i + 1) + c + word.Substring(i + 1));
                    }

                    if (i < word.Length - 1)
                    {
                        node = node.Children.GetValueOrDefault(word[i + 1], null);
                    }
                }
            }

            var result = new HashSet<string>();
            result.UnionWith(deletes);
            result.UnionWith(transposes);
            result.UnionWith(replaces);
            result.UnionWith(inserts);

            return result;
        }

        internal ISet<string> GetKnownEdits2(string word)
        {
            var result = new HashSet<string>();
            foreach (var e1 in this.GetEdits1(word))
            {
                result.UnionWith(this.GetEdits1(e1).Where(e => WordDictionary.Instance.ContainsWord(e)));
            }
            return result;
        }

        internal ISet<string> GetKnownWords(IEnumerable<string> words)
        {
            return new HashSet<string>(words.Where(w => WordDictionary.Instance.ContainsWord(w)));
        }

        public IEnumerable<string> Suggests(string word)
        {
            if (WordDict.ContainsWord(word))
            {
                return new[] { word };
            }

            var candicates = this.GetKnownWords(this.GetEdits1(word));
            if (candicates.IsNotEmpty())
            {
                return candicates.OrderByDescending(c => WordDict.GetFreqOrDefault(c));
            }

            candicates.UnionWith(this.GetKnownEdits2(word));
            return candicates.OrderByDescending(c => WordDict.GetFreqOrDefault(c));
        }
    }

    class WordSplit
    {
        public string Left { get; set; }

        public string Right { get; set; }
    }
}
