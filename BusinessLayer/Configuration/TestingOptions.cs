namespace BusinessLayer.Configuration
{
    public class TestingOptions
    {
        static public TestingOptions Instance = new TestingOptions();

        public string Immitate { get; set; }

        public bool HasImmitate(string option)
        {
            if (string.IsNullOrEmpty(Immitate)) return false;

            return Immitate.IndexOf(option) >= 0;
        }
    }
}