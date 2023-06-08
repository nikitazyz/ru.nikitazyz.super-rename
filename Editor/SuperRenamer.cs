using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SuperRename
{
    public class SuperRenamer
    {
        private readonly IRenameRule[] _rules;

        public string Pattern { get; set; }

        public bool UseRegex { get; set; }

        public bool CaseSensitive { get; set; }
        public bool MatchAll { get; set; }

        public string RenameTo { get; set; }

        public SuperRenamer()
        {
            _rules = new IRenameRule[]
            {
                new EnumerationRule()
            };
        }

        public string Rename(int index, string name)
        {
            return Rename(index, name, Pattern, RenameTo, UseRegex, CaseSensitive, MatchAll, _rules);
        }

        public static string Rename(int index, string name, string pattern, string renameTo, bool useRegex, bool caseSensitive, bool matchAll, IRenameRule[] rules)
        {
            renameTo ??= string.Empty;
            renameTo = ProcessRenameTo(index, renameTo, rules);
            if (string.IsNullOrEmpty(pattern))
            {
                return renameTo;
            }
            return useRegex ? RenameWithRegex(name, pattern, renameTo, caseSensitive, matchAll) : RenameWithoutRegex(name, pattern, renameTo, caseSensitive, matchAll);
        }

        public static bool IsPatternValid(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return true;
            }
            try
            {
                Regex.Match("", pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }
        
        private static string RenameWithRegex(string name, string pattern, string renameTo, bool caseSensitive, bool matchAll)
        {
            Match match = Regex.Match(name, pattern, caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase);
        
            while (match.Success)
            {
                Debug.Log(match.Value);
                name = name.Substring(0, match.Index) + match.Result(renameTo) + name.Substring(match.Index + match.Length);
                Debug.Log(name);
                if (matchAll)
                {
                    break;
                }
                match = match.NextMatch();
            }

            return name;
        }
        
        private static string RenameWithoutRegex(string name, string pattern, string renameTo, bool caseSensitive, bool matchAll)
        {
            return Replace(name, pattern, renameTo, caseSensitive, matchAll);
        }

        private static string Replace(string original, string oldValue, string newValue, bool caseSensitive,
            bool matchAll)
        {
            do
            {
                var index = original.IndexOf(oldValue,
                    caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    break;
                }

                original = original.Substring(0, index) + newValue + original.Substring(index + oldValue.Length);
                if (!matchAll)
                {
                    break;
                }
            } while (true);

            return original;
        }

        private static string ProcessRenameTo(int index, string renameTo, IRenameRule[] rules)
        {
            StringBuilder stringBuilder = new StringBuilder();
            for (var i = 0; i < renameTo.Length; i++)
            {
                var c = renameTo[i];

                if (c == '$')
                {
                    string result = null;
                    string sub = null;
                    
                    for (int j = renameTo.Length-1; j > i; j--)
                    {
                        sub = renameTo.Substring(i + 1, j-i);
                        string res = null;
                        if (rules.Any(r => r.ProcessRule(index, sub, out res)))
                        {
                            result = res;
                            break;
                        }
                    }

                    if (result != null)
                    {
                        stringBuilder.Append(result);
                        i += sub.Length;
                        continue;
                    }
                }
                
                stringBuilder.Append(c);
            }

            return stringBuilder.ToString();
        }
        
    }

    public interface IRenameRule
    {
        public bool ProcessRule(int index, string part, out string result);
    }
    
    public class EnumerationRule : IRenameRule
    {
        public bool ProcessRule(int index, string part, out string result)
        {
            var match = Regex.Match(part, @"^i([+-]\d+)?$");
            result = "";
            if (!match.Success)
            {
                return false;
            }

            if (match.Groups.Count > 0 && !string.IsNullOrEmpty(match.Groups[1].Value))
            {
                int offset = int.Parse(match.Groups[1].Value);
                index += offset;
            }

            result = index.ToString();
            return true;
        }
    }
}