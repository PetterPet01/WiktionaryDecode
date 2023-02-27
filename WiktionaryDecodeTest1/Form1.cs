namespace WiktionaryDecodeTest1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            WiktionaryProcessor.ReadWiktionary(@"F:\enwiktionary-20230220-pages-articles-multistream.xml");
        }
    }
}