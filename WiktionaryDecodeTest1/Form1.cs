using FlatSharp;
using Newtonsoft.Json;
using System.Diagnostics;
using Wiktionary;

namespace WiktionaryDecodeTest1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        void Serialize(string fn, object? obj)
        {
            using (StreamWriter file = File.CreateText(fn))
            {
                JsonSerializer serializer = new JsonSerializer
                {
                    MissingMemberHandling = MissingMemberHandling.Error,
                    NullValueHandling = NullValueHandling.Ignore
                };
                serializer.Serialize(file, obj);
            }

        }

        void Serialize<T>(string fn, ISerializer<T> serializer, T obj) where T : class
        {
            int maxSize = Senses.Serializer.GetMaxSize(obj);

            byte[] buffer = new byte[maxSize];
            int bytesWritten = Senses.Serializer.Write(buffer, obj);

            Debug.WriteLine(bytesWritten);
            File.WriteAllBytes(fn, buffer);
        }


        void Execute()
        {
            /*var res =*/
            WiktionaryProcessor.ReadWiktionaryAsync(@"E:\WiktionaryEnglishResult.bin", true);

            //Serialize(@"E:\WiktionaryEnglishSenses.json", res.senses);
            //Serialize(@"F:\WiktionaryEnglishQuotes.json", res.quotes);
            //Serialize(@"F:\WiktionaryEnglishExamples.json", res.examples);

            Debug.WriteLine("Complete!");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Execute();
        }
    }
}