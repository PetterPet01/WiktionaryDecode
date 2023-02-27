using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WiktionaryDecodeTest1
{
    public static class Helper
    {
        public static bool StartsWith(this string str, IEnumerable<string> values)
        {
            foreach (string value in values)
                if (str.StartsWith(value))
                    return true;

            return false;
        }
    }

    public class Sense
    {
        public string? senseId { get; set; }
        public string word { get; set; }
        public string gloss { get; set; }
        public string pos { get; set; }
        public List<string> tags { get; set; }
        public List<string>? examples { get; set; }
        public List<KeyValuePair<string, List<string>>>? quotations { get; set; }
        public List<string> synonyms { get; set; }
        public int depth { get; set; }

        public Sense(string word, string gloss, string pos,
            List<string> tags, List<string> examples,
            List<KeyValuePair<string, List<string>>> quotations, List<string> synonyms,
            int depth)
        {
            this.word = word;
            this.gloss = gloss;
            this.pos = pos;
            this.tags = tags;
            this.examples = examples;
            this.quotations = quotations;
            this.synonyms = synonyms;
            this.depth = depth;
        }
    }

    public class Quotation
    {
        public string sentence { get; set; }
        public string sentenceId { get; set; }
        public List<string> attributes { get; set; }

        public Quotation(string sentence, string sentenceId, List<string> attributes)
        {
            this.sentence = sentence;
            this.sentenceId = sentenceId;
            this.attributes = attributes;
        }
    }

    public class Example
    {
        public string sentence { get; set; }
        public string sentenceId { get; set; }

        public Example(string sentence, string sentenceId)
        {
            this.sentence = sentence;
            this.sentenceId = sentenceId;
        }
    }

    internal static class WiktionaryProcessor
    {
        static readonly string[] PARTS_OF_SPEECH = new string[] { "noun", "verb", "adjective", "adverb", "proper noun" };
        static readonly int CHAR_THRESHOLD = 9;
        static readonly double MIN_MENTION_RATIO = 0.5;
        static string lcs(string one, string two, bool ignoreCase = false)
        {
            return LongestCommonSubstring.GetLongestCommonSubsequence(one, two, ignoreCase);
        }

        static Regex rgx1 = new Regex(@"{{.*?}}", RegexOptions.Compiled);
        static Regex rgx2 = new Regex(@"\[\[.*?\]\]", RegexOptions.Compiled);
        static Regex rgx3 = new Regex(@"\'\'\'.*?\'\'\'", RegexOptions.Compiled);
        static string CleanText(string text, string matchSense = "")
        {
            text = Regex.Replace(text, @"\[\[Category:.*?\]\]", "");
            text = Regex.Replace(text, @"\[\[File:.*?\]\]", "");
            text = Regex.Replace(text, @"/ :*? \'\'\'Usage.*$", "");

            text = Regex.Replace(text, @"&lt;", "<");
            text = Regex.Replace(text, @"&gt;", ">");
            text = Regex.Replace(text, @"&amp;", "&");
            text = Regex.Replace(text, @"&nbsp;|&emsp;", " ");
            text = Regex.Replace(text, @"&hellip;", "...");
            text = Regex.Replace(text, @"<math>|</math>|<sup>|</sup>", "");
            text = Regex.Replace(text, @"\\forall", "∀");
            text = Regex.Replace(text, @"\\exists", "∃");
            text = Regex.Replace(text, @"\\pi", "π");
            text = Regex.Replace(text, @"\\dot", ".");
            text = Regex.Replace(text, @"<br/?>", "/ ");


            foreach (Match m in rgx1.Matches(text))
            {
                string value = m.Groups[0].Value;
                value = Regex.Replace(value, @"{{|}}", "");
                value = "(" + value.Trim().Split('|').Last() + ")";
                text = rgx1.Replace(text, value, 1);
            }

            foreach (Match m in rgx2.Matches(text))
            {
                string value = m.Groups[0].Value;
                value = Regex.Replace(value, @"\[\[|\]\]", "");
                value = "" + value.Trim().Split('|').Last() + "";
                if (value == "\\") value = "\\\\";
                text = rgx2.Replace(text, value, 1);
            }

            text = Regex.Replace(text, @"\[\[|\]\]", "");

            text = Regex.Replace(text, "@{{|}}", "");

            if (matchSense != "")
            {
                string word = matchSense.Split('.')[0].Replace("_", " ");
                var matches = rgx3.Matches(text);
                foreach (Match m in matches)
                {
                    string value = m.Groups[0].Value;
                    value = Regex.Replace(value, @"\'\'\'", "");

                    if (value.Length > 0 && ((lcs(value, word, true).Length / value.Length) > MIN_MENTION_RATIO))
                        text = rgx3.Replace(text, "<WSD>" + value + "</WSD>", 1);
                    else
                        text = rgx3.Replace(text, value, 1);
                }
            }

            text = Regex.Replace(text, "’", "'");
            text = Regex.Replace(text, @"&quot;", "\"");
            text = Regex.Replace(text, @"(?<!\')\'{2}(?!\')", "\"");
            text = Regex.Replace(text, @"&ldquo;|&rdquo;", "\"");

            text = string.Join(' ', text.Split(' ').Select(t => t.Trim()));

            text = text.Trim();
            if (matchSense != "")
            {
                string context = Regex.Replace(text, @"<WSD>.*?</WSD>", "");
                if (text.Contains("<WSD>") && context.Contains(" "))
                    return text;
                else
                    return "";
            }
            else
                return text;
        }

        static Sense GenerateSense(string word, string pos)
        {
            Sense s = new(
                word: word,
                gloss: "",
                pos: pos,
                tags: new List<string>(),
                examples: new List<string>(),
                quotations: new List<KeyValuePair<string, List<string>>>(),
                synonyms: new List<string>(),
                depth: -1
            );
            return s;
        }

        static (string? gloss, int depth, List<string>? tags) ProcessGloss(string line)
        {
            int depth = line.Length - line.TrimStart('#').Length;
            line = line.Trim('#').Trim();

            string gloss = Regex.Replace(line, @"{{lb.*?}}", "").Trim();
            gloss = Regex.Replace(gloss, @"&lt;!--.*?--&gt;", "").Trim();
            gloss = CleanText(gloss);

            if (gloss.Length == 0 || Regex.Replace(gloss.Trim(), @"\(.*?\)\.?", "").Length == 0)
                return (null, -1, null);

            List<string>? tags = null;
            Match t = Regex.Match(line, "{{lb(.*?)}}");
            if (t.Success)
                tags = t.Groups[1].Value.Trim().Split('|').Skip(1).ToList();

            return (gloss, depth, tags);
        }

        static string? ProcessExample(string line)
        {
            string ex = Regex.Replace(line, @"#*?: {{ux", "");
            ex = Regex.Replace(ex, @"}}", "").Trim().Split('|').Last();

            if (ex.Length > CHAR_THRESHOLD && ex.Contains(' '))
                return ex;
            else
                return null;
        }

        static IEnumerable<string> ProcessSynonym(string line)
        {
            string synStr = Regex.Replace(line, @"#*?: {{syn", "");
            IEnumerable<string> syn = Regex.Replace(synStr, "}}", "").Split('|').Skip(1);
            if (syn.First() == "en")
                syn = syn.Skip(1);
            syn = syn.Where(s => !s.Contains("Thesaurus:"));

            return syn;
        }

        static (string? q, IEnumerable<string>? qTags) ProcessQuotation(string line2)
        {
            string[] quoteFlags = new string[] { "passage=", "text=" };
            string line = Regex.Replace(line2, @"#*?\*:?", "");

            if (line.ToLower().Contains("seemorecites"))
                return (null, null);

            string q;
            IEnumerable<string> qTags;
            if (line.Contains("|| QUOTE="))
            {
                IEnumerable<string> qTemp = line.Split("|| QUOTE=");
                qTags = new string[1] { qTemp.First() };
                if (qTemp.Count() > 2)
                    q = string.Join("/ ", qTemp.Skip(1));
                else
                    q = qTemp.ElementAt(1);

                if (Regex.IsMatch(q, @"{{.*?}}"))
                {
                    q = Regex.Replace(q, @"{{|}}", "");
                    q = q.Split('|').Last();
                    q = Regex.Replace(q, @"passage=|text=", "");
                }
            }
            else if (Regex.IsMatch(line, @"&lt;ref&gt;.*?&lt;/ref&gt;"))
            {
                qTags = new string[1] { Regex.Match(line, @"&lt;ref&gt;(.*?)&lt;/ref&gt;").Groups[1].Value };
                q = Regex.Replace(line, @"&lt;ref&gt;.*?&lt;/ref&gt;", "");
            }
            else if (Regex.IsMatch(line, "{{.*?}}"))
            {
                qTags = new string[1] { Regex.Replace(line, "{{|}}", "") };
                qTags = qTags.First().Trim().Split("|").Select(t => t.Trim());

                IEnumerable<string> qTemp = qTags.Where(t => t.ToLower().StartsWith(quoteFlags));
                if (qTemp.Count() > 0)
                {
                    q = Regex.Replace(qTemp.First(), @"passage=|text=", "");
                    qTags = qTags.Where(t => !t.ToLower().StartsWith(quoteFlags));
                }
                else
                {
                    qTemp = qTags.Where(t => !Regex.IsMatch(t, "^.*?="));
                    if (qTemp.Count() > 0)
                    {
                        q = qTemp.Last();
                        qTags = qTags.Where(t => t != q);
                    }
                    else
                        return (null, null);
                }
            }

            else
            {
                line = Regex.Replace(line, @"(?<!\')\'{2}(?!\')", "\"");
                if (Regex.IsMatch(line, "\".*?\""))
                {
                    q = Regex.Match(line, "\".*?\"").Groups[0].Value;
                    q = Regex.Replace(q, "\"", "");
                    qTags = new string[1] { Regex.Replace(line, "\".*?\"", "") };
                }
                else
                {
                    q = line;
                    qTags = new string[0];
                }
            }

            if (q.Count() > CHAR_THRESHOLD && q.Contains(' '))
            {
                return (q, qTags);
            }
            else
                return (null, null);
        }

        static List<string> CompressLines(IEnumerable<string> lines)
        {
            List<string> compressed = new List<string>();
            foreach (string line in lines)
            {
                if (line.StartsWith('#'))
                {
                    if (Regex.IsMatch(line, @"#*?\*:"))
                    {
                        if (compressed.Count > 0)
                        {
                            string l = Regex.Replace(line, @"#*?\*:", "");
                            l = l.Trim();
                            l = compressed.Last() + " || QUOTE=" + l;
                            compressed[compressed.Count - 1] = l;
                        }
                        else
                            continue;
                    }
                    else
                        compressed.Add(line);
                }
                else
                    if (compressed.Count > 0)
                    compressed[compressed.Count - 1] = compressed[compressed.Count - 1] + line;
            }

            return compressed;
        }

        static Sense? ProcessSense(IEnumerable<string> lines, string word, string pos)
        {
            Sense sense = GenerateSense(word, pos);

            string firstLine = lines.First();
            (string? gloss, int depth, List<string>? tags) = ProcessGloss(firstLine);
            if (gloss == null) return null;

            sense.gloss = gloss;
            sense.depth = depth;
            if (tags != null)
                sense.tags.AddRange(tags);

            lines = CompressLines(lines.Skip(1));

            foreach (string line in lines)
            {
                if (Regex.IsMatch(line, @"#*?: {{ux"))
                {
                    string? ex = ProcessExample(line);
                    if (ex != null)
                        sense.examples!.Add(ex);
                }
                else if (Regex.IsMatch(line, @"#*?: {{syn"))
                {
                    IEnumerable<string> syn = ProcessSynonym(line);
                    sense.synonyms.AddRange(syn);
                }
                else if (Regex.IsMatch(line, @"#*?\* "))
                {
                    (string? q, IEnumerable<string>? qTags) = ProcessQuotation(line);
                    if (q != null)
                        sense.quotations!.Add(new KeyValuePair<string, List<string>>(q, qTags!.ToList()));
                }
            }

            return sense;
        }

        static List<Sense> ProcessPOS(IEnumerable<string> lines, string word, string pos)
        {
            List<Sense> senses = new List<Sense>();

            bool inSense = false;
            List<string> senseLines = new List<string>();
            foreach (string line in lines)
            {
                if (Regex.IsMatch(line, @"^#* "))
                {
                    if (inSense)
                        if (senseLines.Count > 0)
                        {
                            Sense? sense = ProcessSense(senseLines, word, pos);
                            if (sense != null)
                                senses.Add(sense);
                        }
                    senseLines = new List<string> { line };
                    inSense = true;
                }
                else if (inSense)
                    senseLines.Add(line);
            }

            if (senseLines.Count > 0)
            {
                Sense? sense = ProcessSense(senseLines, word, pos);
                if (sense != null) senses.Add(sense);
            }

            return senses;
        }

        static List<Sense>? ProcessLanguage(string word, IEnumerable<string> lines)
        {
            List<Sense> senses = new List<Sense>();
            string pos = "";
            List<string> posLines = new List<string>();

            foreach (string line in lines)
                if (line.StartsWith('='))
                {
                    if (PARTS_OF_SPEECH.Contains(line.Trim('=').ToLower()))
                    {
                        if (PARTS_OF_SPEECH.Contains(pos))
                        {
                            senses.AddRange(ProcessPOS(posLines, word, pos));
                            posLines = new List<string>();
                        }
                        pos = line.Trim('=').ToLower();
                    }
                    else if (PARTS_OF_SPEECH.Contains(pos))
                    {
                        senses.AddRange(ProcessPOS(posLines, word, pos));
                        posLines = new List<string>();
                        pos = "";
                    }
                }
                else if (pos != "")
                {
                    posLines.Add(line);
                }

            if (posLines.Count > 0)
                senses.AddRange(ProcessPOS(posLines, word, pos));

            if (senses.Count > 0)
                return senses;
            else
                return null;
        }

        static int page = 0;

        static List<Sense>? ProcessPage(IEnumerable<string> lines)
        {
            if (++page % 10000 == 0)
                Debug.WriteLine(page);

            List<Sense> senses = new List<Sense>();

            string? title = lines.Where(line => line.StartsWith("<title>")).Select(line => line.Replace("<title>", "").Replace("</title>", "")).FirstOrDefault();

            if (title == null || Regex.IsMatch(title, @"^\w*?:")) return null;

            List<string> tmpLines = new List<string>();
            foreach (string line in lines)
            {
                string tmpLine = Regex.Replace(line, @"<.*?>.*?</.*?>", "");
                tmpLine = Regex.Replace(tmpLine, @"<.*?>", "");
                tmpLine = tmpLine.Trim();
                if (tmpLine.Length != 0) tmpLines.Add(tmpLine);
            }
            if (tmpLines.Count == 0) return null;
            else lines = tmpLines;

            List<Sense>? tmpLanguages;
            int langs_count = lines.Count(line => Regex.IsMatch(line, @"^==[^=]*?==$"));

            if (langs_count > 0)
            {
                bool inLang = false;
                List<string> langLines = new List<string>();
                string lang = "";
                foreach (string line in lines)
                {
                    if (Regex.IsMatch(line, @"^==[^=]*?==$"))
                    {
                        lang = line.Replace("==", "");
                        langLines = new List<string>();
                        if (lang == "English") inLang = true;
                        else inLang = false;
                    }
                    else if (inLang)
                        if (line == "----")
                        {
                            tmpLanguages = ProcessLanguage(title, langLines);
                            if (tmpLanguages != null)
                                senses.AddRange(tmpLanguages);
                            inLang = false;
                            langLines = new List<string>();
                        }
                        else
                            langLines.Add(line);
                }

                if (inLang)
                {
                    tmpLanguages = ProcessLanguage(title, langLines);
                    if (tmpLanguages != null)
                        senses.AddRange(tmpLanguages);
                }
            }
            else
            {
                tmpLanguages = ProcessLanguage(title, lines);
                if (tmpLanguages != null)
                    senses.AddRange(tmpLanguages);
            }

            if (senses.Count > 0)
                return senses;
            else
                return null;
        }

        static string GenerateWordKey(Sense sense)
        {
            if (sense.pos == "proper noun")
                return string.Format("{0}.{1}", sense.word.ToLower().Replace(" ", "_"), "noun");
            else
                return string.Format("{0}.{1}", sense.word.ToLower().Replace(" ", "_"), sense.pos);
        }

        public static (List<Sense> senses, List<Quotation> quotes, List<Example> examples) PostProcessing(List<Sense> senses)
        {
            List<Quotation> quotes = new List<Quotation>();
            List<Example> examples = new List<Example>();

            IEnumerable<string> wordKeys = senses.Select(s => GenerateWordKey(s)).Distinct();
            Dictionary<string, int> wordIdxs = new Dictionary<string, int>();
            foreach (string w in wordKeys)
                wordIdxs.Add(w, 0);

            foreach (Sense s in senses)
            {
                string wordK = GenerateWordKey(s);
                string sId = string.Format("{0}.{1}", wordK, wordIdxs[wordK]);
                wordIdxs[wordK] += 1;

                s.senseId = sId;

                if (s.quotations!.Count > 0)
                {
                    foreach ((string x, List<string> attrib) in s.quotations)
                    {
                        string sent = CleanText(x, matchSense: sId);
                        if (sent.Length > 0)
                        {
                            quotes.Add(new Quotation
                            (
                                sentence: sent,
                                sentenceId: sId,
                                attributes: attrib
                            ));
                        }
                    }
                }

                if (s.examples!.Count > 0)
                    foreach (string x in s.examples)
                    {
                        string sent = CleanText(x, matchSense: sId);
                        if (sent.Length > 0)
                        {
                            examples.Add(new Example
                                (
                                    sentence: sent,
                                    sentenceId: sId
                                ));
                        }
                    }

                s.quotations = null;
                s.examples = null;
            }

            return (senses, quotes, examples);
        }

        public static (List<Sense> senses, List<Quotation> quotes, List<Example> examples) ReadWiktionary(string filename, bool processed = false)
        {
            IEnumerable<string> f = File.ReadLines(filename);

            List<string> currPages = new List<string>();
            bool isPage = false;

            List<Sense> senses = new List<Sense>();

            if (processed)
            {
                using (StreamReader file = File.OpenText(@"E:\WiktionaryEnglishResult.json"))
                {
                    JsonSerializer serializer = new JsonSerializer
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    };
                    senses = (List<Sense>)serializer.Deserialize(file, typeof(List<Sense>))!;
                }

                Debug.WriteLine("DESERIALIZED!");
            }
            else
            {
                foreach (string l in f)
                {
                    string line = l.Trim();
                    if (line == "<page>")
                        isPage = true;
                    else if (line == "</page>")
                    {
                        List<Sense>? s = ProcessPage(currPages);
                        if (s != null) senses.AddRange(s);
                        isPage = false;
                        currPages = new List<string>();
                    }
                    else
                        if (isPage & line.Length > 0) currPages.Add(line);
                }

                using (StreamWriter file = File.CreateText(@"E:\WiktionaryEnglishResult.json"))
                {
                    JsonSerializer serializer = new JsonSerializer
                    {
                        MissingMemberHandling = MissingMemberHandling.Error,
                        NullValueHandling = NullValueHandling.Ignore
                    };
                    serializer.Serialize(file, senses);
                }

                Debug.WriteLine("SERIALIZED!");
            }

            return PostProcessing(senses);
        }
    }
}
